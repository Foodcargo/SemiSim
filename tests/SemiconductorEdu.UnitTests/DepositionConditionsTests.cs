using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;
using SemiconductorEdu.Simulation.Initialization;
using SemiconductorEdu.Simulation.Processes;
namespace SemiconductorEdu.UnitTests;
public sealed class DepositionConditionsTests
{
    private static DepositionConditions Cvd => new(DepositionMethod.Cvd, WaferMaterial.SiliconDioxide,
        Temperature.FromCelsius(400), Pressure.FromTorr(1), Duration.FromMinutes(10), Sccm.FromValue(100));
    private static DepositionConditions Ald => new(DepositionMethod.Ald, WaferMaterial.SiliconDioxide,
        Temperature.FromCelsius(200), Pressure.FromTorr(1), Cycles:100, PulseTime:Duration.FromSeconds(1), PurgeTime:Duration.FromSeconds(5));
    private static ProcessModelOutput Run(DepositionConditions p)
    {
        var before = new WaferStateFactory().Create(WaferInitializationParameters.EducationalDefaults(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        return new DepositionAndCmpModels().ApplyDeposition(before, p, Guid.NewGuid(), DateTimeOffset.UtcNow);
    }
    [Fact] public void DefaultCvdIs100Nm() => Assert.Equal(100, Run(Cvd).Metrics.Single(m => m.Id == "depositedThickness").Value!.Value, 9);
    [Fact] public void DefaultAldIs10NmAnd1200Seconds()
    {
        var output = Run(Ald);
        Assert.Equal(10, output.Metrics.Single(m => m.Id == "depositedThickness").Value!.Value, 9);
        Assert.Equal(1200, output.Metrics.Single(m => m.Id == "duration").Value!.Value, 9);
    }
    [Fact] public void PvdOxideCombinationRejected() => Assert.Contains(Run(Cvd with { Method = DepositionMethod.Pvd }).Validation.Errors, e => e.Code == "DEPOSITION_COMBINATION_INVALID");
    [Fact] public void AldRejectsInactiveDuration() => Assert.Contains(Run(Ald with { Duration = Duration.FromMinutes(10) }).Validation.Errors, e => e.Code == "INAPPLICABLE_PARAMETER");
    [Fact] public void ValidInputMayTripEquipmentInterlock() => Assert.Contains(Run(Cvd with { Temperature = Temperature.FromCelsius(850) }).Validation.Errors, e => e.Stage == ExecutionStage.EquipmentInterlock.ToString());
    [Fact] public void AldPurgePenaltyIsNotHidden() => Assert.Equal(16, Run(Ald with { PurgeTime = Duration.FromSeconds(1) }).Metrics.Single(m => m.Id == "purgePenaltyPct").Value!.Value, 9);
}
