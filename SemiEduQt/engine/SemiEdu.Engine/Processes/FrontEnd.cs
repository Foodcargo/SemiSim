namespace SemiEdu.Engine;

public sealed class InitProcess : ProcessBase<InitParameters>
{
    protected override InitParameters Bind(Recipe recipe) => recipe;

    public const int Columns = 512;
    public override string Id => "init";
    public override string Group => "① 웨이퍼 제조";
    public override string Name => "웨이퍼 초기화";
    public override bool AllowAfterFailure => true;
    public override string[] Unsupported => new[] { "결정 방향에 따른 효과(메타데이터로만 저장)", "전체 웨이퍼 맵(국소 단면만 표현)" };

    static readonly ParamDef[] Ps =
    {
        En("substrate", "기판 재료", Mat.Si),
        new() { Id = "diameter", Label = "직경 (mm)", Kind = Kind.Enum, Options = _ => new[] { "100", "150", "200", "300" }, EnumDefault = _ => "200" },
        new() { Id = "orientation", Label = "결정 방향", Kind = Kind.Enum, Role = Role.Metadata, Options = _ => new[] { "(100)", "(110)", "(111)" }, Note = "현재 모델에서는 효과 미반영" },
        En("dopingType", "도핑 유형", "P-type", "N-type"),
        En("startSurface", "시작 표면", "BareSi", "UniformSiO2"),
        Num("substrateThickness", "기판 두께", "µm", 300, 1000, 725),
        Num("backgroundDoping", "초기 도핑 농도", "cm⁻³", 1e13, 1e19, 1e15),
        Num("initialOxide", "초기 산화막", "nm", 1, 1000, 100, f => f.E("startSurface") == "UniformSiO2"),
        Num("domainWidth", "국소 단면 폭", "µm", 20, 100, 20),
        Num("contamination", "초기 오염 지수", "점", 0, 100, 50),
    };
    public override IReadOnlyList<ParamDef> Params => Ps;

    public static WaferState CreateRoot()
    {
        var s = new WaferState { CreatedBy = "init" };
        s.Columns = Enumerable.Range(0, Columns).Select(_ => new Column()).ToArray();
        return s;
    }

    public override Output Calculate(InitParameters r, Ctx c, CancellationToken t)
    {
        var s = new WaferState
        {
            ParentStateId = c.State.StateId,
            DiameterMm = int.Parse(r.Diameter),
            SubstrateThicknessUm = Required(r.SubstrateThickness),
            Orientation = r.Orientation,
            DopingType = r.DopingType,
            BackgroundDoping = Required(r.BackgroundDoping),
            DomainWidthUm = Required(r.DomainWidth),
            Contamination = Required(r.Contamination),
        };
        s.Columns = Enumerable.Range(0, Columns).Select(_ => new Column()).ToArray();
        double ox = 0;
        if (r.StartSurface == "UniformSiO2")
        {
            ox = Required(r.InitialOxide);
            var id = s.NewLayer(Mat.SiO2, "init");
            foreach (var col in s.Columns) col.Segs.Add(new Seg { Material = Mat.SiO2, Thickness = ox, LayerId = id });
        }
        var o = new Output { Candidate = s };
        o.M("columns", "단면 열 수", Columns, "열");
        o.M("columnWidth", "열 폭", s.ColWidthNm, "nm");
        o.M("initialOxide", "초기 산화막", ox, "nm");
        return o;
    }

    public override List<Criterion> Criteria(InitParameters r, Ctx c, Output o) => new();
}

public sealed class OxidationProcess : ProcessBase<OxidationParameters>
{
    protected override OxidationParameters Bind(Recipe recipe) => recipe;

    public override string Id => "oxidation";
    public override string Group => "② 산화";
    public override string Name => "열산화";
    public override string EquipmentId => "OxidationEdu";
    public override string[] Unsupported => new[]
    {
        "결정 방향·도핑 농도에 따른 성장률 차이", "얇은 산화막 초기 급성장 영역", "다차원 형상·패턴 산화", "산화 유도 결함",
    };

