#include "Interop/SemiSimApi.h"

#include "Model/Wafer.h"
#include "Process/CMP.h"
#include "Process/Deposition.h"
#include "Process/Etching.h"
#include "Process/Implantation.h"
#include "Process/Oxidation.h"
#include "Process/Photo.h"
#include "Scene/WaferRenderer.h"

#include <QByteArray>
#include <QColor>
#include <QJsonArray>
#include <QJsonDocument>
#include <QJsonObject>
#include <QStringList>
#include <algorithm>
#include <cstring>
#include <limits>

namespace {
struct SemiSimInstance {
	Wafer wafer;
	QStringList failures;
	CameraState camera;
};

QString getString(const QJsonObject &parameters, const char *key, const QString &fallback = {})
{
	return parameters.value(QLatin1String(key)).toString(fallback);
}

double getNumber(const QJsonObject &parameters, const char *key, double fallback = 0.0)
{
	return parameters.value(QLatin1String(key)).toDouble(fallback);
}

QColor materialColor(const QString &material)
{
	if (material.startsWith("Copper")) return QColor("#c2410c");
	if (material.startsWith("Tungsten")) return QColor("#f59e0b");
	return QColor("#2563eb");
}

void checkRange(QStringList &failures, const QJsonObject &parameters, const char *key, const QString &label, double minimum, double maximum, const QString &unit)
{
	const double value = getNumber(parameters, key);
	if (value < minimum || value > maximum) {
		failures.append(QString("%1 %2 %3: allowed %4-%5 %6")
							.arg(label).arg(value, 0, 'g', 4).arg(unit).arg(minimum, 0, 'g', 4).arg(maximum, 0, 'g', 4).arg(unit));
	}
}

QJsonObject stateJson(const SemiSimInstance &instance)
{
	QJsonArray layers;
	for (const Layer &layer : instance.wafer.layers()) {
		layers.append(QJsonObject{
			{"name", layer.material.name},
			{"color", layer.material.color.name(QColor::HexRgb)},
			{"opacity", layer.material.opacity},
			{"thicknessNm", layer.thicknessNm},
		});
	}

	QJsonArray log;
	for (const QString &entry : instance.wafer.processLog()) log.append(entry);
	QJsonArray failures;
	for (const QString &failure : instance.failures) failures.append(failure);
	return {{"step", instance.wafer.processStep()}, {"maskPattern", instance.wafer.maskPattern()}, {"layers", layers}, {"log", log}, {"failures", failures}};
}

void applyProcess(SemiSimInstance &instance, int step, const QJsonObject &p)
{
	Wafer &wafer = instance.wafer;
	switch (step) {
	case 0:
		checkRange(instance.failures, p, "substrateThicknessUm", "Wafer thickness", 500.0, 800.0, "um");
		wafer.addLayer({{"Prepared Silicon", QColor("#94a3b8"), 0.28}, getNumber(p, "substrateThicknessUm")});
		wafer.recordProcess(QString("Substrate: %1, %2 um").arg(getString(p, "orientation")).arg(getNumber(p, "substrateThicknessUm")));
		break;
	case 1: {
		checkRange(instance.failures, p, "wellDose", "Well implant dose", 1.0e12, 5.0e13, "ions/cm2");
		Implantation process;
		process.apply(wafer, getNumber(p, "wellDose"));
		wafer.recordProcess(QString("Well implant: %1, dose %2 ions/cm2").arg(getString(p, "wellDopant")).arg(getNumber(p, "wellDose"), 0, 'g', 3));
		break;
	}
	case 2: {
		checkRange(instance.failures, p, "photoThicknessNm", "Photoresist thickness", 500.0, 2000.0, "nm");
		Photo process;
		process.apply(wafer, getNumber(p, "photoThicknessNm"));
		wafer.setMaskPattern(getString(p, "maskPattern", "Single gate line"));
		wafer.recordProcess(QString("Photo mask: %1, developer %2, resist %3 nm").arg(getString(p, "maskPattern")).arg(getString(p, "developer")).arg(getNumber(p, "photoThicknessNm")));
		break;
	}
	case 3: {
		checkRange(instance.failures, p, "gateTemperatureC", "Gate oxidation temperature", 850.0, 1100.0, "C");
		checkRange(instance.failures, p, "gateTimeMin", "Gate oxidation time", 10.0, 120.0, "min");
		checkRange(instance.failures, p, "gateOxideNm", "Gate oxide thickness", 5.0, 120.0, "nm");
		checkRange(instance.failures, p, "polyThicknessNm", "Polysilicon thickness", 150.0, 500.0, "nm");
		OxidationParameters oxidationParameters;
		oxidationParameters.mode = getString(p, "oxidationMode") == "Wet" ? OxidationMode::Wet : OxidationMode::Dry;
		oxidationParameters.temperatureC = getNumber(p, "gateTemperatureC");
		oxidationParameters.timeMin = getNumber(p, "gateTimeMin");
		oxidationParameters.targetThicknessNm = getNumber(p, "gateOxideNm");
		Oxidation oxidation;
		oxidation.apply(wafer, oxidationParameters);
		Deposition deposition;
		deposition.apply(wafer, getNumber(p, "polyThicknessNm"));
		wafer.recordProcess(QString("Gate stack: %1 oxidation at %2 C for %3 min, oxide %4 nm, poly %5 nm")
								.arg(getString(p, "oxidationMode")).arg(getNumber(p, "gateTemperatureC")).arg(getNumber(p, "gateTimeMin")).arg(getNumber(p, "gateOxideNm")).arg(getNumber(p, "polyThicknessNm")));
		break;
	}
	case 4: {
		checkRange(instance.failures, p, "sourceDrainDose", "Source/drain implant dose", 5.0e14, 5.0e15, "ions/cm2");
		Implantation process;
		process.apply(wafer, getNumber(p, "sourceDrainDose"));
		wafer.recordProcess(QString("Source/drain implant: dose %1 ions/cm2").arg(getNumber(p, "sourceDrainDose"), 0, 'g', 3));
		break;
	}
	case 5: {
		checkRange(instance.failures, p, "contactEtchDepthNm", "Contact etch depth", 100.0, 500.0, "nm");
		checkRange(instance.failures, p, "contactFillThicknessNm", "Contact fill thickness", 200.0, 1000.0, "nm");
		Etching etching;
		etching.apply(wafer);
		const QString fillMaterial = getString(p, "contactFill", "Tungsten");
		wafer.addLayer({{fillMaterial, materialColor(fillMaterial), 0.88}, getNumber(p, "contactFillThicknessNm")});
		CMP cmp;
		cmp.apply(wafer);
		wafer.addLayer({{"Planarized Contact", materialColor(fillMaterial).lighter(115), 0.82}, getNumber(p, "contactEtchDepthNm")});
		wafer.recordProcess(QString("Contact: %1 etch, %2 nm depth, %3 fill %4 nm").arg(getString(p, "etchChemistry")).arg(getNumber(p, "contactEtchDepthNm")).arg(fillMaterial).arg(getNumber(p, "contactFillThicknessNm")));
		break;
	}
	case 6: {
		checkRange(instance.failures, p, "metalThicknessNm", "Metal thickness", 300.0, 1200.0, "nm");
		const QString metal = getString(p, "metalMaterial", "Copper");
		wafer.addLayer({{metal + " Interconnect", materialColor(metal), 0.84}, getNumber(p, "metalThicknessNm")});
		wafer.recordProcess(QString("Metal: %1 interconnect, %2 nm").arg(metal).arg(getNumber(p, "metalThicknessNm")));
		break;
	}
	default:
		return;
	}

	wafer.advanceProcess();
	if (wafer.processStep() >= 7) {
		if (instance.failures.isEmpty()) {
			wafer.recordProcess("Final result: Process completed successfully.");
		} else {
			wafer.recordProcess("Final result: Process failed. " + instance.failures.join("; "));
		}
	}
}
}

