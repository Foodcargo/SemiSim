#include "Scene/WaferScene.h"

#include "Model/Wafer.h"

#include <QColor>
#include <QMouseEvent>
#include <QPainter>
#include <QPen>
#include <QPointF>
#include <QRectF>
#include <QString>
#include <QVector>
#include <QWheelEvent>
#include <QtMath>
#include <QtGlobal>

namespace {
struct LayoutBox {
    QColor color;
    int step = 0;
    QRectF footprint;
    double bottom = 0.0;
    double height = 0.1;
    double opacity = 0.7;
};

QVector<LayoutBox> demoLayout(const QString &maskPattern)
{
    QVector<LayoutBox> boxes = {
        {QColor("#64748b"), 0, QRectF(-2.6, -1.65, 5.2, 3.3), -0.34, 0.20, 0.34},
        {QColor("#8b5cf6"), 1, QRectF(-2.1, -1.18, 4.2, 2.36), -0.12, 0.14, 0.34},
        {QColor("#22c55e"), 2, QRectF(-1.48, -0.43, 2.96, 0.86), 0.02, 0.14, 0.72},
        {QColor("#ef4444"), 3, QRectF(-0.13, -0.78, 0.26, 1.56), 0.26, 0.26, 0.88},
        {QColor("#f59e0b"), 3, QRectF(-0.24, -0.68, 0.08, 1.36), 0.18, 0.24, 0.58},
        {QColor("#f59e0b"), 3, QRectF(0.16, -0.68, 0.08, 1.36), 0.18, 0.24, 0.58},
        {QColor("#14b8a6"), 4, QRectF(-1.33, -0.38, 0.90, 0.76), 0.16, 0.09, 0.58},
        {QColor("#14b8a6"), 4, QRectF(0.43, -0.38, 0.90, 0.76), 0.16, 0.09, 0.58},
        {QColor("#f97316"), 5, QRectF(-1.30, -0.36, 0.24, 0.24), 0.25, 0.33, 0.86},
        {QColor("#f97316"), 5, QRectF(-1.30, 0.12, 0.24, 0.24), 0.25, 0.33, 0.86},
        {QColor("#f97316"), 5, QRectF(1.06, -0.36, 0.24, 0.24), 0.25, 0.33, 0.86},
        {QColor("#f97316"), 5, QRectF(1.06, 0.12, 0.24, 0.24), 0.25, 0.33, 0.86},
        {QColor("#f97316"), 5, QRectF(-0.12, 0.60, 0.24, 0.24), 0.52, 0.22, 0.86},
        {QColor("#2563eb"), 6, QRectF(-1.39, -0.59, 0.42, 1.18), 0.58, 0.20, 0.80},
        {QColor("#2563eb"), 6, QRectF(0.97, -0.59, 0.42, 1.18), 0.58, 0.20, 0.80},
        {QColor("#2563eb"), 6, QRectF(-0.21, 0.56, 0.42, 0.92), 0.74, 0.20, 0.80},
        {QColor("#06b6d4"), 6, QRectF(-1.30, -0.62, 0.24, 0.24), 0.78, 0.24, 0.88},
        {QColor("#06b6d4"), 6, QRectF(1.06, 0.38, 0.24, 0.24), 0.78, 0.24, 0.88},
        {QColor("#06b6d4"), 6, QRectF(-0.12, 1.20, 0.24, 0.24), 0.94, 0.20, 0.88},
        {QColor("#16a34a"), 6, QRectF(-2.43, -1.30, 2.50, 0.28), 1.02, 0.23, 0.72},
        {QColor("#16a34a"), 6, QRectF(-0.07, 1.02, 2.50, 0.28), 1.02, 0.23, 0.72},
        {QColor("#16a34a"), 6, QRectF(-1.10, 1.21, 2.20, 0.22), 1.14, 0.21, 0.72},
    };

    if (maskPattern == "Two finger gate") {
        boxes[3].footprint = QRectF(-0.46, -0.78, 0.20, 1.56);
        boxes.insert(4, {QColor("#ef4444"), 3, QRectF(0.26, -0.78, 0.20, 1.56), 0.26, 0.26, 0.88});
        boxes.append({QColor("#2563eb"), 6, QRectF(-0.55, 0.88, 1.10, 0.28), 0.74, 0.20, 0.80});
    } else if (maskPattern == "Contact array") {
        for (int x = -1; x <= 1; ++x) {
            for (int y = -1; y <= 1; ++y) {
                boxes.append({QColor("#f97316"), 5, QRectF(x * 0.42 - 0.10, y * 0.36 - 0.10, 0.20, 0.20), 0.54, 0.20, 0.86});
            }
        }
    } else if (maskPattern == "Guard ring") {
        boxes.append({QColor("#22c55e"), 2, QRectF(-2.05, -1.05, 4.10, 0.18), 0.05, 0.10, 0.72});
        boxes.append({QColor("#22c55e"), 2, QRectF(-2.05, 0.87, 4.10, 0.18), 0.05, 0.10, 0.72});
        boxes.append({QColor("#22c55e"), 2, QRectF(-2.05, -0.87, 0.18, 1.74), 0.05, 0.10, 0.72});
        boxes.append({QColor("#22c55e"), 2, QRectF(1.87, -0.87, 0.18, 1.74), 0.05, 0.10, 0.72});
    }

    return boxes;
}

QPointF isoProject(double x, double y, double z, double scale, const QPointF &origin, double yawDegrees)
{
    const double yaw = qDegreesToRadians(yawDegrees);
    const double rx = x * qCos(yaw) - y * qSin(yaw);
    const double ry = x * qSin(yaw) + y * qCos(yaw);
    return QPointF(origin.x() + (rx - ry) * scale, origin.y() + (rx + ry) * scale * 0.46 - z * scale * 1.35);
}

QPolygonF face(const QVector<QPointF> &points)
{
    QPolygonF polygon;
    for (const QPointF &point : points) {
        polygon << point;
    }
    return polygon;
}

void drawBox(QPainter &painter, const LayoutBox &box, double scale, const QPointF &origin, double yawDegrees)
{
    const QRectF r = box.footprint;
    const double left = r.left();
    const double right = r.right();
    const double front = r.top();
    const double back = r.bottom();
    const double bottom = box.bottom;
    const double top = box.bottom + box.height;

    const QPointF lfb = isoProject(left, front, bottom, scale, origin, yawDegrees);
    const QPointF rfb = isoProject(right, front, bottom, scale, origin, yawDegrees);
    const QPointF rbb = isoProject(right, back, bottom, scale, origin, yawDegrees);
    const QPointF lbb = isoProject(left, back, bottom, scale, origin, yawDegrees);
    const QPointF lft = isoProject(left, front, top, scale, origin, yawDegrees);
    const QPointF rft = isoProject(right, front, top, scale, origin, yawDegrees);
    const QPointF rbt = isoProject(right, back, top, scale, origin, yawDegrees);
    const QPointF lbt = isoProject(left, back, top, scale, origin, yawDegrees);

    QColor side = box.color.darker(122);
    QColor rightSide = box.color.darker(145);
    QColor topColor = box.color.lighter(118);
    side.setAlphaF(box.opacity);
    rightSide.setAlphaF(box.opacity);
    topColor.setAlphaF(box.opacity);

    painter.setPen(QPen(box.color.lighter(135), 1.1));
    painter.setBrush(side);
    painter.drawPolygon(face({lfb, rfb, rft, lft}));
    painter.setBrush(rightSide);
    painter.drawPolygon(face({rfb, rbb, rbt, rft}));
    painter.setBrush(topColor);
    painter.drawPolygon(face({lft, rft, rbt, lbt}));
}
}

