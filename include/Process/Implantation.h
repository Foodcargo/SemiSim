#pragma once

class Wafer;

class Implantation {
public:
    void apply(Wafer &wafer, double dose = 1.0e15) const;
};
