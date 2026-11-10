namespace SemiconductorEdu.Domain.Processes;

public enum InspectionMode
{
    Thickness,
    Cd,
    Contamination,
    Electrical,
    Profile
}

public sealed record InspectionParameters(
    InspectionMode Mode,
    Guid? TargetId,
    QualityTarget Target) : IProcessParameters;
