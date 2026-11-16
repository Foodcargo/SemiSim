using System.Text;

namespace SemiEdu.Engine;

public sealed class SceneOptions
{
    public bool Preview;
    public bool Classification;
    public bool Transparent = true;
    public string View = "wafer";
}

public sealed class SceneData
{
    /// <summary>11 floats per box: x0,x1,y0,y1,z0,z1,r,g,b,a,outline (0 none, 1 dark edge, 2 bright edge in box colour).</summary>
    public readonly List<float> Boxes = new();
    /// <summary>Pick key per box ("" = not selectable). Sent back to se_inspect when the user clicks the box.</summary>
    public readonly List<string> Picks = new();
    public string LegendHtml = "";
    public string ScaleText = "";
    public string Terminals = "";
    public bool IsPreview;
    public string ViewKind = "wafer";
    public int BoxCount => Boxes.Count / 11;

    public void Add(double x0, double x1, double y0, double y1, double z0, double z1, float r, float g, float b, float a, int outline, string pick = "")
    {
        Boxes.Add((float)x0); Boxes.Add((float)x1); Boxes.Add((float)y0); Boxes.Add((float)y1);
        Boxes.Add((float)z0); Boxes.Add((float)z1); Boxes.Add(r); Boxes.Add(g); Boxes.Add(b); Boxes.Add(a); Boxes.Add(outline);
        Picks.Add(pick);
    }
}

/// <summary>Display frame of the wafer view: nm ↔ world units (spec 6).</summary>
public readonly record struct SceneFrame(double Bottom, double Top, double SiDepth, double Sz)
{
    public double Z(double h) => (h - Bottom) * Sz;
    public double Nm(double worldZ) => Bottom + worldZ / Sz;
}

/// <summary>WaferState → boxes, legend and scale text (spec 6, 8, 9, 10.7). The renderer only draws boxes.</summary>
public static class SceneBuilder
{
    public const double WorldX = 12, WorldY = 12, WorldZ = 1;

    static string Hex(float r, float g, float b) => $"#{(int)(r * 255):x2}{(int)(g * 255):x2}{(int)(b * 255):x2}";
    static string Swatch(float r, float g, float b, string label) => $"<span style=\"color:{Hex(r, g, b)}\">■</span> {label}";

    static float MatAlpha(string m, bool transparent)
    {
        if (transparent) return m switch { Mat.Si => 0.30f, Mat.SiO2 => 0.42f, Mat.Si3N4 => 0.50f, Mat.PR => 0.55f, _ => 0.70f };
        return m switch { Mat.SiO2 => 0.78f, Mat.Si3N4 => 0.9f, Mat.PR => 0.85f, _ => 1f };
    }

    public static WaferState StateFor(Session s, SceneOptions o) => o.Preview && s.PreviewState != null ? s.PreviewState : s.Current;

    public static ElectricalResult? ElectricalFor(Session s, SceneOptions o, WaferState st)
    {
        var el = o.Preview ? s.PreviewElectrical : s.LastElectrical;
        if (o.Preview && s.PreviewElectrical == null) el = s.LastElectrical;
        return el != null && el.StateId == st.StateId ? el : null;
    }

    public static SceneFrame Frame(WaferState st)
    {
        double filmMax = st.Columns.Max(c => c.Film);
        double siTopMin = st.Columns.Min(c => c.SiTop);
        double top = st.Columns.Max(c => c.Surface);
        double dopDepth = st.Doping?.SignificantDepth() ?? 0;
        double siDepth = Math.Max(100, Math.Max(0.35 * filmMax, dopDepth));
        double bottom = siTopMin - siDepth;
        return new SceneFrame(bottom, top, siDepth, WorldZ / (top - bottom));
    }

    public static SceneData Build(Session s, SceneOptions o)
    {
        if (o.View == "mask") return BuildMask(s, o);
        var st = StateFor(s, o);
        bool isPreview = o.Preview && (s.PreviewState != null || s.PreviewElectrical != null);
        if (o.View == "device" && o.Classification) return BuildDevice(st, o, ElectricalFor(s, o, st), isPreview);
        return BuildWafer(st, o, ElectricalFor(s, o, st), isPreview);
    }

