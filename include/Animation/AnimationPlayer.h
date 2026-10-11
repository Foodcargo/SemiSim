#pragma once

#include <QObject>

class AnimationPlayer : public QObject {
    Q_OBJECT

public:
    explicit AnimationPlayer(QObject *parent = nullptr);
};
