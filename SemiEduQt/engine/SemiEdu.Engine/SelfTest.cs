namespace SemiEdu.Engine;

public sealed record TestResult(string Id, bool Pass, string Expected, string Actual);

/// <summary>
/// Scenario tests shared by se_selftest (deployed DLL) and xUnit. Expected values come from the spec formulas.
/// Passing them is software verification, not physical validation.
/// </summary>
public static class SelfTest
{
    public static readonly string[] Ids =
    {
        "M02", "M03", "E01", "E02", "Q01", "Q02", "Q03", "Q04", "V01", "V02", "V03", "V04", "U01",
        "M01", "M08", "M09", "M04", "M06", "M07", "M10", "CMP", "M05", "S01", "S02", "S03", "A01",
        "K01", "K02", "K03", "K04", "C01", "C02", "C03",
        "T01", "T02", "T03", "T04", "T05", "T06", "T07", "T08", "T09",
    };

    public static List<TestResult> RunAll() => Ids.Select(Run).ToList();

    public static TestResult Run(string id)
    {
        try
        {
            return id switch
            {
                "M02" => M02(), "M03" => M03(), "E01" => E01(), "E02" => E02(), "Q01" => Q01(), "Q02" => Q02(), "Q03" => Q03(),
                "Q04" => Q04(), "V01" => V01(), "V02" => V02(), "V03" => V03(), "V04" => V04(), "U01" => U01(), "M01" => M01(),
                "M08" => M08(), "M09" => M09(), "M04" => M04(), "M06" => M06(), "M07" => M07(), "M10" => M10(), "CMP" => Cmp(),
                "M05" => M05(), "S01" => S01(), "S02" => S02(), "S03" => S03(), "A01" => A01(), "K01" => K01(), "K02" => K02(),
                "K03" => K03(), "K04" => K04(), "C01" => C01(), "C02" => C02(), "C03" => C03(), "T01" => T01(), "T02" => T02(),
                "T03" => T03(), "T04" => T04(), "T05" => T05(), "T06" => T06(), "T07" => T07(), "T08" => T08(), "T09" => T09(),
                _ => new TestResult(id, false, "known test", "unknown id"),
            };
        }
        catch (Exception ex)
        {
            return new TestResult(id, false, "no exception", ex.GetType().Name + ": " + ex.Message);
        }
    }

    // ---------------- helpers ----------------

    static readonly Equipment Ok = new();
    static readonly Equipment Continue = new() { ContinueOnFailure = true };

    static Dictionary<string, string> V(params (string K, string V)[] kv) => kv.ToDictionary(x => x.K, x => x.V);

    static RunResult Run(Session s, string pid, Dictionary<string, string>? v = null, Equipment? eq = null, bool commit = true) =>
        Api.RunRaw(s, pid, v ?? new(), eq ?? Ok, commit, s.Revision, CancellationToken.None);

    static double M(RunResult r, string id) => r.Metric(id) ?? double.NaN;
    static bool Near(double a, double b, double tol) => Math.Abs(a - b) <= tol;
    static bool Rel(double a, double b, double rel) => Math.Abs(a - b) <= rel * Math.Abs(b);
    static string S(double v) => Fmt.S(v, 10);
    static TestResult T(string id, bool pass, string expected, string actual) => new(id, pass, expected, actual);

    static Session Fresh(string surface = "BareSi")
    {
        var s = new Session();
        var r = Run(s, "init", V(("startSurface", surface)));
        if (r.Status != "ProcessSuccess") throw new InvalidOperationException("init failed: " + r.Status);
        return s;
    }

    static double OxideNow(Session s) => s.Current.Columns[0].Total(Mat.SiO2);

    // ---------------- basic process tests ----------------

    static TestResult M02()
    {
        var s = Fresh();
        var r = Run(s, "oxidation");
        double x = M(r, "oxideThickness");
        return T("M02", r.Status == "ProcessSuccess" && Near(x, 70.4159458, 1e-6), "70.4159458 nm ±1e-6, ProcessSuccess", $"{S(x)} nm, {r.Status}");
    }

