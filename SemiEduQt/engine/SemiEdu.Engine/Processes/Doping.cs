namespace SemiEdu.Engine;

public sealed class ImplantProcess : ProcessBase<ImplantParameters>
{
    protected override ImplantParameters Bind(Recipe recipe) => recipe;

    public override string Id => "implant";
    public override string Group => "⑤ 증착·이온주입";
    public override string Name => "이온주입";
    public override string EquipmentId => "ImplantEdu";
    public override string[] Unsupported => new[]
    {
        "채널링", "손상·비정질화", "횡방향 산란·그림자 효과", "차폐막 투과(이상적 차폐 가정, X1)", "스크린 산화막 투과 손실(X2)",
    };

    public static double DefaultDepth(string sp) => sp switch { "B" => 150, "P" => 100, _ => 50 };
    static double K(string sp) => sp switch { "B" => 3, "P" => 2, _ => 1 };

    static readonly ParamDef[] Ps =
    {
        En("species", "이온", "B", "P", "As"),
        Num("energy", "에너지", "keV", 1, 500, 50),
        Num("implantDose", "주입량", "ions/cm²", 1e11, 1e16, 1e13),
        Num("tilt", "기울기", "°", 0, 45, 7),
        QF("targetDepth", "목표 평균 주입 깊이", "nm", f => (1.0, 5000.0, DefaultDepth(f.E("species")))),
        Q("depthTolerance", "깊이 허용오차", "%", 0, 100, 20),
    };
    public override IReadOnlyList<ParamDef> Params => Ps;

    public static (double Rp, double Sigma) RpSigma(ImplantParameters r)
    {
        double rp = K(r.Species) * Required(r.Energy) * Math.Cos(Required(r.Tilt) * Math.PI / 180);
        return (rp, Math.Max(1, 0.30 * rp));
    }

    sealed class Plan
    {
        public bool[] Implanted = Array.Empty<bool>();
        public double[] Shield = Array.Empty<double>();
        public double ToxMin = double.PositiveInfinity, ToxMax = double.NegativeInfinity;
        public bool BadStack;
    }

    static Plan Analyze(WaferState s)
    {
        var gateLayers = s.Wires.Where(w => w.Role == "Gate").Select(w => w.LayerId).ToHashSet();
        var p = new Plan { Implanted = new bool[s.N], Shield = new double[s.N] };
        for (int i = 0; i < s.N; i++)
        {
            var col = s.Columns[i];
            double shield = 0;
            foreach (var g in col.Segs)
                if (g.Material == Mat.PR || (Mat.IsMetal(g.Material) && gateLayers.Contains(g.LayerId))) shield += g.Thickness;
            p.Shield[i] = shield;
            p.Implanted[i] = shield <= 0;
            if (!p.Implanted[i]) continue;
            if (col.Segs.Any(g => g.Material != Mat.SiO2)) p.BadStack = true;
            double tox = col.Total(Mat.SiO2);
            p.ToxMin = Math.Min(p.ToxMin, tox);
            p.ToxMax = Math.Max(p.ToxMax, tox);
        }
        return p;
    }

    static (double Dz, int N) Grid(ImplantParameters r, WaferState s)
    {
        var (rp, sig) = RpSigma(r);
        double zmax = Math.Max(2000, rp + 8 * sig), dz = sig / 8;
        if (s.Doping != null)
        {
            dz = Math.Min(dz, s.Doping.Dz);
            zmax = Math.Max(zmax, s.Doping.ZMax);
        }
        return (dz, (int)Math.Ceiling(zmax / dz - 1e-9) + 1);
    }

    public override void Preconditions(ImplantParameters r, Ctx c, List<Err> e)
    {
        var s = c.State;
        var gateLayers = s.Wires.Where(w => w.Role == "Gate").Select(w => w.LayerId).ToHashSet();
        if (s.Contacts.Count > 0 || s.Columns.Any(col => col.Segs.Any(g => Mat.IsMetal(g.Material) && !gateLayers.Contains(g.LayerId))))
            Pre(e, "METAL_PRESENT", "게이트 금속(role=Gate) 외의 금속이 있어 이온주입을 할 수 없습니다.", "금속 공정 전에 주입하세요.");
        var plan = Analyze(s);
        if (!plan.Implanted.Any(x => x)) { Pre(e, "NO_EXPOSED_COLUMNS", "모든 열이 PR 또는 게이트로 덮여 주입될 영역이 없습니다.", "차폐 영역을 확인하세요."); return; }
        if (plan.BadStack) Pre(e, "NOT_BARE_SI", "노출 열에는 Si 또는 20 nm 이하의 균일 SiO2(스크린 산화막)만 허용합니다.", "노출 영역의 막을 확인하세요.");
        else if (plan.ToxMax - plan.ToxMin > 1e-6) Pre(e, "SCREEN_OXIDE_NONUNIFORM", "노출 열의 스크린 산화막 두께가 균일하지 않습니다.", "균일한 산화막 위에서 주입하세요.");
        else if (plan.ToxMax > 20) Pre(e, "SCREEN_OXIDE_TOO_THICK", $"스크린 산화막 {F(plan.ToxMax)} nm가 20 nm를 넘습니다(X2).", "더 얇은 산화막에서 주입하세요.");
    }