    static readonly ParamDef[] Ps =
    {
        En("method", "방식", "Dry", "Wet"),
        Num("temperature", "온도", "°C", 700, 1200, 1000),
        Num("duration", "시간", "min", 1, 240, 60),
        Num("pressure", "압력", "Torr", 1, 760, 760),
        Q("targetThickness", "목표 두께", "nm", 1, 10000, 70),
        Q("thicknessTolerance", "두께 허용오차", "%", 0, 50, 10),
    };
    public override IReadOnlyList<ParamDef> Params => Ps;

    public static (double XOld, double XNew, double Growth) Compute(OxidationParameters r, WaferState s)
    {
        double xOld = s.Columns[0].Total(Mat.SiO2);
        double a = 100, b0 = r.Method == "Wet" ? 800 : 200;
        double b = b0 * Math.Exp((Required(r.Temperature) - 1000) / 150) * (Required(r.Pressure) / 760);
        double t = Required(r.Duration);
        double xNew = (-a + Math.Sqrt((a + 2 * xOld) * (a + 2 * xOld) + 4 * b * t)) / 2;
        return (xOld, xNew, xNew - xOld);
    }

    public override void Preconditions(OxidationParameters r, Ctx c, List<Err> e)
    {
        var s = c.State;
        if (s.HasPR) Pre(e, "PR_PRESENT", "감광막(PR)이 남아 있어 산화할 수 없습니다.", "PRStrip으로 PR을 먼저 제거하세요.");
        if (s.HasMetalSeg || s.Wires.Count > 0 || s.Contacts.Count > 0) Pre(e, "METAL_PRESENT", "금속이 있어 고온 산화를 할 수 없습니다.", "금속 공정 전에 산화하세요.");
        if (s.HasDoping) Pre(e, "DOPING_PRESENT", "도핑 후 산화는 프로파일 좌표 이동을 지원하지 않아 차단합니다.", "산화를 이온주입보다 먼저 수행하세요.");
        if (!s.IsUniform() || s.Columns.Any(col => col.Segs.Any(g => g.Material != Mat.SiO2)))
            Pre(e, "NOT_PLANAR_SI_OR_SIO2", "v1 산화는 평탄한 Si 또는 균일 SiO2/Si만 지원합니다.", "패턴·다른 막이 없는 상태에서 산화하세요.");
    }

    public override void Interlock(OxidationParameters r, Ctx c, List<Err> e)
    {
        double T = Required(r.Temperature), P = Required(r.Pressure);
        if (T < 800 || T > 1100) Ilk(e, "OXIDATION_TEMPERATURE_LIMIT", "temperature", T, "800–1100 °C", "OxidationEdu 장비의 온도 운전 한계를 벗어났습니다(입력 범위 안이지만 장비 인터록).");
        if (P < 100 || P > 760) Ilk(e, "OXIDATION_PRESSURE_LIMIT", "pressure", P, "100–760 Torr", "OxidationEdu 장비의 압력 운전 한계를 벗어났습니다.");
    }

    public override void Domain(OxidationParameters r, Ctx c, List<Err> e)
    {
        var (_, xNew, growth) = Compute(r, c.State);
        if (xNew > 2000) Dom(e, "OXIDE_TOO_THICK", $"최종 산화막 {F(xNew)} nm가 모델 한계 2000 nm를 넘습니다.", "시간이나 온도를 낮추세요.");
        if (0.44 * growth >= c.State.SubstrateThicknessUm * 1000) Dom(e, "SUBSTRATE_CONSUMED", "Si 소모량이 기판 두께 이상입니다.", "조건을 낮추세요.");
    }

    public override Output Calculate(OxidationParameters r, Ctx c, CancellationToken t)
    {
        var s = c.State.CloneAsChild();
        var (_, xNew, growth) = Compute(r, c.State);
        var id = s.NewLayer(Mat.SiO2, "oxidation");
        foreach (var col in s.Columns)
        {
            col.Segs.Insert(0, new Seg { Material = Mat.SiO2, Thickness = growth, LayerId = id });
            col.SiTop -= 0.44 * growth;
        }
        var o = new Output { Candidate = s };
        o.M("oxideThickness", "최종 산화막 두께", xNew, "nm");
        o.M("growth", "이번 성장량", growth, "nm");
        o.M("siConsumed", "Si 소모량", 0.44 * growth, "nm");
        return o;
    }

    public override List<Criterion> Criteria(OxidationParameters r, Ctx c, Output o) =>
        new() { new("oxideThickness", CK.RelTarget, Required(r.TargetThickness), Required(r.ThicknessTolerance)) };
}

