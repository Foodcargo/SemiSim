using System.Globalization;

namespace SemiEdu.Engine;

public enum Kind { Enum, Double, Integer }
public enum Role { ModelInput, QualityTarget, Metadata }

public sealed class Equipment
{
    public bool DoorClosed = true;
    public bool EmergencyStop;
    public bool Available = true;
    public bool ContinueOnFailure;
}

/// <summary>Active, validated inputs only. Inactive fields are never present.</summary>
public sealed class Recipe
{
    public readonly Dictionary<string, string> Enums = new();
    public readonly Dictionary<string, double> Nums = new();
    public string E(string id) => Enums.TryGetValue(id, out var v) ? v : "";
    public double N(string id) => Nums[id];
    public double N(string id, double fallback) => Nums.TryGetValue(id, out var v) ? v : fallback;
    public bool Has(string id) => Enums.ContainsKey(id) || Nums.ContainsKey(id);
}

public sealed class Ctx
{
    public required Session Session { get; init; }
    public required WaferState State { get; init; }
    public required Equipment Eq { get; init; }
    public readonly Dictionary<string, object> Cache = new();
}

public sealed class FormCtx
{
    public required Recipe R { get; init; }
    public required Ctx C { get; init; }
    public string E(string id) => R.E(id);
}

public sealed class ParamDef
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public string Unit { get; init; } = "";
    public Kind Kind { get; init; }
    public Role Role { get; init; } = Role.ModelInput;
    public Func<FormCtx, string[]>? Options { get; init; }
    public Func<FormCtx, string?>? EnumDefault { get; init; }
    public Func<FormCtx, (double Min, double Max, double Def)>? Range { get; init; }
    public Func<FormCtx, bool>? When { get; init; }
    public Func<FormCtx, string?>? Info { get; init; }
    public Func<FormCtx, bool>? Locked { get; init; }
    /// <summary>Stale/missing option is a precondition failure (state-derived ids), not an input error.</summary>
    public bool OptionErrorIsPrecondition { get; init; }
    public string Note { get; init; } = "";
}

/// <summary>Structured error. Stage is one of Input, Precondition, Interlock, Domain, Calculation, Invariant, Quality, Commit.</summary>
public sealed record Err(string Code, string Stage, string? ParamId, string? Actual, string? Expected, string MessageKo, string SuggestedAction);

public sealed record Metric(string NameKo, double? Value, string Unit, string? NaReason = null);

public enum CK { RelTarget, AbsTarget, Max, Min, Present }

/// <summary>Quality criterion. Boundaries are inclusive with epsilon 1e-9·max(1,|actual|,|boundary|).</summary>
public sealed record Criterion(string Metric, CK Kind, double Target, double Tol = 0)
{
    public static double Eps(double a, double b) => 1e-9 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));

    public (double Lo, double Hi) Bounds() => Kind switch
    {
        CK.RelTarget => (Target - Target * Tol / 100, Target + Target * Tol / 100),
        CK.AbsTarget => (Target - Tol, Target + Tol),
        CK.Max => (double.NegativeInfinity, Target),
        CK.Min => (Target, double.PositiveInfinity),
        _ => (0, double.PositiveInfinity),
    };

    public bool Pass(double a)
    {
        if (!double.IsFinite(a)) return false;
        if (Kind == CK.Present) return a > 0;
        var (lo, hi) = Bounds();
        bool okLo = double.IsNegativeInfinity(lo) || a >= lo - Eps(a, lo);
        bool okHi = double.IsPositiveInfinity(hi) || a <= hi + Eps(a, hi);
        return okLo && okHi;
    }

    public string TargetText(string unit) => Kind switch
    {
        CK.RelTarget => $"{Fmt.S(Target)} {unit} ±{Fmt.S(Tol)}%",
        CK.AbsTarget => $"{Fmt.S(Target)} {unit} ±{Fmt.S(Tol)} {unit}",
        CK.Max => $"≤ {Fmt.S(Target)} {unit}",
        CK.Min => $"≥ {Fmt.S(Target)} {unit}",
        _ => "존재(> 0)",
    };

    public string RangeText(string unit)
    {
        if (Kind == CK.Present) return "> 0";
        var (lo, hi) = Bounds();
        string l = double.IsNegativeInfinity(lo) ? "−∞" : Fmt.S(lo);
        string h = double.IsPositiveInfinity(hi) ? "∞" : Fmt.S(hi);
        return $"[{l}, {h}] {unit}".Trim();
    }
}

public sealed class Output
{
    public WaferState? Candidate;
    public readonly Dictionary<string, Metric> Metrics = new();
    public readonly List<string> MetricOrder = new();
    public readonly List<string> Warnings = new();
    public readonly List<Err> InvariantErrors = new();
    public Mask? NewMask;
    public ElectricalResult? Electrical;

    public void M(string id, string nameKo, double? value, string unit, string? naReason = null)
    {
        if (!Metrics.ContainsKey(id)) MetricOrder.Add(id);
        Metrics[id] = new Metric(nameKo, value, unit, naReason);
    }
}

public abstract class ProcessBase
{
    public abstract string Id { get; }
    public abstract string Group { get; }
    public abstract string Name { get; }
    public virtual bool Enabled => true;
    public virtual string DisabledReason => "";
    public virtual bool ChangesState => true;
    /// <summary>Initialization and mask making may run after a failed process.</summary>
    public virtual bool AllowAfterFailure => false;
    public virtual string EquipmentId => "";
    public abstract IReadOnlyList<ParamDef> Params { get; }
    public virtual string[] Unsupported => Array.Empty<string>();

