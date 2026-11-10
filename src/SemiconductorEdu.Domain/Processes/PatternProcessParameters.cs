using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;

namespace SemiconductorEdu.Domain.Processes;

public sealed record PhotolithographyParameters(
    Length ResistThickness,
    IReadOnlyList<LengthInterval> OpenIntervals) : IProcessParameters;

public sealed record EtchParameters(
    WaferMaterial TargetMaterial,
    Length EtchDepth,
    double OverEtchPercent) : IProcessParameters;

public enum CleaningMethod
{
    PhotoresistStrip
}

public sealed record CleaningParameters(CleaningMethod Method) : IProcessParameters;
