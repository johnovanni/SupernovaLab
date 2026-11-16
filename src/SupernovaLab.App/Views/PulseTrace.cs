using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SupernovaLab.App.Services;
using SupernovaLab.App.ViewModels;

namespace SupernovaLab.App.Views;

/// <summary>
/// The Close-up's pulse trace (spec §4.4): a strip, like a radio telescope's chart, scrolling over the last 10 s, with a thin mark each
/// time a drawn beam crosses the Earth arrow. The marks follow the shared <see cref="BeamClock"/>'s phase at the drawn period, so they
/// are at the drawn rate (never over once a second) and in step with the drawn beams. Marks are thin and move; nothing flashes. It reads
/// the clock once per WPF frame only while pulses are shown (the Close-up keeps animating then, ruling R-P3-11). With no pulses it draws
/// an empty baseline; the reason is in words beside it. Code-only, as <see cref="Sparkline"/> is.
/// </summary>
public sealed class PulseTrace : FrameworkElement
{
    private static readonly Brush Back = Freeze(new SolidColorBrush(Color.FromRgb(0x0B, 0x10, 0x1C)));
    private static readonly Brush Line = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x47, 0x66)));
    private static readonly Brush Mark = Freeze(new SolidColorBrush(Color.FromRgb(0xF2, 0xE2, 0x7A)));
    private static readonly Brush Label = Freeze(new SolidColorBrush(Color.FromRgb(0x88, 0x87, 0x80)));
    private static readonly Pen LinePen = FreezePen(new Pen(Line, 1));
    private static readonly Pen MarkPen = FreezePen(new Pen(Mark, 2));

    private readonly PulseTraceRecorder _recorder = new();
    private CloseUpViewModel? _vm;
    private BeamClock? _clock;
    private bool _subscribed;

    public PulseTrace()
    {
        SnapsToDevicePixels = true;
        MinHeight = 24;
        Unloaded += (_, _) => Detach();
        IsVisibleChanged += (_, _) => UpdateSubscription();
        Loaded += (_, _) => UpdateSubscription();
    }

    /// <summary>The marks recorded now (the tests and the snapshot read them).</summary>
    public PulseTraceRecorder Recorder => _recorder;

    /// <summary>Starts following the view model's pulses with the shared beam clock.</summary>
    public void Attach(CloseUpViewModel vm, BeamClock clock)
    {
        Detach();
        _vm = vm;
        _clock = clock;
        vm.PropertyChanged += OnVmChanged;
        UpdateSubscription();
    }

    private void Detach()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = null;
        _clock = null;
        UpdateSubscription();
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CloseUpViewModel.PulsesShown)) UpdateSubscription();
    }

    private void UpdateSubscription()
    {
        bool want = _vm is { PulsesShown: true } && _clock is not null && IsVisible && IsLoaded;
        if (want == _subscribed)
        {
            if (!want) InvalidateVisual();
            return;
        }
        _subscribed = want;
        if (want) CompositionTarget.Rendering += OnFrame;
        else
        {
            CompositionTarget.Rendering -= OnFrame;
            _recorder.Update(0, 0, Array.Empty<double>(), active: false);
            InvalidateVisual();
        }
    }


    private void OnFrame(object? sender, EventArgs e)
    {
        if (_vm is not { PulsesShown: true } vm || _clock is null) return;
        double drawn = vm.State.DrawnPeriodS;
        if (!(drawn > 0)) return;
        long t0 = BenchTimings.Begin();   // Task 14b: the bench's timing (off in a normal run)
        _recorder.Update(_clock.ElapsedS, _clock.PhaseRad(drawn), vm.CrossingPhasesRad, active: true);
        InvalidateVisual();
        BenchTimings.End(BenchTimings.PulseTrace, t0);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 4 || h < 4) return;
        dc.DrawRectangle(Back, null, new Rect(0, 0, w, h));
        double baseY = h - 10;
        dc.DrawLine(LinePen, new Point(0, baseY), new Point(w, baseY));
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        DrawLabel(dc, "10 s ago", 3, h - 12, dpi, false, w);
        DrawLabel(dc, "now", w - 3, h - 12, dpi, true, w);
        if (_vm is not { PulsesShown: true } || _clock is null) return;
        double now = _clock.ElapsedS;
        foreach (double t in _recorder.TickTimesS)
        {
            double x = w * (1 - (now - t) / PulseTraceRecorder.WindowS);
            if (x < 0 || x > w) continue;
            dc.DrawLine(MarkPen, new Point(x, 3), new Point(x, baseY));
        }
    }

    private void DrawLabel(DrawingContext dc, string text, double x, double y, double dpi, bool right, double width)
    {
        var f = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 9, Label, dpi);
        dc.DrawText(f, new Point(right ? x - f.Width : x, y));
    }

    private static Brush Freeze(Brush b) { b.Freeze(); return b; }
    private static Pen FreezePen(Pen p) { p.Freeze(); return p; }
}
