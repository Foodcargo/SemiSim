#include "Process/Etching.h"

#include "Model/Wafer.h"

void Etching::apply(Wafer &wafer) const
{
    wafer.removeTopLayer();
}
