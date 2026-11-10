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

public sealed partial class StageOneViewModel : ObservableObject
{
    private readonly ProcessSession _session;
    private readonly WaferStateFactory _factory;
    private readonly Guid _initializationId = Guid.NewGuid();

    [ObservableProperty] private string _statusMessage = "웨이퍼를 초기화하세요.";
    [ObservableProperty] private string _temperature = "1000";
    [ObservableProperty] private string _duration = "60";
    [ObservableProperty] private string _pressure = "760";
    [ObservableProperty] private string _diameter = "200";
    [ObservableProperty] private string _substrateThickness = "725";
    [ObservableProperty] private string _domainWidth = "20";
    [ObservableProperty] private string _initialOxide = "100";
    [ObservableProperty] private bool _uniformOxide;
    [ObservableProperty] private string _method = "Dry";
    [ObservableProperty] private string _oxideThickness = "0.000";
    [ObservableProperty] private string _quality = "미평가";
    [ObservableProperty] private bool _canUndo;
    [ObservableProperty] private bool _canRedo;

    public StageOneViewModel(ProcessSession session, WaferStateFactory factory)
    {
        _session = session;
        _factory = factory;
        CrossSection = new ObservableCollection<CrossSectionColumnViewModel>();
        History = new ObservableCollection<string>();
        InitializeCommand.Execute(null);
    }

    public ObservableCollection<CrossSectionColumnViewModel> CrossSection { get; }
    public ObservableCollection<string> History { get; }
    public IReadOnlyList<string> Methods { get; } = new[] { "Dry", "Wet" };
    public IReadOnlyList<string> Diameters { get; } = new[] { "150", "200", "300" };

    [RelayCommand]
    private void Initialize()
    {
        if (!int.TryParse(Diameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var diameter) || !TryPositive( SubstrateThickness, out var waferThickness) || !TryPositive(DomainWidth, out var width) || !TryNonNegative(InitialOxide, out var initialOxide))
        {
            StatusMessage = "웨이퍼 입력값을 확인하세요.";
            return;
        }
        var parameters = WaferInitializationParameters.EducationalDefaults() with
        {
            DiameterMm = diameter,
            SubstrateThickness = Length.FromMicrometers(waferThickness),
            DomainWidth = Length.FromMicrometers(width),
            StartSurface = UniformOxide ? WaferStartSurface.UniformSiO2 : WaferStartSurface.BareSi,
            InitialOxideThickness = Length.FromNanometers(initialOxide)
        };
        var state = _factory.Create(parameters, _initializationId, DateTimeOffset.UtcNow);
        _session.Initialize(state);
        OxideThickness = (UniformOxide ? initialOxide : 0).ToString("F3", CultureInfo.InvariantCulture);
        Quality = "미평가";
        StatusMessage = $"초기화 완료 · Si {diameter} mm";
        History.Clear();
        History.Add("웨이퍼 초기화");
        Refresh();
    }

    [RelayCommand]
    private void Oxidize()
    {
        if (!TryFinite(Temperature, out var temp) || !TryFinite(Duration, out var time) || !TryFinite(Pressure, out var pressure))
        {
            StatusMessage = "산화 입력은 유한한 숫자여야 합니다.";
            return;
        }
        var method = Method == "Wet" ? OxidationMethod.Wet : OxidationMethod.Dry;
        var recipe = new ProcessRecipe(Guid.NewGuid(), ProcessType.Oxidation,
            new OxidationParameters(method, SemiconductorEdu.Domain.Units.Temperature.FromCelsius(temp), SemiconductorEdu.Domain.Units.Duration.FromMinutes(time), SemiconductorEdu.Domain.Units.Pressure.FromTorr(pressure)),
            "educational-furnace", "educational-default", "1.0.0", "1.0.0");
        Execute(recipe);
    }

    [RelayCommand] private void Undo() { if (_session.Undo()) { Update(); Refresh(); } }
    [RelayCommand] private void Redo() { if (_session.Redo()) { Update(); Refresh(); } }

    private void Execute(ProcessRecipe recipe)
    {
        var response = _session.Execute(recipe, DateTimeOffset.UtcNow);
        StatusMessage = response.CommittedState is not null ? $"산화 완료 · 품질 {response.Result.QualityStatus}" : string.Join(Environment.NewLine, response.Result.Errors.Select(issue => issue.MessageKo));
        History.Insert(0, $"{recipe.ProcessType} · {response.Result.ExecutionStatus}");
        if (response.CommittedState is not null) Update();
        Quality = response.Result.QualityStatus.ToString();
        Refresh();
    }

    private void Update()
    {
        if (_session.CurrentState is not { } state) return;
        OxideThickness = state.CrossSection.Columns[0].SegmentsBottomToTop.Where(segment => segment.Material == WaferMaterial.SiliconDioxide).Sum(segment => segment.Thickness.Nanometers).ToString("F3", CultureInfo.InvariantCulture);
        Quality = "미평가";
    }

    private void Refresh()
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
