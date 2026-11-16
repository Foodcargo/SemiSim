using SemiEdu.Engine;
using Xunit;

namespace SemiEdu.Engine.Tests;

public sealed class UndoSelectionTests
{
    static RunResult Run(Session s, string id, bool commit = true) => Api.RunRaw(s, id,
        id == "inspection" ? new Dictionary<string, string> { ["inspectionMode"] = "Contamination", ["targetId"] = "WAFER" } : new Dictionary<string, string>(),
        new Equipment { ContinueOnFailure = true }, commit, s.Revision, CancellationToken.None);

    [Fact]
    public void UndoSelectedRestoresWholeSuffixAndKeepsEarlierRuns()
    {
        var s = SelfTest.Path5(out _);
        var runs = s.ActiveRuns.ToArray();
        var target = runs.First(r => r.ProcessId == "oxidation");
        int index = Array.IndexOf(runs, target);
        Assert.Equal(runs.Length - index, s.UndoTo(target.RunId, s.UndoRevision));
        Assert.Equal(target.BeforeStateId, s.Current.StateId);
        Assert.Equal(index, s.ActiveRuns.Count());
        Assert.All(runs.Take(index), r => Assert.False(r.Undone));
        Assert.All(runs.Skip(index), r => Assert.True(r.Undone));
        Assert.Contains(target, s.History);
        Assert.Equal(3, s.Masks.Count);
    }

    [Fact]
    public void UndoMaskRestoresLibraryAndCounter()
    {
        var s = new Session();
        var first = Run(s, "mask");
        var second = Run(s, "mask");
        Assert.True(first.Committed && second.Committed);
        Assert.Equal(1, s.UndoTo(second.RunId, s.UndoRevision));
        Assert.Single(s.Masks);
        Assert.Equal(1, s.MaskCounter);
        Assert.True(s.Undo());
        Assert.Empty(s.Masks);
        Assert.Equal(0, s.MaskCounter);
    }

    [Fact]
    public void InspectionInvalidatesPendingUndoWithoutChangingWaferRevision()
    {
        var s = new Session();
        var mask = Run(s, "mask");
        long undoRevision = s.UndoRevision, waferRevision = s.Revision;
        var inspection = Run(s, "inspection");
        Assert.True(inspection.Committed);
        Assert.Equal(waferRevision, s.Revision);
        Assert.Equal(-1, s.UndoTo(mask.RunId, undoRevision));
        Assert.Equal(2, s.ActiveRuns.Count());
        var state = s.Current;
        Assert.True(s.Undo());
        Assert.Same(state, s.Current);
        Assert.True(inspection.Undone);
    }

    [Fact]
    public void PreviewAndBlockedRunsAreNotUndoTargets()
    {
        var s = new Session();
        var preview = Run(s, "mask", false);
        var blocked = Run(s, "packaging");
        Assert.False(preview.Committed);
        Assert.False(blocked.Committed);
        Assert.False(s.CanUndo);
        Assert.Equal(0, s.UndoTo(preview.RunId, s.UndoRevision));
    }

    [Fact]
    public void UndoElectricalTestRestoresPreviousResult()
    {
        var s = SelfTest.Path5(out _);
        var previous = new ElectricalResult();
        s.LastElectrical = previous;
        var wafer = s.Current;
        var run = Run(s, "transistor");
        Assert.True(run.Committed);
        Assert.NotSame(previous, s.LastElectrical);
        Assert.True(s.Undo());
        Assert.Same(previous, s.LastElectrical);
        Assert.Same(wafer, s.Current);
    }

    [Fact]
    public void NewBranchCannotUndoRemovedRunsAndUndoClearsPreview()
    {
        var s = new Session();
        var old = Run(s, "mask");
        Run(s, "mask", false);
        Assert.NotNull(s.PreviewMask);
        Assert.True(s.Undo());
        Assert.Null(s.PreviewMask);
        var current = Run(s, "mask");
        Assert.Equal(0, s.UndoTo(old.RunId, s.UndoRevision));
        Assert.Equal(current, Assert.Single(s.ActiveRuns));
        Assert.True(s.Undo());
        Assert.False(s.Undo());
    }
}
