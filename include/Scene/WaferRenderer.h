#pragma once

#include <QImage>

class Wafer;

struct CameraState {
	double yawDegrees = -28.0;
	double pitchDegrees = 35.0;
	double zoom = 1.0;
};

void orbitCamera(CameraState &camera, double deltaYaw, double deltaPitch);
void zoomCamera(CameraState &camera, double delta);
QImage renderWaferFrame(const Wafer &wafer, int width, int height, const CameraState &camera);
