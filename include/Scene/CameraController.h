#pragma once

#include <QObject>
#include <QVector3D>

class CameraController : public QObject {
    Q_OBJECT

public:
    explicit CameraController(QObject *parent = nullptr);

    QVector3D position() const;

private:
    QVector3D m_position = QVector3D(0.0f, 0.0f, 8.0f);
};
