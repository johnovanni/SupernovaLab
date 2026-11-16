using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SupernovaLab.App.Services;
using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;
using PulsarModel = SupernovaLab.Core.Signals.PulsarModel;
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
        return new ReadoutRow(r.Label, LabelTip(r), r.ValueText, q.Confidence, q.BadgeToolTip, q.Published, q.PublishedToolTip);
    }

    /// <summary>
    /// Task 14 (glossary): a label that names a term (Horizon, Shadow, Gain radius, …) explains it first, then the readout's own note.
    /// </summary>
    public static string? LabelTip(Readout r)
    {
        string? term = Core.Text.Glossary.CloseUpReadoutTerms.TryGetValue(r.Label, out var key) ? Core.Text.Glossary.Get(key) : null;
        if (term is null) return r.ToolTip;
        return string.IsNullOrEmpty(r.ToolTip) ? term : term + Environment.NewLine + Environment.NewLine + r.ToolTip;
    }
}

/// <summary>One line of the cutaway's key (spec §4.2): a schematic colour, the layer's label, and its confidence qualifier (empty for none).</summary>
public sealed record CutawayKeyRow(string Label, string ColorHex, string ToolTip, string Qualifier);

/// <summary>One piece of the magnified strip, placed in DIP from the strip's left (deep) end.</summary>
public sealed record CutawayStripPiece(double XDip, double WidthDip, string ColorHex, string ToolTip);

/// <summary>
/// Task 14b: one line of the readout column as the window binds it. The window's rows are made once and updated in place, a field at a
/// time, so a time step that changes a value changes only that text: replacing the bound list made WPF build every row's elements again
/// (its grid, texts and badges) on every frame, about 7 to 9 ms of the UI thread's layout per time step with a neutron star or a growing
/// black hole, and that cost ran while the window was hidden too. It shows exactly its <see cref="Row"/>'s fields.
/// </summary>
public sealed class ReadoutRowView : ObservableObject
{
    private ReadoutRow _row;

    public ReadoutRowView(ReadoutRow row) => _row = row ?? throw new ArgumentNullException(nameof(row));

    /// <summary>The row shown now.</summary>
    public ReadoutRow Row => _row;

    public string Label => _row.Label;
    public string? LabelToolTip => _row.LabelToolTip;
    public string ValueText => _row.ValueText;
    public Confidence Confidence => _row.Confidence;
    public string BadgeToolTip => _row.BadgeToolTip;
    public bool Published => _row.Published;
    public string PublishedToolTip => _row.PublishedToolTip;

    /// <summary>Shows <paramref name="row"/>: raises a change for each field that differs, and none when the row is the same.</summary>
    public void Set(ReadoutRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var old = _row;
        if (old == row) return;
        _row = row;
        if (old.Label != row.Label) OnPropertyChanged(nameof(Label));
        if (old.LabelToolTip != row.LabelToolTip) OnPropertyChanged(nameof(LabelToolTip));
        if (old.ValueText != row.ValueText) OnPropertyChanged(nameof(ValueText));
        if (old.Confidence != row.Confidence) OnPropertyChanged(nameof(Confidence));
        if (old.BadgeToolTip != row.BadgeToolTip) OnPropertyChanged(nameof(BadgeToolTip));
        if (old.Published != row.Published) OnPropertyChanged(nameof(Published));
        if (old.PublishedToolTip != row.PublishedToolTip) OnPropertyChanged(nameof(PublishedToolTip));
    }

