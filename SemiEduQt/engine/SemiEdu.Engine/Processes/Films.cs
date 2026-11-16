namespace SemiEdu.Engine;

public sealed class EtchProcess : ProcessBase
{
    public override string Id => "etch";
    public override string Group => "④ 식각";
    public override string Name => "식각";
    public override string[] Unsupported => new[]
    {
        "Wet 등방성 언더컷", "Dry 측벽 물리·측벽 각도", "하부층 실제 선택비(완전 정지막 가정)", "로딩 효과", "도핑 후 Si 식각(차단)",
    };

    static bool Dry(FormCtx f) => f.E("method") == "Dry";
    static bool Wet(FormCtx f) => f.E("method") == "Wet";

    static string[] Chem(FormCtx f)
    {
        string t = f.E("targetMaterial");
        if (Dry(f)) return t == Mat.Si ? new[] { "EduChlorine" } : new[] { "EduFluorine" };
        return t switch { Mat.SiO2 => new[] { "EduOxide" }, Mat.Si3N4 => new[] { "EduNitride" }, _ => new[] { "EduSilicon" } };
    }

    static readonly ParamDef[] Ps =
    {
        En("method", "방식", "Dry", "Wet"),
        En("targetMaterial", "대상 재료", Mat.SiO2, Mat.Si3N4, Mat.Si),
        EnF("chemistry", "가상 가스/약액", Chem, note: "교육용 가상 ID, 실제 조성 아님"),
        En("scope", "적용 범위", "Patterned", "Blanket"),
        Num("duration", "시간", "s", 1, 600, 60),
        Num("pressure", "챔버 압력", "mTorr", 1, 500, 50, Dry),
        Num("rfPower", "RF 전력", "W", 10, 2000, 300, Dry),
        NumF("temperature", "온도", "°C", f => Dry(f) ? (-20.0, 200.0, 25.0) : (20.0, 90.0, 25.0)),
        Num("flow", "가스 유량", "sccm", 1, 500, 100, Dry),
        Num("concentration", "약액 농도", "%", 0.1, 30, 5, Wet),
        Q("targetEtchDepth", "목표 식각 깊이", "nm", 1, 10000, 100),
        Q("etchTolerance", "식각 깊이 허용오차", "%", 0, 50, 10),
    };
    public override IReadOnlyList<ParamDef> Params => Ps;

    public override string EquipmentId => "DryEtchEdu/WetEtchEdu";

    static double Rate(Recipe r)
    {
        double r0 = r.E("targetMaterial") switch { Mat.SiO2 => 1.0, Mat.Si3N4 => 0.5, _ => 1.5 };
        double T = r.N("temperature");
        double f = r.E("method") == "Dry"
            ? Math.Sqrt(r.N("rfPower") / 300) * Math.Pow(50 / r.N("pressure"), 0.2) * Math.Pow(r.N("flow") / 100, 0.2) * Math.Exp((T - 25) / 200)
            : Math.Sqrt(r.N("concentration") / 5) * Math.Exp((T - 25) / 50);
        return r0 * f;
    }

    static string TopBelowPr(Column c)
    {
        for (int i = c.Segs.Count - 1; i >= 0; i--)
            if (c.Segs[i].Material != Mat.PR) return c.Segs[i].Material;
        return Mat.Si;
    }

