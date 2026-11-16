using SemiEdu.Engine;
using Xunit;

namespace SemiEdu.Engine.Tests;

public sealed class TypedParameterTests
{
    static Ctx Context(Session session) => new()
    {
        Session = session, State = session.Current, Eq = new Equipment(),
    };

    [Fact]
    public void CalculationInputsRemainStableWhenFormRecipeChanges()
    {
        var session = new Session();
        var process = new OxidationProcess();
        var evaluated = Validator.Evaluate(process, new Dictionary<string, string>(), Context(session), true);
        Assert.Empty(evaluated.Errors);
        OxidationParameters parameters = evaluated.Recipe;

        evaluated.Recipe.Nums["temperature"] = 900;
        evaluated.Recipe.Nums["targetThickness"] = 200;

        var result = OxidationProcess.Compute(parameters, session.Current);
        Assert.Equal(70.4159458, result.XNew, 6);
        Assert.Equal(1000d, parameters.Temperature);
        Assert.Equal(70d, parameters.TargetThickness);
    }

    [Fact]
    public void AldCalculationDoesNotRequireInactiveCvdParameters()
    {
        var session = new Session();
        var process = new DepositionProcess();
        var evaluated = Validator.Evaluate(process,
            new Dictionary<string, string> { ["method"] = "ALD" }, Context(session), true);
        Assert.Empty(evaluated.Errors);
        DepositionParameters parameters = evaluated.Recipe;
        Assert.Null(parameters.Duration);
        Assert.Equal(10d, DepositionProcess.Thickness(parameters), 6);
    }

    [Fact]
    public void MissingRequiredCalculationInputIsNeverSilentlyZero()
    {
        OxidationParameters parameters = new Recipe();
        Assert.Throws<InvalidOperationException>(() =>
            OxidationProcess.Compute(parameters, new Session().Current));
    }
}
