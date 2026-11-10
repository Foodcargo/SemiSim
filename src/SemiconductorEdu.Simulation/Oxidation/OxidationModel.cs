using System.Globalization;
using SemiconductorEdu.Domain.Equipment;
using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;
using SemiconductorEdu.Domain.Validation;

namespace SemiconductorEdu.Simulation.Oxidation;

public sealed record OxidationCoefficients(
    double A_Nanometers = 100,
    double DryB0_NanometersSquaredPerMinute = 200,
    double WetB0_NanometersSquaredPerMinute = 800,
    double ReferenceTemperatureCelsius = 1000,
    double TemperatureScaleCelsius = 150,
    double ReferencePressureTorr = 760,
    double SiliconConsumptionRatio = 0.44,
    double MaximumOxideThicknessNanometers = 2000,
    string Version = "1.0.0");

public sealed record OxidationModelOutput(
    WaferState? CandidateState,
    IReadOnlyList<ProcessMetric> Metrics,
    ValidationResult Validation);

public sealed record OxidationQualityEvaluation(
    QualityStatus Status,
    ProcessMetric Metric,
    IReadOnlyList<ValidationIssue> Errors);

public sealed class OxidationModel(OxidationCoefficients? coefficients = null)
{
    private readonly OxidationCoefficients _coefficients = coefficients ?? new OxidationCoefficients();

    public ValidationResult ValidateInput(OxidationParameters parameters)
    {
        var errors = new List<ValidationIssue>();
        CheckRange(errors, "temperature", "온도", parameters.Temperature.Celsius, 700, 1200, "°C");
        CheckRange(errors, "duration", "시간", parameters.Duration.Minutes, 1, 240, "min");
        CheckRange(errors, "pressure", "압력", parameters.Pressure.Torr, 1, 760, "Torr");
        if (!Enum.IsDefined(parameters.Method))
            errors.Add(Issue("INVALID_ENUM", "method", parameters.Method.ToString(), "Dry 또는 Wet", "산화 방식 선택을 확인하세요."));
        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(false, errors, Array.Empty<ValidationIssue>());
    }

    public ValidationResult CheckPreconditions(WaferState? wafer)
    {
        var errors = new List<ValidationIssue>();
        if (wafer is null)
            errors.Add(Issue("PRECONDITION_WAFER_REQUIRED", null, null, "초기화된 웨이퍼 상태", "먼저 웨이퍼를 초기화하세요.", ExecutionStage.Preconditions));
        else if (wafer.Revision < 0 || wafer.StateId == Guid.Empty)
            errors.Add(Issue("PRECONDITION_INVALID_WAFER_STATE", null, wafer.Revision.ToString(CultureInfo.InvariantCulture), "유효한 웨이퍼 상태 ID 및 revision", "웨이퍼 상태를 다시 초기화하세요.", ExecutionStage.Preconditions));
        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(false, errors, Array.Empty<ValidationIssue>());
    }

    public ValidationResult CheckInterlock(WaferState wafer, OxidationParameters parameters, EquipmentSnapshot equipment)
    {
        var errors = new List<ValidationIssue>();
        if (!equipment.DoorClosed)
            errors.Add(Issue("INTERLOCK_DOOR_OPEN", null, "false", "DoorClosed=true", "교육용 장비 상태에서 도어 닫힘을 설정하세요.", ExecutionStage.EquipmentInterlock));
        if (equipment.EmergencyStop)
            errors.Add(Issue("INTERLOCK_EMERGENCY_STOP", null, "true", "EmergencyStop=false", "교육용 비상정지 상태를 해제한 뒤 다시 검사하세요.", ExecutionStage.EquipmentInterlock));
        if (!equipment.Available)
            errors.Add(Issue("INTERLOCK_UNAVAILABLE", null, "false", "Available=true", "교육용 장비 사용 가능 상태를 확인하세요.", ExecutionStage.EquipmentInterlock));
        if (!equipment.AllowedWaferDiametersMm.Contains(wafer.Substrate.DiameterMm))
            errors.Add(Issue("INTERLOCK_WAFER_DIAMETER", "diameter", wafer.Substrate.DiameterMm.ToString(CultureInfo.InvariantCulture), "장비 허용 직경", "허용되는 웨이퍼 직경으로 초기화하세요.", ExecutionStage.EquipmentInterlock));

        CheckRange(errors, "temperature", "온도", parameters.Temperature.Celsius, 800, 1100, "°C", ExecutionStage.EquipmentInterlock, "INTERLOCK_TEMPERATURE");
        CheckRange(errors, "pressure", "압력", parameters.Pressure.Torr, 100, 760, "Torr", ExecutionStage.EquipmentInterlock, "INTERLOCK_PRESSURE");
        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(false, errors, Array.Empty<ValidationIssue>());
    }

