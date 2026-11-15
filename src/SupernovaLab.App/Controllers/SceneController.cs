using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenTK.Graphics.OpenGL4;
using SupernovaLab.App.Services;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Structure;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;

namespace SupernovaLab.App.Controllers;

/// <summary>
/// The simple 3D explosion view (spec §6). It follows the shared <see cref="MainViewModel"/>: a new outcome or observer
/// (<see cref="MainViewModel.OutcomeChanged"/>) rebuilds the <see cref="ExplosionScene"/>, and each time change (observer
/// time ÷ (1 + z) = source time) restates what is drawn and redraws. It is also the overlay's view model: the time, the
/// physical radius, the notes and the distance-ring labels. Drawing goes through an <see cref="IRenderHost"/> made by the factory
/// given, so the failure path (spec §9) can be tested without OpenGL: if the host cannot start or a frame fails, the view
/// shows <see cref="FailureMessage"/>, naming the requirement, and the rest of the app carries on.
/// <para>Camera: the <see cref="Director"/> (plan ruling P5) holds the view's half-height still while the debris grow and
/// steps it back to the next distance ring when they reach the outermost one; a new outcome, a timeline click or Home
/// jumps. Dragging rotates (and keeps auto-framing); the wheel zooms and freezes the physical scale, so the debris then
/// grow across the view. The scene is drawn in units of the view's half-height (or the frozen one), so it never stresses
/// float precision whatever the physical size.</para>
/// <para>Call <see cref="Dispose"/> when the window closes (its Closing event, while the GL control is still loaded):
/// the view model lives as long as the app (Task 15's leak).</para>
/// </summary>
public sealed partial class SceneController : ObservableObject, IDisposable
{
    /// <summary>The OpenGL version the 3D view needs (spec §1 and §9).</summary>
    public const string Requirement = "OpenGL 4.1";

    /// <summary>The longest wall-clock gap one frame hands the director (a frame after idle time is not a long ease).</summary>
    private const double MaxFrameGapS = 0.1;

    private readonly MainViewModel _vm;
    private readonly Func<IRenderHost> _hostFactory;
    private readonly Func<Outcome, double, ExplosionScene> _sceneFactory;
    private readonly BeamClock _beamClock;
    private readonly Func<double> _clock;
    private double? _lastFrameS;
    private IRenderHost? _host;
    private ExplosionScene? _scene;
    private SceneState _state = new();
    /// <summary>Review M1: the 3D view's model error, shown in place of its notes; null when the scene is fine.</summary>
    private string? _modelError;
    /// <summary>The explosion's matter (star, particles, shells), shared with the Close-up's environment (Project 3).</summary>
    private ExplosionPainter? _painter;
    private HdrPipeline? _hdr;
    private bool _uploadParticles = true;
    private QualityLevel _quality = QualityLevels.Default;
    private readonly QualityGovernor _governor = new(QualityLevels.Default);
    private string? _qualityNote;
    private FlashRenderer? _flash;
    private GpuTimer? _gpuTimer;
    private JetRenderer? _jets;
    private RemnantRenderer? _remnant;
    private RingRenderer? _rings;
    private TorusRingRenderer? _torusRings;
    private double? _frozenUnitCm;
    private Size _viewDip;
    private TaskCompletionSource<BitmapSource?>? _readback;
    private bool _disposed;

