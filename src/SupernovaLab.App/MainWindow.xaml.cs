using System.Windows;
using System.Windows.Input;
using SupernovaLab.App.Controllers;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Rendering;

namespace SupernovaLab.App;

/// <summary>
/// The lab window (spec §5.0). Its code-behind only wires the 3D view: it makes the <see cref="SceneController"/> once
/// the window is loaded with its <see cref="MainViewModel"/>, passes it the mouse and the Home key, and disposes it when
/// the window closes, so the long-lived view model does not keep it alive.
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
        DataContextChanged += OnDataContextChanged;
        PreviewKeyDown += OnPreviewKeyDown;
        // GLWpfControl releases its GL context in its own Unloaded handler, which runs before any handler of ours on
        // the window's Unloaded or Closed. Closing comes first, while the control is still loaded and its context alive,
        // so the scene's GL objects are deleted there with the context current (Closed is a no-op fallback).
        Closing += (_, _) => DisposeScene();
        Closed += (_, _) => DisposeScene();
    }

    private GlHost? _glHost;
    private LayoutViewModel? _layout;

    /// <summary>Task 15: the side columns take their widths from the layout (a fold strip, the panel, or nothing in the Focus view).</summary>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_layout is not null) _layout.PropertyChanged -= OnLayoutChanged;
        _layout = (e.NewValue as MainViewModel)?.Layout;
        if (_layout is null) return;
        _layout.PropertyChanged += OnLayoutChanged;
        ApplyColumnWidths();
    }

    private void OnLayoutChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LayoutViewModel.LabColumnWidth) or nameof(LayoutViewModel.OutcomeColumnWidth)) ApplyColumnWidths();
    }

    private void ApplyColumnWidths()
    {
        if (_layout is null) return;
        LabColumn.Width = _layout.LabColumnWidth;
        OutcomeColumn.Width = _layout.OutcomeColumnWidth;
    }

    /// <summary>F toggles the Focus view, but not while the user types in a text box or combo box (Task 15).</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F || Keyboard.Modifiers != ModifierKeys.None || e.IsRepeat || _layout is null) return;
        if (!LayoutViewModel.FocusShortcutApplies(Keyboard.FocusedElement)) return;
        _layout.ToggleFocusCommand.Execute(null);
        e.Handled = true;
    }

    /// <summary>Whether the GL control had its context right after Start (--snapshot logs it; see GlHost).</summary>
    public bool? GlContextExistsAfterStart => _glHost?.ContextExistsAfterStart;

    /// <summary>The 3D view's controller, once the window has loaded.</summary>
    public SceneController? Scene { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Scene is not null || DataContext is not MainViewModel vm) return;
        Scene = new SceneController(vm, () => _glHost = new GlHost(GlControl));
        SceneHost.DataContext = Scene;
        vm.Tour.AttachScene(Scene);
        Scene.SetViewSize(new Size(SceneGrid.ActualWidth, SceneGrid.ActualHeight));
        SceneGrid.SizeChanged += OnSceneSizeChanged;
        SceneGrid.MouseDown += OnMouseDown;
        SceneGrid.MouseMove += OnMouseMove;
        SceneGrid.MouseUp += OnMouseUp;
        SceneGrid.LostMouseCapture += OnLostMouseCapture;
        SceneGrid.MouseWheel += OnMouseWheel;
        SceneHost.KeyDown += OnSceneKeyDown;
        Scene.Start();
    }

    private void DisposeScene()
    {
        if (Scene is null) return;
        (DataContext as MainViewModel)?.Tour.AttachScene(null);
        SceneGrid.SizeChanged -= OnSceneSizeChanged;
        SceneGrid.MouseDown -= OnMouseDown;
        SceneGrid.MouseMove -= OnMouseMove;
        SceneGrid.MouseUp -= OnMouseUp;
        SceneGrid.LostMouseCapture -= OnLostMouseCapture;
        SceneGrid.MouseWheel -= OnMouseWheel;
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

    /// <summary>A press on the tour caption is not the start of a drag of the 3D view.</summary>
    private void OnTourPanelMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        // Task 15: a click anywhere on the caption resumes a paused tour.
        if (e.ChangedButton == MouseButton.Left && (sender as FrameworkElement)?.DataContext is TourViewModel tour) tour.OnCaptionClicked();
    }

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
        _downAt = _lastMouse = e.GetPosition(SceneGrid);
        SceneGrid.CaptureMouse();
        GlControl.Focus();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed) return;
        var p = e.GetPosition(SceneGrid);
        if (!_dragged && (p - _downAt).Length < DragSlopDip) return;
        if (!_dragged) Scene?.BeginUserDrag();   // the tour's sway waits while the mouse is down (Task 15)
        _dragged = true;
        Scene?.Rotate(p.X - _lastMouse.X, p.Y - _lastMouse.Y);
        _lastMouse = p;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || !_pressed) return;
        _pressed = false;
        Scene?.EndUserDrag();
        SceneGrid.ReleaseMouseCapture();
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        _pressed = false;
        Scene?.EndUserDrag();
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
