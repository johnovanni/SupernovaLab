using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;

namespace SupernovaLab.App.ViewModels;

/// <summary>One line of the Close-up's readout column: the label (and its tooltip), the value, and the confidence badge's text.</summary>
public sealed record ReadoutRow(string Label, string? LabelToolTip, string ValueText, Confidence Confidence, string BadgeToolTip,
    bool Published, string PublishedToolTip)
{
    /// <summary>The row for a <see cref="Readout"/>; the badge tooltips are <see cref="QuantityRow"/>'s, as everywhere else.</summary>
    public static ReadoutRow From(Readout r)
    {
        var q = new QuantityRow("", r.Label, "", r.Value, r.ValueText);
        return new ReadoutRow(r.Label, r.ToolTip, r.ValueText, q.Confidence, q.BadgeToolTip, q.Published, q.PublishedToolTip);
    }
}

/// <summary>
/// The Close-up window's view model (spec §2). It has no timeline of its own: it follows the shared <see cref="MainViewModel"/>'s
/// outcome, observer, timeline and run number, as the Instruments window does. A new outcome, observer or run makes a new
/// <see cref="CloseUpScene"/>; each time change restates <see cref="State"/>. A model error keeps the last state and shows a
/// message in the notes; the other views carry on (spec §8.4). The window's menu switches (Grid, Field lines, Light cylinder,
/// Cutaway, Labels, Focus view) are kept here. Call <see cref="Dispose"/> when the window closes: the main view model lives as long
/// as the app.
/// </summary>
public sealed partial class CloseUpViewModel : ObservableObject, IDisposable
{
    private CloseUpScene? _scene;
    private string? _modelError;
    private bool _disposed;

    public CloseUpViewModel(MainViewModel main)
    {
        Main = main ?? throw new ArgumentNullException(nameof(main));
        BuildScene();
        ComputeState();
        main.OutcomeChanged += OnOutcomeChanged;
        main.Timeline.PropertyChanged += OnTimelineChanged;
        main.PropertyChanged += OnMainChanged;
        main.Layout.PropertyChanged += OnLayoutChanged;
    }

    /// <summary>The shared lab state this window follows.</summary>
    public MainViewModel Main { get; }

    /// <summary>What the Close-up draws now.</summary>
    public CloseUpState State { get; private set; } = new();

    /// <summary>The source time now (s after bounce): observer time ÷ (1 + z).</summary>
    public double SourceTimeS => Main.Timeline.ObserverTimeS / Main.CurrentObserver.TimeDilation;

    /// <summary>Raised after <see cref="State"/> is restated: true when the scene was rebuilt (a new outcome, observer or run), false for a time step.</summary>
    public event EventHandler<bool>? StateChanged;

    public string Title => State.Title;

    /// <summary>The window's title: "Close-up: Black hole".</summary>
    public string WindowTitle => $"Close-up: {State.Title}";

    public IReadOnlyList<ReadoutRow> Readouts { get; private set; } = Array.Empty<ReadoutRow>();

    /// <summary>
    /// The scene notes (or the model error in their place), and while something is lensed the light-bending note (ruling R-P3-9):
    /// "Light bending: half resolution (your setting)", or "(automatic)" for Auto.
    /// </summary>
    public IReadOnlyList<string> Notes => _modelError is not null ? new[] { _modelError }
        : State.IsLensed ? State.Notes.Append(LightBendingNote).ToList()
        : State.Notes;

    /// <summary>The light-bending note for the level shown (or, before the view has drawn, the level it settles at).</summary>
    public string LightBendingNote
        => LensingDetails.Note(ShownLensingLevel ?? LensingDetails.StillLevel(LensingDetail), LensingDetail == LensingDetail.Auto);

    /// <summary>
    /// The user's light-bending detail (Close-up menu ▸ Light-bending detail, spec §2.5): kept in the main window's layout, so it is
    /// saved to layout.json and outlives this window.
    /// </summary>
    public LensingDetail LensingDetail
    {
        get => Main.Layout.CloseUpLensingDetail;
        set => Main.Layout.CloseUpLensingDetail = value;   // OnLayoutChanged raises the change
    }

    private LensingLevel? _shownLensingLevel;

