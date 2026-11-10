using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SemiconductorEdu.Application.Execution;
using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;

namespace SemiconductorEdu.Wpf.ViewModels;

public sealed partial class StageTwoViewModel : ObservableObject
{
    private readonly ProcessSession _session;

    [ObservableProperty] private string _photoThicknessNanometers = "1200";
    [ObservableProperty] private string _openStartMicrometers = "5";
    [ObservableProperty] private string _openEndMicrometers = "10";
    [ObservableProperty] private string _etchDepthNanometers = "50";
    [ObservableProperty] private string _etchTarget = "SiO₂";
    [ObservableProperty] private string _statusMessage = "포토 → 식각 → PR 제거 순서로 실행하세요.";

    public StageTwoViewModel(ProcessSession session) => _session = session;

    [RelayCommand]
    private void ApplyPhoto()
    {
        if (!TryPositive(PhotoThicknessNanometers, out var thickness) || !TryFinite(OpenStartMicrometers, out var start) || !TryFinite(OpenEndMicrometers, out var end))
        {
            StatusMessage = "포토 두께와 개구 구간을 확인하세요.";
            return;
        }
        Execute(new ProcessRecipe(Guid.NewGuid(), ProcessType.Photolithography,
            new PhotolithographyParameters(Length.FromNanometers(thickness), new[] { new LengthInterval(Length.FromMicrometers(start), Length.FromMicrometers(end)) }),
            "educational-lithography", "not-evaluated", "1.0.0", "1.0.0"));
    }

    [RelayCommand]
    private void Etch()
    {
        if (!TryPositive(EtchDepthNanometers, out var depth))
        {
            StatusMessage = "식각 깊이는 양수로 입력하세요.";
            return;
        }
        var target = EtchTarget == "Si" ? WaferMaterial.Silicon : WaferMaterial.SiliconDioxide;
        Execute(new ProcessRecipe(Guid.NewGuid(), ProcessType.Etch,
            new EtchParameters(target, Length.FromNanometers(depth), 0), "educational-etcher", "not-evaluated", "1.0.0", "1.0.0"));
    }

    [RelayCommand]
    private void StripResist() => Execute(new ProcessRecipe(Guid.NewGuid(), ProcessType.Cleaning,
        new CleaningParameters(CleaningMethod.PhotoresistStrip), "educational-cleaner", "not-evaluated", "1.0.0", "1.0.0"));

    private void Execute(ProcessRecipe recipe)
    {
        try
        {
            var response = _session.Execute(recipe, DateTimeOffset.UtcNow);
            StatusMessage = response.CommittedState is not null
                ? $"{recipe.ProcessType} 완료 · 교육용 단순 모델 · 품질 미평가"
                : string.Join(Environment.NewLine, response.Result.Errors.Select(issue => issue.MessageKo));
        }
        catch (NotSupportedException exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private static bool TryPositive(string text, out double value) => TryFinite(text, out value) && value > 0;
    private static bool TryFinite(string text, out double value) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
}