    public override void Preconditions(Recipe r, Ctx c, List<Err> e)
    {
        var s = c.State;
        bool pat = r.E("scope") == "Patterned";
        string tgt = r.E("targetMaterial");
        if (pat && (s.Resist == null || !s.Resist.Open.Any(x => x) || !s.HasPR))
            Pre(e, "NO_PR_OPENING", "Patterned 식각에는 PR 개구부가 필요합니다.", "포토 공정을 먼저 수행하세요.");
        if (!pat && s.HasPR) Pre(e, "PR_PRESENT_BLANKET", "Blanket 식각은 PR이 없어야 합니다.", "PRStrip 후 실행하거나 Patterned를 선택하세요.");
        if (tgt == Mat.Si && s.HasDoping) Pre(e, "DOPING_PRESENT", "도핑 후 Si 식각은 프로파일 좌표 이동을 지원하지 않아 차단합니다.", "식각을 이온주입보다 먼저 수행하세요.");
        if (s.HasMetalSeg || s.Wires.Count > 0 || s.Contacts.Count > 0)
            Pre(e, "METAL_MODEL_UNSUPPORTED", "금속이 있는 상태의 식각은 v1에서 지원하지 않습니다(금속 형상 변경 모델 없음).", "금속 공정 전에 식각하세요.");
        if (e.Count > 0) return;
        bool exposed = false;
        for (int i = 0; i < s.N; i++)
        {
            if (pat && !s.Resist!.Open[i]) continue;
            if (TopBelowPr(s.Columns[i]) == tgt) { exposed = true; break; }
        }
        if (!exposed) Pre(e, "TARGET_NOT_EXPOSED", $"식각 영역 표면에 대상 재료 {tgt}이(가) 노출되어 있지 않습니다.", "대상 재료를 확인하세요.");
    }

    public override void Interlock(Recipe r, Ctx c, List<Err> e)
    {
        if (r.E("method") == "Dry")
        {
            double p = r.N("pressure"), rf = r.N("rfPower");
            if (p < 5 || p > 300) Ilk(e, "DRY_ETCH_PRESSURE_LIMIT", "pressure", p, "5–300 mTorr", "DryEtchEdu 압력 운전 한계를 벗어났습니다.");
            if (rf > 1000) Ilk(e, "DRY_ETCH_RF_LIMIT", "rfPower", rf, "≤ 1000 W", "DryEtchEdu RF 전력 한계를 넘었습니다.");
        }
        else if (r.N("temperature") > 80) Ilk(e, "WET_ETCH_TEMPERATURE_LIMIT", "temperature", r.N("temperature"), "≤ 80 °C", "WetEtchEdu 약액 온도 한계를 넘었습니다.");
    }

    public override void Domain(Recipe r, Ctx c, List<Err> e)
    {
        if (r.E("targetMaterial") == Mat.Si && Rate(r) * r.N("duration") >= c.State.SubstrateThicknessUm * 1000)
            Dom(e, "SUBSTRATE_PENETRATION", "기판 관통은 모델 범위 밖입니다.", "식각 시간을 줄이세요.");
    }

    public override Output Calculate(Recipe r, Ctx c, CancellationToken t)
    {
        var s = c.State.CloneAsChild();
        bool pat = r.E("scope") == "Patterned", dry = r.E("method") == "Dry";
        string tgt = r.E("targetMaterial");
        double R = Rate(r), prRate = R / (dry ? 5 : 10), dur = r.N("duration");
        bool[] evaluated = pat ? (bool[])s.Resist!.Open.Clone() : Enumerable.Repeat(true, s.N).ToArray();
        bool stop = false;
        int depleted = 0, nEval = 0;
        double sumRemoved = 0, sumRemaining = 0;
        for (int i = 0; i < s.N; i++)
        {
            var col = s.Columns[i];
            bool hadPr = col.Segs.Any(g => g.Material == Mat.PR);
            double time = dur, removed = 0;
            while (time > 1e-12)
            {
                if (col.Segs.Count > 0 && col.Segs[^1].Material == Mat.PR)
                {
                    var top = col.Segs[^1];
                    double need = top.Thickness / prRate;
                    if (need <= time) { time -= need; col.Segs.RemoveAt(col.Segs.Count - 1); }
                    else { top.Thickness -= prRate * time; time = 0; }
                    continue;
                }
                if (col.Segs.Count > 0 && col.Segs[^1].Material == tgt)
                {
                    var top = col.Segs[^1];
                    double need = top.Thickness / R;
                    if (need <= time) { time -= need; removed += top.Thickness; col.Segs.RemoveAt(col.Segs.Count - 1); }
                    else { top.Thickness -= R * time; removed += R * time; time = 0; }
                    continue;
                }
                if (col.Segs.Count == 0 && tgt == Mat.Si)
                {
                    col.SiTop -= R * time;
                    removed += R * time;
                    time = 0;
                    continue;
                }
                stop = true;
                break;
            }
            if (pat && hadPr && !s.Resist!.Open[i] && !col.Segs.Any(g => g.Material == Mat.PR)) depleted++;
            if (evaluated[i])
            {
                nEval++;
                sumRemoved += removed;
                sumRemaining += tgt == Mat.Si ? 0 : col.Total(tgt);
            }
        }
        s.Normalize();
        var o = new Output { Candidate = s };
        o.M("etchDepth", "제거 깊이(처음 열린 영역 평균)", nEval > 0 ? sumRemoved / nEval : null, "nm", nEval > 0 ? null : "평가 대상 열 없음");
        if (pat) o.M("protectedPrDepletedColumns", "보호부 PR 완전 소진 열 수", depleted, "열");
        o.M("remainingTarget", "대상층 잔량(평가 영역 평균)", nEval > 0 ? sumRemaining / nEval : null, "nm", nEval > 0 ? null : "평가 대상 열 없음");
        o.M("etchRate", "식각 속도", R, "nm/s");
        o.M("prRate", "PR 제거 속도", prRate, "nm/s");
        o.M("stopLayerReached", "정지막 도달", stop ? 1 : 0, "");
        if (stop) o.Warnings.Add("STOP_LAYER_REACHED: 대상층 아래 다른 재료에서 제거를 멈췄습니다(하부층은 완전 정지막으로 가정).");
        if (depleted > 0) o.Warnings.Add($"보호부 PR이 {depleted}개 열에서 완전히 소진되어 남은 시간 동안 해당 영역도 식각되었습니다.");
        return o;
    }

