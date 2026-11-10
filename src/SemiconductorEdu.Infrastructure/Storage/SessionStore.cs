using System.Text.Json;
using System.Text.Json.Serialization;
using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Validation;

namespace SemiconductorEdu.Infrastructure.Storage;

public sealed record SessionLoadResult(SessionDocument? Document, IReadOnlyList<ValidationIssue> Errors)
{
    public bool IsValid => Document is not null && Errors.Count == 0;
}

public sealed class SessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    public async Task SaveAsync(string path, SessionDocument document, CancellationToken cancellationToken = default)
    {
        ValidateForSave(document);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = fullPath + ".tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        File.Move(temporaryPath, fullPath, true);
    }

    public async Task<SessionLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var document = await JsonSerializer.DeserializeAsync<SessionDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            return document is null ? Invalid("SESSION_EMPTY", "세션 파일이 비어 있습니다.") : Validate(document);
        }
        catch (JsonException exception)
        {
            return Invalid("SESSION_JSON_INVALID", exception.Message);
        }
        catch (IOException exception)
        {
            return Invalid("SESSION_IO_ERROR", exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Invalid("SESSION_ACCESS_DENIED", exception.Message);
        }
    }

    private static SessionLoadResult Validate(SessionDocument document)
    {
        var errors = new List<ValidationIssue>();
        if (document.SchemaVersion != 1)
            errors.Add(Issue("SESSION_SCHEMA_UNSUPPORTED", document.SchemaVersion.ToString(), "1", "지원하는 세션 스키마로 저장된 파일을 선택하세요."));
        if (document.ApplicationVersion is null || document.ConfigurationVersion is null || document.ModelVersion is null)
            errors.Add(Issue("SESSION_VERSION_MISSING", null, "application/configuration/model version", "버전 정보가 있는 세션 파일을 선택하세요."));

        var stateIds = new HashSet<Guid>();
        foreach (var state in document.States)
        {
            if (state.StateId == Guid.Empty || !stateIds.Add(state.StateId))
                errors.Add(Issue("SESSION_STATE_ID_INVALID", state.StateId.ToString(), "unique non-empty state IDs", "세션의 상태 식별자를 확인하세요."));
            if (state.Revision < 0 || state.CrossSection.Resolution < 2 || state.CrossSection.Columns.Count != state.CrossSection.Resolution)
                errors.Add(Issue("SESSION_STATE_SHAPE_INVALID", state.StateId.ToString(), "consistent nonnegative revision and cross-section resolution", "손상된 단면 상태를 제거하거나 다시 저장하세요."));
            if (state.CrossSection.Columns.Any(column => column.SegmentsBottomToTop is null || column.SegmentsBottomToTop.Any(segment => !double.IsFinite(segment.Thickness.Meters) || segment.Thickness.Meters < 0)))
                errors.Add(Issue("SESSION_SEGMENT_INVALID", state.StateId.ToString(), "finite nonnegative segment thickness", "세션 상태의 재료 두께를 확인하세요."));
        }

        if (document.CurrentStateId is Guid currentId && !stateIds.Contains(currentId))
            errors.Add(Issue("SESSION_CURRENT_STATE_MISSING", currentId.ToString(), "an ID in states", "현재 상태를 포함하는 세션을 다시 불러오세요."));
        if (document.History.Any(result => result.RunId == Guid.Empty || !Enum.IsDefined(result.ExecutionStatus) || !Enum.IsDefined(result.QualityStatus)))
            errors.Add(Issue("SESSION_HISTORY_INVALID", null, "valid run IDs and status values", "손상된 실행 이력을 확인하세요."));
        if (document.History.Any(result => result.AfterStateId is Guid afterId && !stateIds.Contains(afterId)))
            errors.Add(Issue("SESSION_HISTORY_STATE_MISSING", null, "history state IDs present in states", "결과가 참조하는 상태를 복구하거나 해당 이력을 제거하세요."));

        return errors.Count == 0 ? new SessionLoadResult(document, Array.Empty<ValidationIssue>()) : new SessionLoadResult(null, Array.AsReadOnly(errors.ToArray()));
    }

    private static void ValidateForSave(SessionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != 1)
            throw new ArgumentException("Only session schema version 1 can be saved.", nameof(document));
        if (document.CurrentStateId is Guid currentId && document.States.All(state => state.StateId != currentId))
            throw new ArgumentException("Current state must be included in the session state list.", nameof(document));
    }

    private static SessionLoadResult Invalid(string code, string message) => new(null, new[] { Issue(code, null, "valid session document", message) });

    private static ValidationIssue Issue(string code, string? actual, string expected, string action) =>
        new(code, "SessionLoad", null, actual, expected, code, action);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new LengthJsonConverter());
        options.Converters.Add(new DurationJsonConverter());
        options.Converters.Add(new PressureJsonConverter());
        options.Converters.Add(new TemperatureJsonConverter());
        options.Converters.Add(new ConcentrationJsonConverter());
        options.Converters.Add(new ArealDoseJsonConverter());
        options.Converters.Add(new EnergyJsonConverter());
        options.Converters.Add(new AngularSpeedJsonConverter());
        options.Converters.Add(new SccmJsonConverter());
        options.Converters.Add(new PercentJsonConverter());
        return options;
    }
}
