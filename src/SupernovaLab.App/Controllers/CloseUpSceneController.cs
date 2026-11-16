using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SupernovaLab.App.Services;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Rendering;

namespace SupernovaLab.App.Controllers;

/// <summary>
/// The Close-up's GL objects, made together once the context exists (<see cref="Create"/>). Task 4b fix round 1: a seam, so tests can
/// give the controller a fake <see cref="ILensingPass"/> (and nothing else) and drive its per-frame lensing decisions without OpenGL.
/// </summary>
public sealed record CloseUpGpu(ILensingPass Lensing, HdrPipeline? Hdr = null, EnvironmentCapture? Environment = null,
    ExplosionPainter? Painter = null, RingRenderer? Rings = null, GpuTimer? Timer = null, NeutronStarRenderer? NeutronStar = null,
    NeutronStarCutawayRenderer? Cutaway = null, CoreCollapseRenderer? Core = null, LensOverlayRenderer? Overlay = null,
    JetRenderer? Jets = null)
{
    /// <summary>The real objects (call with the GL context current).</summary>
    public static CloseUpGpu Create()
    {
        var hdr = new HdrPipeline();
        var environment = new EnvironmentCapture(hdr.Enabled);
        var painter = new ExplosionPainter((int)QualityLevel.Low);
        var rings = new RingRenderer();
        var timer = new GpuTimer();
        var neutronStar = new NeutronStarRenderer();
        var cutaway = new NeutronStarCutawayRenderer();
        var core = new CoreCollapseRenderer();
        var overlay = new LensOverlayRenderer();
        var jets = new JetRenderer();
        return new CloseUpGpu(new LensingPass(hdr.Enabled), hdr, environment, painter, rings, timer, neutronStar, cutaway, core, overlay, jets);
    }
}
/// <summary>
/// The Close-up window's 3D view (spec §2, §6.6), on <see cref="SceneController"/>'s pattern with a GL host of its own: an
/// <see cref="IRenderHost"/> made by the factory given (a fake in tests), the failure message when OpenGL 4.1 is missing (spec §8.4),
/// a GPU timer, and the GL objects released with the context current on <see cref="Dispose"/> (<see cref="GlReleased"/>).
/// <para>Each frame: the environment cube map is refreshed if ruling P3-6 calls for it (Project 2's scene at Low quality, drawn from
/// the remnant's position by an <see cref="ExplosionPainter"/> of this context), then the <see cref="LensingPass"/> draws the light
/// bending through its cached ray map (ruling R-P3-10: a <see cref="RayMapPlanner"/> retraces it only when the camera, spin, surface,
/// viewport or level change, at the moving level while the camera moves, and refines it to the still level in bands once it stops;
/// the levels follow the user's <see cref="LensingDetail"/>, Auto being a <see cref="LensingGovernor"/>), then the distance rings,
/// then the main view's bloom and tone map at its exposure. A time step that changes nothing it draws asks for no frame (R-P3-11).
/// It skips drawing while the window is hidden or minimized (<see cref="SetVisible"/>, spec §2.5).</para>
/// <para>Plan Task 6: a neutron star's surface is shaded in the lensing pass (its blackbody colour tone-mapped,
/// <see cref="ToneMappedSurface"/>, and markings turned by the spin phase); its beams are drawn before the bloom and its field lines,
/// light cylinder and Earth arrow after it (<see cref="NeutronStarRenderer"/>), all turned by <see cref="BeamPhaseRad"/>, the main
/// view's phase from the shared <see cref="BeamClock"/>. A neutron star keeps the view drawing (<see cref="IsAnimating"/>), paused
/// or not. A refinement band or a whole map that keeps failing is given up after <see cref="MaxConsecutiveFailures"/> in a row.</para>
/// <para>Camera (spec §2.2): <see cref="OrbitCamera"/>, <see cref="CameraDirector"/> and <see cref="DistanceRings"/>, as the main
/// view, centred on the compact object. The scene unit is 1 km (plan ruling P3-1): the camera's distance carries the scale
/// (<see cref="SceneController.AutoDistance"/> half-heights while auto-framing). Drag rotates; the wheel zooms and freezes the scale;
/// Home hands the camera back to the director. The lensing pass gets the camera in GM/c², converted here.</para>
/// </summary>
public sealed partial class CloseUpSceneController : ObservableObject, IDisposable
{
    /// <summary>The longest wall-clock gap one frame hands the director.</summary>
    private const double MaxFrameGapS = 0.1;

    /// <summary>The scene unit (plan ruling P3-1): 1 km.</summary>
    public const double UnitCm = 1e5;

    /// <summary>The camera's default pitch: side-on to the spin axis (spec §6.4).</summary>
    public const double DefaultPitchDeg = 0;

    /// <summary>The camera's default yaw.</summary>
    public const double DefaultYawDeg = 30;

    /// <summary>
    /// Plan Task 7: the camera's pitch while the cutaway is on, so the camera looks into the removed wedge (x &gt; 0, z &gt; 0) and sees
    /// both cut faces (a side-on camera sees the z = 0 face edge-on).
    /// </summary>
    public const double CutawayPitchDeg = 30;

    private double? _pitchBeforeCutaway;

    /// <summary>
    /// The camera's pitch when the cutaway switches: on goes to <see cref="CutawayPitchDeg"/> and remembers the current pitch; off restores
    /// the remembered one (the default when none). Returns the new pitch and the pitch to remember.
    /// </summary>
    public static (double Pitch, double? Saved) CutawayPitch(bool on, double current, double? saved)
        => on ? (CutawayPitchDeg, current) : (saved ?? DefaultPitchDeg, null);

    /// <summary>
    /// [Presentation, spec C12] The surface's tone-map input: its blackbody colour, linearised, at this level ÷ the exposure, so the
    /// surface shows at about 0.88 of full brightness after the tone map whatever the surroundings' exposure. Tuned after Task 6's
    /// launch: at 1.1 the disc and its bloom saturated and hid the surface's markings.
    /// </summary>
    public const float SurfaceLevel = 0.8f;

    /// <summary>
    /// [Presentation] The lines' level (field lines, light cylinder, arrow), as <see cref="SurfaceLevel"/>: low enough that their
    /// colours do not saturate to white (Task 6's launch, at 0.9).
    /// </summary>
    public const float OverlayLevel = 0.6f;

    /// <summary>
    /// [Presentation] The beams' level ÷ the exposure: about the main view's beam brightness (its cones at gain 1 and alpha 0.8, at
    /// exposure 0.2). At 0.9 (Task 6's launch) the wide cones washed the view out.
    /// </summary>
    public const float BeamLevel = 0.3f;

    /// <summary>
    /// [Presentation] Plan Task 11: the collapsar jets' base, its level ÷ the exposure; faded with the disk. Task 11's launch: at 0.6 the
    /// cones were a saturated white band over the disk and the shadow, so 0.12, under the beams' 0.3 (not yet seen in a launch).
    /// </summary>
    public const float JetLevel = 0.12f;

    /// <summary>
    /// [Presentation] Plan Task 9: the core's level ÷ the exposure (the proto-neutron star at full neutrino brightness reads at about
    /// the neutron star's surface level; the translucent shock and gain shell and the particles are scaled the same way).
    /// </summary>
    public const float CoreLevel = 0.8f;

    /// <summary>[Presentation] The beams' length, in half-heights of the view: they always reach past its edge.</summary>
    public const double BeamLengthHalfHeights = 1.6;

    /// <summary>Task 4b review carry-over: a refinement band or a whole map failing this many times in a row is given up.</summary>
    public const int MaxConsecutiveFailures = 3;

    private readonly CloseUpViewModel _vm;
    private readonly Func<IRenderHost> _hostFactory;
    private readonly Func<Outcome, double, ExplosionScene> _envSceneFactory;
    private readonly Func<CloseUpGpu> _gpuFactory;
    private readonly Func<double> _clock;
    private IRenderHost? _host;
    private double? _lastFrameS;
    private double? _frozenHalfHeightCm;
    private Size _viewDip;
    private bool _visible = true;
    private bool _disposed;
    private TaskCompletionSource<BitmapSource?>? _readback;

    // GL objects (null until the context exists).
    private HdrPipeline? _hdr;
    private ILensingPass? _lensing;
    private EnvironmentCapture? _environment;
    private ExplosionPainter? _painter;
    private RingRenderer? _rings;
    private GpuTimer? _gpuTimer;
    private NeutronStarRenderer? _neutronStar;
    private NeutronStarCutawayRenderer? _cutawayRenderer;
    private CoreCollapseRenderer? _coreRenderer;
    private LensOverlayRenderer? _overlay;
    private JetRenderer? _jets;   // plan Task 11

