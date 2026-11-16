namespace SemiEdu.Engine;

public sealed class StageRow
{
    public string Stage = "";
    public string NameKo = "";
    public string Verdict = "NotRun";
}

public sealed class MetricRow
{
    public string Id = "", Name = "", Value = "", Unit = "", Target = "", Range = "", Pass = "—", Note = "";
    public double? Raw;
}

public sealed class RunResult
{
    public string RunId = Guid.NewGuid().ToString("N")[..12];
    public string ProcessId = "", ProcessName = "";
    public string Status = "";
    public string ExecutionStatus = "";
    public string QualityStatus = "NotEvaluated";
    public List<StageRow> Stages = new();
    public readonly List<MetricRow> Metrics = new();
    public readonly List<Err> Errors = new();
    public readonly List<string> Warnings = new();
    public Guid BeforeStateId;
    public Guid? AfterStateId;
    public bool Committed, Preview, PhysicalChange;
    public long Revision;
    public DateTime StartedUtc, FinishedUtc;
    public ElectricalResult? Electrical;
    public string[] Unsupported = Array.Empty<string>();
    public readonly Dictionary<string, string> Inputs = new();
    public Output? Output;

    public string StatusKind => Status switch
    {
        "ProcessSuccess" => "success",
        "ProcessFailure" => "failure",
        "Cancelled" => "cancelled",
        "CalculationError" or "ConfigError" => "error",
        _ => "blocked",
    };

    public string StatusText => StatusKind switch
    {
        "success" => "[성공] ProcessSuccess",
        "failure" => "[불량] ProcessFailure",
        "cancelled" => "[취소] Cancelled",
        "error" => $"[오류] {Status}",
        _ => $"[차단] {Status}",
    };

    public double? Metric(string id) => Output != null && Output.Metrics.TryGetValue(id, out var m) ? m.Value : null;
    public bool HasError(string code) => Errors.Any(e => e.Code == code);
}

/// <summary>
/// Input → precondition → interlock → model domain → calculation → invariants → quality → commit.
/// A blocked stage leaves the following stages NotRun.
/// </summary>
public static class Pipeline
{
    static readonly (string Id, string Ko)[] StageDefs =
    {
        ("Input", "입력 검증"), ("Precondition", "웨이퍼 선행조건"), ("Interlock", "장비 인터록"), ("Domain", "모델 적용 범위"),
        ("Calculation", "계산"), ("Invariant", "결과 불변조건"), ("Quality", "품질 평가"), ("Commit", "상태 커밋"),
    };

    public static RunResult Run(Session s, ProcessBase p, IReadOnlyDictionary<string, string> raw, Equipment eq,
        bool commit, long expectedRevision, CancellationToken token)
    {
        var res = new RunResult
        {
            ProcessId = p.Id,
            ProcessName = p.Name,
            StartedUtc = DateTime.UtcNow,
            Preview = !commit,
            BeforeStateId = s.Current.StateId,
            Revision = s.Revision,
            Unsupported = p.Unsupported,
            Stages = StageDefs.Select(d => new StageRow { Stage = d.Id, NameKo = d.Ko }).ToList(),
        };
        foreach (var kv in raw) res.Inputs[kv.Key] = kv.Value;
        try
        {
            RunCore(s, p, raw, eq, commit, expectedRevision, token, res);
        }
        catch (Exception ex)
        {
            res.Status = "CalculationError";
            res.ExecutionStatus = "Error";
            res.Errors.Add(new Err("INTERNAL_ERROR", "Calculation", null, null, null, $"내부 오류: {ex.Message}", "입력을 확인하고 다시 실행하세요."));
            res.Committed = false;
        }
        res.FinishedUtc = DateTime.UtcNow;
        if (commit) s.History.Add(res);
        return res;
    }

