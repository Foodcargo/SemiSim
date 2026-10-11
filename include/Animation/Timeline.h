#pragma once

#include <QObject>

class Timeline : public QObject {
    Q_OBJECT

public:
    explicit Timeline(QObject *parent = nullptr);
};
