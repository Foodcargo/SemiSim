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

    public bool CanUndo => Current.ParentStateId is Guid g && States.ContainsKey(g);

    /// <summary>Returns to the parent snapshot. Run history is kept.</summary>
    public bool Undo()
    {
        if (!CanUndo) return false;
        Current = States[Current.ParentStateId!.Value];
        Revision++;
        PreviewState = null;
        PreviewMask = null;
        PreviewElectrical = null;
        return true;
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