    public override List<Criterion> Criteria(Recipe r, Ctx c, Output o)
    {
        var list = new List<Criterion> { new("etchDepth", CK.RelTarget, r.N("targetEtchDepth"), r.N("etchTolerance")) };
        if (r.E("scope") == "Patterned") list.Add(new("protectedPrDepletedColumns", CK.Max, 0));
        return list;
    }
}

public sealed class CleanProcess : ProcessBase
{
    public override string Id => "clean";
    public override string Group => "④ 식각";
    public override string Name => "세정·PR 제거";
    public override string EquipmentId => "WetCleanEdu/DryCleanEdu";
    public override string[] Unsupported => new[] { "재료 손상", "재오염", "실제 약액 조성·혼합 절차" };

    static bool Wet(FormCtx f) => f.E("method") == "Wet";

    static readonly ParamDef[] Ps =
    {
        En("method", "방식", "Wet", "Dry"),
        En("purpose", "목적", "SurfaceClean", "PRStrip"),
        EnF("chemistry", "가상 약액/가스", f => Wet(f) ? (f.E("purpose") == "PRStrip" ? new[] { "EduStrip" } : new[] { "EduClean" }) : new[] { "EduPlasma" },
            note: "교육용 가상 ID"),
        NumF("temperature", "온도", "°C", f => Wet(f) ? (20.0, 90.0, 25.0) : (20.0, 200.0, 80.0)),
        Num("concentration", "농도", "%", 0.1, 30, 5, Wet),
        Num("power", "전력", "W", 10, 500, 100, f => !Wet(f)),
        Num("pressure", "압력", "mTorr", 10, 1000, 100, f => !Wet(f)),
        NumF("duration", "시간", "s", f => Wet(f) ? (10.0, 1800.0, 120.0) : (10.0, 600.0, 60.0)),
        Q("maxContamination", "최대 오염 지수", "점", 0, 100, 10),
        Q("maxPrResidual", "PR 잔량 한계", "nm", 0, 1000, 1, f => f.E("purpose") == "PRStrip"),
    };
    public override IReadOnlyList<ParamDef> Params => Ps;

    public override void Preconditions(Recipe r, Ctx c, List<Err> e)
    {
        if (r.E("purpose") == "PRStrip" && !c.State.HasPR) Pre(e, "NO_PR", "PRStrip은 PR이 있을 때만 실행할 수 있습니다.", "SurfaceClean을 선택하세요.");
    }

