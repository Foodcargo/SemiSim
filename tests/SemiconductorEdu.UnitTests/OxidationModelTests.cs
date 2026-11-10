using SemiconductorEdu.Domain.Equipment;
using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;
using SemiconductorEdu.Simulation.Initialization;
using SemiconductorEdu.Simulation.Oxidation;

namespace SemiconductorEdu.UnitTests;

public sealed class OxidationModelTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultDryOxidationProducesExpectedEducationalThickness()
    {
        var processId = Guid.NewGuid();
        var wafer = new WaferStateFactory().Create(WaferInitializationParameters.EducationalDefaults(), processId, Now);
        var output = new OxidationModel().Calculate(wafer, OxidationParameters.EducationalDefaults(), Guid.NewGuid(), Now.AddMinutes(1));

        Assert.True(output.Validation.IsValid);
        Assert.NotNull(output.CandidateState);
        Assert.Equal(70.41594578792295, output.Metrics.Single(metric => metric.Id == "oxideThickness").Value!.Value, 8);
        Assert.Equal(1, output.CandidateState!.Revision);
        Assert.Equal(wafer.StateId, output.CandidateState.ParentStateId);
    }

    [Fact]
    public void RetainsUniformInitialOxideAndConsumesSiliconByGrowthRatio()
    {
        var parameters = WaferInitializationParameters.EducationalDefaults() with
        {
            StartSurface = WaferStartSurface.UniformSiO2,
            InitialOxideThickness = Length.FromNanometers(100)
        };
        var wafer = new WaferStateFactory().Create(parameters, Guid.NewGuid(), Now);
        var output = new OxidationModel().Calculate(wafer, OxidationParameters.EducationalDefaults(), Guid.NewGuid(), Now.AddMinutes(1));

        Assert.True(output.Validation.IsValid);
        Assert.NotNull(output.CandidateState);
        var initialSilicon = wafer.CrossSection.Columns[0].SegmentsBottomToTop.Single(segment => segment.Material == WaferMaterial.Silicon).Thickness.Nanometers;
        var resultingSilicon = output.CandidateState!.CrossSection.Columns[0].SegmentsBottomToTop.Single(segment => segment.Material == WaferMaterial.Silicon).Thickness.Nanometers;
        var growth = output.Metrics.Single(metric => metric.Id == "oxideGrowth").Value!.Value;
        var resultingOxide = output.CandidateState.CrossSection.Columns[0].SegmentsBottomToTop.Where(segment => segment.Material == WaferMaterial.SiliconDioxide).Sum(segment => segment.Thickness.Nanometers);

        Assert.Equal(100 + growth, resultingOxide, 8);
        Assert.Equal(0.44 * growth, initialSilicon - resultingSilicon, 8);
    }

    [Fact]
    public void SeparatesValidInputFromEquipmentInterlockLimits()
    {
        var model = new OxidationModel();
        var wafer = new WaferStateFactory().Create(WaferInitializationParameters.EducationalDefaults(), Guid.NewGuid(), Now);
        var belowEquipmentRange = OxidationParameters.EducationalDefaults() with
        {
            Temperature = Temperature.FromCelsius(750)
        };
        var equipment = new EquipmentSnapshot(true, false, true, new HashSet<int> { 200 });

        Assert.True(model.ValidateInput(belowEquipmentRange).IsValid);
        Assert.Contains(model.CheckInterlock(wafer, belowEquipmentRange, equipment).Errors, issue => issue.Code == "INTERLOCK_TEMPERATURE");
    }

    [Fact]
    public void RejectsUnsupportedPatternedSurfaceAsModelDomainError()
    {
        var wafer = new WaferStateFactory().Create(WaferInitializationParameters.EducationalDefaults(), Guid.NewGuid(), Now) with
        {
            ResistPattern = new ResistPatternState(Guid.NewGuid(), Array.Empty<LengthInterval>(), Array.Empty<Length>())
        };

        var result = new OxidationModel().CheckDomain(wafer);

        Assert.Contains(result.Errors, issue => issue.Code == "OXIDATION_PATTERN_UNSUPPORTED");
    }

    [Fact]
    public void QualityFailureDoesNotDiscardCalculatedCandidateState()
    {
        var wafer = new WaferStateFactory().Create(WaferInitializationParameters.EducationalDefaults(), Guid.NewGuid(), Now);
        var output = new OxidationModel().Calculate(wafer, OxidationParameters.EducationalDefaults(), Guid.NewGuid(), Now.AddMinutes(1));
        var target = new QualityTarget("oxideThickness", 100, 1, null, null, null, "nm", true);
        var quality = new OxidationModel().EvaluateThicknessQuality(
            Length.FromNanometers(output.Metrics.Single(metric => metric.Id == "oxideThickness").Value!.Value), target);

        Assert.Equal(QualityStatus.Fail, quality.Status);
        Assert.NotNull(output.CandidateState);
        Assert.NotEqual(wafer.StateId, output.CandidateState!.StateId);
    }
}
