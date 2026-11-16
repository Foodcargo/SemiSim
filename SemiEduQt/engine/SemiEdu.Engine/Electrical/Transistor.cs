namespace SemiEdu.Engine;

public sealed class ElectricalResult
{
    public Guid StateId;
    public string Mode = "";
    public string DeviceType = "";
    public bool Normal;
    public string Verdict = "";
    public readonly List<string> FailureCodes = new();
    public readonly List<(string Name, string Value)> Extracted = new();
    public readonly List<(double X, double Y)> Sweep = new();
    public string SweepKind = "", XLabel = "", YLabel = "", Region = "", Applied = "";
    public bool LogY;
    public double? VthExt;
    public double Vov, Vref = 1.2;
    public int GateA = -1, GateB = -1, SourceA = -1, SourceB = -1, DrainA = -1, DrainB = -1, FailA = -1, FailB = -1;
    public bool StructureFail;
}

public sealed class DeviceStructure
{
    public Wire? Gate;
    public ContactRec? Source, Drain;
    public double ToxNm = double.NaN;
    public bool GateShortChannel, GateShortSD, OpenSource, OpenDrain, SdShort, NoJunction, UngatedGap;
    public string ShortTo = "";
    public string SdType = "", BodyType = "";
    public double Nb;
    public double LNm = double.NaN;
    public int GapA = -1, GapB = -1;
    public readonly List<string> Codes = new();
    public string DeviceType = "NMOS";

    public static readonly Dictionary<string, string> MessagesKo = new()
    {
        ["GATE_SHORT_TO_CHANNEL"] = "게이트 금속과 채널(Si) 사이 절연막이 없는 열이 있음",
        ["GATE_SHORT_TO_SD"] = "게이트가 소스/드레인과 같은 도체 네트에 연결됨",
        ["OPEN_SOURCE"] = "소스 컨택 아래에 도체성(N+/P+) Si가 없음",
        ["OPEN_DRAIN"] = "드레인 컨택 아래에 도체성(N+/P+) Si가 없음",
        ["SD_SHORT"] = "게이트 없이도 소스–드레인이 도체성 영역으로 연결됨",
        ["NO_JUNCTION"] = "소스/드레인 도핑형이 게이트 아래 바디와 같아 PN 접합이 없음",
        ["UNGATED_GAP"] = "소스–드레인 사이에 게이트가 덮지 않은 간격이 있음",
        ["TYPE_MISMATCH"] = "지정한 소자 형과 구조에서 추출한 형이 다름",
    };

    /// <summary>Minimum insulator thickness between the gate metal and Si over the gate columns (no netlist needed).</summary>
    public static double GateTox(WaferState s, Wire gate)
    {
        double tox = double.PositiveInfinity;
        for (int c = gate.ColStart; c < gate.ColEnd; c++)
        {
            var segs = s.Columns[c].Segs;
            int k = segs.FindIndex(g => g.LayerId == gate.LayerId);
            if (k < 0) continue;
            double ins = 0;
            for (int j = 0; j < k; j++) if (Mat.IsInsulator(segs[j].Material)) ins += segs[j].Thickness;
            tox = Math.Min(tox, ins);
        }
        return double.IsPositiveInfinity(tox) ? double.NaN : tox;
    }