    public override void Interlock(ImplantParameters r, Ctx c, List<Err> e)
    {
        if (Required(r.Energy) > 200) Ilk(e, "IMPLANT_ENERGY_LIMIT", "energy", Required(r.Energy), "≤ 200 keV", "ImplantEdu 에너지 한계를 넘었습니다.");
        if (Required(r.ImplantDose) > 1e15) Ilk(e, "IMPLANT_DOSE_LIMIT", "implantDose", Required(r.ImplantDose), "≤ 1e15 ions/cm²", "ImplantEdu 주입량 한계를 넘었습니다.");
    }

    public override void Domain(ImplantParameters r, Ctx c, List<Err> e)
    {
        var (rp, sig) = RpSigma(r);
        if (Math.Max(2000, rp + 8 * sig) > 10000) Dom(e, "GRID_DEPTH_LIMIT", "도핑 격자 깊이가 10000 nm를 넘습니다.", "에너지를 낮추세요.");
        var (_, n) = Grid(r, c.State);
        if (n > 8192) Dom(e, "GRID_CELL_LIMIT", $"도핑 격자가 {n}셀로 최대 8192셀을 넘습니다(Δz ≤ σ/8).", "에너지를 높이거나 조건을 바꾸세요.");
        var plan = Analyze(c.State);
        double need = rp + 4 * sig;
        for (int i = 0; i < c.State.N; i++)
            if (!plan.Implanted[i] && plan.Shield[i] < need)
            {
                Dom(e, "MASK_TOO_THIN", $"차폐막 두께 {F(plan.Shield[i])} nm가 Rp+4σ = {F(need)} nm보다 얇습니다(X1).", "차폐막을 두껍게 하거나 에너지를 낮추세요.");
                break;
            }
    }

    public override Output Calculate(ImplantParameters r, Ctx c, CancellationToken t)
    {
        var s = c.State.CloneAsChild();
        var plan = Analyze(c.State);
        var (rp, sig) = RpSigma(r);
        var (dz, n) = Grid(r, c.State);
        if (s.Doping == null)
            s.Doping = new DopingMap { Dz = dz, N = n, Groups = { new ColumnGroup { Start = 0, End = s.N } } };
        else if (dz < s.Doping.Dz - 1e-15 || n > s.Doping.N)
            s.Doping.Regrid(dz, n);
        var map = s.Doping;
        map.SplitBy(plan.Implanted);

        double tox = plan.ToxMax > double.NegativeInfinity ? plan.ToxMax : 0;
        double center = rp - tox, dose = Required(r.ImplantDose);
        var g = new double[map.N];
        for (int i = 0; i < map.N; i++)
        {
            double z = i * map.Dz;
            g[i] = Math.Exp(-(z - center) * (z - center) / (2 * sig * sig));
        }
        double integral = DopingMap.Trap(g, map.Dz * 1e-7);
        var conc = g.Select(v => dose * v / integral).ToArray();
        string sp = r.Species;
        foreach (var grp in map.Groups)
        {
            if (!plan.Implanted[grp.Start]) continue;
            var prof = grp.Get(sp, map.N);
            for (int i = 0; i < map.N; i++)
            {
                prof.Total[i] += conc[i];
                prof.Active[i] += 0.10 * conc[i];
            }
        }
        double integrated = DopingMap.Trap(conc, map.Dz * 1e-7);
        double relErr = Math.Abs(integrated - dose) / dose;
        var o = new Output { Candidate = s };
        o.M("meanDepth", "평균 주입 깊이(1차 모멘트)", DopingMap.MeanDepth(conc, map.Dz), "nm");
        o.M("doseRelError", "dose 보존 상대오차", relErr, "");
        o.M("rp", "Rp(가우시안 중심, Si 기준 전)", rp, "nm");
        o.M("sigma", "σ", sig, "nm");
        o.M("screenOxide", "스크린 산화막", tox, "nm");
        o.M("implantedColumns", "주입된 열 수", plan.Implanted.Count(x => x), "열");
        o.M("activeFraction", "주입 직후 활성 분율", 10, "%");
        if (relErr > 1e-6)
            o.InvariantErrors.Add(new Err("DOSE_NOT_CONSERVED", "Invariant", "implantDose", Fmt.S(integrated), Fmt.S(dose), "주입량이 보존되지 않았습니다.", "격자 조건을 확인하세요."));
        if (tox > 0) o.Warnings.Add($"스크린 산화막 {F(tox)} nm: Si 좌표 가우시안 중심을 Rp − tox로 옮기고 z ≥ 0에서 다시 정규화(X2, 투과 손실 없음).");
        if (plan.Implanted.Any(x => !x)) o.Warnings.Add("PR·게이트가 덮은 열은 주입량 0으로 처리했습니다(이상적 차폐, X1).");
        return o;
    }

