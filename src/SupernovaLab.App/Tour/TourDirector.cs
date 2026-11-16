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
/// <item>At a <b>stop</b> the time holds for <see cref="TourStop.DwellS"/>; the cutaway and highlight are set and, at a stop with an
/// orbit (<see cref="TourStop.Sways"/>), the camera <b>sways</b> (Task 15): yaw = centre + A sin(2 pi t / P),
/// A = <see cref="SwayAmplitudeDeg"/> (on each side the most that keeps both cut faces exposed, see <see cref="CurrentSwayAmplitudesDeg"/>), P = <see cref="SwayPeriodS"/>, so the core stays in sight. The centre is the notch-facing yaw on a cutaway
/// stop, the entering yaw otherwise; after the user turns the view it is the yaw they left it at. <see cref="Next"/> and
/// <see cref="Previous"/> are jumps.</item>
/// <item>Only the <b>timeline</b> (a scrub, a marker click, Play/Pause, Explode) <b>pauses</b> the tour (the caption then offers "Resume tour");
/// resuming continues from the stop the time is now at. Turning, zooming or Home on the 3D view does not pause it (Task 15): a wheel
/// zoom freezes the scale until the next stop hands it back. A new outcome ends the tour with <see cref="NewExplosionNote"/> (Review
/// Focus 4). <see cref="Exit"/> restores the user's cutaway setting and clears the highlight.</item>
/// </list>
/// </summary>
public sealed class TourDirector
{
    /// <summary>Within this many decades of the next stop the timeline slows to its minimum speed.</summary>
    public const double SlowDecades = 0.2;

    public const string NewExplosionNote = "Tour ended: new explosion";

    /// <summary>The sway's amplitude (deg) and period (s), Task 15.</summary>
    public const double SwayAmplitudeDeg = 60, SwayPeriodS = 12;

    private readonly MainViewModel _vm;
    private SceneController? _scene;
    private IReadOnlyList<TourStop> _stops = Array.Empty<TourStop>();
    private TourState _resumeState = TourState.Dwelling;
    private double _dwellLeftS;
    private bool _savedCutaway;
    private bool _swaying, _swayCutaway;
    private double _swayCentreDeg, _swayPhaseS, _swayExpectedYawDeg, _swayPlusDeg = SwayAmplitudeDeg, _swayMinusDeg = SwayAmplitudeDeg;
    private bool _wasDragging;
    private bool _closeUpOpened, _closeUpDismissed;

