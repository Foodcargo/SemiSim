using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SemiEdu.Engine;

/// <summary>
/// Managed implementation of the C ABI. JSON is produced with Utf8JsonWriter and parsed with JsonDocument
/// (no reflection-based serialization, NativeAOT-safe). Every entry point returns {"ok":true,...} or {"ok":false,"error":{...}}.
/// </summary>
public static class Api
{
    public const int AbiVersion = 1;
    public const string EngineVersion = "1.0.0";
    public const string ModelVersion = "edu-1.0";
    public const string EducationalNote = "검증되지 않은 교육용 예시 모델";

    static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    static string Write(Action<Utf8JsonWriter> body)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, WriterOptions))
        {
            w.WriteStartObject();
            body(w);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static string Error(string code, string message) => Write(w =>
    {
        w.WriteBoolean("ok", false);
        w.WriteStartObject("error");
        w.WriteString("Code", code);
        w.WriteString("MessageKo", message);
        w.WriteEndObject();
    });

    static string Guard(Func<string> f)
    {
        try { return f(); }
        catch (JsonException ex) { return Error("INVALID_JSON", "JSON 형식 오류: " + ex.Message); }
        catch (Exception ex) { return Error("ENGINE_ERROR", ex.Message); }
    }

    static void Num(Utf8JsonWriter w, string name, double? v)
    {
        if (v is double d && double.IsFinite(d)) w.WriteNumber(name, d);
        else w.WriteNull(name);
    }

    // ---------------- parsing ----------------

    public static (Dictionary<string, string> Values, Equipment? Eq) ParseValues(string? json)
    {
        var values = new Dictionary<string, string>();
        Equipment? eq = null;
        if (string.IsNullOrWhiteSpace(json)) return (values, null);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("valuesJson은 객체여야 합니다.");
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            if (p.Name == "$equipment") { eq = ParseEquipmentElement(p.Value); continue; }
            values[p.Name] = p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString() ?? "",
                JsonValueKind.Number => p.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => throw new JsonException($"'{p.Name}' 값은 문자열이어야 합니다."),
            };
        }
        return (values, eq);
    }

    static Equipment ParseEquipmentElement(JsonElement e)
    {
        var eq = new Equipment();
        if (e.ValueKind != JsonValueKind.Object) return eq;
        foreach (var p in e.EnumerateObject())
        {
            bool b = p.Value.ValueKind == JsonValueKind.True;
            switch (p.Name)
            {
                case "doorClosed": eq.DoorClosed = b; break;
                case "emergencyStop": eq.EmergencyStop = b; break;
                case "available": eq.Available = b; break;
                case "continueOnFailure": eq.ContinueOnFailure = b; break;
            }
        }
        return eq;
    }

    public static Equipment ParseEquipment(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Equipment();
        using var doc = JsonDocument.Parse(json);
        return ParseEquipmentElement(doc.RootElement);
    }

    public static SceneOptions ParseSceneOptions(string? json)
    {
        var o = new SceneOptions();
        if (string.IsNullOrWhiteSpace(json)) return o;
        using var doc = JsonDocument.Parse(json);
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            switch (p.Name)
            {
                case "preview": o.Preview = p.Value.ValueKind == JsonValueKind.True; break;
                case "classificationView": o.Classification = p.Value.ValueKind == JsonValueKind.True; break;
                case "view": o.View = p.Value.GetString() ?? "wafer"; break;
            }
        }
        return o;
    }

    // ---------------- entry points ----------------

    public static string Version() => Write(w =>
    {
        w.WriteBoolean("ok", true);
        w.WriteNumber("abiVersion", AbiVersion);
        w.WriteString("engineVersion", EngineVersion);
        w.WriteString("modelVersion", ModelVersion);
        w.WriteString("note", EducationalNote);
    });

    public static string Catalog(Session s) => Guard(() => Write(w =>
    {
        w.WriteBoolean("ok", true);
        w.WriteStartArray("groups");
        foreach (var g in Engine.Catalog.Groups) w.WriteStringValue(g);
        w.WriteEndArray();
        w.WriteStartArray("processes");
        foreach (var p in Engine.Catalog.All)
        {
            w.WriteStartObject();
            w.WriteString("group", p.Group);
            w.WriteString("processId", p.Id);
            w.WriteString("name", p.Name);
            w.WriteBoolean("enabled", p.Enabled);
            w.WriteString("reason", p.DisabledReason);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }));

    static void WriteField(Utf8JsonWriter w, FieldView f)
    {
        w.WriteStartObject();
        w.WriteString("paramId", f.Def.Id);
        w.WriteString("label", f.Def.Label);
        w.WriteString("kind", f.Def.Info != null ? "info" : f.Def.Kind == Kind.Enum ? "enum" : "number");
        w.WriteString("role", f.Def.Role.ToString());
        w.WriteStartArray("options");
        foreach (var o in f.Options) w.WriteStringValue(o);
        w.WriteEndArray();
        w.WriteNumber("currentIndex", f.Index);
        w.WriteString("text", f.Text);
        w.WriteString("unit", f.Def.Unit);
        w.WriteString("hint", f.Hint);
        w.WriteString("error", f.Error);
        w.WriteBoolean("readOnly", f.ReadOnly);
        w.WriteEndObject();
    }

    static void WriteErr(Utf8JsonWriter w, Err e)
    {
        w.WriteStartObject();
        w.WriteString("Code", e.Code);
        w.WriteString("Stage", e.Stage);
        w.WriteString("ParamId", e.ParamId ?? "");
        w.WriteString("Actual", e.Actual ?? "");
        w.WriteString("Expected", e.Expected ?? "");
        w.WriteString("MessageKo", e.MessageKo);
        w.WriteString("SuggestedAction", e.SuggestedAction);
        w.WriteEndObject();
    }

    public static string Form(Session s, string processId, string? valuesJson) => Guard(() =>
    {
        var p = Engine.Catalog.Find(processId);
        if (p == null) return Error("UNKNOWN_PROCESS", $"알 수 없는 공정 '{processId}'");
        var (values, eq) = ParseValues(valuesJson);
        var ctx = new Ctx { Session = s, State = s.Current, Eq = eq ?? new Equipment() };
        var ev = Validator.Evaluate(p, values, ctx, strict: false);
        var cross = new List<Err>();
        if (ev.Errors.Count == 0) p.Cross(ev.Recipe, ctx, cross);
        bool inputOk = p.Enabled && ev.Errors.Count == 0 && cross.Count == 0;
        var pre = new List<Err>();
        string precheck;
        if (!p.Enabled) precheck = p.DisabledReason;
        else if (!inputOk) precheck = "입력 오류를 먼저 고치세요.";
        else
        {
            var stages = new List<(string Name, List<Err> Errs)>();
            var e1 = new List<Err>();
            if (!p.AllowAfterFailure && s.Current.LastRunFailed && !ctx.Eq.ContinueOnFailure)
                e1.Add(new Err("PREVIOUS_FAILURE", "Precondition", null, null, null, "직전 공정이 품질 불량입니다. ‘불량 상태로 계속 학습’을 켜야 진행할 수 있습니다.", ""));
            p.Preconditions(ev.Recipe, ctx, e1);
            stages.Add(("선행조건", e1));
            if (e1.Count == 0)
            {
                var e2 = new List<Err>();
                if (!ctx.Eq.DoorClosed) e2.Add(new Err("INTERLOCK_DOOR_OPEN", "Interlock", null, null, null, "장비 문이 열려 있습니다.", ""));
                if (ctx.Eq.EmergencyStop) e2.Add(new Err("INTERLOCK_EMERGENCY_STOP", "Interlock", null, null, null, "비상정지가 눌려 있습니다.", ""));
                if (!ctx.Eq.Available) e2.Add(new Err("INTERLOCK_UNAVAILABLE", "Interlock", null, null, null, "장비를 사용할 수 없습니다.", ""));
                p.Interlock(ev.Recipe, ctx, e2);
                stages.Add(("장비 인터록", e2));
                if (e2.Count == 0)
                {
                    var e3 = new List<Err>();
                    p.Domain(ev.Recipe, ctx, e3);
                    stages.Add(("모델 적용 범위", e3));
                }
            }
            foreach (var st in stages) pre.AddRange(st.Errs);
            precheck = pre.Count == 0
                ? "사전 점검 통과: 선행조건·장비 인터록·모델 적용 범위 이상 없음(실행 직전에 다시 검사)."
                : string.Join("\n", stages.Where(x => x.Errs.Count > 0).SelectMany(x => x.Errs.Select(e => $"[{x.Name}] {e.Code}: {e.MessageKo}")));
        }
        return Write(w =>
        {
            w.WriteBoolean("ok", true);
            w.WriteString("processId", p.Id);
            w.WriteString("title", $"{p.Group} · {p.Name}");
            w.WriteStartArray("fields");
            foreach (var f in ev.Fields.Where(f => f.Def.Role != Role.QualityTarget)) WriteField(w, f);
            w.WriteEndArray();
            w.WriteStartArray("qualityFields");
            foreach (var f in ev.Fields.Where(f => f.Def.Role == Role.QualityTarget)) WriteField(w, f);
            w.WriteEndArray();
            w.WriteStartArray("crossErrors");
            foreach (var e in cross) WriteErr(w, e);
            w.WriteEndArray();
            w.WriteStartArray("precheckErrors");
            foreach (var e in pre) WriteErr(w, e);
            w.WriteEndArray();
            w.WriteString("precheckText", precheck + (cross.Count > 0 ? "\n" + string.Join("\n", cross.Select(c => $"[교차 검증] {c.Code}: {c.MessageKo}")) : ""));
            w.WriteBoolean("canPreview", inputOk);
            w.WriteBoolean("canRun", inputOk);
            w.WriteStartArray("activeParams");
            foreach (var f in ev.Fields.Where(f => !f.ReadOnly && f.Def.Info == null)) w.WriteStringValue(f.Def.Id);
            w.WriteEndArray();
            w.WriteStartArray("inactiveProvided");
            foreach (var id in ev.InactiveProvided) w.WriteStringValue(id);
            w.WriteEndArray();
            w.WriteStartArray("unsupportedEffects");
            foreach (var u in p.Unsupported) w.WriteStringValue(u);
            w.WriteEndArray();
            w.WriteString("note", EducationalNote);
        });
    });

    public static RunResult RunRaw(Session s, string processId, IReadOnlyDictionary<string, string> values, Equipment eq, bool commit,
        long expectedRevision, CancellationToken token)
    {
        var p = Engine.Catalog.Find(processId) ?? throw new ArgumentException($"알 수 없는 공정 '{processId}'");
        return Pipeline.Run(s, p, values, eq, commit, expectedRevision, token);
    }

    public static string Run(Session s, string processId, string? valuesJson, string? equipmentJson, bool commit, long expectedRevision, CancellationToken token) => Guard(() =>
    {
        if (Engine.Catalog.Find(processId) == null) return Error("UNKNOWN_PROCESS", $"알 수 없는 공정 '{processId}'");
        var (values, _) = ParseValues(valuesJson);
        var eq = ParseEquipment(equipmentJson);
        var res = RunRaw(s, processId, values, eq, commit, expectedRevision, token);
        return RunJson(s, res);
    });

    public static string RunJson(Session s, RunResult res) => Write(w =>
    {
        w.WriteBoolean("ok", true);
        w.WriteString("runId", res.RunId);
        w.WriteString("processId", res.ProcessId);
        w.WriteString("processName", res.ProcessName);
        w.WriteString("status", res.Status);
        w.WriteString("executionStatus", res.ExecutionStatus);
        w.WriteString("qualityStatus", res.QualityStatus);
        w.WriteString("statusKind", res.StatusKind);
        w.WriteString("statusText", res.StatusText);
        w.WriteBoolean("committed", res.Committed);
        w.WriteBoolean("isPreview", res.Preview);
        w.WriteNumber("revision", s.Revision);
        w.WriteString("beforeStateId", res.BeforeStateId.ToString());
        w.WriteString("afterStateId", res.AfterStateId?.ToString() ?? "");
        w.WriteString("startedAtUtc", res.StartedUtc.ToString("O"));
        w.WriteString("finishedAtUtc", res.FinishedUtc.ToString("O"));
        w.WriteString("modelVersion", ModelVersion);
        w.WriteString("note", EducationalNote);
        w.WriteStartArray("stages");
        foreach (var st in res.Stages)
        {
            w.WriteStartObject();
            w.WriteString("stage", st.Stage);
            w.WriteString("name", st.NameKo);
            w.WriteString("verdict", st.Verdict);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartArray("metrics");
        foreach (var m in res.Metrics)
        {
            w.WriteStartObject();
            w.WriteString("id", m.Id);
            w.WriteString("name", m.Name);
            w.WriteString("value", m.Value + (m.Unit.Length > 0 && m.Value != "N/A" ? " " + m.Unit : ""));
            Num(w, "raw", m.Raw);
            w.WriteString("unit", m.Unit);
            w.WriteString("target", m.Target);
            w.WriteString("range", m.Range);
            w.WriteString("pass", m.Pass);
            w.WriteString("note", m.Note);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartArray("errors");
        foreach (var e in res.Errors) WriteErr(w, e);
        w.WriteEndArray();
        w.WriteStartArray("warnings");
        foreach (var x in res.Warnings) w.WriteStringValue(x);
        w.WriteEndArray();
        w.WriteStartArray("unsupportedEffects");
        foreach (var x in res.Unsupported) w.WriteStringValue(x);
        w.WriteEndArray();
        if (res.Electrical is { } el)
        {
            w.WriteStartObject("electrical");
            w.WriteString("mode", el.Mode);
            w.WriteString("deviceType", el.DeviceType);
            w.WriteBoolean("normal", el.Normal);
            w.WriteString("verdict", el.Verdict);
            w.WriteString("region", el.Region);
            w.WriteString("applied", el.Applied);
            w.WriteStartArray("failureCodes");
            foreach (var c in el.FailureCodes) w.WriteStringValue(c);
            w.WriteEndArray();
            w.WriteStartArray("extracted");
            foreach (var (n, v) in el.Extracted)
            {
                w.WriteStartObject();
                w.WriteString("name", n);
                w.WriteString("value", v);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteString("sweepKind", el.SweepKind);
            w.WriteString("xLabel", el.XLabel);
            w.WriteString("yLabel", el.YLabel);
            w.WriteBoolean("logY", el.LogY);
            Num(w, "vthExt", el.VthExt);
            w.WriteStartArray("sweep");
            foreach (var (x, y) in el.Sweep)
            {
                w.WriteStartArray();
                w.WriteNumberValue(x);
                w.WriteNumberValue(double.IsFinite(y) ? y : 0);
                w.WriteEndArray();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
    });

    public static string Undo(Session s) => Guard(() =>
    {
        if (!s.Undo()) return Error("NO_PARENT", "되돌릴 부모 상태가 없습니다.");
        return Write(w =>
        {
            w.WriteBoolean("ok", true);
            w.WriteNumber("revision", s.Revision);
            w.WriteString("stateId", s.Current.StateId.ToString());
            w.WriteBoolean("canUndo", s.CanUndo);
        });
    });

    public static string State(Session s) => Guard(() => Write(w =>
    {
        var st = s.Current;
        w.WriteBoolean("ok", true);
        w.WriteNumber("revision", s.Revision);
        w.WriteString("stateId", st.StateId.ToString());
        w.WriteString("parentStateId", st.ParentStateId?.ToString() ?? "");
        w.WriteBoolean("canUndo", s.CanUndo);
        w.WriteBoolean("lastRunFailed", st.LastRunFailed);
        w.WriteNumber("domainWidthUm", st.DomainWidthUm);
        w.WriteNumber("contamination", st.Contamination);
        w.WriteStartArray("layers");
        foreach (var id in st.PresentLayerIds())
        {
            w.WriteStartObject();
            w.WriteString("layerId", id);
            var mat = st.MaterialOfLayer(id);
            var (cr, cg, cb) = Mat.Rgb(mat);
            w.WriteString("material", mat);
            w.WriteString("materialKo", Mat.Ko(mat));
            w.WriteString("color", $"#{(int)(cr * 255):x2}{(int)(cg * 255):x2}{(int)(cb * 255):x2}");
            w.WriteString("origin", st.Layers.First(l => l.LayerId == id).Origin);
            double avg = st.Columns.Average(c => c.LayerThickness(id));
            w.WriteNumber("avgThickness", avg);
            w.WriteString("thicknessText", Fmt.S(avg, 5) + " nm");
            w.WriteString("coverage", $"{st.Columns.Count(c => c.Segs.Any(s => s.LayerId == id))}/{st.N} 열");
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartArray("masks");
        foreach (var m in s.Masks)
        {
            w.WriteStartObject();
            w.WriteString("maskId", m.MaskId);
            w.WriteString("maskType", m.MaskType);
            w.WriteString("maskLayer", m.MaskLayer);
            w.WriteNumber("maskCd", m.MaskCd);
            w.WriteNumber("pitch", m.Pitch);
            w.WriteNumber("offsetUm", m.OffsetUm);
            w.WriteNumber("repeatCount", m.RepeatCount);
            w.WriteBoolean("passed", m.Passed);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartArray("history");
        foreach (var h in s.History)
        {
            w.WriteStartObject();
            w.WriteString("runId", h.RunId);
            w.WriteString("processId", h.ProcessId);
            w.WriteString("name", h.ProcessName);
            w.WriteString("status", h.Status);
            w.WriteString("statusKind", h.StatusKind);
            w.WriteString("statusText", h.StatusText);
            w.WriteString("time", h.StartedUtc.ToLocalTime().ToString("HH:mm:ss"));
            w.WriteBoolean("physical", h.PhysicalChange);
            w.WriteString("afterStateId", h.AfterStateId?.ToString() ?? "");
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }));

    public static SceneData Scene(Session s, string? optionsJson) => SceneBuilder.Build(s, ParseSceneOptions(optionsJson));

    public static string SceneMeta(Session s, string? optionsJson) => Guard(() =>
    {
        var sd = Scene(s, optionsJson);
        return Write(w =>
        {
            w.WriteBoolean("ok", true);
            w.WriteString("legendHtml", sd.LegendHtml);
            w.WriteString("scaleText", sd.ScaleText);
            w.WriteString("terminals", sd.Terminals);
            w.WriteBoolean("isPreview", sd.IsPreview);
            w.WriteString("viewKind", sd.ViewKind);
            w.WriteNumber("boxCount", sd.BoxCount);
        });
    });

    public static string Objects(Session s) => Guard(() =>
    {
        var nl = new Netlist(s.Current);
        var rows = ObjectTable.Build(nl);
        return Write(w =>
        {
            w.WriteBoolean("ok", true);
            w.WriteStartArray("objects");
            foreach (var r in rows)
            {
                w.WriteStartObject();
                w.WriteString("objectId", r.ObjectId);
                w.WriteString("material", r.Material);
                w.WriteString("cls", r.Cls);
                w.WriteString("reason", r.Reason);
                w.WriteString("range", r.Range);
                w.WriteString("net", r.Net);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("nets");
            foreach (var n in nl.Ordered)
            {
                w.WriteStartObject();
                w.WriteString("netId", n.Id);
                w.WriteString("kind", n.Kind);
                w.WriteNumber("cells", n.Cells);
                w.WriteNumber("colMin", n.ColMin);
                w.WriteNumber("colMax", n.ColMax);
                w.WriteString("objects", string.Join(", ", n.Objects));
                Num(w, "minInsulatorBelowNm", double.IsFinite(n.MinInsulatorBelow) ? n.MinInsulatorBelow : null);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteNumber("conductorNets", nl.ConductorNetCount);
            w.WriteNumber("cellSum", nl.CellSum);
        });
    });

    public static string SelfTestJson() => Guard(() =>
    {
        var results = SelfTest.RunAll();
        return Write(w =>
        {
            w.WriteBoolean("ok", true);
            w.WriteNumber("total", results.Count);
            w.WriteNumber("passed", results.Count(r => r.Pass));
            w.WriteStartArray("tests");
            foreach (var r in results)
            {
                w.WriteStartObject();
                w.WriteString("id", r.Id);
                w.WriteBoolean("pass", r.Pass);
                w.WriteString("expected", r.Expected);
                w.WriteString("actual", r.Actual);
                w.WriteEndObject();
            }
            w.WriteEndArray();
        });
    });
}