    public override List<Criterion> Criteria(ImplantParameters r, Ctx c, Output o) =>
        new() { new("meanDepth", CK.RelTarget, Required(r.TargetDepth), Required(r.DepthTolerance)) };
}

public sealed class AnnealProcess : ProcessBase<AnnealParameters>
{
    protected override AnnealParameters Bind(Recipe recipe) => recipe;

    public override string Id => "anneal";
    public override string Group => "⑤ 증착·이온주입";
    public override string Name => "열처리(활성화)";
    public override string EquipmentId => "AnnealEdu";
    public override string[] Unsupported => new[]
    {
        "농도 의존 확산", "산화 유도 확산(OED)", "결정 손상·TED", "승온·냉각 램프", "측방향 확산(게이트 아래 파고듦 없음)", "N2·Ar 분위기 차이(동일 불활성 분위기)",
    };

    static double D0(string sp) => sp switch { "B" => 1, "P" => 0.5, _ => 0.2 };
    public const int Steps = 200;

    static readonly ParamDef[] Ps =
    {
        En("method", "방식", "RTA", "Furnace"),
        new() { Id = "ambient", Label = "분위기", Kind = Kind.Enum, Options = _ => new[] { "N2", "Ar" }, Note = "v1은 N2·Ar를 동일한 불활성 분위기로 모델링" },
        Num("temperature", "온도", "°C", 200, 1200, 1000),
        NumF("duration", "시간", "s", f => f.E("method") == "RTA" ? (1.0, 300.0, 30.0) : (60.0, 7200.0, 1800.0)),
        Q("minActivation", "최소 활성화율", "%", 0, 100, 80),
    };
    public override IReadOnlyList<ParamDef> Params => Ps;

    public override void Preconditions(AnnealParameters r, Ctx c, List<Err> e)
    {
        var s = c.State;
        if (s.HasPR) Pre(e, "PR_PRESENT", "PR이 남아 있어 열처리를 할 수 없습니다.", "PRStrip을 먼저 수행하세요.");
        var gate = s.Wires.Where(w => w.Role == "Gate" && w.Material is Mat.W or Mat.TiN).Select(w => w.LayerId).ToHashSet();
        if (s.Contacts.Count > 0 || s.Wires.Any(w => !gate.Contains(w.LayerId))
            || s.Columns.Any(col => col.Segs.Any(g => Mat.IsMetal(g.Material) && !gate.Contains(g.LayerId))))
            Pre(e, "METAL_NOT_REFRACTORY", "role=Gate인 W·TiN 외의 금속(Al·Cu·컨택 금속)이 있어 열처리를 할 수 없습니다(X3).", "금속 배선 전에 열처리하세요.");
    }

    public override void Interlock(AnnealParameters r, Ctx c, List<Err> e)
    {
        if (Required(r.Temperature) > 1100) Ilk(e, "ANNEAL_TEMPERATURE_LIMIT", "temperature", Required(r.Temperature), "≤ 1100 °C", "AnnealEdu 온도 한계를 넘었습니다.");
    }

    static double D(string sp, double T) => D0(sp) * Math.Exp((T - 1000) / 100);

    static int NeededCells(AnnealParameters r, DopingMap m)
    {
        double T = Required(r.Temperature), t = Required(r.Duration);
        double dmax = m.SpeciesPresent().Select(sp => D(sp, T)).DefaultIfEmpty(0).Max();
        double need = m.SignificantDepth() + 8 * Math.Sqrt(2 * dmax * t);
        return Math.Max(m.N, (int)Math.Ceiling(need / m.Dz) + 1);
    }

    public override void Domain(AnnealParameters r, Ctx c, List<Err> e)
    {
        var m = c.State.Doping;
        if (m == null) return;
        int n = NeededCells(r, m);
        if ((n - 1) * m.Dz > 10000 || n > 8192)
            Dom(e, "GRID_LIMIT", $"확산 여유(8·√(2Dt))를 포함한 격자가 10000 nm/8192셀 한계를 넘습니다({n}셀).", "온도나 시간을 낮추세요.");
    }

