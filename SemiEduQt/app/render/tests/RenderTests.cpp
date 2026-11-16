#include "BoxRenderer.h"

#include <QElapsedTimer>
#include <QFile>
#include <QGuiApplication>
#include <QImage>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QOffscreenSurface>
#include <QOpenGLContext>
#include <QOpenGLFramebufferObject>
#include <QOpenGLFunctions>
#include <QSurfaceFormat>
#include <algorithm>
#include <cmath>
#include <iostream>
#include <limits>
#include <stdexcept>

namespace {
void require(bool value, const char *message) { if (!value) throw std::runtime_error(message); }
void appendBox(QVector<float> &out, float x, float y, float z, float width,
               float r, float g, float b, float alpha, float outline = 1)
{
    out.append({x,x+width,y,y+width,z,z+width,r,g,b,alpha,outline});
}
}

int main(int argc, char **argv)
{
    QSurfaceFormat format;
    format.setVersion(3,3); format.setProfile(QSurfaceFormat::CoreProfile);
    format.setDepthBufferSize(24); format.setStencilBufferSize(8);
    QSurfaceFormat::setDefaultFormat(format);
    QGuiApplication app(argc, argv);
    try {
        QMatrix4x4 identity;
        require(BoxRenderer::intersectsFrustum(identity, {-0.2f,-0.2f,-0.2f}, {0.2f,0.2f,0.2f}), "Inside AABB was culled");
        require(BoxRenderer::intersectsFrustum(identity, {1,-0.2f,-0.2f}, {2,0.2f,0.2f}), "Boundary AABB was culled");
        require(BoxRenderer::intersectsFrustum(identity, {-3,-3,-3}, {3,3,3}), "AABB enclosing camera was culled");
        for (int axis = 0; axis < 3; ++axis) {
            QVector3D low(-0.1f,-0.1f,-0.1f), high(0.1f,0.1f,0.1f);
            low[axis] = 2; high[axis] = 3;
            require(!BoxRenderer::intersectsFrustum(identity, low, high), "Positive outside AABB was retained");
            low[axis] = -3; high[axis] = -2;
            require(!BoxRenderer::intersectsFrustum(identity, low, high), "Negative outside AABB was retained");
        }

        QOpenGLContext context;
        context.setFormat(format);
        require(context.create(), "OpenGL context unavailable");
        QOffscreenSurface surface;
        surface.setFormat(context.format()); surface.create();
        require(surface.isValid() && context.makeCurrent(&surface), "Cannot activate offscreen OpenGL surface");
        QOpenGLFunctions *gl = context.functions();
        QOpenGLFramebufferObjectFormat fboFormat;
        fboFormat.setAttachment(QOpenGLFramebufferObject::CombinedDepthStencil);
        QOpenGLFramebufferObject fbo(QSize(512,512), fboFormat);
        require(fbo.isValid() && fbo.bind(), "Cannot create render target");
        BoxRenderer renderer;
        require(renderer.initialize(), qPrintable(renderer.error()));
        QMatrix4x4 projection, view;
        projection.perspective(45, 1, 0.1f, 100);
        view.lookAt({0,0,8}, {0,0,0}, {0,1,0});
        auto render = [&] {
            require(fbo.bind(), "Lost render target");
            renderer.render(view, projection, fbo.size());
            gl->glFinish();
            require(gl->glGetError() == GL_NO_ERROR, "OpenGL error during rendering");
        };

        QVector<float> boxes;
        appendBox(boxes, -1,-1,-1,2, 1,0,0,1);
        appendBox(boxes, 1000,0,0,2, 0,1,0,1);
        renderer.setBoxes(boxes); render();
        require(renderer.stats().visibleBoxes == 1, "Frustum culling failed");
        require(renderer.stats().instanceUploadBytes == 80, "Expected 40-byte face + outline instances");
        require(renderer.stats().drawCalls == 3, "Expected grid, opaque and outlines");
        const auto redPixel = fbo.toImage().pixelColor(256,256);
        require(redPixel.red() > 100 && redPixel.blue() < 30, "Instanced cube is not visible at the expected position");
        render();
        require(renderer.stats().instanceUploadBytes == 0 && renderer.stats().selectionUpdates == 0,
                "Unchanged frame rebuilt selection or uploaded data");
        view.setToIdentity(); view.lookAt({0.01f,0,8}, {0,0,0}, {0,1,0}); render();
        require(renderer.stats().instanceUploadBytes == 0, "Camera movement with unchanged visible IDs uploaded data");
        require(renderer.stats().bufferAllocations == 0, "Camera movement reallocated GPU memory");

        boxes[6] = 0; boxes[7] = 1;
        renderer.setBoxes(boxes); render();
        require(renderer.stats().bufferAllocations == 0 && renderer.stats().instanceUploadBytes == 80,
                "Scene update did not reuse allocated instance memory");
        require(fbo.toImage().pixelColor(256,256).green() > 100, "Color-only scene change was not uploaded");

        boxes.clear();
        // Submit the near blue box BEFORE the far red box: the renderer must reorder them.
        appendBox(boxes, -1,-1,1, 2, 0,0,1,0.5f,0);
        appendBox(boxes, -1,-1,-2, 2, 1,0,0,0.5f,0);
        renderer.setBoxes(boxes); render();
        const auto frontBlue = fbo.toImage().pixelColor(256,256);
        std::cout << "Transparent front pixel: " << frontBlue.red() << "," << frontBlue.green() << "," << frontBlue.blue()
                  << "; visible=" << renderer.stats().visibleBoxes << "; uploads=" << renderer.stats().instanceUploadBytes << std::endl;
        fbo.toImage().save("render-transparent-check.png");
        require(frontBlue.blue() > frontBlue.red(), "Transparent boxes are not drawn far-to-near");
        render();
        require(renderer.stats().instanceUploadBytes == 0, "Unchanged transparent order was reuploaded");
        view.setToIdentity(); view.lookAt({0,0,-8}, {0,0,0}, {0,1,0}); render();
        const auto frontRed = fbo.toImage().pixelColor(256,256);
        require(frontRed.red() > frontRed.blue(), "Transparent ordering did not change with camera direction");
        require(renderer.stats().instanceUploadBytes == 80, "Only two transparent instances should be uploaded");

        boxes.clear();
        appendBox(boxes, 0,0,0, 1, 1,1,1,1);
        boxes[0] = std::numeric_limits<float>::quiet_NaN();
        renderer.setBoxes(boxes); render();
        require(renderer.stats().rejectedBoxes == 1 && renderer.stats().totalBoxes == 0, "Invalid bounds reached the GPU");
        require(renderer.stats().drawCalls == 1, "Removed scene retained stale instances");
        renderer.setBoxes({}); render();
        require(renderer.stats().visibleBoxes == 0 && renderer.stats().instanceUploadBytes == 0, "Empty scene is not handled");

        QJsonArray measurements;
        for (int count : {1000,10000,50000}) {
            boxes.clear(); boxes.reserve(count * 11);
            const int columns = int(std::ceil(std::sqrt(count)));
            const float spacing = 5.0f / columns;
            for (int i = 0; i < count; ++i) {
                const float x = -2.5f + (i % columns) * spacing;
                const float y = -2.5f + (i / columns) * spacing;
                appendBox(boxes, x,y,0,spacing * 0.65f, 0.2f,0.65f,0.9f, i % 3 == 0 ? 0.5f : 1.0f);
            }
            view.setToIdentity(); view.lookAt({0,0,8}, {0,0,0}, {0,1,0});
            QElapsedTimer timer; timer.start();
            renderer.setBoxes(boxes); render();
            const double firstMs = timer.nsecsElapsed() / 1e6;
            const auto firstStats = renderer.stats();
            require(firstStats.visibleBoxes == count, "Synthetic scene unexpectedly culled boxes");
            require(firstStats.drawCalls == 4, "Draw calls grew with object count");
            require(firstStats.instanceUploadBytes == qint64(count) * 80, "Instance upload cost is not linear");
            timer.restart();
            for (int frame = 0; frame < 10; ++frame) {
                render();
                require(renderer.stats().instanceUploadBytes == 0 && renderer.stats().bufferAllocations == 0,
                        "Steady frame uploaded or allocated instance memory");
            }
            const double stationaryMs = timer.nsecsElapsed() / 1e6 / 10;
            qint64 movingBytes = 0;
            timer.restart();
            for (int frame = 0; frame < 10; ++frame) {
                view.setToIdentity(); view.lookAt({0.02f * (frame + 1),0,8}, {0,0,0}, {0,1,0});
                render(); movingBytes += renderer.stats().instanceUploadBytes;
                require(renderer.stats().bufferAllocations == 0, "Moving frame grew GPU buffers without new objects");
            }
            const double movingMs = timer.nsecsElapsed() / 1e6 / 10;
            measurements.append(QJsonObject{
                {"boxes",count}, {"drawCalls",firstStats.drawCalls},
                {"firstFrameMsIncludingGpuWait",firstMs},
                {"stationaryFrameMsIncludingGpuWait",stationaryMs},
                {"movingFrameMsIncludingGpuWait",movingMs},
                {"initialInstanceBytes",double(firstStats.instanceUploadBytes)},
                {"movingInstanceBytesTotal10Frames",double(movingBytes)},
                {"legacyExpandedFaceAndOutlineBytesCalculated",double(qint64(count) * 60 * 10 * sizeof(float))}
            });
            if (count == 10000) require(fbo.toImage().save("render-stress-10000.png"), "Cannot save render check image");
        }
        QJsonObject report{
            {"passed",true},
            {"gpu",QString::fromLatin1(reinterpret_cast<const char *>(gl->glGetString(GL_RENDERER)))},
            {"version",QString::fromLatin1(reinterpret_cast<const char *>(gl->glGetString(GL_VERSION)))},
            {"viewport","512x512, offscreen, no MSAA; glFinish after each frame"},
            {"note","Synthetic workload, not application FPS. Legacy bytes are calculated, not a baseline timing."},
            {"measurements",measurements}
        };
        QFile output("render-test-results.json");
        require(output.open(QIODevice::WriteOnly), "Cannot write render report");
        output.write(QJsonDocument(report).toJson());
        std::cout << QJsonDocument(report).toJson().constData() << std::endl;
        return 0;
    } catch (const std::exception &e) {
        std::cerr << "Render test failed: " << e.what() << std::endl;
        return 1;
    }
}
