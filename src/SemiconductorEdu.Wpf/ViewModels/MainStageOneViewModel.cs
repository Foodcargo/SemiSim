using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SemiconductorEdu.Application.Execution;
using SemiconductorEdu.Domain.Processes;
using SemiconductorEdu.Domain.State;
using SemiconductorEdu.Domain.Units;
using SemiconductorEdu.Simulation.Initialization;

namespace SemiconductorEdu.Wpf.ViewModels;

public sealed partial class MainStageOneViewModel : ObservableObject
{
    private readonly ProcessSession _session;
    private readonly WaferStateFactory _waferFactory;
    private readonly Guid _initializationProcessId = Guid.NewGuid();

    [ObservableProperty] private string _statusMessage = "초기화된 웨이퍼에서 공정을 실행하세요.";
    [ObservableProperty] private string _oxidationMethod = "Dry";
    [ObservableProperty] private string _temperatureCelsius = "1000";
    [ObservableProperty] private string _durationMinutes = "60";
    [ObservableProperty] private string _pressureTorr = "760";
    [ObservableProperty] private bool _canUndo;
    [ObservableProperty] private bool _canRedo;
    [ObservableProperty] private string _oxideThickness = "0.000";
    [ObservableProperty] private string _qualityResult = "미평가";
    [ObservableProperty] private string _waferDiameterMm = "200";
    [ObservableProperty] private string _substrateThicknessMicrometers = "725";
    [ObservableProperty] private string _domainWidthMicrometers = "20";
    [ObservableProperty] private string _initialOxideNanometers = "100";
    [ObservableProperty] private bool _isUniformOxideEnabled;

    public MainStageOneViewModel(ProcessSession session, WaferStateFactory waferFactory)
    {
        _session = session;
        _waferFactory = waferFactory;
        CrossSection = new ObservableCollection<CrossSectionColumnViewModel>();
        History = new ObservableCollection<string>();
        InitializeWaferCommand.Execute(null);
    }

    public ObservableCollection<CrossSectionColumnViewModel> CrossSection { get; }
    public ObservableCollection<string> History { get; }
    public IReadOnlyList<string> OxidationMethods { get; } = new[] { "Dry", "Wet" };
    public IReadOnlyList<string> WaferDiameters { get; } = new[] { "150", "200", "300" };

    [RelayCommand]
    private void InitializeWafer()
    {
        if (!int.TryParse(WaferDiameterMm, NumberStyles.Integer, CultureInfo.InvariantCulture, out var diameter) ||
            !TryPositive(SubstrateThicknessMicrometers, out var thickness) || !TryPositive(DomainWidthMicrometers, out var width) || !TryNonNegative(InitialOxideNanometers, out var oxide))
        {
            StatusMessage = "웨이퍼 입력값을 확인하세요.";
            return;
        }
        var parameters = WaferInitializationParameters.EducationalDefaults() with
        {
            DiameterMm = diameter,
            SubstrateThickness = Length.FromMicrometers(thickness),
            DomainWidth = Length.FromMicrometers(width),
            StartSurface = IsUniformOxideEnabled ? WaferStartSurface.UniformSiO2 : WaferStartSurface.BareSi,
            InitialOxideThickness = Length.FromNanometers(oxide)
        };
        var state = _waferFactory.Create(parameters, _initializationProcessId, DateTimeOffset.UtcNow);
        _session.Initialize(state);
        OxideThickness = (IsUniformOxideEnabled ? oxide : 0).ToString("F3", CultureInfo.InvariantCulture);
        QualityResult = "미평가";
        StatusMessage = $"웨이퍼 초기화 완료 · Si {diameter} mm";
        History.Clear();
        History.Add($"{state.CreatedAtUtc:HH:mm:ss} · 웨이퍼 초기화");
        RefreshState();
    }

    [RelayCommand]
    private void RunOxidation()
    {
        if (!TryFinite(TemperatureCelsius, out var temperature) || !TryFinite(DurationMinutes, out var duration) || !TryFinite(PressureTorr, out var pressure))
        {
            StatusMessage = "산화 입력은 유한한 숫자여야 합니다.";
            return;
        }
        var method = OxidationMethod == "Wet" ? SemiconductorEdu.Domain.Processes.OxidationMethod.Wet : SemiconductorEdu.Domain.Processes.OxidationMethod.Dry;
        Execute(new ProcessRecipe(Guid.NewGuid(), ProcessType.Oxidation,
            new OxidationParameters(method, Temperature.FromCelsius(temperature), Duration.FromMinutes(duration), Pressure.FromTorr(pressure)),
            "educational-furnace", "educational-default", "1.0.0", "1.0.0"), true);
    }

    [RelayCommand]
    private void Undo() { if (_session.Undo()) { UpdateDisplays(); RefreshState(); } }

    [RelayCommand]
    private void Redo() { if (_session.Redo()) { UpdateDisplays(); RefreshState(); } }

    private void Execute(ProcessRecipe recipe, bool oxidation)
    {
        try
        {
            var response = _session.Execute(recipe, DateTimeOffset.UtcNow);
            if (response.CommittedState is not null)
            {
                StatusMessage = $"{recipe.ProcessType} 완료 · 교육용 단순 모델";
                QualityResult = oxidation ? response.Result.QualityStatus.ToString() : "미평가";
                UpdateDisplays();
            }
            else StatusMessage = string.Join(Environment.NewLine, response.Result.Errors.Select(issue => issue.MessageKo));
            History.Insert(0, $"{response.Result.StartedAtUtc:HH:mm:ss} · {recipe.ProcessType} · {response.Result.ExecutionStatus}");
        }
        catch (NotSupportedException exception) { StatusMessage = exception.Message; }
    }

    private void UpdateDisplays()
    {
        if (_session.CurrentState is not { } state) return;
        OxideThickness = state.CrossSection.Columns[0].SegmentsBottomToTop.Where(segment => segment.Material == WaferMaterial.SiliconDioxide).Sum(segment => segment.Thickness.Nanometers).ToString("F3", CultureInfo.InvariantCulture);
        QualityResult = "미평가";
    }

    private void RefreshState()
    {
        CrossSection.Clear();
        if (_session.CurrentState is { } state) foreach (var column in state.CrossSection.Columns) CrossSection.Add(CrossSectionColumnViewModel.From(column));
        CanUndo = _session.CanUndo;
        CanRedo = _session.CanRedo;
    }

    private static bool TryPositive(string text, out double value) => TryFinite(text, out value) && value > 0;
    private static bool TryNonNegative(string text, out double value) => TryFinite(text, out value) && value >= 0;
    private static bool TryFinite(string text, out double value) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
}