    /// <summary>Finite-volume backward Euler with zero-flux ends; conserves the trapezoid integral exactly.</summary>
    public static double[] Diffuse(double[] c, double d, double dz, double dt, int steps, CancellationToken tok)
    {
        int n = c.Length;
        var x = (double[])c.Clone();
        if (n < 2 || d <= 0) return x;
        double r = d * dt / (dz * dz);
        var b = new double[n];
        for (int i = 0; i < n; i++) b[i] = (i == 0 || i == n - 1) ? 0.5 + r : 1 + 2 * r;
        var cp = new double[n];
        var dp = new double[n];
        for (int s = 0; s < steps; s++)
        {
            tok.ThrowIfCancellationRequested();
            for (int i = 0; i < n; i++)
            {
                double v = (i == 0 || i == n - 1) ? 0.5 : 1.0;
                double rhs = v * x[i];
                double a = i == 0 ? 0 : -r, cc = i == n - 1 ? 0 : -r;
                double denom = b[i] - (i == 0 ? 0 : a * cp[i - 1]);
                cp[i] = cc / denom;
                dp[i] = (rhs - (i == 0 ? 0 : a * dp[i - 1])) / denom;
            }
            x[n - 1] = dp[n - 1];
            for (int i = n - 2; i >= 0; i--) x[i] = dp[i] - cp[i] * x[i + 1];
        }
        return x;
    }

    public override Output Calculate(AnnealParameters r, Ctx c, CancellationToken t)
    {
        var s = c.State.CloneAsChild();
        var o = new Output { Candidate = s };
        double T = Required(r.Temperature), time = Required(r.Duration);
        double tau = r.Method == "RTA" ? 10 : 600;
        double inc = 1 - Math.Exp(-(time / tau) * Math.Exp((T - 1000) / 100));
        var m = s.Doping;
        if (m == null || !s.HasDoping)
        {
            o.M("activation", "주입 성분 활성 분율(종별 최솟값)", null, "%", "주입 성분이 없어 활성화율을 정의할 수 없음");
            o.Warnings.Add("주입 이력이 없어 확산·활성화 계산을 하지 않았습니다.");
            return o;
        }
        m.Extend(NeededCells(r, m));
        double maxErr = 0;
        foreach (var g in m.Groups)
            foreach (var sp in g.Species)
            {
                double d = D(sp.Species, T), dt = time / Steps;
                double before = DopingMap.Trap(sp.Total, m.Dz), beforeA = DopingMap.Trap(sp.Active, m.Dz);
                sp.Total = Diffuse(sp.Total, d, m.Dz, dt, Steps, t);
                sp.Active = Diffuse(sp.Active, d, m.Dz, dt, Steps, t);
                double after = DopingMap.Trap(sp.Total, m.Dz), afterA = DopingMap.Trap(sp.Active, m.Dz);
                if (before > 0) maxErr = Math.Max(maxErr, Math.Abs(after - before) / before);
                if (beforeA > 0) maxErr = Math.Max(maxErr, Math.Abs(afterA - beforeA) / beforeA);
                for (int i = 0; i < m.N; i++) sp.Active[i] += (sp.Total[i] - sp.Active[i]) * inc;
            }
        double minAct = double.PositiveInfinity;
        foreach (var sp in m.SpeciesPresent())
        {
            double tot = 0, act = 0;
            foreach (var g in m.Groups)
            {
                var p = g.Species.FirstOrDefault(x => x.Species == sp);
                if (p == null) continue;
                tot += g.Count * DopingMap.Trap(p.Total, m.Dz);
                act += g.Count * DopingMap.Trap(p.Active, m.Dz);
            }
            if (tot > 0) minAct = Math.Min(minAct, act / tot * 100);
        }
        o.M("activation", "주입 성분 활성 분율(종별 최솟값)", minAct, "%");
        o.M("doseConservation", "종별 적분 보존 상대오차(최대)", maxErr, "");
        o.M("diffusionLength", "확산 길이 √(2Dt)(최대 종)", m.SpeciesPresent().Select(sp => Math.Sqrt(2 * D(sp, T) * time)).Max(), "nm");
        o.M("activationIncrement", "이번 활성화 증분", inc * 100, "%");
        if (maxErr > 1e-6)
            o.InvariantErrors.Add(new Err("DOSE_NOT_CONSERVED", "Invariant", null, Fmt.S(maxErr), "≤ 1e-6", "무유량 확산에서 종별 적분이 보존되지 않았습니다.", "격자를 확인하세요."));
        o.Warnings.Add("N2·Ar는 동일한 불활성 분위기로 모델링합니다. 측방향 확산은 없습니다.");
        return o;
    }

    public override List<Criterion> Criteria(AnnealParameters r, Ctx c, Output o) =>
        new() { new("activation", CK.Min, Required(r.MinActivation)) };
}

public sealed class MetalProcess : ProcessBase<MetalParameters>
{
    protected override MetalParameters Bind(Recipe recipe) => recipe;

    public override string Id => "metal";
    public override string Group => "⑥ 금속 배선";
    public override string Name => "금속 배선(이상적 패턴)";
    public override string EquipmentId => "WireEdu";
    public override string[] Unsupported => new[]
    {
        "증착·포토·식각·도금·배리어를 대체한 교육용 묶음 동작", "접촉 저항", "배리어층", "단선·EM", "기생 성분", "크기·온도에 따른 비저항 보정",
    };