    static TestResult M03()
    {
        var s = Fresh();
        Run(s, "oxidation");
        var r = Run(s, "oxidation", V(("targetThickness", "112.79")));
        double x = M(r, "oxideThickness");
        return T("M03", Near(x, 112.788206, 1e-5), "112.788206 nm (xOld 사용)", $"{S(x)} nm");
    }

    static TestResult E01()
    {
        var a = Run(Fresh(), "oxidation", V(("temperature", "1201")));
        var b = Run(Fresh(), "oxidation", V(("temperature", "1150")));
        var c = Run(Fresh(), "oxidation", V(("temperature", "1000")));
        bool pass = a.Status == "InvalidInput" && b.Status == "Interlock" && c.Stages[4].Verdict == "Pass";
        return T("E01", pass, "InvalidInput / Interlock / 계산 진행", $"{a.Status} / {b.Status} / calc={c.Stages[4].Verdict}");
    }

    static TestResult E02()
    {
        var s = Fresh();
        int before = s.CalcCalls;
        var a = Run(s, "oxidation", null, new Equipment { DoorClosed = false });
        var b = Run(s, "oxidation", null, new Equipment { EmergencyStop = true });
        bool pass = a.Status == "Interlock" && b.Status == "Interlock" && s.CalcCalls == before;
        return T("E02", pass, "Interlock ×2, 계산 호출 0회", $"{a.Status}, {b.Status}, calls {s.CalcCalls - before}");
    }

    static TestResult Q01()
    {
        var c = new Criterion("x", CK.RelTarget, 70, 10);
        double hi = 77, lo = 63;
        bool pass = c.Pass(hi) && c.Pass(lo) && !c.Pass(hi + 100 * Criterion.Eps(hi, hi)) && !c.Pass(lo - 100 * Criterion.Eps(lo, lo));
        var a = new Criterion("x", CK.AbsTarget, 500, 50);
        pass &= a.Pass(550) && a.Pass(450) && !a.Pass(550 + 100 * Criterion.Eps(550, 550));
        return T("Q01", pass, "경계 포함 Pass, 100ε 밖 Fail", pass ? "일치" : "불일치");
    }

    static TestResult Q02()
    {
        var s = Fresh();
        var r = Run(s, "oxidation", V(("targetThickness", "200")));
        bool pass = r.Status == "ProcessFailure" && r.Committed && Near(OxideNow(s), 70.416, 1e-3);
        return T("Q02", pass, "ProcessFailure, 약 70.416 nm 커밋", $"{r.Status}, committed={r.Committed}, {S(OxideNow(s))} nm");
    }

    static TestResult Q03()
    {
        var a = Run(Fresh(), "oxidation", V(("targetThickness", "70")));
        var b = Run(Fresh(), "oxidation", V(("targetThickness", "200")));
        bool pass = M(a, "oxideThickness") == M(b, "oxideThickness") && a.QualityStatus == "Pass" && b.QualityStatus == "Fail";
        return T("Q03", pass, "두께 동일, 판정만 변화", $"{S(M(a, "oxideThickness"))} vs {S(M(b, "oxideThickness"))}, {a.QualityStatus}/{b.QualityStatus}");
    }

    sealed class MissingMetricProcess : ProcessBase
    {
        public override string Id => "fake-missing";
        public override string Group => "test";
        public override string Name => "missing metric";
        public override IReadOnlyList<ParamDef> Params => Array.Empty<ParamDef>();
        public override Output Calculate(Recipe r, Ctx c, CancellationToken t) => new() { Candidate = c.State.CloneAsChild() };
        public override List<Criterion> Criteria(Recipe r, Ctx c, Output o) => new() { new("absent", CK.Max, 1) };
    }

    static TestResult Q04()
    {
        var s = Fresh();
        var id = s.Current.StateId;
        var r = Pipeline.Run(s, new MissingMetricProcess(), new Dictionary<string, string>(), Ok, true, s.Revision, CancellationToken.None);
        bool pass = r.Status == "CalculationError" && !r.Committed && s.Current.StateId == id && r.HasError("MISSING_METRIC");
        return T("Q04", pass, "CalculationError, 커밋 없음", $"{r.Status}, committed={r.Committed}");
    }