extern "C" {
void *semisim_create()
{
	return new SemiSimInstance;
}

void semisim_destroy(void *instance)
{
	delete static_cast<SemiSimInstance *>(instance);
}

int semisim_reset(void *instance)
{
	if (!instance) return 0;
	auto &state = *static_cast<SemiSimInstance *>(instance);
	state.failures.clear();
	state.wafer.clear();
	return 1;
}

int semisim_apply_process(void *instance, int step, const char *parametersJson)
{
	if (!instance || !parametersJson) return 0;
	auto &state = *static_cast<SemiSimInstance *>(instance);
	if (step != state.wafer.processStep() || step < 0 || step > 6) return 0;
	QJsonParseError error;
	const QJsonDocument document = QJsonDocument::fromJson(QByteArray(parametersJson), &error);
	if (error.error != QJsonParseError::NoError || !document.isObject()) return 0;
	applyProcess(state, step, document.object());
	return 1;
}

int semisim_get_state(void *instance, char *buffer, int capacity)
{
	if (!instance) return 0;
	const QByteArray json = QJsonDocument(stateJson(*static_cast<SemiSimInstance *>(instance))).toJson(QJsonDocument::Compact);
	const int required = json.size() + 1;
	if (buffer && capacity >= required) std::memcpy(buffer, json.constData(), required);
	return required;
}

int semisim_camera_orbit(void *instance, double deltaYaw, double deltaPitch)
{
	if (!instance) return 0;
	orbitCamera(static_cast<SemiSimInstance *>(instance)->camera, deltaYaw, deltaPitch);
	return 1;
}

int semisim_camera_zoom(void *instance, double delta)
{
	if (!instance) return 0;
	zoomCamera(static_cast<SemiSimInstance *>(instance)->camera, delta);
	return 1;
}

int semisim_render(void *instance, int width, int height, uint8_t *pixels, int capacity)
{
	if (!instance || width <= 0 || height <= 0) return 0;
	const qint64 required = static_cast<qint64>(width) * height * 4;
	if (required > capacity || required > std::numeric_limits<int>::max()) return static_cast<int>(std::min<qint64>(required, std::numeric_limits<int>::max()));
	if (!pixels) return static_cast<int>(required);
	auto &state = *static_cast<SemiSimInstance *>(instance);
	const QImage image = renderWaferFrame(state.wafer, width, height, state.camera);
	for (int row = 0; row < height; ++row) {
		std::memcpy(pixels + static_cast<qint64>(row) * width * 4, image.constScanLine(row), static_cast<size_t>(width) * 4);
	}
	return static_cast<int>(required);
}
}