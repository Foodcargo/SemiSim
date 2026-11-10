using SemiconductorEdu.Domain.Parameters;
using SemiconductorEdu.Domain.Validation;

namespace SemiconductorEdu.Domain.Processes;

public interface IProcessParameters
{
}

public sealed record ProcessRecipe(
    Guid RecipeId,
    ProcessType ProcessType,
    IProcessParameters Parameters,
    string EquipmentProfileId,
    string QualityProfileId,
    string DefinitionVersion,
    string ConfigurationVersion);

public sealed record QualityTarget(
    string MetricId,
    double Target,
    double? TolerancePercent,
    double? AbsoluteTolerance,
    double? Maximum,
    double? Minimum,
    string Unit,
    bool Required);

public sealed record QualityProfile(string Id, string Version, IReadOnlyList<QualityTarget> Targets);

public sealed record ProcessMetric(
    string Id,
    double? Value,
    string Unit,
    string? NotAvailableCode = null,
    string? Explanation = null);

public sealed record ProcessResult(
    Guid RunId,
    Guid? ParentRunId,
    ProcessType ProcessType,
    ProcessExecutionStatus ExecutionStatus,
    QualityStatus QualityStatus,
    Guid BeforeStateId,
    Guid? AfterStateId,
    IReadOnlyList<ProcessMetric> Metrics,
    IReadOnlyList<ValidationIssue> Errors,
    IReadOnlyList<ValidationIssue> Warnings,
    string ConfigurationHash,
    string ModelVersion,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    string EducationalNotice);
