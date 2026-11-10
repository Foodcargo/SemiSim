using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;

namespace SemiconductorEdu.Domain.Processes;

public sealed record WaferInitializationParameters(
    WaferMaterial Material,
    int DiameterMm,
    CrystalOrientation Orientation,
    DopingType DopingType,
    WaferStartSurface StartSurface,
    Length SubstrateThickness,
    Concentration BackgroundDoping,
    Length InitialOxideThickness,
    Length DomainWidth,
    double ContaminationIndex) : IProcessParameters
{
    public static WaferInitializationParameters EducationalDefaults() => new(
        WaferMaterial.Silicon,
        200,
        CrystalOrientation.OneZeroZero,
        DopingType.PType,
        WaferStartSurface.BareSi,
        Length.FromMicrometers(725),
        Concentration.FromPerCubicCentimeter(1e15),
        Length.FromNanometers(100),
        Length.FromMicrometers(20),
        50);
}
