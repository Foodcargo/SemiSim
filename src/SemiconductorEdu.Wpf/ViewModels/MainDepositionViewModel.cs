using CommunityToolkit.Mvvm.ComponentModel;
namespace SemiconductorEdu.Wpf.ViewModels;
public sealed partial class MainViewModel
{
    [ObservableProperty] private string _metricSummary = "";
    [ObservableProperty] private string _depositionTemperature = "400";
    [ObservableProperty] private string _depositionPressure = "1";
    [ObservableProperty] private string _depositionDuration = "10";
    [ObservableProperty] private string _depositionFlow = "100";
    [ObservableProperty] private string _depositionPower = "500";
    [ObservableProperty] private string _depositionCycles = "100";
    [ObservableProperty] private string _depositionPulse = "1";
    [ObservableProperty] private string _depositionPurge = "5";
    public bool IsCvd => DepositionMethod == "CVD";
    public bool IsPvd => DepositionMethod == "PVD";
    public bool IsAld => DepositionMethod == "ALD";
    public bool HasDepositionDuration => !IsAld;
    partial void OnDepositionMethodChanged(string value)
    {
        OnPropertyChanged(nameof(IsCvd)); OnPropertyChanged(nameof(IsPvd)); OnPropertyChanged(nameof(IsAld));
        OnPropertyChanged(nameof(HasDepositionDuration));
    }
}