    public override void Interlock(Recipe r, Ctx c, List<Err> e)
    {
        if (r.E("method") == "Wet")
        {
            if (r.N("temperature") > 80) Ilk(e, "WET_CLEAN_TEMPERATURE_LIMIT", "temperature", r.N("temperature"), "≤ 80 °C", "WetCleanEdu 온도 한계를 넘었습니다.");
        }
        else if (r.N("power") > 300) Ilk(e, "DRY_CLEAN_POWER_LIMIT", "power", r.N("power"), "≤ 300 W", "DryCleanEdu 전력 한계를 넘었습니다.");
    }

    public override Output Calculate(Recipe r, Ctx c, CancellationToken t)
    {
        var s = c.State.CloneAsChild();
        bool wet = r.E("method") == "Wet";
        double T = r.N("temperature"), time = r.N("duration");
        double factor = wet
            ? Math.Sqrt(r.N("concentration") / 5) * Math.Exp((T - 25) / 100)
            : Math.Sqrt(r.N("power") / 100) * Math.Pow(100 / r.N("pressure"), 0.1) * Math.Exp((T - 80) / 200);
        double before = s.Contamination;
        s.Contamination = before * Math.Exp(-0.02 * factor * time);
        var o = new Output { Candidate = s };
        o.M("contamination", "오염 지수", s.Contamination, "점");
        if (r.E("purpose") == "PRStrip")
        {
            double removal = 10 * factor * time, maxResid = 0;
            foreach (var col in s.Columns)
            {
                double rem = removal;
                while (rem > 0 && col.Segs.Count > 0 && col.Segs[^1].Material == Mat.PR)
                {
                    var top = col.Segs[^1];
                    if (top.Thickness <= rem) { rem -= top.Thickness; col.Segs.RemoveAt(col.Segs.Count - 1); }
                    else { top.Thickness -= rem; rem = 0; }
                }
                maxResid = Math.Max(maxResid, col.Total(Mat.PR));
            }
            s.Normalize();
            if (!s.HasPR) s.Resist = null;
            o.M("prResidualMax", "최대 PR 잔량", maxResid, "nm");
            o.M("prRemovalCapacity", "PR 제거 능력", removal, "nm");
            if (s.Resist == null) o.Warnings.Add("PR이 모두 제거되어 패턴 참조를 해제했습니다.");
        }
        o.M("cleanFactor", "세정 계수", factor, "");
        return o;
    }

    public override List<Criterion> Criteria(Recipe r, Ctx c, Output o)
    {
        var list = new List<Criterion> { new("contamination", CK.Max, r.N("maxContamination")) };
        if (r.E("purpose") == "PRStrip") list.Add(new("prResidualMax", CK.Max, r.N("maxPrResidual")));
        return list;
    }
}

public sealed class DepositionProcess : ProcessBase
{
    public override string Id => "deposition";
    public override string Group => "⑤ 증착·이온주입";
    public override string Name => "증착(CVD/PVD/ALD)";
    public override string[] Unsupported => new[] { "단차 피복성", "빈틈 충진", "측벽 성장", "실제 장비 균일도(불균일도 0은 모델 가정)" };

    static string M(FormCtx f) => f.E("method");

    public static double DefaultTarget(string method, string mat) => (method, mat) switch
    {
        ("CVD", Mat.SiO2) => 100, ("CVD", Mat.Si3N4) => 80, ("PVD", Mat.Al) => 200, ("PVD", Mat.TiN) => 100,
        ("ALD", Mat.SiO2) => 10, ("ALD", Mat.TiN) => 5, _ => 100,
    };

