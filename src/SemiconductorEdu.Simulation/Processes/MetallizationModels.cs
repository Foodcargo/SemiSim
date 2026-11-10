using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;
using SemiconductorEdu.Domain.Validation;

namespace SemiconductorEdu.Simulation.Processes;

public sealed class MetallizationModels
{
    public ProcessModelOutput ApplyIdealPattern(WaferState before, MetallizationParameters parameters, Guid runId, DateTimeOffset nowUtc)
    {
        var errors = new List<ValidationIssue>();
        if (parameters.Material is not WaferMaterial.Aluminum and not WaferMaterial.Copper and not WaferMaterial.Tungsten)
            errors.Add(Issue("WIRE_MATERIAL_UNSUPPORTED", "material", parameters.Material.ToString(), "Al, Cu or W", "Select a supported educational wire material."));
        if (!Enum.IsDefined(parameters.Mode))
            errors.Add(Issue("WIRE_MODE_UNSUPPORTED", "mode", parameters.Mode.ToString(), "IdealPattern", "Select the supported ideal-pattern mode."));
        if (!InRange(parameters.Width.Nanometers, 10, 10000))
            errors.Add(Issue("WIRE_WIDTH_RANGE", "width", parameters.Width.Nanometers.ToString("G17"), "10–10000 nm", "Enter a width within the educational model range."));
        if (!InRange(parameters.Thickness.Nanometers, 10, 5000))
            errors.Add(Issue("WIRE_THICKNESS_RANGE", "thickness", parameters.Thickness.Nanometers.ToString("G17"), "10–5000 nm", "Enter a thickness within the educational model range."));
        if (!InRange(parameters.Length.Micrometers, 0.1, 10000))
            errors.Add(Issue("WIRE_LENGTH_RANGE", "length", parameters.Length.Micrometers.ToString("G17"), "0.1–10000 µm", "Enter a length within the educational model range."));
        if (!double.IsFinite(parameters.Offset.Micrometers) || parameters.Offset.Micrometers < 0 || parameters.Offset.Micrometers + parameters.Width.Micrometers > before.CrossSection.Width.Micrometers)
            errors.Add(Issue("WIRE_OFFSET_RANGE", "offset", parameters.Offset.Micrometers.ToString("G17"), "offset + width <= cross-section width", "Place the wire completely within the local cross-section width."));
        if (before.ResistPattern is not null)
            errors.Add(Issue("WIRE_PHOTORESIST_PRESENT", null, null, "PR-free wafer", "Remove photoresist before adding the educational wire.", ExecutionStage.Preconditions));
        if (before.Wires.Count > 0 || before.Layers.Any(layer => layer.Material is WaferMaterial.Aluminum or WaferMaterial.Copper or WaferMaterial.Tungsten))
            errors.Add(Issue("WIRE_DUPLICATE_OR_EXISTING_METAL", "material", null, "no existing wire or blanket metal layer", "Remove or select a different supported wafer state; duplicate metal generation is not modeled.", ExecutionStage.Preconditions));
        if (before.Layers.Count == 0 || before.Layers.Any(layer => layer.Material is not WaferMaterial.Silicon and not WaferMaterial.SiliconDioxide and not WaferMaterial.SiliconNitride) ||
            before.Layers.All(layer => layer.Material == WaferMaterial.Silicon))
            errors.Add(Issue("WIRE_DIELECTRIC_SUBSTRATE_REQUIRED", null, null, "planar SiO2 or Si3N4 surface over Si", "Use a wafer with an exposed supported dielectric surface.", ExecutionStage.Preconditions));
        if (before.CrossSection.Columns.Count < 2 || before.CrossSection.Columns.Any(column => column.SegmentsBottomToTop.Count == 0 || column.SegmentsBottomToTop[^1].Material is not WaferMaterial.SiliconDioxide and not WaferMaterial.SiliconNitride))
            errors.Add(Issue("WIRE_SURFACE_MUST_BE_DIELECTRIC", null, null, "SiO2 or Si3N4 as top segment in every column", "Use a planar wafer with a supported dielectric at the surface.", ExecutionStage.Preconditions));
        if (before.CrossSection.Columns.Select(column => column.SurfaceHeight.Meters).Max() - before.CrossSection.Columns.Select(column => column.SurfaceHeight.Meters).Min() > 1e-12)
            errors.Add(Issue("WIRE_NONPLANAR_SURFACE", null, null, "flat surface", "Use a planar wafer for ideal-pattern metallization.", ExecutionStage.ModelDomain));
        if (errors.Count > 0) return Failure(errors);

        var resistivity = parameters.Material switch
        {
            WaferMaterial.Aluminum => 2.8e-8,
            WaferMaterial.Copper => 1.7e-8,
            WaferMaterial.Tungsten => 5.6e-8,
            _ => throw new InvalidOperationException()
        };
        var area = parameters.Width.Meters * parameters.Thickness.Meters;
        var resistance = resistivity * parameters.Length.Meters / area;
        if (!double.IsFinite(resistance) || resistance <= 0)
            return Failure(new[] { Issue("WIRE_RESISTANCE_INVALID", "resistance", resistance.ToString("G17"), "finite positive resistance", "No wire state was committed because the calculation was invalid.", ExecutionStage.Calculation) });

        var wire = new WireState(Guid.NewGuid(), parameters.Material,
            new WireGeometry(parameters.Width, parameters.Thickness, parameters.Length, parameters.Offset),
            resistance, runId);
        var candidate = before with
        {
            StateId = Guid.NewGuid(), ParentStateId = before.StateId, Revision = before.Revision + 1, CreatedAtUtc = nowUtc,
            Wires = Array.AsReadOnly(before.Wires.Append(wire).ToArray()),
            HistoryIds = Array.AsReadOnly(before.HistoryIds.Append(runId).ToArray())
        };
        return new ProcessModelOutput(candidate, Array.AsReadOnly(new[] { new ProcessMetric("wireResistance", resistance, "Ω") }), ValidationResult.Success());
    }

    private static bool InRange(double value, double minimum, double maximum) => double.IsFinite(value) && value >= minimum && value <= maximum;

    private static ProcessModelOutput Failure(IReadOnlyList<ValidationIssue> issues) =>
        new(null, Array.Empty<ProcessMetric>(), new ValidationResult(false, issues, Array.Empty<ValidationIssue>()));

    private static ValidationIssue Issue(string code, string? parameterId, string? actual, string expected, string action, ExecutionStage stage = ExecutionStage.InputValidation) =>
        new(code, stage.ToString(), parameterId, actual, expected, code, action);
}