    // Plan Task 9: the infalling matter's fall phase (cycles, wrapped to [0, 1)), accumulated frame by frame so a change of the shown
    // slow-down never makes the particles jump (as ruling R-P3-18's beam clock), and the wall time of the last core frame.
    private double _infallCycles;
    private double? _lastCoreFrameS;

    // Task 4b review carry-over: failures in a row, and what was given up (reset with a new context or viewport).
    private int _bandFailuresInRow, _traceFailuresInRow;
    private bool _refineGivenUp, _directFallback;

    // The environment's scene (Project 2's, rebuilt on a new outcome) and what was last captured (ruling P3-6).
    private ExplosionScene? _envScene;
    private bool _uploadEnvParticles = true;
    private double _envLogT = double.NaN;
    private Outcome? _envOutcome;
    private object? _envObserver;
    private int _envRun;
    private double _envWallS = double.NegativeInfinity;
    private bool _envPending;
    private bool _envHaze;   // ruling R-P3-21: the cube map holds the haze inside the star

    /// <summary>Ruling R-P3-21: whether the lensing environment is the haze inside the star now (the test hook).</summary>
    public bool EnvironmentIsHaze => _envHaze;

    /// <summary>Ruling R-P3-21: the haze's colour in this view's target (linear HDR, or sRGB without it).</summary>
    public (float R, float G, float B) HazeColor => HdrEnabled ? CloseUpScene.InsideStarHazeHdr : CloseUpScene.InsideStarHazeSrgb;
    private float _exposure = 1f;

    /// <param name="envSceneFactory">Builds the environment's scene of an outcome at a time dilation; null for <see cref="ExplosionScene"/>'s own.</param>
    /// <param name="wallClock">Wall-clock seconds; null for the real clock (tests pass a fake).</param>
    /// <param name="gpuFactory">Makes the GL objects once the context exists; null for the real ones (tests pass a fake lensing pass).</param>
    /// <param name="beamClock">The beams' wall clock shared with the main view (plan ruling P3-5); the beams are drawn from Task 6.</param>
    public CloseUpSceneController(CloseUpViewModel vm, Func<IRenderHost> hostFactory, BeamClock? beamClock = null,
        Func<double>? wallClock = null, Func<Outcome, double, ExplosionScene>? envSceneFactory = null,
        Func<CloseUpGpu>? gpuFactory = null)
    {
        _gpuFactory = gpuFactory ?? CloseUpGpu.Create;
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        _hostFactory = hostFactory ?? throw new ArgumentNullException(nameof(hostFactory));
        BeamClock = beamClock ?? new BeamClock();
        _clock = wallClock ?? (() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
        // Plan Task 6: the surroundings' scene knows the observer and run, so it is captured from where the kick has carried the star.
        _envSceneFactory = envSceneFactory ?? ((o, z1) => new ExplosionScene(o, z1, _vm.Main.CurrentObserver, _vm.Main.RunNumber));
        BuildEnvironmentScene();
        Home(jumpOnly: true);
        vm.StateChanged += OnStateChanged;
        vm.PropertyChanged += OnViewModelChanged;
        vm.Main.Timeline.PropertyChanged += OnTimelineChanged;
        (_drawnState, _drawnSwitches) = (vm.State, DrawnSwitches());
    }

    /// <summary>The beams' clock, the same instance as the main view's (plan ruling P3-5).</summary>
    public BeamClock BeamClock { get; }

    /// <summary>
    /// Plan Task 6: the neutron star's spin phase now (radians, not wrapped): the shared <see cref="BeamClock"/> at the drawn period, the
    /// main view's (<see cref="SceneController.BeamPhaseRad"/>); it turns the beams, the field lines and the surface's markings. 0 for
    /// anything but a neutron star.
    /// </summary>
    public double BeamPhaseRad => _vm.State.NeutronStar is not null ? BeamClock.PhaseRad(_vm.State.DrawnPeriodS) : 0;

    /// <summary>Whether the beams are drawn now (spec C6: from the magnetosphere's formation).</summary>
    public bool ShowsBeams => _vm.State.ShowBeams;

    private IReadOnlyList<RingLabel> _viewLabels = Array.Empty<RingLabel>();

    /// <summary>Plan Task 6: the view's own labels ("To Earth" at the arrow's tip, "Light cylinder" on its ring), DIP, while on screen.</summary>
    public IReadOnlyList<RingLabel> ViewLabels
    {
        get => _viewLabels;
        private set => SetProperty(ref _viewLabels, value);
    }

    public OrbitCamera Camera { get; } = new() { YawDeg = DefaultYawDeg, PitchDeg = DefaultPitchDeg };

    public CameraDirector Director { get; } = new();

    private IReadOnlyList<RingLabel> _ringLabels = Array.Empty<RingLabel>();

    /// <summary>The distance rings' labels, outermost first, each at the top of its ring (DIP).</summary>
    public IReadOnlyList<RingLabel> RingLabels
    {
        get => _ringLabels;
        private set => SetProperty(ref _ringLabels, value);
    }

    /// <summary>Frames drawn (the bench's frame count).</summary>
    public long FramesDrawn { get; private set; }

    /// <summary>Frames the host offered while the window was hidden or minimized, not drawn (spec §2.5).</summary>
    public long SkippedFrames { get; private set; }

    /// <summary>True when <see cref="Dispose"/> released the GL objects with the context current.</summary>
    public bool GlReleased { get; private set; }

    /// <summary>True once the GL context and the renderers exist.</summary>
    public bool IsReady => _lensing is not null;

    /// <summary>"vendor | renderer | version"; empty before the first frame.</summary>
    public string GlInfo { get; private set; } = "";

    public bool HdrEnabled => _hdr?.Enabled == true;

    public bool GpuTimingAvailable => _gpuTimer?.Available == true;

    [ObservableProperty] private bool _isFailed;
    [ObservableProperty] private string _failureMessage = "";
    [ObservableProperty] private bool _isAutoFraming = true;

    /// <summary>Whether the window is shown and not minimized (it draws only then).</summary>
    public bool IsVisible => _visible;

    /// <summary>The bench's switch: draw every frame, as a pulsar's beams or the settling fade will (Tasks 6 and 10).</summary>
    public bool ForceAnimating
    {
        get => _forceAnimating;
        set { _forceAnimating = value; UpdateAnimating(); RequestRender(); }
    }

    private bool _forceAnimating;

    /// <summary>
    /// The bench's baseline switch (<c>--bench-baseline</c>): false skips the lensing pass and the environment capture, so a frame is
    /// only the HDR clear, the rings and the tone map. It separates the lensing's cost from the cost of a second GL window. On by
    /// default.
    /// </summary>
    public bool LensingEnabled
    {
        get => _lensingEnabled;
        set { _lensingEnabled = value; RequestRender(); }
    }

    private bool _lensingEnabled = true;

    // ---- Lensing level (ruling P3-9) ----

    private readonly LensingGovernor _governor = new(LensingLevel.Full);

    // ---- The ray cache (ruling R-P3-10) and the user's detail (R-P3-9) ----

    private readonly RayMapPlanner _planner = new();

    /// <summary>The user's light-bending detail (the view model's, from layout.json): Auto is the governor.</summary>
    public LensingDetail Detail => _vm.LensingDetail;

    /// <summary>The level traced while the camera moves: the governor's for Auto, else the user's (ruling R-P3-10).</summary>
    public LensingLevel MovingLevel => LensingDetails.MovingLevel(Detail, _governor.Level);

    /// <summary>The level the view refines to once the camera stops: full for Auto, else the user's.</summary>
    public LensingLevel StillLevel => LensingDetails.StillLevel(Detail);

    /// <summary>The lensing pass's level for a moving camera (<see cref="MovingLevel"/>; the governor's under Auto).</summary>
    public LensingLevel Level => MovingLevel;

    /// <summary>The level of the ray map on screen (null before one is traced).</summary>
    public LensingLevel? ShownLevel => _planner.HasMap ? _planner.ShownLevel : null;

    /// <summary>Ray-map traces since the last <see cref="ResetGpuStats"/>: whole maps and refinement bands (the bench logs them).</summary>
    public int MapTraces { get; private set; }
    public int RefineBands { get; private set; }

    /// <summary>Whole-map traces and refinement bands whose pass failed (no target could be made); the map is then traced again.</summary>
    public int MapTracesFailed { get; private set; }
    public int RefineBandsFailed { get; private set; }

    private readonly int[] _traceReasons = new int[Enum.GetValues<RayMapTraceReason>().Length];

    /// <summary>Whole maps traced since <see cref="ResetGpuStats"/> for each reason (diagnostics: a map retraced every frame shows here).</summary>
    public int TracesFor(RayMapTraceReason reason) => _traceReasons[(int)reason];

    /// <summary>Frames the host offered while shown, and the frames drawn, since <see cref="ResetGpuStats"/> (diagnostics).</summary>
    public int FramesSinceReset { get; private set; }

    /// <summary>Whether the host is asked to draw every frame now (diagnostics).</summary>
    public bool HostAnimating => _host?.Animating == true;

    /// <summary>Whether the governor may step the level down (on by default; the bench measures fixed levels with it off).</summary>
    public bool AutoLevel { get; set; } = true;

    /// <summary>Sets the governor's level (the bench) and starts its window over; it is the moving level under Auto.</summary>
    public void SetLevel(LensingLevel level)
    {
        _governor.Reset(level);
        OnLevelsChanged();
        RequestRender();
    }

    /// <summary>
    /// Feeds the governor one drawn frame's GPU time and the wall time since the previous frame (s); public as the test hook. Only
    /// under Auto: a level the user chose is never stepped down. With the ray cache the controller feeds it only the frames that
    /// traced a whole map (the moving camera's frames, whose cost the governor's level sets).
    /// </summary>
    public void FeedRenderTime(double gpuTimeS, double frameIntervalS)
    {
        if (!AutoLevel || Detail != LensingDetail.Auto || frameIntervalS > SceneController.MaxGovernedFrameS) return;
        if (_governor.Update(gpuTimeS, frameIntervalS)) OnLevelsChanged();
    }

    private void OnLevelsChanged()
    {
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(MovingLevel));
        OnPropertyChanged(nameof(StillLevel));
    }

