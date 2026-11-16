using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;

namespace SemiconductorEdu.Simulation.Initialization;

public sealed class WaferStateFactory
{
    public const int DefaultResolution = 512;

    public SemiconductorEdu.Domain.Validation.ValidationResult Validate(WaferInitializationParameters p, int resolution = DefaultResolution)
    {
        var errors = new List<SemiconductorEdu.Domain.Validation.ValidationIssue>();
        void Check(bool condition, string field, string expected)
        {
            if (!condition) errors.Add(new("INITIALIZATION_INPUT_INVALID", "InputValidation", field, null, expected,
                field + ": " + expected, "입력 범위와 선택지를 확인하세요."));
        }
        Check(p.Material == WaferMaterial.Silicon, "material", "Si");
        Check(p.DiameterMm is 100 or 150 or 200 or 300, "diameter", "100,150,200,300 mm");
        Check(Enum.IsDefined(p.Orientation), "orientation", "(100),(110),(111)");
        Check(Enum.IsDefined(p.DopingType), "dopingType", "P-type,N-type");
        Check(Enum.IsDefined(p.StartSurface), "startSurface", "BareSi,UniformSiO2");
        Check(p.SubstrateThickness.Meters >= Length.FromMicrometers(300).Meters && p.SubstrateThickness.Meters <= Length.FromMicrometers(1000).Meters, "substrateThickness", "300-1000 um");
        Check(p.BackgroundDoping.PerCubicMeter >= Concentration.FromPerCubicCentimeter(1e13).PerCubicMeter && p.BackgroundDoping.PerCubicMeter <= Concentration.FromPerCubicCentimeter(1e19).PerCubicMeter, "backgroundDoping", "1e13-1e19 cm^-3");
        Check(p.DomainWidth.Meters >= Length.FromMicrometers(20).Meters && p.DomainWidth.Meters <= Length.FromMicrometers(100).Meters, "domainWidth", "20-100 um");
        Check(double.IsFinite(p.ContaminationIndex) && p.ContaminationIndex is >= 0 and <= 100, "contamination", "0-100");
        if (p.StartSurface == WaferStartSurface.UniformSiO2)
            Check(p.InitialOxideThickness.Meters >= Length.FromNanometers(1).Meters && p.InitialOxideThickness.Meters <= Length.FromNanometers(1000).Meters, "initialOxide", "1-1000 nm");
        Check(resolution is >= 2 and <= 4096, "resolution", "2-4096");
        return new(errors.Count == 0, errors, Array.Empty<SemiconductorEdu.Domain.Validation.ValidationIssue>());
    }

    public WaferState Create(
        WaferInitializationParameters parameters,
        Guid initializationProcessId,
        DateTimeOffset createdAtUtc,
        int resolution = DefaultResolution)
    {
        var validation = Validate(parameters, resolution);
        if (!validation.IsValid) throw new ArgumentException(string.Join(", ", validation.Errors.Select(e => e.MessageKo)), nameof(parameters));

        var siliconLayerId = Guid.NewGuid();
        var oxideThickness = parameters.StartSurface == WaferStartSurface.UniformSiO2
            ? parameters.InitialOxideThickness
            : Length.FromMeters(0);
        var oxideLayerId = oxideThickness.Meters > 0 ? Guid.NewGuid() : Guid.Empty;
        var layers = new List<LayerReference>
        {
            new(siliconLayerId, WaferMaterial.Silicon, initializationProcessId)
        };
        if (oxideThickness.Meters > 0)
            layers.Add(new LayerReference(oxideLayerId, WaferMaterial.SiliconDioxide, initializationProcessId));

        var columns = new CrossSectionColumn[resolution];
        for (var index = 0; index < resolution; index++)
        {
            var x = parameters.DomainWidth.Meters * index / (resolution - 1);
            var segments = new List<MaterialSegment>
            {
                new(siliconLayerId, WaferMaterial.Silicon, parameters.SubstrateThickness, initializationProcessId)
            };
            if (oxideThickness.Meters > 0)
                segments.Add(new MaterialSegment(oxideLayerId, WaferMaterial.SiliconDioxide, oxideThickness, initializationProcessId));

            columns[index] = new CrossSectionColumn(
                Length.FromMeters(x),
                Array.AsReadOnly(segments.ToArray()),
                Length.FromMeters(parameters.SubstrateThickness.Meters + oxideThickness.Meters));
        }

        return new WaferState(
            Guid.NewGuid(),
            null,
            0,
            createdAtUtc,
            new SubstrateState(
                parameters.Material,
                parameters.DiameterMm,
                parameters.SubstrateThickness,
                parameters.Orientation,
                parameters.DopingType,
                parameters.BackgroundDoping,
                parameters.StartSurface),
            new CrossSectionState(parameters.DomainWidth, Length.FromMicrometers(20), resolution, Array.AsReadOnly(columns)),
            Array.AsReadOnly(layers.ToArray()),
            null,
            Array.Empty<ImplantProfileState>(),
            Array.Empty<WireState>(),
            parameters.ContaminationIndex,
            Array.Empty<QualityFlag>(),
            Array.AsReadOnly(new[] { initializationProcessId }));
    }
}
