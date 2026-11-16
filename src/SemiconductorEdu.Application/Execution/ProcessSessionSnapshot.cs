using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
namespace SemiconductorEdu.Application.Execution;
public sealed record ProcessSessionSnapshot(Guid? CurrentStateId, IReadOnlyList<WaferState> States, IReadOnlyList<ProcessResult> History);
