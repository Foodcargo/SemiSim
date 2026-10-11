#include "Process/CMP.h"

#include "Model/Wafer.h"

void CMP::apply(Wafer &wafer) const
{
    wafer.removeTopLayer();
}