    public ValidationResult CheckDomain(WaferState wafer)
    {
        var errors = new List<ValidationIssue>();
        if (wafer.ResistPattern is not null)
            errors.Add(Issue("OXIDATION_PATTERN_UNSUPPORTED", null, null, "패턴 없는 평탄 표면", "v1 산화 모델은 포토 패턴이 없는 표면만 지원합니다.", ExecutionStage.ModelDomain));
        if (wafer.Wires.Count > 0)
            errors.Add(Issue("OXIDATION_METAL_UNSUPPORTED", null, null, "금속 배선 없음", "금속 배선이 없는 상태에서 산화를 실행하세요.", ExecutionStage.ModelDomain));
        if (wafer.ImplantProfiles.Count > 0)
            errors.Add(Issue("OXIDATION_IMPLANT_UNSUPPORTED", null, null, "주입 프로파일 없음", "주입 프로파일과 산화의 연계는 v1 모델 범위 밖입니다.", ExecutionStage.ModelDomain));

        if (wafer.CrossSection.Columns.Count == 0)
            errors.Add(Issue("OXIDATION_EMPTY_CROSS_SECTION", null, null, "단면 열 1개 이상", "유효한 웨이퍼 단면을 초기화하세요.", ExecutionStage.ModelDomain));
        else
        {
            var reference = wafer.CrossSection.Columns[0].SegmentsBottomToTop;
            for (var index = 0; index < wafer.CrossSection.Columns.Count; index++)
            {
                var segments = wafer.CrossSection.Columns[index].SegmentsBottomToTop;
                if (!HaveSameStack(reference, segments))
                {
                    errors.Add(Issue("OXIDATION_NON_PLANAR_SURFACE", null, index.ToString(CultureInfo.InvariantCulture), "모든 단면 열의 동일한 재료 적층", "평탄 Si 또는 균일 SiO₂/Si 상태에서만 실행할 수 있습니다.", ExecutionStage.ModelDomain));
                    break;
                }

                var siliconCount = 0;
                var oxideSeen = false;
                foreach (var segment in segments)
                {
                    if (segment.Material == WaferMaterial.Silicon && !oxideSeen)
                    {
                        siliconCount++;
                        continue;
                    }
                    if (segment.Material == WaferMaterial.SiliconDioxide && siliconCount > 0)
                    {
                        oxideSeen = true;
                        continue;
                    }
                    errors.Add(Issue("OXIDATION_UNSUPPORTED_STACK", null, segment.Material.ToString(), "Si 또는 SiO₂/Si 적층", "현재 표면 재료와 적층 순서는 v1 산화 모델에서 지원하지 않습니다.", ExecutionStage.ModelDomain));
                    break;
                }
                if (errors.Count > 0) break;
                if (siliconCount == 0)
                {
                    errors.Add(Issue("OXIDATION_SILICON_MISSING", null, null, "산화 가능한 Si 기판", "Si 기판이 있는 상태에서 산화를 실행하세요.", ExecutionStage.ModelDomain));
                    break;
                }
            }
        }

        foreach (var layer in wafer.Layers)
        {
            if (layer.Material is not WaferMaterial.Silicon and not WaferMaterial.SiliconDioxide)
            {
                errors.Add(Issue("OXIDATION_UNSUPPORTED_MATERIAL", null, layer.Material.ToString(), "Si 또는 SiO₂만 존재", "PR·금속 등 지원하지 않는 재료를 제거하거나 새 웨이퍼를 초기화하세요.", ExecutionStage.ModelDomain));
                break;
            }
        }

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(false, errors, Array.Empty<ValidationIssue>());
    }

