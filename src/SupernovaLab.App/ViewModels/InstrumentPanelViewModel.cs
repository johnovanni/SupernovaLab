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
/// One drawn curve, in observer time (s). A model's NaNs (outside its valid range) are never drawn: a curve with gaps is
/// split into several segments with the same <see cref="Label"/>, so <see cref="Values"/> are always finite, and positive
/// when <see cref="LogY"/>.
/// </summary>
public sealed record PlotSeries(string Label, double[] ObserverTimesS, double[] Values, string Unit, bool LogY)
{
    /// <summary>The model channel drawn (e.g. "appV"), for matching preset facts; null for derived curves.</summary>
    public string? ChannelId { get; init; }

    /// <summary>Drawn as separate dots (the simulated neutrino events), not as a line.</summary>
    public bool IsDots { get; init; }
}

/// <summary>A shaded range between two curves (the neutron star's surface temperature before ~100 yr, signals §8.4).</summary>
public sealed record PlotBand(string Label, double[] ObserverTimesS, double[] Low, double[] High, string Unit);

/// <summary>A labelled straight line: a level (an instrument's limit, an untimed observation) or, when <see cref="IsTime"/>, a time.</summary>
public sealed record ReferenceLine(string Label, double Value, bool IsTime);

/// <summary>A preset's measurement drawn as a marker on the curve of its channel, at its observer time.</summary>
public sealed record FactMarker(ObservedFact Fact, double? ObserverTimeS, string Label);

/// <summary>A text row under a panel's plot: a preset's measurement (marked or not) with its source.</summary>
public sealed record FactNote(string Text, string SourceText);

/// <summary>An instrument in a panel's picker, with its published limit and that limit's confidence (rulings R24, R27).</summary>
public sealed record InstrumentChoice(Detector Detector, string Text, string ToolTip)
{
    public override string ToString() => Text;
}

/// <summary>What a panel's plot shows.</summary>
public enum PlotKind
{
    /// <summary>Curves against log observer time, with the shared "now" cursor.</summary>
    TimeSeries,
    /// <summary>The gravitational-wave spectrogram: frequency against linear time, log colour scale.</summary>
    Spectrogram,
    /// <summary>The schematic spectrum at the current time, against wavelength.</summary>
    Spectrum,
    /// <summary>One second of pulsar pulses at the current time.</summary>
    PulseTrain,
}

/// <summary>
/// The gravitational-wave spectrogram as log10 of the power normalized to 1, held at or above −<see cref="FloorDecades"/>
/// (the bounce bin is about 10⁵ times the track, so a linear scale would show only the bounce). [timeBin, freqBin].
/// </summary>
public sealed record SpectrogramView(double[,] Log10Power, double MaxObserverTimeS, double MaxFrequencyHz)
{
    public const double FloorDecades = 6;
}

/// <summary>The schematic spectrum at the current time (signals §10) and a caption that says what it is.</summary>
public sealed record SpectrumView(double[] WavelengthsAngstrom, double[] RelativeFlux, IReadOnlyList<SpectralLine> Lines,
    double VelocityKmS, string Caption);

