using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;
using SemiconductorEdu.Domain.Validation;

namespace SemiconductorEdu.Simulation.Processes;

public sealed record ProcessModelOutput(WaferState? CandidateState, IReadOnlyList<ProcessMetric> Metrics, ValidationResult Validation);

public sealed class PatternProcessModels
{
    public ProcessModelOutput ApplyPhotolithography(
        WaferState before,
        PhotolithographyParameters parameters,
        Guid runId,
        DateTimeOffset nowUtc)
    {
        var errors = new List<ValidationIssue>();
        if (parameters.ResistThickness.Nanometers is < 100 or > 5000)
            errors.Add(Issue("PHOTO_THICKNESS_RANGE", "resistThickness", parameters.ResistThickness.Nanometers.ToString("G17"), "100–5000 nm", "감광액 두께를 100–5000 nm 범위로 입력하세요."));
        if (parameters.OpenIntervals.Count == 0)
            errors.Add(Issue("PHOTO_PATTERN_REQUIRED", "openIntervals", null, "1개 이상의 개구 구간", "단면 폭 안의 포토 패턴 개구 구간을 지정하세요."));
        if (parameters.OpenIntervals.Any(interval => interval.Start.Meters < 0 || interval.End.Meters > before.CrossSection.Width.Meters || interval.Start.Meters >= interval.End.Meters))
            errors.Add(Issue("PHOTO_PATTERN_RANGE", "openIntervals", null, $"0–{before.CrossSection.Width.Micrometers:G} µm", "개구 구간이 단면 폭 안에 있고 시작점보다 끝점이 큰지 확인하세요."));
        if (before.ResistPattern is not null)
            errors.Add(Issue("PHOTO_RESIST_ALREADY_PRESENT", "resistThickness", null, "감광액이 없는 웨이퍼", "기존 감광액을 세정한 후 포토 공정을 적용하세요.", ExecutionStage.Preconditions));
        if (before.CrossSection.Columns.Count < 2 || before.CrossSection.Resolution != before.CrossSection.Columns.Count)
            errors.Add(Issue("PHOTO_CROSS_SECTION_INVALID", null, null, "유효한 2D 단면 격자", "웨이퍼 단면을 다시 초기화하세요.", ExecutionStage.ModelDomain));
        if (parameters.OpenIntervals.OrderBy(interval => interval.Start.Meters).Zip(
                parameters.OpenIntervals.OrderBy(interval => interval.Start.Meters).Skip(1),
                (left, right) => left.End.Meters > right.Start.Meters).Any(overlaps => overlaps))
            errors.Add(Issue("PHOTO_PATTERN_OVERLAP", "openIntervals", null, "겹치지 않는 개구 구간", "중복되는 패턴 구간을 병합하거나 조정하세요."));
        if (before.Layers.Any(layer => layer.Material is not WaferMaterial.Silicon and not WaferMaterial.SiliconDioxide))
            errors.Add(Issue("PHOTO_UNSUPPORTED_STACK", null, null, "Si/SiO₂/Si 기판 적층", "현재 교육 모델은 Si와 SiO₂ 기판 위 포토 패턴만 지원합니다.", ExecutionStage.ModelDomain));
        if (errors.Count > 0) return Failure(errors);

        var patternId = Guid.NewGuid();
        var openIntervals = Array.AsReadOnly(parameters.OpenIntervals.OrderBy(interval => interval.Start.Meters).ToArray());
        var residualByColumn = new Length[before.CrossSection.Columns.Count];
        for (var index = 0; index < before.CrossSection.Columns.Count; index++)
        {
            var x = before.CrossSection.Columns[index].X.Meters;
            var isOpen = openIntervals.Any(interval => x >= interval.Start.Meters && x <= interval.End.Meters);
            residualByColumn[index] = isOpen ? Length.FromMeters(0) : parameters.ResistThickness;
        }

        var resistLayerId = Guid.NewGuid();
        var columns = before.CrossSection.Columns.Select((column, index) =>
        {
            var thickness = residualByColumn[index];
            if (thickness.Meters <= 0) return column;
            var segment = new MaterialSegment(resistLayerId, WaferMaterial.Photoresist, thickness, runId);
            return column with
            {
                SegmentsBottomToTop = Array.AsReadOnly(column.SegmentsBottomToTop.Append(segment).ToArray()),
                SurfaceHeight = Length.FromMeters(column.SurfaceHeight.Meters + thickness.Meters)
            };
        }).ToArray();
        var candidate = before with
        {
            StateId = Guid.NewGuid(),
            ParentStateId = before.StateId,
            Revision = before.Revision + 1,
            CreatedAtUtc = nowUtc,
            CrossSection = before.CrossSection with { Columns = Array.AsReadOnly(columns) },
            Layers = Array.AsReadOnly(before.Layers.Append(new LayerReference(resistLayerId, WaferMaterial.Photoresist, runId)).ToArray()),
            ResistPattern = new ResistPatternState(patternId, openIntervals, Array.AsReadOnly(residualByColumn)),
            HistoryIds = Array.AsReadOnly(before.HistoryIds.Append(runId).ToArray())
        };
        return Success(candidate, new ProcessMetric("resistThickness", parameters.ResistThickness.Nanometers, "nm"));
    }

