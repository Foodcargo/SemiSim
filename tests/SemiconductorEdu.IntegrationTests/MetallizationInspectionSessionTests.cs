using SemiconductorEdu.Application.Execution;
using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;
using SemiconductorEdu.Simulation.Initialization;
using SemiconductorEdu.Simulation.Processes;

namespace SemiconductorEdu.IntegrationTests;

public sealed class MetallizationInspectionSessionTests
{
    [Fact]
    public void MetallizationAndElectricalInspectionCommitStateAndInspectionRecord()
    {
        var now = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var wafer = new WaferStateFactory().Create(WaferInitializationParameters.EducationalDefaults() with
        {
            StartSurface = WaferStartSurface.UniformSiO2,
            InitialOxideThickness = Length.FromNanometers(100)
        }, Guid.NewGuid(), now, 17);
        var metallization = new MetallizationProcessExecutor(new MetallizationModels());
        var inspection = new InspectionProcessExecutor(new InspectionModels());
        var session = new ProcessSession(new Dictionary<ProcessType, IProcessExecutor>
        {
            [ProcessType.Metallization] = metallization,
            [ProcessType.Inspection] = inspection
        });
        session.Initialize(wafer);

        var wireResult = session.Execute(new ProcessRecipe(Guid.NewGuid(), ProcessType.Metallization,
            new MetallizationParameters(WaferMaterial.Aluminum, MetallizationMode.IdealPattern,
                Length.FromNanometers(1000), Length.FromNanometers(500), Length.FromMicrometers(100), Length.FromMicrometers(0)),
            "wire-edu", "not-evaluated", "1.0.0", "1.0.0"), now.AddSeconds(1));
        var wire = Assert.Single(session.CurrentState!.Wires);
        var inspectionResult = session.Execute(new ProcessRecipe(Guid.NewGuid(), ProcessType.Inspection,
            new InspectionParameters(InspectionMode.Electrical, wire.WireId,
                new QualityTarget("resistance", 5.6, 0, 0.01, null, null, "Ω", true)),
            "inspection-edu", "custom", "1.0.0", "1.0.0"), now.AddSeconds(2));

        Assert.Equal(ProcessExecutionStatus.ProcessSuccess, wireResult.Result.ExecutionStatus);
        Assert.Equal(ProcessExecutionStatus.ProcessSuccess, inspectionResult.Result.ExecutionStatus);
        Assert.Equal(QualityStatus.Pass, inspectionResult.Result.QualityStatus);
        Assert.Single(session.CurrentState!.Inspections!);
        Assert.Single(session.CurrentState.Wires);
        Assert.Equal(2, session.History.Count);
    }

    [Fact]
    public void FailedQualityInspectionStillCommitsInspectionResult()
    {
        var now = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var wafer = new WaferStateFactory().Create(WaferInitializationParameters.EducationalDefaults() with
        {
            StartSurface = WaferStartSurface.UniformSiO2,
            InitialOxideThickness = Length.FromNanometers(100)
        }, Guid.NewGuid(), now, 17);
        var session = new ProcessSession(new Dictionary<ProcessType, IProcessExecutor>
        {
            [ProcessType.Inspection] = new InspectionProcessExecutor(new InspectionModels())
        });
        session.Initialize(wafer);
        var oxide = Assert.Single(wafer.Layers, layer => layer.Material == WaferMaterial.SiliconDioxide);

        var result = session.Execute(new ProcessRecipe(Guid.NewGuid(), ProcessType.Inspection,
            new InspectionParameters(InspectionMode.Thickness, oxide.LayerId,
                new QualityTarget("Thickness", 200, 0, 0, null, null, "nm", true)),
            "inspection-edu", "custom", "1.0.0", "1.0.0"), now.AddSeconds(1));

        Assert.Equal(ProcessExecutionStatus.ProcessFailure, result.Result.ExecutionStatus);
        Assert.Equal(QualityStatus.Fail, result.Result.QualityStatus);
        Assert.NotNull(result.CommittedState);
        Assert.Single(session.CurrentState!.Inspections!);
        Assert.Equal(1, session.CurrentState.Revision);
    }
}
