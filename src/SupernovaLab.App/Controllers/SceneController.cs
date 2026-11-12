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
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;

namespace SupernovaLab.App.Controllers;

/// <summary>
/// The simple 3D explosion view (spec §6). It follows the shared <see cref="MainViewModel"/>: a new outcome or observer
/// (<see cref="MainViewModel.OutcomeChanged"/>) rebuilds the <see cref="ExplosionScene"/>, and each time change (observer
/// time ÷ (1 + z) = source time) restates what is drawn and redraws. It is also the overlay's view model: the time, the
/// physical radius, the notes and the scale bar. Drawing goes through an <see cref="IRenderHost"/> made by the factory
/// given, so the failure path (spec §9) can be tested without OpenGL: if the host cannot start or a frame fails, the view
/// shows <see cref="FailureMessage"/>, naming the requirement, and the rest of the app carries on.
/// <para>Camera: auto-framing keeps the framing radius (ejecta or star) at 60% of the view's height; dragging rotates
/// (and keeps auto-framing); the wheel zooms and freezes the physical scale, so the debris then grow across the view;
/// Home restores auto-framing. The scene is drawn in units of the framing radius (or the frozen one), so it never
/// stresses float precision whatever the physical size.</para>
/// <para>Call <see cref="Dispose"/> when the window closes (its Closing event, while the GL control is still loaded):
/// the view model lives as long as the app (Task 15's leak).</para>
/// </summary>
public sealed partial class SceneController : ObservableObject, IDisposable
{
    /// <summary>The OpenGL version the 3D view needs (spec §1 and §9).</summary>
    public const string Requirement = "OpenGL 4.1";

    /// <summary>Auto-framing puts the framing diameter at this fraction of the view's height (task brief).</summary>
    public const double FrameFraction = 0.6;

    /// <summary>The scale bar is at most this long, in device-independent pixels.</summary>
    public const double ScaleBarMaxDip = 150;

    private readonly MainViewModel _vm;
    private readonly Func<IRenderHost> _hostFactory;
    private readonly Stopwatch _wall = Stopwatch.StartNew();
    private IRenderHost? _host;
    private ExplosionScene _scene;
    private SceneState _state;
    private StarRenderer? _star;
    private EjectaRenderer? _ejecta;
    private JetRenderer? _jets;
    private RemnantRenderer? _remnant;
    private double? _frozenUnitCm;
    private Size _viewDip;
    private TaskCompletionSource<BitmapSource?>? _readback;
    private bool _disposed;

    public SceneController(MainViewModel vm, Func<IRenderHost> hostFactory)
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        _hostFactory = hostFactory ?? throw new ArgumentNullException(nameof(hostFactory));
        _scene = new ExplosionScene(vm.CurrentOutcome, vm.CurrentObserver.TimeDilation);
        _state = _scene.At(SourceTimeS);
        vm.OutcomeChanged += OnOutcomeChanged;
        vm.Timeline.PropertyChanged += OnTimelineChanged;
        UpdateOverlay();
    }

    /// <summary>The camera distance at which a sphere of radius 1 fills <see cref="FrameFraction"/> of the view's height.</summary>
    public static double AutoDistance { get; } = 1 / (FrameFraction * Math.Tan(45 * Math.PI / 360));

    public OrbitCamera Camera { get; } = new() { YawDeg = 30, PitchDeg = 20, DistanceMpc = AutoDistance };

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
    [ObservableProperty] private string _scaleBarText = "";
    [ObservableProperty] private double _scaleBarWidth;
    [ObservableProperty] private bool _isAutoFraming = true;
    [ObservableProperty] private bool _isFailed;
    [ObservableProperty] private string _failureMessage = "";

    private double SourceTimeS => _vm.Timeline.ObserverTimeS / _vm.CurrentObserver.TimeDilation;

    /// <summary>The physical size (cm) of one scene unit: the framing radius, or the frozen one after a zoom.</summary>
    private double UnitCm => _frozenUnitCm ?? _state.FrameRadiusCm;

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
            _host.Animating = _state.Marker == SceneMarker.Pulsar;
            _host.RequestRender();
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
        }
    }

    /// <summary>The view's size in device-independent pixels (for the scale bar and marker sizes).</summary>
    public void SetViewSize(Size dip)
    {
        _viewDip = dip;
        UpdateScaleBar();
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
        _frozenUnitCm ??= _state.FrameRadiusCm;
        Camera.Zoom(notches);
        IsAutoFraming = false;
        UpdateScaleBar();
        RequestRender();
    }

    /// <summary>Home: auto-framing again.</summary>
    public void Home()
    {
        _frozenUnitCm = null;
        Camera.DistanceMpc = AutoDistance;
        Camera.Target = default;
        IsAutoFraming = true;
        UpdateScaleBar();
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
        _scene = new ExplosionScene(_vm.CurrentOutcome, _vm.CurrentObserver.TimeDilation);
        _frozenUnitCm = null;
        Camera.DistanceMpc = AutoDistance;
        Camera.Target = default;
        IsAutoFraming = true;
        Restate();
    }

    private void OnTimelineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TimelineViewModel.ObserverTimeS)) Restate();
    }

    private void Restate()
    {
        _state = _scene.At(SourceTimeS);
        UpdateOverlay();
        if (_host is not null && !IsFailed) _host.Animating = _state.Marker == SceneMarker.Pulsar;
        RequestRender();
    }

    private void UpdateOverlay()
    {
        TimeText = _vm.Timeline.ObserverTimeText;
        RadiusText = double.IsFinite(_state.RadiusCm) && _state.RadiusCm > 0
            ? $"{_state.RadiusLabel}: {UnitFormat.Distance(_state.RadiusCm)}"
            : "";
        NotesText = string.Join(Environment.NewLine, _state.Notes);
        UpdateScaleBar();
    }

    private void UpdateScaleBar()
    {
        double cmPerDip = _viewDip.Height > 0
            ? 2 * Camera.DistanceMpc * Math.Tan(Camera.FieldOfViewDeg * Math.PI / 360) * UnitCm / _viewDip.Height
            : double.NaN;
        var bar = ScaleBar.Choose(cmPerDip, ScaleBarMaxDip);
        ScaleBarText = bar?.Label ?? "";
        ScaleBarWidth = bar?.LengthPx ?? 0;
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
        if (_star is null || _ejecta is null || _jets is null || _remnant is null) return;
        FramesDrawn++;
        var s = _state;
        double unit = UnitCm;
        if (unit > 0 && double.IsFinite(unit))
        {
            double pxPerDip = _viewDip.Height > 0 ? viewportPx.Height / _viewDip.Height : 1;
            if (s.ShowGas) _star.RenderGas(Camera, viewportPx, s.GasRadiusCm / unit);
            if (s.ShowStar) _star.Render(Camera, viewportPx, s.StarRadiusCm / unit, s.StarColor);
            if (s.ShowEjecta)
                _ejecta.Render(Camera, viewportPx, pxPerDip, s.EjectaRadiusCm / unit, s.EjectaColor, s.EjectaBrightness);
            if (s.ShowJets) _jets.RenderJets(Camera, viewportPx, s.JetHalfAngleRad, s.FrameRadiusCm / unit);
            double phase = 2 * Math.PI * _wall.Elapsed.TotalSeconds / ExplosionScene.ShownPulsarPeriodS;
            _remnant.Render(Camera, viewportPx, pxPerDip, s.Marker, phase);
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
        _remnant = null;
        _jets = null;
        _ejecta = null;
        _star = null;
    }
}
