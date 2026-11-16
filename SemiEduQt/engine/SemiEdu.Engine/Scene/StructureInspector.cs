namespace SemiEdu.Engine;

public sealed class InspectResult
{
    public string Title = "";
    public string Subtitle = "";
    public string Color = "#888888";
    public string Kind = "";
    public readonly List<(string Group, string Label, string Value)> Rows = new();
    public readonly List<string> Notes = new();

    public void R(string group, string label, string value) => Rows.Add((group, label, value));
}

/// <summary>
/// Answers "what is this?" for a clicked 3D box: material, thickness, position and educational properties.
/// Everything is read from the current (or preview) state — nothing is cached between clicks.
/// </summary>
public static class StructureInspector
{
    static string Hex((float R, float G, float B) c) => $"#{(int)(c.R * 255):x2}{(int)(c.G * 255):x2}{(int)(c.B * 255):x2}";
    static string Nm(double v) => $"{Fmt.S(v, 5)} nm";
    static string Um(double v) => $"{Fmt.S(v, 5)} µm";
    static string Cm3(double v) => $"{Fmt.S(v, 3)} cm⁻³";

    public static string OriginKo(string origin) => origin switch
    {
        "init" => "웨이퍼 초기화(균일 산화막)",
        "oxidation" => "열산화(Si 소모로 성장)",
        "photo" => "포토(PR 도포·노광·현상)",
        "deposition-CVD" => "CVD 증착",
        "deposition-PVD" => "PVD 증착",
        "deposition-ALD" => "ALD 증착",
        "gate" => "금속 배선(게이트 전극)",
        "wire" => "금속 배선(이상적 패턴)",
        "contact-Source" => "컨택 형성(소스 플러그)",
        "contact-Drain" => "컨택 형성(드레인 플러그)",
        _ => origin,
    };

    static string MaterialNote(string m) => m switch
    {
        Mat.SiO2 => "절연체 · 상대 유전율 3.9(MOS 모델에 쓰는 교육용 값) · 게이트 산화막·층간 절연·식각 대상",
        Mat.Si3N4 => "절연체 · 식각 정지·보호막 용도(교육용 설명)",
        Mat.PR => "감광막(고분자) · 절연체 · 식각·주입 마스크 역할, 고온·금속 공정 전 제거 필요",
        Mat.Al => "금속(도체) · 교육용 비저항 2.8e-8 Ω·m · 일함수 4.10 eV",
        Mat.Cu => "금속(도체) · 교육용 비저항 1.7e-8 Ω·m · 일함수 4.65 eV",
        Mat.W => "금속(도체) · 교육용 비저항 5.6e-8 Ω·m · 일함수 4.55 eV · 내열 금속(게이트 열처리 허용, X3)",
        Mat.TiN => "금속(도체) · 일함수 4.60 eV · 내열 금속",
        _ => "",
    };

    public static InspectResult Inspect(Session s, SceneOptions o, string pick, double hitX, double hitZ)
    {
        if (o.View == "mask") return InspectMask(s, o, pick);
        var st = SceneBuilder.StateFor(s, o);
        var parts = pick.Split(':');
        int ColAt(int a, int b)
        {
            int c = (int)Math.Floor(hitX / SceneBuilder.WorldX * st.N);
            return Math.Clamp(c, a, Math.Max(a, b - 1));
        }
        return parts[0] switch
        {
            "f" => InspectFilm(st, ColAt(int.Parse(parts[1]), int.Parse(parts[2])), int.Parse(parts[3])),
            "s" => InspectSilicon(st, ColAt(int.Parse(parts[1]), int.Parse(parts[2])), hitZ),
            "sub" => InspectSubstrate(st),
            "t" => InspectTerminal(st, SceneBuilder.ElectricalFor(s, o, st), parts[1]),
            "inv" => InspectInversion(SceneBuilder.ElectricalFor(s, o, st)),
            _ => new InspectResult { Title = "선택 정보 없음", Kind = "none" },
        };
    }

    static (int A, int B) LayerRun(WaferState st, int col, string layerId)
    {
        int a = col, b = col + 1;
        while (a > 0 && st.Columns[a - 1].Segs.Any(g => g.LayerId == layerId)) a--;
        while (b < st.N && st.Columns[b].Segs.Any(g => g.LayerId == layerId)) b++;
        return (a, b);
    }