/// <summary>One second of pulses (observer time from the first pulse, s), with a caption giving the period and flux.</summary>
public sealed record PulseTrainView(IReadOnlyList<double> PulseOffsetsS, double PeriodS, double WindowS, string Caption);

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
        _currentDetection = new Detection(DetectionStatus.NotModeled, "No outcome yet.");
    }

    // ---------------------------------------------------------------- identity

    public abstract Messenger Messenger { get; }

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

    /// <summary>Sets the sub-tab and instrument without counting as the user's choice (no resample: the caller resamples).</summary>
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

    // ---------------------------------------------------------------- badges

    private Detection _currentDetection;

    /// <summary>The selected instrument's verdict at the timeline's current time.</summary>
    public Detection CurrentDetection
    {
        get => _currentDetection;
        private set
        {
            if (SetProperty(ref _currentDetection, value)) OnPropertyChanged(nameof(DetectionToolTip));
        }
    }

    private (string Full, string Short) _badge = ("Not modeled here", "Not modeled");

    /// <summary>The badge's words: Detected / Too faint at this distance / Blocked by dust / … (spec §5.1).</summary>
    public string DetectionText => ErrorText is not null ? ModelErrorText : _badge.Full;

    /// <summary>The tile's shorter badge words.</summary>
    public string DetectionShortText => ErrorText is not null ? ModelErrorText : _badge.Short;

    /// <summary>The badge's tooltip: the instrument, the verdict and its reason.</summary>
    public string DetectionToolTip => $"{SelectedInstrument.Name}: {DetectionText}.{Environment.NewLine}{Environment.NewLine}" +
                                      (ErrorText ?? CurrentDetection.Reason);

    private void SetBadge((string Full, string Short) badge)
    {
        if (badge == _badge) return;
        _badge = badge;
        OnPropertyChanged(nameof(DetectionText));
        OnPropertyChanged(nameof(DetectionShortText));
        OnPropertyChanged(nameof(DetectionToolTip));
    }

    /// <summary>
    /// The badge words for a verdict, in full and for a tile; a panel may say more (the X-ray panel's filling exposure,
    /// a burst that is over). Computed once per time step, so a panel may ask its models here.
    /// </summary>
    protected virtual (string Full, string Short) Badge(Detection d) => d.Status switch
    {
        DetectionStatus.Detected => ("Detected", "Detected"),
        DetectionStatus.TooFaint => ("Too faint at this distance", "Too faint"),
        DetectionStatus.BlockedByDust => ("Blocked by dust", "Blocked by dust"),
        DetectionStatus.NotPointedAtUs => ("Not pointed at us", "Not pointed at us"),
        DetectionStatus.NotProduced => ("Not produced by this supernova", "Not produced"),
        DetectionStatus.NotYet => ("Not yet", "Not yet"),
        _ => ("Not modeled here", "Not modeled"),
    };

    /// <summary>
    /// For a brief signal (the neutrino burst, the gravitational waves) whose model has ended: "Over", with whether the
    /// instrument caught it, from the model's verdict at its last moment. Null while the signal lasts.
    /// </summary>
    protected (string Full, string Short)? OverBadge(ISignalModel model, Detection d)
    {
        if (d.Status != DetectionStatus.NotModeled || !model.Applies(Outcome)) return null;
        double end = model.ValidRange(Outcome).EndS;
        if (!(SourceTimeS > end)) return null;
        return DetectCached(model, SelectedInstrument, end).Status == DetectionStatus.Detected
            ? ("Over: it was detected", "Over: detected")
            : ("Over: too faint at this distance", "Over: too faint");
    }

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
        OnContextChanged();
        Facts = _presetFacts.Where(f => f.Messenger == Messenger).ToList();
        Resample(main: true);
    }

    /// <summary>Called after the context changes, before resampling (a panel may preselect its view here).</summary>
    protected virtual void OnContextChanged() { }

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
        try
        {
            if (main) MainSeries = BuildMainSeries();
            content = BuildTab(SelectedSubTab, SelectedInstrument);
        }
        catch (Exception e)
        {
            content = new PanelContent();
            if (main) MainSeries = Array.Empty<PlotSeries>();
            _sampleError = Describe(e);
            Debug.WriteLine($"SupernovaLab {Title} panel: {e}");
        }

        Kind = content.Kind;
        YLabel = content.YLabel;
        LogY = content.LogY;
        InvertY = content.InvertY;
        Spectrogram = content.Spectrogram;
        PanelNote = content.Note;
        Bands = content.Bands;
        var (markers, levels, notes) = PlaceFacts(content.Series);
        ReferenceLines = content.Lines.Concat(levels).ToList();
        FactMarkers = markers;
        FactNotes = notes;
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
            var verdict = Judge();
            CurrentDetection = verdict;
            SetBadge(Badge(verdict));
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
            CurrentDetection = new Detection(DetectionStatus.NotModeled, _timeError);
            SetBadge((ModelErrorText, ModelErrorText));
            CurrentValueText = null;
            PlotNote = null;
        }
        ErrorText = _sampleError ?? _timeError;
    }

    private string Describe(Exception e) => e is ModelErrorException m
        ? $"{ModelErrorText} in the {m.ModelId} model: {m.Detail}. This panel cannot draw it; the other panels are unaffected."
        : $"{ModelErrorText} in this panel: {e.GetType().Name}: {e.Message}. The other panels are unaffected.";

    /// <summary>
    /// The verdict for the selected instrument now: whether it sees this messenger at all, so every model of the panel
    /// that judges it is asked and the most favourable verdict wins (Detected, then the reasons closest to it); between
    /// equal verdicts, the selected view's model's.
    /// </summary>
    private Detection Judge()
    {
        var all = DetectorModels(SelectedInstrument);
        if (all.Count == 0)
            return new Detection(DetectionStatus.NotModeled, $"{SelectedInstrument.Name} is not modeled in this panel.");
        var tab = TabModels(SelectedSubTab);
        double t = SourceTimeS;
        return all.Select(m => (Verdict: DetectCached(m, SelectedInstrument, t), InTab: tab.Contains(m)))
            .OrderBy(x => Rank(x.Verdict.Status)).ThenBy(x => x.InTab ? 0 : 1).First().Verdict;
    }

    private static int Rank(DetectionStatus s) => s switch
    {
        DetectionStatus.Detected => 0,
        DetectionStatus.BlockedByDust => 1,
        DetectionStatus.TooFaint => 2,
        DetectionStatus.NotPointedAtUs => 3,
        DetectionStatus.NotYet => 4,
        DetectionStatus.NotModeled => 5,
        _ => 6,
    };

    /// <summary>The primary channel's value now, or why nothing is drawn now.</summary>
    private (string? Value, string? Note) Readout()
    {
        if (Kind != PlotKind.TimeSeries) return (Spectrum?.Caption ?? PulseTrain?.Caption, InstantViewNote());
        if (PrimaryChannel(SelectedSubTab, SelectedInstrument) is not var (model, channelId)) return (null, null);
        double v = SampleOne(model, channelId, SourceTimeS);
        var channel = model.Channels.First(c => c.Id == channelId);
        if (double.IsFinite(v)) return ($"Now, {LowerFirst(channel.Label)}: {FormatValue(v, channel.Unit)}", null);
        return (null, NotDrawnReason(model));
    }

    private static string LowerFirst(string s)
        => s.Length > 1 && char.IsUpper(s[0]) && char.IsLower(s[1]) ? char.ToLowerInvariant(s[0]) + s[1..] : s;

    /// <summary>
    /// Why a model draws nothing now (spec §9, "not modeled here"): its own verdict's reason, asked of the first of this
    /// panel's instruments that it judges; for a model no instrument here judges, its range.
    /// </summary>
    protected virtual string NotDrawnReason(ISignalModel model)
    {
        var detector = Instruments.FirstOrDefault(d => DetectorModels(d).Contains(model));
        if (detector is null)
        {
            if (!model.Applies(Outcome)) return "Not produced by this supernova.";
            var (start, end) = model.ValidRange(Outcome);
            double z1 = Observer.TimeDilation;
            return SourceTimeS < start || SourceTimeS > end
                ? $"Not modeled here: this view covers {UnitFormat.Time(start * z1)} to {UnitFormat.Time(end * z1)} after the explosion."
                : "Not modeled at this time.";
        }
        var d = DetectCached(model, detector, SourceTimeS);
        return d.Status is DetectionStatus.NotModeled or DetectionStatus.NotProduced or DetectionStatus.NotYet
            or DetectionStatus.NotPointedAtUs
            ? d.Reason
            : CurveSpan();
    }

    /// <summary>Where the drawn curves begin and end, for a time when they draw nothing.</summary>
    private string CurveSpan()
    {
        var times = Series.Where(s => !s.IsDots).SelectMany(s => s.ObserverTimesS).ToList();
        if (times.Count == 0) return "Nothing is drawn for this outcome.";
        double first = times.Min(), last = times.Max();
        return TimeS > last ? $"Not modeled here: the curve ends {UnitFormat.Time(last)} after the explosion."
            : TimeS < first ? $"Not modeled here yet: the curve starts {UnitFormat.Time(first)} after the explosion."
            : "Not modeled at this moment (a gap in the curve).";
    }

    /// <summary>A value with its unit: magnitudes to 0.1; three figures, or scientific notation below 0.01 and from 10⁴.</summary>
    protected static string FormatValue(double v, string unit)
    {
        var inv = CultureInfo.InvariantCulture;
        if (unit is "mag" or "AB mag") return $"{v.ToString("0.0", inv)} {unit}";
        return v != 0 && (Math.Abs(v) < 0.01 || Math.Abs(v) >= 1e4) ? UnitFormat.Scientific(v, unit).TrimEnd()
            : $"{UnitFormat.Number(v)} {unit}".TrimEnd();
    }

    // ---------------------------------------------------------------- preset facts

    /// <summary>
    /// Where each of this messenger's facts goes: a marker on a drawn curve of its channel at its time, a labelled level
    /// when it has no time, and always a text row (with the lab's own value where there is one).
    /// </summary>
    private (List<FactMarker>, List<ReferenceLine>, List<FactNote>) PlaceFacts(IReadOnlyList<PlotSeries> drawn)
    {
        var markers = new List<FactMarker>();
        var levels = new List<ReferenceLine>();
        var notes = new List<FactNote>();
        if (Facts.Count > 0 && !_factsMeasuredHere)
            notes.Add(new FactNote(
                "The observer has been moved from where this supernova was really seen, so its measurements (made from " +
                "Earth) are listed but not marked on the plot.",
                "Ruling R49: an observer edit keeps the preset's explosion values; its measurements belong to the real distance and dust."));
        foreach (var f in Facts)
        {
            var series = drawn.FirstOrDefault(s => s.ChannelId == f.ChannelId && s.Unit == f.Unit);
            bool marked = false;
            if (_factsMeasuredHere && series is not null)
            {
                string value = FormatValue(f.Value, f.Unit);
                if (f.SourceTimeS is double t)
                {
                    markers.Add(new FactMarker(f, t * _observer.TimeDilation, $"Observed: {value}"));
                    marked = true;
                }
                else
                {
                    levels.Add(new ReferenceLine($"Observed: {value}", f.Value, IsTime: false));
                    marked = true;
                }
            }
            // The descriptions already say when each value was measured; a marked fact is flagged with the marker's ◆.
            notes.Add(new FactNote($"{(marked ? "◆ " : "")}{f.Description}{(marked ? "" : LabComparison(f))}",
                $"{f.Source.Citation} ({f.Source.ResearchRef})"));
        }
        return (markers, levels, notes);
    }

    /// <summary>
    /// The lab's own value beside a fact that is not marked: a channel of one of this panel's models at the fact's time,
    /// a detector the lab models (Kamiokande-II), or an outcome member (⁴⁴Ti mass).
    /// </summary>
    private string LabComparison(ObservedFact f)
    {
        var model = SubTabs.SelectMany(TabModels).Distinct().FirstOrDefault(m => m.Channels.Any(c => c.Id == f.ChannelId));
        if (model is not null)
        {
            var channel = model.Channels.First(c => c.Id == f.ChannelId);
            if (f.SourceTimeS is not double t) return "";
            double v = SampleOne(model, f.ChannelId, t);
            return double.IsFinite(v) && channel.Unit == f.Unit
                ? $" The lab gives {FormatValue(v, channel.Unit)} then."
                : "";
        }
        if (f.ChannelId == "cumulative@kamiokande-ii" && Messenger == Messenger.Neutrinos)
            return $" The lab expects about {NeutrinoModel.KamiokandeIIEvents(Outcome, _observer).ToString("0.#", CultureInfo.InvariantCulture)} " +
                   "Kamiokande-II events here.";
        if (f.ChannelId is "cumulative@imb" or "cumulative@baksan")
            return " (The lab has no model of this detector.)";
        if (f.ChannelId == nameof(Outcome.Ti44Mass))
            return Outcome.Ti44Mass is { } ti
                ? $" The lab uses {UnitFormat.Scientific(ti.Value, "M☉")}{(ti.Published ? " (the published value)" : "")}."
                : " The lab makes no ⁴⁴Ti for this outcome.";
        return "";
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