    static TestResult V01()
    {
        var lo = Run(Fresh(), "oxidation", V(("temperature", "700")));
        var hi = Run(Fresh(), "oxidation", V(("temperature", "1200")));
        var below = Run(Fresh(), "oxidation", V(("temperature", "699.999999")));
        var above = Run(Fresh(), "oxidation", V(("temperature", "1200.000001")));
        bool pass = lo.Stages[0].Verdict == "Pass" && hi.Stages[0].Verdict == "Pass" && below.Status == "InvalidInput" && above.Status == "InvalidInput";
        return T("V01", pass, "양끝 입력 유효(이후 단계는 별도), 바깥 InvalidInput",
            $"700:{lo.Stages[0].Verdict}/{lo.Status} 1200:{hi.Stages[0].Verdict}/{hi.Status} 699.999999:{below.Status} 1200.000001:{above.Status}");
    }

    static TestResult V02()
    {
        var s = Fresh();
        long rev = s.Revision;
        var bad = new[] { "NaN", "Infinity", "", "abc", "1,000" };
        bool pass = bad.All(b => Run(s, "oxidation", V(("temperature", b))).Status == "InvalidInput");
        var cyc = Run(s, "deposition", V(("method", "ALD"), ("cycles", "1.5")));
        pass &= cyc.Status == "InvalidInput" && cyc.HasError("NOT_INTEGER") && s.Revision == rev;
        return T("V02", pass, "모두 InvalidInput, Revision 불변", $"cycles=1.5 → {cyc.Status}, rev {rev}→{s.Revision}");
    }

    static TestResult V03()
    {
        var a = Run(Fresh(), "deposition", V(("method", "PVD"), ("material", "SiO2")));
        var b = Run(Fresh("UniformSiO2"), "etch", V(("method", "Wet"), ("scope", "Blanket"), ("rfPower", "300")));
        bool pass = a.Status == "InvalidInput" && a.HasError("OPTION_INVALID") && b.Status == "InvalidInput" && b.HasError("INAPPLICABLE_PARAMETER");
        return T("V03", pass, "지원 조합 오류 / 비활성 입력 오류", $"{a.Status}({string.Join(",", a.Errors.Select(e => e.Code))}) / {b.Status}({string.Join(",", b.Errors.Select(e => e.Code))})");
    }

    static TestResult V04()
    {
        var s = Fresh();
        var ctx = new Ctx { Session = s, State = s.Current, Eq = Ok };
        var p = Catalog.Find("deposition")!;
        var cvd = Validator.Evaluate(p, V(("method", "CVD")), ctx, false).Fields.Select(f => f.Def.Id).ToHashSet();
        var ald = Validator.Evaluate(p, V(("method", "ALD"), ("duration", "10"), ("flow", "100")), ctx, false);
        var ids = ald.Fields.Select(f => f.Def.Id).ToHashSet();
        bool pass = cvd.Contains("duration") && cvd.Contains("flow") && !ids.Contains("duration") && !ids.Contains("flow")
                    && ids.Contains("cycles") && ids.Contains("pulseTime") && ids.Contains("purgeTime") && ald.Errors.Count == 0
                    && ald.InactiveProvided.Contains("duration");
        return T("V04", pass, "시간·유량 제거, cycles·pulse·purge 활성", string.Join(",", ids));
    }

    static TestResult U01()
    {
        double a = Units.Torr(760), b = Units.MilliTorr(760000);
        return T("U01", Math.Abs(a - b) <= 1e-12 * a, "같은 Pa(상대오차 ≤1e-12)", $"{S(a)} Pa vs {S(b)} Pa");
    }

    static TestResult M01()
    {
        var s = Fresh("UniformSiO2");
        var m = Run(s, "mask", V(("designClearWidth", "50")));
        var r = Run(s, "photo", V(("maskId", s.Masks[^1].MaskId)));
        bool pass = m.Status == "ProcessSuccess" && r.Status == "ModelOutOfDomain" && r.HasError("OPTICAL_RESOLUTION");
        return T("M01", pass, "ModelOutOfDomain(OPTICAL_RESOLUTION)", $"mask {m.Status}, photo {r.Status} {string.Join(",", r.Errors.Select(e => e.Code))}");
    }

