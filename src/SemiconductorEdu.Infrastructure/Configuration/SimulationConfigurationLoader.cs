using System.Text.Json;
using SemiconductorEdu.Domain.Validation;

namespace SemiconductorEdu.Infrastructure.Configuration;

public sealed class SimulationConfigurationLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false
    };

    public ConfigurationLoadResult Load(string json)
    {
        SimulationConfiguration? configuration;
        try
        {
            configuration = JsonSerializer.Deserialize<SimulationConfiguration>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            return Invalid("CONFIG_JSON_INVALID", exception.Message);
        }

        if (configuration is null)
            return Invalid("CONFIG_EMPTY", "설정 문서가 비어 있습니다.");

        var errors = new List<ValidationIssue>();
        if (configuration.SchemaVersion != 1)
            errors.Add(Issue("CONFIG_SCHEMA_UNSUPPORTED", "schemaVersion", configuration.SchemaVersion.ToString(), "1", "지원하는 설정 스키마 버전으로 저장하세요."));
        if (string.IsNullOrWhiteSpace(configuration.ConfigurationVersion) ||
            string.IsNullOrWhiteSpace(configuration.DefinitionVersion) ||
            string.IsNullOrWhiteSpace(configuration.ModelVersion))
            errors.Add(Issue("CONFIG_VERSION_REQUIRED", "version", null, "configuration/definition/model version", "버전 정보를 모두 입력하세요."));

        var model = configuration.Oxidation;
        if (model is null)
            errors.Add(Issue("CONFIG_OXIDATION_REQUIRED", "oxidation", null, "oxidation configuration", "산화 모델 설정을 추가하세요."));
        else
        {
            CheckPositive(errors, "oxidation.A_nm", model.A_Nanometers);
            CheckPositive(errors, "oxidation.dryB0_nm2PerMin", model.DryB0_NanometersSquaredPerMinute);
            CheckPositive(errors, "oxidation.wetB0_nm2PerMin", model.WetB0_NanometersSquaredPerMinute);
            CheckPositive(errors, "oxidation.temperatureScaleC", model.TemperatureScaleCelsius);
            CheckPositive(errors, "oxidation.referencePressureTorr", model.ReferencePressureTorr);
            CheckPositive(errors, "oxidation.maximumOxideThicknessNm", model.MaximumOxideThicknessNanometers);
            if (!double.IsFinite(model.ReferenceTemperatureCelsius))
                errors.Add(Issue("CONFIG_NONFINITE", "oxidation.referenceTemperatureC", model.ReferenceTemperatureCelsius.ToString(), "finite number", "유한한 수를 입력하세요."));
            if (!double.IsFinite(model.SiliconConsumptionRatio) || model.SiliconConsumptionRatio <= 0 || model.SiliconConsumptionRatio >= 1)
                errors.Add(Issue("CONFIG_RATIO_RANGE", "oxidation.siliconConsumptionRatio", model.SiliconConsumptionRatio.ToString(), "0 < ratio < 1", "Si 소모 비율을 0과 1 사이로 설정하세요."));
            if (string.IsNullOrWhiteSpace(model.Version))
                errors.Add(Issue("CONFIG_MODEL_VERSION_REQUIRED", "oxidation.version", null, "non-empty version", "모델 버전을 입력하세요."));
        }

        if (configuration.Equipment is null)
            errors.Add(Issue("CONFIG_EQUIPMENT_REQUIRED", "equipment", null, "equipment profile", "장비 프로파일을 추가하세요."));
        else
        {
            if (string.IsNullOrWhiteSpace(configuration.Equipment.Id))
                errors.Add(Issue("CONFIG_EQUIPMENT_ID_REQUIRED", "equipment.id", null, "non-empty id", "장비 프로파일 ID를 입력하세요."));
            if (configuration.Equipment.AllowedWaferDiametersMm.Count == 0 || configuration.Equipment.AllowedWaferDiametersMm.Any(diameter => diameter <= 0))
                errors.Add(Issue("CONFIG_WAFER_DIAMETERS_INVALID", "equipment.allowedWaferDiametersMm", null, "positive diameter entries", "양수 직경을 하나 이상 설정하세요."));
            foreach (var (id, limits) in configuration.Equipment.Limits)
            {
                if (!double.IsFinite(limits.Minimum) || !double.IsFinite(limits.Maximum) || limits.Minimum > limits.Maximum || string.IsNullOrWhiteSpace(limits.Unit))
                    errors.Add(Issue("CONFIG_EQUIPMENT_LIMIT_INVALID", $"equipment.limits.{id}", null, "finite minimum <= maximum and unit", "장비 범위와 단위를 확인하세요."));
            }
        }

        if (configuration.Quality is null)
            errors.Add(Issue("CONFIG_QUALITY_REQUIRED", "quality", null, "quality profile", "품질 프로파일을 추가하세요."));
        else
        {
            if (string.IsNullOrWhiteSpace(configuration.Quality.Id) || string.IsNullOrWhiteSpace(configuration.Quality.Version))
                errors.Add(Issue("CONFIG_QUALITY_ID_REQUIRED", "quality", null, "profile id and version", "품질 프로파일 ID와 버전을 입력하세요."));
            var duplicateMetric = configuration.Quality.Targets.GroupBy(target => target.MetricId, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
            if (duplicateMetric is not null)
                errors.Add(Issue("CONFIG_QUALITY_DUPLICATE_METRIC", "quality.targets", duplicateMetric.Key, "unique metric ids", "중복 품질 지표를 제거하세요."));
            foreach (var target in configuration.Quality.Targets)
            {
                if (string.IsNullOrWhiteSpace(target.MetricId) || string.IsNullOrWhiteSpace(target.Unit) ||
                    !double.IsFinite(target.Target) ||
                    (target.TolerancePercent is not null && (!double.IsFinite(target.TolerancePercent.Value) || target.TolerancePercent < 0)) ||
                    (target.AbsoluteTolerance is not null && (!double.IsFinite(target.AbsoluteTolerance.Value) || target.AbsoluteTolerance < 0)) ||
                    (target.Minimum is not null && !double.IsFinite(target.Minimum.Value)) ||
                    (target.Maximum is not null && !double.IsFinite(target.Maximum.Value)) ||
                    (target.Minimum is not null && target.Maximum is not null && target.Minimum > target.Maximum))
                    errors.Add(Issue("CONFIG_QUALITY_TARGET_INVALID", $"quality.targets.{target.MetricId}", null, "finite target/tolerances and valid limits", "품질 목표의 값·단위·허용오차를 검토하세요."));
            }
        }

        if (configuration.EnabledProcesses is null || configuration.EnabledProcesses.Count == 0)
            errors.Add(Issue("CONFIG_ENABLED_PROCESSES_REQUIRED", "enabledProcesses", null, "at least one supported process", "구현 완료된 공정만 명시적으로 활성화하세요."));
        else
        {
            var distinct = configuration.EnabledProcesses.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (distinct != configuration.EnabledProcesses.Count)
                errors.Add(Issue("CONFIG_ENABLED_PROCESSES_DUPLICATE", "enabledProcesses", null, "unique process names", "중복 공정 ID를 제거하세요."));
            var supportedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "WaferInitialization", "Oxidation", "Photolithography", "Etch", "Cleaning"
            };
            if (configuration.EnabledProcesses.Any(process => !supportedProcesses.Contains(process)))
                errors.Add(Issue("CONFIG_UNIMPLEMENTED_PROCESS_ENABLED", "enabledProcesses", string.Join(',', configuration.EnabledProcesses), "implemented Stage 1 and Stage 2 process models only", "아직 구현되지 않은 공정은 비활성화하세요."));
        }

        return errors.Count == 0
            ? new ConfigurationLoadResult(configuration, Array.Empty<ValidationIssue>())
            : new ConfigurationLoadResult(null, Array.AsReadOnly(errors.ToArray()));
    }

    public ConfigurationLoadResult LoadFile(string path) => Load(File.ReadAllText(path));

    private static void CheckPositive(ICollection<ValidationIssue> errors, string id, double value)
    {
        if (!double.IsFinite(value) || value <= 0)
            errors.Add(Issue("CONFIG_POSITIVE_VALUE_REQUIRED", id, value.ToString(), "> 0 finite", "양의 유한한 값을 입력하세요."));
    }

    private static ConfigurationLoadResult Invalid(string code, string message) =>
        new(null, new[] { Issue(code, "json", null, "valid JSON configuration", message) });

    private static ValidationIssue Issue(string code, string parameter, string? actual, string expected, string message) =>
        new(code, "Configuration", parameter, actual, expected, message, "JSON 설정을 수정한 뒤 다시 불러오세요.");
}
