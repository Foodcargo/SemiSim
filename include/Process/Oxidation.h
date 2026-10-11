#pragma once

class Wafer;

enum class OxidationMode {
    Dry,
    Wet
};

struct OxidationParameters {
    OxidationMode mode = OxidationMode::Dry;
    double temperatureC = 1000.0;
    double timeMin = 30.0;
    double targetThicknessNm = 100.0;
};

class Oxidation {
public:
    void apply(Wafer &wafer, const OxidationParameters &parameters) const;
    double estimateThicknessNm(const OxidationParameters &parameters) const;
};