    public static DeviceStructure Analyze(WaferState s, Netlist nl)
    {
        var ds = new DeviceStructure
        {
            Gate = s.Wires.FirstOrDefault(w => w.Role == "Gate"),
            Source = s.Contacts.FirstOrDefault(x => x.Role == "Source"),
            Drain = s.Contacts.FirstOrDefault(x => x.Role == "Drain"),
        };
        if (ds.Gate == null || ds.Source == null || ds.Drain == null) return ds;
        var g = ds.Gate;
        ds.ToxNm = GateTox(s, g);
        ds.GateShortChannel = !(ds.ToxNm > 1e-9);

        NetInfo? NetOfLayer(int col, string layer)
        {
            int k = s.Columns[col].Segs.FindIndex(x => x.LayerId == layer);
            return k < 0 ? null : nl.NetOfFilm(col, k);
        }
        var gNet = NetOfLayer(g.ColStart, g.LayerId);
        var sNet = NetOfLayer(ds.Source.ColStart, ds.Source.LayerId);
        var dNet = NetOfLayer(ds.Drain.ColStart, ds.Drain.LayerId);
        if (gNet != null && (gNet == sNet || gNet == dNet))
        {
            ds.GateShortSD = true;
            ds.ShortTo = gNet == sNet ? "Source" : "Drain";
        }
        ds.OpenSource = sNet == null || !(sNet.HasNPlus || sNet.HasPPlus);
        ds.OpenDrain = dNet == null || !(dNet.HasNPlus || dNet.HasPPlus);
        ds.SdShort = !ds.OpenSource && !ds.OpenDrain && sNet == dNet;
        var sdNet = !ds.OpenSource ? sNet : !ds.OpenDrain ? dNet : null;
        ds.SdType = sdNet == null ? "" : sdNet.HasNPlus ? "N" : "P";

        int gc = (g.ColStart + g.ColEnd - 1) / 2;
        double body = nl.Grid.NetAt(gc, 0);
        ds.BodyType = body >= 0 ? "n" : "p";
        ds.Nb = Math.Abs(body);
        ds.NoJunction = ds.SdType.Length > 0 && ((ds.SdType == "N" && ds.BodyType == "n") || (ds.SdType == "P" && ds.BodyType == "p"));

        if (!ds.OpenSource && !ds.OpenDrain && !ds.SdShort)
        {
            var sc = sNet!.ConductiveSiCols;
            var dc = dNet!.ConductiveSiCols;
            if (sc.Max < dc.Min) { ds.GapA = sc.Max + 1; ds.GapB = dc.Min; }
            else if (dc.Max < sc.Min) { ds.GapA = dc.Max + 1; ds.GapB = sc.Min; }
            if (ds.GapB > ds.GapA)
            {
                ds.LNm = (ds.GapB - ds.GapA) * s.ColWidthNm;
                ds.UngatedGap = ds.GapA < g.ColStart || ds.GapB > g.ColEnd;
            }
        }
        ds.DeviceType = ds.SdType == "P" ? "PMOS" : ds.SdType == "N" ? "NMOS" : (ds.BodyType == "n" ? "PMOS" : "NMOS");

        if (ds.GateShortChannel) ds.Codes.Add("GATE_SHORT_TO_CHANNEL");
        if (ds.GateShortSD) ds.Codes.Add("GATE_SHORT_TO_SD");
        if (ds.OpenSource) ds.Codes.Add("OPEN_SOURCE");
        if (ds.OpenDrain) ds.Codes.Add("OPEN_DRAIN");
        if (ds.SdShort) ds.Codes.Add("SD_SHORT");
        if (ds.NoJunction) ds.Codes.Add("NO_JUNCTION");
        if (ds.UngatedGap) ds.Codes.Add("UNGATED_GAP");
        return ds;
    }
}

/// <summary>Long-channel square-law MOSFET, 300 K. Educational constants, not a device-characteristic prediction.</summary>
public sealed class MosfetModel
{
    public const double Q = 1.602176634e-19, Eps0 = 8.8541878128e-12, Vt = 0.025852, Ni = 1e10, NSub = 1.5, Leak = 1e-12;
    public const double ChiSi = 4.05, Eg = 1.12;

    public readonly bool Pmos;
    public readonly double PhiF, Cox, Gamma, Vfb, Beta, Vth0, ToxNm, Nb, WUm, LNm;
    readonly DeviceStructure ds;

