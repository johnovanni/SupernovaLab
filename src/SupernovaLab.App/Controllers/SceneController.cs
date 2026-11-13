using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenTK.Graphics.OpenGL4;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Outcomes;
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

    /// <summary>A change of observer time bigger than this many decades in one tick is a jump (timeline click), not playback.</summary>
    public const double JumpDecades = 0.05;

    /// <summary>The longest wall-clock gap one frame hands the director (a frame after idle time is not a long ease).</summary>
    private const double MaxFrameGapS = 0.1;

    private readonly MainViewModel _vm;
    private readonly Func<IRenderHost> _hostFactory;
    private readonly Func<Outcome, double, ExplosionScene> _sceneFactory;
    private readonly Stopwatch _wall = Stopwatch.StartNew();
    private readonly Func<double> _clock;
    private double? _lastFrameS;
    private double _lastObserverS;
    private IRenderHost? _host;
    private ExplosionScene? _scene;
    private SceneState _state = new();
    /// <summary>Review M1: the 3D view's model error, shown in place of its notes; null when the scene is fine.</summary>
    private string? _modelError;
    private StarRenderer? _star;
    private EjectaRenderer? _ejecta;
    private JetRenderer? _jets;
    private RemnantRenderer? _remnant;
    private RingRenderer? _rings;
    private double? _frozenUnitCm;
    private Size _viewDip;
    private TaskCompletionSource<BitmapSource?>? _readback;
    private bool _disposed;

    /// <param name="sceneFactory">Builds the scene of an outcome at a time dilation; null for <see cref="ExplosionScene"/>'s
    /// own constructor (tests pass one that fails, review M1).</param>
    /// <param name="wallClock">Wall-clock seconds for the director's ease; null for the real clock (tests pass a fake).</param>
    public SceneController(MainViewModel vm, Func<IRenderHost> hostFactory,
        Func<Outcome, double, ExplosionScene>? sceneFactory = null, Func<double>? wallClock = null)
    {
        _clock = wallClock ?? (() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        _hostFactory = hostFactory ?? throw new ArgumentNullException(nameof(hostFactory));
        _sceneFactory = sceneFactory ?? ((o, z1) => new ExplosionScene(o, z1));
        BuildScene();
        ComputeState();
        _lastObserverS = vm.Timeline.ObserverTimeS;
        Director.Jump(_state.FrameRadiusCm);
        vm.OutcomeChanged += OnOutcomeChanged;
        vm.Timeline.PropertyChanged += OnTimelineChanged;
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

    /// <summary>True once the GL context and the renderers exist.</summary>
    public bool IsReady => _star is not null;

    [ObservableProperty] private string _timeText = "";
    [ObservableProperty] private string _radiusText = "";
    [ObservableProperty] private string _notesText = "";
    [ObservableProperty] private bool _showRings = true;
    [ObservableProperty] private bool _isAutoFraming = true;
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

    /// <summary>Wheel: zooms (positive notches move closer) and freezes the physical scale until <see cref="Home"/>.</summary>
    public void Zoom(int notches)
    {
        if (notches == 0) return;
        _frozenUnitCm ??= Director.HalfHeightCm;
        Camera.Zoom(notches);
        IsAutoFraming = false;
        UpdateRings();
        RequestRender();
    }

    /// <summary>Home: auto-framing again.</summary>
    public void Home()
    {
        _frozenUnitCm = null;
        Camera.DistanceMpc = AutoDistance;
        Camera.Target = default;
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
        IsAutoFraming = true;
        _lastObserverS = _vm.Timeline.ObserverTimeS;
        Restate(jump: true);   // Review Focus 4: a new outcome reframes at once, even in the middle of an ease
    }

    private void OnTimelineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TimelineViewModel.ObserverTimeS)) return;
        double t = _vm.Timeline.ObserverTimeS;
        bool jump = Math.Abs(Math.Log10(t / _lastObserverS)) > JumpDecades;   // a click or a marker, not a playback tick
        _lastObserverS = t;
        Restate(jump);
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
        if (_host is not null && !IsFailed) _host.Animating = _state.Marker == SceneMarker.Pulsar || Director.IsEasing;
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

    private void UpdateOverlay()
    {
        TimeText = _vm.Timeline.ObserverTimeText;
        RadiusText = double.IsFinite(_state.RadiusCm) && _state.RadiusCm > 0
            ? $"{_state.RadiusLabel}: {UnitFormat.Distance(_state.RadiusCm)}"
            : "";
        NotesText = _modelError ?? string.Join(Environment.NewLine, _state.Notes);
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

    private static string ModelErrorText(Exception e)
    {
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
            RingLabels = Array.Empty<RingLabel>();
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
        RingLabels = labels;
    }

    /// <summary>"vendor | renderer | version" as the GL driver reports them (the GPU name for the performance notes); empty before the first frame.</summary>
    public string GlInfo { get; private set; } = "";

    private void OnContextReady(string glInfo)
    {
        GlInfo = glInfo;
        string version = glInfo.Split('|').ElementAtOrDefault(2)?.Trim() ?? "";
        if (!IsSupported(version))
            throw new NotSupportedException($"this computer's OpenGL is {(version.Length > 0 ? version : "unknown")}");
        _star = new StarRenderer();
        _ejecta = new EjectaRenderer();
        _jets = new JetRenderer();
        _remnant = new RemnantRenderer(_jets);
        _rings = new RingRenderer();
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
        StepDirector();
        if (_star is null || _ejecta is null || _jets is null || _remnant is null || _rings is null) return;
        FramesDrawn++;
        var s = _state;
        double unit = UnitCm;
        if (unit > 0 && double.IsFinite(unit) && viewportPx.Width > 0 && viewportPx.Height > 0)
        {
            double pxPerDip = _viewDip.Height > 0 ? viewportPx.Height / _viewDip.Height : 1;
            if (s.ShowGas) _star.RenderGas(Camera, viewportPx, s.GasRadiusCm / unit);
            if (s.ShowStar) _star.Render(Camera, viewportPx, s.StarRadiusCm / unit, s.StarColor);
            if (s.ShowEjecta)
                _ejecta.Render(Camera, viewportPx, pxPerDip, s.EjectaRadiusCm / unit, s.EjectaColor, s.EjectaBrightness);
            if (s.ShowJets) _jets.RenderJets(Camera, viewportPx, s.JetHalfAngleRad, s.FrameRadiusCm / unit);
            foreach (var ring in CurrentRings()) _rings.Render(Camera, viewportPx, ring.RadiusCm / unit, ring.Opacity);
            double phase = 2 * Math.PI * _wall.Elapsed.TotalSeconds / ExplosionScene.ShownPulsarPeriodS;
            // The beams reach 0.9 of the framing radius, as before the director (the scene unit is now the half-height).
            double beam = double.IsFinite(s.FrameRadiusCm) ? 0.9 * s.FrameRadiusCm / unit : 0.9;
            _remnant.Render(Camera, viewportPx, pxPerDip, s.Marker, phase, beam);
        }
        if (_readback is { } readback)
        {
            _readback = null;
            readback.TrySetResult(ReadPixels(viewportPx));
        }
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
                    _rings?.Dispose();
                    _remnant?.Dispose();
                    _jets?.Dispose();
                    _ejecta?.Dispose();
                    _star?.Dispose();
                });
            }
            catch (Exception e)
            {
                GlReleased = false;
                Debug.WriteLine($"SceneController: releasing the GL objects failed: {e}");
            }
        }
        _rings = null;
        _remnant = null;
        _jets = null;
        _ejecta = null;
        _star = null;
    }
}

/// <summary>A distance ring's label: its text, its position (device-independent pixels from the view's top left) and opacity.</summary>
public sealed record RingLabel(string Text, double XDip, double YDip, double Opacity);
