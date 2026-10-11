#include "Scene/CameraController.h"

CameraController::CameraController(QObject *parent)
    : QObject(parent)
{
}

QVector3D CameraController::position() const
{
    return m_position;
}