    public MosfetModel(DeviceStructure s, double toxNm, double nb, string gateMetal, double wUm, double lNm, bool pmos)
    {
        ds = s;
        Pmos = pmos;
        ToxNm = toxNm;
        Nb = nb;
        WUm = wUm;
        LNm = lNm;
        double eox = 3.9 * Eps0, esi = 11.7 * Eps0;
        PhiF = Vt * Math.Log(nb / Ni);
        Cox = eox / (toxNm * 1e-9);
        Gamma = Math.Sqrt(2 * Q * esi * nb * 1e6) / Cox;
        double phiM = Mat.WorkFunction(gateMetal);
        double vfbReal = pmos ? phiM - (ChiSi + Eg / 2 - PhiF) : phiM - (ChiSi + Eg / 2 + PhiF);
        // NMOS frame: PMOS is evaluated with every voltage and current sign inverted.
        Vfb = pmos ? -vfbReal : vfbReal;
        double mu = (pmos ? 150 : 400) * 1e-4;
        Beta = mu * Cox * (wUm * 1e-6) / (lNm * 1e-9);
        Vth0 = VthAt(0);
    }

    public double VthAt(double vsb)
    {
        double p2 = 2 * PhiF;
        return Vfb + p2 + Gamma * Math.Sqrt(p2) + Gamma * (Math.Sqrt(Math.Max(0, p2 + vsb)) - Math.Sqrt(p2));
    }

    /// <summary>Reported threshold in the device's own sign convention.</summary>
    public double Vth => Pmos ? -Vth0 : Vth0;

    double IdN(double vgs, double vds, double vsb)
    {
        if (vds < 0) return -IdN(vgs - vds, -vds, vsb + vds);
        double vov = vgs - VthAt(vsb);
        double id;
        if (vov <= 0) id = Beta * (NSub - 1) * Vt * Vt * Math.Exp(vov / (NSub * Vt)) * (1 - Math.Exp(-vds / Vt));
        else if (vds < vov) id = Beta * (vov * vds - vds * vds / 2);
        else id = 0.5 * Beta * vov * vov;
        return id + Leak * Math.Sign(vds);
    }

    public string Region(double vg, double vd, double vs, double vb)
    {
        if (ds.Codes.Count > 0) return "구조 실패";
        double sgn = Pmos ? -1 : 1;
        double vgs = sgn * (vg - vs), vds = sgn * (vd - vs), vsb = sgn * (vs - vb);
        if (vds < 0) { vgs -= vds; vsb += vds; vds = -vds; }
        double vov = vgs - VthAt(vsb);
        return vov <= 0 ? "차단(서브문턱)" : vds < vov ? "선형" : "포화";
    }

    public double Vov(double vg, double vs, double vb)
    {
        double sgn = Pmos ? -1 : 1;
        return sgn * (vg - vs) - VthAt(sgn * (vs - vb));
    }

    /// <summary>Drain current (A, into the drain) including structural failure models (spec 10.4).</summary>
    public double Id(double vg, double vd, double vs, double vb)
    {
        if (ds.OpenSource || ds.OpenDrain) return 0;
        if (ds.SdShort) return (vd - vs) / 100;
        if (ds.NoJunction) return (vd - vs) / 1000;
        if (ds.GateShortChannel || ds.UngatedGap) return Leak * Math.Sign(vd - vs);
        double sgn = Pmos ? -1 : 1;
        return sgn * IdN(sgn * (vg - vs), sgn * (vd - vs), sgn * (vs - vb));
    }

    public double Ig(double vg, double vd, double vs, double vb)
    {
        if (ds.GateShortChannel) return (vg - vb) / 100;
        if (ds.GateShortSD) return (vg - (ds.ShortTo == "Source" ? vs : vd)) / 100;
        return 0;
    }
}

public sealed class TransistorTestProcess : ProcessBase
{
    public override string Id => "transistor";
    public override string Group => "⑦ EDS(검사)";
    public override string Name => "트랜지스터 전기 검사";
    public override bool ChangesState => false;
    public override string EquipmentId => "ProberEdu";
    public override string[] Unsupported => new[]
    {
        "짧은 채널 효과", "이동도 열화", "소스·드레인 저항", "컨택 저항", "열전자", "터널링 누설", "온도", "측방향 확산·게이트 아래 파고듦",
    };

    static string Mode(FormCtx f) => f.E("testMode");
    static bool Sweep(FormCtx f) => Mode(f) is "IdVgSweep" or "IdVdSweep";

