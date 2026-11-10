using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;

namespace SemiconductorEdu.Domain.Processes;

public enum MetallizationMode
{
    IdealPattern
}

public sealed record MetallizationParameters(
    WaferMaterial Material,
    MetallizationMode Mode,
    Length Width,
    Length Thickness,
    Length Length,
    Length Offset) : IProcessParameters;
