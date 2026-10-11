#pragma once

#include <QColor>
#include <QString>

struct Material {
    QString name;
    QColor color;
    double opacity = 1.0;
};
