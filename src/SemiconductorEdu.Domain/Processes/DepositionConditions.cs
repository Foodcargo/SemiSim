using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;
namespace SemiconductorEdu.Domain.Processes;
public sealed record DepositionConditions(
    DepositionMethod Method, WaferMaterial Material, Temperature Temperature, Pressure Pressure,
    Duration? Duration = null, Sccm? Flow = null, double? PowerWatts = null,
    int? Cycles = null, Duration? PulseTime = null, Duration? PurgeTime = null) : IProcessParameters;
