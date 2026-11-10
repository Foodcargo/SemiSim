using SemiconductorEdu.Application.Execution;
using SemiconductorEdu.Domain.Equipment;
using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Simulation.Initialization;
using SemiconductorEdu.Simulation.Oxidation;

namespace SemiconductorEdu.UnitTests;

public sealed class ProcessSessionTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SuccessfulExecutionCommitsCandidateAndUndoRedoRestoresStates()
    {
        var initial = new WaferStateFactory().Create(WaferInitializationParameters.EducationalDefaults(), Guid.NewGuid(), Now);
        var session = CreateSession();
        session.Initialize(initial);

        var response = session.Execute(CreateOxidationRecipe(), Now.AddMinutes(1));

        Assert.Equal(ProcessExecutionStatus.ProcessSuccess, response.Result.ExecutionStatus);
        Assert.NotNull(response.CommittedState);
        Assert.Equal(response.CommittedState, session.CurrentState);
        Assert.True(session.Undo());
        Assert.Equal(initial, session.CurrentState);
        Assert.True(session.Redo());
        Assert.Equal(response.CommittedState, session.CurrentState);
        Assert.Single(session.History);
    }

    [Fact]
    public void QualityFailStillCommitsStateButInputFailureDoesNot()
    {
        var initial = new WaferStateFactory().Create(WaferInitializationParameters.EducationalDefaults(), Guid.NewGuid(), Now);
        var session = CreateSession(new QualityProfile("strict", "1", new[] { new QualityTarget("oxideThickness", 100, 1, null, null, null, "nm", true) }));
        session.Initialize(initial);
        var failedQuality = session.Execute(CreateOxidationRecipe(), Now.AddMinutes(1));
        Assert.Equal(QualityStatus.Fail, failedQuality.Result.QualityStatus);
        Assert.NotNull(failedQuality.CommittedState);

        var changed = session.CurrentState;
        var invalidRecipe = CreateOxidationRecipe() with
        {
            Parameters = OxidationParameters.EducationalDefaults() with { Duration = SemiconductorEdu.Domain.Units.Duration.FromMinutes(0) }
        };
        var inputFailure = session.Execute(invalidRecipe, Now.AddMinutes(2));

        Assert.Equal(ProcessExecutionStatus.InvalidInput, inputFailure.Result.ExecutionStatus);
        Assert.Null(inputFailure.CommittedState);
        Assert.Equal(changed, session.CurrentState);
        Assert.Equal(2, session.History.Count);
    }

    [Fact]
    public void UnimplementedProcessesAreRejectedRatherThanReportedAsSuccessful()
    {
        var session = CreateSession();
        var recipe = new ProcessRecipe(Guid.NewGuid(), ProcessType.Etch, OxidationParameters.EducationalDefaults(), "equipment", "quality", "1", "1");

        Assert.Throws<NotSupportedException>(() => session.Execute(recipe, Now));
    }

    private static ProcessSession CreateSession(QualityProfile? quality = null)
    {
        var profile = quality ?? new QualityProfile("default", "1", new[] { new QualityTarget("oxideThickness", 70, 10, null, null, null, "nm", true) });
        var executor = new OxidationProcessExecutor(
            new OxidationModel(),
            new EquipmentSnapshot(true, false, true, new HashSet<int> { 200 }),
            profile);
        return new ProcessSession(new Dictionary<ProcessType, IProcessExecutor> { [ProcessType.Oxidation] = executor });
    }

    private static ProcessRecipe CreateOxidationRecipe() => new(
        Guid.NewGuid(),
        ProcessType.Oxidation,
        OxidationParameters.EducationalDefaults(),
        "educational-furnace",
        "default",
        "1.0.0",
        "1.0.0");
}
