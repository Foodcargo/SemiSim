#pragma once

#include <QPointF>
#include <QPointer>
#include <QQuickFramebufferObject>
#include <QVector>
#include <QtQml/qqmlregistration.h>

#include "viewmodels/ResultViewModels.h"

// 3D wafer cross-section. Draws only the boxes SceneViewModel copied from se_scene_boxes (OpenGL 3.3 core).
class WaferView : public QQuickFramebufferObject
{
    Q_OBJECT
    QML_ELEMENT
    Q_PROPERTY(SceneViewModel *scene READ scene WRITE setScene NOTIFY sceneChanged)
    Q_PROPERTY(qreal yaw READ yaw WRITE setYaw NOTIFY cameraChanged)
    Q_PROPERTY(qreal pitch READ pitch WRITE setPitch NOTIFY cameraChanged)
    Q_PROPERTY(qreal distance READ distance WRITE setDistance NOTIFY cameraChanged)
    // Left-drag behaviour selected from the toolbar: "rotate" or "pan".
    Q_PROPERTY(QString dragMode READ dragMode WRITE setDragMode NOTIFY dragModeChanged)
public:
    explicit WaferView(QQuickItem *parent = nullptr);
    Renderer *createRenderer() const override;

    SceneViewModel *scene() const { return m_scene; }
    void setScene(SceneViewModel *s);
    qreal yaw() const { return m_yaw; }
    qreal pitch() const { return m_pitch; }
    qreal distance() const { return m_dist; }
    qreal panX() const { return m_panX; }
    qreal panY() const { return m_panY; }
    void setYaw(qreal v);
    void setPitch(qreal v);
    void setDistance(qreal v);

    int sceneRevision() const { return m_scene ? m_scene->sceneRevision() : -1; }
    QVector<float> boxSnapshot() const { return m_scene ? m_scene->boxes() : QVector<float>(); }

    QString dragMode() const { return m_dragMode; }
    void setDragMode(const QString &m);

    Q_INVOKABLE void resetCamera();
    Q_INVOKABLE void zoomBy(qreal factor);

signals:
    void sceneChanged();
    void cameraChanged();
    void dragModeChanged();

protected:
    void mousePressEvent(QMouseEvent *e) override;
    void mouseMoveEvent(QMouseEvent *e) override;
    void mouseReleaseEvent(QMouseEvent *e) override;
    void mouseDoubleClickEvent(QMouseEvent *e) override;
    void wheelEvent(QWheelEvent *e) override;

private:
    QPointer<SceneViewModel> m_scene;
    qreal m_yaw = -35, m_pitch = 30, m_dist = 19, m_panX = 0, m_panY = 0;
    QString m_dragMode = QStringLiteral("rotate");
    QPointF m_last;
    Qt::MouseButton m_button = Qt::NoButton;
};
