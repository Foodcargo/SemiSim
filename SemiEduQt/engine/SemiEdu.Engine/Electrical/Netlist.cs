namespace SemiEdu.Engine;

public enum Cls { Insulator, Metal, NPlus, PPlus, NSemi, PSemi, Intrinsic }

public static class Classes
{
    public const double ConductiveThreshold = 1e19, SemiThreshold = 1e14;

    public static Cls OfNet(double net)
    {
        double a = Math.Abs(net);
        if (a >= ConductiveThreshold) return net > 0 ? Cls.NPlus : Cls.PPlus;
        if (a >= SemiThreshold) return net > 0 ? Cls.NSemi : Cls.PSemi;
        return Cls.Intrinsic;
    }

    public static bool IsConductiveSi(Cls c) => c is Cls.NPlus or Cls.PPlus;

    public static string Ko(Cls c) => c switch
    {
        Cls.Metal => "도체",
        Cls.NPlus => "도체성(N+)",
        Cls.PPlus => "도체성(P+)",
        Cls.NSemi => "반도체(n형)",
        Cls.PSemi => "반도체(p형)",
        Cls.Intrinsic => "반도체(진성에 가까움)",
        _ => "절연체",
    };

    public static (float R, float G, float B, float A) Color(Cls c) => c switch
    {
        Cls.Metal => (0.95f, 0.78f, 0.20f, 1f),
        Cls.NPlus => (0.95f, 0.45f, 0.20f, 1f),
        Cls.PPlus => (0.62f, 0.40f, 0.90f, 1f),
        Cls.NSemi => (0.55f, 0.80f, 0.45f, 1f),
        Cls.PSemi => (0.55f, 0.60f, 0.78f, 1f),
        Cls.Intrinsic => (0.80f, 0.80f, 0.80f, 1f),
        _ => (0.60f, 0.60f, 0.60f, 0.35f),
    };
}

/// <summary>Net active doping on a column × depth grid below each column's Si surface.</summary>
public sealed class SiGrid
{
    public int Cols, Depth;
    public double Dz;
    public double[] Net = Array.Empty<double>();
    public double[] Total = Array.Empty<double>();

    public double NetAt(int col, int d) => Net[col * Depth + d];
    public Cls ClassAt(int col, int d) => Classes.OfNet(Net[col * Depth + d]);

    public static SiGrid Build(WaferState s)
    {
        double dz = s.Doping?.Dz ?? 10;
        double depthNm = Math.Max(s.Doping?.ZMax ?? 0, 500);
        int depth = (int)Math.Ceiling(depthNm / dz) + 1;
        if (depth > 4096)
        {
            depth = 4096;
            dz = depthNm / (depth - 1);
        }
        var g = new SiGrid { Cols = s.N, Depth = depth, Dz = dz, Net = new double[s.N * depth], Total = new double[s.N * depth] };
        double bg = s.BackgroundNet;
        var groups = s.Doping?.Groups ?? new List<ColumnGroup> { new() { Start = 0, End = s.N } };
        foreach (var grp in groups)
        {
            var net = new double[depth];
            var tot = new double[depth];
            for (int d = 0; d < depth; d++)
            {
                double z = d * dz;
                net[d] = bg + (s.Doping?.NetActiveAt(grp, z) ?? 0);
                tot[d] = s.Doping?.TotalAt(grp, z) ?? 0;
            }
            for (int c = grp.Start; c < grp.End; c++)
            {
                Array.Copy(net, 0, g.Net, c * depth, depth);
                Array.Copy(tot, 0, g.Total, c * depth, depth);
            }
        }
        return g;
    }

    /// <summary>Junction depth: first sign change of net doping in a column (linear interpolation), NaN if none.</summary>
    public double JunctionDepth(int col)
    {
        for (int d = 1; d < Depth; d++)
        {
            double a = NetAt(col, d - 1), b = NetAt(col, d);
            if (a == 0 || Math.Sign(a) == Math.Sign(b)) continue;
            return (d - 1 + a / (a - b)) * Dz;
        }
        return double.NaN;
    }
}