    /// <summary>
    /// Schematic MOSFET view for the transistor step: body block, S/D wells, gate oxide and gate on top.
    /// Topology (which side of the gate each region/contact is on, relative widths inside each zone and relative
    /// junction depths) comes from the state; absolute proportions are normalised for readability.
    /// </summary>
    static SceneData BuildDevice(WaferState st, SceneOptions o, ElectricalResult? el, bool isPreview)
    {
        var sd = new SceneData { IsPreview = isPreview, ViewKind = "device" };
        // Same transparency rules as the classification view: bright edges, see-through body, stronger S/D wells.
        bool tr = o.Transparent;
        int edge = tr ? 2 : 1;
        float aBody = tr ? 0.38f : 1f, aWell = tr ? 0.75f : 1f, aMetal = tr ? 0.6f : 1f, aOx = tr ? 0.35f : 1f;
        int n = st.N;
        const double bodyBottom = -0.55, bodyTop = 3.2, bodyH = bodyTop - bodyBottom, depthY = 6;
        const double oxideH = 0.1, gateH = 1.3, plugH = 0.9;
        var gate = st.Wires.FirstOrDefault(w => w.Role == "Gate" && w.ColEnd > w.ColStart);

        // Piecewise X map: [0,gA) → [0,4), [gA,gB) → [4,8), [gB,n) → [8,12). Linear when there is no usable gate.
        bool piecewise = gate != null && gate.ColStart > 0 && gate.ColEnd < n;
        int gA = gate?.ColStart ?? 0, gB = gate?.ColEnd ?? n;
        double X(int c)
        {
            if (!piecewise) return (double)c / n * WorldX;
            double third = WorldX / 3;
            if (c <= gA) return third * c / gA;
            if (c <= gB) return third + third * (c - gA) / (gB - gA);
            return 2 * third + third * (c - gB) / (n - gB);
        }

        // Conductive Si (N+/P+) depth per column, measured from the Si surface.
        var grid = SiGrid.Build(st);
        var depth = new double[n];
        var kind = new Cls[n];
        int searchCells = Math.Max(1, (int)Math.Ceiling(100 / grid.Dz));
        for (int c = 0; c < n; c++)
        {
            int first = -1;
            for (int d = 0; d < Math.Min(grid.Depth, searchCells); d++)
                if (Classes.IsConductiveSi(grid.ClassAt(c, d))) { first = d; break; }
            if (first < 0) continue;
            kind[c] = grid.ClassAt(c, first);
            int last = first;
            while (last + 1 < grid.Depth && grid.ClassAt(c, last + 1) == kind[c]) last++;
            depth[c] = (last + 1) * grid.Dz;
        }
        double maxDepth = depth.DefaultIfEmpty(0).Max();

        var bodyCls = grid.ClassAt(n / 2, grid.Depth - 1);
        // Same palette as the electrical classification view (Classes.Color), drawn opaque.
        var (bR, bG, bB, _) = Classes.Color(bodyCls is Cls.PSemi or Cls.NSemi ? bodyCls : Cls.PSemi);
        var (mR, mG, mB, _) = Classes.Color(Cls.Metal);
        var (iR, iG, iB, _) = Classes.Color(Cls.Insulator);
        sd.Add(0, WorldX, 0, depthY, bodyBottom, bodyTop, bR, bG, bB, aBody, edge, "sub");

        // Source/drain wells: runs of columns with the same conductive type.
        var sdTypes = new HashSet<Cls>();
        for (int a = 0; a < n;)
        {
            if (depth[a] <= 0) { a++; continue; }
            int b = a + 1;
            while (b < n && depth[b] > 0 && kind[b] == kind[a]) b++;
            double runDepth = 0;
            for (int c = a; c < b; c++) runDepth = Math.Max(runDepth, depth[c]);
            double visDepth = bodyH * Math.Max(0.12, 0.6 * runDepth / maxDepth);
            var (r, g, bl, _) = Classes.Color(kind[a]);
            sdTypes.Add(kind[a]);
            string pick = piecewise ? (b <= gA ? "t:S" : a >= gB ? "t:D" : "") : "";
            // Grow outward faces slightly so they do not z-fight with the body's coplanar faces.
            const double eps = 0.004;
            sd.Add(X(a) - (a == 0 ? eps : 0), X(b) + (b == n ? eps : 0), -eps, depthY + eps,
                bodyTop - visDepth, bodyTop + 0.002, r, g, bl, aWell, tr ? 2 : 1, pick);
            a = b;
        }

        // Gate oxide + gate electrode.
        double topZ = bodyTop;
        if (gate != null)
        {
            int mid = (gate.ColStart + gate.ColEnd) / 2;
            var col = st.Columns[mid];
            int gateSeg = col.Segs.FindIndex(g => g.LayerId == gate.LayerId);
            sd.Add(X(gate.ColStart), X(gate.ColEnd), 0, depthY, bodyTop, bodyTop + oxideH, iR, iG, iB, aOx, edge,
                $"f:{gate.ColStart}:{gate.ColEnd}:0");
            sd.Add(X(gate.ColStart), X(gate.ColEnd), 0, depthY, bodyTop + oxideH, bodyTop + oxideH + gateH, mR, mG, mB, aMetal, edge,
                gateSeg >= 0 ? $"f:{gate.ColStart}:{gate.ColEnd}:{gateSeg}" : "t:G");
            topZ = bodyTop + oxideH + gateH;
        }

        // Contact plugs on the S/D wells (middle third in Y so they read as plugs, not walls).
        foreach (var ct in st.Contacts.Where(x => x.ColEnd > x.ColStart))
        {
            int mid = (ct.ColStart + ct.ColEnd) / 2;
            int seg = st.Columns[mid].Segs.FindIndex(g => g.LayerId == ct.LayerId);
            sd.Add(X(ct.ColStart), X(ct.ColEnd), 0.4 * depthY, 0.6 * depthY, bodyTop, bodyTop + plugH, mR, mG, mB, aMetal, edge,
                seg >= 0 ? $"f:{ct.ColStart}:{ct.ColEnd}:{seg}" : "");
            topZ = Math.Max(topZ, bodyTop + plugH);
        }

        if (el != null)
        {
            // Small floating cubes above each terminal (like the Source/Gate/Drain arrows of a textbook figure).
            double markZ0 = topZ + 0.6, markZ1 = markZ0 + 0.45;
            void Mark(int a, int b, float r, float g, float bl, string pick)
            {
                if (a < 0 || b <= a) return;
                double cx = 0.5 * (X(a) + X(b));
                sd.Add(cx - 0.25, cx + 0.25, 0.5 * depthY - 0.25, 0.5 * depthY + 0.25, markZ0, markZ1, r, g, bl, 1, 1, pick);
            }
            Mark(el.GateA, el.GateB, 0.95f, 0.15f, 0.15f, "t:G");
            Mark(el.SourceA, el.SourceB, 0.15f, 0.80f, 0.25f, "t:S");
            Mark(el.DrainA, el.DrainB, 0.20f, 0.40f, 0.95f, "t:D");
            if (el.Vov > 0 && !el.StructureFail)
            {
                float alpha = (float)Math.Min(0.9, 0.3 + el.Vov / Math.Max(1e-9, el.Vref));
                sd.Add(X(el.GateA), X(el.GateB), 0, depthY, bodyTop - 0.08, bodyTop + 0.003, 1f, 0.92f, 0.2f, alpha, 0, "inv");
            }
            sd.Terminals = $"<span style=\"color:#f22\">■ G</span> <span style=\"color:#2c4\">■ S</span> <span style=\"color:#36f\">■ D</span> · {el.Applied}"
                           + (el.Vov > 0 && !el.StructureFail ? $" · 반전층 표시(Vov {Fmt.S(el.Vov, 3)} V)" : "");
        }

        var leg = new StringBuilder();
        if (isPreview) leg.Append("<b style=\"color:#d80\">미리보기 — 커밋되지 않음</b><br/>");
        string bodyLabel = bodyCls is Cls.NSemi or Cls.NPlus ? "Body (n형 기판)" : bodyCls is Cls.PSemi or Cls.PPlus ? "Body (p형 기판)" : "Body (기판)";
        leg.Append(Swatch(bR, bG, bB, bodyLabel)).Append("&nbsp; ");
        foreach (var c in new[] { Cls.NPlus, Cls.PPlus })
            if (sdTypes.Contains(c)) { var (r, g, b, _) = Classes.Color(c); leg.Append(Swatch(r, g, b, $"Source/Drain ({Classes.Ko(c)})")).Append("&nbsp; "); }
        if (gate != null)
        {
            leg.Append(Swatch(mR, mG, mB, $"Gate ({gate.Material}, 도체)")).Append("&nbsp; ");
            leg.Append(Swatch(iR, iG, iB, "게이트 산화막(SiO2, 절연체)")).Append("&nbsp; ");
        }
        if (st.Contacts.Count > 0) leg.Append(Swatch(mR, mG, mB, "컨택 플러그(도체)"));
        leg.Append("<br/>소자 보기: 위치 관계·상대 깊이만 표시(비율 정규화). 실제 단면은 보기 ▸ 전기적 분류 보기를 끄세요.");
        sd.LegendHtml = leg.ToString();
        sd.ScaleText = "소자 보기(개략도) · X: 소스 | 게이트 | 드레인 구간을 같은 폭으로 정규화 · Z: 접합 깊이는 상대값";
        return sd;
    }