    static readonly ParamDef[] Ps =
    {
        En("testMode", "검사 모드", "AutoJudge", "SinglePoint", "IdVgSweep", "IdVdSweep"),
        En("deviceType", "소자 형", "Auto", "NMOS", "PMOS"),
        Num("vg", "게이트 전압 Vg", "V", -5, 5, 1.2, f => Mode(f) is "SinglePoint" or "IdVdSweep"),
        Num("vd", "드레인 전압 Vd", "V", -5, 5, 1.2, f => Mode(f) is "SinglePoint" or "IdVgSweep"),
        Num("vs", "소스 전압 Vs", "V", -5, 5, 0, f => Mode(f) != "AutoJudge"),
        Num("vb", "바디 전압 Vb", "V", -5, 5, 0, note: "기판 뒷면 이상 접촉"),
        Num("vdd", "전원 전압 Vdd", "V", 0.1, 5, 1.2, f => Mode(f) == "AutoJudge"),
        Num("sweepStart", "스위프 시작", "V", -5, 5, -0.5, Sweep),
        Num("sweepStop", "스위프 끝", "V", -5, 5, 1.2, Sweep),
        Num("sweepPoints", "스위프 점 수", "점", 5, 201, 61, Sweep, integer: true),
        Q("minIon", "최소 on 전류", "A", 1e-12, 1, 1e-6, f => Mode(f) != "SinglePoint"),
        Q("maxIoff", "최대 off 전류", "A", 1e-15, 1e-3, 1e-9, f => Mode(f) != "SinglePoint"),
        Q("minOnOff", "최소 on/off 비", "", 1, 1e12, 1e3, f => Mode(f) != "SinglePoint"),
        Q("maxIg", "최대 게이트 전류", "A", 1e-15, 1e-3, 1e-9),
        Q("minVth", "최소 문턱 전압", "V", -2, 3, 0.0, f => Mode(f) != "SinglePoint"),
        Q("maxVth", "최대 문턱 전압", "V", -2, 3, 1.5, f => Mode(f) != "SinglePoint"),
    };
    public override IReadOnlyList<ParamDef> Params => Ps;

    public override void Cross(Recipe r, Ctx c, List<Err> e)
    {
        if (r.Nums.ContainsKey("sweepStart") && r.N("sweepStart") >= r.N("sweepStop"))
            InputErr(e, "SWEEP_RANGE", "sweepStart", F(r.N("sweepStart")), "< sweepStop", "스위프 시작은 끝보다 작아야 합니다.");
    }

    public override void Preconditions(Recipe r, Ctx c, List<Err> e)
    {
        var s = c.State;
        var missing = new List<string>();
        if (!s.Wires.Any(w => w.Role == "Gate")) missing.Add("Gate");
        if (!s.Contacts.Any(x => x.Role == "Source")) missing.Add("Source");
        if (!s.Contacts.Any(x => x.Role == "Drain")) missing.Add("Drain");
        if (missing.Count > 0)
            Pre(e, "TERMINAL_MISSING", $"단자가 없습니다: {string.Join(", ", missing)}", "게이트(role=Gate 배선)와 소스·드레인 컨택을 먼저 만드세요.");
        if (s.HasPR) Pre(e, "PR_PRESENT", "PR이 남아 있어 프로빙할 수 없습니다.", "PRStrip을 먼저 수행하세요.");
    }

    /// <summary>Bias points (vg, vd, vs) the prober will apply.</summary>
    static IEnumerable<(double Vg, double Vd, double Vs)> Points(Recipe r, bool pmos)
    {
        double sg = pmos ? -1 : 1;
        switch (r.E("testMode"))
        {
            case "AutoJudge":
                double vdd = r.N("vdd");
                yield return (sg * vdd, sg * vdd, 0);
                yield return (0, sg * vdd, 0);
                break;
            case "SinglePoint":
                yield return (r.N("vg"), r.N("vd"), r.N("vs"));
                break;
            case "IdVgSweep":
                yield return (r.N("sweepStart"), r.N("vd"), r.N("vs"));
                yield return (r.N("sweepStop"), r.N("vd"), r.N("vs"));
                break;
            default:
                yield return (r.N("vg"), r.N("sweepStart"), r.N("vs"));
                yield return (r.N("vg"), r.N("sweepStop"), r.N("vs"));
                break;
        }
    }