    public ProcessModelOutput ApplyEtch(WaferState before, EtchParameters parameters, Guid runId, DateTimeOffset nowUtc)
    {
        var errors = new List<ValidationIssue>();
        if (before.ResistPattern is null)
            errors.Add(Issue("ETCH_PATTERN_REQUIRED", null, null, "포토 패턴 상태", "식각 전에 포토 공정으로 개구 패턴을 생성하세요.", ExecutionStage.Preconditions));
        if (before.ResistPattern is not null && before.ResistPattern.ResidualThicknessByColumn.Count != before.CrossSection.Columns.Count)
            errors.Add(Issue("ETCH_PATTERN_GRID_MISMATCH", "openIntervals", before.ResistPattern.ResidualThicknessByColumn.Count.ToString("G17"), before.CrossSection.Columns.Count.ToString("G17"), "포토 패턴과 단면 격자가 일치하는 웨이퍼 상태를 사용하세요.", ExecutionStage.ModelDomain));
        if (parameters.TargetMaterial is not WaferMaterial.Silicon and not WaferMaterial.SiliconDioxide)
            errors.Add(Issue("ETCH_MATERIAL_UNSUPPORTED", "targetMaterial", parameters.TargetMaterial.ToString(), "Si 또는 SiO₂", "v1 식각 모델은 Si와 SiO₂만 지원합니다."));
        if (parameters.EtchDepth.Nanometers is <= 0 or > 10000)
            errors.Add(Issue("ETCH_DEPTH_RANGE", "etchDepth", parameters.EtchDepth.Nanometers.ToString("G17"), "0–10000 nm", "양수이며 모델 상한 이하인 식각 깊이를 입력하세요."));
        if (!double.IsFinite(parameters.OverEtchPercent) || parameters.OverEtchPercent is < 0 or > 100)
            errors.Add(Issue("ETCH_OVERETCH_RANGE", "overEtchPercent", parameters.OverEtchPercent.ToString("G17"), "0–100%", "오버에치 비율을 0–100% 범위로 입력하세요."));
        if (errors.Count > 0) return Failure(errors);

        var pattern = before.ResistPattern!;
        var depthMeters = parameters.EtchDepth.Meters * (1 + parameters.OverEtchPercent / 100.0);
        var removedVolumeArea = 0.0;
        var columns = new CrossSectionColumn[before.CrossSection.Columns.Count];
        var cellWidth = before.CrossSection.Width.Meters / (before.CrossSection.Columns.Count - 1);
        for (var index = 0; index < columns.Length; index++)
        {
            var column = before.CrossSection.Columns[index];
            var x = column.X.Meters;
            var open = pattern.OpenIntervals.Any(interval => x >= interval.Start.Meters && x <= interval.End.Meters);
            if (!open)
            {
                columns[index] = column;
                continue;
            }

            var exposed = column.SegmentsBottomToTop.LastOrDefault();
            if (exposed is null || exposed.Material != parameters.TargetMaterial)
                return Failure(new[] { Issue("ETCH_TARGET_NOT_EXPOSED", "targetMaterial", parameters.TargetMaterial.ToString(), "target material at patterned opening", "포토 개구부에 지정 재료가 노출되도록 공정 순서를 확인하세요.", ExecutionStage.ModelDomain) });

            var remaining = depthMeters;
            var updatedSegments = new List<MaterialSegment>(column.SegmentsBottomToTop.Count);
            for (var segmentIndex = column.SegmentsBottomToTop.Count - 1; segmentIndex >= 0; segmentIndex--)
            {
                var segment = column.SegmentsBottomToTop[segmentIndex];
                if (remaining > 0 && segment.Material == parameters.TargetMaterial)
                {
                    var removed = Math.Min(remaining, segment.Thickness.Meters);
                    remaining -= removed;
                    removedVolumeArea += removed * cellWidth;
                    if (segment.Thickness.Meters - removed > 1e-15)
                        updatedSegments.Add(segment with { Thickness = Length.FromMeters(segment.Thickness.Meters - removed) });
                    continue;
                }

                updatedSegments.Add(segment);
                if (remaining > 0)
                {
                    for (var lowerIndex = segmentIndex - 1; lowerIndex >= 0; lowerIndex--)
                        updatedSegments.Add(column.SegmentsBottomToTop[lowerIndex]);
                    break;
                }
            }
            updatedSegments.Reverse();
            if (updatedSegments.Count == 0 || (parameters.TargetMaterial == WaferMaterial.Silicon && updatedSegments.All(segment => segment.Material != WaferMaterial.Silicon)))
                return Failure(new[] { Issue("ETCH_SILICON_BREAKTHROUGH", "etchDepth", parameters.EtchDepth.Nanometers.ToString("G17"), "Si substrate remains", "기판을 관통하는 식각 깊이를 줄이세요.", ExecutionStage.InvariantValidation) });

            var surfaceHeight = updatedSegments.Sum(segment => segment.Thickness.Meters);
            columns[index] = column with
            {
                SegmentsBottomToTop = Array.AsReadOnly(updatedSegments.ToArray()),
                SurfaceHeight = Length.FromMeters(surfaceHeight)
            };
        }

        var candidate = before with
        {
            StateId = Guid.NewGuid(),
            ParentStateId = before.StateId,
            Revision = before.Revision + 1,
            CreatedAtUtc = nowUtc,
            CrossSection = before.CrossSection with { Columns = Array.AsReadOnly(columns) },
            HistoryIds = Array.AsReadOnly(before.HistoryIds.Append(runId).ToArray())
        };
        return Success(candidate,
            new ProcessMetric("etchedCrossSectionArea", removedVolumeArea, "m²"),
            new ProcessMetric("etchDepth", parameters.EtchDepth.Nanometers, "nm"));
    }

