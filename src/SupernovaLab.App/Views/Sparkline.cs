using System.Windows;
using System.Windows.Media;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Timeline;

namespace SupernovaLab.App.Views;

/// <summary>
/// A tile's sparkline (spec §5.0): a panel's main series over the whole log timeline (so its cursor lines up with the
/// scrubber below), drawn as each pixel column's minimum and maximum, so a dense waveform shows as its envelope. Log
/// series show the top 8 decades. The geometry is rebuilt only when the series or size change; the cursor is drawn on top.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty SeriesProperty = DependencyProperty.Register(nameof(Series),
        typeof(IReadOnlyList<PlotSeries>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((Sparkline)d)._geometry = null));

    public static readonly DependencyProperty InvertYProperty = DependencyProperty.Register(nameof(InvertY), typeof(bool),
        typeof(Sparkline),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((Sparkline)d)._geometry = null));

    public static readonly DependencyProperty CursorFractionProperty = DependencyProperty.Register(nameof(CursorFraction),
        typeof(double), typeof(Sparkline), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(nameof(LineBrush), typeof(Brush),
        typeof(Sparkline), new FrameworkPropertyMetadata(Brushes.SkyBlue, FrameworkPropertyMetadataOptions.AffectsRender,
            (d, _) => ((Sparkline)d)._pen = null));

    public static readonly DependencyProperty CursorBrushProperty = DependencyProperty.Register(nameof(CursorBrush), typeof(Brush),
        typeof(Sparkline), new FrameworkPropertyMetadata(Brushes.Gold, FrameworkPropertyMetadataOptions.AffectsRender,
            (d, _) => ((Sparkline)d)._cursorPen = null));

    private const double LogDecades = 8;

    private StreamGeometry? _geometry;
    private Size _geometrySize;
    private Pen? _pen, _cursorPen;

    public IReadOnlyList<PlotSeries>? Series
    {
        get => (IReadOnlyList<PlotSeries>?)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public bool InvertY
    {
        get => (bool)GetValue(InvertYProperty);
        set => SetValue(InvertYProperty, value);
    }

    public double CursorFraction
    {
        get => (double)GetValue(CursorFractionProperty);
        set => SetValue(CursorFractionProperty, value);
    }

    public Brush LineBrush
    {
        get => (Brush)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public Brush CursorBrush
    {
        get => (Brush)GetValue(CursorBrushProperty);
        set => SetValue(CursorBrushProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = RenderSize;
        if (size.Width <= 2 || size.Height <= 2) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(size));
        if (_geometry is null || _geometrySize != size)
        {
            _geometry = Build(size);
            _geometrySize = size;
        }
        _pen ??= Frozen(new Pen(LineBrush, 1.2));
        _cursorPen ??= Frozen(new Pen(CursorBrush, 1.5));
        if (_geometry is not null) dc.DrawGeometry(null, _pen, _geometry);
        double x = Math.Round(Math.Clamp(CursorFraction, 0, 1) * (size.Width - 1)) + 0.5;
        dc.DrawLine(_cursorPen, new Point(x, 0), new Point(x, size.Height));
    }

    private static Pen Frozen(Pen p)
    {
        p.Freeze();
        return p;
    }

    /// <summary>Each series as per-column min/max zigzags (a new figure across gaps).</summary>
    private StreamGeometry? Build(Size size)
    {
        var series = Series;
        if (series is null || series.Count == 0) return null;
        bool log = series[0].LogY;
        double Y(double v) => log ? Math.Log10(v) : v;

        double max = double.NegativeInfinity, min = double.PositiveInfinity;
        foreach (var s in series)
        foreach (double v in s.Values)
        {
            double y = Y(v);
            if (!double.IsFinite(y)) continue;
            max = Math.Max(max, y);
            min = Math.Min(min, y);
        }
        if (!double.IsFinite(max)) return null;
        if (log) min = Math.Max(min, max - LogDecades);
        if (!(max > min)) { max += 0.5; min -= 0.5; }
        double h = size.Height - 2, w = size.Width - 1;
        double Py(double y)
        {
            double f = (Math.Clamp(y, min, max) - min) / (max - min);
            return 1 + (InvertY ? f : 1 - f) * h;
        }

        int columns = Math.Max(1, (int)w + 1);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            foreach (var s in series)
            {
                var lo = new double[columns];
                var hi = new double[columns];
                Array.Fill(lo, double.PositiveInfinity);
                Array.Fill(hi, double.NegativeInfinity);
                for (int i = 0; i < s.Values.Length; i++)
                {
                    double y = Y(s.Values[i]);
                    if (!double.IsFinite(y)) continue;
                    int c = (int)Math.Round(TimeAxis.ToFraction(s.ObserverTimesS[i]) * w);
                    if (c < 0 || c >= columns) continue;
                    lo[c] = Math.Min(lo[c], y);
                    hi[c] = Math.Max(hi[c], y);
                }
                bool open = false;
                for (int c = 0; c < columns; c++)
                {
                    if (!double.IsFinite(lo[c])) continue;
                    var a = new Point(c + 0.5, Py(lo[c]));
                    var b = new Point(c + 0.5, Py(hi[c]));
                    if (!open)
                    {
                        ctx.BeginFigure(a, false, false);
                        open = true;
                    }
                    else ctx.LineTo(a, true, true);
                    if (hi[c] > lo[c]) ctx.LineTo(b, true, true);
                }
            }
        }
        geometry.Freeze();
        return geometry;
    }
}
