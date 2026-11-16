using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Validation;
using SemiconductorEdu.Simulation.Processes;

namespace SemiconductorEdu.Application.Execution;

public sealed class PatternProcessExecutor(PatternProcessModels models, SurfaceProcessModels? surfaceModels = null) : IProcessExecutor
{
    public ProcessExecutionResponse Execute(ProcessExecutionRequest request)
    {
        var runId = Guid.NewGuid();
        if (request.CurrentState is null)
            return Failed(request, runId, ProcessExecutionStatus.PreconditionFailed, new[] { Issue("PRECONDITION_WAFER_REQUIRED", ExecutionStage.Preconditions) });

        var output = request.Recipe.Parameters switch
        {
            PhotoConditions photo when request.Recipe.ProcessType == ProcessType.Photolithography => (surfaceModels ?? new()).Photo(request.CurrentState, photo, runId, request.RequestedAtUtc, request.CancellationToken),
            EtchConditions etch when request.Recipe.ProcessType == ProcessType.Etch => (surfaceModels ?? new()).Etch(request.CurrentState, etch, runId, request.RequestedAtUtc, request.CancellationToken),
            CleanConditions clean when request.Recipe.ProcessType == ProcessType.Cleaning => (surfaceModels ?? new()).Clean(request.CurrentState, clean, runId, request.RequestedAtUtc, request.CancellationToken),
            PhotolithographyParameters photo when request.Recipe.ProcessType == ProcessType.Photolithography => models.ApplyPhotolithography(request.CurrentState, photo, runId, request.RequestedAtUtc),
            EtchParameters etch when request.Recipe.ProcessType == ProcessType.Etch => models.ApplyEtch(request.CurrentState, etch, runId, request.RequestedAtUtc),
            CleaningParameters cleaning when request.Recipe.ProcessType == ProcessType.Cleaning => models.ApplyPhotoresistStrip(request.CurrentState, cleaning, runId, request.RequestedAtUtc),
            _ => null
        };
        return Complete(request, runId, output);
    }

    internal static ProcessExecutionResponse Complete(ProcessExecutionRequest request, Guid runId, ProcessModelOutput? output)
    {
        if (output is null)
            return Failed(request, runId, ProcessExecutionStatus.InvalidInput, new[] { Issue("PROCESS_TYPE_OR_PARAMETERS_UNSUPPORTED", ExecutionStage.InputValidation) });
        if (!output.Validation.IsValid || output.CandidateState is null)
        {
            var status = output.Validation.Errors.Any(error => error.Stage is "Calculation" or "InvariantValidation")
                ? ProcessExecutionStatus.CalculationError
                : output.Validation.Errors.Any(error => error.Stage == ExecutionStage.EquipmentInterlock.ToString())
                ? ProcessExecutionStatus.Interlock
                : output.Validation.Errors.Any(error => error.Stage == ExecutionStage.ModelDomain.ToString())
                ? ProcessExecutionStatus.ModelOutOfDomain
                : output.Validation.Errors.Any(error => error.Stage == ExecutionStage.Preconditions.ToString())
                    ? ProcessExecutionStatus.PreconditionFailed
                    : ProcessExecutionStatus.InvalidInput;
            return Failed(request, runId, status, output.Validation.Errors.ToArray());
        }

        var state = request.CurrentState!;
        var result = new ProcessResult(runId, LastRunId(state), request.Recipe.ProcessType, ProcessExecutionStatus.ProcessSuccess,
            QualityStatus.NotEvaluated, state.StateId, output.CandidateState.StateId, output.Metrics,
            Array.Empty<ValidationIssue>(), output.Validation.Warnings, request.ConfigurationHash, "1.0.0",
            request.RequestedAtUtc, request.RequestedAtUtc, EducationalModelNotice.SourceNote);
        return QualityEvaluation.Apply(new ProcessExecutionResponse(result with {UnsupportedEffects=output.UnsupportedEffects}, output.CandidateState), request.Recipe.QualityOverride ?? output.DefaultQuality);
    }