    static TestResult M08()
    {
        var s = Fresh("UniformSiO2");
        var id = s.Current.StateId;
        var r = Run(s, "etch", V(("scope", "Patterned")));
        return T("M08", r.Status == "PreconditionFailed" && s.Current.StateId == id, "PreconditionFailed, 상태 불변", $"{r.Status}");
    }

    static TestResult M09()
    {
        var s = Fresh("UniformSiO2");
        double siBefore = s.Current.Columns[0].SiTop;
        var r = Run(s, "etch", V(("scope", "Blanket"), ("duration", "200")));
        bool pass = r.Status == "ProcessSuccess" && Near(M(r, "remainingTarget"), 0, 1e-9) && Near(M(r, "etchDepth"), 100, 1e-9)
                    && Near(s.Current.Columns[0].SiTop, siBefore, 1e-12) && r.Warnings.Any(w => w.StartsWith("STOP_LAYER_REACHED"));
        return T("M09", pass, "대상층 0 nm, 제거 100 nm, Si 보존, STOP_LAYER_REACHED",
            $"{r.Status}, 잔량 {S(M(r, "remainingTarget"))}, 제거 {S(M(r, "etchDepth"))}");
    }

    static TestResult M04()
    {
        var a = Run(Fresh(), "deposition");
        var b = Run(Fresh(), "deposition", V(("method", "ALD")));
        bool pass = Near(M(a, "thickness"), 100, 1e-9) && Near(M(b, "thickness"), 10, 1e-9) && Near(M(b, "aldTotalTime"), 1200, 1e-9)
                    && a.Status == "ProcessSuccess" && b.Status == "ProcessSuccess";
        return T("M04", pass, "CVD 100 nm / ALD 10 nm(1200 s)", $"{S(M(a, "thickness"))} / {S(M(b, "thickness"))} nm, {S(M(b, "aldTotalTime"))} s");
    }

    static TestResult M06()
    {
        var r = Run(Fresh(), "implant");
        double err = M(r, "doseRelError"), depth = M(r, "meanDepth");
        return T("M06", err <= 1e-6 && Near(depth, 148.95, 0.02) && r.Status == "ProcessSuccess", "dose 상대오차 ≤1e-6, 평균 깊이 ≈148.95 nm",
            $"err {S(err)}, depth {S(depth)} nm, {r.Status}");
    }

    static TestResult M07()
    {
        var s = Fresh();
        Run(s, "implant");
        var r = Run(s, "anneal");
        double act = M(r, "activation"), err = M(r, "doseConservation");
        bool pass = r.Status == "ProcessSuccess" && err <= 1e-6 && Near(act, 95.52, 0.05);
        return T("M07", pass, "적분 보존 ≤1e-6, 활성 ≈95.5%, 활성 ≤ 전체", $"err {S(err)}, act {S(act)} %, {r.Status}");
    }

    static TestResult M10()
    {
        var s = Fresh();
        Run(s, "implant");
        var r = Run(s, "oxidation");
        return T("M10", r.Status == "PreconditionFailed" && r.HasError("DOPING_PRESENT"), "PreconditionFailed(DOPING_PRESENT)", r.Status);
    }

    static TestResult Cmp()
    {
        var r = Run(Fresh("UniformSiO2"), "cmp");
        bool pass = r.Status == "ProcessSuccess" && Near(M(r, "remainingThickness"), 28, 1e-9) && Near(M(r, "stepHeight"), 0, 1e-9);
        return T("CMP", pass, "잔여 28 nm, 단차 0", $"{S(M(r, "remainingThickness"))} nm, 단차 {S(M(r, "stepHeight"))}, {r.Status}");
    }

    static TestResult M05()
    {
        var r = Run(Fresh("UniformSiO2"), "metal");
        double R = M(r, "resistance");
        return T("M05", Rel(R, 5.6, 1e-9) && r.Status == "ProcessSuccess", "5.6 Ω(상대오차 ≤1e-9)", $"{S(R)} Ω, {r.Status}");
    }

