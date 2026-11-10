using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;
using SemiconductorEdu.Domain.Validation;

namespace SemiconductorEdu.Simulation.Processes;

public sealed class DepositionAndCmpModels
{
    public ProcessModelOutput ApplyDeposition(WaferState before, DepositionParameters parameters, Guid runId, DateTimeOffset nowUtc)
    {
        var errors = new List<ValidationIssue>();
        if (!Enum.IsDefined(parameters.Method))
            errors.Add(Issue("DEPOSITION_METHOD_INVALID", "method", parameters.Method.ToString(), "CVD, PVD or ALD", "지원하는 증착 방식을 선택하세요."));
        if (parameters.Material is not WaferMaterial.SiliconDioxide and not WaferMaterial.SiliconNitride and not WaferMaterial.Aluminum and not WaferMaterial.Copper and not WaferMaterial.Tungsten)
            errors.Add(Issue("DEPOSITION_MATERIAL_UNSUPPORTED", "material", parameters.Material.ToString(), "SiO₂, Si₃N₄, Al, Cu or W", "교육용 예제에서 지원하는 막 재료를 선택하세요."));
        if (!double.IsFinite(parameters.Thickness.Nanometers) || parameters.Thickness.Nanometers is < 1 or > 10000)
            errors.Add(Issue("DEPOSITION_THICKNESS_RANGE", "thickness", parameters.Thickness.Nanometers.ToString("G17"), "1–10000 nm", "증착 두께를 1–10000 nm 범위로 입력하세요."));
        if (before.CrossSection.Columns.Count < 2 || before.CrossSection.Resolution != before.CrossSection.Columns.Count)
            errors.Add(Issue("DEPOSITION_CROSS_SECTION_INVALID", null, null, "valid cross-section grid", "웨이퍼 단면을 다시 초기화하세요.", ExecutionStage.ModelDomain));
        if (errors.Count > 0) return Failure(errors);

        if (before.ResistPattern is not null)
            return Failure(new[] { Issue("DEPOSITION_PATTERN_UNSUPPORTED", null, null, "unpatterned blanket surface", "현재 교육용 증착 모델은 평탄한 blanket 증착만 지원합니다. PR 패턴을 먼저 제거하세요.", ExecutionStage.ModelDomain) });

        var layerId = Guid.NewGuid();
        var columns = before.CrossSection.Columns.Select(column => column with
        {
            SegmentsBottomToTop = Array.AsReadOnly(column.SegmentsBottomToTop.Append(
                new MaterialSegment(layerId, parameters.Material, parameters.Thickness, runId)).ToArray()),
            SurfaceHeight = Length.FromMeters(column.SurfaceHeight.Meters + parameters.Thickness.Meters)
        }).ToArray();
        var candidate = before with
        {
            StateId = Guid.NewGuid(),
            ParentStateId = before.StateId,
            Revision = before.Revision + 1,
            CreatedAtUtc = nowUtc,
            CrossSection = before.CrossSection with { Columns = Array.AsReadOnly(columns) },
            Layers = Array.AsReadOnly(before.Layers.Append(new LayerReference(layerId, parameters.Material, runId)).ToArray()),
            HistoryIds = Array.AsReadOnly(before.HistoryIds.Append(runId).ToArray())
        };
        return Success(candidate, new ProcessMetric("depositedThickness", parameters.Thickness.Nanometers, "nm"));
    }