    static readonly ParamDef[] Ps =
    {
        En("method", "방식", "CVD", "PVD", "ALD"),
        EnF("material", "재료", f => M(f) switch { "CVD" => new[] { Mat.SiO2, Mat.Si3N4 }, "PVD" => new[] { Mat.Al, Mat.TiN }, _ => new[] { Mat.SiO2, Mat.TiN } }),
        NumF("temperature", "온도", "°C", f => M(f) switch { "CVD" => (200.0, 900.0, 400.0), "PVD" => (20.0, 400.0, 100.0), _ => (100.0, 400.0, 200.0) }),
        NumF("pressure", "압력", "Torr", f => M(f) switch { "CVD" => (0.1, 760.0, 1.0), "PVD" => (0.001, 0.1, 0.01), _ => (0.1, 10.0, 1.0) }),
        Num("flow", "유량", "sccm", 1, 2000, 100, f => M(f) == "CVD"),
        Num("duration", "시간", "min", 1, 180, 10, f => M(f) != "ALD"),
        Num("power", "전력", "W", 50, 2000, 500, f => M(f) == "PVD"),
        Num("cycles", "사이클", "cycle", 1, 5000, 100, f => M(f) == "ALD", integer: true),
        Num("pulseTime", "펄스 시간", "s", 0.1, 10, 1, f => M(f) == "ALD"),
        Num("purgeTime", "퍼지 시간", "s", 0.1, 30, 5, f => M(f) == "ALD"),
        Info("aldTotalTime", "ALD 총 시간(자동 산출)", f =>
            f.R.Nums.TryGetValue("cycles", out var cy) && f.R.Nums.TryGetValue("pulseTime", out var pu) && f.R.Nums.TryGetValue("purgeTime", out var pg)
                ? $"{F(cy * 2 * (pu + pg))} s = cycles × 2 × (pulse + purge)" : "입력 확인 필요", f => M(f) == "ALD"),
        QF("targetThickness", "목표 두께", "nm", f => (1.0, 10000.0, DefaultTarget(M(f), f.E("material")))),
        Q("thicknessTolerance", "두께 허용오차", "%", 0, 50, 10),
        Q("maxNonuniformity", "최대 불균일도", "%", 0, 100, 10, f => M(f) != "ALD"),
        Q("maxPurgePenalty", "최대 퍼지 페널티", "%", 0, 100, 5, f => M(f) == "ALD"),
    };
    public override IReadOnlyList<ParamDef> Params => Ps;

    public static double Thickness(Recipe r)
    {
        string m = r.E("method"), mat = r.E("material");
        double T = r.N("temperature"), P = r.N("pressure");
        switch (m)
        {
            case "CVD":
            {
                double r0 = mat == Mat.SiO2 ? 10 : 8;
                return r0 * Math.Exp((T - 400) / 300) * Math.Pow(P / 1, 0.1) * Math.Pow(r.N("flow") / 100, 0.2) * r.N("duration");
            }
            case "PVD":
            {
                double r0 = mat == Mat.Al ? 20 : 10;
                return r0 * (r.N("power") / 500) * Math.Pow(0.01 / P, 0.1) * Math.Exp((T - 100) / 500) * r.N("duration");
            }
            default:
            {
                double gpc = mat == Mat.SiO2 ? 0.10 : 0.05;
                double sat = Math.Min(1, r.N("pulseTime") / 1);
                double thermal = Math.Exp(-Math.Pow((T - 200) / 100, 2));
                return gpc * r.N("cycles") * sat * thermal * Math.Pow(P / 1, 0.05);
            }
        }
    }

    public override void Preconditions(Recipe r, Ctx c, List<Err> e)
    {
        if (c.State.HasPR) Pre(e, "PR_PRESENT", "PR이 있으면 증착할 수 없습니다(v1).", "PRStrip을 먼저 수행하세요.");
        if (!c.State.IsUniform()) Pre(e, "NOT_PLANAR", "v1 증착은 평탄 표면에 수직 막 추가만 지원합니다(단차 피복 모델 없음).", "평탄한 상태에서 증착하세요.");
    }