    public override void Interlock(Recipe r, Ctx c, List<Err> e)
    {
        var gate = c.State.Wires.First(w => w.Role == "Gate");
        double tox = DeviceStructure.GateTox(c.State, gate);
        if (!(tox > 1e-9)) return;
        double vb = r.N("vb"), worst = 0;
        bool pmos = Analysis(r, c).DeviceType == "PMOS";
        foreach (var (vg, vd, vs) in Points(r, pmos))
            worst = Math.Max(worst, Math.Max(Math.Abs(vg - vs), Math.Max(Math.Abs(vg - vd), Math.Abs(vg - vb))));
        double eox = worst / (tox * 1e-7) / 1e6;
        if (eox > 8)
            Ilk(e, "PROBER_OXIDE_FIELD", "vg", eox, "≤ 8 MV/cm", $"산화막 전계 {F(eox)} MV/cm(tox {F(tox)} nm)가 8 MV/cm를 넘어 전압을 걸기 전에 차단했습니다(산화막 파괴 방지).");
    }

    static DeviceStructure Analysis(Recipe r, Ctx c)
    {
        if (c.Cache.TryGetValue("device", out var o)) return (DeviceStructure)o;
        var nl = new Netlist(c.State);
        var ds = DeviceStructure.Analyze(c.State, nl);
        c.Cache["device"] = ds;
        return ds;
    }

    public override void Domain(Recipe r, Ctx c, List<Err> e)
    {
        var ds = Analysis(r, c);
        double cw = c.State.ColWidthNm;
        if (double.IsFinite(ds.LNm) && ds.LNm < 4 * cw)
            Dom(e, "GEOMETRY_RESOLUTION", $"채널 길이 {F(ds.LNm)} nm가 4열({F(4 * cw)} nm)보다 짧습니다.", "게이트를 넓히세요.");
        if (ds.Nb < Classes.SemiThreshold && ds.Codes.Count == 0)
            Dom(e, "BODY_UNDEFINED", "게이트 아래 바디 농도가 너무 낮아 문턱 전압을 정의할 수 없습니다.", "기판 도핑을 확인하세요.");
    }