    public OxidationModelOutput Calculate(
        WaferState before,
        OxidationParameters parameters,
        Guid runId,
        DateTimeOffset nowUtc)
    {
        var input = ValidateInput(parameters);
        var domain = CheckDomain(before);
        if (!input.IsValid || !domain.IsValid)
        {
            var errors = input.Errors.Concat(domain.Errors).ToArray();
            return new OxidationModelOutput(null, Array.Empty<ProcessMetric>(), new ValidationResult(false, errors, Array.Empty<ValidationIssue>()));
        }

        var oldOxideNanometers = before.CrossSection.Columns[0].SegmentsBottomToTop
            .Where(segment => segment.Material == WaferMaterial.SiliconDioxide)
            .Sum(segment => segment.Thickness.Nanometers);
        var temperatureFactor = Math.Exp((parameters.Temperature.Celsius - _coefficients.ReferenceTemperatureCelsius) / _coefficients.TemperatureScaleCelsius);
        var pressureFactor = parameters.Pressure.Torr / _coefficients.ReferencePressureTorr;
        var baseB = parameters.Method == OxidationMethod.Dry
            ? _coefficients.DryB0_NanometersSquaredPerMinute
            : _coefficients.WetB0_NanometersSquaredPerMinute;
        var b = baseB * temperatureFactor * pressureFactor;
        var radicand = Math.Pow(_coefficients.A_Nanometers + 2 * oldOxideNanometers, 2)
            + 4 * b * parameters.Duration.Minutes;
        if (!double.IsFinite(radicand) || radicand < 0)
            return CalculationError("OXIDATION_NONFINITE_RESULT", "산화 계산 중 유한하지 않은 중간값이 발생했습니다.");

        var newOxideNanometers = (-_coefficients.A_Nanometers + Math.Sqrt(radicand)) / 2;
        var growthNanometers = newOxideNanometers - oldOxideNanometers;
        var siliconConsumedNanometers = _coefficients.SiliconConsumptionRatio * growthNanometers;
        if (!double.IsFinite(newOxideNanometers) || !double.IsFinite(growthNanometers) || growthNanometers < 0)
            return CalculationError("OXIDATION_NONFINITE_RESULT", "산화 계산 결과가 유한한 비음수 값이 아닙니다.");
        if (newOxideNanometers > _coefficients.MaximumOxideThicknessNanometers)
            return CalculationError("OXIDATION_THICKNESS_DOMAIN", "산화막 두께가 모델 적용 상한을 초과했습니다.", ExecutionStage.ModelDomain);

        var firstSegments = before.CrossSection.Columns[0].SegmentsBottomToTop;
        var siliconIndex = -1;
        for (var index = 0; index < firstSegments.Count; index++)
            if (firstSegments[index].Material == WaferMaterial.Silicon) { siliconIndex = index; break; }
        if (siliconIndex < 0 || firstSegments[siliconIndex].Thickness.Nanometers <= siliconConsumedNanometers)
            return CalculationError("OXIDATION_SILICON_CONSUMED", "산화에 필요한 Si 두께가 남아 있지 않습니다.", ExecutionStage.InvariantValidation);

        var grownLayerId = Guid.NewGuid();
        var updatedColumns = new CrossSectionColumn[before.CrossSection.Columns.Count];
        for (var columnIndex = 0; columnIndex < before.CrossSection.Columns.Count; columnIndex++)
        {
            var column = before.CrossSection.Columns[columnIndex];
            var segments = column.SegmentsBottomToTop.ToArray();
            var silicon = segments[siliconIndex];
            segments[siliconIndex] = silicon with
            {
                Thickness = Length.FromNanometers(silicon.Thickness.Nanometers - siliconConsumedNanometers)
            };
            var grownOxide = new MaterialSegment(
                grownLayerId,
                WaferMaterial.SiliconDioxide,
                Length.FromNanometers(growthNanometers),
                runId);
            segments = segments.Append(grownOxide).ToArray();
            var surfaceDelta = Length.FromNanometers(growthNanometers - siliconConsumedNanometers);
            updatedColumns[columnIndex] = column with
            {
                SegmentsBottomToTop = Array.AsReadOnly(segments),
                SurfaceHeight = Length.FromMeters(column.SurfaceHeight.Meters + surfaceDelta.Meters)
            };
        }

        var candidate = before with
        {
            StateId = Guid.NewGuid(),
            ParentStateId = before.StateId,
            Revision = before.Revision + 1,
            CreatedAtUtc = nowUtc,
            CrossSection = before.CrossSection with { Columns = Array.AsReadOnly(updatedColumns) },
            Layers = Array.AsReadOnly(before.Layers.Append(new LayerReference(grownLayerId, WaferMaterial.SiliconDioxide, runId)).ToArray()),
            HistoryIds = Array.AsReadOnly(before.HistoryIds.Append(runId).ToArray())
        };

        var metrics = new[]
        {
            new ProcessMetric("oxideThickness", newOxideNanometers, "nm"),
            new ProcessMetric("oxideGrowth", growthNanometers, "nm"),
            new ProcessMetric("siliconConsumed", siliconConsumedNanometers, "nm")
        };
        return new OxidationModelOutput(candidate, metrics, ValidationResult.Success());
    }