    static SceneData BuildWafer(WaferState st, SceneOptions o, ElectricalResult? el, bool isPreview)
    {
        var sd = new SceneData { IsPreview = isPreview, ViewKind = o.View == "device" ? "device" : "wafer" };
        int n = st.N;
        double X(int i) => i * WorldX / n;
        var fr = Frame(st);
        double Z(double h) => fr.Z(h);
        bool tr = o.Transparent;
        int edge = tr ? 2 : 1;
        SiGrid? grid = o.Classification ? SiGrid.Build(st) : null;

        // Runs of columns with identical stacks.
        for (int a = 0; a < n;)
        {
            int b = a + 1;
            while (b < n && st.Columns[b].SameStack(st.Columns[a])) b++;
            var col = st.Columns[a];
            double x0 = X(a), x1 = X(b);
            if (!o.Classification)
            {
                var (r, g, bl) = Mat.Rgb(Mat.Si);
                sd.Add(x0, x1, 0, WorldY, 0, Z(col.SiTop), r, g, bl, MatAlpha(Mat.Si, tr), edge, $"s:{a}:{b}");
            }
            double h = col.SiTop;
            for (int k = 0; k < col.Segs.Count; k++)
            {
                var seg = col.Segs[k];
                string pick = $"f:{a}:{b}:{k}";
                if (o.Classification)
                {
                    var cls = Mat.IsMetal(seg.Material) ? Cls.Metal : Cls.Insulator;
                    var (r, g, bl, al) = Classes.Color(cls);
                    sd.Add(x0, x1, 0, WorldY, Z(h), Z(h + seg.Thickness), r, g, bl, tr ? Math.Min(al, 0.6f) : al, edge, pick);
                }
                else
                {
                    var (r, g, bl) = Mat.Rgb(seg.Material);
                    sd.Add(x0, x1, 0, WorldY, Z(h), Z(h + seg.Thickness), r, g, bl, MatAlpha(seg.Material, tr), edge, pick);
                }
                h += seg.Thickness;
            }
            a = b;
        }

        // Silicon: classification bins or doping heat-map overlay on the front face.
        var groups = st.Doping?.Groups ?? new List<ColumnGroup> { new() { Start = 0, End = n } };
        const int bins = 60;
        foreach (var grp in groups)
        {
            for (int a = grp.Start; a < grp.End;)
            {
                int b = a + 1;
                while (b < grp.End && Math.Abs(st.Columns[b].SiTop - st.Columns[a].SiTop) < 1e-9) b++;
                double siTop = st.Columns[a].SiTop, span = siTop - fr.Bottom, binH = span / bins;
                double x0 = X(a), x1 = X(b);
                if (grid != null)
                {
                    int k0 = 0;
                    Cls cur = ClassAtDepth(grid, a, 0.5 * binH);
                    for (int k = 1; k <= bins; k++)
                    {
                        Cls next = k < bins ? ClassAtDepth(grid, a, (k + 0.5) * binH) : (Cls)(-1);
                        if (next == cur) continue;
                        var (r, g, bl, al) = Classes.Color(cur);
                        float alpha = tr ? (Classes.IsConductiveSi(cur) ? 0.75f : 0.38f) : al;
                        sd.Add(x0, x1, 0, WorldY, Z(siTop - k * binH), Z(siTop - k0 * binH), r, g, bl, alpha,
                            Classes.IsConductiveSi(cur) ? 2 : edge, $"s:{a}:{b}");
                        k0 = k;
                        cur = next;
                    }
                }
                else if (st.Doping != null && grp.Species.Count > 0)
                {
                    for (int k = 0; k < bins; k++)
                    {
                        double tot = st.Doping.TotalAt(grp, (k + 0.5) * binH);
                        if (tot < 1e14) continue;
                        var (r, g, bl) = Heat(Math.Log10(tot));
                        sd.Add(x0, x1, -0.02, 0, Z(siTop - (k + 1) * binH), Z(siTop - k * binH), r, g, bl, 0.85f, 0, $"s:{a}:{b}");
                    }
                }
                a = b;
            }
        }

        // Omitted substrate band below a visible gap.
        {
            var (r, g, bl) = Mat.Rgb(Mat.Si);
            sd.Add(0, WorldX, 0, WorldY, -0.55, -0.35, r, g, bl, tr ? 0.45f : 1f, edge, "sub");
        }

        // Electrical overlays.
        if (el != null)
        {
            double markZ0 = Z(fr.Top) + 0.15, markZ1 = markZ0 + 0.3;
            sd.Add(X(el.GateA), X(el.GateB), 1.7, 2.3, markZ0, markZ1, 0.95f, 0.15f, 0.15f, 1, 1, "t:G");
            sd.Add(X(el.SourceA), X(el.SourceB), 1.7, 2.3, markZ0, markZ1, 0.15f, 0.80f, 0.25f, 1, 1, "t:S");
            sd.Add(X(el.DrainA), X(el.DrainB), 1.7, 2.3, markZ0, markZ1, 0.20f, 0.40f, 0.95f, 1, 1, "t:D");
            if (el.Vov > 0 && !el.StructureFail)
            {
                double siTop = st.Columns[(el.GateA + el.GateB) / 2].SiTop;
                double thick = Math.Max(2 * fr.Sz, 0.03);
                float alpha = (float)Math.Min(0.8, el.Vov / Math.Max(1e-9, el.Vref));
                sd.Add(X(el.GateA), X(el.GateB), 0, WorldY, Z(siTop) - thick, Z(siTop) + 0.004, 1f, 0.92f, 0.2f, alpha, 0, "inv");
            }
            if (el.StructureFail && el.FailA >= 0)
                sd.Add(X(el.FailA), X(el.FailB), -0.05, WorldY + 0.05, 0, Z(fr.Top) + 0.05, 1f, 0.1f, 0.1f, 0.06f, 2);
            sd.Terminals = $"<span style=\"color:#f22\">■ G</span> <span style=\"color:#2c4\">■ S</span> <span style=\"color:#36f\">■ D</span> · {el.Applied}"
                           + (el.Vov > 0 && !el.StructureFail ? $" · 반전층 표시(Vov {Fmt.S(el.Vov, 3)} V)" : "");
        }

        // Legend and scale.
        var leg = new StringBuilder();
        if (isPreview) leg.Append("<b style=\"color:#d80\">미리보기 — 커밋되지 않음</b><br/>");
        if (o.Classification)
        {
            foreach (var c in new[] { Cls.Metal, Cls.NPlus, Cls.PPlus, Cls.NSemi, Cls.PSemi, Cls.Insulator })
            {
                var (r, g, b, _) = Classes.Color(c);
                leg.Append(Swatch(r, g, b, Classes.Ko(c))).Append("&nbsp; ");
            }
            leg.Append("<br/>전기적 분류 보기(교육용 임계값: |순활성| ≥ 1e19 도체성, ≥ 1e14 반도체)");
        }
        else
        {
            var mats = new List<string> { Mat.Si };
            foreach (var c in st.Columns) foreach (var g in c.Segs) if (!mats.Contains(g.Material)) mats.Add(g.Material);
            foreach (var m in mats)
            {
                var (r, g, b) = Mat.Rgb(m);
                leg.Append(Swatch(r, g, b, $"{Mat.Ko(m)}({m})")).Append("&nbsp; ");
            }
            if (st.HasDoping) leg.Append("<br/>앞면 오버레이: 도핑 log10(전체 농도) 1e14(파랑) → 1e21(빨강)");
        }
        var ids = st.PresentLayerIds().ToList();
        leg.Append("<br/>LayerId: ").Append(ids.Count == 0 ? "없음(BareSi)" : string.Join(", ", ids));
        sd.LegendHtml = leg.ToString();
        double hScale = WorldX / (st.DomainWidthUm * 1000);
        sd.ScaleText = $"X: 단면 폭 {Fmt.S(st.DomainWidthUm, 4)} µm → 12 · Z: 표시 창 {Fmt.S(fr.Top - fr.Bottom, 4)} nm → 5 (수직 과장 ×{Fmt.S(fr.Sz / hScale, 3)}) · "
                       + $"기판 {Fmt.S(st.SubstrateThicknessUm, 4)} µm 중 상부 {Fmt.S(fr.SiDepth, 4)} nm만 표시(아래 띠는 생략 표시) · Y: 배선 길이 방향(실제 길이 아님)";
        return sd;
    }