public sealed class NetInfo
{
    public string Id = "";
    public int Root;
    public bool HasMetal, HasNPlus, HasPPlus, HasNSemi, HasPSemi;
    public int Cells;
    public int ColMin = int.MaxValue, ColMax = -1;
    public readonly SortedSet<int> ConductiveSiCols = new();
    public readonly HashSet<string> Objects = new();
    public double MinInsulatorBelow = double.PositiveInfinity;

    public string Kind => HasMetal ? "Metal" : HasNPlus ? "N+" : HasPPlus ? "P+" : HasNSemi ? "n-body" : HasPSemi ? "p-body" : "intrinsic";
}

/// <summary>
/// Union-Find over film segments (one cell each) and Si depth cells. Conductors and conductive Si (N+/P+) join a
/// conductor net; metal joins only the directly adjacent N+/P+ surface cell.
/// Semiconductor cells are grouped by type into body regions. N+ and P+ never merge (PN junction).
/// </summary>
public sealed class Netlist
{
    public readonly WaferState State;
    public readonly SiGrid Grid;
    readonly int[] parent;
    readonly int siCount;
    readonly int[] filmBase;
    readonly (double Lo, double Hi)[][] segZ;
    public readonly Dictionary<int, NetInfo> Nets = new();
    public readonly List<NetInfo> Ordered = new();
    public readonly List<string> Junctions = new();

    public Netlist(WaferState s)
    {
        State = s;
        Grid = SiGrid.Build(s);
        siCount = s.N * Grid.Depth;
        filmBase = new int[s.N + 1];
        segZ = new (double, double)[s.N][];
        for (int c = 0; c < s.N; c++)
        {
            filmBase[c + 1] = filmBase[c] + s.Columns[c].Segs.Count;
            var z = new (double, double)[s.Columns[c].Segs.Count];
            double h = s.Columns[c].SiTop;
            for (int k = 0; k < z.Length; k++)
            {
                double t = s.Columns[c].Segs[k].Thickness;
                z[k] = (h, h + t);
                h += t;
            }
            segZ[c] = z;
        }
        parent = new int[siCount + filmBase[s.N]];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        Build();
    }

    public int SiNode(int col, int d) => col * Grid.Depth + d;
    public int FilmNode(int col, int seg) => siCount + filmBase[col] + seg;

    public int Find(int x)
    {
        while (parent[x] != x)
        {
            parent[x] = parent[parent[x]];
            x = parent[x];
        }
        return x;
    }

    void Union(int a, int b)
    {
        a = Find(a);
        b = Find(b);
        if (a == b) return;
        if (a < b) parent[b] = a; else parent[a] = b;
    }

    static bool SiJoin(Cls a, Cls b) => a == b && a is Cls.NPlus or Cls.PPlus or Cls.NSemi or Cls.PSemi;

    bool IsMetal(int col, int seg) => Mat.IsMetal(State.Columns[col].Segs[seg].Material);