    /// <summary>The level of the light bending on screen now, set by the view's controller (null before it has drawn).</summary>
    public LensingLevel? ShownLensingLevel
    {
        get => _shownLensingLevel;
        set
        {
            if (_shownLensingLevel == value) return;
            _shownLensingLevel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Notes));
        }
    }

    /// <summary>The menu's radio items (as the main view's Quality items): true chooses that detail; false on the checked one keeps it.</summary>
    public bool IsDetailAuto { get => LensingDetail == LensingDetail.Auto; set => ChooseDetail(value, LensingDetail.Auto, nameof(IsDetailAuto)); }
    public bool IsDetailFull { get => LensingDetail == LensingDetail.Full; set => ChooseDetail(value, LensingDetail.Full, nameof(IsDetailFull)); }
    public bool IsDetailHalf { get => LensingDetail == LensingDetail.Half; set => ChooseDetail(value, LensingDetail.Half, nameof(IsDetailHalf)); }
    public bool IsDetailQuarter { get => LensingDetail == LensingDetail.Quarter; set => ChooseDetail(value, LensingDetail.Quarter, nameof(IsDetailQuarter)); }

    private void ChooseDetail(bool on, LensingDetail detail, string property)
    {
        if (on) LensingDetail = detail;
        else OnPropertyChanged(property);   // a click on the checked item: it stays checked
    }

    private void OnLayoutChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LayoutViewModel.CloseUpLensingDetail)) return;
        OnPropertyChanged(nameof(LensingDetail));
        OnPropertyChanged(nameof(IsDetailAuto));
        OnPropertyChanged(nameof(IsDetailFull));
        OnPropertyChanged(nameof(IsDetailHalf));
        OnPropertyChanged(nameof(IsDetailQuarter));
        OnPropertyChanged(nameof(Notes));
    }

    /// <summary>Spec §2.3: why there is nothing to show; null when there is a compact remnant.</summary>
    public string? NoRemnantMessage => State.NoRemnantMessage;

    public bool HasRemnant => State.NoRemnantMessage is null;

    /// <summary>The view's label (spec §6.5): shown whenever a remnant is drawn.</summary>
    public string NoTelescopeNote => CloseUpScene.NoTelescopeNote;

    /// <summary>Spec §6.3: the measuring grid round a black hole (drawn from Task 10).</summary>
    [ObservableProperty] private bool _showGrid;
    /// <summary>Spec §4.3: a neutron star's magnetic field lines.</summary>
    [ObservableProperty] private bool _showFieldLines;
    /// <summary>Spec §4.3: a pulsar's light cylinder.</summary>
    [ObservableProperty] private bool _showLightCylinder;
    /// <summary>Spec §4.2: the neutron star's cutaway (its layers).</summary>
    [ObservableProperty] private bool _isCutaway;
    /// <summary>The view's labels (the distance rings' labels, and later the grid's).</summary>
    [ObservableProperty] private bool _showLabels = true;
    /// <summary>F: the view alone, without the readout column.</summary>
    [ObservableProperty] private bool _isFocusView;
    /// <summary>The readout column's "Notes ▾" expander.</summary>
    [ObservableProperty] private bool _isNotesExpanded;

    [RelayCommand]
    private void ToggleFocus() => IsFocusView = !IsFocusView;

    partial void OnShowGridChanged(bool value) => StateChanged?.Invoke(this, false);
    partial void OnIsCutawayChanged(bool value) => StateChanged?.Invoke(this, false);
    partial void OnShowLabelsChanged(bool value) => StateChanged?.Invoke(this, false);

    private double _benchSpin;

    /// <summary>The --bench-spin diagnostic: the placeholder black hole's spin (0 by default). Setting it rebuilds the scene.</summary>
    public double BenchSpin
    {
        get => _benchSpin;
        set
        {
            if (_benchSpin == value) return;
            _benchSpin = value;
            Rebuild();
        }
    }

    /// <summary>How many model errors the Close-up has caught (the sweep counts them).</summary>
    public int ModelErrorCount { get; private set; }

    private void OnOutcomeChanged(object? sender, EventArgs e) => Rebuild();

    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.RunNumber)) Rebuild();
    }

    private void OnTimelineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TimelineViewModel.ObserverTimeS)) return;
        ComputeState();
        StateChanged?.Invoke(this, false);
    }

    private void Rebuild()
    {
        if (_disposed) return;
        BuildScene();
        ComputeState();
        StateChanged?.Invoke(this, true);
    }

    private void BuildScene()
    {
        try
        {
            _scene = new CloseUpScene(Main.CurrentOutcome, Main.CurrentObserver, Main.RunNumber, _benchSpin);
            _modelError = null;
        }
        catch (Exception e)
        {
            _scene = null;
            _modelError = ModelErrorText(e);
        }
    }

    private void ComputeState()
    {
        var before = (State, _modelError);
        if (_scene is not null)
        {
            try
            {
                State = _scene.At(SourceTimeS);
                _modelError = null;
            }
            catch (Exception e)
            {
                _modelError = ModelErrorText(e);
            }
        }
        // A time step that leaves the state as it was (the same record) changes nothing on screen: no rebinding at 60 Hz.
        if (ReferenceEquals(before.State, State) && before._modelError == _modelError && Readouts.Count == State.Readouts.Count) return;
        Readouts = State.Readouts.Select(ReadoutRow.From).ToList();
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(Readouts));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(NoRemnantMessage));
        OnPropertyChanged(nameof(HasRemnant));
    }

    private string ModelErrorText(Exception e)
    {
        ModelErrorCount++;
        Trace.TraceError($"Close-up: {e}");
        return $"Close-up: model error ({e.GetType().Name}: {e.Message}). The other views are unaffected.";
    }

    /// <summary>Stops following the main view model (the window closed).</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Main.OutcomeChanged -= OnOutcomeChanged;
        Main.Timeline.PropertyChanged -= OnTimelineChanged;
        Main.PropertyChanged -= OnMainChanged;
        Main.Layout.PropertyChanged -= OnLayoutChanged;
    }
}
