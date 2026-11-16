using SemiconductorEdu.Domain.Parameters;
using SemiconductorEdu.Domain.Validation;

namespace SemiconductorEdu.Domain.Processes;

[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "processType")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(OxidationParameters), "Oxidation")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PhotolithographyParameters), "Photolithography")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(EtchParameters), "Etch")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CleaningParameters), "Cleaning")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(DepositionParameters), "Deposition")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CmpParameters), "Cmp")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(IonImplantationParameters), "IonImplantation")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(AnnealParameters), "Anneal")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(MetallizationParameters), "Metallization")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(InspectionParameters), "Inspection")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(DepositionConditions), "DepositionConditions")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PhotoConditions), "PhotoConditions")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(EtchConditions), "EtchConditions")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CleanConditions), "CleanConditions")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(CmpConditions), "CmpConditions")]
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
    string ConfigurationVersion)
{
    public QualityProfile? QualityOverride { get; init; }
}

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
    string EducationalNotice)
{
    public ProcessRecipe? Recipe { get; init; }
    public QualityProfile? AppliedQualityProfile { get; init; }
    public IReadOnlyList<QualityCriterionResult> Criteria { get; init; } = Array.Empty<QualityCriterionResult>();
    public IReadOnlyList<string> UnsupportedEffects { get; init; } = Array.Empty<string>();
}

public sealed record QualityCriterionResult(string MetricId,double? Actual,string Unit,double? Minimum,double? Maximum,bool Pass,string? Reason);
