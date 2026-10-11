#include "Process/Implantation.h"

#include "Model/Wafer.h"

void Implantation::apply(Wafer &wafer, double dose) const
{
    Q_UNUSED(dose)
    wafer.addLayer({{"Doped Region", QColor("#22c55e"), 0.65}, 80.0});
}