public sealed class MaskMakingProcess : ProcessBase<MaskMakingParameters>
{
    protected override MaskMakingParameters Bind(Recipe recipe) => recipe;

    public override string Id => "mask";
    public override string Group => "③ 포토";
    public override string Name => "마스크 제작";
    public override bool ChangesState => false;
    public override bool AllowAfterFailure => true;
    public override string EquipmentId => "MaskWriterEdu";
    public override string[] Unsupported => new[] { "근접 효과 보정(OPC)", "마스크 결함·이물", "위상 오차", "전자빔 근접 효과·대전" };

    static bool Euv(FormCtx f) => f.E("maskType") == "EUVReflective";

    static readonly ParamDef[] Ps =
    {
        En("maskType", "마스크 종류", "Binary", "AttPSM", "EUVReflective"),
        En("maskLayer", "마스크 층", "Generic", "Gate", "Contact", "Metal1"),
        En("pattern", "패턴", "LineSpace"),
        new() { Id = "designTone", Label = "톤 기준", Kind = Kind.Enum, Role = Role.Metadata, Options = _ => new[] { "ClearIsLine", "ClearIsSpace" }, Note = "설명용 메타데이터, 계산에 미사용" },
        En("pellicle", "펠리클", "Yes", "No"),
        Num("designClearWidth", "설계 투명부 폭(웨이퍼 기준)", "nm", 10, 10000, 500),
        Num("designPitch", "설계 피치(웨이퍼 기준)", "nm", 20, 20000, 1000, note: "반복 수 1이면 피치 제약 미적용"),
        Num("offset", "패턴 시작 위치", "µm", 0, 100, 0),
        Num("repeatCount", "패턴 반복 수", "회", 1, 1000, 1000, integer: true, note: "1이면 단일 개구"),
        Num("writeDose", "묘화 도즈", "µC/cm²", 50, 500, 200),
        Num("beamSize", "빔 크기(웨이퍼 환산)", "nm", 2, 50, 10),
        Num("developTime", "현상 시간", "s", 10, 180, 60),
        Num("crEtchTime", "Cr 식각 시간", "s", 10, 600, 120, f => !Euv(f)),
        Num("absorberEtchTime", "흡수체 식각 시간", "s", 10, 600, 120, Euv),
        Q("maskCdTolerance", "마스크 CD 허용오차", "nm", 0, 100, 10),
        Q("maxAbsorberResidual", "흡수체 잔막 한계", "nm", 0, 100, 0.5),
    };
    public override IReadOnlyList<ParamDef> Params => Ps;

    public override void Cross(MaskMakingParameters r, Ctx c, List<Err> e)
    {
        double cl = Required(r.DesignClearWidth), p = Required(r.DesignPitch), off = Required(r.Offset), w = c.State.DomainWidthUm;
        if (Required(r.RepeatCount) > 1)
        {
            if (cl >= p) InputErr(e, "CLEAR_GE_PITCH", "designClearWidth", F(cl), $"< 피치 {F(p)} nm", "설계 투명부 폭은 피치보다 작아야 합니다.");
            if (off + p / 1000 > w + 1e-9) InputErr(e, "PATTERN_OUTSIDE_DOMAIN", "offset", F(off), $"offset + pitch ≤ {F(w)} µm", "패턴 시작 위치 + 피치가 단면 폭을 넘습니다.");
        }
        else if (off + cl / 1000 > w + 1e-9)
            InputErr(e, "PATTERN_OUTSIDE_DOMAIN", "offset", F(off), $"offset + 투명부 ≤ {F(w)} µm", "단일 개구가 단면 폭을 넘습니다.");
    }

    public override void Interlock(MaskMakingParameters r, Ctx c, List<Err> e)
    {
        if (Required(r.WriteDose) > 400) Ilk(e, "MASK_WRITER_DOSE_LIMIT", "writeDose", Required(r.WriteDose), "≤ 400 µC/cm²", "MaskWriterEdu 묘화 도즈 한계를 넘었습니다.");
        if (Required(r.BeamSize) < 4) Ilk(e, "MASK_WRITER_BEAM_LIMIT", "beamSize", Required(r.BeamSize), "≥ 4 nm", "MaskWriterEdu 최소 빔 크기보다 작습니다.");
    }

