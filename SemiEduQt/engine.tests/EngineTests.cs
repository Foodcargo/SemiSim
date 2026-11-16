using SemiEdu.Engine;
using Xunit;

namespace SemiEdu.Engine.Tests;

public sealed class ScenarioTests
{
    public static IEnumerable<object[]> Ids => SelfTest.Ids.Select(id => new object[] { id });

    /// <summary>Spec tables 7 (M/E/Q/S/A/CMP), 8 (K), 9 (C), 10 (T), plus V/U input tests.</summary>
    [Theory]
    [MemberData(nameof(Ids))]
    public void Scenario(string id)
    {
        var r = SelfTest.Run(id);
        Assert.True(r.Pass, $"{id}: expected {r.Expected} | actual {r.Actual}");
    }
}

public sealed class DirectTests
{
    [Fact]
    public void FormDropsInactiveFieldsWhenSwitchingCvdToAld()
    {
        var s = new Session();
        var json = Api.Form(s, "deposition", "{\"method\":\"ALD\",\"duration\":\"10\"}");
        Assert.Contains("\"cycles\"", json);
        Assert.DoesNotContain("\"paramId\":\"duration\"", json);
        Assert.Contains("\"inactiveProvided\":[\"duration\"]", json);
    }

    [Fact]
    public void InspectionDoesNotChangeState()
    {
        var s = new Session();
        var before = s.Current.StateId;
        var r = Api.RunRaw(s, "inspection", new Dictionary<string, string> { ["inspectionMode"] = "Contamination", ["targetId"] = "WAFER" },
            new Equipment(), true, s.Revision, CancellationToken.None);
        Assert.Equal(before, s.Current.StateId);
        Assert.Equal("ProcessFailure", r.Status); // default contamination 50 > 10
        Assert.True(r.Committed);
    }

    [Fact]
    public void StaleTargetIdIsPreconditionFailure()
    {
        var s = new Session();
        var r = Api.RunRaw(s, "inspection", new Dictionary<string, string> { ["inspectionMode"] = "Thickness", ["targetId"] = "L99-SiO2" },
            new Equipment(), true, s.Revision, CancellationToken.None);
        Assert.Equal("PreconditionFailed", r.Status);
        Assert.Contains(r.Errors, e => e.Code == "TARGET_MISSING");
    }

    [Fact]
    public void FailureBlocksNextProcessUnlessContinue()
    {
        var s = new Session();
        Api.RunRaw(s, "oxidation", new Dictionary<string, string> { ["targetThickness"] = "200" }, new Equipment(), true, s.Revision, CancellationToken.None);
        var blocked = Api.RunRaw(s, "deposition", new Dictionary<string, string>(), new Equipment(), true, s.Revision, CancellationToken.None);
        Assert.Equal("PreconditionFailed", blocked.Status);
        Assert.Contains(blocked.Errors, e => e.Code == "PREVIOUS_FAILURE");
        var go = Api.RunRaw(s, "deposition", new Dictionary<string, string>(), new Equipment { ContinueOnFailure = true }, true, s.Revision, CancellationToken.None);
        Assert.Equal("ProcessSuccess", go.Status);
    }

    [Fact]
    public void AnnealWithoutImplantIsNotEvaluable()
    {
        var s = new Session();
        var r = Api.RunRaw(s, "anneal", new Dictionary<string, string>(), new Equipment(), true, s.Revision, CancellationToken.None);
        Assert.Equal("ProcessFailure", r.Status);
        Assert.Contains(r.Errors, e => e.Code == "QUALITY_NOT_EVALUABLE");
    }

    [Fact]
    public void PackagingIsDisabled()
    {
        var s = new Session();
        var r = Api.RunRaw(s, "packaging", new Dictionary<string, string>(), new Equipment(), true, s.Revision, CancellationToken.None);
        Assert.NotEqual("ProcessSuccess", r.Status);
        Assert.Contains(r.Errors, e => e.Code == "PROCESS_NOT_IMPLEMENTED");
    }

    [Fact]
    public void SceneHasBoxesAndScaleText()
    {
        var s = SelfTest.Path5(out _);
        Api.RunRaw(s, "transistor", new Dictionary<string, string>(), new Equipment(), true, s.Revision, CancellationToken.None);
        var plain = Api.Scene(s, "{}");
        var cls = Api.Scene(s, "{\"classificationView\":true}");
        Assert.True(plain.BoxCount > 5);
        Assert.True(cls.BoxCount > 5);
        Assert.Contains("수직 과장", plain.ScaleText);
        Assert.Contains("■ G", plain.Terminals);
        Assert.Equal(0, plain.Boxes.Count % 11);
    }

    [Fact]
    public void SweepProducesCurve()
    {
        var s = SelfTest.Path5(out _);
        var r = Api.RunRaw(s, "transistor", new Dictionary<string, string> { ["testMode"] = "IdVgSweep" }, new Equipment(), true, s.Revision, CancellationToken.None);
        Assert.Equal(61, r.Electrical!.Sweep.Count);
        Assert.True(r.Electrical.LogY);
        Assert.NotNull(r.Electrical.VthExt);
    }
}
