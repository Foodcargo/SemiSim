#include "WaferView.h"

#include <QMatrix4x4>
#include <QOpenGLBuffer>
#include <QOpenGLExtraFunctions>
#include <QOpenGLFramebufferObject>
#include <QOpenGLFramebufferObjectFormat>
#include <QOpenGLShaderProgram>
#include <QOpenGLVertexArrayObject>
#include <QQuickOpenGLUtils>
#include <QVector3D>
#include <QtMath>
#include <algorithm>
#include <vector>

namespace {

constexpr int kFloatsPerVertex = 10; // pos3 normal3 rgba4
constexpr float kCenterX = 6.0f, kCenterY = 2.0f, kCenterZ = 2.4f;

const char *kVertex = R"(#version 330 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNrm;
layout(location = 2) in vec4 aCol;
uniform mat4 uMvp;
out vec3 vN;
out vec4 vC;
void main() { vN = aNrm; vC = aCol; gl_Position = uMvp * vec4(aPos, 1.0); }
)";

const char *kFragment = R"(#version 330 core
in vec3 vN;
in vec4 vC;
uniform int uUnlit;
out vec4 fragColor;
void main() {
    if (uUnlit == 1) { fragColor = vC; return; }
    vec3 n = normalize(vN);
    vec3 l1 = normalize(vec3(0.45, -0.75, 0.85));
    vec3 l2 = normalize(vec3(-0.6, 0.45, 0.35));
    float d = 0.38 + 0.55 * max(dot(n, l1), 0.0) + 0.22 * max(dot(n, l2), 0.0);
    fragColor = vec4(vC.rgb * min(d, 1.15), vC.a);
}
)";

struct Box
{
    float x0, x1, y0, y1, z0, z1, r, g, b, a, outline;
};

void pushVertex(std::vector<float> &v, float x, float y, float z, const QVector3D &n, float r, float g, float b, float a)
{
    v.insert(v.end(), {x, y, z, n.x(), n.y(), n.z(), r, g, b, a});
}

void pushFaces(std::vector<float> &v, const Box &bx)
{
    const float X0 = bx.x0, X1 = bx.x1, Y0 = bx.y0, Y1 = bx.y1, Z0 = bx.z0, Z1 = bx.z1;
    struct Face { QVector3D n; float p[4][3]; };
    const Face faces[6] = {
        {{-1, 0, 0}, {{X0, Y0, Z0}, {X0, Y1, Z0}, {X0, Y1, Z1}, {X0, Y0, Z1}}},
        {{1, 0, 0}, {{X1, Y0, Z0}, {X1, Y0, Z1}, {X1, Y1, Z1}, {X1, Y1, Z0}}},
        {{0, -1, 0}, {{X0, Y0, Z0}, {X0, Y0, Z1}, {X1, Y0, Z1}, {X1, Y0, Z0}}},
        {{0, 1, 0}, {{X0, Y1, Z0}, {X1, Y1, Z0}, {X1, Y1, Z1}, {X0, Y1, Z1}}},
        {{0, 0, -1}, {{X0, Y0, Z0}, {X1, Y0, Z0}, {X1, Y1, Z0}, {X0, Y1, Z0}}},
        {{0, 0, 1}, {{X0, Y0, Z1}, {X0, Y1, Z1}, {X1, Y1, Z1}, {X1, Y0, Z1}}},
    };
    for (const auto &f : faces) {
        const int idx[6] = {0, 1, 2, 0, 2, 3};
        for (int i : idx) pushVertex(v, f.p[i][0], f.p[i][1], f.p[i][2], f.n, bx.r, bx.g, bx.b, bx.a);
    }
}

