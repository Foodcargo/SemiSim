namespace SemiEdu.Engine;

/// <summary>Material ids (stored, English) and their educational display data.</summary>
public static class Mat
{
    public const string Si = "Si", SiO2 = "SiO2", Si3N4 = "Si3N4", PR = "PR", Al = "Al", TiN = "TiN", Cu = "Cu", W = "W";

    public static bool IsMetal(string m) => m is Al or TiN or Cu or W;
    public static bool IsInsulator(string m) => m is SiO2 or Si3N4 or PR;

    public static (float R, float G, float B) Rgb(string m) => m switch
    {
        Si => (0.52f, 0.56f, 0.64f),
        SiO2 => (0.50f, 0.80f, 0.95f),
        Si3N4 => (0.90f, 0.78f, 0.42f),
        PR => (0.88f, 0.36f, 0.58f),
        Al => (0.82f, 0.83f, 0.86f),
        TiN => (0.78f, 0.62f, 0.24f),
        Cu => (0.86f, 0.50f, 0.26f),
        W => (0.42f, 0.44f, 0.50f),
        _ => (1f, 0f, 1f),
    };

    public static string Ko(string m) => m switch
    {
        Si => "실리콘",
        SiO2 => "산화막",
        Si3N4 => "질화막",
        PR => "감광막(PR)",
        Al => "알루미늄",
        TiN => "질화티타늄",
        Cu => "구리",
        W => "텅스텐",
        _ => m,
    };

    /// <summary>Educational resistivity constants (Ω·m), not corrected for size or temperature.</summary>
    public static double Rho(string m) => m switch { Al => 2.8e-8, Cu => 1.7e-8, W => 5.6e-8, _ => double.NaN };

    /// <summary>Educational gate work functions (eV).</summary>
    public static double WorkFunction(string m) => m switch { Al => 4.10, W => 4.55, TiN => 4.60, Cu => 4.65, _ => double.NaN };
}

public sealed class Seg
{
    public string Material = "";
    public double Thickness;
    public string LayerId = "";
    public Seg Clone() => new() { Material = Material, Thickness = Thickness, LayerId = LayerId };
}

/// <summary>One cross-section column: Si surface height (nm, relative to the original Si surface) and films bottom → top.</summary>
public sealed class Column
{
    public double SiTop;
    public List<Seg> Segs = new();

    public double Film
    {
        get
        {
            double t = 0;
            foreach (var s in Segs) t += s.Thickness;
            return t;
        }
    }

    public double Surface => SiTop + Film;
    public string TopMaterial => Segs.Count == 0 ? Mat.Si : Segs[^1].Material;

    public double Total(string material)
    {
        double t = 0;
        foreach (var s in Segs) if (s.Material == material) t += s.Thickness;
        return t;
    }

    public double LayerThickness(string layerId)
    {
        double t = 0;
        foreach (var s in Segs) if (s.LayerId == layerId) t += s.Thickness;
        return t;
    }

    public Column Clone()
    {
        var c = new Column { SiTop = SiTop, Segs = new List<Seg>(Segs.Count) };
        foreach (var s in Segs) c.Segs.Add(s.Clone());
        return c;
    }

    public bool SameStack(Column o, double tol = 1e-9)
    {
        if (Math.Abs(SiTop - o.SiTop) > tol || Segs.Count != o.Segs.Count) return false;
        for (int i = 0; i < Segs.Count; i++)
        {
            if (Segs[i].Material != o.Segs[i].Material || Segs[i].LayerId != o.Segs[i].LayerId) return false;
            if (Math.Abs(Segs[i].Thickness - o.Segs[i].Thickness) > tol) return false;
        }
        return true;
    }
}

public sealed record LayerInfo(string LayerId, string Material, string Origin);

public sealed class ResistPattern
{
    public string PatternId = "";
    public bool[] Open = Array.Empty<bool>();
    public double OpeningCdNm;
    public string MaskId = "";
    public ResistPattern Clone() => new() { PatternId = PatternId, Open = (bool[])Open.Clone(), OpeningCdNm = OpeningCdNm, MaskId = MaskId };
}

public sealed class Wire
{
    public string WireId = "";
    public string Material = "";
    public string Role = "Interconnect";
    public string MaskId = "None";
    public int ColStart, ColEnd;
    public double WidthNm, ThicknessNm, LengthUm, OffsetUm;
    public string LayerId = "";
    public double ResistanceOhm => Mat.Rho(Material) * (LengthUm * 1e-6) / (WidthNm * 1e-9 * ThicknessNm * 1e-9);
    public Wire Clone() => (Wire)MemberwiseClone();
}

public sealed class ContactRec
{
    public string ContactId = "";
    public string Role = "";
    public string Metal = "";
    public int ColStart, ColEnd;
    public string LayerId = "";
    public ContactRec Clone() => (ContactRec)MemberwiseClone();
}