WaferScene::WaferScene(QWidget *parent)
    : QWidget(parent)
{
    setMinimumSize(640, 420);
    setFocusPolicy(Qt::StrongFocus);
}

void WaferScene::setWafer(Wafer *wafer)
{
    if (m_wafer) {
        disconnect(m_wafer, nullptr, this, nullptr);
    }

    m_wafer = wafer;
    if (m_wafer) {
        connect(m_wafer, &Wafer::changed, this, QOverload<>::of(&WaferScene::update));
    }
    update();
}

void WaferScene::paintEvent(QPaintEvent *event)
{
    Q_UNUSED(event)

    QPainter painter(this);
    painter.setRenderHint(QPainter::Antialiasing);
    painter.fillRect(rect(), QColor("#090d12"));

    const double scale = qMin(width() / 8.2, height() / 5.6) * m_zoom;
    const QPointF origin(width() * 0.50, height() * 0.64);

    painter.setPen(QPen(QColor(120, 139, 166, 54), 1.0));
    for (int i = -12; i <= 12; ++i) {
        const double v = i * 0.35;
        painter.drawLine(isoProject(-4.4, v, -0.36, scale, origin, m_yawDegrees),
                         isoProject(4.4, v, -0.36, scale, origin, m_yawDegrees));
        painter.drawLine(isoProject(v, -3.4, -0.36, scale, origin, m_yawDegrees),
                         isoProject(v, 3.4, -0.36, scale, origin, m_yawDegrees));
    }

    const int processStep = m_wafer ? m_wafer->processStep() : 0;
    const QString maskPattern = m_wafer ? m_wafer->maskPattern() : QStringLiteral("Single gate line");
    for (const LayoutBox &box : demoLayout(maskPattern)) {
        if (box.step <= processStep) {
            drawBox(painter, box, scale, origin, m_yawDegrees);
        }
    }

    painter.setPen(QColor("#e5edf7"));
    painter.drawText(QRectF(18, 16, width() - 36, 28), Qt::AlignLeft, "NMOS_X1 transistor process view");
    painter.setPen(QColor("#94a3b8"));
    painter.drawText(QRectF(18, 42, width() - 36, 24), Qt::AlignLeft, "Mask: " + maskPattern);
}

void WaferScene::mousePressEvent(QMouseEvent *event)
{
    m_lastMousePosition = event->pos();
}

void WaferScene::mouseMoveEvent(QMouseEvent *event)
{
    if (!(event->buttons() & Qt::LeftButton)) {
        return;
    }

    const QPoint delta = event->pos() - m_lastMousePosition;
    m_lastMousePosition = event->pos();
    m_yawDegrees += static_cast<float>(delta.x()) * 0.35f;
    update();
}

void WaferScene::wheelEvent(QWheelEvent *event)
{
    const float steps = static_cast<float>(event->angleDelta().y()) / 120.0f;
    m_zoom = qBound(0.65f, m_zoom + steps * 0.08f, 1.8f);
    update();
}