    public ProcessModelOutput ApplyPhotoresistStrip(WaferState before, CleaningParameters parameters, Guid runId, DateTimeOffset nowUtc)
    {
        if (parameters.Method != CleaningMethod.PhotoresistStrip)
            return Failure(new[] { Issue("CLEANING_METHOD_UNSUPPORTED", "method", parameters.Method.ToString(), "PhotoresistStrip", "지원하는 PR 제거 세정 방식을 선택하세요.") });
        if (before.ResistPattern is null)
            return Failure(new[] { Issue("CLEANING_RESIST_REQUIRED", null, null, "감광액이 있는 상태", "제거할 감광액이 없습니다.", ExecutionStage.Preconditions) });

        var columns = before.CrossSection.Columns.Select(column =>
        {
            var removedResist = column.SegmentsBottomToTop.Where(segment => segment.Material == WaferMaterial.Photoresist).Sum(segment => segment.Thickness.Meters);
            var segments = column.SegmentsBottomToTop.Where(segment => segment.Material != WaferMaterial.Photoresist).ToArray();
            return column with
            {
                SegmentsBottomToTop = Array.AsReadOnly(segments),
                SurfaceHeight = Length.FromMeters(column.SurfaceHeight.Meters - removedResist)
            };
        }).ToArray();
        var candidate = before with
        {
            StateId = Guid.NewGuid(),
            ParentStateId = before.StateId,
            Revision = before.Revision + 1,
            CreatedAtUtc = nowUtc,
            CrossSection = before.CrossSection with { Columns = Array.AsReadOnly(columns) },
            Layers = Array.AsReadOnly(before.Layers.Where(layer => layer.Material != WaferMaterial.Photoresist).ToArray()),
            ResistPattern = null,
            HistoryIds = Array.AsReadOnly(before.HistoryIds.Append(runId).ToArray())
        };
        return Success(candidate, new ProcessMetric("photoresistRemaining", 0, "nm"));
    }

    private static ProcessModelOutput Success(WaferState state, params ProcessMetric[] metrics) =>
        new(state, Array.AsReadOnly(metrics), ValidationResult.Success());

    private static ProcessModelOutput Failure(IReadOnlyList<ValidationIssue> issues) =>
        new(null, Array.Empty<ProcessMetric>(), new ValidationResult(false, issues, Array.Empty<ValidationIssue>()));

    private static ValidationIssue Issue(
        string code,
        string? parameterId,
        string? actual,
        string expected,
        string action,
        ExecutionStage stage = ExecutionStage.InputValidation) =>
        new(code, stage.ToString(), parameterId, actual, expected, code, action);
}