    internal static ProcessExecutionResponse Failed(ProcessExecutionRequest request, Guid runId, ProcessExecutionStatus status, IReadOnlyList<ValidationIssue> errors) =>
        new(new ProcessResult(runId, LastRunId(request.CurrentState), request.Recipe.ProcessType, status, QualityStatus.NotEvaluated,
            request.CurrentState?.StateId ?? Guid.Empty, null, Array.Empty<ProcessMetric>(), errors, Array.Empty<ValidationIssue>(),
            request.ConfigurationHash, "1.0.0", request.RequestedAtUtc, request.RequestedAtUtc, EducationalModelNotice.SourceNote), null);

    internal static ValidationIssue Issue(string code, ExecutionStage stage) =>
        new(code, stage.ToString(), null, null, "valid supported recipe and wafer", code, "입력과 공정 상태를 확인하세요.");

    internal static Guid? LastRunId(WaferState? state) => state is { HistoryIds.Count: > 0 } ? state.HistoryIds[^1] : null;
}

public sealed class InspectionProcessExecutor(InspectionModels models) : IProcessExecutor
{
    public ProcessExecutionResponse Execute(ProcessExecutionRequest request)
    {
        var runId = Guid.NewGuid();
        if (request.CurrentState is null)
            return PatternProcessExecutor.Failed(request, runId, ProcessExecutionStatus.PreconditionFailed,
                new[] { PatternProcessExecutor.Issue("PRECONDITION_WAFER_REQUIRED", ExecutionStage.Preconditions) });
        if (request.Recipe.ProcessType != ProcessType.Inspection || request.Recipe.Parameters is not InspectionParameters parameters)
            return PatternProcessExecutor.Failed(request, runId, ProcessExecutionStatus.InvalidInput,
                new[] { PatternProcessExecutor.Issue("PROCESS_TYPE_OR_PARAMETERS_UNSUPPORTED", ExecutionStage.InputValidation) });

        var evaluation = models.Inspect(request.CurrentState, parameters, runId, request.RequestedAtUtc);
        if (!evaluation.Output.Validation.IsValid || evaluation.Output.CandidateState is null)
            return PatternProcessExecutor.Complete(request, runId, evaluation.Output);

        var output = evaluation.Output;
        var result = new ProcessResult(runId, PatternProcessExecutor.LastRunId(request.CurrentState), ProcessType.Inspection,
            evaluation.QualityStatus == QualityStatus.Pass ? ProcessExecutionStatus.ProcessSuccess : ProcessExecutionStatus.ProcessFailure,
            evaluation.QualityStatus, request.CurrentState.StateId, output.CandidateState.StateId, evaluation.Metrics,
            Array.Empty<ValidationIssue>(), output.Validation.Warnings, request.ConfigurationHash, "1.0.0",
            request.RequestedAtUtc, request.RequestedAtUtc, EducationalModelNotice.SourceNote);
        return new ProcessExecutionResponse(result, output.CandidateState);
    }
}

public sealed class MetallizationProcessExecutor(MetallizationModels models) : IProcessExecutor
{
    public ProcessExecutionResponse Execute(ProcessExecutionRequest request)
    {
        var runId = Guid.NewGuid();
        if (request.CurrentState is null)
            return PatternProcessExecutor.Failed(request, runId, ProcessExecutionStatus.PreconditionFailed,
                new[] { PatternProcessExecutor.Issue("PRECONDITION_WAFER_REQUIRED", ExecutionStage.Preconditions) });
        var output = request.Recipe.Parameters is MetallizationParameters parameters && request.Recipe.ProcessType == ProcessType.Metallization
            ? models.ApplyIdealPattern(request.CurrentState, parameters, runId, request.RequestedAtUtc)
            : null;
        return PatternProcessExecutor.Complete(request, runId, output);
    }
}