    static InspectResult InspectFilm(WaferState st, int col, int k)
    {
        var c = st.Columns[col];
        if (k >= c.Segs.Count) return new InspectResult { Title = "선택 정보 없음", Kind = "none" };
        var seg = c.Segs[k];
        var info = st.Layers.FirstOrDefault(l => l.LayerId == seg.LayerId);
        double bottom = c.SiTop + c.Segs.Take(k).Sum(g => g.Thickness);
        var (a, b) = LayerRun(st, col, seg.LayerId);
        double cw = st.DomainWidthUm / st.N;
        double avg = st.Columns.Average(x => x.LayerThickness(seg.LayerId));
        int covered = st.Columns.Count(x => x.Segs.Any(g => g.LayerId == seg.LayerId));
        bool metal = Mat.IsMetal(seg.Material);

        var r = new InspectResult
        {
            Kind = "film",
            Title = $"{Mat.Ko(seg.Material)} ({seg.Material})",
            Subtitle = $"{seg.LayerId} · {OriginKo(info?.Origin ?? "")}",
            Color = Hex(Mat.Rgb(seg.Material)),
        };
        r.R("물질", "재료", $"{Mat.Ko(seg.Material)} ({seg.Material})");
        r.R("물질", "LayerId", seg.LayerId);
        r.R("물질", "만든 공정", OriginKo(info?.Origin ?? "알 수 없음"));
        r.R("치수", "두께(이 위치)", Nm(seg.Thickness));
        r.R("치수", "층 평균 두께(전체 열)", Nm(avg));
        r.R("치수", "아래면 / 윗면 높이", $"{Fmt.S(bottom, 5)} / {Fmt.S(bottom + seg.Thickness, 5)} nm (원래 Si 표면 = 0)");
        r.R("치수", "이 구조의 가로 범위", $"{Fmt.S(a * cw, 5)}–{Fmt.S(b * cw, 5)} µm (폭 {Um((b - a) * cw)})");
        r.R("치수", "층이 덮은 열", $"{covered}/{st.N} 열");
        r.R("특성", "전기적 분류", metal ? "도체" : "절연체");
        r.R("특성", "물질 특성", MaterialNote(seg.Material));
        r.R("특성", "적층 위치", $"아래에서 {k + 1}번째 막 / 이 열의 막 {c.Segs.Count}개");

        if (seg.Material == Mat.PR && st.Resist != null)
        {
            bool open = col < st.Resist.Open.Length && st.Resist.Open[col];
            r.R("패턴", "PR 영역", open ? "개구부(잔막 ≤ 1 nm, 식각에 열림)" : "보호부(식각·주입을 막음)");
            r.R("패턴", "패턴 ID · 마스크", $"{st.Resist.PatternId} · {st.Resist.MaskId}");
            r.R("패턴", "계산 개구부 CD", Nm(st.Resist.OpeningCdNm));
        }
        if (metal)
        {
            var wire = st.Wires.FirstOrDefault(w => w.LayerId == seg.LayerId);
            var ct = st.Contacts.FirstOrDefault(x => x.LayerId == seg.LayerId);
            if (wire != null)
            {
                r.R("전기", "배선", $"{wire.WireId} · 역할 {(wire.Role == "Gate" ? "게이트 전극" : "배선")}{(wire.MaskId != "None" ? " · 마스크 " + wire.MaskId : "")}");
                r.R("전기", "형상", $"폭 {Nm(wire.WidthNm)} × 두께 {Nm(wire.ThicknessNm)} × 길이 {Um(wire.LengthUm)}(화면 안쪽 방향)");
                r.R("전기", "저항 R = ρL/(W·t)", $"{Fmt.S(wire.ResistanceOhm, 5)} Ω");
                if (wire.Role == "Gate")
                {
                    double tox = DeviceStructure.GateTox(st, wire);
                    r.R("전기", "게이트 아래 절연막 tox", double.IsFinite(tox) ? Nm(tox) : "N/A");
                }
            }
            if (ct != null) r.R("전기", "컨택", $"{ct.ContactId} · {(ct.Role == "Source" ? "소스" : "드레인")} 플러그 · 하부 Si와 이상적 오믹 접촉");
            var nl = new Netlist(st);
            var net = nl.NetOfFilm(col, k);
            if (net != null)
                r.R("전기", "소속 도체 네트", $"{net.Id} ({net.Kind}) · 연결 객체 {string.Join(", ", net.Objects)}{(net.HasNPlus || net.HasPPlus ? " · 도체성 Si와 연결" : "")}");
        }
        r.Notes.Add("검증되지 않은 교육용 예시 모델의 값입니다. 실제 물성·공정값이 아닙니다.");
        return r;
    }

