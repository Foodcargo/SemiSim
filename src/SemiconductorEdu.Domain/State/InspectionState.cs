using SemiconductorEdu.Domain.Processes;

namespace SemiconductorEdu.Domain.State;

public sealed record InspectionState(
    Guid InspectionId,
    InspectionMode Mode,
    Guid? TargetId,
    IReadOnlyList<ProcessMetric> Metrics,
    QualityStatus QualityStatus,
    DateTimeOffset CreatedAtUtc,
    Guid OriginProcessId);
