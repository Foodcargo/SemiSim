#include "Scene/WaferRenderer.h"

#include "Model/Wafer.h"

#include <QColor>
#include <QPainter>
#include <QPen>
#include <QPointF>
#include <QPolygonF>
#include <QString>
#include <QVector>
#include <QtMath>
#include <algorithm>
#include <cmath>

namespace {
struct Vec3 {
	double x;
	double y;
	double z;
};

struct MeshFace {
	QVector<Vec3> vertices;
	QColor color;
	double opacity;
};

struct ProjectedFace {
	QPolygonF polygon;
	QColor color;
	double depth;
	double opacity;
};

const Layer *findLayer(const Wafer &wafer, const QString &name, int occurrence = 0)
{
	for (const Layer &layer : wafer.layers()) {
		if (layer.material.name == name && occurrence-- == 0) return &layer;
	}
	return nullptr;
}

double visualThickness(const Layer &layer)
{
	return qBound(0.045, std::log10(qMax(1.0, layer.thicknessNm)) * 0.018, 0.14);
}

void addFace(QVector<MeshFace> &faces, const QVector<Vec3> &vertices, const QColor &color, double opacity)
{
	faces.append({vertices, color, opacity});
}

void addBox(QVector<MeshFace> &faces, double x, double y, double z, double width, double depth, double height, const Layer &layer)
{
	const double x2 = x + width;
	const double y2 = y + depth;
	const double z2 = z + height;
	const QColor color = layer.material.color;
	addFace(faces, {{x, y, z2}, {x2, y, z2}, {x2, y2, z2}, {x, y2, z2}}, color.lighter(125), layer.material.opacity);
	addFace(faces, {{x, y, z}, {x2, y, z}, {x2, y, z2}, {x, y, z2}}, color.darker(125), layer.material.opacity);
	addFace(faces, {{x2, y, z}, {x2, y2, z}, {x2, y2, z2}, {x2, y, z2}}, color.darker(145), layer.material.opacity);
	addFace(faces, {{x, y2, z}, {x, y, z}, {x, y, z2}, {x, y2, z2}}, color.darker(135), layer.material.opacity);
	addFace(faces, {{x, y2, z}, {x2, y2, z}, {x2, y2, z2}, {x, y2, z2}}, color.darker(120), layer.material.opacity);
}

void addCylinder(QVector<MeshFace> &faces, double radius, double bottom, double height, const Layer &layer)
{
	constexpr int segments = 48;
	constexpr double pi = 3.14159265358979323846;
	QVector<Vec3> top;
	QVector<Vec3> base;
	top.reserve(segments);
	base.reserve(segments);
	for (int i = 0; i < segments; ++i) {
		const double angle = 2.0 * pi * i / segments;
		const double x = qCos(angle) * radius;
		const double y = qSin(angle) * radius;
		top.append({x, y, bottom + height});
		base.append({x, y, bottom});
	}
	addFace(faces, top, layer.material.color.lighter(115), layer.material.opacity);
	addFace(faces, base, layer.material.color.darker(150), layer.material.opacity);
	for (int i = 0; i < segments; ++i) {
		const int next = (i + 1) % segments;
		QColor side = layer.material.color.darker(118 + static_cast<int>(35.0 * std::abs(qCos(2.0 * pi * i / segments))));
		addFace(faces, {base[i], base[next], top[next], top[i]}, side, layer.material.opacity);
	}
}

void addMaskFeatures(QVector<MeshFace> &faces, const Layer &photoresist, const QString &pattern, double z)
{
	const double height = visualThickness(photoresist);
	if (pattern == "Two finger gate") {
		addBox(faces, -0.62, -1.0, z, 0.24, 2.0, height, photoresist);
		addBox(faces, 0.38, -1.0, z, 0.24, 2.0, height, photoresist);
	} else if (pattern == "Contact array") {
		for (int x = -1; x <= 1; ++x)
			for (int y = -1; y <= 1; ++y)
				addBox(faces, x * 0.75 - 0.16, y * 0.65 - 0.16, z, 0.32, 0.32, height, photoresist);
	} else if (pattern == "Guard ring") {
		addBox(faces, -2.1, -1.5, z, 4.2, 0.18, height, photoresist);
		addBox(faces, -2.1, 1.32, z, 4.2, 0.18, height, photoresist);
		addBox(faces, -2.1, -1.32, z, 0.18, 2.64, height, photoresist);
		addBox(faces, 1.92, -1.32, z, 0.18, 2.64, height, photoresist);
	} else {
		addBox(faces, -1.0, -0.18, z, 2.0, 0.36, height, photoresist);
	}
}

void addGateFeatures(QVector<MeshFace> &faces, const Layer &poly, const QString &pattern, double z)
{
	const double height = visualThickness(poly);
	if (pattern == "Two finger gate") {
		addBox(faces, -0.54, -1.0, z, 0.20, 2.0, height, poly);
		addBox(faces, 0.34, -1.0, z, 0.20, 2.0, height, poly);
	} else {
		addBox(faces, -1.0, -0.16, z, 2.0, 0.32, height, poly);
	}
}

void buildScene(const Wafer &wafer, QVector<MeshFace> &faces)
{
	const Layer *silicon = findLayer(wafer, "Silicon");
	if (!silicon) return;
	addCylinder(faces, 3.0, -0.22, 0.44, *silicon);

	if (const Layer *prepared = findLayer(wafer, "Prepared Silicon"))
		addCylinder(faces, 2.98, 0.22, visualThickness(*prepared) * 0.35, *prepared);

	const Layer *doped = findLayer(wafer, "Doped Region");
	if (doped && wafer.processStep() >= 2)
		addBox(faces, -2.15, -1.2, 0.27, 4.3, 2.4, visualThickness(*doped), *doped);

	if (const Layer *photo = findLayer(wafer, "Photoresist"); photo && wafer.processStep() == 3)
		addMaskFeatures(faces, *photo, wafer.maskPattern(), 0.34);

	const Layer *oxide = findLayer(wafer, "Silicon Dioxide");
	if (oxide) addBox(faces, -1.25, -0.58, 0.30, 2.5, 1.16, visualThickness(*oxide), *oxide);

	const Layer *poly = findLayer(wafer, "Polysilicon");
	if (poly) addGateFeatures(faces, *poly, wafer.maskPattern(), 0.34 + (oxide ? visualThickness(*oxide) : 0.0));

	if (doped && wafer.processStep() >= 5) {
		const double height = visualThickness(*doped);
		addBox(faces, -2.15, -0.62, 0.29, 0.90, 1.24, height, *doped);
		addBox(faces, 1.25, -0.62, 0.29, 0.90, 1.24, height, *doped);
	}

	const Layer *contact = findLayer(wafer, "Planarized Contact");
	if (contact) {
		for (int x : {-1, 1})
			for (int y : {-1, 1})
				addBox(faces, x * 1.25 - 0.13, y * 0.62 - 0.13, 0.38, 0.26, 0.26, visualThickness(*contact), *contact);
	}

	for (const Layer &layer : wafer.layers()) {
		if (layer.material.name.endsWith("Interconnect")) {
			addBox(faces, -2.0, -1.55, 0.50, 1.65, 0.22, visualThickness(layer), layer);
			addBox(faces, 0.35, -1.55, 0.50, 1.65, 0.22, visualThickness(layer), layer);
			break;
		}
	}
}

QPointF project(const Vec3 &point, double scale, const QPointF &origin, const CameraState &camera, double &depth)
{
	const double yaw = qDegreesToRadians(camera.yawDegrees);
	const double pitch = qDegreesToRadians(camera.pitchDegrees);
	const double rotatedX = point.x * qCos(yaw) - point.y * qSin(yaw);
	const double rotatedY = point.x * qSin(yaw) + point.y * qCos(yaw);
	const double cameraY = rotatedY * qCos(pitch) - point.z * qSin(pitch);
	depth = rotatedY * qSin(pitch) + point.z * qCos(pitch);
	const double perspective = 15.0 / qMax(1.0, 15.0 - depth);
	return QPointF(origin.x() + rotatedX * scale * perspective,
				   origin.y() + cameraY * scale * perspective);
}
}

