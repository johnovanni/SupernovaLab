using System.Windows;
using System.Windows.Input;
using SupernovaLab.App.Controllers;
using SupernovaLab.App.Services;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Rendering;

namespace SupernovaLab.App.Windows;

/// <summary>
/// The Close-up window (spec §2): the compact remnant's 3D view beside its readouts, on the main window's timeline. Its
/// code-behind only wires the view, as <see cref="MainWindow"/>'s does: it makes the <see cref="CloseUpSceneController"/> once the
/// window has loaded with its <see cref="CloseUpViewModel"/>, passes it the mouse, the Home and F keys and whether the window is
/// shown (it stops drawing when minimized or hidden, spec §2.5), and disposes the controller and the view model when the window
/// closes, so the long-lived main view model keeps neither alive.
/// </summary>
public partial class CloseUpWindow : Window
{
    private const double DragSlopDip = 3;

    private Point _lastMouse, _downAt;
    private bool _pressed, _dragged;
    private int _wheelDelta;
    private GlHost? _glHost;

    public CloseUpWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
        IsVisibleChanged += (_, _) => UpdateVisible();
        StateChanged += (_, _) => UpdateVisible();
        // As MainWindow: Closing comes while the GL control is still loaded, so the GL objects are released there.
        Closing += (_, _) => DisposeScene();
        Closed += (_, _) =>
        {
            DisposeScene();
            (DataContext as CloseUpViewModel)?.Dispose();
        };
    }

    /// <summary>The beams' clock shared with the main view (plan ruling P3-5); set by the window service before the window loads.</summary>
    public BeamClock? BeamClock { get; init; }

    /// <summary>The view's controller, once the window has loaded.</summary>
    public CloseUpSceneController? Scene { get; private set; }

    /// <summary>Whether the GL control had its context right after Start (--snapshot logs it).</summary>
    public bool? GlContextExistsAfterStart => _glHost?.ContextExistsAfterStart;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Scene is not null || DataContext is not CloseUpViewModel vm) return;
        Scene = new CloseUpSceneController(vm, () => _glHost = new GlHost(GlControl), BeamClock);
        ViewHost.DataContext = Scene;
        Scene.SetViewSize(new Size(ViewGrid.ActualWidth, ViewGrid.ActualHeight));
        ViewGrid.SizeChanged += OnViewSizeChanged;
        ViewGrid.MouseDown += OnMouseDown;
        ViewGrid.MouseMove += OnMouseMove;
        ViewGrid.MouseUp += OnMouseUp;
        ViewGrid.LostMouseCapture += OnLostMouseCapture;
        ViewGrid.MouseWheel += OnMouseWheel;
        UpdateVisible();
        Scene.Start();
    }

    private void UpdateVisible() => Scene?.SetVisible(IsVisible && WindowState != WindowState.Minimized);

    private void DisposeScene()
    {
        if (Scene is null) return;
        ViewGrid.SizeChanged -= OnViewSizeChanged;
        ViewGrid.MouseDown -= OnMouseDown;
        ViewGrid.MouseMove -= OnMouseMove;
        ViewGrid.MouseUp -= OnMouseUp;
        ViewGrid.LostMouseCapture -= OnLostMouseCapture;
        ViewGrid.MouseWheel -= OnMouseWheel;
        Scene.Dispose();
        LastGlReleased = Scene.GlReleased;
        Scene = null;
    }

    /// <summary>Whether the last controller released its GL objects with the context current (logged by --snapshot).</summary>
    public bool? LastGlReleased { get; private set; }

    /// <summary>--snapshot: shows the view's current frame, read back with GL.ReadPixels, in place of the GL control. Returns a log line.</summary>
    public async Task<string> ShowFrameForSnapshotAsync()
    {
        if (Scene is null) return "Close-up view: not started";
        var frame = await Scene.CaptureFrameAsync(TimeSpan.FromSeconds(5));
        if (frame is null)
            return Scene.IsFailed ? $"Close-up view: {Scene.FailureMessage}" : "Close-up view: no frame was read back";
        SnapshotImage.Source = frame;
        SnapshotImage.Visibility = Visibility.Visible;
        return $"Close-up view read back with GL.ReadPixels: {frame.PixelWidth}x{frame.PixelHeight} px";
    }

    /// <summary>--snapshot: back to the live GL control.</summary>
    public void HideFrameForSnapshot()
    {
        SnapshotImage.Visibility = Visibility.Collapsed;
        SnapshotImage.Source = null;
    }

    private void OnViewSizeChanged(object sender, SizeChangedEventArgs e) => Scene?.SetViewSize(e.NewSize);

    private void OnAutoFrameClick(object sender, RoutedEventArgs e) => Scene?.Home();

    /// <summary>F toggles the Focus view (not while typing in a box or using a menu); Home frames the remnant again.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None || e.IsRepeat) return;
        if (e.Key == Key.Home && Scene is not null)
        {
            Scene.Home();
            e.Handled = true;
            return;
        }
        if (e.Key != Key.F || DataContext is not CloseUpViewModel vm) return;
        if (!LayoutViewModel.FocusShortcutApplies(Keyboard.FocusedElement)) return;
        vm.ToggleFocusCommand.Execute(null);
        e.Handled = true;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        _pressed = true;
        _dragged = false;
        _downAt = _lastMouse = e.GetPosition(ViewGrid);
        ViewGrid.CaptureMouse();
        GlControl.Focus();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed) return;
        var p = e.GetPosition(ViewGrid);
        if (!_dragged && (p - _downAt).Length < DragSlopDip) return;
        _dragged = true;
        Scene?.Rotate(p.X - _lastMouse.X, p.Y - _lastMouse.Y);
        _lastMouse = p;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !_pressed) return;
        _pressed = false;
        ViewGrid.ReleaseMouseCapture();
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e) => _pressed = false;

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _wheelDelta += e.Delta;
        int notches = _wheelDelta / Mouse.MouseWheelDeltaForOneLine;
        if (notches == 0) return;
        _wheelDelta -= notches * Mouse.MouseWheelDeltaForOneLine;
        Scene?.Zoom(notches);
        e.Handled = true;
    }
}
