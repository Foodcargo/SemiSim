using System.Globalization;

namespace SemiEdu.Engine;

public sealed class FieldView
{
    public required ParamDef Def { get; init; }
    public string Text = "";
    public string[] Options = Array.Empty<string>();
    public int Index = -1;
    public string Hint = "";
    public string Error = "";
    public bool ReadOnly;
}

public sealed class Evaluation
{
    public readonly Recipe Recipe = new();
    public readonly List<FieldView> Fields = new();
    public readonly List<Err> Errors = new();
    public readonly List<string> InactiveProvided = new();
}

/// <summary>Single validator used by both se_form (strict = false) and se_run (strict = true).</summary>
public static class Validator
{
    public const string Placeholder = "— 다시 선택 —";

    public static Evaluation Evaluate(ProcessBase p, IReadOnlyDictionary<string, string> raw, Ctx c, bool strict)
    {
        var ev = new Evaluation();
        var fc = new FormCtx { R = ev.Recipe, C = c };
        var ids = new HashSet<string>(p.Params.Select(d => d.Id));
        foreach (var k in raw.Keys)
            if (!ids.Contains(k))
                ev.Errors.Add(new Err("UNKNOWN_PARAMETER", "Input", k, raw[k], null, $"알 수 없는 입력 항목 '{k}'입니다.", "정의된 입력 항목만 보내세요."));

        foreach (var d in p.Params)
        {
            bool active = d.When?.Invoke(fc) ?? true;
            raw.TryGetValue(d.Id, out var given);
            if (!active)
            {
                if (given != null)
                {
                    ev.InactiveProvided.Add(d.Id);
                    if (strict)
                        ev.Errors.Add(new Err("INAPPLICABLE_PARAMETER", "Input", d.Id, given, "현재 조건에서 비활성",
                            $"'{d.Label}'은(는) 현재 조건에서 사용하지 않는 항목입니다.", "실행 레시피에서 이 항목을 빼세요."));
                }
                continue;
            }
            if (d.Info != null)
            {
                var t = d.Info(fc);
                if (t != null) ev.Fields.Add(new FieldView { Def = d, Text = t, ReadOnly = true });
                continue;
            }
            if (d.Kind == Kind.Enum) EvalEnum(d, given, fc, ev);
            else EvalNum(d, given, fc, ev);
        }
        return ev;
    }

    static void EvalEnum(ParamDef d, string? given, FormCtx fc, Evaluation ev)
    {
        var opts = d.Options!(fc);
        var def = d.EnumDefault?.Invoke(fc) ?? (opts.Length > 0 ? opts[0] : null);
        var f = new FieldView { Def = d, Hint = d.Note };
        string? value = given ?? def;
        if (value != null && opts.Contains(value))
        {
            ev.Recipe.Enums[d.Id] = value;
            f.Options = opts;
            f.Index = Array.IndexOf(opts, value);
            f.Text = value;
        }
        else if (d.OptionErrorIsPrecondition)
        {
            ev.Recipe.Enums[d.Id] = value ?? "";
            f.Options = opts.Length == 0 ? new[] { "— 대상 없음 —" } : new[] { Placeholder }.Concat(opts).ToArray();
            f.Index = 0;
            f.Error = opts.Length == 0 ? "현재 상태에 선택 가능한 대상이 없습니다(실행 시 선행조건 오류)." : "현재 상태에 없는 대상입니다. 다시 선택하세요.";
        }
        else
        {
            f.Options = opts.Length == 0 ? new[] { "— 선택 가능 항목 없음 —" } : new[] { Placeholder }.Concat(opts).ToArray();
            f.Index = 0;
            string msg = opts.Length == 0
                ? $"'{d.Label}'에 선택 가능한 항목이 없습니다. {d.Note}".Trim()
                : $"'{value}'은(는) 현재 조건에서 선택할 수 없습니다. 다시 선택하세요.";
            f.Error = msg;
            ev.Errors.Add(new Err(opts.Length == 0 ? "NO_OPTIONS" : "OPTION_INVALID", "Input", d.Id, value,
                string.Join(", ", opts), $"{d.Label}: {msg}", "목록에서 다시 선택하세요."));
        }
        ev.Fields.Add(f);
    }

    static void EvalNum(ParamDef d, string? given, FormCtx fc, Evaluation ev)
    {
        var (min, max, def) = d.Range!(fc);
        bool locked = d.Locked?.Invoke(fc) ?? false;
        string text = locked ? Fmt.N(def) : (given ?? Fmt.N(def));
        var f = new FieldView
        {
            Def = d,
            Text = text,
            ReadOnly = locked,
            Hint = $"{Fmt.N(min)}–{Fmt.N(max)} {d.Unit}, 기본 {Fmt.N(def)}" + (d.Kind == Kind.Integer ? ", 정수" : "")
                   + (locked ? " · 교육용 기본 프로파일(고정)" : "") + (d.Note.Length > 0 ? " · " + d.Note : ""),
        };
        string? err = null, code = null;
        var s = text.Trim();
        if (s.Length == 0) { code = "EMPTY"; err = "값을 입력하세요."; }
        else if (!double.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                     CultureInfo.InvariantCulture, out var v))
        { code = "PARSE_ERROR"; err = "숫자 형식이 아닙니다(소수점 '.', 지수 1e15 허용, 천 단위 구분 불가)."; }
        else if (!double.IsFinite(v)) { code = "NOT_FINITE"; err = "NaN·Infinity는 입력할 수 없습니다."; }
        else if (d.Kind == Kind.Integer && Math.Floor(v) != v) { code = "NOT_INTEGER"; err = "정수만 입력할 수 있습니다."; }
        else if (v < min || v > max) { code = "OUT_OF_RANGE"; err = $"허용 범위 {Fmt.N(min)}–{Fmt.N(max)} {d.Unit}을(를) 벗어났습니다."; }
        else ev.Recipe.Nums[d.Id] = v;

        if (err != null)
        {
            f.Error = err;
            ev.Errors.Add(new Err(code!, "Input", d.Id, text, $"{Fmt.N(min)}–{Fmt.N(max)} {d.Unit}", $"{d.Label}: {err}", "입력값을 고치세요."));
        }
        ev.Fields.Add(f);
    }
}