    public override void Domain(MaskMakingParameters r, Ctx c, List<Err> e)
    {
        double cl = Required(r.DesignClearWidth), p = Required(r.DesignPitch), beam = Required(r.BeamSize);
        double feature = Required(r.RepeatCount) == 1 ? cl : Math.Min(cl, p - cl);
        if (feature < 2 * beam)
            Dom(e, "MASK_WRITE_RESOLUTION", $"최소 형상 {F(feature)} nm가 빔 크기의 2배({F(2 * beam)} nm)보다 작아 묘화 해상도가 부족합니다.", "빔 크기를 줄이거나 형상을 키우세요.");
    }

    public override Output Calculate(MaskMakingParameters r, Ctx c, CancellationToken t)
    {
        bool euv = r.MaskType == "EUVReflective";
        double etch = euv ? Required(r.AbsorberEtchTime) : Required(r.CrEtchTime);
        double bias = 10 * (Required(r.WriteDose) / 200 - 1) + 5 * (Required(r.DevelopTime) / 60 - 1) + 4 * (etch / 120 - 1);
        double cl = Required(r.DesignClearWidth);
        var m = new Mask
        {
            MaskId = $"{r.MaskLayer}-{c.Session.MaskCounter + 1}",
            MaskType = r.MaskType,
            MaskLayer = r.MaskLayer,
            Tone = r.DesignTone,
            Pellicle = r.Pellicle == "Yes",
            DesignClear = cl,
            Pitch = Required(r.DesignPitch),
            OffsetUm = Required(r.Offset),
            RepeatCount = (int)Required(r.RepeatCount),
            WriteDose = Required(r.WriteDose),
            BeamSize = Required(r.BeamSize),
            Bias = bias,
            MaskCd = cl + bias,
            EdgeBlur = 0.5 * Required(r.BeamSize),
            AbsorberResidual = Math.Max(0, 100 - 1 * etch),
        };
        var o = new Output { NewMask = m };
        o.M("maskCd", "마스크 CD(투명부)", m.MaskCd, "nm");
        o.M("absorberResidual", "흡수체 잔막(투명부)", m.AbsorberResidual, "nm");
        o.M("pellicleAttached", "펠리클 부착", m.Pellicle ? 1 : 0, "");
        o.M("bias", "CD 바이어스", bias, "nm");
        o.M("edgeBlur", "가장자리 흐림(기록용)", m.EdgeBlur, "nm");
        o.Warnings.Add($"마스크 ID {m.MaskId}: 마스크 축척 4×, 값은 웨이퍼 환산. 결과는 세션 MaskLibrary에 기록되며 웨이퍼 상태는 바뀌지 않음.");
        return o;
    }

    public override List<Criterion> Criteria(MaskMakingParameters r, Ctx c, Output o)
    {
        var list = new List<Criterion>
        {
            new("maskCd", CK.AbsTarget, Required(r.DesignClearWidth), Required(r.MaskCdTolerance)),
            new("absorberResidual", CK.Max, Required(r.MaxAbsorberResidual)),
        };
        if (r.MaskType != "EUVReflective") list.Add(new("pellicleAttached", CK.Present, 0));
        return list;
    }
}

public sealed class PhotoProcess : ProcessBase<PhotoParameters>
{
    protected override PhotoParameters Bind(Recipe recipe) => recipe;

    public override string Id => "photo";
    public override string Group => "③ 포토";
    public override string Name => "포토(도포·노광·현상)";
    public override string EquipmentId => "LithographyEdu";
    public override string[] Unsupported => new[] { "실제 광학 결상·광산 발생", "EUV 확률 결함", "3D 마스크 효과", "PR 측벽 각도", "정재파·반사" };

    static Mask? Sel(FormCtx f) => f.C.Session.Masks.FirstOrDefault(m => m.MaskId == f.E("maskId"));
    public static Mask? Sel(PhotoParameters r, Ctx c) => c.Session.Masks.FirstOrDefault(m => m.MaskId == r.MaskId);