    public override void Interlock(Recipe r, Ctx c, List<Err> e)
    {
        double T = r.N("temperature"), P = r.N("pressure");
        switch (r.E("method"))
        {
            case "CVD":
                if (T > 800) Ilk(e, "CVD_TEMPERATURE_LIMIT", "temperature", T, "≤ 800 °C", "CvdEdu 온도 한계를 넘었습니다.");
                if (P > 100) Ilk(e, "CVD_PRESSURE_LIMIT", "pressure", P, "≤ 100 Torr", "CvdEdu 압력 한계를 넘었습니다.");
                break;
            case "PVD":
                if (r.N("power") > 1500) Ilk(e, "PVD_POWER_LIMIT", "power", r.N("power"), "≤ 1500 W", "PvdEdu 전력 한계를 넘었습니다.");
                break;
            default:
                if (T > 350) Ilk(e, "ALD_TEMPERATURE_LIMIT", "temperature", T, "≤ 350 °C", "AldEdu 온도 한계를 넘었습니다.");
                if (r.N("cycles") > 3000) Ilk(e, "ALD_CYCLE_LIMIT", "cycles", r.N("cycles"), "≤ 3000", "AldEdu 사이클 한계를 넘었습니다.");
                break;
        }
    }

    public override void Domain(Recipe r, Ctx c, List<Err> e)
    {
        double th = Thickness(r);
        if (th > 10000) Dom(e, "FILM_TOO_THICK", $"새 막 두께 {F(th)} nm가 모델 한계 10000 nm를 넘습니다.", "시간이나 사이클을 줄이세요.");
    }

    public override Output Calculate(Recipe r, Ctx c, CancellationToken t)
    {
        var s = c.State.CloneAsChild();
        double th = Thickness(r);
        string mat = r.E("material");
        var id = s.NewLayer(mat, "deposition-" + r.E("method"));
        foreach (var col in s.Columns) col.Segs.Add(new Seg { Material = mat, Thickness = th, LayerId = id });
        var per = s.Columns.Select(col => col.LayerThickness(id)).ToArray();
        double mean = per.Average();
        var o = new Output { Candidate = s };
        o.M("thickness", $"새 막 {id} 평균 두께", mean, "nm");
        o.M("nonuniformity", "불균일도(모델 가정상 0)", mean > 0 ? 100 * (per.Max() - per.Min()) / (2 * mean) : null, "%", mean > 0 ? null : "평균 두께 0");
        if (r.E("method") == "ALD")
        {
            o.M("purgePenalty", "퍼지 페널티(교육용 지표)", Math.Max(0, (5 - r.N("purgeTime")) / 5) * 20, "%");
            o.M("aldTotalTime", "ALD 총 시간", r.N("cycles") * 2 * (r.N("pulseTime") + r.N("purgeTime")), "s");
        }
        o.Warnings.Add($"새 LayerId {id}를 만들었습니다. 각 열에 같은 두께를 더하므로 불균일도 0은 실제 장비 균일도를 뜻하지 않습니다.");
        return o;
    }

    public override List<Criterion> Criteria(Recipe r, Ctx c, Output o)
    {
        var list = new List<Criterion> { new("thickness", CK.RelTarget, r.N("targetThickness"), r.N("thicknessTolerance")) };
        if (r.E("method") == "ALD") list.Add(new("purgePenalty", CK.Max, r.N("maxPurgePenalty")));
        else list.Add(new("nonuniformity", CK.Max, r.N("maxNonuniformity")));
        return list;
    }
}

public sealed class CmpProcess : ProcessBase
{
    public override string Id => "cmp";
    public override string Group => "⑥ 금속 배선";
    public override string Name => "CMP(화학적 기계 연마)";
    public override string EquipmentId => "CmpEdu";
    public override string[] Unsupported => new[] { "패드 탄성", "실제 선택비", "디싱·에로전", "웨이퍼 크기 효과", "실제 Preston 계수(회전수 지표 사용)" };

    static readonly ParamDef[] Ps =
    {
        En("slurry", "슬러리", "Oxide", "Metal"),
        EnF("targetMaterial", "대상 재료", f => f.E("slurry") == "Oxide" ? new[] { Mat.SiO2 } : new[] { Mat.Al, Mat.Cu, Mat.W, Mat.TiN }),
        Num("polishPressure", "연마 압력", "kPa", 5, 70, 20),
        Num("padSpeed", "패드 회전수", "rpm", 10, 200, 60),
        Num("waferSpeed", "웨이퍼 회전수", "rpm", 10, 200, 60),
        Num("duration", "연마 시간", "s", 10, 600, 60),
        Q("targetThickness", "목표 잔여 두께", "nm", 1, 10000, 28),
        Q("thicknessTolerance", "두께 허용오차", "%", 0, 50, 10),
        Q("maxStepHeight", "최대 단차", "nm", 0, 10000, 20),
    };
    public override IReadOnlyList<ParamDef> Params => Ps;

