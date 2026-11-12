using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SupernovaLab.App.Services;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Timeline;

namespace SupernovaLab.App.ViewModels;

/// <summary>
/// The lab's shared state (spec §2–§5.0). Every window binds to this one instance. Any input edit recomputes the outcome
/// at once and keeps the current time (spec §2.4, Review Focus 4). View models never create windows: the commands call
/// <see cref="IWindowService"/>.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly OutcomeEngine _engine;
    private readonly IWindowService _windows;

    public MainViewModel(OutcomeEngine engine, IWindowService windows)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _windows = windows ?? throw new ArgumentNullException(nameof(windows));
        Lab = new LabViewModel();
        Outcome = new OutcomeViewModel(engine, Lab);
        Timeline = new TimelineViewModel();

        ExplodeCommand = new RelayCommand(Explode);
        RerollCommand = new RelayCommand(() => RunNumber++);
        ShowInstrumentsCommand = new RelayCommand<Messenger?>(focus => _windows.ShowInstruments(focus));
        ShowOutcomeDetailsCommand = new RelayCommand(_windows.ShowOutcomeDetails);
        ShowPresetInfoCommand = new RelayCommand(_windows.ShowPresetInfo);

        _currentOutcome = Recompute();
        _currentObserver = Lab.Observer;
        Lab.Changed += OnLabChanged;
    }

    public LabViewModel Lab { get; }
    public OutcomeViewModel Outcome { get; }
    public TimelineViewModel Timeline { get; }

    /// <summary>
    /// Raised once after each recompute, when an input edit or a preset changed the outcome or the observer. Panels
    /// resample on this. The time is not changed.
    /// </summary>
    public event EventHandler? OutcomeChanged;

    private Outcome _currentOutcome;

    /// <summary>The outcome of the current inputs and pins, recomputed on any input change.</summary>
    public Outcome CurrentOutcome
    {
        get => _currentOutcome;
        private set => SetProperty(ref _currentOutcome, value);
    }

    private ObserverSetup _currentObserver;

    public ObserverSetup CurrentObserver
    {
        get => _currentObserver;
        private set => SetProperty(ref _currentObserver, value);
    }

    private int _runNumber = 1;

    /// <summary>The seed of EventSampler (spec §2.4); Re-roll increments it.</summary>
    public int RunNumber
    {
        get => _runNumber;
        private set
        {
            if (SetProperty(ref _runNumber, value)) OnPropertyChanged(nameof(RunText));
        }
    }

    /// <summary>"Run #1": the seed of the displayed random events (spec §2.4).</summary>
    public string RunText => $"Run #{RunNumber}";

    /// <summary>A one-line message for the status bar, e.g. that editing an input dropped a preset's published values.</summary>
    [ObservableProperty] private string? _statusMessage;

    public IRelayCommand ExplodeCommand { get; }
    public IRelayCommand RerollCommand { get; }
    public IRelayCommand<Messenger?> ShowInstrumentsCommand { get; }
    public IRelayCommand ShowOutcomeDetailsCommand { get; }
    public IRelayCommand ShowPresetInfoCommand { get; }

    /// <summary>Spec §2.4: Explode starts the timeline at t = 0 (the axis's first millisecond) and plays it.</summary>
    private void Explode()
    {
        Timeline.ObserverTimeS = TimeAxis.MinS;
        Timeline.IsPlaying = true;
    }

    private void OnLabChanged(object? sender, LabChangedEventArgs e)
    {
        if (e.AppliedPreset is { } p)
            StatusMessage = $"{p.Name}: the lab is set to its best match, with its published values pinned.";
        else if (e.DroppedPresetName is { } name)
            StatusMessage = $"Inputs changed, so {name}'s published values no longer apply: the lab shows its own model.";
        else if (e.ObserverLeftPresetName is { } moved)
            StatusMessage = $"Observer changed: {moved}'s published explosion values still apply, but this is no longer " +
                            "the distance and dust it was really seen at.";

        CurrentOutcome = Recompute();
        CurrentObserver = Lab.Observer;
        OutcomeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Computes the outcome and refreshes the summary, map, markers and time texts (never the time itself).</summary>
    private Outcome Recompute()
    {
        var outcome = _engine.Compute(Lab.Inputs, Lab.Pins);
        var observer = Lab.Observer;
        Outcome.Update(outcome, observer);
        Timeline.Update(EventMarkers.For(outcome, observer), observer, OutcomeTexts.TimeOrigin(outcome));
        return outcome;
    }
}
