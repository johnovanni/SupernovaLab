using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Core.Units;

namespace SupernovaLab.App.ViewModels;

/// <summary>
/// The base of the six instrument panels (spec §5.1). A panel samples its models on the 400-point log grid (spec §7)
/// only when the outcome, observer, run number, instrument or sub-tab changes (<see cref="Update"/>); moving the time
/// only moves the cursor, refreshes <see cref="CurrentDetection"/> and the readouts, and (on the spectrum and pulsar
/// sub-tabs) the view at the current time. Spec §9: any exception or infinite value from a model is caught here, logged
/// with <see cref="Debug.WriteLine(string)"/> and shown as "model error" in this panel only.
/// </summary>
public abstract partial class InstrumentPanelViewModel : ObservableObject
{
    /// <summary>The detection badge's words (spec §5.1).</summary>
    public const string ModelErrorText = "Model error";

    private static readonly double[] ObserverGrid = TimeAxis.LogGrid();

    private Outcome? _outcome;
    private ObserverSetup _observer = ObserverSetup.Default;
    private int _runNumber = 1;
    private IReadOnlyList<ObservedFact> _presetFacts = Array.Empty<ObservedFact>();
    private bool _factsMeasuredHere = true;
    private double _timeS = TimeAxis.MinS;
    private string? _sampleError;
    private string? _timeError;
    private readonly Dictionary<(string Model, string Detector, double T), Detection> _detections = new();

    protected InstrumentPanelViewModel(IReadOnlyList<string> subTabs, IReadOnlyList<Detector> instruments)
    {
        if (subTabs.Count == 0) throw new ArgumentException("A panel needs at least one sub-tab.", nameof(subTabs));
        if (instruments.Count == 0) throw new ArgumentException("A panel needs at least one instrument.", nameof(instruments));
        SubTabs = subTabs;
        Instruments = instruments;
        InstrumentChoices = instruments.Select(Choice).ToList();
        _selectedSubTab = subTabs[0];
        _selectedInstrument = instruments[0];
        _currentDetection = _stripDetection = new Detection(DetectionStatus.NotModeled, "No outcome yet.");
    }

    // ---------------------------------------------------------------- identity

    public abstract Messenger Messenger { get; }

    /// <summary>
    /// Whether the guided tour is pointing at this instrument (Task 12): its outline frame is drawn 2 px in the accent
    /// brush. <see cref="MainViewModel.Highlight"/> sets it on exactly one panel, or none.
    /// </summary>
    [ObservableProperty] private bool _isHighlighted;

    /// <summary>The panel's title, e.g. "Neutrinos".</summary>
    public abstract string Title { get; }

    /// <summary>The panel's plain-language tooltip (a glossary text).</summary>
    public virtual string TitleToolTip => Glossary.Instruments;

    /// <summary>Whether the main series (the tile's sparkline) and this panel's magnitude plots run brighter-up.</summary>
    public virtual bool MainSeriesInvertY => false;

    // ---------------------------------------------------------------- choices

    public IReadOnlyList<string> SubTabs { get; }

    private string _selectedSubTab;

    /// <summary>The selected sub-tab; changing it resamples.</summary>
    public string SelectedSubTab
    {
        get => _selectedSubTab;
        set
        {
            if (value is null || !SubTabs.Contains(value) || value == _selectedSubTab) return;
            _selectedSubTab = value;
            OnPropertyChanged();
            OnUserChoice();
            Resample(main: false);
        }
    }

    public IReadOnlyList<Detector> Instruments { get; }

    /// <summary>The instruments with their limits, for the picker.</summary>
    public IReadOnlyList<InstrumentChoice> InstrumentChoices { get; }

    private Detector _selectedInstrument;