    static double TopTarget(Column c, string tgt)
    {
        double t = 0;
        for (int i = c.Segs.Count - 1; i >= 0 && c.Segs[i].Material == tgt; i--) t += c.Segs[i].Thickness;
        return t;
    }

    public override void Preconditions(Recipe r, Ctx c, List<Err> e)
    {
        var s = c.State;
        string tgt = r.E("targetMaterial");
        if (s.HasPR) Pre(e, "PR_PRESENT", "PR이 있으면 CMP를 할 수 없습니다.", "PRStrip을 먼저 수행하세요.");
        if (s.Wires.Count > 0 || s.Contacts.Count > 0) Pre(e, "METAL_MODEL_UNSUPPORTED", "배선·컨택 형상을 바꾸는 CMP 모델은 v1에서 지원하지 않습니다.", "배선 전에 CMP를 수행하세요.");
        if (s.Columns.Any(col => col.TopMaterial != tgt)) Pre(e, "TOP_NOT_TARGET", $"v1은 모든 열의 최상층이 {tgt}인 상태만 지원합니다(혼합 표면 미지원).", "대상 재료를 확인하세요.");
    }

    public override void Interlock(Recipe r, Ctx c, List<Err> e)
    {
        if (r.N("polishPressure") > 50) Ilk(e, "CMP_PRESSURE_LIMIT", "polishPressure", r.N("polishPressure"), "≤ 50 kPa", "CmpEdu 연마 압력 한계를 넘었습니다.");
    }

    public override Output Calculate(Recipe r, Ctx c, CancellationToken t)
    {
        var s = c.State.CloneAsChild();
        string tgt = r.E("targetMaterial");
        double k = r.E("slurry") == "Oxide" ? 0.001 : 0.002;
        double speed = (r.N("padSpeed") + r.N("waferSpeed")) / 2;
        double baseRemoval = k * r.N("polishPressure") * speed * r.N("duration");
        double hmin = s.Columns.Min(col => col.Surface);
        var start = s.Columns.Select(col => col.Surface).ToArray();
        double sumRemoved = 0;
        bool exhausted = false;
        for (int i = 0; i < s.N; i++)
        {
            var col = s.Columns[i];
            double rem = baseRemoval * (1 + (start[i] - hmin) / 1000);
            while (rem > 0 && col.Segs.Count > 0 && col.Segs[^1].Material == tgt)
            {
                var top = col.Segs[^1];
                if (top.Thickness <= rem) { rem -= top.Thickness; sumRemoved += top.Thickness; col.Segs.RemoveAt(col.Segs.Count - 1); }
                else { top.Thickness -= rem; sumRemoved += rem; rem = 0; }
            }
            if (rem > 0) exhausted = true;
        }
        var o = new Output { Candidate = s };
        o.M("remainingThickness", "대상층 잔여 두께(평균)", s.Columns.Average(col => TopTarget(col, tgt)), "nm");
        o.M("stepHeight", "최종 단차(peak-to-valley)", s.Columns.Max(col => col.Surface) - s.Columns.Min(col => col.Surface), "nm");
        o.M("removal", "평균 제거량", sumRemoved / s.N, "nm");
        o.M("baseRemoval", "기준 제거량", baseRemoval, "nm");
        if (exhausted) o.Warnings.Add("일부 열에서 대상층이 모두 제거되어 하부 재료에서 멈췄습니다(음수 두께 없음).");
        return o;
    }

    public override List<Criterion> Criteria(Recipe r, Ctx c, Output o) => new()
    {
        new("remainingThickness", CK.RelTarget, r.N("targetThickness"), r.N("thicknessTolerance")),
        new("stepHeight", CK.Max, r.N("maxStepHeight")),
    };
}