    /// <param name="sceneFactory">Builds the scene of an outcome at a time dilation; null for <see cref="ExplosionScene"/>'s
    /// own constructor (tests pass one that fails, review M1).</param>
    /// <param name="wallClock">Wall-clock seconds for the director's ease; null for the real clock (tests pass a fake).</param>
    /// <param name="beamClock">The beams' wall clock, shared with the Close-up (plan ruling P3-5); null for one of its own.</param>
    public SceneController(MainViewModel vm, Func<IRenderHost> hostFactory,
        Func<Outcome, double, ExplosionScene>? sceneFactory = null, Func<double>? wallClock = null, BeamClock? beamClock = null)
    {
        _clock = wallClock ?? (() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
        _beamClock = beamClock ?? new BeamClock();
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        _hostFactory = hostFactory ?? throw new ArgumentNullException(nameof(hostFactory));
        _sceneFactory = sceneFactory ?? ((o, z1) => new ExplosionScene(o, z1));
        BuildScene();
        ResetCameraAngles();
        ComputeState();
        Director.Jump(_state.FrameRadiusCm);
        vm.OutcomeChanged += OnOutcomeChanged;
        vm.Timeline.PropertyChanged += OnTimelineChanged;
        _isNotesExpanded = vm.Layout.IsNotesExpanded;   // remembered across launches (Task 15)
        vm.Layout.PropertyChanged += OnLayoutChanged;
        UpdateOverlay();
    }

    /// <summary>The camera distance at which one scene unit is the view's half-height: 1 / tan(fov / 2).</summary>
    public static double AutoDistance { get; } = 1 / Math.Tan(45 * Math.PI / 360);

    public OrbitCamera Camera { get; } = new() { YawDeg = 30, PitchDeg = 20, DistanceMpc = AutoDistance };

    /// <summary>The director camera (plan ruling P5): holds the scale, steps back by two rungs, jumps on request.</summary>
    public CameraDirector Director { get; } = new();

    private IReadOnlyList<RingLabel> _ringLabels = Array.Empty<RingLabel>();

    /// <summary>The distance rings' labels, outermost first, each at the top of its ring (device-independent pixels).</summary>
    public IReadOnlyList<RingLabel> RingLabels
    {
        get => _ringLabels;
        private set => SetProperty(ref _ringLabels, value);
    }

    /// <summary>What is drawn now.</summary>
    public SceneState State => _state;

    /// <summary>Frames drawn so far (for the --bench frame rate).</summary>
    public long FramesDrawn { get; private set; }

    /// <summary>True when <see cref="Dispose"/> deleted the GL objects with the context current (logged by --snapshot).</summary>
    public bool GlReleased { get; private set; }

    /// <summary>
    /// The particle budget (<see cref="QualityLevel"/>): 200,000, 100,000 or 50,000 debris particles. Changing it re-uploads the set at
    /// the next frame. Setting it is the user's choice (the View menu): it starts the <see cref="QualityGovernor"/> over at that level
    /// and clears the "lowered automatically" note. The governor itself changes it through <see cref="ApplyQuality"/>.
    /// </summary>
    public QualityLevel Quality
    {
        get => _quality;
        set
        {
            _governor.Reset(value);
            if (_qualityNote is not null)
            {
                _qualityNote = null;
                UpdateOverlay();
            }
            ApplyQuality(value);
        }
    }

    private void ApplyQuality(QualityLevel value)
    {
        if (_quality == value) return;
        _quality = value;
        _uploadParticles = true;
        BuildParticles();       // the set (and so v_max) for the new count, before the state is restated
        Restate();
        OnPropertyChanged(nameof(Quality));
        OnPropertyChanged(nameof(IsQualityHigh));
        OnPropertyChanged(nameof(IsQualityMedium));
        OnPropertyChanged(nameof(IsQualityLow));
    }

    /// <summary>The View menu's quality items (radio-style): setting one to true chooses that level; setting it to false does nothing (it stays checked).</summary>
    public bool IsQualityHigh { get => _quality == QualityLevel.High; set => ChooseQuality(value, QualityLevel.High, nameof(IsQualityHigh)); }
    public bool IsQualityMedium { get => _quality == QualityLevel.Medium; set => ChooseQuality(value, QualityLevel.Medium, nameof(IsQualityMedium)); }
    public bool IsQualityLow { get => _quality == QualityLevel.Low; set => ChooseQuality(value, QualityLevel.Low, nameof(IsQualityLow)); }

    private void ChooseQuality(bool on, QualityLevel level, string property)
    {
        if (on) Quality = level;
        else OnPropertyChanged(property);   // a click on the checked item: it stays checked
    }

    /// <summary>
    /// Whether the <see cref="QualityGovernor"/> may lower the quality when the GPU needs more than 20 ms a frame (needs <see cref="GpuTimingAvailable"/>). On by default; the --snapshot and
    /// --bench runs switch it off so a measurement runs at the quality asked for.
    /// </summary>
    public bool AutoQuality { get; set; } = true;

    /// <summary>A frame this long (s) is a pause between on-demand frames, not a frame in a run: its render time is not fed to the governor.</summary>
    public const double MaxGovernedFrameS = 0.5;

    /// <summary>The overlay note after the governor lowers the quality to this level.</summary>
    public static string QualityStepDownNote(QualityLevel level)
        => $"Quality lowered to {level} ({(int)level:N0} particles) automatically: the GPU took more than {1000 / QualityGovernor.MinFps:0} ms to draw a frame. Choose a level in the View menu.";

    /// <summary>
    /// Whether the GPU's time per frame can be measured (GL timer queries work). Without it the governor stays off and only the View &gt;
    /// Quality menu changes the quality (ruling R-P2-30).
    /// </summary>
    public bool GpuTimingAvailable => _gpuTimer?.Available == true;

    /// <summary>
    /// Feeds the governor one rendered frame: its GPU render time and the wall time since the previous frame (both s) (ruling R-P2-30:
    /// render time, not the frame interval, which a 30 Hz host or an event-bound on-demand rate would inflate). Applies a step down.
    /// Public as the test hook for the timer's results.
    /// </summary>
    public void FeedRenderTime(double renderTimeS, double frameIntervalS)
    {
        if (!AutoQuality || frameIntervalS > MaxGovernedFrameS) return;
        if (!_governor.Update(renderTimeS, frameIntervalS)) return;
        _qualityNote = QualityStepDownNote(_governor.Level);
        ApplyQuality(_governor.Level);
        UpdateOverlay();
    }

    private double _gpuSumS;

    /// <summary>The GPU render times collected since <see cref="ResetGpuStats"/> (the governor's own measurement; the bench logs them).</summary>
    public int GpuSamples { get; private set; }

    public double MeanGpuRenderTimeS => GpuSamples > 0 ? _gpuSumS / GpuSamples : double.NaN;

    public double MaxGpuRenderTimeS { get; private set; }

    public void ResetGpuStats() { _gpuSumS = 0; GpuSamples = 0; MaxGpuRenderTimeS = 0; }

    private void RecordGpuTime(double renderTimeS)
    {
        if (!(renderTimeS >= 0) || !double.IsFinite(renderTimeS)) return;
        _gpuSumS += renderTimeS;
        GpuSamples++;
        MaxGpuRenderTimeS = Math.Max(MaxGpuRenderTimeS, renderTimeS);
    }

    /// <summary>True when the scene is drawn through the HDR target with bloom (false before the context exists, or without RGBA16F).</summary>
    public bool HdrEnabled => _hdr?.Enabled == true;

    /// <summary>
    /// [Presentation, schematic] The bloom strength of the HDR pipeline. Kept at Task 4's 0.5 after Task 11's retune: tuned together with
    /// the layer intensities below against these snapshots (default at 2.6 d, 23 d, 1 yr (cutaway) and 30 yr; SN 1987A at 8,000 and 12,000 d; SN 2023ixf at 5 d; SN 1998bw's burst), not against physics.
    /// The tone-map exposure is <see cref="SceneState.Exposure"/>.
    /// </summary>
    private const float BloomStrength = 0.5f;

    /// <summary>
    /// The no-HDR flash quad's alpha now: <see cref="FlashLayer.NoHdrAlpha"/> of the scene's flash, min(1, intensity / 8), when the HDR
    /// target is not in use; 0 otherwise (the flash is then scene light in the HDR target).
    /// </summary>
    public float FlashOverlayAlpha => HdrEnabled ? 0f : FlashLayer.NoHdrAlpha(_state.FlashIntensity);

    /// <summary>True once the GL context and the renderers exist.</summary>
    public bool IsReady => _painter is not null;

    /// <summary>The beams' wall clock (plan ruling P3-5), the same instance the Close-up is given.</summary>
    public BeamClock BeamClock => _beamClock;

    [ObservableProperty] private string _timeText = "";
    [ObservableProperty] private string _radiusText = "";
    [ObservableProperty] private string _notesText = "";
    [ObservableProperty] private bool _showRings = true;
    [ObservableProperty] private bool _isAutoFraming = true;
    /// <summary>The wedge cutaway (plan ruling P7): a quarter of the view removed, the cut faces coloured by element.</summary>
    [ObservableProperty] private bool _isCutaway;
    [ObservableProperty] private bool _isFailed;
    [ObservableProperty] private string _failureMessage = "";

    private double SourceTimeS => _vm.Timeline.ObserverTimeS / _vm.CurrentObserver.TimeDilation;

    /// <summary>The physical size (cm) of one scene unit: the view's half-height, or the frozen one after a zoom.</summary>
    public double UnitCm => _frozenUnitCm ?? Director.HalfHeightCm;

    partial void OnShowRingsChanged(bool value)
    {
        UpdateRings();
        RequestRender();
    }

    /// <summary>The overlay note while the cutaway is on.</summary>
    public const string CutawayNote =
        "Cutaway: colours show elements (schematic); the cut faces' layers and mixing fingers are drawn analytically, not from the particles; " +
        "the plume shapes, the boundary undulation and the small fingers along every layer boundary are schematic: only the reach of the " +
        "nickel plumes and hydrogen downflows is from the lab's model";

    partial void OnIsCutawayChanged(bool value)
    {
        // A camera still at the old default follows the default to its new place (the notch keeps facing the viewer); one the
        // user has turned stays where it is.
        double yaw = _defaultYawDeg < 0 ? _defaultYawDeg + 360 : _defaultYawDeg;
        if (Math.Abs(Camera.YawDeg - yaw) < 1e-6 && Math.Abs(Camera.PitchDeg - _defaultPitchDeg) < 1e-6) ResetCameraAngles();
        UpdateOverlay();
        RequestRender();
    }

    private IReadOnlyList<LegendItem> _legend = Array.Empty<LegendItem>();

    /// <summary>
    /// The cutaway's colour key: one entry for each element layer present in what the cut faces show now (the star's
    /// layers before breakout, the ejecta's after), centre outward. Empty with the cutaway off.
    /// </summary>
    public IReadOnlyList<LegendItem> Legend
    {
        get => _legend;
        private set => SetProperty(ref _legend, value);
    }

    /// <summary>Whether the legend also explains the shock ring (the shock is inside the star and the cutaway is on).</summary>
    public bool ShowShockKey => IsCutaway && double.IsFinite(_state.ShockInStarRadiusCm);

    /// <summary>Whether the key explains the shock shells (they are drawn now: structure §S4).</summary>
    public bool ShowShellKey => _state.Shocks is not null;

    /// <summary>Whether the key explains the pulsar wind nebula (it is drawn now: structure §S9).</summary>
    public bool ShowNebulaKey => _state.Notes.Any(n => n.StartsWith("Pulsar wind nebula", StringComparison.Ordinal));

    /// <summary>Whether the key explains SN 1987A's rings (they are drawn and the view is wide enough to reach them: structure §S8).</summary>
    public bool ShowSn1987aRingsKey => _state.ShowRings1987A && _state.Notes.Contains(Sn1987aRings.Note);

    /// <summary>The nebula's key colour, "#RRGGBB" (the synchrotron blue-white).</summary>
    public string NebulaHex => Hex(NebulaColor);

    /// <summary>The rings' key colour, "#RRGGBB".</summary>
    public string Sn1987aRingsHex => Hex((TorusRingRenderer.RingColor.X, TorusRingRenderer.RingColor.Y, TorusRingRenderer.RingColor.Z));

    /// <summary>Whether the bottom-left key is shown at all: the cutaway's colours, the shock shells, the nebula or the rings.</summary>
    public bool ShowKey => Legend.Count > 0 || ShowShellKey || ShowNebulaKey || ShowSn1987aRingsKey;

    /// <summary>The swept-up shell's key colour, "#RRGGBB".</summary>
    public string ForwardShellHex => Hex(ForwardShellColor);

    /// <summary>The reverse shock's key colour, "#RRGGBB" (the hot-gas tint).</summary>
    public string ReverseShellHex => Hex(ExplosionScene.HotGasColor);

    private static string Hex((float R, float G, float B) c)
        => $"#{(int)Math.Round(255 * c.R):X2}{(int)Math.Round(255 * c.G):X2}{(int)Math.Round(255 * c.B):X2}";

    /// <summary>Whether the legend also explains the Rayleigh-Taylor fingers (they are drawn and the cutaway is on).</summary>
    public bool ShowFingerKey => IsCutaway && _state.ShowParticles && _scene?.Structure is { IsEnvelopeShell: false, Mixing.HasFingers: true };

    private IReadOnlyList<LegendItem> LegendNow()
    {
        if (!IsCutaway) return Array.Empty<LegendItem>();
        IEnumerable<ElementLayer> layers = _state.ShowParticles && _scene?.Structure is { } structure
            ? structure.Layers.Select(l => l.Layer)
            : _state.StarLayers.Select(l => l.Layer);
        return layers.Select(l => new LegendItem(LayerColors.NameOf(l), LayerColors.HexOf(l))).ToList();
    }

    /// <summary>Creates the GL host and its context; on failure shows the message and leaves the rest of the app alone.</summary>
    public void Start()
    {
        if (_disposed || _host is not null || IsFailed) return;
        try
        {
            _host = _hostFactory();
            _host.ContextReady += OnContextReady;
            _host.Draw += OnDraw;
            _host.FrameFailed += OnFrameFailed;
            _host.Start();
            UpdateAnimating();
            _host.RequestRender();
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
        }
    }

    /// <summary>The view's size in device-independent pixels (for the ring labels and marker sizes).</summary>
    public void SetViewSize(Size dip)
    {
        _viewDip = dip;
        UpdateRings();
        RequestRender();
    }

    /// <summary>Drag: turns the view; auto-framing stays on.</summary>
    public void Rotate(double dxDip, double dyDip)
    {
        Camera.Rotate(dxDip, dyDip);
        RequestRender();
    }

    /// <summary>
    /// True while the user holds the mouse down on the view to turn it (Task 15). The guided tour's sway is suspended meanwhile and
    /// carries on from the user's angle after the release. The window sets it with <see cref="BeginUserDrag"/> and <see cref="EndUserDrag"/>.
    /// </summary>
    public bool IsUserDragging { get; private set; }

    public void BeginUserDrag() => IsUserDragging = true;

    public void EndUserDrag() => IsUserDragging = false;

    private bool _isTourOrbiting;

    /// <summary>
    /// True while the guided tour's camera orbits (its stop sways and the tour is travelling or dwelling): the view
    /// then animates every frame (ruling R-P2-32), rather than drawing one on-demand frame per sway step. The tour sets it and
    /// clears it on Pause, Exit and a stop that does not sway.
    /// </summary>
    public bool IsTourOrbiting
    {
        get => _isTourOrbiting;
        set
        {
            if (_isTourOrbiting == value) return;
            _isTourOrbiting = value;
            UpdateAnimating();
            RequestRender();
        }
    }

    /// <summary>Whether the view renders every frame now: a pulsar's beams, a camera ease, or the tour's orbit.</summary>
    public bool IsAnimating => !IsFailed && (_state.Marker == SceneMarker.Pulsar || (Director.IsEasing && _frozenUnitCm is null) || _isTourOrbiting);

    /// <summary>The guided tour's sway (Task 15): sets the camera's yaw (not user input).</summary>
    public void SetTourYaw(double yawDeg)
    {
        double yaw = ((yawDeg % 360) + 360) % 360;
        if (yaw == Camera.YawDeg) return;
        Camera.YawDeg = yaw;
        RequestRender();
    }

    /// <summary>
    /// The largest sway amplitudes (deg), each at most <paramref name="wantedDeg"/>, on the two sides of this centre yaw (Plus: yaw above
    /// the centre; Minus: below it) that keep both cut faces facing the eye over the whole sway at the camera's pitch (Task 15, ruling
    /// R-P2-34: "the cut faces and core are never seen from behind"). A face seen exactly edge-on at an extreme counts as exposed.
    /// Without the cutaway, or with no wedge turn, both are <paramref name="wantedDeg"/>; the wedge turns about the scene's y axis while the sway
    /// turns about the vertical, so with a turned wedge the room is unequal: the full amount on one side, less on the tight one.
    /// </summary>
    public (double Plus, double Minus) SwayAmplitudesFor(double centreYawDeg, double wantedDeg)
        => (SideAmplitude(centreYawDeg, wantedDeg, +1), SideAmplitude(centreYawDeg, wantedDeg, -1));

    private double SideAmplitude(double centreYawDeg, double wantedDeg, int sign)
    {
        if (!IsCutaway) return wantedDeg;
        var cam = new OrbitCamera { PitchDeg = Camera.PitchDeg };
        for (double a = wantedDeg; a > 0; a = Math.Ceiling(a) - 1)
        {
            bool ok = true;
            for (double off = 0; off <= a + 1e-9 && ok; off += 1)
            {
                cam.YawDeg = centreYawDeg + sign * Math.Min(off, a);
                var e = cam.Backward;
                var w = WedgeOrientation.ToWedge(_wedgeAngleRad, e.X, e.Y, e.Z);
                ok = w.X >= -1e-9 && w.Z >= -1e-9;
            }
            if (ok) return a;
        }
        return 0;
    }

    /// <summary>The yaw (0 to 360 deg) that faces the cutaway's notch to the viewer for this outcome: the cutaway's default view (ruling R-P2-22).</summary>
    public double NotchFacingYawDeg => DefaultAngles(cutaway: true).YawDeg;

    /// <summary>
    /// Hands the physical scale back to the director (Task 15: the tour's next stop after a wheel zoom froze it). The camera's
    /// distance and the framing go back as Home does, but the angles stay as they are.
    /// </summary>
    public void HandBackScale()
    {
        if (_frozenUnitCm is null && IsAutoFraming) return;
        _frozenUnitCm = null;
        Camera.DistanceMpc = AutoDistance;
        Camera.Target = default;
        IsAutoFraming = true;
        Director.Jump(_state.FrameRadiusCm);
        UpdateAnimating();
        UpdateRings();
        RequestRender();
    }

    /// <summary>Wheel: zooms (positive notches move closer) and freezes the physical scale until <see cref="Home"/>.</summary>
    public void Zoom(int notches)
    {
        if (notches == 0) return;
        _frozenUnitCm ??= Director.HalfHeightCm;
        Camera.Zoom(notches);
        IsAutoFraming = false;
        UpdateAnimating();   // a zoom freezes the scale: an ease in progress no longer needs frames
        UpdateRings();
        RequestRender();
    }

    /// <summary>Home: auto-framing again.</summary>
    public void Home()
    {
        _frozenUnitCm = null;
        Camera.DistanceMpc = AutoDistance;
        Camera.Target = default;
        ResetCameraAngles();
        IsAutoFraming = true;
        Director.Jump(_state.FrameRadiusCm);
        UpdateAnimating();
        UpdateRings();
        RequestRender();
    }

    /// <summary>
    /// The next frame of the 3D view read back with GL.ReadPixels (ruling R19: no screen capture), or null when the view
    /// has failed or no frame comes within the timeout.
    /// </summary>
    public async Task<BitmapSource?> CaptureFrameAsync(TimeSpan timeout)
    {
        if (_disposed || IsFailed || _host is null) return null;
        _readback?.TrySetResult(null);
        var tcs = new TaskCompletionSource<BitmapSource?>();
        _readback = tcs;
        _host.RequestRender();
        var done = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
        if (done != tcs.Task) tcs.TrySetResult(null);
        return await tcs.Task;
    }

    private void RequestRender()
    {
        if (!_disposed && !IsFailed) _host?.RequestRender();
    }

    /// <summary>A new outcome or observer: a new scene, framed automatically again (review M5: a zoom frozen on one
    /// outcome's scale could hide the next one).</summary>
    private void OnOutcomeChanged(object? sender, EventArgs e)
    {
        BuildScene();
        _frozenUnitCm = null;
        Camera.DistanceMpc = AutoDistance;
        Camera.Target = default;
        ResetCameraAngles();
        IsAutoFraming = true;
        Restate(jump: true);   // Review Focus 4: a new outcome reframes at once, even in the middle of an ease
    }

    private void OnTimelineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TimelineViewModel.ObserverTimeS)) return;
        // Only playback ticks ease; a scrub, marker click or loop wrap is a jump. While a wheel zoom has frozen the scale
        // (ruling R-P2-9) a timeline click keeps it: only Home or a new outcome hands back to the director.
        Restate(jump: !_vm.Timeline.IsPlaybackStep);
    }

    private void Restate(bool jump = false)
    {
        ComputeState();
        if (jump && _frozenUnitCm is null) Director.Jump(_state.FrameRadiusCm);
        UpdateOverlay();
        UpdateAnimating();
        RequestRender();
    }

    private void UpdateAnimating()
    {
        if (_host is not null && !IsFailed) _host.Animating = IsAnimating;
    }

    /// <summary>Each frame: hands the director the wall time since the last frame and the framing radius, then the labels follow.</summary>
    private void StepDirector()
    {
        double now = _clock();
        double dt = _lastFrameS is { } last ? Math.Clamp(now - last, 0, MaxFrameGapS) : 0;
        _lastFrameS = now;
        if (_frozenUnitCm is null) Director.Update(_state.FrameRadiusCm, dt);
        UpdateAnimating();
        UpdateRings();
    }

    /// <summary>Whether the notes overlay shows every note (expanded) or the first only (collapsed, the default). Kept for the session.</summary>
    [ObservableProperty] private bool _isNotesExpanded;

    /// <summary>What the notes overlay shows: every note when expanded, else the first only (<see cref="NotesText"/> is always all of them).</summary>
    [ObservableProperty] private string _notesDisplayText = "";

    /// <summary>The overlay's toggle text: "{n} more notes" with a down arrow when collapsed, "Hide notes" with an up arrow when expanded; empty when there is at most one note.</summary>
    [ObservableProperty] private string _notesToggleText = "";

    /// <summary>The collapsed toggle's text for this many hidden notes.</summary>
    public static string MoreNotesText(int hidden) => hidden == 1 ? "1 more note \u25BE" : $"{hidden} more notes \u25BE";

    public const string HideNotesText = "Hide notes \u25B4";

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ToggleNotes() => IsNotesExpanded = !IsNotesExpanded;

    partial void OnIsNotesExpandedChanged(bool value)
    {
        _vm.Layout.IsNotesExpanded = value;
        UpdateNotesDisplay();
    }

    private void OnLayoutChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LayoutViewModel.IsNotesExpanded)) IsNotesExpanded = _vm.Layout.IsNotesExpanded;
    }

    private void UpdateNotesDisplay()
    {
        var lines = NotesText.Length == 0 ? Array.Empty<string>() : NotesText.Split(Environment.NewLine);
        NotesDisplayText = IsNotesExpanded || lines.Length <= 1 ? NotesText : lines[0];
        NotesToggleText = lines.Length <= 1 ? "" : IsNotesExpanded ? HideNotesText : MoreNotesText(lines.Length - 1);
    }

    /// <summary>The notes that describe what is on screen now (the breakout flash, the cutaway, SN 1987A's rings).</summary>
    private static readonly HashSet<string> EventNotes = new(StringComparer.Ordinal)
    {
        ExplosionScene.FlashNote, CutawayNote, Sn1987aRings.Note,
    };

    /// <summary>The notes that say what the radius readout's lines are (final review I3: they come right after the current event).</summary>
    private static readonly HashSet<string> ReadoutNotes = new(StringComparer.Ordinal)
    {
        ExplosionScene.UniformSphereNote, ExplosionScene.BlendNote, ExplosionScene.UniformRemnantNote, ExplosionScene.RemnantRadiusNote,
    };

    /// <summary>
    /// The group a note sorts into (final review I3), first shown first:
    /// <list type="number">
    /// <item>(the quality note, placed by <see cref="OrderNotes"/>);</item>
    /// <item>what is on screen now (<see cref="EventNotes"/>: the flash, the cutaway, SN 1987A's rings);</item>
    /// <item>the stage the explosion is at (the shock crossing the star, the core collapsing);</item>
    /// <item>the readout's labels (<see cref="ReadoutNotes"/> and the shocks' note);</item>
    /// <item>disclosures of the lab's misses (any note citing a scientific inconsistency);</item>
    /// <item>schematic and structure notes (everything else);</item>
    /// <item>the remnant's marker and the jets, last.</item>
    /// </list>
    /// </summary>
    public static int NoteRank(string note)
    {
        if (EventNotes.Contains(note)) return 1;
        if (note.StartsWith("The shock is crossing the star", StringComparison.Ordinal) ||
            note.StartsWith("The core is collapsing", StringComparison.Ordinal)) return 2;
        if (ReadoutNotes.Contains(note) || note.StartsWith("Shocks: radii from", StringComparison.Ordinal)) return 3;
        if (note.Contains("inconsistency #", StringComparison.Ordinal) || note.Contains("inconsistencies #", StringComparison.Ordinal))
            return 4;
        if (note.Contains("(marker)", StringComparison.Ordinal) || note.Contains("(schematic marker)", StringComparison.Ordinal) ||
            note.Contains("beams slowed down", StringComparison.Ordinal) ||
            note.StartsWith("Gamma-ray-burst jets", StringComparison.Ordinal) ||
            note.StartsWith("The gamma-ray burst's jets", StringComparison.Ordinal)) return 6;
        return 5;
    }

    /// <summary>
    /// The notes in the order the collapsed overlay needs, since it shows the first one only: a "quality lowered" note, then each
    /// <see cref="NoteRank"/> group in turn (the current event, the stage, the readout's labels, disclosures, schematic notes, the
    /// marker and jets), each group in the scene's own order.
    /// </summary>
    public static IReadOnlyList<string> OrderNotes(IEnumerable<string> notes, string? qualityNote)
    {
        var ordered = new List<string>();
        if (qualityNote is not null) ordered.Add(qualityNote);
        ordered.AddRange(notes.Select((n, i) => (n, i)).OrderBy(p => NoteRank(p.n)).ThenBy(p => p.i).Select(p => p.n));
        return ordered;
    }

    /// <summary>
    /// The radius readout: the state's radius line and, while shocks are drawn, a second line for the drawn forward shock (ruling
    /// R-P2-25). Both lines are in one unit, the larger value's (ruling R-P2-33, final review I4), so they compare at a glance. Empty
    /// without a radius.
    /// </summary>
    public static string ReadoutText(SceneState s)
    {
        if (!(double.IsFinite(s.RadiusCm) && s.RadiusCm > 0)) return "";
        if (!(double.IsFinite(s.ForwardShockCm) && s.ForwardShockCm > 0)) return $"{s.RadiusLabel}: {UnitFormat.Distance(s.RadiusCm)}";
        double unit = Math.Max(s.RadiusCm, s.ForwardShockCm);
        return $"{s.RadiusLabel}: {UnitFormat.Distance(s.RadiusCm, unit)}" + Environment.NewLine +
               $"{s.ForwardShockLabel}: {UnitFormat.Distance(s.ForwardShockCm, unit)}";
    }

    private void UpdateOverlay()
    {
        TimeText = _vm.Timeline.ObserverTimeText;
        RadiusText = ReadoutText(_state);
        var notes = IsCutaway ? _state.Notes.Append(CutawayNote) : _state.Notes;
        var all = _modelError is not null ? new List<string> { _modelError } : OrderNotes(notes, _qualityNote).ToList();
        NotesText = string.Join(Environment.NewLine, all);
        UpdateNotesDisplay();
        Legend = LegendNow();
        OnPropertyChanged(nameof(ShowShockKey));
        OnPropertyChanged(nameof(ShowFingerKey));
        OnPropertyChanged(nameof(ShowShellKey));
        OnPropertyChanged(nameof(ShowNebulaKey));
        OnPropertyChanged(nameof(ShowSn1987aRingsKey));
        OnPropertyChanged(nameof(ShowKey));
        UpdateRings();
    }

    /// <summary>
    /// Review M1 (spec §9): the scene's models run inside a catch. A model error keeps the last drawn state and shows a
    /// message in the view's notes instead of ending the app; the panels are unaffected.
    /// </summary>
    private void BuildScene()
    {
        try
        {
            _scene = _sceneFactory(_vm.CurrentOutcome, _vm.CurrentObserver.TimeDilation);
            _uploadParticles = true;   // a new outcome has a new particle set
            _modelError = null;
            BuildParticles();
            ComputeWedgeAngle();
        }
        catch (Exception e)
        {
            _scene = null;
            _modelError = ModelErrorText(e);
        }
    }

    /// <summary>Samples the scene's particles at the current quality (once per outcome and quality; the state and the GPU upload then share this set).</summary>
    private void BuildParticles()
    {
        try { _scene?.ParticlesFor((int)_quality); }
        catch (Exception e) { _modelError = ModelErrorText(e); }
    }

    private void ComputeState()
    {
        if (_scene is null) return;
        try
        {
            _state = _scene.At(SourceTimeS);
            _modelError = null;
        }
        catch (Exception e)
        {
            _modelError = ModelErrorText(e);
        }
    }

    /// <summary>How many model errors the scene has caught since it was made (the sweep counts them, Task 14).</summary>
    public int ModelErrorCount { get; private set; }

    private string ModelErrorText(Exception e)
    {
        ModelErrorCount++;
        Trace.TraceError($"3D view: {e}");
        return $"3D view: model error ({e.GetType().Name}: {e.Message}). The panels are unaffected.";
    }

    /// <summary>The rings now drawn, outermost first, for the view's scale (the director's half-height, or the frozen one).</summary>
    private IReadOnlyList<Ring> CurrentRings() => ShowRings && UnitCm > 0 && double.IsFinite(UnitCm)
        ? DistanceRings.Rungs(UnitCm)
        : Array.Empty<Ring>();

    /// <summary>
    /// Places each ring's label at the top of its circle, from the GL projection (ruling P6): a ring of radius r is r / unit
    /// scene units from the centre, in the plane facing the camera at the camera's distance D, so it is
    /// (r / unit) / (D tan(fov / 2)) of the half-height above the middle of the view. Labels outside the view are dropped.
    /// </summary>
    private void UpdateRings()
    {
        if (!(_viewDip.Width > 0) || !(_viewDip.Height > 0))
        {
            if (_ringLabels.Count > 0) RingLabels = Array.Empty<RingLabel>();
            return;
        }
        double perUnit = 1 / (Camera.DistanceMpc * Math.Tan(Camera.FieldOfViewDeg * Math.PI / 360));
        var labels = new List<RingLabel>();
        foreach (var ring in CurrentRings())
        {
            double y = _viewDip.Height / 2 * (1 - ring.RadiusCm / UnitCm * perUnit);
            if (y < 0 || y > _viewDip.Height) continue;
            labels.Add(new RingLabel(ring.Label, _viewDip.Width / 2, y, ring.Opacity));
        }
        // Final review M1: this runs every frame; a new list only when a label changed, so the ItemsControl does not rebuild at 60 Hz.
        if (!labels.SequenceEqual(_ringLabels)) RingLabels = labels;
    }

    /// <summary>"vendor | renderer | version" as the GL driver reports them (the GPU name for the performance notes); empty before the first frame.</summary>
    public string GlInfo { get; private set; } = "";

    private void OnContextReady(string glInfo)
    {
        GlInfo = glInfo;
        string version = glInfo.Split('|').ElementAtOrDefault(2)?.Trim() ?? "";
        if (!IsSupported(version))
            throw new NotSupportedException($"this computer's OpenGL is {(version.Length > 0 ? version : "unknown")}");
        _painter = new ExplosionPainter((int)QualityLevel.High);
        _hdr = new HdrPipeline();
        _uploadParticles = true;
        _jets = new JetRenderer();
        _remnant = new RemnantRenderer(_jets);
        _rings = new RingRenderer();
        _torusRings = new TorusRingRenderer();
        _cutFaces = new CutFaceRenderer();
        _flash = new FlashRenderer();
        _gpuTimer = new GpuTimer();
    }

    /// <summary>True when a GL version string ("4.6.0 NVIDIA 551.86", "4.1 ...") is 4.1 or later.</summary>
    public static bool IsSupported(string version)
    {
        var m = Regex.Match(version, @"^\s*(\d+)\.(\d+)");
        if (!m.Success) return false;
        int major = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), minor = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        return major > 4 || (major == 4 && minor >= 1);
    }

    private void OnFrameFailed(Exception ex) => Fail(ex.Message);

    private void Fail(string reason)
    {
        FailureMessage = $"The 3D view needs {Requirement} (Intel UHD 620 class graphics or better) and could not use it: " +
                         $"{reason.TrimEnd('.')}. The lab, the timeline and the instrument panels work without it.";
        IsFailed = true;
        if (_host is not null) _host.Animating = false;
        _readback?.TrySetResult(null);
        _readback = null;
    }

    private void OnDraw(TimeSpan delta, Size viewportPx)
    {
        if (_disposed) return;
        double interval = delta.TotalSeconds;
        _gpuTimer?.Poll(t => { RecordGpuTime(t); FeedRenderTime(t, interval); });   // measurements of earlier frames, read late
        StepDirector();
        if (_painter is null || _hdr is null || _jets is null || _remnant is null || _rings is null) return;
        FramesDrawn++;
        if (_uploadParticles) UploadParticles();
        var s = _state;
        _gpuTimer?.Begin();
        _hdr.Begin(viewportPx);
        GL.ColorMask(true, true, true, false);   // alpha is the cut faces' mask for the tone map: only the faces write it
        Action? cutFaces = null;
        double unit = UnitCm;
        if (unit > 0 && double.IsFinite(unit) && viewportPx.Width > 0 && viewportPx.Height > 0)
        {
            double pxPerDip = _viewDip.Height > 0 ? viewportPx.Height / _viewDip.Height : 1;
            // The wedge: the particles in the removed quarter are dropped here; the cut faces (debris and star) are painted after
            // the bloom is built (below), so they feed no glow (rulings R-P2-21 and R-P2-23).
            bool starCut = IsCutaway && s.ShowStar && s.StarLayers.Count > 0 && s.StarRadiusCm > 0;
            bool particleCut = IsCutaway && s.ShowParticles && _painter.Uploaded is not null;
            var faceColors = starCut || particleCut ? FaceColors() : Array.Empty<(float R, float G, float B)>();
            if (_cutFaces is not null) _cutFaces.WedgeAngleRad = _wedgeAngleRad;
            _painter.Paint(Camera, viewportPx, pxPerDip, s, unit, starCut, particleCut, IsCutaway, _wedgeAngleRad);
            if (s.ShowJets) _jets.RenderJets(Camera, viewportPx, s.JetHalfAngleRad, s.FrameRadiusCm / unit, hdr: HdrEnabled);
            foreach (var ring in CurrentRings()) _rings.Render(Camera, viewportPx, ring.RadiusCm / unit, ring.Opacity);
            if (s.ShowRings1987A) _torusRings?.Render(Camera, viewportPx, pxPerDip, unit, s.RingBrightness);
            double phase = _beamClock.PhaseRad(ExplosionScene.ShownPulsarPeriodS);   // plan ruling P3-5: one clock for both views
            // The beams reach 0.9 of the framing radius, as before the director (the scene unit is now the half-height).
            double beam = double.IsFinite(s.FrameRadiusCm) ? 0.9 * s.FrameRadiusCm / unit : 0.9;
            _remnant.Render(Camera, viewportPx, pxPerDip, s.Marker, phase, beam);
            // The breakout flash is scene light in the HDR target, so it blooms; without the target it is drawn after the frame.
            if (s.FlashIntensity > 0 && HdrEnabled) _flash?.RenderHdr(viewportPx, s.FlashColor, s.FlashIntensity);
            if (starCut || particleCut)
            {
                // Final review M1: one cached delegate reads this frame's inputs from a field (no closure per frame).
                _cutFrame = (viewportPx, s, _painter.Uploaded, unit, faceColors, starCut, particleCut);
                cutFaces = _drawCutFacesNow ??= DrawCutFacesNow;
            }
        }
        GL.ColorMask(true, true, true, true);
        _hdr.End(s.Exposure, BloomStrength, cutFaces);
        if (FlashOverlayAlpha > 0) _flash?.RenderNoHdr(viewportPx, s.FlashColor, FlashOverlayAlpha);
        _gpuTimer?.End();
        if (_readback is { } readback)
        {
            _readback = null;
            readback.TrySetResult(ReadPixels(viewportPx));
        }
    }

    /// <summary>The camera's default direction (yaw 30, pitch 20 deg), as seen from the wedge: the notch faces the viewer.</summary>
    private const double DefaultYawDeg = SkyFrame.DefaultYawDeg, DefaultPitchDeg = SkyFrame.DefaultPitchDeg;

    private double _wedgeAngleRad, _defaultYawDeg = DefaultYawDeg, _defaultPitchDeg = DefaultPitchDeg;

    /// <summary>The wedge's angle about the scene's y axis for this outcome (ruling R-P2-22), 0 without fingers.</summary>
    public double WedgeAngleRad => _wedgeAngleRad;

    private void ComputeWedgeAngle()
        => _wedgeAngleRad = _scene?.Structure is { IsEnvelopeShell: false, Mixing.HasFingers: true } st
            ? WedgeOrientation.AngleFor(ConesFor(st)) : 0;

    /// <summary>
    /// The camera's default angles: the original yaw/pitch seen from the wedge, i.e. turned with it (ruling R-P2-22), so the notch
    /// faces the viewer as it always did. Only while the cutaway is on; with it off the view is the original 30/20.
    /// </summary>
    private void ResetCameraAngles()
    {
        (_defaultYawDeg, _defaultPitchDeg) = DefaultAngles(IsCutaway);
        Camera.YawDeg = _defaultYawDeg < 0 ? _defaultYawDeg + 360 : _defaultYawDeg;
        Camera.PitchDeg = _defaultPitchDeg;
    }

    /// <summary>The default view's yaw (as it is stored, in -180 to 180 here, 0 to 360 for <see cref="NotchFacingYawDeg"/>) and pitch.</summary>
    private (double YawDeg, double PitchDeg) DefaultAngles(bool cutaway)
    {
        double phi = cutaway ? _wedgeAngleRad : 0, yaw = DefaultYawDeg * Math.PI / 180, pitch = DefaultPitchDeg * Math.PI / 180;
        var d = WedgeOrientation.FromWedge(phi, Math.Cos(pitch) * Math.Cos(yaw), Math.Cos(pitch) * Math.Sin(yaw), Math.Sin(pitch));
        double y = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
        return (y < 0 ? y + 360 : y, Math.Asin(Math.Clamp(d.Z, -1, 1)) * 180 / Math.PI);
    }

    private CutFaceRenderer? _cutFaces;
    private EjectaStructure? _conesFor;
    private FingerCones _cones = FingerCones.Empty;

    /// <summary>The finger cones of the structure's particle set (cached: they are the sampler's, one set of axes per outcome).</summary>
    private FingerCones ConesFor(EjectaStructure structure)
    {
        if (!ReferenceEquals(_conesFor, structure))
        {
            _cones = MixingModel.Cones(structure.Mixing, ParticleSampler.DefaultSeed);
            _conesFor = structure;
        }
        return _cones;
    }

    /// <summary>The inputs of this frame's cut faces, read by <see cref="DrawCutFacesNow"/> inside <see cref="HdrPipeline.End"/>.</summary>
    private (Size Viewport, SceneState State, ParticleSet? Set, double Unit, (float R, float G, float B)[] Colors, bool StarCut, bool ParticleCut) _cutFrame;
    private Action? _drawCutFacesNow;

    /// <summary>The cut faces of the frame being drawn (<see cref="_cutFrame"/>): the debris' faces and the star's.</summary>
    private void DrawCutFacesNow()
    {
        var (viewportPx, s, set, unit, colors, starCut, particleCut) = _cutFrame;
        GL.ColorMask(true, true, true, true);
        if (particleCut && set is not null) DrawCutFaces(viewportPx, s, set, unit, colors);
        if (!starCut || _painter is null) return;
        if (!ReferenceEquals(_starCutFor, s) || !ReferenceEquals(_starCutColors, colors))
        {
            var layers = s.StarLayers.Select(l => ((float)(l.OuterRadiusCm / s.StarRadiusCm), ToVector(colors[(int)l.Layer]))).ToList();
            float shock = double.IsFinite(s.ShockInStarRadiusCm) ? (float)(s.ShockInStarRadiusCm / s.StarRadiusCm) : float.NaN;
            _starCut = new StarCutaway(layers, shock);
            _starCutFor = s;
            _starCutColors = colors;
        }
        _painter.Star.RenderCutFaces(Camera, viewportPx, s.StarRadiusCm / unit, _starCut!);
    }

    private SceneState? _starCutFor;
    private (float R, float G, float B)[]? _starCutColors;
    private StarCutaway? _starCut;

    /// <summary>
    /// The debris faces' uniforms, cached per drawn state, unit, particle set and face colours (final review M1): building them runs
    /// <see cref="CutFaceModel.Build"/> and several lists, which a frame of an unchanged state (the tour's orbit, a pulsar's beams)
    /// need not redo.
    /// </summary>
    private (SceneState? State, double Unit, ParticleSet? Set, (float R, float G, float B)[]? Colors, CutFaceUniforms? Uniforms, float Extent) _faceCache;

    /// <summary>
    /// The debris' two cut faces (ruling R-P2-21), drawn after the bloom is built (<see cref="HdrPipeline.End"/>'s hook), over
    /// the particles and shells; the tone map's face mask (alpha) is written by the faces only. Their radii are confined behind the
    /// reverse shock as the particles are (Task 8).
    /// </summary>
    private void DrawCutFaces(Size viewportPx, SceneState s, ParticleSet set, double unit, (float R, float G, float B)[] colors)
    {
        if (_scene?.Structure is not { } structure || _cutFaces is null) return;
        var c = _faceCache;
        if (ReferenceEquals(c.State, s) && c.Unit == unit && ReferenceEquals(c.Set, set) && ReferenceEquals(c.Colors, colors)
            && (c.Uniforms is null || c.Uniforms.Faces.WedgeAngleRad == _wedgeAngleRad))
        {
            if (c.Uniforms is not null) _cutFaces.Render(Camera, viewportPx, c.Extent, c.Uniforms);
            return;
        }
        var built = BuildFaceUniforms(structure, s, set, unit, colors, out float extent);
        _faceCache = (s, unit, set, colors, built, extent);
        if (built is not null) _cutFaces.Render(Camera, viewportPx, extent, built);
    }

    private CutFaceUniforms? BuildFaceUniforms(EjectaStructure structure, SceneState s, ParticleSet set, double unit,
        (float R, float G, float B)[] colors, out float extent)
    {
        extent = 0;
        double starCm = double.IsFinite(s.StarRadiusCm) ? s.StarRadiusCm : 0;
        var model = CutFaceModel.Build(structure, ConesFor(structure), s.SinceBreakoutS, starCm, set.MaxCmS, s.Shocks, _wedgeAngleRad);
        if (model is null) return null;
        var layerColors = model.Layers.Select(l => ToVector(colors[(int)l.Layer])).ToList();
        var uniforms = new CutFaceUniforms(model, layerColors,
            ToVector(colors[(int)ElementLayer.NickelIron]), ToVector(colors[(int)ElementLayer.Hydrogen]),
            (float)(model.TransitionRadiusCm / unit), (float)model.OuterIndex)
        {
            FreeScale = (float)((starCm + set.MaxCmS * s.SinceBreakoutS) / unit),
            Reverse = (float)((s.Shocks?.ReverseCm ?? 0) / unit),
            Contact = (float)((s.Shocks?.ContactCm ?? 0) / unit),
            ShockWeight = (float)(s.Shocks?.ConfinementWeight ?? 0),
            ShockRatio = (float)ParticleKinematics.ShockedSpeedRatio(s.Shocks, s.SinceBreakoutS, starCm, set.MaxCmS),
        };
        extent = (float)(model.OuterRadiusCm / unit);
        return uniforms;
    }

    private static OpenTK.Mathematics.Vector3 ToVector((float R, float G, float B) c) => new(c.R, c.G, c.B);

    /// <summary>The pulsar wind nebula's colour (schematic): a synchrotron blue-white.</summary>
    public static readonly (float R, float G, float B) NebulaColor = (0.15f, 0.40f, 1.00f);

    /// <summary>[Presentation, tuned in Task 11 (2 to 1)] The nebula's HDR brightness along its brightest line of sight (through its centre); at 2 it added to the swept-up shell's white-out.</summary>
    public const float NebulaIntensity = 1f;

    /// <summary>[Presentation, kept in Task 11] How much of the debris behind the nebula's centre it hides (0 to 1), so a blue bubble shows against bright debris.</summary>
    public const float NebulaCover = 0.85f;

    /// <summary>The swept-up shell's colour: a pale blue-white (schematic), for gas heated by the forward shock.</summary>
    public static readonly (float R, float G, float B) ForwardShellColor = (0.80f, 0.88f, 1.00f);

    /// <summary>
    /// [Presentation, tuned in Task 11 (1.5 to 0.45 and 2.0 to 1.0)] HDR brightness of the brightest line of sight through the swept-up shell
    /// and the reverse-shock shell. At the first guess the swept-up shell filled the view with white from about 20 days on (the 30-year
    /// frame's interior was a white disc); at these values the debris show through it and its limb brightening reads as a rim.
    /// </summary>
    public const float ForwardShellIntensity = 0.45f, ReverseShellIntensity = 1.0f;

    /// <summary>The reverse shock is drawn as a thin shell this fraction of its radius thick (at least <see cref="ReverseShellMinPx"/> pixels).</summary>
    public const double ReverseShellFraction = 0.015, ReverseShellMinPx = 2;

    /// <summary>The wind haze: faint grey-blue (schematic).</summary>
    public static readonly (float R, float G, float B) HazeColor = (0.55f, 0.65f, 0.85f);

    /// <summary>The dense shell's colour (schematic): a little warmer than the haze.</summary>
    public static readonly (float R, float G, float B) DenseShellColor = (0.75f, 0.70f, 0.80f);

    /// <summary>The colour of the gas where the shock lights it (schematic): pale blue-white.</summary>
    public static readonly (float R, float G, float B) GlowColor = (0.85f, 0.92f, 1.00f);

    /// <summary>
    /// [Presentation, tuned in Task 11 (0.06 to 0.18, with the haze visible faintly around SN 1998bw's star and in the day-5 and day-2.6 frames)] The haze's HDR brightness at an impact parameter of 2 R* (the lines of sight
    /// nearest the star), for A_* = 10 (the lab's ordinary red-supergiant wind); the brightness elsewhere follows the column
    /// density (about 1/b). Faint, so the ejecta and shocks stay the picture.
    /// </summary>
    public const float HazeIntensityAtTenAStar = 0.18f;

    /// <summary>[APPROX, kept in Task 11] The haze's brightness changes by this factor for each decade of A_* (a log scale, ∝ A_*^0.25), clamped to <see cref="HazeMinScale"/>–<see cref="HazeMaxScale"/>.</summary>
    public const double HazeScalePerDecade = 0.25, HazeMinScale = 0.25, HazeMaxScale = 2.0;

    /// <summary>[Presentation, tuned in Task 11 (0.12 to 0.15; 0.36 turned SN 2023ixf's lit confined shell into a flat white disc)] The dense shell's HDR brightness at its brightest line of sight, and how much the glow adds to it (so it flares when hit); the flare factor is kept.</summary>
    public const float DenseShellIntensity = 0.15f, DenseShellFlare = 3f;

    /// <summary>[Presentation, tuned in Task 11 (1.2 to 0.6)] The lit gas at the forward shock: a thin shell of this fraction of R_f, at most this HDR brightness (times the glow; the dense gas lights this much more).</summary>
    public const double GlowShellFraction = 0.04;
    public const float GlowShellIntensity = 0.6f, DenseGlowGain = 2f;

    /// <summary>The haze's brightness scale for a wind of this A_* (1 at A_* = 10): 10^(0.25 (log10 A_* − 1)), clamped.</summary>
    public static double HazeScale(double aStar)
        => aStar > 0 ? Math.Clamp(Math.Pow(10, HazeScalePerDecade * (Math.Log10(aStar) - 1)), HazeMinScale, HazeMaxScale) : 0;

    /// <summary>
    /// The cut faces' colours, by layer: through the HDR path, the values that tone-map to the element's hex
    /// (<see cref="LayerColors.FaceHdr"/>); without it, the hex itself.
    /// </summary>
    private (float R, float G, float B)[] FaceColors()
    {
        // Final review M1: the same array while the HDR path and the exposure are unchanged (the caches above key on it).
        bool hdr = HdrEnabled;
        float exposure = _state.Exposure;
        if (_faceColors is { } cached && _faceColorsHdr == hdr && _faceColorsExposure == exposure) return cached;
        var colors = new (float R, float G, float B)[LayerColors.Count];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = hdr ? LayerColors.FaceHdr((ElementLayer)i, exposure) : LayerColors.For((ElementLayer)i);
        (_faceColors, _faceColorsHdr, _faceColorsExposure) = (colors, hdr, exposure);
        return colors;
    }

    private (float R, float G, float B)[]? _faceColors;
    private bool _faceColorsHdr;
    private float _faceColorsExposure;

    /// <summary>
    /// [Presentation, tuned in Task 11 (1 to 0.5)] The debris' overall brightness against Project 1's: at 1 the core of the debris near
    /// the light curve's peak (2.6 d) was a featureless white blob once the layered particles replaced Project 1's points; at 0.5 the
    /// cloud's grain and its mixing fingers show while it still reads as the brightest thing in the view.
    /// </summary>
    public const float ParticleGain = 0.5f;

    /// <summary>
    /// Uploads the scene's particle set at the current quality (built once per outcome and count by
    /// <see cref="ExplosionScene.ParticlesFor"/>); nothing for an outcome without ejecta.
    /// </summary>
    private void UploadParticles()
    {
        _uploadParticles = false;
        _painter!.Upload(_scene?.ParticlesFor((int)_quality));
    }

    /// <summary>The frame just drawn, top row first, as a 96-dpi bitmap; null if the read fails (the error is cleared).</summary>
    private static BitmapSource? ReadPixels(Size viewportPx)
    {
        int w = (int)viewportPx.Width, h = (int)viewportPx.Height, stride = 4 * w;
        if (w <= 0 || h <= 0) return null;
        var bottomUp = new byte[stride * h];
        GL.ReadPixels(0, 0, w, h, OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, bottomUp);
        if (GL.GetError() != OpenTK.Graphics.OpenGL4.ErrorCode.NoError) return null;
        var topDown = new byte[bottomUp.Length];
        for (int y = 0; y < h; y++) System.Buffer.BlockCopy(bottomUp, y * stride, topDown, (h - 1 - y) * stride, stride);
        var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgr32, null, topDown, stride);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Stops listening to the view model and releases the GL objects. Call it while the GL control is still loaded (the
    /// window's Closing): GLWpfControl releases its context when it unloads, after which the objects cannot be deleted.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _vm.OutcomeChanged -= OnOutcomeChanged;
        _vm.Timeline.PropertyChanged -= OnTimelineChanged;
        _vm.Layout.PropertyChanged -= OnLayoutChanged;
        _readback?.TrySetResult(null);
        _readback = null;
        if (_host is { } host)
        {
            host.ContextReady -= OnContextReady;
            host.Draw -= OnDraw;
            host.FrameFailed -= OnFrameFailed;
            // Task 16 re-review N2: this runs inside the window's Closing, so a GL error must never stop the window closing.
            try
            {
                GlReleased = host.Shutdown(() =>
                {
                    _gpuTimer?.Dispose();
                    _flash?.Dispose();
                    _cutFaces?.Dispose();
                    _torusRings?.Dispose();
                    _rings?.Dispose();
                    _remnant?.Dispose();
                    _jets?.Dispose();
                    _painter?.Dispose();
                    _hdr?.Dispose();
                });
            }
            catch (Exception e)
            {
                GlReleased = false;
                Debug.WriteLine($"SceneController: releasing the GL objects failed: {e}");
            }
        }
        _gpuTimer = null;
        _flash = null;
        _cutFaces = null;
        _rings = null;
        _torusRings = null;
        _remnant = null;
        _jets = null;
        _painter = null;
        _hdr = null;
    }
}

/// <summary>One entry of the cutaway's colour key: an element layer's name and its colour as "#RRGGBB".</summary>
public sealed record LegendItem(string Name, string ColorHex);

/// <summary>A distance ring's label: its text, its position (device-independent pixels from the view's top left) and opacity.</summary>
public sealed record RingLabel(string Text, double XDip, double YDip, double Opacity);
