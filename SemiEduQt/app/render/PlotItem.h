#pragma once

#include <QPointer>
#include <QQuickPaintedItem>
#include <QtQml/qqmlregistration.h>

#include "viewmodels/ResultViewModels.h"

// Line plot for Id–Vg (log Y) and Id–Vd sweeps, drawn with QPainter.
class PlotItem : public QQuickPaintedItem
{
    Q_OBJECT
    QML_ELEMENT
    Q_PROPERTY(PlotViewModel *plot READ plot WRITE setPlot NOTIFY plotChanged)
public:
    explicit PlotItem(QQuickItem *parent = nullptr);
    PlotViewModel *plot() const { return m_plot; }
    void setPlot(PlotViewModel *p);
    void paint(QPainter *painter) override;

signals:
    void plotChanged();

private:
    QPointer<PlotViewModel> m_plot;
};