    static bool Block(RunResult res, int stage, string status, List<Err> errs)
    {
        if (errs.Count == 0)
        {
            res.Stages[stage].Verdict = "Pass";
            return false;
        }
        res.Stages[stage].Verdict = "Fail";
        res.Status = status;
        res.ExecutionStatus = status == "CalculationError" ? "Error" : "Blocked";
        res.Errors.AddRange(errs);
        return true;
    }

    static void RunCore(Session s, ProcessBase p, IReadOnlyDictionary<string, string> raw, Equipment eq, bool commit,
        long expectedRevision, CancellationToken token, RunResult res)
    {
        var ctx = new Ctx { Session = s, State = s.Current, Eq = eq };

        // 1. Input
        var errs = new List<Err>();
        if (!p.Enabled)
            errs.Add(new Err("PROCESS_NOT_IMPLEMENTED", "Input", null, null, null, $"{p.Name}: {p.DisabledReason}", "다른 공정을 선택하세요."));
        var ev = Validator.Evaluate(p, raw, ctx, strict: true);
        errs.AddRange(ev.Errors);
        if (errs.Count == 0) p.Cross(ev.Recipe, ctx, errs);
        if (Block(res, 0, "InvalidInput", errs)) return;
        var r = ev.Recipe;

        // 2. Preconditions
        errs = new List<Err>();
        if (!p.AllowAfterFailure && s.Current.LastRunFailed && !eq.ContinueOnFailure)
            errs.Add(new Err("PREVIOUS_FAILURE", "Precondition", null, null, null,
                "직전 공정이 품질 불량(ProcessFailure)입니다. 기본적으로 다음 공정을 막습니다.",
                "‘불량 상태로 계속 학습’을 켜거나 Undo로 되돌리세요."));
        p.Preconditions(r, ctx, errs);
        if (Block(res, 1, "PreconditionFailed", errs)) return;

        // 3. Interlock (common + process-specific)
        errs = new List<Err>();
        if (!eq.DoorClosed) errs.Add(new Err("INTERLOCK_DOOR_OPEN", "Interlock", "doorClosed", "false", "true", "교육용 장비 문이 열려 있습니다.", "장비 상태 패널에서 문을 닫으세요."));
        if (eq.EmergencyStop) errs.Add(new Err("INTERLOCK_EMERGENCY_STOP", "Interlock", "emergencyStop", "true", "false", "비상정지가 눌려 있습니다.", "비상정지를 해제하세요."));
        if (!eq.Available) errs.Add(new Err("INTERLOCK_UNAVAILABLE", "Interlock", "available", "false", "true", "장비를 사용할 수 없는 상태입니다.", "장비 상태를 ‘사용 가능’으로 바꾸세요."));
        if (s.Current.DiameterMm is not (100 or 150 or 200 or 300))
            errs.Add(new Err("INTERLOCK_DIAMETER", "Interlock", "diameter", s.Current.DiameterMm.ToString(), "100/150/200/300", "장비가 지원하지 않는 웨이퍼 직경입니다.", "지원 직경으로 초기화하세요."));
        p.Interlock(r, ctx, errs);
        if (Block(res, 2, "Interlock", errs)) return;

        // 4. Model domain
        errs = new List<Err>();
        p.Domain(r, ctx, errs);
        if (Block(res, 3, "ModelOutOfDomain", errs)) return;

        // 5. Calculation
        Output o;
        s.CalcCalls++;
        try
        {
            token.ThrowIfCancellationRequested();
            o = p.Calculate(r, ctx, token);
            token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            res.Stages[4].Verdict = "Fail";
            res.Status = "Cancelled";
            res.ExecutionStatus = "Cancelled";
            res.Errors.Add(new Err("CANCELLED", "Calculation", null, null, null, "사용자가 계산을 취소했습니다. 상태는 바뀌지 않았습니다.", "필요하면 다시 실행하세요."));
            return;
        }
        catch (Exception ex)
        {
            Block(res, 4, "CalculationError", new List<Err>
            {
                new("CALCULATION_EXCEPTION", "Calculation", null, null, null, $"계산 중 오류: {ex.Message}", "입력 조건을 바꿔 다시 실행하세요."),
            });
            return;
        }
        res.Stages[4].Verdict = "Pass";
        res.Output = o;
        res.Warnings.AddRange(o.Warnings);
        res.Electrical = o.Electrical;

        // 6. Invariants
        errs = new List<Err>(o.InvariantErrors);
        if (o.Candidate != null)
        {
            o.Candidate.Normalize();
            Invariants.Check(o.Candidate, errs);
        }
        foreach (var (id, m) in o.Metrics)
            if (m.Value is double v && !double.IsFinite(v))
                errs.Add(new Err("NON_FINITE_METRIC", "Invariant", id, Fmt.S(v), "유한값", $"지표 {m.NameKo}가 유한한 값이 아닙니다.", "모델 적용 범위를 확인하세요."));
        if (Block(res, 5, "CalculationError", errs)) return;

        // 7. Quality
        var criteria = p.Criteria(r, ctx, o);
        errs = new List<Err>();
        bool allPass = true, notEvaluable = false;
        var used = new HashSet<string>();
        foreach (var c in criteria)
        {
            if (!o.Metrics.TryGetValue(c.Metric, out var m))
            {
                errs.Add(new Err("MISSING_METRIC", "Quality", c.Metric, null, null, $"필수 품질 지표 '{c.Metric}'가 계산 결과에 없습니다.", "모델 구현을 확인하세요."));
                continue;
            }
            used.Add(c.Metric);
            var row = new MetricRow
            {
                Id = c.Metric, Name = m.NameKo, Unit = m.Unit, Raw = m.Value,
                Value = m.Value is double v ? Fmt.S(v) : "N/A",
                Target = c.TargetText(m.Unit), Range = c.RangeText(m.Unit),
            };
            if (m.Value is double av)
            {
                bool ok = c.Pass(av);
                row.Pass = ok ? "합격" : "불합격";
                if (!ok)
                {
                    allPass = false;
                    res.Errors.Add(new Err("QUALITY_FAIL", "Quality", c.Metric, row.Value + " " + m.Unit, row.Target,
                        $"{m.NameKo} {row.Value} {m.Unit}이(가) 기준 {row.Target}을(를) 만족하지 않습니다.", "조건 또는 품질 목표를 확인하세요."));
                }
            }
            else
            {
                allPass = false;
                notEvaluable = true;
                row.Pass = "N/A(불합격)";
                row.Note = m.NaReason ?? "정의되지 않음";
            }
            res.Metrics.Add(row);
        }
        if (Block(res, 6, "CalculationError", errs)) return;
        foreach (var id in o.MetricOrder)
        {
            if (used.Contains(id)) continue;
            var m = o.Metrics[id];
            res.Metrics.Add(new MetricRow
            {
                Id = id, Name = m.NameKo, Unit = m.Unit, Raw = m.Value,
                Value = m.Value is double v ? Fmt.S(v) : "N/A", Note = m.NaReason ?? "정보",
            });
        }
        if (notEvaluable)
            res.Errors.Add(new Err("QUALITY_NOT_EVALUABLE", "Quality", null, null, null, "필수 품질 기준을 평가할 수 없습니다(N/A는 합격으로 처리하지 않음).", "상태와 검사 대상을 확인하세요."));
        res.Stages[6].Verdict = allPass ? "Pass" : "Fail";
        res.QualityStatus = allPass ? "Pass" : "Fail";
        res.Status = allPass ? "ProcessSuccess" : "ProcessFailure";
        res.ExecutionStatus = "Completed";
        if (o.NewMask != null) o.NewMask.Passed = allPass;
        if (o.Electrical != null) o.Electrical.Normal = allPass;

        // 8. Commit
        if (!commit)
        {
            res.Stages[7].Verdict = "Skipped";
            s.PreviewState = o.Candidate;
            s.PreviewMask = o.NewMask;
            if (o.Electrical != null) o.Electrical.StateId = s.Current.StateId;
            s.PreviewElectrical = o.Electrical;
            res.AfterStateId = o.Candidate?.StateId;
            return;
        }
        if (expectedRevision >= 0 && expectedRevision != s.Revision)
        {
            res.Stages[7].Verdict = "Fail";
            res.Status = "PreconditionFailed";
            res.ExecutionStatus = "Blocked";
            res.Errors.Add(new Err("STALE_STATE", "Commit", null, expectedRevision.ToString(), s.Revision.ToString(),
                "계산 시작 후 웨이퍼 상태가 바뀌어 커밋을 거부했습니다.", "현재 상태에서 다시 실행하세요."));
            return;
        }
        if (p.ChangesState && o.Candidate != null)
        {
            var cand = o.Candidate;
            cand.Revision = s.Revision + 1;
            cand.LastRunFailed = !allPass;
            cand.CreatedBy = p.Id;
            s.States[cand.StateId] = cand;
            s.Current = cand;
            s.Revision++;
            res.PhysicalChange = true;
        }
        if (o.NewMask != null)
        {
            s.Masks.Add(o.NewMask);
            s.MaskCounter++;
        }
        if (o.Electrical != null)
        {
            o.Electrical.StateId = s.Current.StateId;
            s.LastElectrical = o.Electrical;
        }
        s.PreviewState = null;
        s.PreviewMask = null;
        s.PreviewElectrical = null;
        res.AfterStateId = s.Current.StateId;
        res.Committed = true;
        res.Revision = s.Revision;
        res.Stages[7].Verdict = "Pass";
    }
}

