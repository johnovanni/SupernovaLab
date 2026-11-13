using SupernovaLab.App.Controllers;
using SupernovaLab.App.ViewModels;

namespace SupernovaLab.App.Tour;

/// <summary>Where the guided tour is: not running, travelling to the next stop, held for the user, or dwelling on a stop.</summary>
public enum TourState { Off, Playing, Paused, Dwelling }

/// <summary>
/// Plays the guided tour (Project 2, Task 13). It never edits the outcome; it drives the timeline's observer time, the 3D
/// view's cutaway and orbit, and the instrument highlight, from <see cref="TourBuilder"/>'s stops.
/// <list type="bullet">
/// <item><b>Travel</b> between stops advances the time as playback steps (the camera eases) at
/// <see cref="TimelineViewModel.DefaultDecadesPerSecond"/>, slowed to <see cref="TimelineViewModel.MinDecadesPerSecond"/> within
/// <see cref="SlowDecades"/> decades of the next stop, and lands exactly on it.</item>
/// <item>At a <b>stop</b> the time holds for <see cref="TourStop.DwellS"/>; the cutaway and highlight are set and the camera orbits
/// at <see cref="TourStop.OrbitDegPerS"/> (kept while travelling on, until the next stop sets its own). <see cref="Next"/> and
/// <see cref="Previous"/> are jumps.</item>
/// <item>The user's input to the camera or the timeline <b>pauses</b> the tour (the caption then offers "Resume tour"); resuming
/// continues from the stop the time is now at. A new outcome ends the tour with <see cref="NewExplosionNote"/> (Review Focus 4).
/// <see cref="Exit"/> restores the user's cutaway setting and clears the highlight.</item>
/// </list>
/// </summary>
public sealed class TourDirector
{
    /// <summary>Within this many decades of the next stop the timeline slows to its minimum speed.</summary>
    public const double SlowDecades = 0.2;

    public const string NewExplosionNote = "Tour ended: new explosion";

    private readonly MainViewModel _vm;
    private SceneController? _scene;
    private IReadOnlyList<TourStop> _stops = Array.Empty<TourStop>();
    private TourState _resumeState = TourState.Dwelling;
    private double _dwellLeftS;
    private bool _savedCutaway;

