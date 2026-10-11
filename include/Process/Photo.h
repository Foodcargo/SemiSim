#pragma once

class Wafer;

class Photo {
public:
    void apply(Wafer &wafer, double thicknessNm = 1200.0) const;
};