    public OxidationQualityEvaluation EvaluateThicknessQuality(Length actualThickness, QualityTarget? target)
    {
        var metric = new ProcessMetric("oxideThickness", actualThickness.Nanometers, "nm");
        if (target is null)
        {
            return new OxidationQualityEvaluation(
                QualityStatus.NotEvaluated,
                metric with { NotAvailableCode = "QUALITY_TARGET_MISSING", Explanation = "필수 두께 목표가 설정되지 않았습니다." },
                new[] { Issue("QUALITY_TARGET_MISSING", "oxideThickness", null, "필수 품질 목표", "품질 프로파일 설정을 확인하세요.", ExecutionStage.QualityEvaluation) });
        }
        if (!target.Required)
            return new OxidationQualityEvaluation(QualityStatus.NotEvaluated, metric, Array.Empty<ValidationIssue>());
        if (target.TolerancePercent is null || target.Target < 0 || target.TolerancePercent < 0)
        {
            return new OxidationQualityEvaluation(
                QualityStatus.NotEvaluated,
                metric with { NotAvailableCode = "INVALID_QUALITY_TARGET", Explanation = "두께 목표 또는 허용오차 설정이 유효하지 않습니다." },
                new[] { Issue("INVALID_QUALITY_TARGET", "oxideThickness", null, "유효한 target 및 tolerancePercent", "품질 프로파일을 검증하세요.", ExecutionStage.Configuration) });
        }

        var lower = target.Target * (1 - target.TolerancePercent.Value / 100.0);
        var upper = target.Target * (1 + target.TolerancePercent.Value / 100.0);
        var lowerEpsilon = 1e-9 * Math.Max(1, Math.Max(Math.Abs(actualThickness.Nanometers), Math.Abs(lower)));
        var upperEpsilon = 1e-9 * Math.Max(1, Math.Max(Math.Abs(actualThickness.Nanometers), Math.Abs(upper)));
        var pass = actualThickness.Nanometers >= lower - lowerEpsilon && actualThickness.Nanometers <= upper + upperEpsilon;
        return new OxidationQualityEvaluation(pass ? QualityStatus.Pass : QualityStatus.Fail, metric, Array.Empty<ValidationIssue>());
    }

    private static bool HaveSameStack(IReadOnlyList<MaterialSegment> left, IReadOnlyList<MaterialSegment> right)
    {
        if (left.Count != right.Count) return false;
        for (var index = 0; index < left.Count; index++)
        {
            if (left[index].Material != right[index].Material ||
                Math.Abs(left[index].Thickness.Meters - right[index].Thickness.Meters) > 1e-12)
                return false;
        }
        return true;
    }

    private static void CheckRange(
        ICollection<ValidationIssue> errors,
        string parameterId,
        string displayName,
        double value,
        double minimum,
        double maximum,
        string unit,
        ExecutionStage stage = ExecutionStage.InputValidation,
        string? code = null)
    {
        if (value >= minimum && value <= maximum) return;
        errors.Add(Issue(
            code ?? "INVALID_INPUT_RANGE",
            parameterId,
            value.ToString("G17", CultureInfo.InvariantCulture),
            $"{minimum.ToString("G", CultureInfo.InvariantCulture)}–{maximum.ToString("G", CultureInfo.InvariantCulture)} {unit}",
            $"{displayName} 입력 범위를 확인하세요.",
            stage));
    }

    private static ValidationIssue Issue(
        string code,
        string? parameterId,
        string? actualValue,
        string expected,
        string suggestedAction,
        ExecutionStage stage = ExecutionStage.InputValidation) =>
        new(code, stage.ToString(), parameterId, actualValue, expected, $"{code}: {expected}", suggestedAction);

    private static OxidationModelOutput CalculationError(string code, string message, ExecutionStage stage = ExecutionStage.Calculation)
    {
        var issue = new ValidationIssue(code, stage.ToString(), null, null, "유한하고 모델 범위 내의 결과", message, "입력과 모델 설정을 확인하세요.");
        return new OxidationModelOutput(null, Array.Empty<ProcessMetric>(), ValidationResult.Failure(issue));
    }
}
