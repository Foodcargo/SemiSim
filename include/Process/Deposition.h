#pragma once

class Wafer;

class Deposition {
public:
    void apply(Wafer &wafer, double thicknessNm = 300.0) const;
};
