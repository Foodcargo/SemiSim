namespace SemiEdu.Engine;

public sealed class Session
{
    public readonly object Sync = new();
    public readonly Dictionary<Guid, WaferState> States = new();
    public WaferState Current;
    public long Revision;
    public readonly List<RunResult> History = new();
    public readonly List<Mask> Masks = new();
    public int MaskCounter;
    // Includes mask/inspection commits, which do not change the wafer Revision.
    public long UndoRevision;
    sealed record UndoCheckpoint(RunResult Run, WaferState State, Mask[] Masks, int MaskCounter,
                                 ElectricalResult? Electrical);
    readonly List<UndoCheckpoint> undo = new();
    public IEnumerable<RunResult> ActiveRuns => undo.Select(x => x.Run);

    public WaferState? PreviewState;
    public Mask? PreviewMask;
    public ElectricalResult? PreviewElectrical;
    public ElectricalResult? LastElectrical;

    /// <summary>Token source of the running calculation; se_cancel touches it without taking <see cref="Sync"/>.</summary>
    public volatile CancellationTokenSource? Cts;
    /// <summary>Number of times a model calculation was entered (tests check that blocked runs never calculate).</summary>
    public int CalcCalls;

    public Session()
    {
        Current = InitProcess.CreateRoot();
        States[Current.StateId] = Current;
    }

    public bool CanUndo => undo.Count > 0;

    public void RecordBeforeCommit(RunResult run)
    {
        // Committed wafer states and masks are immutable; later calculations use candidate clones.
        undo.Add(new UndoCheckpoint(run, Current, Masks.ToArray(), MaskCounter, LastElectrical));
        UndoRevision++;
    }

    /// <summary>Undoes the most recent committed operation, retaining its audit history.</summary>
    public bool Undo()
    {
        if (!CanUndo) return false;
        return UndoTo(undo[^1].Run.RunId, UndoRevision) > 0;
    }

    /// <summary>Restores the snapshot BEFORE the selected active run, atomically dropping its suffix.</summary>
    public int UndoTo(string runId, long expectedUndoRevision)
    {
        if (expectedUndoRevision != UndoRevision) return -1;
        int index = undo.FindIndex(x => x.Run.RunId == runId);
        if (index < 0) return 0;
        var before = undo[index];
        int count = undo.Count - index;
        Current = before.State;
        Masks.Clear(); Masks.AddRange(before.Masks);
        MaskCounter = before.MaskCounter;
        LastElectrical = before.Electrical;
        foreach (var checkpoint in undo.Skip(index)) checkpoint.Run.Undone = true;
        undo.RemoveRange(index, count);
        Revision++;
        UndoRevision++;
        PreviewState = null;
        PreviewMask = null;
        PreviewElectrical = null;
        return count;
    }
}

public static class Catalog
{
    public static readonly string[] Groups =
    {
        "① 웨이퍼 제조", "② 산화", "③ 포토", "④ 식각", "⑤ 증착·이온주입", "⑥ 금속 배선", "⑦ EDS(검사)", "⑧ 패키징",
    };

    public static readonly ProcessBase[] All =
    {
        new InitProcess(), new OxidationProcess(), new MaskMakingProcess(), new PhotoProcess(), new EtchProcess(),
        new CleanProcess(), new DepositionProcess(), new ImplantProcess(), new AnnealProcess(), new MetalProcess(),
        new CmpProcess(), new ContactProcess(), new InspectionProcess(), new TransistorTestProcess(), new PackagingProcess(),
    };

    public static ProcessBase? Find(string id) => All.FirstOrDefault(p => p.Id == id);
}
