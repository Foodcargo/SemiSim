namespace SemiconductorEdu.Domain.Validation;

public sealed record ValidationIssue(
    string Code,
    string Stage,
    string? ParameterId,
    string? ActualValue,
    string ExpectedCondition,
    string MessageKo,
    string SuggestedAction);

public sealed record ValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Errors,
    IReadOnlyList<ValidationIssue> Warnings)
{
    public static ValidationResult Success(IReadOnlyList<ValidationIssue>? warnings = null) =>
        new(true, Array.Empty<ValidationIssue>(), warnings ?? Array.Empty<ValidationIssue>());

    public static ValidationResult Failure(params ValidationIssue[] errors) =>
        new(false, errors, Array.Empty<ValidationIssue>());
}
