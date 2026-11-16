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

    /// <summary>[Presentation, placeholder] A neutron star's surface colour (HDR) until its own surface is drawn (Task 5).</summary>
    public static readonly Vector3 PlaceholderSurfaceColor = new(0.6f, 0.65f, 0.75f);

    private readonly CloseUpViewModel _vm;
    private readonly Func<IRenderHost> _hostFactory;
    private readonly Func<Outcome, double, ExplosionScene> _envSceneFactory;
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
    private LensingPass? _lensing;
    private EnvironmentCapture? _environment;
    private ExplosionPainter? _painter;
    private RingRenderer? _rings;
    private GpuTimer? _gpuTimer;

    // The environment's scene (Project 2's, rebuilt on a new outcome) and what was last captured (ruling P3-6).
    private ExplosionScene? _envScene;
    private bool _uploadEnvParticles = true;
    private double _envLogT = double.NaN;
    private Outcome? _envOutcome;
    private object? _envObserver;
    private int _envRun;
    private double _envWallS = double.NegativeInfinity;
    private bool _envPending;
    private float _exposure = 1f;

    /// <param name="envSceneFactory">Builds the environment's scene of an outcome at a time dilation; null for <see cref="ExplosionScene"/>'s own.</param>
    /// <param name="wallClock">Wall-clock seconds; null for the real clock (tests pass a fake).</param>
    /// <param name="beamClock">The beams' wall clock shared with the main view (plan ruling P3-5); the beams are drawn from Task 6.</param>
    public CloseUpSceneController(CloseUpViewModel vm, Func<IRenderHost> hostFactory, BeamClock? beamClock = null,
        Func<double>? wallClock = null, Func<Outcome, double, ExplosionScene>? envSceneFactory = null)
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        _hostFactory = hostFactory ?? throw new ArgumentNullException(nameof(hostFactory));
        BeamClock = beamClock ?? new BeamClock();
        _clock = wallClock ?? (() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
        _envSceneFactory = envSceneFactory ?? ((o, z1) => new ExplosionScene(o, z1));
        BuildEnvironmentScene();
        Home(jumpOnly: true);
        vm.StateChanged += OnStateChanged;
        vm.PropertyChanged += OnViewModelChanged;
        vm.Main.Timeline.PropertyChanged += OnTimelineChanged;
        (_drawnState, _drawnSwitches) = (vm.State, (vm.ShowGrid, vm.ShowLabels, vm.IsCutaway));
    }

    /// <summary>The beams' clock, the same instance as the main view's (plan ruling P3-5).</summary>
    public BeamClock BeamClock { get; }

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

    /// <summary>Whether a finer ray map is owed (the camera has stopped, the shown map is coarser than the still level).</summary>
    private bool RefineOwed => _planner.HasMap && _lensingEnabled && _vm.State.IsLensed && (int)StillLevel < (int)_planner.ShownLevel;

    private double _gpuSumS;

    public int GpuSamples { get; private set; }
    public double MeanGpuRenderTimeS => GpuSamples > 0 ? _gpuSumS / GpuSamples : double.NaN;
    public double MaxGpuRenderTimeS { get; private set; }
    public void ResetGpuStats() { _gpuSumS = 0; GpuSamples = 0; MaxGpuRenderTimeS = 0; MapTraces = RefineBands = 0; }

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
        _viewDip = dip;
        UpdateRings();
        RequestRender();
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

    /// <summary>Home: the camera goes back to the director, at the default angles.</summary>
    public void Home() => Home(jumpOnly: false);

    private void Home(bool jumpOnly)
    {
        _frozenHalfHeightCm = null;
        Camera.YawDeg = DefaultYawDeg;
        Camera.PitchDeg = DefaultPitchDeg;
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
    private (bool Grid, bool Labels, bool Cutaway) _drawnSwitches;

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
        var switches = (_vm.ShowGrid, _vm.ShowLabels, _vm.IsCutaway);
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
    /// Whether the view renders every frame now: the bench's switch, a camera ease, a finer ray map owed (R-P3-10), or an environment
    /// refresh held back by its rate cap once the timeline has stopped (while it plays, the next time step asks for it, R-P3-11).
    /// </summary>
    public bool IsAnimating => !IsFailed && _visible &&
                               (_forceAnimating || (Director.IsEasing && _frozenHalfHeightCm is null) || (_envPending && !_vm.Main.Timeline.IsPlaying)
                                || RefineOwed);

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
        _hdr = new HdrPipeline();
        _environment = new EnvironmentCapture(_hdr.Enabled);
        _painter = new ExplosionPainter((int)QualityLevel.Low);
        _rings = new RingRenderer();
        _gpuTimer = new GpuTimer();
        _lensing = new LensingPass(_hdr.Enabled);
        _planner.Invalidate();
        _uploadEnvParticles = true;
        _envOutcome = null;
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
        // Measurements of earlier frames, read late. The tag is the rays that frame traced: positive for a whole map (a moving camera:
        // the governor's cost), negative for a refinement band. The planner learns the cost per ray from both (its band size).
        _gpuTimer?.Poll((t, tag) =>
        {
            RecordGpuTime(t);
            _planner.ReportGpuTime((int)Math.Abs(tag), t);
            if (tag > 0) FeedRenderTime(t, interval);
        });
        StepDirector();
        if (_hdr is null || _lensing is null || _environment is null || _painter is null || _rings is null) return;
        FramesDrawn++;
        var state = _vm.State;
        _gpuTimer?.Begin();
        bool lens = state.IsLensed && _lensingEnabled;
        if (lens) RefreshEnvironment();
        _hdr.Begin(viewportPx);
        long tracedTag = 0;
        if (lens && viewportPx.Width > 0 && viewportPx.Height > 0)
            tracedTag = DrawLensing(state, viewportPx);
        if (state.IsLensed)
            foreach (var ring in CurrentRings()) _rings.Render(Camera, viewportPx, ring.RadiusCm / UnitCm, ring.Opacity);
        _hdr.End(_exposure, BloomStrength);
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
    /// the GPU timer's tag: the rays traced, positive for a whole map, negative for a refinement band, 0 for none.
    /// </summary>
    private long DrawLensing(CloseUpState state, Size viewportPx)
    {
        var u = Uniforms(state, viewportPx);
        if (_lensing is null || _environment is null) return 0;
        if (!_lensing.CachesRays)
        {
            _lensing.RenderDirect(viewportPx, MovingLevel, u, _environment.Texture);
            _vm.ShownLensingLevel = MovingLevel;
            MapTraces++;
            var (w, h) = LensingLevels.TargetSize((int)viewportPx.Width, (int)viewportPx.Height, MovingLevel);
            return (long)w * h;
        }
        var step = _planner.Next(u, (int)viewportPx.Width, (int)viewportPx.Height, MovingLevel, StillLevel);
        if (!_lensing.Render(viewportPx, step, u, _environment.Texture))
        {
            _planner.Invalidate();
            return 0;
        }
        _vm.ShownLensingLevel = _planner.ShownLevel;
        if (step.Work == RayMapWork.Trace) MapTraces++;
        if (step.Work == RayMapWork.RefineStrip) RefineBands++;
        return step.Work == RayMapWork.RefineStrip ? -step.Pixels : step.Pixels;
    }

    /// <summary>[Presentation] The bloom strength, the main view's.</summary>
    private const float BloomStrength = 0.5f;

    /// <summary>The lensing pass's uniforms: the camera in GM/c² (scene km × 10⁵ / (GM/c² in cm)), its basis and the hole.</summary>
    private LensingUniforms Uniforms(CloseUpState state, Size viewportPx)
    {
        double rgCm = BlackHoleOptics.GravitationalRadiusCm(state.MassMsun);
        Vector3d eye = Camera.Eye * (UnitCm / rgCm);
        var (right, up) = Camera.ScreenAxes();
        var forward = -Camera.Backward;
        double tanY = Math.Tan(Camera.FieldOfViewDeg * Math.PI / 360);
        double aspect = viewportPx.Width / viewportPx.Height;
        return new LensingUniforms((float)state.Spin, (Vector3)eye, (Vector3)forward, (Vector3)right, (Vector3)up,
            new Vector2((float)(tanY * aspect), (float)tanY), (float)BlackHoleOptics.EscapeRadiusFor(eye.Length),
            (float)(state.SurfaceRadiusKm * UnitCm / rgCm), PlaceholderSurfaceColor, _vm.ShowGrid);
    }

    /// <summary>
    /// Ruling P3-6: renders the environment cube map when the source time has moved by more than 0.005 in log₁₀, or the outcome,
    /// observer or run changed, at most 4 times per wall second; a refresh held back by the rate keeps the view animating until it
    /// is made. The main view's cutaway and quality are not part of it: the environment is the physical surroundings, always at Low.
    /// </summary>
    private void RefreshEnvironment()
    {
        if (_envScene is null || _environment is null || _painter is null) return;
        double t = _vm.SourceTimeS;
        double logT = Math.Log10(Math.Max(t, 1e-9));
        var main = _vm.Main;
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
        _environment.Capture(Vector3d.Zero, (camera, size) => _painter.Paint(camera, size, 1, s, unit));
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
    private IReadOnlyList<Ring> CurrentRings() => _vm.State.IsLensed && HalfHeightCm > 0 && double.IsFinite(HalfHeightCm)
        ? DistanceRings.Rungs(HalfHeightCm)
        : Array.Empty<Ring>();

    /// <summary>Places each ring's label at the top of its circle, from the projection (as the main view, ruling P6).</summary>
    private void UpdateRings()
    {
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

    /// <summary>The next frame read back with GL.ReadPixels (no screen capture), or null on failure or timeout.</summary>
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
        _lensing = null;
        _rings = null;
        _painter = null;
        _environment = null;
        _hdr = null;
    }
}