public static class Invariants
{
    public static void Check(WaferState s, List<Err> errs)
    {
        foreach (var c in s.Columns)
        {
            if (!double.IsFinite(c.SiTop))
            {
                errs.Add(new Err("NON_FINITE_STATE", "Invariant", null, null, null, "Si 표면 위치가 유한하지 않습니다.", "조건을 확인하세요."));
                return;
            }
            foreach (var g in c.Segs)
                if (!double.IsFinite(g.Thickness) || g.Thickness < 0)
                {
                    errs.Add(new Err("NEGATIVE_THICKNESS", "Invariant", g.LayerId, Fmt.S(g.Thickness), "≥ 0", "막 두께가 음수이거나 유한하지 않습니다.", "조건을 확인하세요."));
                    return;
                }
        }
        if (!double.IsFinite(s.Contamination) || s.Contamination < 0)
            errs.Add(new Err("INVALID_CONTAMINATION", "Invariant", null, Fmt.S(s.Contamination), "≥ 0", "오염 지수가 올바르지 않습니다.", "조건을 확인하세요."));
        if (s.Doping == null) return;
        foreach (var g in s.Doping.Groups)
            foreach (var sp in g.Species)
            {
                double peak = 0;
                foreach (var v in sp.Total) peak = Math.Max(peak, Math.Abs(v));
                double tol = 1e-9 * Math.Max(peak, 1e-30);
                for (int i = 0; i < sp.Total.Length; i++)
                {
                    double t = sp.Total[i], a = sp.Active[i];
                    if (!double.IsFinite(t) || !double.IsFinite(a) || t < -tol || a < -tol)
                    {
                        errs.Add(new Err("NEGATIVE_CONCENTRATION", "Invariant", sp.Species, Fmt.S(Math.Min(t, a)), "≥ 0", "농도가 음수이거나 유한하지 않습니다.", "열처리 조건을 확인하세요."));
                        return;
                    }
                    if (a > t + tol)
                    {
                        errs.Add(new Err("ACTIVE_EXCEEDS_TOTAL", "Invariant", sp.Species, Fmt.S(a), $"≤ {Fmt.S(t)}", "활성 농도가 전체 농도를 넘었습니다.", "모델 구현을 확인하세요."));
                        return;
                    }
                }
            }
    }
}