    /// <summary>
    /// Makes <paramref name="views"/> show <paramref name="rows"/>, in order: the rows both have are updated in place, extra views are
    /// removed from the end and missing ones added at the end. A step that changes only values adds and removes nothing.
    /// </summary>
    public static void Sync(System.Collections.ObjectModel.ObservableCollection<ReadoutRowView> views, IReadOnlyList<ReadoutRow> rows)
    {
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(rows);
        int shared = Math.Min(views.Count, rows.Count);
        for (int i = 0; i < shared; i++) views[i].Set(rows[i]);
        while (views.Count > rows.Count) views.RemoveAt(views.Count - 1);
        for (int i = views.Count; i < rows.Count; i++) views.Add(new ReadoutRowView(rows[i]));
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

    /// <param name="main">The shared lab state.</param>
    /// <param name="audioFactory">Makes the pulse sound when it is first switched on (plan Task 8); null: the window has no sound.</param>
    public CloseUpViewModel(MainViewModel main, Func<IPulseAudio>? audioFactory = null)
    {
        Main = main ?? throw new ArgumentNullException(nameof(main));
        _audioFactory = audioFactory;
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

    /// <summary>The source time now (s after bounce): observer time ÷ (1 + z); 0 at the timeline's left end, t = 0 (ruling R-P3-42), where the
    /// Close-up shows the core before collapse.</summary>
    public double SourceTimeS => Main.Timeline.IsAtStart ? 0 : Main.Timeline.ObserverTimeS / Main.CurrentObserver.TimeDilation;

    /// <summary>Raised after <see cref="State"/> is restated: true when the scene was rebuilt (a new outcome, observer or run), false for a time step.</summary>
    public event EventHandler<bool>? StateChanged;

    public string Title => State.Title;

    /// <summary>The window's title: "Close-up: Black hole".</summary>
    public string WindowTitle => $"Close-up: {State.Title}";

    public IReadOnlyList<ReadoutRow> Readouts { get; private set; } = Array.Empty<ReadoutRow>();

    /// <summary>Task 14b: what the window's readout column binds: <see cref="Readouts"/>, its rows kept and updated in place.</summary>
    public System.Collections.ObjectModel.ObservableCollection<ReadoutRowView> ReadoutViews { get; } = new();

    /// <summary>Task 14b: what the window's cutaway rows bind: <see cref="CutawayReadouts"/>, kept and updated in place.</summary>
    public System.Collections.ObjectModel.ObservableCollection<ReadoutRowView> CutawayReadoutViews { get; } = new();

    /// <summary>
    /// The scene notes (or the model error in their place), and while something is lensed the light-bending note (ruling R-P3-9):
    /// "Light bending: half resolution (your setting)", or "(automatic)" for Auto.
    /// </summary>
    public IReadOnlyList<string> Notes => _modelError is not null ? new[] { _modelError }
        : State.IsLensed ? WithFlareLimit(State.Notes.Concat(ActiveCutaway?.Notes ?? Array.Empty<string>()).Append(LightBendingNote)).ToList()
        : WithFlareLimit(State.Notes).ToList();

    /// <summary>Review fix 5: shown while the flash limiter holds a giant flare clearly below what the model asks for.</summary>
    public const string FlareDimmedNote = "Flare dimmed by the flash-safety limit (at most one rise per half second, none faster than 0.15 s)";

    private bool _flareDimmed;

    /// <summary>Set by the view's controller each drawn frame: the limiter is dimming the flare now.</summary>
    public bool FlareDimmed
    {
        get => _flareDimmed;
        set
        {
            if (_flareDimmed == value) return;
            _flareDimmed = value;
            OnPropertyChanged(nameof(Notes));
        }
    }

    private IEnumerable<string> WithFlareLimit(IEnumerable<string> notes) => _flareDimmed ? notes.Prepend(FlareDimmedNote) : notes;

    /// <summary>The light-bending note for the level shown (or, before the view has drawn, the level it settles at).</summary>
    public string LightBendingNote
        => LensingDetails.Note(ShownLensingLevel ?? LensingDetails.StillLevel(LensingDetail), LensingDetail == LensingDetail.Auto)
           + (RefineFailed ? "; it could not be refined further on this computer's graphics" : "")
           + (TracingDirectly ? "; traced afresh every frame (its cache failed on this computer's graphics), so it may be slower" : "");

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
    /// <summary>Spec §4.3: a neutron star's magnetic field lines (on by default, plan Task 6).</summary>
    [ObservableProperty] private bool _showFieldLines = true;
    /// <summary>Spec §4.3: a pulsar's light cylinder (on by default; it lies far outside the default framing, so zoom out to see it).</summary>
    [ObservableProperty] private bool _showLightCylinder = true;
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
    partial void OnIsCutawayChanged(bool value)
    {
        UpdateCutaway();
        StateChanged?.Invoke(this, false);
    }

    // ---- Plan Task 7: the neutron star's cutaway (spec §4.2) ----

    /// <summary>The magnified strip's length, DIP.</summary>
    public const double StripWidthDip = 250;

    /// <summary>The cutaway being shown: the neutron star's, while the Cutaway switch is on; null otherwise.</summary>
    public CutawayInfo? ActiveCutaway => IsCutaway && HasRemnant && State.NeutronStar is not null ? State.Cutaway : null;

    /// <summary>The cutaway's key (empty with it off).</summary>
    public IReadOnlyList<CutawayKeyRow> CutawayKey { get; private set; } = Array.Empty<CutawayKeyRow>();

    /// <summary>The magnified strip's pieces, deep to the surface (empty with the cutaway off or before the crust forms).</summary>
    public IReadOnlyList<CutawayStripPiece> CutawayStrip { get; private set; } = Array.Empty<CutawayStripPiece>();

    /// <summary>The numbers the column adds while the cutaway is on, each with its confidence and source.</summary>
    public IReadOnlyList<ReadoutRow> CutawayReadouts { get; private set; } = Array.Empty<ReadoutRow>();

    /// <summary>The strip's drip-line marker, DIP from its left end (NaN: none).</summary>
    public double CutawayDripXDip { get; private set; } = double.NaN;

    public string? MagnifiedLabel => ActiveCutaway?.MagnifiedLabel;
    public string? DripLabel => ActiveCutaway?.DripLabel;
    public string? CutawayStateLabel => ActiveCutaway?.StateLabel;
    public bool ShowCutawayKey => ActiveCutaway is not null;
    public bool ShowStrip => ActiveCutaway is { HasCrust: true };

    private CutawayInfo? _shownCutaway;

    private void UpdateCutaway()
    {
        var info = ActiveCutaway;
        var rows = info is null ? Array.Empty<ReadoutRow>() : info.Readouts.Select(ReadoutRow.From).ToArray();
        if (!rows.SequenceEqual(CutawayReadouts))
        {
            CutawayReadouts = rows;
            ReadoutRowView.Sync(CutawayReadoutViews, rows);   // Task 14b: in place, as the column's rows
            OnPropertyChanged(nameof(CutawayReadouts));
        }
        if (ReferenceEquals(info, _shownCutaway)) return;
        _shownCutaway = info;
        CutawayKey = info is null ? Array.Empty<CutawayKeyRow>() : info.Legend.Select(KeyRow).ToArray();
        CutawayStrip = info is null ? Array.Empty<CutawayStripPiece>()
            : info.Strip.Select(p => new CutawayStripPiece(p.StartFraction * StripWidthDip, (p.EndFraction - p.StartFraction) * StripWidthDip,
                p.ColorHex, p.ToolTip)).ToArray();
        CutawayDripXDip = info is { HasCrust: true } ? info.DripStripFraction * StripWidthDip : double.NaN;
        foreach (var name in new[] { nameof(CutawayKey), nameof(CutawayStrip), nameof(CutawayDripXDip), nameof(MagnifiedLabel), nameof(DripLabel),
                     nameof(CutawayStateLabel), nameof(ShowCutawayKey), nameof(ShowStrip), nameof(ActiveCutaway), nameof(Notes) })
            OnPropertyChanged(name);
    }

    private static CutawayKeyRow KeyRow(CutawayLegendEntry e) => new(e.Label, e.ColorHex, e.ToolTip,
        e.ConfidenceText ?? e.Confidence switch { Confidence.ModelDependent => "model-dependent", Confidence.Speculative => "speculative", _ => "" });
    partial void OnShowLabelsChanged(bool value) => StateChanged?.Invoke(this, false);
    // Plan Task 6 (ruling R-P3-11's condition): the field lines and the light cylinder are drawn, so their switches redraw the view.
    partial void OnShowFieldLinesChanged(bool value) => StateChanged?.Invoke(this, false);
    partial void OnShowLightCylinderChanged(bool value) => StateChanged?.Invoke(this, false);

    /// <summary>The view's speed label (spec §2.1, §4.3): "Shown 60× slower" for a neutron star; null for anything else.</summary>
    public string? SpeedLabel => State.SpeedLabel;

    /// <summary>Plan Task 9 (spec §3.2): the stage's label on the view ("From simulations · typical track"); null outside the core's eras.</summary>
    public string? StageLabel => State.StageLabel;

    /// <summary>Plan Task 9: the stage's key on the view, one line per drawn thing (the proto-neutron star, the shock, the gain shell, the infall).</summary>
    public IReadOnlyList<string> StageKey => State.StageKey;

    private bool _refineFailed;

    /// <summary>
    /// Set by the view's controller when the finer light-bending map could not be built on this graphics card (Task 4b review
    /// carry-over: a band that always fails is given up, not retraced forever); the light-bending note then says so.
    /// </summary>
    public bool RefineFailed
    {
        get => _refineFailed;
        set
        {
            if (_refineFailed == value) return;
            _refineFailed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Notes));
        }
    }