    static Mask? GateMask(FormCtx f) => f.E("maskId") == "None" ? null : f.C.Session.Masks.FirstOrDefault(m => m.MaskId == f.E("maskId"));

    static readonly ParamDef[] Ps =
    {
        En("metal", "금속", Mat.Al, Mat.Cu, Mat.W),
        En("mode", "구현 모드", "IdealPattern"),
        En("role", "역할", "Interconnect", "Gate"),
        EnF("maskId", "마스크(Gate층, 선택)", f => new[] { "None" }.Concat(f.C.Session.Masks
            .Where(m => m.MaskLayer == "Gate" && (m.Passed || f.C.Eq.ContinueOnFailure)).Select(m => m.MaskId)).ToArray()),
        Num("width", "배선 폭", "nm", 10, 10000, 1000, f => f.E("maskId") == "None"),
        Info("maskGeom", "마스크에서 온 폭·위치(읽기 전용)", f => GateMask(f) is { } m ? $"폭 {F(m.MaskCd)} nm · 시작 {F(m.OffsetUm)} µm" : null,
            f => f.E("maskId") != "None"),
        Num("thickness", "배선 두께", "nm", 10, 5000, 500),
        Num("length", "배선 길이(화면 안쪽 방향)", "µm", 0.1, 10000, 100),
        Num("offset", "단면 시작 위치", "µm", 0, 100, 0, f => f.E("maskId") == "None"),
        Q("maxResistance", "최대 저항", "Ω", 0.001, 1e6, 100),
    };
    public override IReadOnlyList<ParamDef> Params => Ps;

    public static (double WidthNm, double OffsetUm, Mask? Mask) Geometry(MetalParameters r, Ctx c)
    {
        if (r.MaskId == "None") return (Required(r.Width), Required(r.Offset), null);
        var m = c.Session.Masks.First(x => x.MaskId == r.MaskId);
        return (m.MaskCd, m.OffsetUm, m);
    }

    public override void Cross(MetalParameters r, Ctx c, List<Err> e)
    {
        var (w, off, _) = Geometry(r, c);
        if (off + w / 1000 > c.State.DomainWidthUm + 1e-9)
            InputErr(e, "WIRE_OUTSIDE_DOMAIN", "offset", F(off), $"offset + width ≤ {F(c.State.DomainWidthUm)} µm", "배선이 단면 폭을 넘습니다.");
    }

    public override void Preconditions(MetalParameters r, Ctx c, List<Err> e)
    {
        var s = c.State;
        if (s.HasPR) Pre(e, "PR_PRESENT", "PR이 남아 있어 배선을 만들 수 없습니다.", "PRStrip을 먼저 수행하세요.");
        if (!s.IsUniform() || s.Columns.Any(col => col.TopMaterial is not (Mat.SiO2 or Mat.Si3N4)))
            Pre(e, "NOT_PLANAR_DIELECTRIC", "PR 없는 평탄한 SiO2 또는 Si3N4 위에만 이상적 배선을 추가할 수 있습니다.", "절연막을 먼저 형성하세요.");
        var (_, _, m) = Geometry(r, c);
        if (m != null && m.RepeatCount != 1) Pre(e, "MASK_NOT_SINGLE", "배선용 마스크는 반복 수 1이어야 합니다.", "repeatCount 1인 Gate층 마스크를 쓰세요.");
    }

    public override void Domain(MetalParameters r, Ctx c, List<Err> e)
    {
        var (w, off, _) = Geometry(r, c);
        var (a, b) = c.State.ColumnsIn(off, off + w / 1000);
        if (w < 4 * c.State.ColWidthNm || b - a < 4)
            Dom(e, "GEOMETRY_RESOLUTION", $"배선 폭 {F(w)} nm가 열 폭 {F(c.State.ColWidthNm)} nm의 4배보다 작습니다.", "폭을 키우거나 단면 폭을 줄이세요.");
    }

    public override Output Calculate(MetalParameters r, Ctx c, CancellationToken t)
    {
        var s = c.State.CloneAsChild();
        var (w, off, m) = Geometry(r, c);
        var (a, b) = s.ColumnsIn(off, off + w / 1000);
        string metal = r.Metal;
        var id = s.NewLayer(metal, r.Role == "Gate" ? "gate" : "wire");
        for (int i = a; i < b; i++) s.Columns[i].Segs.Add(new Seg { Material = metal, Thickness = Required(r.Thickness), LayerId = id });
        var wire = new Wire
        {
            WireId = s.NewId("W"), Material = metal, Role = r.Role, MaskId = m?.MaskId ?? "None",
            ColStart = a, ColEnd = b, WidthNm = w, ThicknessNm = Required(r.Thickness), LengthUm = Required(r.Length), OffsetUm = off, LayerId = id,
        };
        s.Wires.Add(wire);
        var o = new Output { Candidate = s };
        o.M("resistance", $"배선 {wire.WireId} 저항", wire.ResistanceOhm, "Ω");
        o.M("wireColumns", "배선 열 수", b - a, "열");
        o.Warnings.Add("이 단계는 증착·포토·식각·도금·배리어를 대체한 교육용 묶음 동작입니다. 배선 길이는 화면 안쪽 방향이며 단면 폭이 아닙니다.");
        return o;
    }