    static readonly ParamDef[] Ps =
    {
        En("resistTone", "PR 종류", "Positive", "Negative"),
        En("exposureMode", "노광 방식", "DUV", "EUV"),
        EnF("maskId", "마스크", f => f.C.Session.Masks.Where(m => m.Passed || f.C.Eq.ContinueOnFailure).Select(m => m.MaskId).ToArray(),
            note: "마스크 제작 공정에서 합격한 마스크만 선택할 수 있습니다(불합격 마스크는 ‘계속 학습’ 시)."),
        Info("maskInfo", "마스크 값(읽기 전용)", f => Sel(f) is { } m
            ? $"CD {F(m.MaskCd)} nm · 피치 {F(m.Pitch)} nm · 시작 {F(m.OffsetUm)} µm · 반복 {m.RepeatCount} · {m.MaskType}{(m.Passed ? "" : " · 불합격 마스크")}"
            : null),
        NumF("resistThickness", "PR 두께", "µm", f => f.E("exposureMode") == "EUV" ? (0.02, 0.10, 0.05) : (0.02, 5.0, 1.0)),
        Num("dose", "노광량", "mJ/cm²", 1, 200, 50),
        Num("focus", "초점 오차", "µm", -1, 1, 0),
        Num("developTime", "현상 시간", "s", 10, 180, 60),
        QF("targetCd", "목표 개구부 CD", "nm", f => (10.0, 10000.0, Sel(f) is { } m
            ? Math.Clamp(f.E("resistTone") == "Negative" ? m.Pitch - m.DesignClear : m.DesignClear, 10, 10000) : 500.0)),
        Q("cdTolerance", "CD 절대 허용오차", "nm", 0, 1000, 50),
        Q("maxPrResidual", "개구부 PR 잔막 한계", "nm", 0, 1000, 1),
    };
    public override IReadOnlyList<ParamDef> Params => Ps;

    public override void Preconditions(PhotoParameters r, Ctx c, List<Err> e)
    {
        var s = c.State;
        var m = Sel(r, c);
        if (m == null) { Pre(e, "MASK_MISSING", "선택한 마스크가 MaskLibrary에 없습니다.", "마스크 제작을 먼저 실행하세요."); return; }
        if (!m.Passed && !c.Eq.ContinueOnFailure) Pre(e, "MASK_FAILED", "불합격 마스크는 ‘불량 상태로 계속 학습’을 켜야 쓸 수 있습니다.", "마스크를 다시 제작하세요.");
        bool duv = r.ExposureMode == "DUV";
        bool ok = duv ? m.MaskType is "Binary" or "AttPSM" : m.MaskType == "EUVReflective";
        if (!ok) Pre(e, "MASK_TYPE_MISMATCH", $"{r.ExposureMode} 노광에는 {m.MaskType} 마스크를 쓸 수 없습니다(DUV: Binary·AttPSM, EUV: EUVReflective).", "노광 방식에 맞는 마스크를 선택하세요.");
        if (s.HasPR) Pre(e, "PR_ALREADY_PRESENT", "기존 PR이 남아 있습니다.", "먼저 PRStrip을 수행하세요.");
        if (!s.IsUniform()) Pre(e, "NOT_PLANAR", "v1 포토는 평탄한 표면만 지원합니다.", "평탄한 상태에서 포토를 수행하세요.");
    }

    public override void Domain(PhotoParameters r, Ctx c, List<Err> e)
    {
        var m = Sel(r, c)!;
        bool euv = r.ExposureMode == "EUV";
        double res = euv ? 20 : (m.MaskType == "AttPSM" ? 80 : 100);
        double feature = m.RepeatCount == 1 ? m.MaskCd : Math.Min(m.MaskCd, m.Pitch - m.MaskCd);
        if (feature < res)
            Dom(e, "OPTICAL_RESOLUTION", $"최소 형상 {F(feature)} nm가 {(euv ? "EUV" : m.MaskType == "AttPSM" ? "DUV(AttPSM)" : "DUV")} 광학 해상도 {F(res)} nm보다 작습니다.", "형상을 키우거나 노광 방식을 바꾸세요.");
        double colW = c.State.ColWidthNm;
        if (feature < 4 * colW)
            Dom(e, "GEOMETRY_RESOLUTION", $"최소 형상 {F(feature)} nm가 열 폭 {F(colW)} nm의 4배보다 작아 단면 해상도로 표현할 수 없습니다.", "단면 폭(domainWidth)을 줄여 초기화하거나 형상을 키우세요.");
        if (r.ResistTone == "Negative" && m.RepeatCount == 1)
            Dom(e, "NEGATIVE_SINGLE_UNSUPPORTED", "반복 수 1 마스크의 Negative 노광은 v1 모델에서 지원하지 않습니다.", "Positive PR을 사용하세요.");
    }

