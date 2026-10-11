#pragma once

#include <QObject>

class LightManager : public QObject {
    Q_OBJECT

public:
    explicit LightManager(QObject *parent = nullptr);
};