    public override List<Criterion> Criteria(MetalParameters r, Ctx c, Output o) => new() { new("resistance", CK.Max, Required(r.MaxResistance)) };
}

public sealed class ContactProcess : ProcessBase<ContactParameters>
{
    protected override ContactParameters Bind(Recipe recipe) => recipe;

    public override string Id => "contact";
    public override string Group => "⑥ 금속 배선";
    public override string Name => "컨택 형성(IdealContact)";
    public override string EquipmentId => "WireEdu";
    public override string[] Unsupported => new[] { "실제 개구 식각·라이너·배리어", "컨택 저항", "실리사이드" };

    static string[] ContactMasks(FormCtx f) => f.C.Session.Masks
        .Where(m => m.MaskLayer == "Contact" && m.RepeatCount == 1 && (m.Passed || f.C.Eq.ContinueOnFailure)).Select(m => m.MaskId).ToArray();

    static readonly ParamDef[] Ps =
    {
        En("metal", "플러그 금속", Mat.W, Mat.Al),
        new() { Id = "sourceMaskId", Label = "소스 컨택 마스크", Kind = Kind.Enum, Options = ContactMasks, Note = "Contact층·반복 수 1 마스크" },
        new() { Id = "drainMaskId", Label = "드레인 컨택 마스크", Kind = Kind.Enum, Options = ContactMasks,
            EnumDefault = f => { var o = ContactMasks(f); return o.Length > 1 ? o[1] : o.FirstOrDefault(); }, Note = "Contact층·반복 수 1 마스크" },
        Num("plugAboveNm", "산화막 위 플러그 높이", "nm", 10, 2000, 300),
    };
    public override IReadOnlyList<ParamDef> Params => Ps;

    static (int A, int B) Cols(Ctx c, string maskId)
    {
        var m = c.Session.Masks.First(x => x.MaskId == maskId);
        return c.State.ColumnsIn(m.OffsetUm, m.OffsetUm + m.MaskCd / 1000);
    }

    public override void Cross(ContactParameters r, Ctx c, List<Err> e)
    {
        if (r.SourceMaskId == r.DrainMaskId)
            InputErr(e, "SAME_MASK", "drainMaskId", r.DrainMaskId, "소스와 다른 마스크", "소스와 드레인에 같은 마스크를 쓸 수 없습니다.");
    }

    public override void Preconditions(ContactParameters r, Ctx c, List<Err> e)
    {
        var s = c.State;
        if (s.HasPR) Pre(e, "PR_PRESENT", "PR이 남아 있어 컨택을 만들 수 없습니다.", "PRStrip을 먼저 수행하세요.");
        var src = Cols(c, r.SourceMaskId);
        var drn = Cols(c, r.DrainMaskId);
        foreach (var (name, (a, b)) in new[] { ("소스", src), ("드레인", drn) })
        {
            if (b <= a) { Pre(e, "CONTACT_EMPTY", $"{name} 컨택이 단면 안에 없습니다.", "마스크 위치를 확인하세요."); continue; }
            for (int i = a; i < b; i++)
                if (s.Columns[i].Segs.Count == 0 || s.Columns[i].Segs.Any(g => g.Material != Mat.SiO2))
                {
                    Pre(e, "CONTACT_STACK_INVALID", $"{name} 컨택 열의 적층은 ‘Si 위 SiO2만’이어야 합니다.", "산화막만 있는 영역에 컨택을 두세요.");
                    break;
                }
            foreach (var g in s.Wires.Where(w => w.Role == "Gate"))
                if (a < g.ColEnd && g.ColStart < b)
                    Pre(e, "CONTACT_OVERLAPS_GATE", $"{name} 컨택이 게이트 열({g.WireId})과 겹칩니다.", "게이트 또는 컨택 마스크 위치를 바꾸세요.");
        }
        if (src.A < drn.B && drn.A < src.B && src.B > src.A) Pre(e, "CONTACT_OVERLAP", "소스와 드레인 컨택 열이 겹칩니다.", "컨택 마스크 위치를 바꾸세요.");
        foreach (var ct in s.Contacts)
            if ((src.A < ct.ColEnd && ct.ColStart < src.B) || (drn.A < ct.ColEnd && ct.ColStart < drn.B))
                Pre(e, "CONTACT_OVERLAP", $"기존 컨택 {ct.ContactId}과 겹칩니다.", "Undo 후 다시 실행하세요.");
    }