    public override Output Calculate(Recipe r, Ctx c, CancellationToken t)
    {
        var s = c.State;
        var ds = Analysis(r, c);
        var gate = ds.Gate!;
        var codes = new List<string>(ds.Codes);
        string want = r.E("deviceType");
        if (want != "Auto" && want != ds.DeviceType) codes.Add("TYPE_MISMATCH");
        bool pmos = ds.DeviceType == "PMOS";
        double lNm = double.IsFinite(ds.LNm) ? ds.LNm : gate.WidthNm;
        double toxForModel = double.IsFinite(ds.ToxNm) && ds.ToxNm > 1e-9 ? ds.ToxNm : 1;
        var mos = new MosfetModel(ds, toxForModel, Math.Max(ds.Nb, Classes.SemiThreshold), gate.Material, gate.LengthUm, lNm, pmos);
        double vb = r.N("vb");
        string mode = r.E("testMode");
        var er = new ElectricalResult
        {
            Mode = mode, DeviceType = ds.DeviceType, StructureFail = ds.Codes.Count > 0,
            GateA = gate.ColStart, GateB = gate.ColEnd,
            SourceA = ds.Source!.ColStart, SourceB = ds.Source.ColEnd, DrainA = ds.Drain!.ColStart, DrainB = ds.Drain.ColEnd,
        };
        er.FailureCodes.AddRange(codes);
        if (ds.Codes.Count > 0)
        {
            if (ds.GapB > ds.GapA) { er.FailA = ds.GapA; er.FailB = ds.GapB; }
            else { er.FailA = Math.Min(er.SourceA, er.DrainA); er.FailB = Math.Max(er.SourceB, er.DrainB); }
        }
        var o = new Output { Electrical = er };
        o.M("structureFailures", "구조 판정 실패 수", codes.Count, "개");

        double sg = pmos ? -1 : 1;
        if (mode == "SinglePoint")
        {
            double vg = r.N("vg"), vd = r.N("vd"), vs = r.N("vs");
            double id = mos.Id(vg, vd, vs, vb), ig = mos.Ig(vg, vd, vs, vb);
            er.Region = mos.Region(vg, vd, vs, vb);
            er.Applied = $"Vg {F(vg)} V · Vd {F(vd)} V · Vs {F(vs)} V · Vb {F(vb)} V";
            er.Vov = mos.Vov(vg, vs, vb);
            er.Vref = Math.Max(0.1, Math.Abs(vg - vs));
            o.M("id", "드레인 전류 Id", id, "A");
            o.M("ig", "게이트 전류 Ig", ig, "A");
            o.M("is", "소스 전류 Is", -(id + ig), "A");
            o.M("igAbs", "|Ig|", Math.Abs(ig), "A");
            o.M("region", $"동작 영역: {er.Region}", null, "", er.Region);
        }
        else
        {
            double vdd = mode == "AutoJudge" ? r.N("vdd") : Math.Max(Math.Abs(r.N("sweepStop")), 0.1);
            double ion = Math.Abs(mos.Id(sg * vdd, sg * vdd, 0, vb));
            double ioff = Math.Abs(mos.Id(0, sg * vdd, 0, vb));
            double igMax = Math.Max(Math.Abs(mos.Ig(sg * vdd, sg * vdd, 0, vb)), Math.Abs(mos.Ig(0, sg * vdd, 0, vb)));
            double vth = mos.Vth;
            o.M("ion", "Ion = |Id(Vg=Vdd, Vd=Vdd, Vs=0)|", ion, "A");
            o.M("ioff", "Ioff = |Id(Vg=0, Vd=Vdd, Vs=0)|", ioff, "A");
            o.M("onOff", "on/off 비", ioff > 0 ? ion / ioff : null, "", ioff > 0 ? null : "Ioff = 0");
            o.M("igAbs", "|Ig| 최대", igMax, "A");
            o.M("vth", ds.Codes.Count == 0 ? "문턱 전압 Vth" : "문턱 전압 Vth(구조 실패, 참고값)", pmos ? Math.Abs(vth) : vth, "V");
            er.Vref = vdd;
            if (mode == "AutoJudge")
            {
                er.Applied = $"Ion: Vg=Vd={F(sg * vdd)} V, Vs=0 · Ioff: Vg=0, Vd={F(sg * vdd)} V · Vb {F(vb)} V";
                er.Vov = mos.Vov(sg * vdd, 0, vb);
            }
            else
            {
                int n = (int)r.N("sweepPoints");
                double a = r.N("sweepStart"), b = r.N("sweepStop"), vs = r.N("vs");
                bool vgSweep = mode == "IdVgSweep";
                er.SweepKind = mode;
                er.LogY = vgSweep;
                er.XLabel = vgSweep ? "Vg (V)" : "Vd (V)";
                er.YLabel = vgSweep ? "|Id| (A, 로그)" : "Id (A)";
                for (int i = 0; i < n; i++)
                {
                    t.ThrowIfCancellationRequested();
                    double x = a + (b - a) * i / (n - 1);
                    double id = vgSweep ? mos.Id(x, r.N("vd"), vs, vb) : mos.Id(r.N("vg"), x, vs, vb);
                    er.Sweep.Add((x, vgSweep ? Math.Abs(id) : id));
                }
                if (vgSweep && ds.Codes.Count == 0)
                {
                    int best = -1;
                    double gmMax = 0;
                    for (int i = 1; i < n - 1; i++)
                    {
                        double gm = (er.Sweep[i + 1].Y - er.Sweep[i - 1].Y) / (er.Sweep[i + 1].X - er.Sweep[i - 1].X);
                        if (gm > gmMax) { gmMax = gm; best = i; }
                    }
                    if (best > 0) er.VthExt = er.Sweep[best].X - er.Sweep[best].Y / gmMax;
                }
                er.Applied = vgSweep ? $"Vg {F(a)}→{F(b)} V · Vd {F(r.N("vd"))} V · Vs {F(vs)} V · Vb {F(vb)} V"
                                     : $"Vd {F(a)}→{F(b)} V · Vg {F(r.N("vg"))} V · Vs {F(vs)} V · Vb {F(vb)} V";
                er.Vov = vgSweep ? mos.Vov(b, vs, vb) : mos.Vov(r.N("vg"), vs, vb);
                if (er.VthExt is double ve) o.M("vthExt", "외삽 Vth(최대 gm점)", ve, "V");
            }
        }

        double eoxRef = er.Vref / (toxForModel * 1e-7) / 1e6;
        er.Extracted.Add(("tox", double.IsFinite(ds.ToxNm) ? $"{F(ds.ToxNm)} nm" : "N/A"));
        er.Extracted.Add(("Nb", $"{Fmt.S(ds.Nb, 4)} cm⁻³ ({ds.BodyType}형 바디)"));
        er.Extracted.Add(("φF / Vfb", $"{F(mos.PhiF)} V / {F(pmos ? -mos.Vfb : mos.Vfb)} V"));
        er.Extracted.Add(("Vth", $"{F(mos.Vth)} V"));
        er.Extracted.Add(("Cox / β", $"{Fmt.S(mos.Cox, 5)} F/m² / {Fmt.S(mos.Beta, 5)} A/V²"));
        er.Extracted.Add(("L / W", $"{(double.IsFinite(ds.LNm) ? F(ds.LNm) + " nm" : "N/A")} / {F(gate.LengthUm)} µm"));
        er.Extracted.Add(("Eox(기준 전압)", $"{F(eoxRef)} MV/cm"));
        o.M("tox", "게이트 산화막 tox", double.IsFinite(ds.ToxNm) ? ds.ToxNm : null, "nm", double.IsFinite(ds.ToxNm) ? null : "게이트 아래 절연막 없음");
        o.M("eox", "산화막 전계(기준 전압)", eoxRef, "MV/cm");

        // Verdict from the same criteria the pipeline evaluates.
        var crit = Criteria(r, c, o);
        var fails = new List<string>();
        foreach (var code in codes) fails.Add($"{code}: {DeviceStructure.MessagesKo[code]}");
        foreach (var cr in crit)
        {
            if (cr.Metric == "structureFailures") continue;
            var m = o.Metrics[cr.Metric];
            if (m.Value is not double v || !cr.Pass(v)) fails.Add($"{cr.Metric.ToUpperInvariant()}: {m.NameKo} {(m.Value is double vv ? Fmt.S(vv, 4) : "N/A")} {m.Unit} (기준 {cr.TargetText(m.Unit)})");
        }
        er.Normal = fails.Count == 0;
        er.Verdict = er.Normal ? $"[정상] {ds.DeviceType} 정상 동작" : "[비정상] " + string.Join(" / ", fails);
        if (ds.Codes.Count > 0) o.Warnings.Add("구조 판정 실패: 전류는 10.4의 실패 전기 모델로 계산했습니다.");
        return o;
    }

    public override List<Criterion> Criteria(Recipe r, Ctx c, Output o)
    {
        var list = new List<Criterion> { new("structureFailures", CK.Max, 0), new("igAbs", CK.Max, r.N("maxIg")) };
        if (r.E("testMode") == "SinglePoint") return list;
        list.Add(new("ion", CK.Min, r.N("minIon")));
        list.Add(new("ioff", CK.Max, r.N("maxIoff")));
        list.Add(new("onOff", CK.Min, r.N("minOnOff")));
        list.Add(new("vth", CK.Min, r.N("minVth")));
        list.Add(new("vth", CK.Max, r.N("maxVth")));
        return list;
    }
}