void pushEdges(std::vector<float> &v, const Box &bx)
{
    const bool bright = bx.outline > 1.5f;
    const float k = bright ? 1.0f : 0.45f;
    const float r = bx.r * k, g = bx.g * k, b = bx.b * k, a = bright ? 1.0f : std::max(0.6f, bx.a);
    const float xs[2] = {bx.x0, bx.x1}, ys[2] = {bx.y0, bx.y1}, zs[2] = {bx.z0, bx.z1};
    const QVector3D n0(0, 0, 0);
    for (int j = 0; j < 2; ++j)
        for (int k2 = 0; k2 < 2; ++k2) {
            pushVertex(v, xs[0], ys[j], zs[k2], n0, r, g, b, a);
            pushVertex(v, xs[1], ys[j], zs[k2], n0, r, g, b, a);
            pushVertex(v, xs[j], ys[0], zs[k2], n0, r, g, b, a);
            pushVertex(v, xs[j], ys[1], zs[k2], n0, r, g, b, a);
            pushVertex(v, xs[j], ys[k2], zs[0], n0, r, g, b, a);
            pushVertex(v, xs[j], ys[k2], zs[1], n0, r, g, b, a);
        }
}

struct Mesh
{
    QOpenGLVertexArrayObject vao;
    QOpenGLBuffer vbo{QOpenGLBuffer::VertexBuffer};
    int count = 0;
};

class WaferRenderer : public QQuickFramebufferObject::Renderer, protected QOpenGLExtraFunctions
{
public:
    WaferRenderer()
    {
        initializeOpenGLFunctions();
        m_prog.addShaderFromSourceCode(QOpenGLShader::Vertex, kVertex);
        m_prog.addShaderFromSourceCode(QOpenGLShader::Fragment, kFragment);
        m_prog.link();
        for (Mesh *m : {&m_opaque, &m_lines, &m_trans, &m_grid}) {
            m->vao.create();
            m->vao.bind();
            m->vbo.create();
            m->vbo.setUsagePattern(QOpenGLBuffer::DynamicDraw);
            m->vbo.bind();
            glEnableVertexAttribArray(0);
            glEnableVertexAttribArray(1);
            glEnableVertexAttribArray(2);
            const int stride = kFloatsPerVertex * sizeof(float);
            glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, stride, nullptr);
            glVertexAttribPointer(1, 3, GL_FLOAT, GL_FALSE, stride, reinterpret_cast<void *>(3 * sizeof(float)));
            glVertexAttribPointer(2, 4, GL_FLOAT, GL_FALSE, stride, reinterpret_cast<void *>(6 * sizeof(float)));
            m->vao.release();
            m->vbo.release();
        }
        // Reference grid floor under the cross-section (display aid only).
        std::vector<float> grid;
        const QVector3D n0(0, 0, 0);
        const float gz = -0.62f;
        for (int i = -14; i <= 26; ++i) {
            const float a = (i % 5 == 0) ? 0.55f : 0.28f;
            pushVertex(grid, float(i), -14.0f, gz, n0, 0.30f, 0.36f, 0.46f, a);
            pushVertex(grid, float(i), 18.0f, gz, n0, 0.30f, 0.36f, 0.46f, a);
        }
        for (int j = -14; j <= 18; ++j) {
            const float a = (j % 5 == 0) ? 0.55f : 0.28f;
            pushVertex(grid, -14.0f, float(j), gz, n0, 0.30f, 0.36f, 0.46f, a);
            pushVertex(grid, 26.0f, float(j), gz, n0, 0.30f, 0.36f, 0.46f, a);
        }
        upload(m_grid, grid);
    }

    QOpenGLFramebufferObject *createFramebufferObject(const QSize &size) override
    {
        QOpenGLFramebufferObjectFormat f;
        f.setAttachment(QOpenGLFramebufferObject::CombinedDepthStencil);
        f.setSamples(4);
        return new QOpenGLFramebufferObject(size, f);
    }

    void synchronize(QQuickFramebufferObject *item) override
    {
        auto *v = static_cast<WaferView *>(item);
        m_yaw = float(v->yaw());
        m_pitch = float(v->pitch());
        m_dist = float(v->distance());
        m_panX = float(v->panX());
        m_panY = float(v->panY());
        const int rev = v->sceneRevision();
        if (rev != m_rev) {
            m_rev = rev;
            m_data = v->boxSnapshot();
            m_dirty = true;
        }
    }

    void render() override
    {
        if (m_dirty) rebuild();
        const QSize sz = framebufferObject()->size();
        glViewport(0, 0, sz.width(), sz.height());
        glClearColor(0.050f, 0.058f, 0.075f, 1.0f);
        glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT | GL_STENCIL_BUFFER_BIT);

        const float yaw = qDegreesToRadians(m_yaw), pitch = qDegreesToRadians(m_pitch);
        const QVector3D dir(std::cos(pitch) * std::sin(yaw), -std::cos(pitch) * std::cos(yaw), std::sin(pitch));
        const QVector3D up0(0, 0, 1);
        const QVector3D right = QVector3D::crossProduct(-dir, up0).normalized();
        const QVector3D camUp = QVector3D::crossProduct(right, -dir).normalized();
        const QVector3D target = QVector3D(kCenterX, kCenterY, kCenterZ) + right * m_panX + camUp * m_panY;
        const float aspect = float(sz.width()) / float(std::max(1, sz.height()));
        // Keep the 12-unit cross-section width in frame on narrow viewports.
        const float fit = std::max(1.0f, 1.5f / std::max(0.2f, aspect));
        const QVector3D eye = target + dir * (m_dist * fit);
        QMatrix4x4 proj, view;
        proj.perspective(35.0f, aspect, 0.1f, 400.0f);
        view.lookAt(eye, target, up0);
        const QMatrix4x4 mvp = proj * view;
        sortTransparent(eye);

        glEnable(GL_DEPTH_TEST);
        glDepthFunc(GL_LEQUAL);
        glDisable(GL_CULL_FACE);
        m_prog.bind();
        m_prog.setUniformValue("uMvp", mvp);

        glEnable(GL_BLEND);
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
        m_prog.setUniformValue("uUnlit", 1);
        draw(m_grid, GL_LINES);

        glDisable(GL_BLEND);
        glDepthMask(GL_TRUE);
        glEnable(GL_POLYGON_OFFSET_FILL);
        glPolygonOffset(1.0f, 1.0f);
        m_prog.setUniformValue("uUnlit", 0);
        draw(m_opaque, GL_TRIANGLES);
        glDisable(GL_POLYGON_OFFSET_FILL);

        glEnable(GL_BLEND);
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
        m_prog.setUniformValue("uUnlit", 1);
        draw(m_lines, GL_LINES);

        glDepthMask(GL_FALSE);
        m_prog.setUniformValue("uUnlit", 0);
        draw(m_trans, GL_TRIANGLES);
        glDepthMask(GL_TRUE);

        m_prog.release();
        glDisable(GL_BLEND);
        glDisable(GL_DEPTH_TEST);
        QQuickOpenGLUtils::resetOpenGLState();
    }

