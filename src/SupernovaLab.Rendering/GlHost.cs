using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Wpf;

namespace SupernovaLab.Rendering;

/// <summary>
/// Copied from Universe3D (spec §7), with the namespace changed, <see cref="IRenderHost"/> added so the scene
/// controller can be tested without a GL context, and the parts this app does not use (the GPU timing) removed.
/// Owns the <see cref="GLWpfControl"/> setup (OpenGL 4.1 core) and turns its render callback into
/// <see cref="ContextReady"/> (once, context current) and <see cref="Draw"/> (each frame, viewport set and cleared).
/// Renders on demand: call <see cref="RequestRender"/> after a change, and set <see cref="Animating"/> while
/// something moves.
/// </summary>
public sealed class GlHost : IRenderHost
{
    // GLWpfControl shows a requested frame one composition late, so a request renders this many frames.
    private const int FramesPerRequest = 2;

    private readonly GLWpfControl _control;
    private bool _contextReady;
    private bool _animating;
    private int _framesOwed;
    // Task 4b review carry-overs (done in plan Task 6): no ticking while the control is not visible, and none after Shutdown.
    private bool _controlVisible;
    private bool _shutdown;

    public GlHost(GLWpfControl control)
    {
        _control = control;
        _control.Render += OnRender;
        _controlVisible = control.IsVisible;   // fix round 1: as the control is now, then followed
        _control.IsVisibleChanged += OnControlVisibleChanged;
    }

