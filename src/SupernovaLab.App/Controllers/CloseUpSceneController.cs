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
/// the remnant's position by an <see cref="ExplosionPainter"/> of this context), then the <see cref="LensingPass"/> traces the view
/// at the current <see cref="LensingLevel"/> (ruling P3-9, stepped down by a <see cref="LensingGovernor"/> when the GPU time stays
/// over 12 ms), then the distance rings, then the main view's bloom and tone map at its exposure. It skips drawing while the
/// window is hidden or minimized (<see cref="SetVisible"/>, spec §2.5).</para>
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

    // ---- Lensing level (ruling P3-9) ----

    private readonly LensingGovernor _governor = new(LensingLevel.Full);

    /// <summary>The lensing pass's resolution now.</summary>
    public LensingLevel Level => _governor.Level;

    /// <summary>Whether the governor may step the level down (on by default; the bench measures fixed levels with it off).</summary>
    public bool AutoLevel { get; set; } = true;

    /// <summary>Sets the level (the bench, or a later menu) and starts the governor's window over.</summary>
    public void SetLevel(LensingLevel level)
    {
        _governor.Reset(level);
        OnPropertyChanged(nameof(Level));
        RequestRender();
    }

    /// <summary>Feeds the governor one drawn frame's GPU time and the wall time since the previous frame (s); public as the test hook.</summary>
    public void FeedRenderTime(double gpuTimeS, double frameIntervalS)
    {
        if (!AutoLevel || frameIntervalS > SceneController.MaxGovernedFrameS) return;
        if (_governor.Update(gpuTimeS, frameIntervalS)) OnPropertyChanged(nameof(Level));
    }

    private double _gpuSumS;

    public int GpuSamples { get; private set; }
    public double MeanGpuRenderTimeS => GpuSamples > 0 ? _gpuSumS / GpuSamples : double.NaN;
    public double MaxGpuRenderTimeS { get; private set; }
    public void ResetGpuStats() { _gpuSumS = 0; GpuSamples = 0; MaxGpuRenderTimeS = 0; }

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

    /// <summary>Wheel: zooms (positive notches move closer) and freezes the scale until <see cref="Home"/>.</summary>
    public void Zoom(int notches)
    {
        if (notches == 0) return;
        _frozenHalfHeightCm ??= Director.HalfHeightCm;
        Camera.Zoom(notches);
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
    }

    private void OnStateChanged(object? sender, bool rebuilt)
    {
        if (_disposed) return;
        if (rebuilt)
        {
            BuildEnvironmentScene();
            // A new outcome reframes at once (as the main view does), keeping the angles the user chose.
            _frozenHalfHeightCm = null;
            IsAutoFraming = true;
            Director.Jump(_vm.State.FrameRadiusKm * UnitCm);
            ApplyAutoDistance();
        }
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

    /// <summary>Whether the view renders every frame now: the bench's switch, a camera ease, or an environment refresh still owed.</summary>
    public bool IsAnimating => !IsFailed && _visible &&
                               (_forceAnimating || (Director.IsEasing && _frozenHalfHeightCm is null) || _envPending);

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
        _gpuTimer?.Poll(t => { RecordGpuTime(t); FeedRenderTime(t, interval); });
        StepDirector();
        if (_hdr is null || _lensing is null || _environment is null || _painter is null || _rings is null) return;
        FramesDrawn++;
        var state = _vm.State;
        _gpuTimer?.Begin();
        if (state.IsLensed) RefreshEnvironment();
        _hdr.Begin(viewportPx);
        if (state.IsLensed && viewportPx.Width > 0 && viewportPx.Height > 0)
            _lensing.Render(viewportPx, Level, Uniforms(state, viewportPx), _environment.Texture);
        if (state.IsLensed)
            foreach (var ring in CurrentRings()) _rings.Render(Camera, viewportPx, ring.RadiusCm / UnitCm, ring.Opacity);
        _hdr.End(_exposure, BloomStrength);
        _gpuTimer?.End();
        if (_readback is { } readback)
        {
            _readback = null;
            readback.TrySetResult(ReadPixels(viewportPx));
        }
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
