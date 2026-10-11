#include "Process/Photo.h"

#include "Model/Wafer.h"

void Photo::apply(Wafer &wafer, double thicknessNm) const
{
    wafer.addLayer({{"Photoresist", QColor("#f97316"), 0.78}, thicknessNm});
}