    /// <summary>
    /// The control was hidden or shown (its window hidden, minimized or restored). Hidden: the frames owed are dropped and the host stops
    /// ticking, even while animating (a hidden control is not composed, so a tick would only spin). Shown: it ticks again if animating,
    /// and owes one fresh frame. The visibility starts as the control's own when the host is made.
    /// </summary>
    private void OnControlVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _controlVisible = e.NewValue is true;
        if (!_controlVisible) _framesOwed = 0;
        else if (!_shutdown)
        {
            _framesOwed = FramesPerRequest;
            _control.InvalidateVisual();
        }
        UpdateContinuous();
    }

    /// <summary>Raised once, inside the first frame, with "vendor | renderer | version".</summary>
    public event Action<string>? ContextReady;

    /// <summary>Raised each frame with the elapsed time and the viewport size in pixels.</summary>
    public event Action<TimeSpan, Size>? Draw;

    /// <summary>Raised once when a frame throws (shader compile error, GL error, ...); drawing then stops.</summary>
    public event Action<Exception>? FrameFailed;

    /// <summary>The UI-thread time of each frame's drawing (the clear, <see cref="Draw"/> and the error check): the bench's measurement.</summary>
    public CpuTimeStats DrawTime { get; } = new();

    /// <summary>The control's whole WPF render (including the D3D interop) when it is a <see cref="TimedGLWpfControl"/>; else null.</summary>
    public CpuTimeStats? ControlRenderTime => (_control as TimedGLWpfControl)?.RenderTime;

    private Exception? _failure;

    /// <summary>
    /// Whether the control had its GL context right after <see cref="Start"/>, i.e. created it synchronously (so a
    /// failure to create it throws from Start, where the scene controller catches it). Logged by --snapshot.
    /// </summary>
    public bool ContextExistsAfterStart { get; private set; }

    /// <summary>Render every composition frame while true (e.g. during a camera flight).</summary>
    public bool Animating
    {
        get => _animating;
        set { _animating = value; UpdateContinuous(); }
    }

    /// <summary>Creates the context. Throws if OpenGL 4.1 core is unavailable.</summary>
    public void Start()
    {
        _control.Start(new GLWpfControlSettings
        {
            MajorVersion = 4,
            MinorVersion = 1,
            Profile = ContextProfile.Core,
            RenderContinuously = false,
        });
        ContextExistsAfterStart = _control.Context is not null;
    }

    /// <summary>
    /// Stops drawing and runs <paramref name="release"/> with the GL context current, so GL objects can be deleted
    /// (call it while the control is still loaded: GLWpfControl releases its context when it unloads). Returns whether
    /// <paramref name="release"/> ran; it does nothing but stop when the context was never created or is gone.
    /// </summary>
    public bool Shutdown(Action release)
    {
        // The guard first: a late Animating or RequestRender can no longer subscribe the static CompositionTarget.Rendering event,
        // which would keep this host, its control and its window alive (Task 4b review carry-over).
        _shutdown = true;
        _control.Render -= OnRender;
        _control.IsVisibleChanged -= OnControlVisibleChanged;
        _animating = false;
        _framesOwed = 0;
        SetTicking(false);
        if (!_contextReady || _control.Context is not { } context) return false;
        context.MakeCurrent();
        release();
        return true;
    }

    /// <summary>Asks for a frame (two compositions' worth). Nothing is owed while the control is hidden or after <see cref="Shutdown"/>.</summary>
    public void RequestRender()
    {
        if (_shutdown || !_controlVisible) return;
        _framesOwed = FramesPerRequest;
        UpdateContinuous();
        _control.InvalidateVisual();
    }

    private void UpdateContinuous()
    {
        SetTicking(!_shutdown && _controlVisible && _failure == null && (_animating || _framesOwed > 0));
    }

    /// <summary>
    /// Task 4b fix round 1: the host drives continuous frames itself, with a CompositionTarget.Rendering handler that invalidates the
    /// control, instead of GLWpfControl's RenderContinuously. GLWpfControl 4.3.6 subscribes its own Rendering handler only from an
    /// IsVisibleChanged handler that its Start() registers, so a control that is already visible when Start() runs (every view here
    /// starts in its window's Loaded) never gets one, and RenderContinuously did nothing until the window was hidden and shown again.
    /// Measured in fix round 1's diagnostic launch: a Close-up refinement owed, with Animating on, drew 1 frame in 120 composition
    /// frames; only frames asked for from outside a render (RequestRender's own InvalidateVisual) were drawn.
    /// </summary>
    private void SetTicking(bool on)
    {
        if (on == _ticking) return;
        _ticking = on;
        if (on) System.Windows.Media.CompositionTarget.Rendering += OnTick;
        else System.Windows.Media.CompositionTarget.Rendering -= OnTick;
    }

    private bool _ticking;

    /// <summary>Whether the host asks for a frame every composition frame now (Animating, or a requested frame still owed).</summary>
    public bool IsTicking => _ticking;

    private void OnTick(object? sender, EventArgs e) => _control.InvalidateVisual();

    private void OnRender(TimeSpan delta)
    {
        if (_failure != null) return;
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            if (!_contextReady)
            {
                _contextReady = true;
                ContextReady?.Invoke($"{GL.GetString(StringName.Vendor)} | {GL.GetString(StringName.Renderer)} | " +
                                     $"{GL.GetString(StringName.Version)}");
            }
            int w = _control.FrameBufferWidth, h = _control.FrameBufferHeight;
            GL.Viewport(0, 0, w, h);
            GL.ClearColor(0.01f, 0.01f, 0.03f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Disable(EnableCap.DepthTest);
            if (w > 0 && h > 0) Draw?.Invoke(delta, new Size(w, h));
            var error = GL.GetError();
            if (error != OpenTK.Graphics.OpenGL4.ErrorCode.NoError)
                throw new InvalidOperationException($"OpenGL error {error}.");
        }
        catch (Exception ex)
        {
            _failure = ex;
            FrameFailed?.Invoke(ex);
        }
        DrawTime.Add(CpuTimeStats.Since(t0));
        if (_framesOwed > 0) _framesOwed--;
        UpdateContinuous();
    }
}

/// <summary>
/// What the scene controller needs from its GL host: <see cref="GlHost"/> in the app, a fake in tests (spec §9: the
/// failure path is tested without OpenGL).
/// </summary>
public interface IRenderHost
{
    /// <summary>Raised once, inside the first frame, with "vendor | renderer | version".</summary>
    event Action<string>? ContextReady;

    /// <summary>Raised each frame with the elapsed time and the viewport size in pixels.</summary>
    event Action<TimeSpan, Size>? Draw;

    /// <summary>Raised once when a frame throws; drawing then stops.</summary>
    event Action<Exception>? FrameFailed;

    /// <summary>Render every composition frame while true.</summary>
    bool Animating { get; set; }

    /// <summary>Creates the context. Throws if OpenGL 4.1 core is unavailable.</summary>
    void Start();

    void RequestRender();

    /// <summary>
    /// Stops drawing and runs <paramref name="release"/> with the context current; returns whether it ran (false when
    /// the context was never created or is already gone).
    /// </summary>
    bool Shutdown(Action release);
}