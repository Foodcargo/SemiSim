using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;
using SemiconductorEdu.Simulation.Initialization;
namespace SemiconductorEdu.UnitTests;
public sealed class InitializationValidationTests
{
    [Theory]
    [InlineData(100, true)] [InlineData(150, true)] [InlineData(200, true)] [InlineData(300, true)] [InlineData(250, false)]
    public void DiameterUsesClosedSet(int diameter, bool valid) => Assert.Equal(valid,
        new WaferStateFactory().Validate(WaferInitializationParameters.EducationalDefaults() with { DiameterMm = diameter }).IsValid);
    [Theory]
    [InlineData(299.99, false)] [InlineData(300, true)] [InlineData(1000, true)] [InlineData(1000.01, false)]
    public void SubstrateRangeIncludesBoundaries(double thickness, bool valid) => Assert.Equal(valid,
        new WaferStateFactory().Validate(WaferInitializationParameters.EducationalDefaults() with { SubstrateThickness = Length.FromMicrometers(thickness) }).IsValid);
    [Fact]
    public void BareSiliconIgnoresInactiveInitialOxide()
    {
        var p = WaferInitializationParameters.EducationalDefaults() with { InitialOxideThickness = Length.FromNanometers(-1) };
        Assert.True(new WaferStateFactory().Validate(p).IsValid);
        Assert.False(new WaferStateFactory().Validate(p with { StartSurface = WaferStartSurface.UniformSiO2 }).IsValid);
    }
}