    public TourDirector(MainViewModel vm, SceneController? scene)
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        _vm.OutcomeChanged += OnOutcomeChanged;
        _vm.Timeline.UserInteracted += OnUserInput;   // only the timeline pauses the tour (Task 15)
        Scene = scene;
    }

    /// <summary>The 3D view's controller, or null (the window attaches it once it is loaded and detaches it when it closes).</summary>
    public SceneController? Scene
    {
        get => _scene;
        set
        {
            if (ReferenceEquals(_scene, value)) return;
            if (_scene is not null) _scene.IsTourOrbiting = false;
            _scene = value;
            _swaying = false;
            SyncOrbit();
        }
    }

    /// <summary>
    /// True while a Close-up stop is shown and the Close-up window is still wanted: the tour's outline highlight for that window
    /// (Task 13). False once the user has closed the window during the tour.
    /// </summary>
    public bool IsCloseUpHighlighted => Current is { CloseUp: true } && !_closeUpDismissed;

    /// <summary>
    /// Task 13: notices that the user closed the Close-up window the tour opened; from then on the Close-up-only stops are skipped and the
    /// tour carries on with the others.
    /// </summary>
    private void DetectCloseUpClosed()
    {
        if (!_closeUpOpened || _closeUpDismissed || _vm.Windows.IsCloseUpOpen) return;
        _closeUpDismissed = true;
        if (State != TourState.Off) RaiseChanged();
    }

    private bool Skipped(int index) => _closeUpDismissed && _stops[index].CloseUpOnly;

    /// <summary>The next stop to play after <paramref name="index"/>, skipping Close-up-only stops once the window is closed; -1 when none.</summary>
    private int NextPlayable(int index)
    {
        for (int k = index + 1; k < _stops.Count; k++)
            if (!Skipped(k)) return k;
        return -1;
    }

    private int PreviousPlayable(int index)
    {
        for (int k = index - 1; k > 0; k--)
            if (!Skipped(k)) return k;
        return 0;
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
        var stops = TourBuilder.For(_vm.CurrentOutcome, _vm.CurrentObserver, _vm.Lab.SelectedPreset, _vm.RunNumber);
        if (stops.Count == 0) return;
        _stops = stops;
        Note = null;
        _closeUpOpened = _closeUpDismissed = false;
        _savedCutaway = _scene?.IsCutaway ?? false;
        _swaying = false;
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
        DetectCloseUpClosed();
        int next = NextPlayable(StopIndex);
        if (next < 0) Exit();
        else GoTo(next);
    }

    public void Previous()
    {
        if (State == TourState.Off) return;
        DetectCloseUpClosed();
        GoTo(PreviousPlayable(StopIndex));
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

        DetectCloseUpClosed();
        Sway(stop, dt);
        if (State == TourState.Dwelling)
        {
            _dwellLeftS -= dt;
            if (_dwellLeftS > 0) return;
            if (NextPlayable(StopIndex) < 0) Exit();
            else
            {
                State = TourState.Playing;
                RaiseChanged();
            }
            return;
        }

        var timeline = _vm.Timeline;
        int nextIndex = NextPlayable(StopIndex);
        if (nextIndex < 0) { Exit(); return; }   // only skipped Close-up stops are left
        double target = _stops[nextIndex].ObserverTimeS, t = timeline.ObserverTimeS;
        double remaining = Math.Log10(target / t);
        double speed = remaining <= SlowDecades ? TimelineViewModel.MinDecadesPerSecond : TimelineViewModel.DefaultDecadesPerSecond;
        double nextT = t * Math.Pow(10, speed * dt);
        if (remaining <= 0 || nextT >= target) EnterStop(nextIndex, jump: false);
        else timeline.DirectTo(nextT, playbackStep: true);
    }

    /// <summary>
    /// The sway of a stop that sways: while the user holds the mouse down it is suspended; a yaw other than the one it set last
    /// (the user turned, or Home reset the view) becomes the new centre with the phase restarted, so the camera carries on from there
    /// without a jump.
    /// </summary>
    private void Sway(TourStop stop, double dt)
    {
        if (_scene is null || !stop.Sways) return;
        if (_scene.IsUserDragging)
        {
            _wasDragging = true;
            return;
        }
        if (!_swaying) StartSway(stop);
        if (_wasDragging || YawDifferenceDeg(_scene.Camera.YawDeg, _swayExpectedYawDeg) > 1e-6)
        {
            _wasDragging = false;
            Recentre();
        }
        _swayPhaseS = (_swayPhaseS + dt) % SwayPeriodS;
        SetSwayYaw();
    }

    /// <summary>The camera's yaw becomes the sway's centre and its phase restarts, so the sway carries on from here without a jump.</summary>
    private void Recentre()
    {
        _swayCentreDeg = _scene!.Camera.YawDeg;
        (_swayPlusDeg, _swayMinusDeg) = _scene.SwayAmplitudesFor(_swayCentreDeg, SwayAmplitudeDeg);
        _swayPhaseS = 0;
    }

    /// <summary>
    /// The sway's amplitudes now (deg) above and below the centre: 60 on a side with room, less on a side where a turned cutaway
    /// wedge would otherwise be seen from behind (ruling R-P2-34).
    /// </summary>
    public (double Plus, double Minus) CurrentSwayAmplitudesDeg => (_swayPlusDeg, _swayMinusDeg);

    private void SetSwayYaw()
    {
        double yaw = SwayYawDeg(_swayCentreDeg, _swayPlusDeg, _swayMinusDeg, _swayPhaseS);
        _scene!.SetTourYaw(yaw);
        _swayExpectedYawDeg = _scene.Camera.YawDeg;
    }

    /// <summary>
    /// The sway's yaw (deg) at a time into its period, about a centre: the sine times the "plus" amplitude while it is positive and
    /// the "minus" amplitude while it is negative (asymmetric, ruling R-P2-34; still continuous).
    /// </summary>
    public static double SwayYawDeg(double centreDeg, double plusDeg, double minusDeg, double tS)
    {
        double s = Math.Sin(2 * Math.PI * tS / SwayPeriodS);
        return centreDeg + (s >= 0 ? plusDeg : minusDeg) * s;
    }

    private static double YawDifferenceDeg(double a, double b) => Math.Abs(((a - b) % 360 + 540) % 360 - 180);

    /// <summary>
    /// Starts a stop's sway when the tour was not swaying, or the cutaway changed: the centre is the notch-facing yaw on a cutaway
    /// stop (the view as Home gives it) and the camera's yaw otherwise. Between stops of the same kind the sway runs on.
    /// </summary>
    private void StartSway(TourStop stop)
    {
        _swaying = true;
        _swayCutaway = stop.Cutaway;
        _swayCentreDeg = stop.Cutaway ? _scene!.NotchFacingYawDeg : _scene!.Camera.YawDeg;
        (_swayPlusDeg, _swayMinusDeg) = _scene.SwayAmplitudesFor(_swayCentreDeg, SwayAmplitudeDeg);
        _swayPhaseS = 0;
        _wasDragging = false;
        SetSwayYaw();
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
        // Task 13: the first Close-up stop opens the Close-up window (once per tour; a window the user closed stays closed).
        DetectCloseUpClosed();
        if (stop.CloseUp && !_closeUpOpened && !_closeUpDismissed)
        {
            _closeUpOpened = true;
            _vm.Windows.ShowCloseUp();
        }
        if (_scene is not null)
        {
            _scene.IsCutaway = stop.Cutaway;
            _scene.HandBackScale();   // a wheel zoom's frozen scale goes back to the director at the next stop (Task 15)
            if (!stop.Sways) _swaying = false;
            else if (_swaying && _swayCutaway != stop.Cutaway) _swaying = false;
            if (stop.Sways && !_swaying) StartSway(stop);
        }
        _vm.Highlight(stop.Highlight);
        if (!quiet) RaiseChanged();
    }

    private void OnUserInput(object? sender, EventArgs e) => Pause();

    /// <summary>
    /// Ruling R-P2-32: the 3D view animates while the camera sways, so the sway renders smoothly (the Task 14 bench drew 17–19 fps
    /// with on-demand frames). On while travelling or dwelling at a stop that sways; off when paused or ended.
    /// </summary>
    private void SyncOrbit()
    {
        if (_scene is not null)
            _scene.IsTourOrbiting = State is TourState.Playing or TourState.Dwelling && Current is { Sways: true };
    }

    private void RaiseChanged()
    {
        // The caption's bindings update first; then the view's render mode changes. (The final fix wave's snapshots showed a stale
        // caption layout at the first tour stop captured after Start with the opposite order; this order is not yet re-checked.)
        Changed?.Invoke(this, EventArgs.Empty);
        SyncOrbit();
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
