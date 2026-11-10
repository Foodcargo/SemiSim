using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.Validation;

namespace SemiconductorEdu.Infrastructure.Configuration;

public sealed record OxidationConfiguration(
    double A_Nanometers,
    double DryB0_NanometersSquaredPerMinute,
    double WetB0_NanometersSquaredPerMinute,
    double ReferenceTemperatureCelsius,
    double TemperatureScaleCelsius,
    double ReferencePressureTorr,
    double SiliconConsumptionRatio,
    double MaximumOxideThicknessNanometers,
    string Version);

public sealed record SimulationConfiguration(
    int SchemaVersion,
    string ConfigurationVersion,
    string DefinitionVersion,
    string ModelVersion,
    OxidationConfiguration Oxidation,
    EquipmentConfiguration Equipment,
    QualityProfile Quality,
    IReadOnlyList<string> EnabledProcesses);

public sealed record ConfigurationLoadResult(
    SimulationConfiguration? Configuration,
    IReadOnlyList<ValidationIssue> Errors)
{
    public bool IsValid => Configuration is not null && Errors.Count == 0;
}

public sealed record QualityConfiguration(
    string Id,
    string Version,
    IReadOnlyList<QualityTarget> Targets);
