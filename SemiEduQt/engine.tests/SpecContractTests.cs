using SemiEdu.Engine;
using System.Text.Json;
using Xunit;

namespace SemiEdu.Engine.Tests;

public sealed class SpecContractTests
{
    [Theory]
    [InlineData("{\"temperature\":1000}")]
    [InlineData("{\"temperature\":true}")]
    public void AbiRejectsNonStringParameterValues(string json)
    {
        using var response = JsonDocument.Parse(Api.Form(new Session(), "oxidation", json));
        Assert.False(response.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("INVALID_JSON", response.RootElement.GetProperty("error").GetProperty("Code").GetString());
    }

    [Fact]
    public void SurfaceMetalCannotSkipSemiconductorCellsToReachBuriedNPlus()
    {
        var session = SelfTest.Path5(out _);
        var state = session.Current;
        var source = state.Contacts.Single(c => c.Role == "Source");
        var nets = new Netlist(state);
        int col = source.ColStart;
        Assert.False(Classes.IsConductiveSi(nets.Grid.ClassAt(col, 0)));
        Assert.Contains(Enumerable.Range(1, nets.Grid.Depth - 1),
            d => nets.Grid.ClassAt(col, d) == Cls.NPlus);
        int segment = state.Columns[col].Segs.FindIndex(s => s.LayerId == source.LayerId);
        var metalNet = nets.NetOfFilm(col, segment);
        Assert.NotNull(metalNet);
        Assert.True(metalNet.HasMetal);
        Assert.False(metalNet.HasNPlus);
    }

    [Fact]
    public void SpecT04At50KevIsBlockedByItsOwnGateThicknessRule()
    {
        SelfTest.Path5(out var steps, new SelfTest.Path5Options { Species = "B" });
        Assert.Contains(steps, step => step == "implant:ModelOutOfDomain(MASK_TOO_THIN)");
    }
}
