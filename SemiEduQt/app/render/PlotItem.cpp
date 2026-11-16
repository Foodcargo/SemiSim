#include "PlotItem.h"

#include <QPainter>
#include <QPainterPath>
#include <QtMath>
#include <algorithm>
#include <cmath>

namespace {
double niceStep(double range, int target)
{
    if (range <= 0) return 1;
    const double raw = range / target;
    const double mag = std::pow(10.0, std::floor(std::log10(raw)));
    const double f = raw / mag;
    return (f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10) * mag;
}

QString fmt(double v)
{
    if (v == 0) return QStringLiteral("0");
    const double a = std::abs(v);
    if (a >= 1e4 || a < 1e-2) return QString::number(v, 'e', 1);
    return QString::number(v, 'g', 4);
}
} // namespace

PlotItem::PlotItem(QQuickItem *parent) : QQuickPaintedItem(parent)
{
    setAntialiasing(true);
}

void PlotItem::setPlot(PlotViewModel *p)
{
    if (p == m_plot) return;
    if (m_plot) disconnect(m_plot, nullptr, this, nullptr);
    m_plot = p;
    if (m_plot) connect(m_plot, &PlotViewModel::plotChanged, this, [this] { update(); });
    emit plotChanged();
    update();
}

void PlotItem::paint(QPainter *p)
{
    const QRectF all(0, 0, width(), height());
    p->fillRect(all, QColor(0x16, 0x19, 0x1e));
    if (!m_plot || !m_plot->visible()) return;
    const auto pts = m_plot->points();
    if (pts.size() < 2) return;

    const QRectF area(all.left() + 78, all.top() + 30, all.width() - 100, all.height() - 72);
    const bool logY = m_plot->logY();
    auto yv = [logY](double y) { return logY ? std::log10(std::max(std::abs(y), 1e-16)) : y; };

    double xmin = pts.first().x(), xmax = pts.first().x(), ymin = yv(pts.first().y()), ymax = ymin;
    for (const auto &q : pts) {
        xmin = std::min(xmin, q.x());
        xmax = std::max(xmax, q.x());
        ymin = std::min(ymin, yv(q.y()));
        ymax = std::max(ymax, yv(q.y()));
    }
    if (logY) {
        ymin = std::floor(ymin);
        ymax = std::ceil(ymax);
    }
    if (ymax - ymin < 1e-30) { ymax += 1; ymin -= 1; }
    if (xmax - xmin < 1e-30) xmax += 1;
    auto X = [&](double x) { return area.left() + (x - xmin) / (xmax - xmin) * area.width(); };
    auto Y = [&](double y) { return area.bottom() - (y - ymin) / (ymax - ymin) * area.height(); };

    QFont f = p->font();
    f.setPixelSize(11);
    p->setFont(f);
    p->setPen(QPen(QColor(0x2b, 0x32, 0x3c), 1));
    const double xs = niceStep(xmax - xmin, 8);
    for (double x = std::ceil(xmin / xs) * xs; x <= xmax + 1e-12; x += xs) {
        p->drawLine(QPointF(X(x), area.top()), QPointF(X(x), area.bottom()));
        p->setPen(QColor(0xa9, 0xb3, 0xc1));
        p->drawText(QRectF(X(x) - 30, area.bottom() + 4, 60, 14), Qt::AlignHCenter, fmt(x));
        p->setPen(QPen(QColor(0x2b, 0x32, 0x3c), 1));
    }
    const double ys = logY ? std::max(1.0, std::ceil((ymax - ymin) / 10)) : niceStep(ymax - ymin, 6);
    for (double y = std::ceil(ymin / ys) * ys; y <= ymax + 1e-12; y += ys) {
        p->drawLine(QPointF(area.left(), Y(y)), QPointF(area.right(), Y(y)));
        p->setPen(QColor(0xa9, 0xb3, 0xc1));
        p->drawText(QRectF(area.left() - 74, Y(y) - 7, 70, 14), Qt::AlignRight | Qt::AlignVCenter,
                    logY ? QStringLiteral("1e%1").arg(int(std::lround(y))) : fmt(y));
        p->setPen(QPen(QColor(0x2b, 0x32, 0x3c), 1));
    }
    p->setPen(QPen(QColor(0x5a, 0x64, 0x72), 1.2));
    p->drawRect(area);

    QPainterPath path;
    path.moveTo(X(pts.first().x()), Y(yv(pts.first().y())));
    for (int i = 1; i < pts.size(); ++i) path.lineTo(X(pts[i].x()), Y(yv(pts[i].y())));
    p->setPen(QPen(QColor(0x4e, 0xa1, 0xff), 2));
    p->drawPath(path);

    if (m_plot->hasVthExt()) {
        const double vx = m_plot->vthExt();
        if (vx >= xmin && vx <= xmax) {
            QPen dash(QColor(0xff, 0x6b, 0x6b), 1.5, Qt::DashLine);
            p->setPen(dash);
            p->drawLine(QPointF(X(vx), area.top()), QPointF(X(vx), area.bottom()));
            p->drawText(QPointF(X(vx) + 4, area.top() + 14), QStringLiteral("외삽 Vth %1 V").arg(QString::number(vx, 'f', 3)));
        }
    }

    p->setPen(QColor(0xd7, 0xdd, 0xe5));
    QFont tf = f;
    tf.setPixelSize(13);
    tf.setBold(true);
    p->setFont(tf);
    p->drawText(QRectF(area.left(), all.top() + 6, area.width(), 18), Qt::AlignHCenter,
                m_plot->title() + QStringLiteral("  ·  검증되지 않은 교육용 예시 모델"));
    p->setFont(f);
    p->drawText(QRectF(area.left(), area.bottom() + 20, area.width(), 16), Qt::AlignHCenter, m_plot->xLabel());
    p->save();
    p->translate(all.left() + 12, area.center().y());
    p->rotate(-90);
    p->drawText(QRectF(-area.height() / 2, -8, area.height(), 16), Qt::AlignHCenter, m_plot->yLabel());
    p->restore();
}
