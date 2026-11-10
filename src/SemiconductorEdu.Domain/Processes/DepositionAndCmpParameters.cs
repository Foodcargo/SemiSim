using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;

namespace SemiconductorEdu.Domain.Processes;

public enum DepositionMethod
{
    Cvd,
    Pvd,
    Ald
}

public sealed record DepositionParameters(
    DepositionMethod Method,
    WaferMaterial Material,
    Length Thickness) : IProcessParameters;

public sealed record CmpParameters(
    Length TargetSurfaceHeight,
    Length RemovalRate,
    Duration Duration) : IProcessParameters;