    public override Output Calculate(PhotoParameters r, Ctx c, CancellationToken t)
    {
        var m = Sel(r, c)!;
        var s = c.State.CloneAsChild();
        bool pos = r.ResistTone == "Positive", euv = r.ExposureMode == "EUV";
        double e0 = euv ? 30 : 50, h0 = euv ? 50 : 1000, fs = euv ? 0.1 : 0.5;
        double dose = Required(r.Dose), dev = Required(r.DevelopTime), focus = Required(r.Focus);
        double clear = m.MaskCd, pitch = m.Pitch, prNm = Required(r.ResistThickness) * 1000;
        double target = pos ? clear : pitch - clear;
        double bias = (pos ? 1 : -1) * (20 * (dose / e0 - 1) + 10 * (dev / 60 - 1));
        double blur = 20 * Math.Pow(focus / fs, 2);
        double cd = target + bias - blur;
        double clearFrac = Math.Min(1, dose / e0 * (dev / 60) * (h0 / prNm));
        double resid = prNm * (1 - clearFrac);

        bool lostNone = cd <= 0, lostFull = m.RepeatCount > 1 && cd >= pitch;
        var inInt = new bool[s.N];
        double colW = s.ColWidthNm, domainNm = s.DomainWidthUm * 1000;
        if (lostFull) Array.Fill(inInt, true);
        else if (!lostNone)
        {
            for (int k = 0; k < m.RepeatCount; k++)
            {
                double start = m.OffsetUm * 1000 + k * pitch;
                if (start >= domainNm) break;
                double center = pos ? start + clear / 2 : start + clear + (pitch - clear) / 2;
                double lo = center - cd / 2, hi = center + cd / 2;
                for (int i = Math.Max(0, (int)Math.Floor(lo / colW) - 1); i < s.N && i <= (int)Math.Ceiling(hi / colW) + 1; i++)
                {
                    double x = (i + 0.5) * colW;
                    if (x >= lo - 1e-9 && x <= hi + 1e-9) inInt[i] = true;
                }
            }
        }
        var open = new bool[s.N];
        var prId = s.NewLayer(Mat.PR, "photo");
        for (int i = 0; i < s.N; i++)
        {
            open[i] = inInt[i] && resid <= 1;
            double th = inInt[i] ? resid : prNm;
            if (th > 1e-9) s.Columns[i].Segs.Add(new Seg { Material = Mat.PR, Thickness = th, LayerId = prId });
        }
        s.Resist = new ResistPattern { PatternId = s.NewId("PAT-"), Open = open, OpeningCdNm = Math.Max(0, cd), MaskId = m.MaskId };
        if (!s.HasPR) s.Resist = null;

        var o = new Output { Candidate = s };
        o.M("openingCd", "개구부 CD(계산값)", cd, "nm");
        o.M("openingResidual", "개구부 PR 잔막", resid, "nm");
        o.M("protectedPr", "보호부 PR 두께", inInt.Any(x => !x) ? prNm : 0, "nm");
        o.M("openColumns", "열린 열 수", open.Count(x => x), "열");
        o.M("bias", "CD 바이어스", bias, "nm");
        o.M("blur", "초점 흐림", blur, "nm");
        if (lostNone || lostFull)
            o.Warnings.Add($"패턴 소실: 계산 CD {F(cd)} nm → {(lostNone ? "개구부 없음" : "전체 개방")}으로 기록(원값은 진단에 보존).");
        if (!m.Passed) o.Warnings.Add("불합격 마스크 사용");
        return o;
    }

    public override List<Criterion> Criteria(PhotoParameters r, Ctx c, Output o) => new()
    {
        new("openingCd", CK.AbsTarget, Required(r.TargetCd), Required(r.CdTolerance)),
        new("openingResidual", CK.Max, Required(r.MaxPrResidual)),
        new("protectedPr", CK.Present, 0),
        new("openColumns", CK.Present, 0),
    };
}