    private bool _tracingDirectly;

    /// <summary>
    /// Set by the view's controller when the cached ray map keeps failing and the light bending is traced afresh every frame instead
    /// (fix round 1: the fallback is said, as the given-up refinement is).
    /// </summary>
    public bool TracingDirectly
    {
        get => _tracingDirectly;
        set
        {
            if (_tracingDirectly == value) return;
            _tracingDirectly = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Notes));
        }
    }

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
        long t0 = BenchTimings.Begin();
        ComputeState();
        StateChanged?.Invoke(this, false);
        BenchTimings.End(BenchTimings.CloseUpViewModel, t0);
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
        ComputeStateCore();
        UpdatePulses();
    }

    private void ComputeStateCore()
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
        // Plan Task 6: a neutron star's state is new at every time step; the column is rebound only when a row's text changes.
        var rows = State.Readouts.Select(ReadoutRow.From).ToList();
        bool rowsChanged = !rows.SequenceEqual(Readouts);
        if (rowsChanged)
        {
            Readouts = rows;
            ReadoutRowView.Sync(ReadoutViews, rows);   // Task 14b: the window's rows change in place, not rebuilt
        }
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(SpeedLabel));
        if (before.State.StageLabel != State.StageLabel) OnPropertyChanged(nameof(StageLabel));
        if (!before.State.StageKey.SequenceEqual(State.StageKey)) OnPropertyChanged(nameof(StageKey));
        if (rowsChanged) OnPropertyChanged(nameof(Readouts));
        if (before._modelError != _modelError || !before.State.Notes.SequenceEqual(State.Notes)) OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(NoRemnantMessage));
        OnPropertyChanged(nameof(HasRemnant));
        UpdateCutaway();
    }

    private string ModelErrorText(Exception e)
    {
        ModelErrorCount++;
        Trace.TraceError($"Close-up: {e}");
        return $"Close-up: model error ({e.GetType().Name}: {e.Message}). The other views are unaffected.";
    }

    // ---- Plan Task 8: the pulse trace and the optional pulsar sound (spec §4.4) ----

    private readonly Func<IPulseAudio>? _audioFactory;
    private IPulseAudio? _audio;
    private PulseVisibility _pulseVisibility = PulseVisibility.NotAPulsar;
    private string _traceText = "";
    private string _speakerState = "Sound off";
    private string? _audioProblem;
    private double[] _crossings = Array.Empty<double>();
    private (Outcome Outcome, ObserverSetup Observer)? _crossingsFor;

    /// <summary>
    /// Spec C19: the sound is off by default, so nothing sounds unasked (Close-up menu ▸ Sound). Not saved between runs; Task 15: the window
    /// service keeps it (and the volume) for the session, so closing and reopening the Close-up does not switch it off.
    /// </summary>
    [ObservableProperty] private bool _isSoundOn;

    /// <summary>The sound's loudness, 0 to 1 (Task 15: 80% by default; it was 50%, too quiet for a hard-of-hearing listener).</summary>
    [ObservableProperty] private float _volume = ClickTrain.DefaultVolume;

    partial void OnIsSoundOnChanged(bool value)
    {
        if (value && _audio is null && !_disposed) CreateAudio();
        if (_audio is not null) _audio.Enabled = value;
        // Task 16 (the owner's second look): a test tone, so switching on is heard even when no pulses reach Earth (at t = 0, before
        // about 10 years, a beam that misses Earth, no pulsar), which was most of the time.
        if (value) _audio?.PlayTestTone();
        UpdatePulses();
        OnPropertyChanged(nameof(SpeakerGlyph));
        OnPropertyChanged(nameof(ShowSoundStatus));
    }

    /// <summary>
    /// Task 15 (the owner could not hear the sound, and saw no sign of it): while the sound is on, its state and the reason for any silence
    /// show at the top of the window for every outcome (<see cref="SpeakerState"/>), not only in the pulse-trace row, which a black hole,
    /// a Type Ia or the core's first second does not have.
    /// </summary>
    public bool ShowSoundStatus => IsSoundOn;

    /// <summary>The sound output, once the sound has been switched on (the --snapshot sound check reads its diagnostic).</summary>
    public IPulseAudio? Audio => _audio;

    partial void OnVolumeChanged(float value)
    {
        if (_audio is not null) _audio.Volume = value;
        if (IsSoundOn) _audio?.PlayTestTone();   // Task 16: the new volume is heard at once; a drag runs one tone on, not many
        UpdatePulses();
    }

    private void CreateAudio()
    {
        if (_audioFactory is null)
        {
            _audioProblem = "this window has no sound output";
            return;
        }
        try
        {
            _audio = _audioFactory();
            _audio.Volume = Volume;
            _audioProblem = null;
        }
        catch (Exception e)
        {
            Trace.TraceError($"Close-up sound: {e}");
            _audioProblem = "no audio output device";
        }
    }

    /// <summary>Whether the trace row shows: the remnant is a pulsar or magnetar (a black hole has none).</summary>
    public bool ShowPulseTrace => _pulseVisibility != PulseVisibility.NotAPulsar;

    /// <summary>True while pulses reach Earth (beam toward Earth, out of the debris): the trace draws, and the Close-up keeps animating (R-P3-11).</summary>
    public bool PulsesShown => _pulseVisibility == PulseVisibility.Received;

    /// <summary>The trace's words: both rates, or the reason there are no pulses (spec §4.4).</summary>
    public string TraceText => _traceText;

    /// <summary>The spin phases at which a drawn beam crosses the Earth arrow (for the trace's marks); empty when none can.</summary>
    public IReadOnlyList<double> CrossingPhasesRad => _crossings;

    /// <summary>The speaker symbol's text: Segoe MDL2 volume or mute.</summary>
    public string SpeakerGlyph => IsSoundOn ? "" : "";

    /// <summary>The speaker's state in words, with its volume and, when it is silent, why: nothing depends on hearing it.</summary>
    public string SpeakerState => _speakerState;

    private void UpdatePulses()
    {
        if (_disposed) return;
        var o = Main.CurrentOutcome;
        var obs = Main.CurrentObserver;
        double t = SourceTimeS;
        var vis = State.NeutronStar is null || _modelError is not null ? PulseVisibility.NotAPulsar : PulseSchedule.Visibility(o, obs, t);
        string text = vis == PulseVisibility.NotAPulsar ? "" : PulseSchedule.TraceText(o, obs, t);
        // Task 15: the sound plays whenever Earth receives the pulses, paused or playing (it was only while the timeline played, so a
        // whole run gave about 6 s of clicks, and none at a paused moment). It agrees with the pulse trace, which keeps running while paused.
        bool sounding = IsSoundOn && vis == PulseVisibility.Received;
        double period = sounding ? PulseSchedule.ReceivedPeriodS(o, obs, t) : double.NaN;
        _audio?.SetPulsePeriod(sounding ? period : null);

        string problem = (IsSoundOn ? _audioProblem ?? _audio?.Problem : null) ?? "";
        string speaker = !IsSoundOn ? "Sound off"
            : problem.Length > 0 ? $"Sound on, but unavailable: {problem}"
            : $"Sound on, volume {(int)Math.Round(Volume * 100)}% · " + (sounding ? $"clicking {PulseSchedule.RateText(period)}" : SilentReason(vis, o, obs));

        var changed = new List<string>();
        if (vis != _pulseVisibility)
        {
            bool shownBefore = ShowPulseTrace, pulsesBefore = PulsesShown;
            _pulseVisibility = vis;
            if (shownBefore != ShowPulseTrace) changed.Add(nameof(ShowPulseTrace));
            if (pulsesBefore != PulsesShown) changed.Add(nameof(PulsesShown));
        }
        if (vis != PulseVisibility.Received)
        {
            if (_crossings.Length > 0) (_crossings, _crossingsFor) = (Array.Empty<double>(), null);
        }
        else if (!ReferenceEquals(_crossingsFor?.Outcome, o) || _crossingsFor.Value.Observer != obs)
            (_crossings, _crossingsFor) = (PulseSchedule.CrossingPhasesRad(o, obs).ToArray(), (o, obs));
        if (text != _traceText) { _traceText = text; changed.Add(nameof(TraceText)); }
        if (speaker != _speakerState) { _speakerState = speaker; changed.Add(nameof(SpeakerState)); changed.Add(nameof(SpeakerStateToolTip)); }
        foreach (var name in changed) OnPropertyChanged(name);
    }

    private static readonly PulsarModel PulsarForEmergence = new();

    /// <summary>Task 15: why the switched-on sound is silent now, in words, for every outcome and moment.</summary>
    private static string SilentReason(PulseVisibility vis, Outcome o, ObserverSetup obs)
    {
        double emergence = PulsarForEmergence.EmergenceTimeS(o);
        string when = double.IsNaN(emergence) ? "" : UnitFormat.Time(emergence * obs.TimeDilation);
        return vis switch
        {
            PulseVisibility.HiddenByDebris => $"silent: pulses hidden by debris until about {when} (a speculative estimate)",
            PulseVisibility.BeamMissesEarth => "silent: the pulsar's beam misses Earth",
            _ when double.IsNaN(emergence) => "silent: no pulsar in this outcome (the sound is a pulsar's clicks)",
            _ when PulseSchedule.Visibility(o, obs, emergence) == PulseVisibility.BeamMissesEarth
                => "silent: no pulsar yet, and its beam will miss Earth",
            _ => $"silent until the pulsar's pulses reach Earth (about {when}, a speculative estimate)",
        };
    }

    /// <summary>The pulsar model's source for when the pulses escape the debris (its "Pulses shown from 10 yr" entry).</summary>
    private static readonly Source EmergenceSource = PulsarForEmergence.Sources.FirstOrDefault(s => s.Citation.StartsWith("Pulses shown", StringComparison.Ordinal))
        ?? PulsarForEmergence.Sources[0];

    /// <summary>
    /// Task 15 fix round: the status line's tooltip while it names when the pulses reach Earth, that number's confidence and source (every
    /// shown number keeps them); null otherwise.
    /// </summary>
    public string? SpeakerStateToolTip => _speakerState.Contains("speculative estimate", StringComparison.Ordinal)
        ? $"When the pulses escape the debris and reach Earth: the lab's pulsar model starts them {UnitFormat.Time(PulsarModel.EmergenceYears * PhysicalConstants.Year)} " +
          $"after core bounce (stretched by 1 + z for a distant supernova). Confidence: Speculative. Source: {EmergenceSource.Citation} ({EmergenceSource.ResearchRef})."
        : null;

    /// <summary>Stops following the main view model (the window closed), and stops and releases the sound.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_audio is not null)
        {
            try
            {
                _audio.SetPulsePeriod(null);
                _audio.Enabled = false;
                _audio.Dispose();
            }
            catch (Exception e) { Trace.TraceError($"Close-up sound: {e}"); }
            _audio = null;
        }
        Main.OutcomeChanged -= OnOutcomeChanged;
        Main.Timeline.PropertyChanged -= OnTimelineChanged;
        Main.PropertyChanged -= OnMainChanged;
        Main.Layout.PropertyChanged -= OnLayoutChanged;
    }
}
