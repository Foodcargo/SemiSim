using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;

namespace SemiconductorEdu.Application.Execution;

public sealed class ProcessSession
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, WaferState> _states = new();
    private readonly List<ProcessResult> _history = new();
    private readonly Stack<WaferState> _redo = new();
    private readonly IReadOnlyDictionary<ProcessType, IProcessExecutor> _executors;
    private readonly Func<ProcessRecipe, string> _configurationHash;
    private WaferState? _currentState;
    private long _generation;

    public ProcessSession(
        IReadOnlyDictionary<ProcessType, IProcessExecutor> executors,
        Func<ProcessRecipe, string>? configurationHash = null)
    {
        _executors = executors;
        _configurationHash = configurationHash ?? (_ => "unconfigured");
    }

    public WaferState? CurrentState
    {
        get { lock (_gate) return _currentState; }
    }

    public IReadOnlyList<ProcessResult> History
    {
        get { lock (_gate) return Array.AsReadOnly(_history.ToArray()); }
    }

    public bool CanUndo
    {
        get { lock (_gate) return _currentState?.ParentStateId is Guid parentId && _states.ContainsKey(parentId); }
    }

    public bool CanRedo
    {
        get { lock (_gate) return _redo.Count > 0; }
    }

    public void Initialize(WaferState state)
    {
        lock (_gate)
        {
            _generation++;
            _states.Clear();
            _history.Clear();
            _currentState = state;
            _states[state.StateId] = state;
            _redo.Clear();
        }
    }

    public Task<ProcessExecutionResponse> ExecuteAsync(ProcessRecipe recipe, DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken = default, bool preview = false) =>
        Task.Run(() => Execute(recipe, requestedAtUtc, cancellationToken, preview));

    public ProcessExecutionResponse Execute(ProcessRecipe recipe, DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken = default, bool preview = false)
    {
        ProcessExecutionRequest request;
        IProcessExecutor executor;
        long generation;
        lock (_gate)
        {
            if (!_executors.TryGetValue(recipe.ProcessType, out executor!))
                throw new NotSupportedException($"Process '{recipe.ProcessType}' has no enabled executor.");
            generation = _generation;
            request = new(recipe, _currentState, _configurationHash(recipe), requestedAtUtc, cancellationToken);
        }

        ProcessExecutionResponse response;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            response = executor.Execute(request);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            response = Rejected(request, ProcessExecutionStatus.Cancelled, "EXECUTION_CANCELLED");
        }

        lock (_gate)
        {
            if (cancellationToken.IsCancellationRequested)
                response = Rejected(request, ProcessExecutionStatus.Cancelled, "EXECUTION_CANCELLED");
            else if (generation != _generation)
                response = Rejected(request, ProcessExecutionStatus.PreconditionFailed, "STALE_SESSION_REVISION");

            response = response with { Result = response.Result with { FinishedAtUtc = DateTimeOffset.UtcNow } };
            if (preview)
                return response with { PreviewState = response.CommittedState, CommittedState = null,
                    Result = response.Result with { AfterStateId = null } };
            // A replaced session must not inherit a run from its previous starting state.
            if (generation != _generation) return response;
            _history.Add(response.Result);
            if (response.CommittedState is not null)
            {
                _generation++;
                _states[response.CommittedState.StateId] = response.CommittedState;
                _currentState = response.CommittedState;
                _redo.Clear();
            }

            return response;
        }
    }

    private static ProcessExecutionResponse Rejected(ProcessExecutionRequest request, ProcessExecutionStatus status, string code) =>
        PatternProcessExecutor.Failed(request, Guid.NewGuid(), status,
            new[] { PatternProcessExecutor.Issue(code, ExecutionStage.Commit) });

    public bool Undo()
    {
        lock (_gate)
        {
            if (_currentState is null || _currentState.ParentStateId is not Guid parentId || !_states.TryGetValue(parentId, out var parent))
                return false;

            _redo.Push(_currentState);
            _currentState = parent;
            _generation++;
            return true;
        }
    }

    public bool Redo()
    {
        lock (_gate)
        {
            if (_redo.Count == 0) return false;
            _currentState = _redo.Pop();
            _generation++;
            return true;
        }
    }
}