    public virtual void Cross(Recipe r, Ctx c, List<Err> e) { }
    public virtual void Preconditions(Recipe r, Ctx c, List<Err> e) { }
    public virtual void Interlock(Recipe r, Ctx c, List<Err> e) { }
    public virtual void Domain(Recipe r, Ctx c, List<Err> e) { }
    public abstract Output Calculate(Recipe r, Ctx c, CancellationToken t);
    public abstract List<Criterion> Criteria(Recipe r, Ctx c, Output o);

    // ---- definition helpers ----
    protected static ParamDef En(string id, string label, params string[] options) =>
        new() { Id = id, Label = label, Kind = Kind.Enum, Options = _ => options };

    protected static ParamDef EnF(string id, string label, Func<FormCtx, string[]> options, Func<FormCtx, bool>? when = null, string note = "") =>
        new() { Id = id, Label = label, Kind = Kind.Enum, Options = options, When = when, Note = note };

    protected static ParamDef Num(string id, string label, string unit, double min, double max, double def,
        Func<FormCtx, bool>? when = null, bool integer = false, Role role = Role.ModelInput, string note = "") =>
        new() { Id = id, Label = label, Unit = unit, Kind = integer ? Kind.Integer : Kind.Double, Range = _ => (min, max, def), When = when, Role = role, Note = note };

    protected static ParamDef NumF(string id, string label, string unit, Func<FormCtx, (double, double, double)> range,
        Func<FormCtx, bool>? when = null, bool integer = false, Role role = Role.ModelInput) =>
        new() { Id = id, Label = label, Unit = unit, Kind = integer ? Kind.Integer : Kind.Double, Range = f => range(f), When = when, Role = role };

    protected static ParamDef Q(string id, string label, string unit, double min, double max, double def, Func<FormCtx, bool>? when = null) =>
        Num(id, label, unit, min, max, def, when, role: Role.QualityTarget);

    protected static ParamDef QF(string id, string label, string unit, Func<FormCtx, (double, double, double)> range, Func<FormCtx, bool>? when = null) =>
        NumF(id, label, unit, range, when, role: Role.QualityTarget);

    protected static ParamDef Info(string id, string label, Func<FormCtx, string?> text, Func<FormCtx, bool>? when = null) =>
        new() { Id = id, Label = label, Kind = Kind.Double, Role = Role.Metadata, Info = text, When = when };

    protected static void Pre(List<Err> e, string code, string msg, string action) =>
        e.Add(new Err(code, "Precondition", null, null, null, msg, action));

    protected static void InputErr(List<Err> e, string code, string param, string actual, string expected, string msg) =>
        e.Add(new Err(code, "Input", param, actual, expected, msg, "입력값을 고치세요."));

    protected static void Ilk(List<Err> e, string code, string param, double actual, string expected, string msg) =>
        e.Add(new Err(code, "Interlock", param, Fmt.S(actual), expected, msg, "교육용 장비 한계 안으로 조건을 조정하세요."));

    protected static void Dom(List<Err> e, string code, string msg, string action) =>
        e.Add(new Err(code, "Domain", null, null, null, msg, action));

    protected static string F(double v) => Fmt.S(v);
}

public static class Fmt
{
    static readonly CultureInfo I = CultureInfo.InvariantCulture;

    /// <summary>Round-trippable short form for default/input values (e.g. 1e15, 0.05, 725).</summary>
    public static string N(double v)
    {
        if (v == 0) return "0";
        double a = Math.Abs(v);
        if (a >= 1e5 || a < 1e-3) return v.ToString("0.##########e0", I);
        return v.ToString("0.##########", I);
    }

    /// <summary>Display form with <paramref name="sig"/> significant digits.</summary>
    public static string S(double v, int sig = 7)
    {
        if (double.IsNaN(v)) return "N/A";
        if (double.IsPositiveInfinity(v)) return "∞";
        if (double.IsNegativeInfinity(v)) return "−∞";
        if (v == 0) return "0";
        double a = Math.Abs(v);
        if (a >= 1e6 || a < 1e-3)
        {
            var s = v.ToString("E" + (sig - 1), I);
            int e = s.IndexOf('E');
            var mant = s[..e].TrimEnd('0').TrimEnd('.');
            int exp = int.Parse(s[(e + 1)..], I);
            return $"{mant}e{exp}";
        }
        return Math.Round(v, Math.Max(0, sig - 1 - (int)Math.Floor(Math.Log10(a)))).ToString("0.##########", I);
    }
}

/// <summary>Explicit unit conversions to SI (spec 3).</summary>
public static class Units
{
    public const double TorrToPa = 133.322368;
    public const double MilliTorrToPa = 0.133322368;
    public static double Torr(double v) => v * TorrToPa;
    public static double MilliTorr(double v) => v * MilliTorrToPa;
    public static double Nm(double v) => v * 1e-9;
    public static double Um(double v) => v * 1e-6;
    public static double Min(double v) => v * 60;
    public static double CelsiusToK(double v) => v + 273.15;
    public static double PerCm3(double v) => v * 1e6;
    public static double PerCm2(double v) => v * 1e4;
    public static double KeV(double v) => v * 1.602176634e-16;
    public static double RpmToRadPerS(double v) => v * 2 * Math.PI / 60;
}
