using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;

namespace SemiconductorEdu.Infrastructure.Storage;

public sealed record SessionDocument(
    int SchemaVersion,
    string ApplicationVersion,
    string ConfigurationVersion,
    string ModelVersion,
    DateTimeOffset SavedAtUtc,
    Guid? CurrentStateId,
    IReadOnlyList<WaferState> States,
    IReadOnlyList<ProcessResult> History);