    public override Output Calculate(ContactParameters r, Ctx c, CancellationToken t)
    {
        var s = c.State.CloneAsChild();
        string metal = r.Metal;
        double plug = Required(r.PlugAboveNm);
        var o = new Output { Candidate = s };
        bool siContact = true;
        foreach (var (role, maskId) in new[] { ("Source", r.SourceMaskId), ("Drain", r.DrainMaskId) })
        {
            var (a, b) = Cols(c, maskId);
            var id = s.NewLayer(metal, "contact-" + role);
            for (int i = a; i < b; i++)
            {
                var col = s.Columns[i];
                double tox = col.Total(Mat.SiO2);
                col.Segs.Clear();
                col.Segs.Add(new Seg { Material = metal, Thickness = tox + plug, LayerId = id });
                siContact &= col.Segs.Count == 1;
            }
            s.Contacts.Add(new ContactRec { ContactId = s.NewId("C"), Role = role, Metal = metal, ColStart = a, ColEnd = b, LayerId = id });
            o.M(role == "Source" ? "sourceColumns" : "drainColumns", role == "Source" ? "소스 컨택 열 수" : "드레인 컨택 열 수", b - a, "열");
        }
        o.M("siContact", "컨택 하부 Si 접촉", siContact ? 1 : 0, "");
        o.Warnings.Add("산화막 개구·식각·플러그 충진을 하나로 묶은 교육용 동작입니다.");
        return o;
    }

    public override List<Criterion> Criteria(ContactParameters r, Ctx c, Output o) => new()
    {
        new("sourceColumns", CK.Min, 4), new("drainColumns", CK.Min, 4), new("siContact", CK.Present, 0),
    };
}

public sealed class InspectionProcess : ProcessBase<InspectionParameters>
{
    protected override InspectionParameters Bind(Recipe recipe) => recipe;

    public override string Id => "inspection";
    public override string Group => "⑦ EDS(검사)";
    public override string Name => "검사";
    public override bool ChangesState => false;
    public override string EquipmentId => "InspectionEdu";
    public override string[] Unsupported => new[] { "측정 노이즈·샘플링 오차", "실제 장비 측정(모델 상태의 이상적 읽기)", "수율" };

    static string Mode(FormCtx f) => f.E("inspectionMode");
    static bool Locked(FormCtx f) => f.E("qualityProfile") == "EducationalDefault";

    public static string[] Targets(WaferState s, string mode) => mode switch
    {
        "Thickness" => s.PresentLayerIds().ToArray(),
        "CD" => s.Resist != null ? new[] { s.Resist.PatternId } : Array.Empty<string>(),
        "Contamination" => new[] { "WAFER" },
        "Electrical" => s.Wires.Select(w => w.WireId).ToArray(),
        _ => s.Doping?.SpeciesPresent().Select(sp => "IMP-" + sp).ToArray() ?? Array.Empty<string>(),
    };

    static ParamDef QL(string id, string label, string unit, double min, double max, double def, string mode) => new()
    {
        Id = id, Label = label, Unit = unit, Kind = Kind.Double, Role = Role.QualityTarget,
        Range = _ => (min, max, def), When = f => Mode(f) == mode, Locked = Locked,
    };

    static readonly ParamDef[] Ps =
    {
        En("inspectionMode", "검사 묶음", "Thickness", "CD", "Contamination", "Electrical", "Profile"),
        new() { Id = "targetId", Label = "검사 대상", Kind = Kind.Enum, Options = f => Targets(f.C.State, Mode(f)), OptionErrorIsPrecondition = true },
        En("qualityProfile", "품질 프로파일", "EducationalDefault", "Custom"),
        QL("targetThickness", "목표 두께", "nm", 1, 10000, 100, "Thickness"),
        QL("thicknessTolerance", "두께 허용오차", "%", 0, 50, 10, "Thickness"),
        QL("maxNonuniformity", "최대 불균일도", "%", 0, 100, 10, "Thickness"),
        QL("targetCd", "목표 개구부 CD", "nm", 10, 10000, 500, "CD"),
        QL("cdTolerance", "CD 절대 허용오차", "nm", 0, 1000, 50, "CD"),
        QL("maxContamination", "최대 오염 지수", "점", 0, 100, 10, "Contamination"),
        QL("maxResistance", "최대 저항", "Ω", 0.001, 1e6, 100, "Electrical"),
        new() { Id = "targetDepth", Label = "목표 평균 주입 깊이", Unit = "nm", Kind = Kind.Double, Role = Role.QualityTarget, When = f => Mode(f) == "Profile", Locked = Locked,
            Range = f => (1, 5000, ImplantProcess.DefaultDepth(f.E("targetId").Replace("IMP-", ""))) },
        QL("depthTolerance", "깊이 허용오차", "%", 0, 100, 20, "Profile"),
        QL("minActivation", "최소 활성화율", "%", 0, 100, 80, "Profile"),
    };
    public override IReadOnlyList<ParamDef> Params => Ps;

