using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;

namespace SemiconductorEdu.Domain.Processes;

public sealed record IonImplantationParameters(
    DopantSpecies Species,
    Energy Energy,
    ArealDose Dose,
    double TiltDegrees) : IProcessParameters;

public enum AnnealMethod
{
    Rta,
    Furnace
}

public sealed record AnnealParameters(
    AnnealMethod Method,
    Temperature Temperature,
    Duration Duration,
    string Ambient) : IProcessParameters;