    public ProcessModelOutput ApplyCmp(WaferState before, CmpParameters parameters, Guid runId, DateTimeOffset nowUtc)
    {
        var errors = new List<ValidationIssue>();
        if (!double.IsFinite(parameters.TargetSurfaceHeight.Nanometers) || parameters.TargetSurfaceHeight.Nanometers < 0)
            errors.Add(Issue("CMP_TARGET_HEIGHT_INVALID", "targetSurfaceHeight", parameters.TargetSurfaceHeight.Nanometers.ToString("G17"), "finite nonnegative target height", "CMP 목표 높이는 0 이상으로 입력하세요."));
        if (!double.IsFinite(parameters.RemovalRate.Nanometers) || parameters.RemovalRate.Nanometers is <= 0 or > 100000)
            errors.Add(Issue("CMP_REMOVAL_RATE_RANGE", "removalRate", parameters.RemovalRate.Nanometers.ToString("G17"), "0–100000 nm/min", "양수이며 모델 상한 이하인 제거율을 입력하세요."));
        if (!double.IsFinite(parameters.Duration.Minutes) || parameters.Duration.Minutes is <= 0 or > 240)
            errors.Add(Issue("CMP_DURATION_RANGE", "duration", parameters.Duration.Minutes.ToString("G17"), "0–240 min", "CMP 시간을 0보다 크고 240분 이하로 입력하세요."));
        if (before.CrossSection.Columns.Count == 0)
            errors.Add(Issue("CMP_EMPTY_CROSS_SECTION", null, null, "at least one cross-section column", "웨이퍼 단면을 다시 초기화하세요.", ExecutionStage.ModelDomain));
        if (before.CrossSection.Columns.Count > 0 && parameters.TargetSurfaceHeight.Meters > before.CrossSection.Columns.Min(column => column.SurfaceHeight.Meters))
            errors.Add(Issue("CMP_TARGET_ABOVE_SURFACE", "targetSurfaceHeight", parameters.TargetSurfaceHeight.Nanometers.ToString("G17"), "target no higher than the lowest current surface", "현재 가장 낮은 표면 높이 이하의 목표를 입력하세요."));
        if (before.CrossSection.Columns.Any(column => column.SegmentsBottomToTop.Count == 0 || column.SegmentsBottomToTop.All(segment => segment.Material != WaferMaterial.Silicon)))
            errors.Add(Issue("CMP_SILICON_SUBSTRATE_REQUIRED", null, null, "silicon substrate present in each column", "실리콘 기판을 포함한 웨이퍼 상태를 사용하세요.", ExecutionStage.InvariantValidation));
        if (before.ResistPattern is not null)
            errors.Add(Issue("CMP_PATTERN_UNSUPPORTED", null, null, "unpatterned blanket surface", "현재 교육용 CMP 모델은 포토 패턴이 없는 blanket 표면만 지원합니다.", ExecutionStage.ModelDomain));
        if (errors.Count > 0) return Failure(errors);

        var removalMeters = parameters.RemovalRate.Meters * parameters.Duration.Seconds / 60.0;
        var targetMeters = parameters.TargetSurfaceHeight.Meters;
        var columns = new CrossSectionColumn[before.CrossSection.Columns.Count];
        var removedByMaterial = new Dictionary<WaferMaterial, double>();
        for (var index = 0; index < columns.Length; index++)
        {
            var column = before.CrossSection.Columns[index];
            var surface = column.SurfaceHeight.Meters;
            var desiredSurface = Math.Max(targetMeters, surface - removalMeters);
            var toRemove = surface - desiredSurface;
            var segments = column.SegmentsBottomToTop.ToArray();
            for (var segmentIndex = segments.Length - 1; segmentIndex >= 0 && toRemove > 0; segmentIndex--)
            {
                var segment = segments[segmentIndex];
                var removed = Math.Min(toRemove, segment.Thickness.Meters);
                if (segment.Material == WaferMaterial.Silicon && removed > 0)
                    return Failure(new[] { Issue("CMP_SILICON_BREAKTHROUGH", null, null, "silicon substrate thickness preserved", "실리콘 기판을 제거하지 않도록 목표 높이를 높이거나 제거량을 줄이세요.", ExecutionStage.InvariantValidation) });
                toRemove -= removed;
                if (removed > 0)
                    removedByMaterial[segment.Material] = removedByMaterial.GetValueOrDefault(segment.Material) + removed;
                if (segment.Thickness.Meters - removed <= 1e-15)
                    segments[segmentIndex] = segment with { Thickness = Length.FromMeters(0) };
                else
                    segments[segmentIndex] = segment with { Thickness = Length.FromMeters(segment.Thickness.Meters - removed) };
            }
            var retained = segments.Where(segment => segment.Thickness.Meters > 1e-15).ToArray();
            if (retained.Length == 0 || retained.All(segment => segment.Material != WaferMaterial.Silicon))
                return Failure(new[] { Issue("CMP_SILICON_BREAKTHROUGH", null, null, "Si substrate retained", "CMP가 기판을 제거하지 않도록 목표 높이를 높이거나 제거량을 줄이세요.", ExecutionStage.InvariantValidation) });
            columns[index] = column with
            {
                SegmentsBottomToTop = Array.AsReadOnly(retained),
                SurfaceHeight = Length.FromMeters(retained.Sum(segment => segment.Thickness.Meters))
            };
        }

        var retainedLayerIds = columns.SelectMany(column => column.SegmentsBottomToTop).Select(segment => segment.LayerId).ToHashSet();
        var candidate = before with
        {
            StateId = Guid.NewGuid(),
            ParentStateId = before.StateId,
            Revision = before.Revision + 1,
            CreatedAtUtc = nowUtc,
            CrossSection = before.CrossSection with { Columns = Array.AsReadOnly(columns) },
            Layers = Array.AsReadOnly(before.Layers.Where(layer => retainedLayerIds.Contains(layer.LayerId)).ToArray()),
            HistoryIds = Array.AsReadOnly(before.HistoryIds.Append(runId).ToArray())
        };
        var metrics = removedByMaterial.Select(pair => new ProcessMetric($"removed_{pair.Key}", pair.Value / columns.Length, "m/column")).ToList();
        metrics.Add(new ProcessMetric("cmpTargetHeight", parameters.TargetSurfaceHeight.Nanometers, "nm"));
        return Success(candidate, metrics.ToArray());
    }

    private static ProcessModelOutput Success(WaferState state, params ProcessMetric[] metrics) =>
        new(state, Array.AsReadOnly(metrics), ValidationResult.Success());

    private static ProcessModelOutput Failure(IReadOnlyList<ValidationIssue> issues) =>
        new(null, Array.Empty<ProcessMetric>(), new ValidationResult(false, issues, Array.Empty<ValidationIssue>()));

    private static ValidationIssue Issue(string code, string? parameterId, string? actual, string expected, string action, ExecutionStage stage = ExecutionStage.InputValidation) =>
        new(code, stage.ToString(), parameterId, actual, expected, code, action);
}