    public static Cls ClassAtDepth(SiGrid g, int col, double z)
    {
        int d = Math.Clamp((int)Math.Round(z / g.Dz), 0, g.Depth - 1);
        return g.ClassAt(col, d);
    }

    static (float, float, float) Heat(double log10)
    {
        double t = Math.Clamp((log10 - 14) / 7, 0, 1);
        // blue → cyan → yellow → red
        if (t < 0.33) { double u = t / 0.33; return (0.1f, (float)(0.3 + 0.6 * u), 0.9f); }
        if (t < 0.66) { double u = (t - 0.33) / 0.33; return ((float)(0.1 + 0.85 * u), 0.9f, (float)(0.9 - 0.8 * u)); }
        { double u = (t - 0.66) / 0.34; return (0.95f, (float)(0.9 - 0.75 * u), 0.1f); }
    }

    public static Mask? MaskFor(Session s, SceneOptions o) => o.Preview && s.PreviewMask != null ? s.PreviewMask : s.Masks.LastOrDefault();

    static SceneData BuildMask(Session s, SceneOptions o)
    {
        var sd = new SceneData { ViewKind = "mask" };
        var m = MaskFor(s, o);
        sd.IsPreview = o.Preview && s.PreviewMask != null;
        double domainNm = s.Current.DomainWidthUm * 1000;
        sd.Add(0, WorldX, 0, WorldY, 0, 0.8, 0.85f, 0.90f, 0.97f, 0.35f, o.Transparent ? 2 : 1, m == null ? "" : "m:quartz");
        if (m == null)
        {
            sd.LegendHtml = "아직 제작한 마스크가 없습니다. 조건을 넣고 [계산 미리보기] 또는 [공정 실행]을 누르세요.";
            sd.ScaleText = "마스크 축척 4×, 웨이퍼 환산 표시";
            return sd;
        }
        bool euv = m.MaskType == "EUVReflective";
        var (ar, ag, ab) = euv ? (0.30f, 0.20f, 0.35f) : (0.22f, 0.22f, 0.25f);
        double X(double nm) => Math.Clamp(nm / domainNm, 0, 1) * WorldX;
        double cursor = 0;
        foreach (var (lo, hi) in m.ClearIntervals(domainNm))
        {
            if (lo > cursor) sd.Add(X(cursor), X(lo), 0, WorldY, 0.8, 1.0, ar, ag, ab, 1, 1, "m:absorber");
            if (m.AbsorberResidual > 0) sd.Add(X(lo), X(hi), 0, WorldY, 0.8, 0.8 + 0.2 * Math.Min(1, m.AbsorberResidual / 100), ar, ag, ab, 0.6f, 1, "m:residual");
            cursor = hi;
        }
        if (cursor < domainNm) sd.Add(X(cursor), WorldX, 0, WorldY, 0.8, 1.0, ar, ag, ab, 1, 1, "m:absorber");
        if (m.Pellicle)
        {
            sd.Add(0, 0.2, 0, WorldY, 1.0, 2.6, 0.6f, 0.6f, 0.65f, 1, 1, "m:pellicle");
            sd.Add(WorldX - 0.2, WorldX, 0, WorldY, 1.0, 2.6, 0.6f, 0.6f, 0.65f, 1, 1, "m:pellicle");
            sd.Add(0, WorldX, 0, WorldY, 2.6, 2.63, 0.8f, 0.9f, 1f, 0.15f, 1, "m:pellicle");
        }
        sd.LegendHtml = (sd.IsPreview ? "<b style=\"color:#d80\">미리보기 — 커밋되지 않음</b><br/>" : "")
                        + Swatch(0.85f, 0.90f, 0.97f, "석영 기판(반투명)") + "&nbsp; " + Swatch(ar, ag, ab, euv ? "EUV 흡수체" : "Cr 차광막")
                        + (m.Pellicle ? "&nbsp; " + Swatch(0.6f, 0.6f, 0.65f, "펠리클 프레임·막") : "")
                        + $"<br/>{m.MaskId} · {m.MaskType} · {m.MaskLayer} · CD {Fmt.S(m.MaskCd, 5)} nm · {(m.Passed ? "합격" : "불합격")}";
        sd.ScaleText = $"마스크 축척 4×, 웨이퍼 환산 표시 · X: {Fmt.S(s.Current.DomainWidthUm, 4)} µm → 12";
        return sd;
    }
}