    public TourDirector(MainViewModel vm, SceneController? scene)
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        _vm.OutcomeChanged += OnOutcomeChanged;
        _vm.Timeline.UserInteracted += OnUserInput;
        Scene = scene;
    }

    /// <summary>The 3D view's controller, or null (the window attaches it once it is loaded and detaches it when it closes).</summary>
    public SceneController? Scene
    {
        get => _scene;
        set
        {
            if (ReferenceEquals(_scene, value)) return;
            if (_scene is not null)
            {
                _scene.UserInteracted -= OnUserInput;
                _scene.IsTourOrbiting = false;
            }
            _scene = value;
            if (_scene is not null) _scene.UserInteracted += OnUserInput;
            SyncOrbit();
        }
    }

    /// <summary>Raised after any change of state, stop or note.</summary>
    public event EventHandler? Changed;

    public TourState State { get; private set; } = TourState.Off;

    /// <summary>The stops of the tour being played (or last played).</summary>
    public IReadOnlyList<TourStop> Stops => _stops;

    public int StopIndex { get; private set; }

    public TourStop? Current => State != TourState.Off && StopIndex >= 0 && StopIndex < _stops.Count ? _stops[StopIndex] : null;

    /// <summary>"Tour ended: new explosion" after a new outcome ended the tour; cleared when a tour starts.</summary>
    public string? Note { get; private set; }

    /// <summary>Starts the tour of the current explosion at its first stop.</summary>
    public void Start()
    {
        if (State != TourState.Off) return;
        var stops = TourBuilder.For(_vm.CurrentOutcome, _vm.CurrentObserver, _vm.Lab.SelectedPreset);
        if (stops.Count == 0) return;
        _stops = stops;
        Note = null;
        _savedCutaway = _scene?.IsCutaway ?? false;
        _vm.Timeline.IsPlaying = false;   // the director drives the time
        EnterStop(0, jump: true);
    }

    public void Pause()
    {
        if (State is not (TourState.Playing or TourState.Dwelling)) return;
        _resumeState = State;
        State = TourState.Paused;
        RaiseChanged();
    }

    /// <summary>Continues from the stop the time is at: the dwell if it was held on the stop, else travel to the next one.</summary>
    public void Resume()
    {
        if (State != TourState.Paused || Current is not { } stop) return;
        _vm.Timeline.IsPlaying = false;
        double t = _vm.Timeline.ObserverTimeS;
        bool atStop = Math.Abs(t - stop.ObserverTimeS) <= 1e-9 * stop.ObserverTimeS;
        if (atStop && _resumeState == TourState.Dwelling)
        {
            State = TourState.Dwelling;
            RaiseChanged();
            return;
        }
        // The user moved the time: carry on from the last stop at or before it.
        int i = 0;
        for (int k = 0; k < _stops.Count; k++)
            if (_stops[k].ObserverTimeS <= t * (1 + 1e-9)) i = k;
        if (i == _stops.Count - 1) { Exit(); return; }   // at or past the last stop: nothing left to play
        if (i != StopIndex) ApplyStop(i);
        State = TourState.Playing;
        RaiseChanged();
    }

    /// <summary>Jumps to the next stop; past the last one it ends the tour.</summary>
    public void Next()
    {
        if (State == TourState.Off) return;
        if (StopIndex + 1 >= _stops.Count) Exit();
        else GoTo(StopIndex + 1);
    }

    public void Previous()
    {
        if (State == TourState.Off) return;
        GoTo(Math.Max(0, StopIndex - 1));
    }

    /// <summary>Jumps to a stop (a jump of the time, not a playback step). A paused tour stays paused there.</summary>
    public void GoTo(int index)
    {
        if (State == TourState.Off || _stops.Count == 0) return;
        bool wasPaused = State == TourState.Paused;
        EnterStop(Math.Clamp(index, 0, _stops.Count - 1), jump: true);
        if (!wasPaused) return;
        _resumeState = TourState.Dwelling;
        State = TourState.Paused;
        RaiseChanged();
    }

    /// <summary>Ends the tour: the user's cutaway setting comes back and the highlight clears.</summary>
    public void Exit()
    {
        if (State == TourState.Off) return;
        State = TourState.Off;
        _vm.Highlight(null);
        if (_scene is not null) _scene.IsCutaway = _savedCutaway;
        RaiseChanged();
    }

    /// <summary>Advances the tour by this much wall-clock time (called once per rendered frame while the tour is on).</summary>
    public void Tick(TimeSpan elapsed)
    {
        double dt = elapsed.TotalSeconds;
        if (State is TourState.Off or TourState.Paused || !(dt > 0) || Current is not { } stop) return;

        _scene?.Orbit(stop.OrbitDegPerS * dt);
        if (State == TourState.Dwelling)
        {
            _dwellLeftS -= dt;
            if (_dwellLeftS > 0) return;
            if (StopIndex + 1 >= _stops.Count) Exit();
            else
            {
                State = TourState.Playing;
                RaiseChanged();
            }
            return;
        }

        var timeline = _vm.Timeline;
        double target = _stops[StopIndex + 1].ObserverTimeS, t = timeline.ObserverTimeS;
        double remaining = Math.Log10(target / t);
        double speed = remaining <= SlowDecades ? TimelineViewModel.MinDecadesPerSecond : TimelineViewModel.DefaultDecadesPerSecond;
        double next = t * Math.Pow(10, speed * dt);
        if (remaining <= 0 || next >= target) EnterStop(StopIndex + 1, jump: false);
        else timeline.DirectTo(next, playbackStep: true);
    }

    private void EnterStop(int index, bool jump)
    {
        ApplyStop(index, quiet: true);
        _vm.Timeline.DirectTo(_stops[index].ObserverTimeS, playbackStep: !jump);
        _dwellLeftS = _stops[index].DwellS;
        State = TourState.Dwelling;
        RaiseChanged();
    }

    /// <summary>Shows a stop's cutaway and highlight (its caption follows from <see cref="Current"/>).</summary>
    private void ApplyStop(int index, bool quiet = false)
    {
        StopIndex = index;
        var stop = _stops[index];
        if (_scene is not null) _scene.IsCutaway = stop.Cutaway;
        _vm.Highlight(stop.Highlight);
        if (!quiet) RaiseChanged();
    }

    private void OnUserInput(object? sender, EventArgs e) => Pause();

    /// <summary>
    /// Ruling R-P2-32: the 3D view animates while the camera orbits, so the orbit renders smoothly (the Task 14 bench drew 17–19 fps
    /// with on-demand frames). On while travelling or dwelling at a stop whose orbit speed is not 0; off when paused or ended.
    /// </summary>
    private void SyncOrbit()
    {
        if (_scene is not null)
            _scene.IsTourOrbiting = State is TourState.Playing or TourState.Dwelling && Current is { OrbitDegPerS: not 0 };
    }

    private void RaiseChanged()
    {
        SyncOrbit();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnOutcomeChanged(object? sender, EventArgs e)
    {
        if (State == TourState.Off) return;
        Exit();
        Note = NewExplosionNote;
        var existing = _vm.StatusMessage;
        _vm.StatusMessage = string.IsNullOrEmpty(existing) ? Note : $"{existing}  {Note}";
        RaiseChanged();
    }
}