    static InspectResult InspectSilicon(WaferState st, int col, double hitZ)
    {
        var fr = SceneBuilder.Frame(st);
        double siTop = st.Columns[col].SiTop;
        double depth = Math.Clamp(siTop - fr.Nm(hitZ), 0, siTop - fr.Bottom);
        double bg = st.BackgroundNet, net = bg, total = 0;
        var species = new List<string>();
        ColumnGroup? grp = null;
        if (st.Doping != null)
        {
            grp = st.Doping.GroupOf(col);
            net += st.Doping.NetActiveAt(grp, depth);
            foreach (var sp in grp.Species)
            {
                double t = DopingMap.Interp(sp.Total, st.Doping.Dz, depth), a = DopingMap.Interp(sp.Active, st.Doping.Dz, depth);
                total += t;
                species.Add($"{sp.Species}({(DopingMap.IsDonor(sp.Species) ? "donor" : "acceptor")}) 전체 {Cm3(t)} · 활성 {Cm3(a)}{(t > 0 ? $" ({Fmt.S(a / t * 100, 3)}%)" : "")}");
            }
        }
        var cls = Classes.OfNet(net);
        var color = Classes.Color(cls);
        double cw = st.DomainWidthUm / st.N;

        var r = new InspectResult
        {
            Kind = "silicon",
            Title = $"실리콘 (Si) — {Classes.Ko(cls)}",
            Subtitle = $"x = {Fmt.S((col + 0.5) * cw, 5)} µm · 표면 아래 약 {Fmt.S(depth, 4)} nm",
            Color = Hex((color.R, color.G, color.B)),
        };
        r.R("물질", "재료", "실리콘 (Si) 기판");
        r.R("물질", "기판", $"{st.DopingType} {Cm3(st.BackgroundDoping)} · {st.Orientation}(효과 미반영) · 두께 {Um(st.SubstrateThicknessUm)}");
        r.R("위치", "선택 위치", $"x = {Fmt.S((col + 0.5) * cw, 5)} µm, Si 표면 아래 약 {Fmt.S(depth, 4)} nm(클릭 위치 기준)");
        r.R("위치", "Si 표면 높이", $"{Fmt.S(siTop, 5)} nm (원래 Si 표면 = 0{(siTop < 0 ? ", 산화·식각으로 내려감" : "")})");
        r.R("도핑", "순활성 농도(donor − acceptor)", $"{(net >= 0 ? "n형" : "p형")} {Cm3(Math.Abs(net))}");
        if (species.Count > 0) foreach (var sp in species) r.R("도핑", "주입 성분", sp);
        else r.R("도핑", "주입 성분", "없음(배경 도핑만)");
        if (grp != null && st.Doping != null)
        {
            double xj = JunctionDepth(st, grp, bg);
            r.R("도핑", "접합 깊이 Xj(이 열)", double.IsNaN(xj) ? "N/A(부호 변화 없음)" : Nm(xj));
        }
        r.R("특성", "전기적 분류", Classes.Ko(cls));
        string reason = Classes.IsConductiveSi(cls) ? "|순활성| ≥ 1e19 cm⁻³ → 도체성(고농도) 영역"
            : cls == Cls.Intrinsic ? "|순활성| < 1e14 cm⁻³ → 진성에 가까운 반도체"
            : "1e14 ≤ |순활성| < 1e19 cm⁻³ → 반도체";
        if (!Classes.IsConductiveSi(cls) && total >= Classes.ConductiveThreshold) reason += " · 열처리 전, 활성 부족(전체 농도는 1e19 이상)";
        r.R("특성", "분류 근거", reason);
        r.Notes.Add("측방향 확산은 모델링하지 않습니다. 교육용 임계값·계수이며 실제 소자 특성이 아닙니다.");
        return r;
    }

    static double JunctionDepth(WaferState st, ColumnGroup g, double bg)
    {
        var m = st.Doping!;
        double prev = bg + m.NetActiveAt(g, 0);
        for (int i = 1; i < m.N; i++)
        {
            double z = i * m.Dz, v = bg + m.NetActiveAt(g, z);
            if (prev != 0 && Math.Sign(prev) != Math.Sign(v)) return z - m.Dz + m.Dz * prev / (prev - v);
            prev = v;
        }
        return double.NaN;
    }