private:
    void draw(Mesh &m, GLenum mode)
    {
        if (m.count == 0) return;
        m.vao.bind();
        glDrawArrays(mode, 0, m.count);
        m.vao.release();
    }

    void upload(Mesh &m, const std::vector<float> &v)
    {
        m.vbo.bind();
        m.vbo.allocate(v.data(), int(v.size() * sizeof(float)));
        m.vbo.release();
        m.count = int(v.size() / kFloatsPerVertex);
    }

    void rebuild()
    {
        m_dirty = false;
        std::vector<float> opaque, lines;
        m_transBoxes.clear();
        const int n = int(m_data.size() / 11);
        for (int i = 0; i < n; ++i) {
            const float *p = m_data.constData() + i * 11;
            const Box bx{p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10]};
            if (bx.a >= 0.999f) pushFaces(opaque, bx);
            else m_transBoxes.push_back(bx);
            if (bx.outline > 0.5f) pushEdges(lines, bx);
        }
        upload(m_opaque, opaque);
        upload(m_lines, lines);
        m_lastEye = QVector3D(1e9f, 0, 0);
    }

    void sortTransparent(const QVector3D &eye)
    {
        if ((eye - m_lastEye).lengthSquared() < 1e-8f) return;
        m_lastEye = eye;
        std::vector<std::pair<float, int>> order;
        order.reserve(m_transBoxes.size());
        for (int i = 0; i < int(m_transBoxes.size()); ++i) {
            const Box &b = m_transBoxes[i];
            const QVector3D c((b.x0 + b.x1) / 2, (b.y0 + b.y1) / 2, (b.z0 + b.z1) / 2);
            order.emplace_back((c - eye).lengthSquared(), i);
        }
        std::sort(order.begin(), order.end(), [](const auto &a, const auto &b) { return a.first > b.first; });
        std::vector<float> v;
        v.reserve(order.size() * 36 * kFloatsPerVertex);
        for (const auto &o : order) pushFaces(v, m_transBoxes[o.second]);
        upload(m_trans, v);
    }

    QOpenGLShaderProgram m_prog;
    Mesh m_opaque, m_lines, m_trans, m_grid;
    std::vector<Box> m_transBoxes;
    QVector<float> m_data;
    QVector3D m_lastEye;
    int m_rev = -2;
    bool m_dirty = true;
    float m_yaw = 0, m_pitch = 0, m_dist = 17, m_panX = 0, m_panY = 0;
};

} // namespace