public sealed class DepositionAndCmpProcessExecutor(DepositionAndCmpModels models, SurfaceProcessModels? surfaceModels = null) : IProcessExecutor
{
    public ProcessExecutionResponse Execute(ProcessExecutionRequest request)
    {
        var runId = Guid.NewGuid();
        if (request.CurrentState is null)
            return PatternProcessExecutor.Failed(request, runId, ProcessExecutionStatus.PreconditionFailed,
                new[] { PatternProcessExecutor.Issue("PRECONDITION_WAFER_REQUIRED", ExecutionStage.Preconditions) });

        var output = request.Recipe.Parameters switch
        {
            DepositionConditions depositionConditions when request.Recipe.ProcessType == ProcessType.Deposition => models.ApplyDeposition(request.CurrentState, depositionConditions, runId, request.RequestedAtUtc),
            DepositionParameters deposition when request.Recipe.ProcessType == ProcessType.Deposition => models.ApplyDeposition(request.CurrentState, deposition, runId, request.RequestedAtUtc),
            CmpConditions cmpConditions when request.Recipe.ProcessType == ProcessType.Cmp => (surfaceModels ?? new()).Cmp(request.CurrentState, cmpConditions, runId, request.RequestedAtUtc, request.CancellationToken),
            CmpParameters cmp when request.Recipe.ProcessType == ProcessType.Cmp => models.ApplyCmp(request.CurrentState, cmp, runId, request.RequestedAtUtc),
            _ => null
        };
        var response = PatternProcessExecutor.Complete(request, runId, output);
        if (response.CommittedState is not null && request.Recipe.Parameters is DepositionConditions conditions)
        {
            var target = conditions.Method switch
            {
                DepositionMethod.Cvd => conditions.Material == WaferMaterial.SiliconDioxide ? 100.0 : 80.0,
                DepositionMethod.Pvd => conditions.Material == WaferMaterial.Aluminum ? 200.0 : 100.0,
                _ => conditions.Material == WaferMaterial.SiliconDioxide ? 10.0 : 5.0
            };
            var thickness = response.Result.Metrics.Single(m => m.Id == "depositedThickness").Value!.Value;
            var purge = response.Result.Metrics.Single(m => m.Id == "purgePenaltyPct").Value!.Value;
            var pass = Math.Abs(thickness - target) <= target * 0.1 + 1e-9 * Math.Max(1, Math.Max(thickness, target)) && purge <= 5 + 5e-9;
            response = response with { Result = response.Result with
            {
                QualityStatus = pass ? QualityStatus.Pass : QualityStatus.Fail,
                ExecutionStatus = pass ? ProcessExecutionStatus.ProcessSuccess : ProcessExecutionStatus.ProcessFailure,
                Metrics = response.Result.Metrics.Append(new ProcessMetric("targetThickness", target, "nm")).ToArray()
            } };
        }
        return response;
    }
}

public sealed class ImplantAnnealProcessExecutor(ImplantAnnealModels models) : IProcessExecutor
{
    public ProcessExecutionResponse Execute(ProcessExecutionRequest request)
    {
        var runId = Guid.NewGuid();
        if (request.CurrentState is null)
            return PatternProcessExecutor.Failed(request, runId, ProcessExecutionStatus.PreconditionFailed,
                new[] { PatternProcessExecutor.Issue("PRECONDITION_WAFER_REQUIRED", ExecutionStage.Preconditions) });

        var output = request.Recipe.Parameters switch
        {
            IonImplantationParameters implant when request.Recipe.ProcessType == ProcessType.IonImplantation => models.ApplyImplantation(request.CurrentState, implant, runId, request.RequestedAtUtc),
            AnnealParameters anneal when request.Recipe.ProcessType == ProcessType.Anneal => models.ApplyAnneal(request.CurrentState, anneal, runId, request.RequestedAtUtc),
            _ => null
        };
        return PatternProcessExecutor.Complete(request, runId, output);
    }
}
