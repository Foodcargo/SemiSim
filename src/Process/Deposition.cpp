#include "Process/Deposition.h"

#include "Model/Wafer.h"

void Deposition::apply(Wafer &wafer, double thicknessNm) const
{
    wafer.addLayer({{"Polysilicon", QColor("#475569"), 0.92}, thicknessNm});
}
