using SemiconductorEdu.Domain.Equipment;
using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Simulation.Oxidation;

namespace SemiconductorEdu.Infrastructure.Configuration;

public static class SimulationConfigurationMapper
{
    public static OxidationCoefficients ToOxidationCoefficients(this OxidationConfiguration configuration) => new(
        configuration.A_Nanometers,
        configuration.DryB0_NanometersSquaredPerMinute,
        configuration.WetB0_NanometersSquaredPerMinute,
        configuration.ReferenceTemperatureCelsius,
        configuration.TemperatureScaleCelsius,
        configuration.ReferencePressureTorr,
        configuration.SiliconConsumptionRatio,
        configuration.MaximumOxideThicknessNanometers,
        configuration.Version);

    public static EquipmentProfile ToEquipmentProfile(this EquipmentConfiguration configuration) => new(
        configuration.Id,
        configuration.Limits.ToDictionary(
            pair => pair.Key,
            pair => (pair.Value.Minimum, pair.Value.Maximum, pair.Value.Unit),
            StringComparer.Ordinal),
        new HashSet<int>(configuration.AllowedWaferDiametersMm));

    public static QualityProfile ToQualityProfile(this QualityConfiguration configuration) =>
        new(configuration.Id, configuration.Version, Array.AsReadOnly(configuration.Targets.ToArray()));
}

public sealed record EquipmentConfiguration(
    string Id,
    IReadOnlyDictionary<string, EquipmentLimitConfiguration> Limits,
    HashSet<int> AllowedWaferDiametersMm);

public sealed record EquipmentLimitConfiguration(double Minimum, double Maximum, string Unit);
