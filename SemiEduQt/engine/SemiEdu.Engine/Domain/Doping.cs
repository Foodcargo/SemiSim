namespace SemiEdu.Engine;

/// <summary>Concentrations in cm⁻³ on a shared 1-D depth grid (nm below the Si surface).</summary>
public sealed class SpeciesProfile
{
    public string Species = "";
    public double[] Total = Array.Empty<double>();
    public double[] Active = Array.Empty<double>();
    public SpeciesProfile Clone() => new() { Species = Species, Total = (double[])Total.Clone(), Active = (double[])Active.Clone() };
}

/// <summary>Contiguous columns [Start, End) that share the same implant history.</summary>
public sealed class ColumnGroup
{
    public int Start, End;
    public List<SpeciesProfile> Species = new();
    public int Count => End - Start;

    public ColumnGroup Clone() => new() { Start = Start, End = End, Species = Species.Select(s => s.Clone()).ToList() };

    public SpeciesProfile Get(string species, int n)
    {
        var p = Species.FirstOrDefault(s => s.Species == species);
        if (p == null)
        {
            p = new SpeciesProfile { Species = species, Total = new double[n], Active = new double[n] };
            Species.Add(p);
        }
        return p;
    }
}

public sealed class DopingMap
{
    public double Dz;
    public int N;
    public List<ColumnGroup> Groups = new();

    public double ZMax => Dz * (N - 1);

    public DopingMap Clone() => new() { Dz = Dz, N = N, Groups = Groups.Select(g => g.Clone()).ToList() };

    public static bool IsDonor(string species) => species is "P" or "As";

    public ColumnGroup GroupOf(int col)
    {
        foreach (var g in Groups) if (col >= g.Start && col < g.End) return g;
        throw new InvalidOperationException($"column {col} not in doping map");
    }

    public IEnumerable<string> SpeciesPresent() =>
        Groups.SelectMany(g => g.Species).Select(s => s.Species).Distinct().OrderBy(s => s, StringComparer.Ordinal);

    /// <summary>Splits groups so that every group lies entirely inside or outside <paramref name="mask"/>.</summary>
    public void SplitBy(bool[] mask)
    {
        var result = new List<ColumnGroup>();
        foreach (var g in Groups)
        {
            int start = g.Start;
            for (int i = g.Start + 1; i <= g.End; i++)
            {
                if (i == g.End || mask[i] != mask[start])
                {
                    var part = g.Clone();
                    part.Start = start;
                    part.End = i;
                    result.Add(part);
                    start = i;
                }
            }
        }
        Groups = result;
    }

    /// <summary>Conservative regrid: linear interpolation, then rescale so the trapezoid integral of Total is preserved.</summary>
    public void Regrid(double newDz, int newN)
    {
        foreach (var g in Groups)
        {
            foreach (var sp in g.Species)
            {
                double oldInt = Trap(sp.Total, Dz);
                var t = new double[newN];
                var a = new double[newN];
                for (int i = 0; i < newN; i++)
                {
                    double z = i * newDz;
                    t[i] = Interp(sp.Total, Dz, z);
                    a[i] = Interp(sp.Active, Dz, z);
                }
                double newInt = Trap(t, newDz);
                double k = newInt > 0 ? oldInt / newInt : 1.0;
                for (int i = 0; i < newN; i++) { t[i] *= k; a[i] *= k; }
                sp.Total = t;
                sp.Active = a;
            }
        }
        Dz = newDz;
        N = newN;
    }

    /// <summary>Pads the grid with zeros to <paramref name="newN"/> cells (same Dz).</summary>
    public void Extend(int newN)
    {
        if (newN <= N) return;
        foreach (var g in Groups)
            foreach (var sp in g.Species)
            {
                Array.Resize(ref sp.Total, newN);
                Array.Resize(ref sp.Active, newN);
            }
        N = newN;
    }

    /// <summary>Trapezoid integral with step <paramref name="dz"/>.</summary>
    public static double Trap(double[] c, double dz)
    {
        int n = c.Length;
        if (n < 2) return 0;
        double s = 0.5 * (c[0] + c[n - 1]);
        for (int i = 1; i < n - 1; i++) s += c[i];
        return s * dz;
    }

    /// <summary>First moment ∫z·c dz / ∫c dz with trapezoid weights (nm).</summary>
    public static double MeanDepth(double[] c, double dz)
    {
        int n = c.Length;
        double num = 0, den = 0;
        for (int i = 0; i < n; i++)
        {
            double w = (i == 0 || i == n - 1) ? 0.5 : 1.0;
            num += w * i * dz * c[i];
            den += w * c[i];
        }
        return den > 0 ? num / den : double.NaN;
    }

    public static double Interp(double[] a, double dz, double z)
    {
        if (a.Length == 0 || z < 0) return 0;
        double x = z / dz;
        int i = (int)Math.Floor(x);
        if (i >= a.Length - 1) return i == a.Length - 1 && Math.Abs(x - i) < 1e-12 ? a[^1] : 0;
        double f = x - i;
        return a[i] * (1 - f) + a[i + 1] * f;
    }

    /// <summary>Net active concentration (donor positive) contributed by implants at depth z in a group.</summary>
    public double NetActiveAt(ColumnGroup g, double z)
    {
        double net = 0;
        foreach (var sp in g.Species)
        {
            double v = Interp(sp.Active, Dz, z);
            net += IsDonor(sp.Species) ? v : -v;
        }
        return net;
    }

    public double TotalAt(ColumnGroup g, double z)
    {
        double t = 0;
        foreach (var sp in g.Species) t += Interp(sp.Total, Dz, z);
        return t;
    }

    /// <summary>Deepest depth where any species' total exceeds 1e-6 of its peak (nm); 0 if none.</summary>
    public double SignificantDepth()
    {
        double deepest = 0;
        foreach (var g in Groups)
            foreach (var sp in g.Species)
            {
                double peak = sp.Total.Length == 0 ? 0 : sp.Total.Max();
                if (peak <= 0) continue;
                for (int i = sp.Total.Length - 1; i >= 0; i--)
                    if (sp.Total[i] > peak * 1e-6) { deepest = Math.Max(deepest, i * Dz); break; }
            }
        return deepest;
    }
}
