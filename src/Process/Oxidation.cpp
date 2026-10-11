#include "Process/Oxidation.h"

#include "Model/Wafer.h"

#include <QtGlobal>

void Oxidation::apply(Wafer &wafer, const OxidationParameters &parameters) const
{
    const double thicknessNm = estimateThicknessNm(parameters);
    wafer.addLayer({{"Silicon Dioxide", QColor("#91d5ff"), 0.86}, thicknessNm});
}

double Oxidation::estimateThicknessNm(const OxidationParameters &parameters) const
{
    if (parameters.targetThicknessNm > 0.0) {
        return parameters.targetThicknessNm;
    }

    const double modeRate = parameters.mode == OxidationMode::Wet ? 2.8 : 0.65;
    const double temperatureFactor = qBound(0.25, parameters.temperatureC / 1000.0, 1.25);
    const double timeFactor = qMax(0.0, parameters.timeMin);

    return qBound(1.0, modeRate * temperatureFactor * timeFactor, 2000.0);
}