void orbitCamera(CameraState &camera, double deltaYaw, double deltaPitch)
{
	camera.yawDegrees = std::fmod(camera.yawDegrees + deltaYaw, 360.0);
	camera.pitchDegrees = qBound(10.0, camera.pitchDegrees + deltaPitch, 80.0);
}

void zoomCamera(CameraState &camera, double delta)
{
	camera.zoom = qBound(0.65, camera.zoom + delta, 1.8);
}

QImage renderWaferFrame(const Wafer &wafer, int width, int height, const CameraState &camera)
{
	if (width <= 0 || height <= 0) {
		return {};
	}

	QImage image(width, height, QImage::Format_RGBA8888);
	image.fill(QColor("#090d12"));
	QPainter painter(&image);
	painter.setRenderHint(QPainter::Antialiasing);

	const double scale = qMin(width / 8.2, height / 6.5) * camera.zoom;
	const QPointF origin(width * 0.50, height * 0.62);
	painter.setPen(QPen(QColor(120, 139, 166, 54), 1.0));
	for (int i = -12; i <= 12; ++i) {
		const double v = i * 0.35;
		double depth = 0.0;
		const QPointF startX = project({-4.4, v, -0.24}, scale, origin, camera, depth);
		const QPointF endX = project({4.4, v, -0.24}, scale, origin, camera, depth);
		const QPointF startY = project({v, -4.4, -0.24}, scale, origin, camera, depth);
		const QPointF endY = project({v, 4.4, -0.24}, scale, origin, camera, depth);
		painter.drawLine(startX, endX);
		painter.drawLine(startY, endY);
	}

	QVector<MeshFace> faces;
	buildScene(wafer, faces);
	QVector<ProjectedFace> projectedFaces;
	projectedFaces.reserve(faces.size());
	for (const MeshFace &face : faces) {
		QPolygonF polygon;
		double depthSum = 0.0;
		for (const Vec3 &vertex : face.vertices) {
			double depth = 0.0;
			polygon << project(vertex, scale, origin, camera, depth);
			depthSum += depth;
		}
		projectedFaces.append({polygon, face.color, depthSum / face.vertices.size(), face.opacity});
	}

	std::sort(projectedFaces.begin(), projectedFaces.end(), [](const ProjectedFace &left, const ProjectedFace &right) {
		return left.depth < right.depth;
	});
	for (const ProjectedFace &face : projectedFaces) {
		QColor color = face.color;
		color.setAlphaF(qBound(0.0, color.alphaF() * face.opacity, 1.0));
		painter.setPen(QPen(color.lighter(135), 0.8));
		painter.setBrush(color);
		painter.drawPolygon(face.polygon);
	}

	return image;
}