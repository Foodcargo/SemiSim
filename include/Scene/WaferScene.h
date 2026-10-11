#pragma once

#include <QPoint>
#include <QWidget>

class QMouseEvent;
class QPaintEvent;
class QWheelEvent;
class Wafer;

class WaferScene : public QWidget {
    Q_OBJECT

public:
    explicit WaferScene(QWidget *parent = nullptr);

    void setWafer(Wafer *wafer);

protected:
    void paintEvent(QPaintEvent *event) override;
    void mousePressEvent(QMouseEvent *event) override;
    void mouseMoveEvent(QMouseEvent *event) override;
    void wheelEvent(QWheelEvent *event) override;

private:
    Wafer *m_wafer = nullptr;
    QPoint m_lastMousePosition;
    float m_yawDegrees = -28.0f;
    float m_pitchDegrees = 30.0f;
    float m_zoom = 1.0f;
};
