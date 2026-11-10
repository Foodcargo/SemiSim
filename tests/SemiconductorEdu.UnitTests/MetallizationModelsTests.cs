using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;
using SemiconductorEdu.Simulation.Initialization;
using SemiconductorEdu.Simulation.Processes;

namespace SemiconductorEdu.UnitTests;

public sealed class MetallizationModelsTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultAlWireProducesFivePointSixOhmsAndPreservesDielectricCrossSection()
    {
        var before = CreateOxideWafer();
        var output = new MetallizationModels().ApplyIdealPattern(before,
            new MetallizationParameters(WaferMaterial.Aluminum, MetallizationMode.IdealPattern,
                Length.FromNanometers(1000), Length.FromNanometers(500), Length.FromMicrometers(100), Length.FromMicrometers(0)),
            Guid.NewGuid(), Now.AddSeconds(1));

        Assert.True(output.Validation.IsValid);
        var wire = Assert.Single(output.CandidateState!.Wires);
        Assert.InRange(Math.Abs(wire.ResistanceOhms - 5.6), 0, 5.6e-9);
        Assert.Equal(before.CrossSection.Columns, output.CandidateState.CrossSection.Columns);
        Assert.Equal(before.Revision + 1, output.CandidateState.Revision);
    }

    [Fact]
    public void WireCreationRejectsOutOfBoundsGeometryAndDuplicateMetalState()
    {
        var model = new MetallizationModels();
        var before = CreateOxideWafer();
        var invalid = model.ApplyIdealPattern(before,
            new MetallizationParameters(WaferMaterial.Aluminum, MetallizationMode.IdealPattern,
                Length.FromNanometers(1000), Length.FromNanometers(500), Length.FromMicrometers(100), Length.FromMicrometers(20)),
            Guid.NewGuid(), Now.AddSeconds(1));
        var wire = model.ApplyIdealPattern(before,
            new MetallizationParameters(WaferMaterial.Aluminum, MetallizationMode.IdealPattern,
                Length.FromNanometers(1000), Length.FromNanometers(500), Length.FromMicrometers(100), Length.FromMicrometers(0)),
            Guid.NewGuid(), Now.AddSeconds(1)).CandidateState!;
        var duplicate = model.ApplyIdealPattern(wire,
            new MetallizationParameters(WaferMaterial.Aluminum, MetallizationMode.IdealPattern,
                Length.FromNanometers(1000), Length.FromNanometers(500), Length.FromMicrometers(100), Length.FromMicrometers(0)),
            Guid.NewGuid(), Now.AddSeconds(2));

        Assert.False(invalid.Validation.IsValid);
        Assert.Null(invalid.CandidateState);
        Assert.Contains(invalid.Validation.Errors, issue => issue.Code == "WIRE_OFFSET_RANGE");
        Assert.False(duplicate.Validation.IsValid);
        Assert.Null(duplicate.CandidateState);
        Assert.Contains(duplicate.Validation.Errors, issue => issue.Code == "WIRE_DUPLICATE_OR_EXISTING_METAL");
    }

    private static WaferState CreateOxideWafer() => new WaferStateFactory().Create(
        WaferInitializationParameters.EducationalDefaults() with
        {
            StartSurface = WaferStartSurface.UniformSiO2,
            InitialOxideThickness = Length.FromNanometers(100)
        }, Guid.NewGuid(), Now, resolution: 17);
}
