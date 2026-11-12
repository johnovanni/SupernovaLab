using System.Windows;
using System.Windows.Input;
using SupernovaLab.App.Controllers;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Rendering;

namespace SupernovaLab.App;

/// <summary>
/// The lab window (spec §5.0). Its code-behind only wires the 3D view: it makes the <see cref="SceneController"/> once
/// the window is loaded with its <see cref="MainViewModel"/>, passes it the mouse and the Home key, and disposes it when
/// the window goes away, so the long-lived view model does not keep it alive.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>A press that moves less than this (DIP) is not a drag.</summary>
    private const double DragSlopDip = 3;

    private Point _lastMouse, _downAt;
    private bool _pressed, _dragged;
    private int _wheelDelta;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, _) => DisposeScene();
        Closed += (_, _) => DisposeScene();
    }

    /// <summary>The 3D view's controller, once the window has loaded.</summary>
    public SceneController? Scene { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Scene is not null || DataContext is not MainViewModel vm) return;
        Scene = new SceneController(vm, () => new GlHost(GlControl));
        SceneHost.DataContext = Scene;
        Scene.SetViewSize(new Size(SceneGrid.ActualWidth, SceneGrid.ActualHeight));
        SceneGrid.SizeChanged += OnSceneSizeChanged;
        GlControl.MouseDown += OnMouseDown;
        GlControl.MouseMove += OnMouseMove;
        GlControl.MouseUp += OnMouseUp;
        GlControl.MouseWheel += OnMouseWheel;
        SceneHost.KeyDown += OnSceneKeyDown;
        Scene.Start();
    }

    private void DisposeScene()
    {
        if (Scene is null) return;
        SceneGrid.SizeChanged -= OnSceneSizeChanged;
        GlControl.MouseDown -= OnMouseDown;
        GlControl.MouseMove -= OnMouseMove;
        GlControl.MouseUp -= OnMouseUp;
        GlControl.MouseWheel -= OnMouseWheel;
        SceneHost.KeyDown -= OnSceneKeyDown;
        Scene.Dispose();
        Scene = null;
    }

    /// <summary>
    /// --snapshot: shows the 3D view's current frame, read back with GL.ReadPixels, in place of the GL control so the
    /// window's RenderTargetBitmap includes it. Returns a line for the snapshot log.
    /// </summary>
    public async Task<string> ShowSceneFrameForSnapshotAsync()
    {
        if (Scene is null) return "3D view: not started";
        var frame = await Scene.CaptureFrameAsync(TimeSpan.FromSeconds(5));
        if (frame is null)
            return Scene.IsFailed ? $"3D view: {Scene.FailureMessage}" : "3D view: no frame was read back";
        SnapshotImage.Source = frame;
        SnapshotImage.Visibility = Visibility.Visible;
        return $"3D view read back with GL.ReadPixels: {frame.PixelWidth}x{frame.PixelHeight} px";
    }

    /// <summary>--snapshot: back to the live GL control.</summary>
    public void HideSceneFrameForSnapshot()
    {
        SnapshotImage.Visibility = Visibility.Collapsed;
        SnapshotImage.Source = null;
    }

    private void OnSceneSizeChanged(object sender, SizeChangedEventArgs e) => Scene?.SetViewSize(e.NewSize);

    private void OnAutoFrameClick(object sender, RoutedEventArgs e) => Scene?.Home();

    private void OnSceneKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Home) return;
        Scene?.Home();
        e.Handled = true;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        _pressed = true;
        _dragged = false;
        _downAt = _lastMouse = e.GetPosition(GlControl);
        GlControl.CaptureMouse();
        GlControl.Focus();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed) return;
        var p = e.GetPosition(GlControl);
        if (!_dragged && (p - _downAt).Length < DragSlopDip) return;
        _dragged = true;
        Scene?.Rotate(p.X - _lastMouse.X, p.Y - _lastMouse.Y);
        _lastMouse = p;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !_pressed) return;
        _pressed = false;
        GlControl.ReleaseMouseCapture();
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Touchpads send fractions of a notch; zoom once a whole notch has accumulated.
        _wheelDelta += e.Delta;
        int notches = _wheelDelta / Mouse.MouseWheelDeltaForOneLine;
        if (notches == 0) return;
        _wheelDelta -= notches * Mouse.MouseWheelDeltaForOneLine;
        Scene?.Zoom(notches);
        e.Handled = true;
    }
}