public sealed class WaferState
{
    public Guid StateId = Guid.NewGuid();
    public Guid? ParentStateId;
    public long Revision;

    public int DiameterMm = 200;
    public double SubstrateThicknessUm = 725;
    public string Orientation = "(100)";
    public string DopingType = "P-type";
    public double BackgroundDoping = 1e15;
    public double DomainWidthUm = 20;

    public Column[] Columns = Array.Empty<Column>();
    public double Contamination = 50;
    public List<LayerInfo> Layers = new();
    public ResistPattern? Resist;
    public List<Wire> Wires = new();
    public List<ContactRec> Contacts = new();
    public DopingMap? Doping;
    public int Counter;
    public bool LastRunFailed;
    public string CreatedBy = "init";

    public int N => Columns.Length;
    public double ColWidthNm => DomainWidthUm * 1000.0 / N;
    public double CenterUm(int i) => (i + 0.5) * DomainWidthUm / N;

    /// <summary>Background net doping (cm⁻³): donors positive, acceptors negative. Fully active by definition.</summary>
    public double BackgroundNet => DopingType == "N-type" ? BackgroundDoping : -BackgroundDoping;

    public bool HasPR => Columns.Any(c => c.Segs.Any(s => s.Material == Mat.PR));
    public bool HasMetalSeg => Columns.Any(c => c.Segs.Any(s => Mat.IsMetal(s.Material)));
    public bool HasDoping => Doping != null && Doping.Groups.Any(g => g.Species.Count > 0);

    public bool IsUniform()
    {
        for (int i = 1; i < Columns.Length; i++)
            if (!Columns[i].SameStack(Columns[0])) return false;
        return true;
    }

    /// <summary>Columns whose centre lies in [startUm, endUm] (inclusive). Returns [a, b).</summary>
    public (int A, int B) ColumnsIn(double startUm, double endUm)
    {
        int a = -1, b = -1;
        for (int i = 0; i < N; i++)
        {
            double x = CenterUm(i);
            if (x >= startUm - 1e-9 && x <= endUm + 1e-9)
            {
                if (a < 0) a = i;
                b = i + 1;
            }
        }
        return a < 0 ? (0, 0) : (a, b);
    }

    public string NewLayer(string material, string origin)
    {
        Counter++;
        var id = $"L{Counter}-{material}";
        Layers.Add(new LayerInfo(id, material, origin));
        return id;
    }

    public string NewId(string prefix)
    {
        Counter++;
        return $"{prefix}{Counter}";
    }

    public void Normalize()
    {
        foreach (var c in Columns) c.Segs.RemoveAll(s => s.Thickness <= 1e-9);
        if (Resist != null && !HasPR) Resist = null;
    }

    public IEnumerable<string> PresentLayerIds()
    {
        var seen = new HashSet<string>();
        foreach (var l in Layers)
            if (Columns.Any(c => c.Segs.Any(s => s.LayerId == l.LayerId)) && seen.Add(l.LayerId))
                yield return l.LayerId;
    }

    public string MaterialOfLayer(string layerId) => Layers.FirstOrDefault(l => l.LayerId == layerId)?.Material ?? "";

    public WaferState DeepClone()
    {
        var c = (WaferState)MemberwiseClone();
        c.Columns = Columns.Select(x => x.Clone()).ToArray();
        c.Layers = new List<LayerInfo>(Layers);
        c.Resist = Resist?.Clone();
        c.Wires = Wires.Select(w => w.Clone()).ToList();
        c.Contacts = Contacts.Select(x => x.Clone()).ToList();
        c.Doping = Doping?.Clone();
        return c;
    }

    public WaferState CloneAsChild()
    {
        var c = DeepClone();
        c.StateId = Guid.NewGuid();
        c.ParentStateId = StateId;
        c.Revision = Revision + 1;
        c.LastRunFailed = false;
        return c;
    }
}

public sealed class Mask
{
    public string MaskId = "";
    public string MaskType = "Binary";
    public string MaskLayer = "Generic";
    public string Tone = "ClearIsLine";
    public bool Pellicle = true;
    public double DesignClear, Pitch, OffsetUm, WriteDose, BeamSize;
    public int RepeatCount = 1;
    public double MaskCd, Bias, AbsorberResidual, EdgeBlur;
    public bool Passed;

    /// <summary>Clear (transparent) intervals in wafer coordinates (nm).</summary>
    public IEnumerable<(double Lo, double Hi)> ClearIntervals(double domainNm)
    {
        for (int k = 0; k < RepeatCount; k++)
        {
            double start = OffsetUm * 1000 + k * Pitch;
            if (start >= domainNm) yield break;
            yield return (start, Math.Min(domainNm, start + MaskCd));
        }
    }
}
