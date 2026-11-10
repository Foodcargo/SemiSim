using SemiconductorEdu.Domain.Equipment;
using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;
using SemiconductorEdu.Domain.Validation;
using SemiconductorEdu.Simulation.Oxidation;

namespace SemiconductorEdu.Application.Execution;

public sealed class OxidationProcessExecutor(
    OxidationModel model,
    EquipmentSnapshot equipment,
    QualityProfile qualityProfile) : IProcessExecutor
{
    public ProcessExecutionResponse Execute(ProcessExecutionRequest request)
    {
        var runId = Guid.NewGuid();
        var started = request.RequestedAtUtc;
        if (request.Recipe.ProcessType != ProcessType.Oxidation || request.Recipe.Parameters is not OxidationParameters parameters)
        {
            var issue = Issue("PROCESS_TYPE_OR_PARAMETERS_UNSUPPORTED", ExecutionStage.InputValidation, "Oxidation process requires OxidationParameters.");
            return Failed(request, runId, started, ProcessExecutionStatus.InvalidInput, new[] { issue });
        }

        if (request.CurrentState is null)
        {
            var issue = Issue("PRECONDITION_WAFER_REQUIRED", ExecutionStage.Preconditions, "Initialize a wafer before oxidation.");
            return Failed(request, runId, started, ProcessExecutionStatus.PreconditionFailed, new[] { issue });
        }

        var input = model.ValidateInput(parameters);
        if (!input.IsValid)
            return Failed(request, runId, started, ProcessExecutionStatus.InvalidInput, input.Errors);

        var interlock = model.CheckInterlock(request.CurrentState, parameters, equipment);
        if (!interlock.IsValid)
            return Failed(request, runId, started, ProcessExecutionStatus.Interlock, interlock.Errors);

        var domain = model.CheckDomain(request.CurrentState);
        if (!domain.IsValid)
            return Failed(request, runId, started, ProcessExecutionStatus.ModelOutOfDomain, domain.Errors);

        var calculated = model.Calculate(request.CurrentState, parameters, runId, started);
        if (!calculated.Validation.IsValid || calculated.CandidateState is null)
        {
            var stage = calculated.Validation.Errors.FirstOrDefault()?.Stage;
            var status = stage == ExecutionStage.ModelDomain.ToString()
                ? ProcessExecutionStatus.ModelOutOfDomain
                : ProcessExecutionStatus.CalculationError;
            return Failed(request, runId, started, status, calculated.Validation.Errors);
        }

        var totalOxide = calculated.CandidateState.CrossSection.Columns[0].SegmentsBottomToTop
            .Where(segment => segment.Material == WaferMaterial.SiliconDioxide)
            .Sum(segment => segment.Thickness.Nanometers);
        var target = qualityProfile.Targets.FirstOrDefault(item => item.MetricId == "oxideThickness");
        var quality = model.EvaluateThicknessQuality(Length.FromNanometers(totalOxide), target);
        var result = new ProcessResult(
            runId,
            LastHistoryId(request.CurrentState),
            ProcessType.Oxidation,
            ProcessExecutionStatus.ProcessSuccess,
            quality.Status,
            request.CurrentState.StateId,
            calculated.CandidateState.StateId,
            calculated.Metrics,
            quality.Errors,
            Array.Empty<ValidationIssue>(),
            request.ConfigurationHash,
            model.GetType().Assembly.GetName().Version?.ToString() ?? "unknown",
            started,
            started,
            EducationalModelNotice.SourceNote);
        return new ProcessExecutionResponse(result, calculated.CandidateState);
    }

    private static Guid? LastHistoryId(WaferState? state) =>
        state is { HistoryIds.Count: > 0 } ? state.HistoryIds[^1] : null;

    private static ProcessExecutionResponse Failed(
        ProcessExecutionRequest request,
        Guid runId,
        DateTimeOffset started,
        ProcessExecutionStatus status,
        IReadOnlyList<ValidationIssue> errors)
    {
        var result = new ProcessResult(
            runId,
            LastHistoryId(request.CurrentState),
            request.Recipe.ProcessType,
            status,
            QualityStatus.NotEvaluated,
            request.CurrentState?.StateId ?? Guid.Empty,
            null,
            Array.Empty<ProcessMetric>(),
            errors,
            Array.Empty<ValidationIssue>(),
            request.ConfigurationHash,
            "1.0.0",
            started,
            started,
            EducationalModelNotice.SourceNote);
        return new ProcessExecutionResponse(result, null);
    }

    private static ValidationIssue Issue(string code, ExecutionStage stage, string message) =>
        new(code, stage.ToString(), null, null, "valid oxidation recipe and wafer state", message, "Review the process input and retry.");
}