    /// <summary>Playback stopping hands a capture still held back by the rate cap to <see cref="IsAnimating"/> (R-P3-11).</summary>
    private void OnTimelineChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_disposed || e.PropertyName != nameof(TimelineViewModel.IsPlaying)) return;
        UpdateAnimating();
        if (_envPending) RequestRender();
    }

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_disposed || e.PropertyName != nameof(CloseUpViewModel.LensingDetail)) return;
        _governor.Reset(LensingLevel.Full);   // a new choice: Auto starts again from full
        OnPropertyChanged(nameof(Detail));
        OnLevelsChanged();
        UpdateAnimating();
        RequestRender();
    }

    /// <summary>The uniforms the tracer would get for this viewport now (the test hook for R-P3-10: no time in them).</summary>
    public LensingUniforms RaysFor(Size viewportPx) => Uniforms(_vm.State, viewportPx);

    /// <summary>
    /// Whether a finer ray map is owed (the camera has stopped, the shown map is coarser than the still level), unless refining has
    /// been given up after <see cref="MaxConsecutiveFailures"/> failed bands.
    /// </summary>
    private bool RefineOwed => !_refineGivenUp && !_directFallback && _planner.HasMap && _lensingEnabled && _vm.State.IsLensed
                               && (int)StillLevel < (int)_planner.ShownLevel;

    /// <summary>The level the planner refines to: the still level, or the moving one once refining has been given up.</summary>
    private LensingLevel PlannedStillLevel => _refineGivenUp ? MovingLevel : StillLevel;

    private double _gpuSumS;

    public int GpuSamples { get; private set; }
    public double MeanGpuRenderTimeS => GpuSamples > 0 ? _gpuSumS / GpuSamples : double.NaN;
    public double MaxGpuRenderTimeS { get; private set; }
    public void ResetGpuStats() { _gpuSumS = 0; GpuSamples = 0; MaxGpuRenderTimeS = 0; MapTraces = RefineBands = MapTracesFailed = RefineBandsFailed = FramesSinceReset = 0; Array.Clear(_traceReasons); }

    private void RecordGpuTime(double t)
    {
        if (!(t >= 0) || !double.IsFinite(t)) return;
        _gpuSumS += t;
        GpuSamples++;
        MaxGpuRenderTimeS = Math.Max(MaxGpuRenderTimeS, t);
    }

    /// <summary>How many times the environment cube map has been rendered.</summary>
    public int EnvironmentCaptures => _environment?.Captures ?? 0;

    // ---- Lifecycle ----

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

    /// <summary>
    /// Whether the window is shown and not minimized (spec §2.5: when the Close-up is minimized or hidden it stops drawing). Hidden:
    /// no continuous frames are asked for, and any the host still offers are skipped. Shown again: one frame is asked for.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (_visible == visible) return;
        _visible = visible;
        OnPropertyChanged(nameof(IsVisible));
        UpdateAnimating();
        RequestRender();
    }

    public void SetViewSize(Size dip)
    {
        if (dip != _viewDip) ResetFailures();   // new targets: what failed may work at the new size
        _viewDip = dip;
        UpdateRings();
        RequestRender();
    }

    private void ResetFailures()
    {
        _bandFailuresInRow = _traceFailuresInRow = 0;
        _refineGivenUp = _directFallback = false;
        _vm.RefineFailed = false;
        _vm.TracingDirectly = false;
    }

    public void Rotate(double dxDip, double dyDip)
    {
        Camera.Rotate(dxDip, dyDip);
        RequestRender();
    }

    /// <summary>Turns the camera about the spin axis by this many degrees of yaw (the bench's orbiting camera, Task 4b).</summary>
    public void Orbit(double yawDeg)
    {
        Camera.YawDeg = (Camera.YawDeg + yawDeg) % 360;
        RequestRender();
    }

    /// <summary>Wheel: zooms (positive notches move closer) and freezes the scale until <see cref="Home"/>.</summary>
    public void Zoom(int notches)
    {
        if (notches == 0) return;
        _frozenHalfHeightCm ??= Director.HalfHeightCm;
        Camera.Zoom(notches);
        ClampDistance();
        IsAutoFraming = false;
        UpdateAnimating();
        UpdateRings();
        RequestRender();
    }

    /// <summary>
    /// The snapshot's wide view (plan Task 6's visual check): freezes the scale so a sphere of this radius (km) fills the view, seen
    /// from this pitch, as a wheel zoom would; <see cref="Home"/> hands the camera back.
    /// </summary>
    public void FrameForSnapshot(double radiusKm, double pitchDeg)
    {
        if (!(radiusKm > 0)) return;
        _frozenHalfHeightCm = radiusKm * UnitCm;
        Camera.DistanceMpc = SceneController.AutoDistance * radiusKm;
        Camera.PitchDeg = pitchDeg;
        ClampDistance();
        IsAutoFraming = false;
        UpdateAnimating();
        UpdateRings();
        RequestRender();
    }

    /// <summary>Home: the camera goes back to the director, at the default angles.</summary>
    public void Home() => Home(jumpOnly: false);

    private void Home(bool jumpOnly)
    {
        _frozenHalfHeightCm = null;
        Camera.YawDeg = DefaultYawDeg;
        Camera.PitchDeg = _vm.IsCutaway ? CutawayPitchDeg : DefaultPitchDeg;
        IsAutoFraming = true;
        Director.Jump(_vm.State.FrameRadiusKm * UnitCm);
        ApplyAutoDistance();
        if (jumpOnly) return;
        UpdateAnimating();
        UpdateRings();
        RequestRender();
    }

    /// <summary>The physical half-height of the view (cm): the director's, or the one frozen by a zoom.</summary>
    public double HalfHeightCm => _frozenHalfHeightCm ?? Director.HalfHeightCm;

    private void ApplyAutoDistance()
    {
        if (_frozenHalfHeightCm is null && Director.HalfHeightCm > 0)
            Camera.DistanceMpc = SceneController.AutoDistance * Director.HalfHeightCm / UnitCm;
        ClampDistance();
    }

    /// <summary>The closest camera, in multiples of GM/c²: outside the ergosphere (2 GM/c² at the equator) with room to spare.</summary>
    public const double MinCameraGravitationalRadii = 3;

    /// <summary>The closest camera, in multiples of a neutron star's radius.</summary>
    public const double MinCameraSurfaceRadii = 1.1;

    /// <summary>
    /// The closest the camera may come (km): max(3 GM/c², 1.1 × the surface radius) of the object drawn now, so a zoom can never put
    /// it inside the horizon, the ergosphere or a neutron star (where every ray would be captured at step 0, or hit the surface, and
    /// the view would go silently black or flat; BlackHoleOptics' tracer assumes a camera beyond 2 GM/c²). With nothing lensed, the
    /// camera's own floor.
    /// </summary>
    public double MinCameraDistanceKm
    {
        get
        {
            var s = _vm.State;
            // Plan Task 9: outside the core's sphere (the proto-neutron star, or the core before collapse).
            if (s.IsCoreStage && s.CoreSphereRadiusKm > 0)
                return Math.Max(OrbitCamera.MinDistanceMpc, MinCameraSurfaceRadii * s.CoreSphereRadiusKm);
            if (!s.IsLensed) return OrbitCamera.MinDistanceMpc;
            return Math.Max(OrbitCamera.MinDistanceMpc,
                Math.Max(MinCameraGravitationalRadii * s.GravitationalRadiusKm, MinCameraSurfaceRadii * s.SurfaceRadiusKm));
        }
    }

    private void ClampDistance()
    {
        double min = MinCameraDistanceKm;
        if (Camera.DistanceMpc < min) Camera.DistanceMpc = min;
    }

    // What the last state change left on screen (Task 4b's on-demand drawing): the state record and the menu switches it draws.
    private CloseUpState _drawnState;
    private (bool Grid, bool Labels, bool Cutaway, bool FieldLines, bool LightCylinder) _drawnSwitches;

    /// <summary>The menu switches the view draws (or will: field lines and the light cylinder from Tasks 6 and 7).</summary>
    private (bool, bool, bool, bool, bool) DrawnSwitches()
        => (_vm.ShowGrid, _vm.ShowLabels, _vm.IsCutaway, _vm.ShowFieldLines, _vm.ShowLightCylinder);

    /// <summary>
    /// Task 4b (ruling proposal R-P3-11): whether a time step must redraw the Close-up. Only when the state record changed, a menu
    /// switch it draws changed, or ruling P3-6 calls for a new capture of the surroundings (moved past its step since the last
    /// capture, and not within its 4-per-second cap); otherwise the picture on screen is already right (the ray map is cached and the
    /// cube map unchanged), so no frame is asked for. A capture held back by the cap is asked for by a later time step while the
    /// timeline plays, and by drawing on when it stops (<see cref="IsAnimating"/>).
    /// </summary>
    public static bool TimeStepNeedsFrame(bool stateChanged, bool switchesChanged, bool lensed, double capturedLogT, double logT,
        bool sameOutcome, double wallSinceCapture)
        => stateChanged || switchesChanged
                        || (lensed && EnvironmentCapture.NeedsRefresh(capturedLogT, logT, sameOutcome, wallSinceCapture));

    /// <summary>The test hook: records the surroundings as captured now (what <see cref="RefreshEnvironment"/> does after drawing them).</summary>
    public void MarkEnvironmentCapturedForTest()
    {
        var main = _vm.Main;
        (_envLogT, _envOutcome, _envObserver, _envRun) =
            (Math.Log10(Math.Max(_vm.SourceTimeS, 1e-9)), main.CurrentOutcome, main.CurrentObserver, main.RunNumber);
    }

    private void OnStateChanged(object? sender, bool rebuilt)
    {
        if (_disposed) return;
        var state = _vm.State;
        var switches = DrawnSwitches();
        if (switches.Item3 != _drawnSwitches.Cutaway)   // Task 7: look into the wedge, then give the user's pitch back
            (Camera.PitchDeg, _pitchBeforeCutaway) = CutawayPitch(switches.Item3, Camera.PitchDeg, _pitchBeforeCutaway);
        if (!rebuilt)
        {
            var main = _vm.Main;
            bool same = ReferenceEquals(_envOutcome, main.CurrentOutcome) && ReferenceEquals(_envObserver, main.CurrentObserver)
                        && _envRun == main.RunNumber;
            bool needed = TimeStepNeedsFrame(!ReferenceEquals(state, _drawnState), switches != _drawnSwitches,
                state.IsLensed && _lensingEnabled, _envLogT, Math.Log10(Math.Max(_vm.SourceTimeS, 1e-9)), same, _clock() - _envWallS);
            (_drawnState, _drawnSwitches) = (state, switches);
            if (!needed) return;
        }
        (_drawnState, _drawnSwitches) = (state, switches);
        if (rebuilt)
        {
            BuildEnvironmentScene();
            // A new outcome reframes at once (as the main view does), keeping the angles the user chose.
            _frozenHalfHeightCm = null;
            IsAutoFraming = true;
            Director.Jump(_vm.State.FrameRadiusKm * UnitCm);
            ApplyAutoDistance();
        }
        ClampDistance();
        UpdateAnimating();
        UpdateRings();
        RequestRender();
    }

    private void BuildEnvironmentScene()
    {
        try
        {
            _envScene = _envSceneFactory(_vm.Main.CurrentOutcome, _vm.Main.CurrentObserver.TimeDilation);
            _envScene.ParticlesFor((int)QualityLevel.Low);
        }
        catch (Exception e)
        {
            _envScene = null;
            Trace.TraceError($"Close-up environment: {e}");
        }
        _uploadEnvParticles = true;
        _envOutcome = null;
    }

    private void RequestRender()
    {
        if (!_disposed && !IsFailed && _visible) _host?.RequestRender();
    }

    /// <summary>
    /// Whether the view renders every frame now: the bench's switch, a camera ease, a finer ray map owed (R-P3-10), an environment
    /// refresh held back by its rate cap once the timeline has stopped (while it plays, the next time step asks for it, R-P3-11), or
    /// (plan Task 6, R-P3-11's condition) a neutron star, whose beams, field lines and surface markings turn with the beam clock even
    /// while the timeline is paused, as Project 2's beams do.
    /// </summary>
    public bool IsAnimating => !IsFailed && _visible &&
                               (_forceAnimating || (Director.IsEasing && _frozenHalfHeightCm is null) || (_envPending && !_vm.Main.Timeline.IsPlaying)
                                || RefineOwed || Turns || _vm.PulsesShown || CoreMoves || FlareKeepsDrawing(_vm.State) || _flareDrawn > 0);

    /// <summary>
    /// Plan Task 9 (R-P3-11's condition): the first second moves on its own (the shock's sloshing and the infalling matter, on the wall
    /// clock, paused or not, as the beams do), so it keeps the view drawing; fix round 1: only while a shock is drawn (a fallback's cooling proto-neutron star at 1-3 s has none, and nothing moves).
    /// </summary>
    public bool CoreMoves => CoreMovesIn(_vm.State);

    /// <summary>Whether this state's first second moves on its own: a shock is drawn (it sloshes, and matter falls onto it).</summary>
    public static bool CoreMovesIn(CloseUpState s) => s.Era == CloseUpEra.FirstSecond && s.Core is { } core && double.IsFinite(core.ShockRadiusKm);

    /// <summary>Whether something drawn turns with the spin now: a neutron star (its surface markings, field lines and beams).</summary>
    private bool Turns => _vm.State.NeutronStar is not null && _vm.State.IsLensed;

    /// <summary>
    /// Plan Task 12 (R-P3-11's condition, spec §5): a giant flare in progress keeps the view drawing, so its one rise and fade play out
    /// frame by frame even if nothing else turns.
    /// </summary>
    public static bool FlareKeepsDrawing(CloseUpState s) => s.FlareIntensity > 0;

    /// <summary>
    /// Flash safety (Review Focus 5; <see cref="FlashLimiter"/>): the flare's brightness as drawn this frame, 0 to 1: the scene's single
    /// rise and fade through at most one rise per half second of wall time, whatever the scrubbing does.
    /// </summary>
    public float FlareDrawn => _flareDrawn;

    private readonly FlashLimiter _flashLimiter = new();
    private float _flareDrawn;

    /// <summary>[Presentation, schematic] How much a flare at full brightness multiplies the surface's and the beams' light (tone-mapped, so it whites out and blooms).</summary>
    public const float FlareSurfaceGain = 0.5f, FlareBeamGain = 1.5f;

    /// <summary>
    /// Flash safety (WCAG 2.3.1's area test): the bloom is thinned while a flare is on, so its spill cannot light the frame. The
    /// tone-mapped peak of the surface (<see cref="SurfaceLevel"/> × (1 + gain), through <see cref="HdrPipeline.Tonemap"/>) stays under 0.85.
    /// </summary>
    public static float BloomDuringFlare(float bloom, float flareDrawn) => bloom * (1f - 0.5f * Math.Clamp(flareDrawn, 0f, 1f));

    /// <summary>The note's condition: the limiter is holding the drawn flare clearly below what the model asks for.</summary>
    public static bool FlareDimmedBy(double requested, double drawn) => requested > 0.1 && drawn < requested - 0.1;

    private void UpdateAnimating()
    {
        if (_host is not null && !IsFailed) _host.Animating = IsAnimating;
    }

    // ---- Drawing ----

    private void OnContextReady(string glInfo)
    {
        GlInfo = glInfo;
        string version = glInfo.Split('|').ElementAtOrDefault(2)?.Trim() ?? "";
        if (!SceneController.IsSupported(version))
            throw new NotSupportedException($"this computer's OpenGL is {(version.Length > 0 ? version : "unknown")}");
        var gpu = _gpuFactory();
        (_hdr, _environment, _painter, _rings, _gpuTimer, _lensing) = (gpu.Hdr, gpu.Environment, gpu.Painter, gpu.Rings, gpu.Timer, gpu.Lensing);
        _neutronStar = gpu.NeutronStar;
        _cutawayRenderer = gpu.Cutaway;
        _coreRenderer = gpu.Core;
        _overlay = gpu.Overlay;
        _jets = gpu.Jets;
        ResetFailures();
        _planner.Invalidate();
        _uploadEnvParticles = true;
        _envOutcome = null;
        _envHaze = false;
    }

    private void OnFrameFailed(Exception ex) => Fail(ex.Message);

    private void Fail(string reason)
    {
        FailureMessage = $"The Close-up needs {SceneController.Requirement} (Intel UHD 620 class graphics or better) and could not use it: " +
                         $"{reason.TrimEnd('.')}. Its readouts, the main window and the instrument panels work without it.";
        IsFailed = true;
        if (_host is not null) _host.Animating = false;
        _readback?.TrySetResult(null);
        _readback = null;
    }

    private void OnDraw(TimeSpan delta, Size viewportPx)
    {
        if (_disposed) return;
        if (!_visible)
        {
            SkippedFrames++;
            return;
        }
        double interval = delta.TotalSeconds;
        // Measurements of earlier frames, read late. The tag says what that frame did (LensingFrameTag): the planner learns the cost per
        // ray from whole maps and bands (its band size), the governor from whole maps (a moving camera); a frame that also captured
        // the environment is left out of both, since the capture's time is not the rays' cost (fix round 1).
        _gpuTimer?.Poll((t, tag) =>
        {
            RecordGpuTime(t);
            var (rays, wholeMap, captured) = LensingFrameTag.Decode(tag);
            if (captured) return;
            _planner.ReportGpuTime(rays, t);
            if (wholeMap) FeedRenderTime(t, interval);
        });
        StepDirector();
        if (_lensing is null) return;   // the GL objects are made together (a test's fake has only the lensing pass)
        FramesDrawn++;
        FramesSinceReset++;
        var state = _vm.State;
        _flashLimiter.ResetIfChanged(_vm.Main.CurrentOutcome, _vm.Main.RunNumber);
        _flareDrawn = (float)_flashLimiter.Apply(state.FlareIntensity, _clock());
        _vm.FlareDimmed = FlareDimmedBy(state.FlareIntensity, _flareDrawn);
        _gpuTimer?.Begin();
        bool lens = state.IsLensed && _lensingEnabled;
        int captures = EnvironmentCaptures;
        if (lens) RefreshEnvironment();
        bool captured = EnvironmentCaptures != captures;
        _hdr?.Begin(viewportPx);
        long tracedTag = LensingFrameTag.Encode(default, captured);
        if (lens && viewportPx.Width > 0 && viewportPx.Height > 0)
            tracedTag = DrawLensing(state, viewportPx, captured);
        // Ruling R-P3-21: inside the star the first second stands in the same dim haze the lensed surroundings show later.
        if (state.IsCoreStage && state.InsideStar) _coreRenderer?.ClearToHaze(HazeColor);
        if (state.DrawsScene)
            foreach (var ring in CurrentRings()) _rings?.Render(Camera, viewportPx, ring.RadiusCm / UnitCm, ring.Opacity);
        if (state.IsCoreStage) DrawCore(state, viewportPx);
        // Plan Task 11: the collapsar jets' base along the spin axis (the main view's jet axis), glowing (before the bloom), unbent, the
        // parts inside the shadow and (fix round 1) behind the disk, along straight lines, dropped; faded with the disk.
        if (_jets is not null && state.ShowJets && lens && viewportPx.Width > 0 && viewportPx.Height > 0)
            _jets.RenderJets(Camera, viewportPx, state.JetHalfAngleRad, JetRenderer.JetLength * HalfHeightCm / UnitCm,
                JetRenderer.JetColor * (Gain(JetLevel) * (float)state.DiskBrightness), JetOccluder(state),
                new Vector2((float)(state.DiskInnerGm * state.GravitationalRadiusKm), (float)(state.DiskOuterGm * state.GravitationalRadiusKm)));
        // Plan Task 6: a neutron star's beams glow (drawn before the bloom); its field lines, light cylinder and Earth arrow are
        // drawn after it (crisp, still tone-mapped), as the main view's cut faces are.
        Action? lines = null;
        if (_neutronStar is not null && state.NeutronStar is not null && state.IsLensed && viewportPx.Width > 0 && viewportPx.Height > 0)
        {
            double phase = BeamPhaseRad;
            var occluder = Occluder(state);
            float gain = OverlayGain;
            _neutronStar.RenderBeams(Camera, viewportPx, state, phase, BeamLengthHalfHeights * HalfHeightCm / UnitCm,
                Gain(BeamLevel) * (1 + FlareBeamGain * _flareDrawn), occluder);
            _linesFrame = (viewportPx, state, phase, occluder, gain);
            lines = _drawLinesNow ??= DrawLinesNow;
            _cutawayFrame = CutawayDrawn(state) ? state.Cutaway : null;
        }
        // Plan Task 10: a black hole's overlay (the shadow's exact edge; with the grid, the horizon, photon orbits and ergosphere), after
        // the bloom as the neutron star's lines are.
        if (_overlay is not null && state.BlackHole is not null && state.IsLensed && viewportPx.Width > 0 && viewportPx.Height > 0)
        {
            _linesFrame = (viewportPx, state, 0, default, OverlayGain);
            _cutawayFrame = null;
            lines = _drawLinesNow ??= DrawLinesNow;
        }
        _hdr?.End(_exposure, BloomDuringFlare(BloomStrength, _flareDrawn), lines);
        if (_hdr is null) lines?.Invoke();
        _gpuTimer?.End(tracedTag);
        UpdateAnimating();   // a refinement still owed keeps the view drawing; a finished one lets it rest
        if (_readback is { } readback)
        {
            _readback = null;
            readback.TrySetResult(ReadPixels(viewportPx));
        }
    }

    /// <summary>
    /// Ruling R-P3-10: the light bending through the cached ray map (the planner says what to trace this frame, the pass colours the
    /// shown map from the surroundings as they are now), or without a float target the direct trace at the moving level. Returns
    /// the GPU timer's tag (<see cref="LensingFrameTag"/>): the rays traced, whole map or band, and whether the frame captured the
    /// environment.
    /// </summary>
    private long DrawLensing(CloseUpState state, Size viewportPx, bool captured)
    {
        var u = Uniforms(state, viewportPx);
        if (_lensing is null) return LensingFrameTag.Encode(default, captured);
        int env = _environment?.Texture ?? 0;
        if (!_lensing.CachesRays || _directFallback)
        {
            _lensing.RenderDirect(viewportPx, MovingLevel, u, env);
            _vm.ShownLensingLevel = MovingLevel;
            MapTraces++;
            var (w, h) = LensingLevels.TargetSize((int)viewportPx.Width, (int)viewportPx.Height, MovingLevel);
            return LensingFrameTag.EncodeDirect((long)w * h, captured);
        }
        var step = _planner.Next(u, (int)viewportPx.Width, (int)viewportPx.Height, MovingLevel, PlannedStillLevel);
        if (!_lensing.Render(viewportPx, step, u, env))
        {
            // The pass could not make a target: forget the map (the next frame traces it whole again) and count it, so a failing band
            // shows in the bench and snapshot logs instead of looking like a refinement that never starts. Task 4b review carry-over:
            // after MaxConsecutiveFailures in a row, a failing band gives up refining (the view rests at the moving level, and the
            // light-bending note says so); a failing whole map falls back to tracing colour directly every frame, as without a ray cache.
            if (step.Work == RayMapWork.RefineStrip)
            {
                RefineBandsFailed++;
                if (++_bandFailuresInRow >= MaxConsecutiveFailures)
                {
                    _refineGivenUp = true;
                    _vm.RefineFailed = true;
                    Trace.TraceWarning($"Close-up: refinement bands failed {_bandFailuresInRow} times in a row; refining given up.");
                }
            }
            else if (step.Work == RayMapWork.Trace)
            {
                MapTracesFailed++;
                if (++_traceFailuresInRow >= MaxConsecutiveFailures)
                {
                    _directFallback = true;
                    _vm.TracingDirectly = true;
                    Trace.TraceWarning($"Close-up: the ray map failed {_traceFailuresInRow} times in a row; tracing colour directly.");
                }
            }
            _planner.Invalidate();
            return LensingFrameTag.Encode(default, captured);
        }
        if (step.Work == RayMapWork.RefineStrip) _bandFailuresInRow = 0;
        if (step.Work == RayMapWork.Trace) _traceFailuresInRow = 0;
        _vm.ShownLensingLevel = _planner.ShownLevel;
        if (step.Work == RayMapWork.Trace)
        {
            MapTraces++;
            _traceReasons[(int)_planner.LastTraceReason]++;
        }
        if (step.Work == RayMapWork.RefineStrip) RefineBands++;
        return LensingFrameTag.Encode(step, captured);
    }

    /// <summary>The infalling matter's fall phase now (cycles, in [0, 1)); the test hook for its frame-by-frame accumulation.</summary>
    public double InfallCycles => _infallCycles;

    /// <summary>The shock's sloshing phase now (radians): the wall clock over <see cref="CoreAnimation.SloshPeriodS"/>.</summary>
    public double SloshPhaseRad => 2 * Math.PI * _clock() / CoreAnimation.SloshPeriodS;

    /// <summary>
    /// Plan Task 9: the core's stage (unlensed surfaces, as ruling R-P3-19; the surroundings, the star's inside, are not drawn): the
    /// exposure is the main view's base, the fall phase advances by this frame's wall time over the shown fall's duration.
    /// </summary>
    private void DrawCore(CloseUpState state, Size viewportPx)
    {
        double now = _clock();
        double dt = _lastCoreFrameS is { } last ? Math.Clamp(now - last, 0, MaxFrameGapS) : 0;
        _lastCoreFrameS = now;
        double cycle = state.Core is { } core ? CoreAnimation.InfallCycleS(core) : double.NaN;
        if (cycle > 0) _infallCycles = (_infallCycles + dt / cycle) % 1;
        _exposure = ExplosionScene.BaseExposure;
        double pxPerDip = _viewDip.Height > 0 ? viewportPx.Height / _viewDip.Height : 1;
        _coreRenderer?.Render(Camera, viewportPx, pxPerDip, state, SloshPhaseRad, _infallCycles, Gain(CoreLevel));
    }

    /// <summary>[Presentation] The bloom strength, the main view's.</summary>
    private const float BloomStrength = 0.5f;

    // This frame's inputs for the lines drawn after the bloom (one cached delegate, no closure per frame).
    private (Size Viewport, CloseUpState State, double Phase, StarOccluder Occluder, float Gain) _linesFrame;
    private Action? _drawLinesNow;
    private CutawayInfo? _cutawayFrame;

    /// <summary>Plan Task 7: whether the cutaway (spec §4.2) is drawn now: its switch is on, and a neutron star is lensed.</summary>
    private bool CutawayDrawn(CloseUpState state)
        => _vm.IsCutaway && state.Cutaway is not null && state.NeutronStar is not null && _cutawayRenderer is not null;

    private void DrawLinesNow()
    {
        var (viewport, state, phase, occluder, gain) = _linesFrame;
        if (state.BlackHole is not null)
        {
            _overlay?.Render(Camera, viewport, state, _vm.ShowGrid, _vm.ShowLabels, gain);
            return;
        }
        if (_cutawayFrame is { } cutaway && _cutawayRenderer is not null)
            _cutawayRenderer.Render(Camera, viewport, cutaway, ToneMappedSurface(state.SurfaceColor, HdrEnabled, _exposure),
                c => ToneMappedSurface(c, HdrEnabled, _exposure));
        _neutronStar?.RenderLines(Camera, viewport, state, phase, _vm.ShowFieldLines, _vm.ShowLightCylinder, gain, occluder);
    }

    /// <summary>The overlays' colour gain: through the HDR target, <see cref="OverlayLevel"/> ÷ the exposure (so they read whatever the surroundings' brightness); 1 without it.</summary>
    private float OverlayGain => Gain(OverlayLevel);

    /// <summary>A colour gain: through the HDR target, this level ÷ the exposure; 1 without it (the no-HDR path draws colours as they are).</summary>
    private float Gain(float level) => HdrEnabled && _exposure > 0 ? level / _exposure : 1f;

    /// <summary>
    /// Plan Task 11: what hides the jets' base: the horizon (km) and, behind the hole's centre, the shadow's mean radius (km), where the
    /// light of the axis near the hole is bent out to the shadow's edge.
    /// </summary>
    private static StarOccluder JetOccluder(CloseUpState state)
    {
        double rg = state.GravitationalRadiusKm;
        return new(Vector3d.Zero, BlackHoleOptics.HorizonRadius(state.Spin) * rg,
            BlackHoleOptics.ApparentShadowDiameterRad(state.MassMsun, 1e5, state.Spin) / 2);
    }

    /// <summary>The star as it hides the overlays: its radius, and the bent disc's apparent radius from the camera (<see cref="StarOcclusion"/>).</summary>
    private StarOccluder Occluder(CloseUpState state)
        => new(Vector3d.Zero, state.SurfaceRadiusKm,
            StarOcclusion.ApparentRadiusKm(state.SurfaceRadiusKm, state.GravitationalRadiusKm, Camera.DistanceMpc));

    /// <summary>
    /// Spec C12: the surface's colour for the tone map. Through the HDR target the blackbody colour (normalized sRGB) is linearised and
    /// set to <see cref="SurfaceLevel"/> ÷ the exposure, so the ACES tone map and gamma give it back at about 0.88 of full brightness;
    /// without the target it is drawn as it is.
    /// </summary>
    public static Vector3 ToneMappedSurface((float R, float G, float B) srgb, bool hdr, float exposure)
    {
        if (!hdr) return new Vector3(srgb.R, srgb.G, srgb.B);
        static float Linear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        float k = exposure > 0 ? SurfaceLevel / exposure : SurfaceLevel;
        return new Vector3(Linear(srgb.R), Linear(srgb.G), Linear(srgb.B)) * k;
    }

    /// <summary>The lensing pass's uniforms: the camera in GM/c² (scene km × 10⁵ / (GM/c² in cm)), its basis and the hole.</summary>
    private LensingUniforms Uniforms(CloseUpState state, Size viewportPx)
    {
        double rgCm = BlackHoleOptics.GravitationalRadiusCm(state.MassMsun);
        Vector3d eye = Camera.Eye * (UnitCm / rgCm);
        var (right, up) = Camera.ScreenAxes();
        var forward = -Camera.Backward;
        double tanY = Math.Tan(Camera.FieldOfViewDeg * Math.PI / 360);
        double aspect = viewportPx.Width / viewportPx.Height;
        // Plan Task 6: the surface's shading (its colour, and the markings turned by the spin phase), applied when the map is sampled.
        bool star = state.NeutronStar is not null;
        double phase = star ? BeamPhaseRad % (2 * Math.PI) : 0;
        // Plan Task 11: the disk's annulus (part of the rays) and brightness (sampled); the escape sphere encloses the disk.
        bool disk = state.HasDisk;
        double escape = Math.Max(BlackHoleOptics.EscapeRadiusFor(eye.Length), disk ? DiskEscapeMargin * state.DiskOuterGm : 0);
        return new LensingUniforms((float)state.Spin, (Vector3)eye, (Vector3)forward, (Vector3)right, (Vector3)up,
            new Vector2((float)(tanY * aspect), (float)tanY), (float)escape,
            (float)(state.SurfaceRadiusKm * UnitCm / rgCm),
            ToneMappedSurface(state.SurfaceColor, HdrEnabled, _exposure) * (1 + FlareSurfaceGain * _flareDrawn), _vm.ShowGrid,
            (float)phase, star ? (float)state.BeamTiltRad : 0f, star ? (float)Math.Cos(state.PolarCapRad) : 1f, star,
            GridLineColor(HdrEnabled, _exposure),
            disk ? (float)state.DiskInnerGm : 0f, disk ? (float)state.DiskOuterGm : 0f,
            disk ? Gain(DiskRenderer.Level) * (float)state.DiskBrightness : 0f);
    }

    /// <summary>Plan Task 11: the escape sphere is at least this many times the disk's outer edge, so no ray leaves before meeting it.</summary>
    public const double DiskEscapeMargin = 1.05;

    /// <summary>[Schematic] Plan Task 10: the grid lines' colour (a pale teal, unlike the warm surroundings), sRGB.</summary>
    public static readonly (float R, float G, float B) GridSrgb = (0.45f, 0.85f, 0.8f);

    /// <summary>[Presentation] The grid's level ÷ the exposure, as <see cref="OverlayLevel"/>: it reads whatever the surroundings' brightness. Task 10's
    /// launch: at 0.5 (and lines 1.5 px wide) the lines and their bloom dominated the view, so 0.22 and about 1 px. Task 11's launch showed
    /// those thinner lines, still the brightest thing round a settled hole, so 0.15 (not yet seen in a launch).</summary>
    public const float GridLevel = 0.15f;

    /// <summary>The grid lines' colour for the lensing pass: through the HDR target linearised at <see cref="GridLevel"/> ÷ the exposure.</summary>
    public static Vector3 GridLineColor(bool hdr, float exposure)
    {
        if (!hdr) return new Vector3(GridSrgb.R, GridSrgb.G, GridSrgb.B);
        static float Linear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        float k = exposure > 0 ? GridLevel / exposure : GridLevel;
        return new Vector3(Linear(GridSrgb.R), Linear(GridSrgb.G), Linear(GridSrgb.B)) * k;
    }

    /// <summary>
    /// Ruling P3-6: renders the environment cube map when the source time has moved by more than 0.005 in log₁₀, or the outcome,
    /// observer or run changed, at most 4 times per wall second; a refresh held back by the rate keeps the view animating until it
    /// is made. The main view's cutaway and quality are not part of it: the environment is the physical surroundings, always at Low.
    /// </summary>
    private void RefreshEnvironment()
    {
        if (_environment is null) return;
        double t = _vm.SourceTimeS;
        double logT = Math.Log10(Math.Max(t, 1e-9));
        var main = _vm.Main;
        // Ruling R-P3-21: before the shock breaks out the surroundings are the star's opaque inside, drawn as a dim uniform haze at the
        // main view's base exposure (the same as behind the first second), not the star sphere seen from within.
        if (_vm.State.InsideStar)
        {
            if (!_envHaze)
            {
                _environment.Fill(HazeColor);
                _envHaze = true;
            }
            if (_envPending) { _envPending = false; UpdateAnimating(); }
            _exposure = ExplosionScene.BaseExposure;
            (_envLogT, _envOutcome, _envObserver, _envRun) = (logT, main.CurrentOutcome, main.CurrentObserver, main.RunNumber);
            return;
        }
        if (_envHaze)
        {
            _envHaze = false;
            _envOutcome = null;   // out of the star: capture the surroundings now
            _envWallS = double.NegativeInfinity;
        }
        if (_envScene is null || _painter is null) return;
        bool same = ReferenceEquals(_envOutcome, main.CurrentOutcome) && ReferenceEquals(_envObserver, main.CurrentObserver)
                    && _envRun == main.RunNumber;
        double now = _clock();
        bool changed = EnvironmentCapture.HasChanged(_envLogT, logT, same);
        bool refresh = EnvironmentCapture.NeedsRefresh(_envLogT, logT, same, now - _envWallS);
        bool pending = changed && !refresh;
        if (pending != _envPending) { _envPending = pending; UpdateAnimating(); }
        if (!refresh) return;
        SceneState s;
        try { s = _envScene.At(t); }
        catch (Exception e)
        {
            Trace.TraceError($"Close-up environment: {e}");
            return;
        }
        if (_uploadEnvParticles)
        {
            _uploadEnvParticles = false;
            _painter.Upload(_envScene.ParticlesFor((int)QualityLevel.Low));
        }
        double unit = double.IsFinite(s.FrameRadiusCm) && s.FrameRadiusCm > 0 ? s.FrameRadiusCm : 1e15;
        // Ruling P3-6 and plan Task 6: seen from the remnant, where the kick has carried it (spec §2.2: the camera moves with it).
        var at = new Vector3d(s.RemnantOffsetCm.X, s.RemnantOffsetCm.Y, s.RemnantOffsetCm.Z) / unit;
        // Plan Task 10 (spec §6.2): without the wind's schematic haze. It stands for unlit gas round the star; seen from the remnant,
        // inside it, it filled the whole sky with a pale grey wash (Task 10's launch: NGC 6946-BH1 at 1.2–10 s).
        _environment.Capture(at, (camera, size) => _painter.Paint(camera, size, 1, s, unit, windHaze: false));
        _exposure = s.Exposure;
        (_envLogT, _envOutcome, _envObserver, _envRun, _envWallS) = (logT, main.CurrentOutcome, main.CurrentObserver, main.RunNumber, now);
    }

    private void StepDirector()
    {
        double now = _clock();
        double dt = _lastFrameS is { } last ? Math.Clamp(now - last, 0, MaxFrameGapS) : 0;
        _lastFrameS = now;
        if (_frozenHalfHeightCm is null)
        {
            Director.Update(_vm.State.FrameRadiusKm * UnitCm, dt);
            ApplyAutoDistance();
        }
        UpdateAnimating();
        UpdateRings();
    }

    /// <summary>The rings now drawn, outermost first (the ladder runs down to metres for the Close-up's small scales).</summary>
    private IReadOnlyList<Ring> CurrentRings() => _vm.State.DrawsScene && HalfHeightCm > 0 && double.IsFinite(HalfHeightCm)
        ? DistanceRings.Rungs(HalfHeightCm)
        : Array.Empty<Ring>();

    /// <summary>Places each ring's label at the top of its circle, from the projection (as the main view, ruling P6).</summary>
    private void UpdateRings()
    {
        UpdateViewLabels();
        if (!(_viewDip.Width > 0) || !(_viewDip.Height > 0) || !_vm.ShowLabels)
        {
            if (_ringLabels.Count > 0) RingLabels = Array.Empty<RingLabel>();
            return;
        }
        double perKm = 1 / (Camera.DistanceMpc * Math.Tan(Camera.FieldOfViewDeg * Math.PI / 360));
        var labels = new List<RingLabel>();
        foreach (var ring in CurrentRings())
        {
            double y = _viewDip.Height / 2 * (1 - ring.RadiusCm / UnitCm * perKm);
            if (y < 0 || y > _viewDip.Height) continue;
            labels.Add(new RingLabel(ring.Label, _viewDip.Width / 2, y, ring.Opacity));
        }
        if (!labels.SequenceEqual(_ringLabels)) RingLabels = labels;
    }

    /// <summary>The label for the Earth arrow (spec §4.3).</summary>
    public const string ToEarthLabel = "To Earth";

    /// <summary>The light cylinder's label (spec §4.3).</summary>
    public const string LightCylinderLabel = "Light cylinder";

    /// <summary>The room a view label needs to its anchor's right, DIP.</summary>
    private const double LabelRoomDip = 100;

    /// <summary>
    /// Plan Task 6: places "To Earth" just past the arrow's tip and "Light cylinder" on its ring (on the side toward the screen's right),
    /// from the camera's projection, when they are on screen, the labels are on and a neutron star is drawn. Neither turns with the spin,
    /// so they move only with the camera.
    /// </summary>
    private void UpdateViewLabels()
    {
        var s = _vm.State;
        if (_viewDip.Width > 0 && _viewDip.Height > 0 && _vm.ShowLabels && s.BlackHole is not null && s.IsLensed)
        {
            // Plan Task 10 (spec §6.3): with the grid on, the horizon, the photon sphere (or orbits) and the ergosphere are labelled.
            var bhLabels = new List<RingLabel>();
            if (_vm.ShowGrid)
            {
                var (right, _) = Camera.ScreenAxes();
                var across = LensOverlay.Across(Camera.Backward, right);
                foreach (var label in LensOverlay.Labels(s.Spin, across, Camera.Backward, s.GravitationalRadiusKm))
                    if (ProjectDip(label.AtKm) is { } at)   // a label on the left ends at its leader (about 6.5 DIP a character)
                        bhLabels.Add(new RingLabel(label.Text, label.LeftOfEnd ? Math.Max(0, at.X - 20 - 6.5 * label.Text.Length) : at.X, at.Y, 1));
            }
            if (!bhLabels.SequenceEqual(_viewLabels)) ViewLabels = bhLabels;
            return;
        }
        if (!(_viewDip.Width > 0) || !(_viewDip.Height > 0) || !_vm.ShowLabels || s.NeutronStar is null || !s.IsLensed)
        {
            if (_viewLabels.Count > 0) ViewLabels = Array.Empty<RingLabel>();
            return;
        }
        var labels = new List<RingLabel>();
        var e = new Vector3d(s.EarthDirection.X, s.EarthDirection.Y, s.EarthDirection.Z);
        // Fix round 1: kept on screen; when the tip is off the view (zoomed in) the label sits at the edge in its direction.
        if (EdgeDip(e * (NeutronStarRenderer.ArrowTipRadii + 0.15) * s.SurfaceRadiusKm) is { } tip)
            labels.Add(new RingLabel(ToEarthLabel, tip.X, tip.Y, 1));
        if (_vm.ShowLightCylinder && double.IsFinite(s.LightCylinderKm))
        {
            // The ring's point nearest the camera, else its sides: the first on screen with room for the label to its right (Task 6's
            // launch: the right-hand point alone put the label past the view's edge).
            var (right, _) = Camera.ScreenAxes();
            var back = Camera.Backward;
            foreach (var dir in new[] { new Vector3d(back.X, back.Y, 0), new Vector3d(-right.X, -right.Y, 0), new Vector3d(right.X, right.Y, 0) })
            {
                if (!(dir.Length > 1e-6) || ProjectDip(dir.Normalized() * s.LightCylinderKm) is not { } ring) continue;
                if (ring.X > _viewDip.Width - LabelRoomDip) continue;
                labels.Add(new RingLabel(LightCylinderLabel, ring.X, ring.Y, 1));
                break;
            }
        }
        // Plan Task 7 (spec §4.2): the neutron drip line's name and density, beside the star on its screen-right (the star is small at
        // the default framing, so a label on the ring would hide the faces), pointing back to the ring, when the cutaway is drawn and
        // the crust has formed.
        if (CutawayDrawn(s) && s.Cutaway is { HasCrust: true, DripLabel: { } dripLabel } cut)
        {
            var (screenRight, _) = Camera.ScreenAxes();
            if (EdgeDip(screenRight * (1.12 * s.SurfaceRadiusKm)) is { } dripAt
                && DripLabelPlacement(dripLabel, dripAt, EdgeDip(-screenRight * (1.12 * s.SurfaceRadiusKm)), _viewDip.Width) is { } drip)
                labels.Add(drip);
        }
        if (!labels.SequenceEqual(_viewLabels)) ViewLabels = labels;
    }

    /// <summary>A view label's text width per character, DIP (12 px Segoe UI, about 6.5; the black hole's left-hand labels use it too).</summary>
    public const double LabelCharDip = 6.5;

    /// <summary>
    /// The cutaway key's box from the view's top-right corner, DIP: its MaxWidth 250 and margins, and its height as Task 14's launch drew it
    /// (about 235 DIP at 900x640, where three rows wrap; less when wider).
    /// </summary>
    public const double CutawayKeyWidthDip = 262, CutawayKeyBottomDip = 245;

    /// <summary>
    /// Task 14: whether a view label placed to the right of this point (its 6 DIP margin, 4 DIP padding each side, about
    /// <see cref="LabelCharDip"/> a character, 20 DIP tall around the point) would run under the cutaway key in the top-right corner.
    /// </summary>
    public static bool DripLabelUnderKey(Point at, string text, double viewWidthDip)
    {
        double left = at.X + 6, right = left + 8 + LabelCharDip * text.Length, top = at.Y - 10;
        return right > viewWidthDip - CutawayKeyWidthDip && top < CutawayKeyBottomDip;
    }

    /// <summary>
    /// Task 14's launch: at the default 900x640 window the drip label on the star's right ran under the cutaway key (top right). The label
    /// beside the star's right edge (<paramref name="rightAt"/>, "◄ text"), or, where that would run under the key, beside its left edge
    /// (<paramref name="leftAt"/>, "text ►", ending at its leader as the black hole's left-hand labels do); null if neither is on the view.
    /// </summary>
    public static RingLabel? DripLabelPlacement(string dripLabel, Point rightAt, Point? leftAt, double viewWidthDip)
    {
        string right = "◄ " + dripLabel;
        if (!DripLabelUnderKey(rightAt, right, viewWidthDip)) return new RingLabel(right, rightAt.X, rightAt.Y, 1);
        if (leftAt is not { } left) return null;
        string text = dripLabel + " ►";
        return new RingLabel(text, Math.Max(0, left.X - 20 - LabelCharDip * text.Length), left.Y, 1);
    }

    /// <summary>The DIP margin a clamped label keeps from the top and bottom edges.</summary>
    private const double LabelEdgeDip = 14;

    /// <summary>
    /// A scene point (km) on the view, in DIP, or, when it is off the view or behind the camera, the view's edge in its direction from
    /// the centre, with room for a label to its right; null only before the view has a size or for a point at the eye itself.
    /// </summary>
    public Point? EdgeDip(Vector3d world)
    {
        if (!(_viewDip.Width > 0) || !(_viewDip.Height > 0)) return null;
        var rel = world - Camera.Eye;
        var clip = new Vector4d(rel.X, rel.Y, rel.Z, 1) * (Camera.View * Camera.Projection(_viewDip.Width / _viewDip.Height));
        if (clip.W == 0) return null;
        // Behind the camera the projection flips: divide by |w| and reverse, so the direction from the centre is the point's own.
        double x = clip.X / Math.Abs(clip.W), y = clip.Y / Math.Abs(clip.W);
        if (clip.W < 0) (x, y) = (-x, -y);
        double reach = Math.Max(Math.Abs(x), Math.Abs(y));
        if (clip.W < 0 || reach > 1) { x /= Math.Max(reach, 1e-9); y /= Math.Max(reach, 1e-9); }
        double px = Math.Clamp((x + 1) / 2 * _viewDip.Width, 0, Math.Max(0, _viewDip.Width - LabelRoomDip));
        double py = Math.Clamp((1 - y) / 2 * _viewDip.Height, LabelEdgeDip, Math.Max(LabelEdgeDip, _viewDip.Height - LabelEdgeDip));
        return new Point(px, py);
    }

    /// <summary>A scene point (km) on the view, in DIP from its top left; null when it is behind the camera or off the view.</summary>
    public Point? ProjectDip(Vector3d world)
    {
        if (!(_viewDip.Width > 0) || !(_viewDip.Height > 0)) return null;
        var rel = world - Camera.Eye;
        var clip = new Vector4d(rel.X, rel.Y, rel.Z, 1) * (Camera.View * Camera.Projection(_viewDip.Width / _viewDip.Height));
        if (!(clip.W > 0)) return null;
        double x = clip.X / clip.W, y = clip.Y / clip.W;
        if (Math.Abs(x) > 1 || Math.Abs(y) > 1) return null;
        return new Point((x + 1) / 2 * _viewDip.Width, (1 - y) / 2 * _viewDip.Height);
    }

    /// <summary>The next frame read back with GL.ReadPixels (no screen capture), or null on failure or timeout.</summary>
    public async Task<BitmapSource?> CaptureFrameAsync(TimeSpan timeout)
    {
        if (_disposed || IsFailed || _host is null) return null;
        _readback?.TrySetResult(null);
        // Fix round 1: the waiting code resumes on the dispatcher, never inline inside this view's OnDraw (with the GL context current).
        var tcs = new TaskCompletionSource<BitmapSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _readback = tcs;
        _host.RequestRender();
        var done = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
        if (done != tcs.Task) tcs.TrySetResult(null);
        return await tcs.Task;
    }

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
    /// Stops following the view model and releases the GL objects. Call it while the GL control is still loaded (the window's
    /// Closing). A GL error here never stops the window closing.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _vm.StateChanged -= OnStateChanged;
        _vm.PropertyChanged -= OnViewModelChanged;
        _vm.Main.Timeline.PropertyChanged -= OnTimelineChanged;
        _readback?.TrySetResult(null);
        _readback = null;
        if (_host is { } host)
        {
            host.ContextReady -= OnContextReady;
            host.Draw -= OnDraw;
            host.FrameFailed -= OnFrameFailed;
            try
            {
                GlReleased = host.Shutdown(() =>
                {
                    _gpuTimer?.Dispose();
                    _coreRenderer?.Dispose();
                    _overlay?.Dispose();
                    _jets?.Dispose();
                    _cutawayRenderer?.Dispose();
                    _neutronStar?.Dispose();
                    _lensing?.Dispose();
                    _rings?.Dispose();
                    _painter?.Dispose();
                    _environment?.Dispose();
                    _hdr?.Dispose();
                });
            }
            catch (Exception e)
            {
                GlReleased = false;
                Debug.WriteLine($"CloseUpSceneController: releasing the GL objects failed: {e}");
            }
        }
        _gpuTimer = null;
        _neutronStar = null;
        _cutawayRenderer = null;
        _coreRenderer = null;
        _overlay = null;
        _jets = null;
        _lensing = null;
        _rings = null;
        _painter = null;
        _environment = null;
        _hdr = null;
    }
}