    public override void Preconditions(InspectionParameters r, Ctx c, List<Err> e)
    {
        if (!Targets(c.State, r.InspectionMode).Contains(r.TargetId))
            Pre(e, "TARGET_MISSING", $"검사 대상 '{r.TargetId}'이(가) 현재 상태에 없습니다(삭제되었거나 다른 상태의 ID).", "현재 상태의 대상을 선택하세요.");
    }

    public override Output Calculate(InspectionParameters r, Ctx c, CancellationToken t)
    {
        var s = c.State;
        var o = new Output();
        string id = r.TargetId;
        switch (r.InspectionMode)
        {
            case "Thickness":
            {
                var per = s.Columns.Select(col => col.LayerThickness(id)).ToArray();
                double mean = per.Average();
                o.M("thickness", $"{id} 평균 두께(전체 열, 없는 열은 0)", mean, "nm");
                o.M("nonuniformity", "불균일도 100·(max−min)/(2·mean)", mean > 0 ? 100 * (per.Max() - per.Min()) / (2 * mean) : null, "%", mean > 0 ? null : "평균 0이라 정의할 수 없음");
                break;
            }
            case "CD":
            {
                var runs = new List<int>();
                int run = 0;
                foreach (var open in s.Resist!.Open)
                {
                    if (open) run++;
                    else if (run > 0) { runs.Add(run); run = 0; }
                }
                if (run > 0) runs.Add(run);
                o.M("openingCd", "개구부 CD(열 단위 측정 평균)", runs.Count > 0 ? runs.Average() * s.ColWidthNm : null, "nm", runs.Count > 0 ? null : "열린 개구부 없음");
                o.M("openings", "개구부 수", runs.Count, "개");
                break;
            }
            case "Contamination":
                o.M("contamination", "오염 지수", s.Contamination, "점");
                break;
            case "Electrical":
            {
                var w = s.Wires.First(x => x.WireId == id);
                o.M("resistance", $"배선 {id} 저항(현재 형상)", w.ResistanceOhm, "Ω");
                break;
            }
            default:
            {
                string sp = id.Replace("IMP-", "");
                var m = s.Doping!;
                var sum = new double[m.N];
                var act = new double[m.N];
                foreach (var g in m.Groups)
                {
                    var p = g.Species.FirstOrDefault(x => x.Species == sp);
                    if (p == null) continue;
                    for (int i = 0; i < m.N; i++) { sum[i] += g.Count * p.Total[i]; act[i] += g.Count * p.Active[i]; }
                }
                double tot = DopingMap.Trap(sum, m.Dz);
                o.M("meanDepth", $"{sp} 평균 깊이", DopingMap.MeanDepth(sum, m.Dz), "nm");
                o.M("activation", $"{sp} 활성 분율", tot > 0 ? DopingMap.Trap(act, m.Dz) / tot * 100 : null, "%", tot > 0 ? null : "농도 0");
                break;
            }
        }
        o.Warnings.Add("검사는 물리 상태를 바꾸지 않고 검사 기록만 추가합니다(모델 상태의 이상적 읽기).");
        return o;
    }

    public override List<Criterion> Criteria(InspectionParameters r, Ctx c, Output o) => r.InspectionMode switch
    {
        "Thickness" => new()
        {
            new("thickness", CK.RelTarget, Required(r.TargetThickness), Required(r.ThicknessTolerance)),
            new("nonuniformity", CK.Max, Required(r.MaxNonuniformity)),
        },
        "CD" => new() { new("openingCd", CK.AbsTarget, Required(r.TargetCd), Required(r.CdTolerance)) },
        "Contamination" => new() { new("contamination", CK.Max, Required(r.MaxContamination)) },
        "Electrical" => new() { new("resistance", CK.Max, Required(r.MaxResistance)) },
        _ => new()
        {
            new("meanDepth", CK.RelTarget, Required(r.TargetDepth), Required(r.DepthTolerance)),
            new("activation", CK.Min, Required(r.MinActivation)),
        },
    };
}

public sealed class PackagingProcess : ProcessBase
{
    public override string Id => "packaging";
    public override string Group => "⑧ 패키징";
    public override string Name => "패키징";
    public override bool Enabled => false;
    public override string DisabledReason => "원본 명세에 입력·모델·품질 기준이 없어 구현하지 않았습니다(범위 밖, 임의 결과 금지).";
    public override IReadOnlyList<ParamDef> Params => Array.Empty<ParamDef>();
    public override Output Calculate(Recipe r, Ctx c, CancellationToken t) => throw new NotSupportedException(DisabledReason);
    public override List<Criterion> Criteria(Recipe r, Ctx c, Output o) => new();
}