    void Build()
    {
        var s = State;
        int D = Grid.Depth;
        for (int c = 0; c < s.N; c++)
            for (int d = 0; d < D; d++)
            {
                var k = Grid.ClassAt(c, d);
                if (d + 1 < D && SiJoin(k, Grid.ClassAt(c, d + 1))) Union(SiNode(c, d), SiNode(c, d + 1));
                if (c + 1 < s.N && SiJoin(k, Grid.ClassAt(c + 1, d))) Union(SiNode(c, d), SiNode(c + 1, d));
                if (d + 1 < D && c + 1 >= 0)
                {
                    var below = Grid.ClassAt(c, d + 1);
                    if (k == Cls.NPlus && below == Cls.PPlus || k == Cls.PPlus && below == Cls.NPlus) Junctions.Add($"PN 접합 col {c} z≈{Fmt.S((d + 0.5) * Grid.Dz, 4)} nm");
                }
            }

        for (int c = 0; c < s.N; c++)
        {
            var segs = s.Columns[c].Segs;
            for (int k = 0; k < segs.Count; k++)
            {
                if (!IsMetal(c, k)) continue;
                if (k + 1 < segs.Count && IsMetal(c, k + 1)) Union(FilmNode(c, k), FilmNode(c, k + 1));
                if (k == 0)
                {
                    int hit = OhmicCell(c);
                    if (hit >= 0) Union(FilmNode(c, 0), SiNode(c, hit));
                }
                if (c + 1 < s.N)
                {
                    var next = s.Columns[c + 1].Segs;
                    for (int j = 0; j < next.Count; j++)
                    {
                        if (!IsMetal(c + 1, j)) continue;
                        var (lo, hi) = segZ[c][k];
                        var (lo2, hi2) = segZ[c + 1][j];
                        if (Math.Min(hi, hi2) - Math.Max(lo, lo2) > 1e-9) Union(FilmNode(c, k), FilmNode(c + 1, j));
                    }
                }
            }
        }

        // Deterministic net ids: film nodes column-major, then Si cells.
        void Visit(int node, int col, Cls cls, string obj, double insulBelow)
        {
            int r = Find(node);
            if (!Nets.TryGetValue(r, out var n))
            {
                n = new NetInfo { Root = r, Id = $"NET{Nets.Count + 1}" };
                Nets[r] = n;
                Ordered.Add(n);
            }
            n.Cells++;
            n.ColMin = Math.Min(n.ColMin, col);
            n.ColMax = Math.Max(n.ColMax, col);
            switch (cls)
            {
                case Cls.Metal: n.HasMetal = true; n.MinInsulatorBelow = Math.Min(n.MinInsulatorBelow, insulBelow); break;
                case Cls.NPlus: n.HasNPlus = true; n.ConductiveSiCols.Add(col); break;
                case Cls.PPlus: n.HasPPlus = true; n.ConductiveSiCols.Add(col); break;
                case Cls.NSemi: n.HasNSemi = true; break;
                case Cls.PSemi: n.HasPSemi = true; break;
            }
            if (obj.Length > 0) n.Objects.Add(obj);
        }

        for (int c = 0; c < s.N; c++)
        {
            double insul = 0;
            var segs = s.Columns[c].Segs;
            for (int k = 0; k < segs.Count; k++)
            {
                if (IsMetal(c, k)) Visit(FilmNode(c, k), c, Cls.Metal, segs[k].LayerId, insul);
                else insul += segs[k].Thickness;
            }
        }
        for (int c = 0; c < s.N; c++)
            for (int d = 0; d < D; d++)
            {
                var k = Grid.ClassAt(c, d);
                if (k == Cls.Intrinsic) continue;
                Visit(SiNode(c, d), c, k, "", 0);
            }
    }

    /// <summary>Only the directly adjacent conductive Si surface cell can join a metal net (spec 9).</summary>
    int OhmicCell(int c)
    {
        return Classes.IsConductiveSi(Grid.ClassAt(c, 0)) ? 0 : -1;
    }

    public NetInfo? NetOfFilm(int col, int seg) => Nets.TryGetValue(Find(FilmNode(col, seg)), out var n) ? n : null;
    public NetInfo? NetOfSi(int col, int d) => Nets.TryGetValue(Find(SiNode(col, d)), out var n) ? n : null;

    public int ConductorNetCount => Ordered.Count(n => n.HasMetal || n.HasNPlus || n.HasPPlus);
    public long CellSum => Ordered.Sum(n => (long)n.Cells);
}

public sealed record ObjectRow(string ObjectId, string Material, string Cls, string Reason, string Range, string Net);