    /// <summary>The selected instrument; changing it resamples (some views draw its limit or its own channel).</summary>
    public Detector SelectedInstrument
    {
        get => _selectedInstrument;
        set
        {
            if (value is null || !Instruments.Contains(value) || ReferenceEquals(value, _selectedInstrument)) return;
            _selectedInstrument = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedInstrumentChoice));
            OnUserChoice();
            Resample(main: false);
        }
    }

    /// <summary>The picker's binding: the choice for <see cref="SelectedInstrument"/>.</summary>
    public InstrumentChoice SelectedInstrumentChoice
    {
        get => InstrumentChoices.First(c => ReferenceEquals(c.Detector, SelectedInstrument));
        set
        {
            if (value is not null) SelectedInstrument = value.Detector;
        }
    }

    /// <summary>Called when the user picks a sub-tab or an instrument (a panel may stop choosing them itself).</summary>
    protected virtual void OnUserChoice() { }

    /// <summary>
    /// For the --snapshot diagnostic: shows a sub-tab without counting as the user's choice, so a panel that picks its own
    /// view (the gamma panel) keeps doing so for the next preset.
    /// </summary>
    internal void ShowSubTabForSnapshot(string subTab)
    {
        if (!SubTabs.Contains(subTab) || subTab == _selectedSubTab) return;
        Preselect(subTab, SelectedInstrument);
        Resample(main: false);
    }

    /// <summary>Task 14: the --snapshot diagnostic's sub-tab with a chosen instrument (the gamma panel's burst tab with Fermi GBM).</summary>
    internal void ShowSubTabForSnapshot(string subTab, Detector instrument)
    {
        Preselect(subTab, instrument);
        Resample(main: false);
    }

    /// <summary>Sets the sub-tab and instrument without counting as the user's choice (no resample: the caller resamples).</summary>
    protected void Preselect(string subTab, Detector instrument)
    {
        if (SubTabs.Contains(subTab) && subTab != _selectedSubTab)
        {
            _selectedSubTab = subTab;
            OnPropertyChanged(nameof(SelectedSubTab));
        }
        if (Instruments.Contains(instrument) && !ReferenceEquals(instrument, _selectedInstrument))
        {
            _selectedInstrument = instrument;
            OnPropertyChanged(nameof(SelectedInstrument));
            OnPropertyChanged(nameof(SelectedInstrumentChoice));
        }
    }

    // ---------------------------------------------------------------- what the plot shows

    private IReadOnlyList<PlotSeries> _series = Array.Empty<PlotSeries>();
    public IReadOnlyList<PlotSeries> Series { get => _series; private set => SetProperty(ref _series, value); }
    private IReadOnlyList<PlotBand> _bands = Array.Empty<PlotBand>();
    public IReadOnlyList<PlotBand> Bands { get => _bands; private set => SetProperty(ref _bands, value); }
    private IReadOnlyList<ReferenceLine> _referenceLines = Array.Empty<ReferenceLine>();
    public IReadOnlyList<ReferenceLine> ReferenceLines { get => _referenceLines; private set => SetProperty(ref _referenceLines, value); }
    private PlotKind _kind;
    public PlotKind Kind { get => _kind; private set => SetProperty(ref _kind, value); }
    private string _yLabel = "";
    public string YLabel { get => _yLabel; private set => SetProperty(ref _yLabel, value); }
    private bool _logY;
    public bool LogY { get => _logY; private set => SetProperty(ref _logY, value); }
    private bool _invertY;
    public bool InvertY { get => _invertY; private set => SetProperty(ref _invertY, value); }
    private SpectrogramView? _spectrogram;
    public SpectrogramView? Spectrogram { get => _spectrogram; private set => SetProperty(ref _spectrogram, value); }
    private SpectrumView? _spectrum;
    public SpectrumView? Spectrum { get => _spectrum; protected set => SetProperty(ref _spectrum, value); }
    private PulseTrainView? _pulseTrain;
    public PulseTrainView? PulseTrain { get => _pulseTrain; protected set => SetProperty(ref _pulseTrain, value); }

    private IReadOnlyList<PlotSeries> _mainSeries = Array.Empty<PlotSeries>();
    /// <summary>The tile's sparkline (spec §5.0), resampled only when the outcome, observer or run changes.</summary>
    public IReadOnlyList<PlotSeries> MainSeries { get => _mainSeries; private set => SetProperty(ref _mainSeries, value); }

    private string? _panelNote;
    /// <summary>A standing note about the selected view (e.g. "1 dot = 2,400 events"), or null.</summary>
    public string? PanelNote { get => _panelNote; private set => SetProperty(ref _panelNote, value); }

    private string? _plotNote;
    /// <summary>Why nothing is drawn at the current time ("not modeled here", spec §9), or null.</summary>
    public string? PlotNote { get => _plotNote; private set => SetProperty(ref _plotNote, value); }

    private string? _currentValueText;
    /// <summary>The selected view's main value at the current time, e.g. "Now, radio flux density at 5 GHz: 18.3 mJy", or null.</summary>
    public string? CurrentValueText { get => _currentValueText; private set => SetProperty(ref _currentValueText, value); }

    private string? _summaryText;
    /// <summary>A panel's sentence about the current state (the visible panel's comparisons), or null.</summary>
    public string? SummaryText { get => _summaryText; private set => SetProperty(ref _summaryText, value); }

    private double _cursorTimeS = TimeAxis.MinS;
    /// <summary>The observer time of the cursor, s.</summary>
    public double CursorTimeS { get => _cursorTimeS; private set => SetProperty(ref _cursorTimeS, value); }

    private IReadOnlyList<ObservedFact> _facts = Array.Empty<ObservedFact>();
    /// <summary>The preset's measurements for this messenger (empty without a preset).</summary>
    public IReadOnlyList<ObservedFact> Facts { get => _facts; private set => SetProperty(ref _facts, value); }

    private IReadOnlyList<FactMarker> _factMarkers = Array.Empty<FactMarker>();
    /// <summary>The facts drawn on the selected view.</summary>
    public IReadOnlyList<FactMarker> FactMarkers { get => _factMarkers; private set => SetProperty(ref _factMarkers, value); }

    private IReadOnlyList<FactNote> _factNotes = Array.Empty<FactNote>();
    /// <summary>Every fact as a text row (marked or not), so none is dropped silently.</summary>
    public IReadOnlyList<FactNote> FactNotes { get => _factNotes; private set => SetProperty(ref _factNotes, value); }

    /// <summary>The least certain confidence of the selected view's models (the confidence badge).</summary>
    public Confidence Confidence => TabConfidence(SelectedSubTab);

    /// <summary>What the confidence means and the selected view's sources.</summary>
    public string ConfidenceToolTip
    {
        get
        {
            string meaning = Confidence switch
            {
                Confidence.Established => Glossary.Established,
                Confidence.ModelDependent => Glossary.ModelDependent,
                _ => Glossary.Speculative,
            };
            var lines = TabSources(SelectedSubTab).Select(s => $"• {s.Citation} ({s.ResearchRef})");
            return $"{meaning}{Environment.NewLine}{Environment.NewLine}Sources:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
        }
    }

    private string? _errorText;

    /// <summary>"Model error …" when a model threw or gave an infinite value (spec §9); null otherwise.</summary>
    public string? ErrorText
    {
        get => _errorText;
        private set
        {
            if (SetProperty(ref _errorText, value))
            {
                OnPropertyChanged(nameof(DetectionText));
                OnPropertyChanged(nameof(DetectionShortText));
                OnPropertyChanged(nameof(StripBadgeText));
                OnPropertyChanged(nameof(StripToolTip));
                OnPropertyChanged(nameof(DetectionToolTip));
            }
        }
    }

    // ---------------------------------------------------------------- the context

    /// <summary>The outcome being shown. Throws before the first <see cref="Update"/>.</summary>
    protected Outcome Outcome => _outcome ?? throw new InvalidOperationException("The panel has no outcome yet.");

    protected ObserverSetup Observer => _observer;

    /// <summary>The seed of the displayed random events.</summary>
    protected int RunNumber => _runNumber;

    /// <summary>The current observer time, s.</summary>
    protected double TimeS => _timeS;

    /// <summary>The current source time, s (observer time ÷ (1 + z)).</summary>
    protected double SourceTimeS => _timeS / _observer.TimeDilation;

    /// <summary>
    /// Sets the outcome, observer, run number and the preset's facts, and resamples. <paramref name="factsMeasuredHere"/>
    /// is false when the observer has been moved from the preset's real one (ruling R49): its measurements, made from
    /// Earth, are then listed but not marked.
    /// </summary>
    public void Update(Outcome outcome, ObserverSetup observer, int runNumber, IReadOnlyList<ObservedFact> presetFacts,
        bool factsMeasuredHere)
    {
        _outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
        _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        _runNumber = runNumber;
        _presetFacts = presetFacts ?? Array.Empty<ObservedFact>();
        _factsMeasuredHere = factsMeasuredHere;
        _detections.Clear();
        // Choosing the facts touches no model; placing them (with the lab's own values) is inside Resample's catch.
        Facts = _presetFacts.Where(f => f.Messenger == Messenger).ToList();
        Resample(main: true);
    }

    /// <summary>
    /// Called after the context changes, before resampling (a panel may preselect its view here). It runs inside the
    /// panel's error catch (spec §9), so it may ask its models.
    /// </summary>
    protected virtual void OnContextChanged() { }

    /// <summary>Whether this panel draws anything from the run number (the seed of its random events).</summary>
    public virtual bool UsesRunNumber => false;

    /// <summary>True once <see cref="Update"/> has given the panel an outcome.</summary>
    protected bool HasOutcome => _outcome is not null;

    /// <summary>Moves the cursor and updates <see cref="CurrentDetection"/> and the readouts; never resamples the series.</summary>
    public void OnTimeChanged(double observerTimeS)
    {
        _timeS = double.IsFinite(observerTimeS) ? Math.Clamp(observerTimeS, TimeAxis.MinS, TimeAxis.MaxS) : TimeAxis.MinS;
        CursorTimeS = _timeS;
        if (_outcome is null) return;
        RefreshAtTime(force: false);
    }

    // ---------------------------------------------------------------- sampling (subclasses)

    /// <summary>What a sub-tab draws.</summary>
    protected sealed class PanelContent
    {
        public PlotKind Kind { get; init; } = PlotKind.TimeSeries;
        public IReadOnlyList<PlotSeries> Series { get; init; } = Array.Empty<PlotSeries>();
        public IReadOnlyList<PlotBand> Bands { get; init; } = Array.Empty<PlotBand>();
        public IReadOnlyList<ReferenceLine> Lines { get; init; } = Array.Empty<ReferenceLine>();
        public string YLabel { get; init; } = "";
        public bool LogY { get; init; }
        public bool InvertY { get; init; }
        public string? Note { get; init; }
        public SpectrogramView? Spectrogram { get; init; }
    }

    /// <summary>Samples the sub-tab for the current context.</summary>
    protected abstract PanelContent BuildTab(string subTab, Detector instrument);

    /// <summary>The tile's sparkline series.</summary>
    protected abstract IReadOnlyList<PlotSeries> BuildMainSeries();

    /// <summary>The models a sub-tab draws (its confidence, sources and "not modeled" reasons).</summary>
    protected abstract IReadOnlyList<ISignalModel> TabModels(string subTab);

    /// <summary>The models of this panel that judge this instrument.</summary>
    protected abstract IReadOnlyList<ISignalModel> DetectorModels(Detector instrument);

    /// <summary>The sub-tab's main channel: its readout at the current time and, when NaN, the reason.</summary>
    protected virtual (ISignalModel Model, string ChannelId)? PrimaryChannel(string subTab, Detector instrument) => null;

    /// <summary>The view at the current time for the spectrum and pulsar sub-tabs; nothing for curves.</summary>
    protected virtual void RefreshInstantView(bool force) { }

    /// <summary>A panel's sentence about the current state, or null.</summary>
    protected virtual string? BuildSummary() => null;

    /// <summary>Why the selected view draws nothing at the current time, for views without a primary channel.</summary>
    protected virtual string? InstantViewNote() => null;

    protected virtual Confidence TabConfidence(string subTab)
        => TabModels(subTab).Select(m => m.Confidence).DefaultIfEmpty(Confidence.Speculative).Max();

    protected virtual IEnumerable<Source> TabSources(string subTab) => TabModels(subTab).SelectMany(m => m.Sources).Distinct();

    // ---------------------------------------------------------------- sampling helpers

    /// <summary>
    /// A channel on the 400-point log grid of observer time (plus any extra source times, e.g. around a brief flash),
    /// split at NaNs (and, for a log channel, at values ≤ 0). An exception or infinite value is a model error.
    /// </summary>
    protected IReadOnlyList<PlotSeries> SampleChannel(ISignalModel model, string channelId, string? label = null,
        IEnumerable<double>? extraSourceTimesS = null)
    {
        var channel = model.Channels.FirstOrDefault(c => c.Id == channelId)
                      ?? throw new ModelErrorException(model.Id, $"it has no channel '{channelId}'", null);
        double z1 = _observer.TimeDilation;
        IEnumerable<double> times = ObserverGrid;
        if (extraSourceTimesS is not null)
            times = times.Concat(extraSourceTimesS.Select(t => t * z1).Where(t => t >= TimeAxis.MinS && t <= TimeAxis.MaxS))
                .Distinct().OrderBy(t => t);
        var obs = times.ToArray();
        var values = new double[obs.Length];
        for (int i = 0; i < obs.Length; i++) values[i] = SampleOne(model, channelId, obs[i] / z1);
        return Split(label ?? channel.Label, obs, values, channel.Unit, channel.LogScale, channelId);
    }

    /// <summary>One sample of a model in source time; NaN is a legitimate "not modeled here", infinity a model error.</summary>
    protected double SampleOne(ISignalModel model, string channelId, double sourceTimeS)
    {
        double v;
        try
        {
            v = model.Sample(Outcome, _observer, sourceTimeS, channelId);
        }
        catch (Exception e) when (e is not ModelErrorException)
        {
            throw new ModelErrorException(model.Id, $"{e.GetType().Name}: {e.Message}", e);
        }
        if (double.IsInfinity(v))
            throw new ModelErrorException(model.Id, $"'{channelId}' is infinite at {UnitFormat.Time(sourceTimeS)}", null);
        return v;
    }

    /// <summary>Splits a curve at NaNs (and at values ≤ 0 for a log axis) into finite segments.</summary>
    protected static IReadOnlyList<PlotSeries> Split(string label, double[] times, double[] values, string unit, bool logY,
        string? channelId)
    {
        var segments = new List<PlotSeries>();
        int start = -1;
        for (int i = 0; i <= values.Length; i++)
        {
            bool ok = i < values.Length && double.IsFinite(values[i]) && (!logY || values[i] > 0) && double.IsFinite(times[i]);
            if (ok && start < 0) start = i;
            if (!ok && start >= 0)
            {
                segments.Add(new PlotSeries(label, times[start..i], values[start..i], unit, logY) { ChannelId = channelId });
                start = -1;
            }
        }
        return segments;
    }

    /// <summary>A model's verdict, cached per model, instrument and time until the context changes.</summary>
    protected Detection DetectCached(ISignalModel model, Detector detector, double sourceTimeS)
    {
        var key = (model.Id, detector.Id, sourceTimeS);
        if (_detections.TryGetValue(key, out var d)) return d;
        try
        {
            d = model.Detect(Outcome, _observer, sourceTimeS, detector);
        }
        catch (Exception e)
        {
            throw new ModelErrorException(model.Id, $"{e.GetType().Name}: {e.Message}", e);
        }
        if (_detections.Count > 256) _detections.Clear();
        _detections[key] = d;
        return d;
    }

    /// <summary>An instrument's limit drawn as a level, when the view's unit is the limit's.</summary>
    protected static ReferenceLine LimitLine(Detector d, double value, string what)
        => new($"{d.Name} {what}", value, IsTime: false);

    /// <summary>A model failure: which model, and what went wrong.</summary>
    protected sealed class ModelErrorException(string modelId, string detail, Exception? inner)
        : Exception($"{modelId}: {detail}", inner)
    {
        public string ModelId { get; } = modelId;
        public string Detail { get; } = detail;
    }

    // ---------------------------------------------------------------- resampling

    /// <summary>Resamples the selected view (and the main series when the context changed), catching model failures.</summary>
    private void Resample(bool main)
    {
        if (_outcome is null) return;
        _sampleError = null;
        PanelContent content;
        (List<FactMarker> Markers, List<ReferenceLine> Levels, List<FactNote> Notes) facts;
        try
        {
            if (main)
            {
                OnContextChanged();
                MainSeries = BuildMainSeries();
            }
            content = BuildTab(SelectedSubTab, SelectedInstrument);
            facts = PlaceFacts(content, withLabValues: true);
        }
        catch (Exception e)
        {
            content = new PanelContent();
            if (main) MainSeries = Array.Empty<PlotSeries>();
            _sampleError = Describe(e);
            Debug.WriteLine($"SupernovaLab {Title} panel: {e}");
            // Spec §9: the facts are still listed, as plain rows without the lab's values (which need the models).
            facts = PlaceFacts(content, withLabValues: false);
        }

        Kind = content.Kind;
        YLabel = content.YLabel;
        LogY = content.LogY;
        InvertY = content.InvertY;
        Spectrogram = content.Spectrogram;
        PanelNote = content.Note;
        Bands = content.Bands;
        ReferenceLines = content.Lines.Concat(facts.Levels).ToList();
        FactMarkers = facts.Markers;
        FactNotes = facts.Notes;
        Series = content.Series;
        OnPropertyChanged(nameof(Confidence));
        OnPropertyChanged(nameof(ConfidenceToolTip));
        RefreshAtTime(force: true);
    }

    /// <summary>The detection, readouts and the view at the current time.</summary>
    private void RefreshAtTime(bool force)
    {
        _timeError = null;
        try
        {
            RefreshInstantView(force);
            JudgeNow();
            SummaryText = BuildSummary();
            var (value, note) = Readout();
            // A panel's summary already states the current value (the visible panel's "Now V …").
            CurrentValueText = Kind == PlotKind.TimeSeries && SummaryText is not null ? null : value;
            PlotNote = note;
        }
        catch (Exception e)
        {
            _timeError = Describe(e);
            Debug.WriteLine($"SupernovaLab {Title} panel at {_timeS} s: {e}");
            SetError(_timeError);
            CurrentValueText = null;
            PlotNote = null;
        }
        ErrorText = _sampleError ?? _timeError;
    }

    private string Describe(Exception e) => e is ModelErrorException m
        ? $"{ModelErrorText} in the {m.ModelId} model: {m.Detail}. This panel cannot draw it; the other panels are unaffected."
        : $"{ModelErrorText} in this panel: {e.GetType().Name}: {e.Message}. The other panels are unaffected.";


    /// <summary>The primary channel's value now, or why nothing is drawn now.</summary>
    private (string? Value, string? Note) Readout()
    {
        if (Kind != PlotKind.TimeSeries) return (Spectrum?.Caption ?? PulseTrain?.Caption, InstantViewNote());
        if (PrimaryChannel(SelectedSubTab, SelectedInstrument) is not var (model, channelId)) return (null, null);
        double v = SampleOne(model, channelId, SourceTimeS);
        var channel = model.Channels.First(c => c.Id == channelId);
        if (v == 0 && channel.LogScale) return ($"Now, {LowerFirst(channel.Label)}: ≈ 0 (faded away)", null);
        if (double.IsFinite(v)) return ($"Now, {LowerFirst(channel.Label)}: {FormatValue(v, channel.Unit)}", null);
        return (null, NotDrawnReason(model));
    }

    private static string LowerFirst(string s)
        => s.Length > 1 && char.IsUpper(s[0]) && char.IsLower(s[1]) ? char.ToLowerInvariant(s[0]) + s[1..] : s;


    /// <summary>A value with its unit: magnitudes to 0.1; three figures, or scientific notation below 0.01 and from 10⁴.</summary>
    protected static string FormatValue(double v, string unit)
    {
        var inv = CultureInfo.InvariantCulture;
        if (unit is "mag" or "AB mag") return $"{v.ToString("0.0", inv)} {unit}";
        return v != 0 && (Math.Abs(v) < 0.01 || Math.Abs(v) >= 1e4) ? UnitFormat.Scientific(v, unit).TrimEnd()
            : $"{UnitFormat.Number(v)} {unit}".TrimEnd();
    }

    private static InstrumentChoice Choice(Detector d)
    {
        var inv = CultureInfo.InvariantCulture;
        string limit = d.Threshold >= 1e-3 && d.Threshold < 1e4 ? d.Threshold.ToString("0.###", inv) : d.Threshold.ToString("0.#e0", inv);
        string confidence = d.ThresholdConfidence switch
        {
            Confidence.Established => "Established",
            Confidence.ModelDependent => "Model-dependent",
            _ => "Speculative",
        };
        return new InstrumentChoice(d, d.Name,
            $"{d.Name}: limit {limit} {d.ThresholdUnit}. Confidence of this limit: {confidence}.{Environment.NewLine}" +
            $"Source: {d.Source.Citation} ({d.Source.ResearchRef})");
    }
}