WaferView::WaferView(QQuickItem *parent) : QQuickFramebufferObject(parent)
{
    setAcceptedMouseButtons(Qt::AllButtons);
    setFlag(ItemHasContents, true);
    // Qt 6 (RHI/OpenGL) samples the FBO texture bottom-up; flip so +Z (film stack) points up on screen.
    setMirrorVertically(true);
}

QQuickFramebufferObject::Renderer *WaferView::createRenderer() const
{
    return new WaferRenderer();
}

void WaferView::setScene(SceneViewModel *s)
{
    if (s == m_scene) return;
    if (m_scene) disconnect(m_scene, nullptr, this, nullptr);
    m_scene = s;
    if (m_scene) {
        connect(m_scene, &SceneViewModel::sceneChanged, this, &QQuickItem::update);
        connect(m_scene, &SceneViewModel::cameraResetRequested, this, &WaferView::resetCamera);
    }
    emit sceneChanged();
    update();
}

void WaferView::setYaw(qreal v)
{
    m_yaw = std::fmod(v, 360.0);
    emit cameraChanged();
    update();
}

void WaferView::setPitch(qreal v)
{
    m_pitch = std::clamp(v, -5.0, 89.0);
    emit cameraChanged();
    update();
}

void WaferView::setDistance(qreal v)
{
    m_dist = std::clamp(v, 3.0, 80.0);
    emit cameraChanged();
    update();
}

void WaferView::resetCamera()
{
    m_yaw = -35;
    m_pitch = 30;
    m_dist = 19;
    m_panX = m_panY = 0;
    emit cameraChanged();
    update();
}

void WaferView::setDragMode(const QString &m)
{
    if (m == m_dragMode) return;
    m_dragMode = m;
    emit dragModeChanged();
}

void WaferView::zoomBy(qreal factor)
{
    m_dist = std::clamp(m_dist * factor, 3.0, 80.0);
    emit cameraChanged();
    update();
}

void WaferView::mousePressEvent(QMouseEvent *e)
{
    m_last = e->position();
    m_button = e->button();
    e->accept();
}

void WaferView::mouseMoveEvent(QMouseEvent *e)
{
    const QPointF d = e->position() - m_last;
    m_last = e->position();
    if (m_button == Qt::LeftButton && m_dragMode == QStringLiteral("rotate")) {
        m_yaw = std::fmod(m_yaw + d.x() * 0.4, 360.0);
        m_pitch = std::clamp(m_pitch + d.y() * 0.3, -5.0, 89.0);
    } else if (m_button != Qt::NoButton) {
        const qreal k = m_dist / std::max<qreal>(200.0, height());
        m_panX -= d.x() * k;
        m_panY += d.y() * k;
    }
    emit cameraChanged();
    update();
    e->accept();
}

void WaferView::mouseReleaseEvent(QMouseEvent *e)
{
    m_button = Qt::NoButton;
    e->accept();
}

void WaferView::mouseDoubleClickEvent(QMouseEvent *e)
{
    resetCamera();
    e->accept();
}

void WaferView::wheelEvent(QWheelEvent *e)
{
    const qreal steps = e->angleDelta().y() / 120.0;
    m_dist = std::clamp(m_dist * std::pow(0.88, steps), 3.0, 80.0);
    emit cameraChanged();
    update();
    e->accept();
}
