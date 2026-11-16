using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SemiconductorEdu.Application.Execution;
using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Infrastructure.Storage;

namespace SemiconductorEdu.Wpf.ViewModels;

public sealed partial class MainViewModel
{
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isResultStale;
    private CancellationTokenSource? _executionCancellation;
    private readonly SessionStore _sessionStore = new();
    public bool IsIdle => !IsBusy;
    public string ResultContext => IsResultStale ? "입력 변경됨 · 이전 레시피 결과" : "현재 실행 결과";
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsIdle));
    partial void OnIsResultStaleChanged(bool value) => OnPropertyChanged(nameof(ResultContext));
    partial void OnInspectionModeChanged(string value) => RefreshInspectionTargets();

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is not (nameof(IsResultStale) or nameof(ResultContext) or nameof(IsBusy) or nameof(IsIdle) or
            nameof(MetricSummary) or nameof(StatusMessage) or nameof(QualityResult) or nameof(OxideThickness) or nameof(InspectionResult) or
            nameof(CanUndo) or nameof(CanRedo) or nameof(CanRunOxidation) or nameof(InspectionTargetId)))
            IsResultStale = _session?.History.Count > 0;
    }

    private async Task<ProcessExecutionResponse> ExecuteWithCancellationAsync(ProcessRecipe recipe)
    {
        IsBusy = true;
        _executionCancellation = new CancellationTokenSource();
        try
        {
            var result = await _session.ExecuteAsync(recipe, DateTimeOffset.UtcNow, _executionCancellation.Token);
            IsResultStale = false;
            return result;
        }
        finally
        {
            _executionCancellation.Dispose();
            _executionCancellation = null;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelExecution() => _executionCancellation?.Cancel();

    [RelayCommand]
    private async Task SaveSession()
    {
        var dialog = new SaveFileDialog { Filter = "Session JSON (*.json)|*.json", DefaultExt = ".json", FileName = "semiconductor-session.json" };
        if (dialog.ShowDialog() != true) return;
        IsBusy = true;
        try
        {
            var snapshot = _session.Capture();
            await _sessionStore.SaveAsync(dialog.FileName, new SessionDocument(1, "1.0.0", "1.0.0", "1.0.0",
                DateTimeOffset.UtcNow, snapshot.CurrentStateId, snapshot.States, snapshot.History));
            StatusMessage = "세션 저장 완료 · 전체 상태와 실행 레시피를 보존했습니다.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        { StatusMessage = "세션 저장 실패: " + e.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadSession()
    {
        var dialog = new OpenFileDialog { Filter = "Session JSON (*.json)|*.json" };
        if (dialog.ShowDialog() != true) return;
        IsBusy = true;
        try
        {
            var loaded = await _sessionStore.LoadAsync(dialog.FileName);
            if (!loaded.IsValid)
            {
                StatusMessage = string.Join(Environment.NewLine, loaded.Errors.Select(e => e.Code + ": " + e.MessageKo));
                return;
            }
            var document = loaded.Document!;
            if (document.ModelVersion != "1.0.0")
            {
                StatusMessage = "이 모델 버전은 현재 앱에서 재실행할 수 없습니다. 기존 세션을 유지합니다.";
                return;
            }
            _session.Restore(new(document.CurrentStateId, document.States, document.History));
            History.Clear();
            foreach (var run in document.History.Reverse())
                History.Add($"{run.StartedAtUtc:HH:mm:ss} · {run.ProcessType} · {run.ExecutionStatus}");
            RefreshState();
            OxideThickness = _session.CurrentState is { } state
                ? state.CrossSection.Columns.Average(c => c.SegmentsBottomToTop.Where(s => s.Material == SemiconductorEdu.Domain.State.WaferMaterial.SiliconDioxide).Sum(s => s.Thickness.Nanometers)).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                : "0";
            QualityResult = "복원된 이력 확인";
            InspectionResult = "검사 전";
            IsResultStale = true;
            StatusMessage = "세션 복원 완료 · 입력 폼은 복원된 실행 레시피와 다를 수 있습니다.";
        }
        catch (ArgumentException e) { StatusMessage = "세션 복원 실패: " + e.Message; }
        finally { IsBusy = false; }
    }
}