    static TestResult S01()
    {
        var s = Fresh();
        Run(s, "oxidation");
        double tox = OxideNow(s);
        Run(s, "mask");
        var photo = Run(s, "photo", V(("maskId", s.Masks[^1].MaskId)));
        var open = (bool[])s.Current.Resist!.Open.Clone();
        var etch = Run(s, "etch", V(("scope", "Patterned"), ("duration", "100"), ("targetEtchDepth", tox.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
        var strip = Run(s, "clean", V(("purpose", "PRStrip")));
        var st = s.Current;
        int openIdx = Array.IndexOf(open, true), closedIdx = Array.IndexOf(open, false);
        bool pass = photo.Status == "ProcessSuccess" && etch.Status == "ProcessSuccess" && strip.Status == "ProcessSuccess"
                    && st.Columns[openIdx].Total(Mat.SiO2) == 0 && Near(st.Columns[closedIdx].Total(Mat.SiO2), tox, 1e-9)
                    && st.Resist == null && !st.HasPR && Near(st.Contamination, 4.54, 0.01);
        return T("S01", pass, "개구부만 제거, PR 참조 해제, 오염 ≈4.54",
            $"photo {photo.Status}, etch {etch.Status}, strip {strip.Status}, 개구 산화막 {S(st.Columns[openIdx].Total(Mat.SiO2))}, 보호 {S(st.Columns[closedIdx].Total(Mat.SiO2))}, 오염 {S(st.Contamination)}");
    }

    static TestResult S02()
    {
        var s = Fresh();
        Run(s, "implant");
        var id = s.Current.StateId;
        long rev = s.Revision;
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var a = Api.RunRaw(s, "anneal", new Dictionary<string, string>(), Ok, true, s.Revision, cts.Token);
        sw.Stop();
        var b = Api.RunRaw(s, "anneal", new Dictionary<string, string>(), Ok, true, s.Revision - 1, CancellationToken.None);
        bool pass = a.Status == "Cancelled" && b.HasError("STALE_STATE") && s.Current.StateId == id && s.Revision == rev && sw.ElapsedMilliseconds <= 500;
        return T("S02", pass, "Cancelled / STALE_STATE, 부분 커밋 없음, 취소 응답 ≤500 ms",
            $"{a.Status} ({sw.ElapsedMilliseconds} ms) / {string.Join(",", b.Errors.Select(e => e.Code))}, state 불변={s.Current.StateId == id}");
    }

    static TestResult S03()
    {
        var s = Fresh();
        var parent = s.Current.StateId;
        Run(s, "oxidation");
        int hist = s.History.Count;
        bool undone = s.Undo();
        bool pass = undone && s.Current.StateId == parent && s.History.Count == hist;
        var again = Run(s, "oxidation");
        pass &= again.Status == "ProcessSuccess" && s.Current.ParentStateId == parent;
        return T("S03", pass, "부모 StateId 복원, 이력 보존", $"undo={undone}, 복원={s.Current.ParentStateId == parent}, 이력 {hist}→{s.History.Count}");
    }

    static TestResult A01()
    {
        var s = new Session();
        string a = Api.Run(s, "oxidation", "{not json", null, true, -1, CancellationToken.None);
        string b = Api.Run(s, "no-such-process", "{}", null, true, -1, CancellationToken.None);
        string c = Api.Form(s, "no-such-process", "{}");
        bool pass = a.Contains("\"ok\":false") && b.Contains("\"ok\":false") && c.Contains("\"ok\":false");
        return T("A01", pass, "ok:false, 예외 없음", pass ? "ok:false ×3" : a + " | " + b);
    }

    // ---------------- mask tests ----------------

    static TestResult K01()
    {
        var s = Fresh();
        var r = Run(s, "mask");
        return T("K01", r.Status == "ProcessSuccess" && Near(M(r, "maskCd"), 500, 1e-9), "CD 500 nm, ProcessSuccess", $"{S(M(r, "maskCd"))} nm, {r.Status}");
    }

    static TestResult K02()
    {
        var s = Fresh("UniformSiO2");
        var r = Run(s, "mask", V(("crEtchTime", "60")));
        var ctx = new Ctx { Session = s, State = s.Current, Eq = Ok };
        var opts = Validator.Evaluate(Catalog.Find("photo")!, new Dictionary<string, string>(), ctx, false).Fields.First(f => f.Def.Id == "maskId").Options;
        bool pass = r.Status == "ProcessFailure" && Near(M(r, "absorberResidual"), 40, 1e-9) && s.Masks.Count == 1 && !s.Masks[0].Passed
                    && !opts.Contains(s.Masks[0].MaskId);
        return T("K02", pass, "잔막 40 nm, ProcessFailure, 기록되지만 포토 선택 불가", $"{r.Status}, 잔막 {S(M(r, "absorberResidual"))}, 포토 선택지 [{string.Join(",", opts)}]");
    }

    static TestResult K03()
    {
        var s = Fresh("UniformSiO2");
        Run(s, "mask", V(("maskType", "EUVReflective")));
        var r = Run(s, "photo", V(("maskId", s.Masks[^1].MaskId), ("exposureMode", "DUV")));
        return T("K03", r.Status == "PreconditionFailed" && r.HasError("MASK_TYPE_MISMATCH"), "PreconditionFailed(MASK_TYPE_MISMATCH)", r.Status);
    }

    static TestResult K04()
    {
        var r = Run(Fresh(), "mask", V(("beamSize", "30"), ("designClearWidth", "50")));
        return T("K04", r.Status == "ModelOutOfDomain" && r.HasError("MASK_WRITE_RESOLUTION"), "ModelOutOfDomain", r.Status);
    }

    // ---------------- classification tests ----------------

    static List<ObjectRow> Objects(Session s) => ObjectTable.Build(new Netlist(s.Current));

    static TestResult C01()
    {
        var s = Fresh("UniformSiO2");
        Run(s, "metal");
        var rows = Objects(s);
        var wire = rows.First(r => r.Material == Mat.Al);
        var ox = rows.First(r => r.Material == Mat.SiO2);
        var sub = rows.First(r => r.ObjectId == "SUB");
        bool pass = wire.Cls == "도체" && ox.Cls == "절연체" && sub.Cls == "반도체(p형)";
        return T("C01", pass, "배선 도체, SiO2 절연체, 기판 p형 반도체", $"{wire.Cls} / {ox.Cls} / {sub.Cls}");
    }

    static double PeakActive(Session s)
    {
        var m = s.Current.Doping!;
        return m.Groups.SelectMany(g => g.Species).Where(p => p.Species == "As").Max(p => p.Active.Max());
    }

    static TestResult C02()
    {
        var s = Fresh();
        Run(s, "implant", V(("species", "As"), ("implantDose", "1e14"), ("targetDepth", "50")));
        double a1 = PeakActive(s);
        var c1 = Classes.OfNet(a1 + s.Current.BackgroundNet);
        Run(s, "anneal");
        double a2 = PeakActive(s);
        var c2 = Classes.OfNet(a2 + s.Current.BackgroundNet);
        bool pass = Rel(a1, 2.68e18, 0.02) && c1 == Cls.NSemi && Rel(a2, 2.5e19, 0.05) && c2 == Cls.NPlus;
        return T("C02", pass, "주입 직후 ≈2.7e18 n형 반도체, RTA 후 ≈2.5e19 N+ 도체성",
            $"{Fmt.S(a1, 4)} {Classes.Ko(c1)} → {Fmt.S(a2, 4)} {Classes.Ko(c2)}");
    }

    static TestResult C03()
    {
        var s = Path5(out _);
        var a = new Netlist(s.Current);
        var b = new Netlist(s.Current);
        bool pass = a.ConductorNetCount == b.ConductorNetCount && a.CellSum == b.CellSum && a.Ordered.Count == b.Ordered.Count;
        return T("C03", pass, "같은 상태에서 네트 수·셀 합계 동일", $"{a.ConductorNetCount}/{a.CellSum} vs {b.ConductorNetCount}/{b.CellSum}");
    }

    // ---------------- transistor (path 5) ----------------

    public sealed class Path5Options
    {
        public bool SkipAnneal, GateAfterImplant, NoGate;
        public string Species = "As";
        public string Energy = "50";
        public string TargetDepth = "50";
        public string GateOffset = "9.5";
        public string OxTime = "10";
        public string OxTarget = "9.4";
    }

    /// <summary>Learning path 5 (spec 10.1). Returns the session; <paramref name="log"/> lists step statuses.</summary>
    public static Session Path5(out List<string> log, Path5Options? opt = null)
    {
        opt ??= new Path5Options();
        var steps = new List<string>();
        log = steps;
        var s = new Session();
        void Step(string pid, Dictionary<string, string> v)
        {
            var r = Run(s, pid, v);
            steps.Add($"{pid}:{r.Status}{(r.Errors.Count > 0 ? "(" + string.Join(",", r.Errors.Select(e => e.Code)) + ")" : "")}");
        }
        Step("init", V(("dopingType", "P-type"), ("backgroundDoping", "1e15"), ("startSurface", "BareSi"), ("domainWidth", "25.6")));
        Step("mask", V(("maskLayer", "Gate"), ("designClearWidth", "1000"), ("offset", opt.GateOffset), ("repeatCount", "1")));
        string gateMask = s.Masks[^1].MaskId;
        Step("mask", V(("maskLayer", "Contact"), ("designClearWidth", "2000"), ("offset", "4"), ("repeatCount", "1")));
        string srcMask = s.Masks[^1].MaskId;
        Step("mask", V(("maskLayer", "Contact"), ("designClearWidth", "2000"), ("offset", "14"), ("repeatCount", "1")));
        string drnMask = s.Masks[^1].MaskId;
        Step("oxidation", V(("method", "Dry"), ("temperature", "900"), ("duration", opt.OxTime), ("pressure", "760"), ("targetThickness", opt.OxTarget)));
        var gate = V(("metal", "W"), ("role", "Gate"), ("maskId", gateMask), ("thickness", "300"), ("length", "10"));
        if (!opt.GateAfterImplant && !opt.NoGate) Step("metal", gate);
        Step("implant", V(("species", opt.Species), ("energy", opt.Energy), ("implantDose", "1e14"), ("tilt", "0"), ("targetDepth", opt.TargetDepth)));
        if (!opt.SkipAnneal) Step("anneal", V(("method", "RTA"), ("temperature", "1000"), ("duration", "30")));
        if (opt.GateAfterImplant && !opt.NoGate) Step("metal", gate);
        Step("contact", V(("metal", "W"), ("sourceMaskId", srcMask), ("drainMaskId", drnMask), ("plugAboveNm", "300")));
        return s;
    }

    static RunResult Test(Session s, Dictionary<string, string>? v = null) => Run(s, "transistor", v);

    static string Codes(RunResult r) => r.Electrical == null ? string.Join(",", r.Errors.Select(e => e.Code)) : string.Join(",", r.Electrical.FailureCodes);

    static TestResult T01()
    {
        var s = Path5(out var log);
        var r = Test(s);
        double tox = M(r, "tox"), vth = M(r, "vth"), ion = M(r, "ion"), ioff = M(r, "ioff"), onoff = M(r, "onOff");
        bool pass = r.Status == "ProcessSuccess" && r.Electrical!.Normal && Rel(tox, 9.38716, 1e-5) && Rel(vth, 0.27585, 1e-4)
                    && Rel(ion, 6.2835e-4, 1e-3) && Rel(ioff, 4.0128e-10, 1e-2) && Rel(onoff, 1.566e6, 1e-2);
        return T("T01", pass, "[정상] NMOS, tox 9.38716 nm, Vth 0.27585 V, Ion 6.2835e-4 A, Ioff 4.0128e-10 A, on/off 1.566e6",
            $"{r.Electrical?.Verdict} · tox {S(tox)} Vth {S(vth)} Ion {Fmt.S(ion, 6)} Ioff {Fmt.S(ioff, 6)} on/off {Fmt.S(onoff, 5)} · {string.Join(" ", log)}");
    }

    static TestResult T02()
    {
        var r = Test(Path5(out _, new Path5Options { SkipAnneal = true }));
        var codes = r.Electrical?.FailureCodes ?? new List<string>();
        bool pass = r.Status == "ProcessFailure" && codes.Contains("OPEN_SOURCE") && codes.Contains("OPEN_DRAIN");
        return T("T02", pass, "OPEN_SOURCE·OPEN_DRAIN, 비정상", $"{r.Status} {Codes(r)}");
    }

    static TestResult T03()
    {
        var r = Test(Path5(out _, new Path5Options { GateAfterImplant = true }));
        var codes = r.Electrical?.FailureCodes ?? new List<string>();
        bool pass = r.Status == "ProcessFailure" && codes.Contains("SD_SHORT") && r.Metrics.Any(m => m.Id == "ioff" && m.Pass == "불합격");
        return T("T03", pass, "SD_SHORT, Ioff 실패", $"{r.Status} {Codes(r)}");
    }

    static TestResult T04()
    {
        // B at 15 keV: Rp+4σ = 99 nm stays below the 300 nm gate (spec X1 MASK_TOO_THIN would block B at 50 keV).
        var r = Test(Path5(out var log, new Path5Options { Species = "B", Energy = "15", TargetDepth = "36" }));
        var codes = r.Electrical?.FailureCodes ?? new List<string>();
        return T("T04", r.Status == "ProcessFailure" && codes.Contains("NO_JUNCTION"), "P+ in p-body → NO_JUNCTION", $"{r.Status} {Codes(r)} · {string.Join(" ", log)}");
    }

    static TestResult T05()
    {
        Path5(out var log, new Path5Options { GateOffset = "13.5" });
        bool pass = log.Last().StartsWith("contact:PreconditionFailed") && log.Last().Contains("CONTACT_OVERLAPS_GATE");
        return T("T05", pass, "컨택 형성 PreconditionFailed(게이트 열 겹침)", log.Last());
    }

    static TestResult T06()
    {
        var s = Path5(out var log, new Path5Options { OxTime = "5", OxTarget = "4.9" });
        int before = s.CalcCalls;
        var r = Test(s, V(("testMode", "SinglePoint"), ("vg", "5")));
        bool pass = r.Status == "Interlock" && r.HasError("PROBER_OXIDE_FIELD") && s.CalcCalls == before;
        return T("T06", pass, "Eox ≈10.2 MV/cm > 8 → Interlock, 계산 0회",
            $"{r.Status} {r.Errors.FirstOrDefault()?.Actual} MV/cm, calls {s.CalcCalls - before} · {string.Join(" ", log)}");
    }

    static TestResult T07()
    {
        var r = Test(Path5(out _, new Path5Options { NoGate = true }));
        bool pass = r.Status == "PreconditionFailed" && r.Errors.Any(e => e.Code == "TERMINAL_MISSING" && e.MessageKo.Contains("Gate"));
        return T("T07", pass, "PreconditionFailed(TERMINAL_MISSING: Gate)", $"{r.Status} {r.Errors.FirstOrDefault()?.MessageKo}");
    }

    static TestResult T08()
    {
        var s = Path5(out _);
        var a = Test(s);
        var b = Test(s, V(("minIon", "1e-3")));
        bool pass = a.Status == "ProcessSuccess" && b.Status == "ProcessFailure" && M(a, "ion") == M(b, "ion");
        return T("T08", pass, "전류 동일, 판정만 [비정상]", $"{a.Status} → {b.Status}, Ion {Fmt.S(M(a, "ion"), 6)} = {Fmt.S(M(b, "ion"), 6)}");
    }

    static TestResult T09()
    {
        var s = Path5(out _);
        var id = s.Current.StateId;
        long rev = s.Revision;
        int hist = s.History.Count;
        Test(s);
        bool pass = s.Current.StateId == id && s.Revision == rev && s.History.Count == hist + 1;
        return T("T09", pass, "웨이퍼 상태 동일, 검사 기록만 추가", $"state 동일={s.Current.StateId == id}, 이력 {hist}→{s.History.Count}");
    }
}
