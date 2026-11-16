#include "WaferView.h"
#include "BoxRenderer.h"

#include <QMatrix4x4>
#include <QOpenGLFramebufferObject>
#include <QOpenGLFramebufferObjectFormat>
#include <QQuickOpenGLUtils>
#include <QVector3D>
#include <QtMath>
#include <QDebug>
#include <algorithm>
#include <cmath>

namespace {
class WaferRenderer : public QQuickFramebufferObject::Renderer
{
public:
    WaferRenderer() { if (!m_boxes.initialize()) qWarning() << "Box renderer:" << m_boxes.error(); }

    QOpenGLFramebufferObject *createFramebufferObject(const QSize &size) override
    {
        QOpenGLFramebufferObjectFormat format;
        format.setAttachment(QOpenGLFramebufferObject::CombinedDepthStencil);
        format.setSamples(4);
        return new QOpenGLFramebufferObject(size, format);
    }

    void synchronize(QQuickFramebufferObject *item) override
    {
        auto *view = static_cast<WaferView *>(item);
        m_yaw = float(view->yaw()); m_pitch = float(view->pitch());
        m_distance = float(view->distance());
        m_panX = float(view->panX()); m_panY = float(view->panY());
        // A different SceneViewModel may have the same numeric revision.
        if (view->scene() != m_scene || view->sceneRevision() != m_revision) {
            m_scene = view->scene(); m_revision = view->sceneRevision();
            m_boxes.setBoxes(view->boxSnapshot());
        }
    }

    void render() override
    {
        const QSize size = framebufferObject()->size();
        const float yaw = qDegreesToRadians(m_yaw), pitch = qDegreesToRadians(m_pitch);
        const QVector3D direction(std::cos(pitch) * std::sin(yaw), -std::cos(pitch) * std::cos(yaw), std::sin(pitch));
        const QVector3D up(0,0,1);
        const QVector3D right = QVector3D::crossProduct(-direction, up).normalized();
        const QVector3D cameraUp = QVector3D::crossProduct(right, -direction).normalized();
        const QVector3D target = QVector3D(6,2,2.4f) + right * m_panX + cameraUp * m_panY;
        const float aspect = float(size.width()) / float(std::max(1, size.height()));
        const float fit = std::max(1.0f, 1.5f / std::max(0.2f, aspect));
        QMatrix4x4 projection, view;
        projection.perspective(35, aspect, 0.1f, 400);
        view.lookAt(target + direction * (m_distance * fit), target, up);
        m_boxes.render(view, projection, size);
        QQuickOpenGLUtils::resetOpenGLState();
    }
private:
    BoxRenderer m_boxes;
    const SceneViewModel *m_scene = nullptr;
    int m_revision = -2;
    float m_yaw = 0, m_pitch = 0, m_distance = 17, m_panX = 0, m_panY = 0;
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
