using SemiconductorEdu.Domain.Processes;

namespace SemiconductorEdu.Domain.Parameters;

public enum ParameterDataType
{
    Enum,
    Double,
    Integer,
    Boolean
}

public enum ParameterRole
{
    ModelInput,
    QualityTarget,
    Metadata
}

public enum ConditionOperator
{
    Equal,
    In,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    All,
    Any
}

public abstract record ParameterValue;
public sealed record EnumParameterValue(string Value) : ParameterValue;
public sealed record DoubleParameterValue(double Value) : ParameterValue;
public sealed record IntegerParameterValue(long Value) : ParameterValue;
public sealed record BooleanParameterValue(bool Value) : ParameterValue;

public abstract record ConditionExpression;
public sealed record ParameterCondition(string ParameterId, ConditionOperator Operator, IReadOnlyList<ParameterValue> Values) : ConditionExpression;
public sealed record CompositeCondition(ConditionOperator Operator, IReadOnlyList<ConditionExpression> Conditions) : ConditionExpression;

public sealed record ConditionalRange(ConditionExpression When, double Minimum, double Maximum);
public sealed record ConditionalOptionSet(ConditionExpression When, IReadOnlyList<ParameterValue> Options);

public sealed record ParameterDefinition
{
    public required string Id { get; init; }
    public required ProcessType ProcessType { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required ParameterDataType DataType { get; init; }
    public required string Unit { get; init; }
    public double? Minimum { get; init; }
    public double? Maximum { get; init; }
    public ParameterValue? DefaultValue { get; init; }
    public IReadOnlyList<ParameterValue> Options { get; init; } = Array.Empty<ParameterValue>();
    public ConditionExpression? VisibleWhen { get; init; }
    public ConditionExpression? RequiredWhen { get; init; }
    public ConditionExpression? ApplicableWhen { get; init; }
    public IReadOnlyList<ConditionalRange> ConditionalRanges { get; init; } = Array.Empty<ConditionalRange>();
    public IReadOnlyList<ConditionalOptionSet> ConditionalOptions { get; init; } = Array.Empty<ConditionalOptionSet>();
    public int Precision { get; init; } = 6;
    public required ParameterRole Role { get; init; }
    public bool IsEducationalExample { get; init; } = true;
    public string SourceNote { get; init; } = EducationalModelNotice.SourceNote;
    public required string DefinitionVersion { get; init; }
}

public sealed record ParameterSet(IReadOnlyDictionary<string, ParameterValue> Values)
{
    public bool TryGetValue<TValue>(string id, out TValue? value) where TValue : ParameterValue
    {
        if (Values.TryGetValue(id, out var raw) && raw is TValue typed)
        {
            value = typed;
            return true;
        }

        value = null;
        return false;
    }
}
