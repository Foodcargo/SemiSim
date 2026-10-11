#pragma once

#include <QObject>

class SceneManager : public QObject {
    Q_OBJECT

public:
    explicit SceneManager(QObject *parent = nullptr);
};