    static InspectResult InspectSubstrate(WaferState st)
    {
        var r = new InspectResult
        {
            Kind = "substrate",
            Title = "실리콘 기판 (생략 표시)",
            Subtitle = $"기판 {Fmt.S(st.SubstrateThicknessUm, 4)} µm 중 3D에는 상부 일부만 그립니다",
            Color = Hex(Mat.Rgb(Mat.Si)),
        };
        r.R("기판", "재료 · 직경", $"Si · {st.DiameterMm} mm");
        r.R("기판", "두께", Um(st.SubstrateThicknessUm));
        r.R("기판", "결정 방향", $"{st.Orientation} (현재 모델에서는 효과 미반영)");
        r.R("기판", "배경 도핑", $"{st.DopingType} {Cm3(st.BackgroundDoping)} (전부 활성으로 간주)");
        r.R("기판", "국소 단면 폭", $"{Um(st.DomainWidthUm)} · {st.N}열(열 폭 {Nm(st.ColWidthNm)})");
        r.R("기판", "오염 지수", $"{Fmt.S(st.Contamination, 4)} 점(교육용 무차원 점수)");
        r.Notes.Add("실제 기판 전체 두께를 막과 같은 축에 그리지 않고, 아래 띠로 생략을 표시합니다.");
        return r;
    }

    static InspectResult InspectTerminal(WaferState st, ElectricalResult? el, string which)
    {
        string name = which switch { "G" => "게이트(Gate)", "S" => "소스(Source)", _ => "드레인(Drain)" };
        var r = new InspectResult
        {
            Kind = "terminal",
            Title = $"단자 표식 — {name}",
            Color = which switch { "G" => "#f22626", "S" => "#26cc40", _ => "#3366f2" },
        };
        if (el == null) { r.Subtitle = "전기 검사 결과가 없습니다."; return r; }
        r.Subtitle = $"{el.DeviceType} · {(el.Normal ? "정상" : "비정상")}";
        r.R("검사", "판정", el.Verdict);
        r.R("검사", "인가 조건", el.Applied);
        foreach (var (n, v) in el.Extracted) r.R("추출값", n, v);
        if (el.FailureCodes.Count > 0) r.R("검사", "실패 코드", string.Join(", ", el.FailureCodes));
        r.Notes.Add("장채널 square-law 교육용 모델입니다. 짧은 채널 효과·접촉 저항 등은 반영하지 않습니다.");
        return r;
    }

    static InspectResult InspectInversion(ElectricalResult? el)
    {
        var r = new InspectResult { Kind = "inversion", Title = "반전층 (채널)", Color = "#ffeb33" };
        if (el == null) return r;
        r.Subtitle = $"Vov = {Fmt.S(el.Vov, 4)} V > 0 이면 게이트 아래 표면에 채널이 생깁니다";
        r.R("채널", "과구동 전압 Vov = Vgs − Vth", $"{Fmt.S(el.Vov, 4)} V");
        r.R("채널", "표시", "두께 2 nm 고정 · 투명도 min(0.8, Vov/Vdd) — 실제 반전층 두께가 아님");
        r.R("채널", "인가 조건", el.Applied);
        return r;
    }

    static InspectResult InspectMask(Session s, SceneOptions o, string pick)
    {
        var m = SceneBuilder.MaskFor(s, o);
        var r = new InspectResult { Kind = "mask", Color = "#d9e6f7" };
        if (m == null) { r.Title = "마스크 없음"; return r; }
        string part = pick.Split(':').ElementAtOrDefault(1) ?? "";
        r.Title = part switch
        {
            "absorber" => m.MaskType == "EUVReflective" ? "EUV 흡수체" : "Cr 차광막",
            "residual" => "투명부에 남은 흡수체(잔막)",
            "pellicle" => "펠리클(프레임·막)",
            _ => "석영 기판(마스크 블랭크)",
        };
        r.Subtitle = $"{m.MaskId} · {m.MaskType} · {m.MaskLayer} · {(m.Passed ? "합격" : "불합격")}";
        r.R("마스크", "마스크 CD(투명부, 웨이퍼 환산)", Nm(m.MaskCd));
        r.R("마스크", "설계 투명부 / 피치", $"{Nm(m.DesignClear)} / {Nm(m.Pitch)}");
        r.R("마스크", "시작 위치 · 반복 수", $"{Um(m.OffsetUm)} · {m.RepeatCount}회");
        r.R("마스크", "흡수체 잔막", Nm(m.AbsorberResidual));
        r.R("마스크", "CD 바이어스 · 가장자리 흐림", $"{Nm(m.Bias)} · {Nm(m.EdgeBlur)}");
        r.R("마스크", "펠리클", m.Pellicle ? "부착" : "없음");
        r.Notes.Add("마스크 축척 4×, 값은 웨이퍼 환산입니다. 검증되지 않은 교육용 예시입니다.");
        return r;
    }
}
