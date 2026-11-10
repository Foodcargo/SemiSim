using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;

namespace SemiconductorEdu.Application.Execution;

public sealed record ProcessExecutionRequest(
    ProcessRecipe Recipe,
    WaferState? CurrentState,
    string ConfigurationHash,
    DateTimeOffset RequestedAtUtc,
    CancellationToken CancellationToken = default);

public sealed record ProcessExecutionResponse(
    ProcessResult Result,
    WaferState? CommittedState)
{
    public WaferState? PreviewState { get; init; }
}

public interface IProcessExecutor
{
    ProcessExecutionResponse Execute(ProcessExecutionRequest request);
}