public static class ObjectTable
{
    public static List<ObjectRow> Build(Netlist nl)
    {
        var s = nl.State;
        var rows = new List<ObjectRow>();
        double cw = s.DomainWidthUm / s.N;
        string Rng(int a, int b) => $"{Fmt.S(a * cw, 4)}–{Fmt.S(b * cw, 4)} µm";

        foreach (var layerId in s.PresentLayerIds())
        {
            string mat = s.MaterialOfLayer(layerId);
            int run = 0;
            for (int c = 0; c < s.N;)
            {
                int k = s.Columns[c].Segs.FindIndex(g => g.LayerId == layerId);
                if (k < 0) { c++; continue; }
                int a = c;
                while (c < s.N && s.Columns[c].Segs.Any(g => g.LayerId == layerId)) c++;
                run++;
                bool metal = Mat.IsMetal(mat);
                string reason = metal
                    ? (double.IsNaN(Mat.Rho(mat)) ? $"금속 {mat}" : $"금속 {mat}, ρ={Fmt.S(Mat.Rho(mat), 3)} Ω·m")
                    : $"{Mat.Ko(mat)}, 절연체";
                string net = metal ? nl.NetOfFilm(a, k)?.Id ?? "—" : "—";
                rows.Add(new ObjectRow($"{layerId}#{run}", mat, Classes.Ko(metal ? Cls.Metal : Cls.Insulator), reason, Rng(a, c), net));
            }
        }

        var groups = s.Doping?.Groups ?? new List<ColumnGroup> { new() { Start = 0, End = s.N } };
        int gi = 0;
        foreach (var g in groups)
        {
            gi++;
            int c = g.Start;
            double surfNet = nl.Grid.NetAt(c, 0);
            double peakNet = 0, peakTot = 0;
            for (int d = 0; d < nl.Grid.Depth; d++)
            {
                double v = nl.Grid.NetAt(c, d);
                if (Math.Abs(v) > Math.Abs(peakNet)) peakNet = v;
                peakTot = Math.Max(peakTot, nl.Grid.Total[c * nl.Grid.Depth + d]);
            }
            var cls = Classes.OfNet(peakNet);
            string type = peakNet >= 0 ? "N" : "P";
            string reason = Classes.IsConductiveSi(cls)
                ? $"{type}+ 활성 {Fmt.S(Math.Abs(peakNet), 3)} cm⁻³ ≥ 1e19"
                : $"{(peakNet >= 0 ? "n" : "p")}형 순활성 피크 {Fmt.S(Math.Abs(peakNet), 3)} cm⁻³";
            if (!Classes.IsConductiveSi(cls) && peakTot >= Classes.ConductiveThreshold)
                reason += $" · 열처리 전, 활성 부족(전체 {Fmt.S(peakTot, 3)} cm⁻³)";
            double xj = nl.Grid.JunctionDepth(c);
            reason += double.IsNaN(xj) ? " · Xj N/A" : $" · Xj {Fmt.S(xj, 4)} nm";
            int peakD = 0;
            for (int d = 0; d < nl.Grid.Depth; d++) if (nl.Grid.NetAt(c, d) == peakNet) { peakD = d; break; }
            rows.Add(new ObjectRow($"SI-G{gi}", Mat.Si, Classes.Ko(cls), reason, Rng(g.Start, g.End), nl.NetOfSi(c, peakD)?.Id ?? "—"));
            _ = surfNet;
        }
        double bg = s.BackgroundNet;
        rows.Add(new ObjectRow("SUB", Mat.Si, Classes.Ko(Classes.OfNet(bg)),
            $"{(bg >= 0 ? "n" : "p")}형 배경 {Fmt.S(Math.Abs(bg), 3)} cm⁻³ (기판 바디)", Rng(0, s.N), nl.NetOfSi(0, nl.Grid.Depth - 1)?.Id ?? "—"));
        return rows;
    }
}
