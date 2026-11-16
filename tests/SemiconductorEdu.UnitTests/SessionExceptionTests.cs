using SemiconductorEdu.Application.Execution;
using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Simulation.Initialization;
namespace SemiconductorEdu.UnitTests;
public sealed class SessionExceptionTests
{
    private sealed class ThrowingExecutor : IProcessExecutor
    {
        public ProcessExecutionResponse Execute(ProcessExecutionRequest request) => throw new ArithmeticException("Numeric failure");
    }
    [Fact]
    public void NumericFailureIsRecordedWithoutCommit()
    {
        var session = new ProcessSession(new Dictionary<ProcessType, IProcessExecutor> { [ProcessType.Oxidation] = new ThrowingExecutor() });
        var initial = new WaferStateFactory().Create(WaferInitializationParameters.EducationalDefaults(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        session.Initialize(initial);
        var recipe = new ProcessRecipe(Guid.NewGuid(), ProcessType.Oxidation, OxidationParameters.EducationalDefaults(), "equipment", "quality", "1", "1");
        var result = session.Execute(recipe, DateTimeOffset.UtcNow);
        Assert.Equal(ProcessExecutionStatus.CalculationError, result.Result.ExecutionStatus);
        Assert.Null(result.CommittedState);
        Assert.Equal(initial, session.CurrentState);
        Assert.Equal(recipe, session.History.Single().Recipe);
    }
}
