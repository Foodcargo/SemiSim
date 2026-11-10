using SemiconductorEdu.Domain.Units;

namespace SemiconductorEdu.Domain.Processes;

public enum OxidationMethod
{
    Dry,
    Wet
}

public sealed record OxidationParameters(
    OxidationMethod Method,
    Temperature Temperature,
    Duration Duration,
    Pressure Pressure) : IProcessParameters
{
    public static OxidationParameters EducationalDefaults() => new(
        OxidationMethod.Dry,
        Temperature.FromCelsius(1000),
        Duration.FromMinutes(60),
        Pressure.FromTorr(760));
}
