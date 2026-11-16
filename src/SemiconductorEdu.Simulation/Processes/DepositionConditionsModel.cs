using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;
using SemiconductorEdu.Domain.Validation;
namespace SemiconductorEdu.Simulation.Processes;
public sealed partial class DepositionAndCmpModels
{
    public ProcessModelOutput ApplyDeposition(WaferState before, DepositionConditions p, Guid runId, DateTimeOffset now)
    {
        var errors = new List<ValidationIssue>();
        void Range(double? value, double min, double max, string id)
        {
            if (value is not double x || !double.IsFinite(x) || x < min || x > max)
                errors.Add(Issue("DEPOSITION_INPUT_RANGE", id, value?.ToString("R"), $"{min}..{max}", "활성 입력 범위를 확인하세요."));
        }
        void Inactive(bool supplied, string id)
        {
            if (supplied) errors.Add(Issue("INAPPLICABLE_PARAMETER", id, null, "omitted", "비활성 입력은 레시피에서 제거하세요."));
        }
        var t = p.Temperature.Celsius;
        var pressure = p.Pressure.Torr;
        var validMaterial = p.Method switch
        {
            DepositionMethod.Cvd => p.Material is WaferMaterial.SiliconDioxide or WaferMaterial.SiliconNitride,
            DepositionMethod.Pvd => p.Material is WaferMaterial.Aluminum or WaferMaterial.TitaniumNitride,
            DepositionMethod.Ald => p.Material is WaferMaterial.SiliconDioxide or WaferMaterial.TitaniumNitride,
            _ => false
        };
        if (!validMaterial) errors.Add(Issue("DEPOSITION_COMBINATION_INVALID", "material", p.Material.ToString(), "CVD: SiO2/Si3N4; PVD: Al/TiN; ALD: SiO2/TiN", "방식에 맞는 재료를 선택하세요."));
        if (p.Method == DepositionMethod.Cvd)
        {
            Range(t, 200, 900, "temperature"); Range(pressure, 0.1, 760, "pressure"); Range(p.Flow?.Value, 1, 2000, "flow");
        }
        else if (p.Method == DepositionMethod.Pvd)
        {
            Range(t, 20, 400, "temperature"); Range(pressure, 0.001, 0.1, "pressure"); Range(p.PowerWatts, 50, 2000, "power");
        }
        else if (p.Method == DepositionMethod.Ald)
        {
            Range(t, 100, 400, "temperature"); Range(pressure, 0.1, 10, "pressure"); Range(p.Cycles, 1, 5000, "cycles");
            Range(p.PulseTime?.Seconds, 0.1, 10, "pulseTime"); Range(p.PurgeTime?.Seconds, 0.1, 30, "purgeTime");
        }
        if (p.Method != DepositionMethod.Ald) Range(p.Duration?.Minutes, 1, 180, "duration");
        Inactive(p.Method == DepositionMethod.Ald && p.Duration is not null, "duration");
        Inactive(p.Method != DepositionMethod.Cvd && p.Flow is not null, "flow");
        Inactive(p.Method != DepositionMethod.Pvd && p.PowerWatts is not null, "power");
        Inactive(p.Method != DepositionMethod.Ald && p.Cycles is not null, "cycles");
        Inactive(p.Method != DepositionMethod.Ald && p.PulseTime is not null, "pulseTime");
        Inactive(p.Method != DepositionMethod.Ald && p.PurgeTime is not null, "purgeTime");
        if (errors.Count > 0) return Failure(errors);
        if (before.ResistPattern is not null || before.CrossSection.Columns.Any(c => c.SegmentsBottomToTop.Any(s => s.Material == WaferMaterial.Photoresist)))
            return Failure(new[] { Issue("DEPOSITION_PR_PRESENT", null, null, "no PR", "PR을 먼저 제거하세요.", ExecutionStage.Preconditions) });
        if (before.CrossSection.Columns.Count == 0 || before.CrossSection.Columns.Max(c => c.SurfaceHeight.Meters) - before.CrossSection.Columns.Min(c => c.SurfaceHeight.Meters) > 1e-12)
            return Failure(new[] { Issue("DEPOSITION_NONPLANAR", null, null, "planar surface", "단차 피복은 지원하지 않습니다.", ExecutionStage.ModelDomain) });
        var interlock = p.Method switch
        {
            DepositionMethod.Cvd => t > 800 || pressure > 100,
            DepositionMethod.Pvd => p.PowerWatts > 1500,
            DepositionMethod.Ald => t > 350 || p.Cycles > 3000,
            _ => true
        };
        if (interlock) return Failure(new[] { Issue("DEPOSITION_EQUIPMENT_LIMIT", null, null, "CVD: T<=800 C/P<=100 Torr; PVD: power<=1500 W; ALD: T<=350 C/cycles<=3000", "장비 운전 한계를 확인하세요.", ExecutionStage.EquipmentInterlock) });
        double thickness;
        double totalSeconds;
        var purgePenalty = 0.0;
        if (p.Method == DepositionMethod.Cvd)
        {
            var rate = p.Material == WaferMaterial.SiliconDioxide ? 10 : 8;
            thickness = rate * Math.Exp((t - 400) / 300) * Math.Pow(pressure, 0.1) * Math.Pow(p.Flow!.Value.Value / 100, 0.2) * p.Duration!.Value.Minutes;
            totalSeconds = p.Duration.Value.Seconds;
        }
        else if (p.Method == DepositionMethod.Pvd)
        {
            var rate = p.Material == WaferMaterial.Aluminum ? 20 : 10;
            thickness = rate * (p.PowerWatts!.Value / 500) * Math.Pow(0.01 / pressure, 0.1) * Math.Exp((t - 100) / 500) * p.Duration!.Value.Minutes;
            totalSeconds = p.Duration.Value.Seconds;
        }
        else
        {
            var gpc = p.Material == WaferMaterial.SiliconDioxide ? 0.1 : 0.05;
            thickness = gpc * p.Cycles!.Value * Math.Min(1, p.PulseTime!.Value.Seconds) * Math.Exp(-Math.Pow((t - 200) / 100, 2)) * Math.Pow(pressure, 0.05);
            purgePenalty = Math.Max(0, (5 - p.PurgeTime!.Value.Seconds) / 5) * 20;
            totalSeconds = p.Cycles.Value * 2 * (p.PulseTime.Value.Seconds + p.PurgeTime.Value.Seconds);
        }
        if (!double.IsFinite(thickness) || thickness > 10000)
            return Failure(new[] { Issue("DEPOSITION_THICKNESS_DOMAIN", "thickness", thickness.ToString("R"), "<=10000 nm", "계산된 막 두께가 모델 범위를 넘었습니다.", ExecutionStage.ModelDomain) });
        var output = ApplyDeposition(before, new DepositionParameters(p.Method, p.Material, Length.FromNanometers(thickness)), runId, now);
        return output with { Metrics = output.Metrics.Concat(new[] { new ProcessMetric("duration", totalSeconds, "s"), new ProcessMetric("purgePenaltyPct", purgePenalty, "%"), new ProcessMetric("nonuniformity", 0, "%") }).ToArray() };
    }
}
