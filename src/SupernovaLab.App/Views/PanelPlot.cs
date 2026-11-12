using System.Globalization;
using ScottPlot;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Core.Units;

namespace SupernovaLab.App.Views;

/// <summary>A legend entry under a panel's plot (drawn in WPF, so it never covers the curves).</summary>
public sealed record LegendEntry(string Text, System.Windows.Media.Brush Brush, bool Dashed, bool Marker);

/// <summary>
/// Draws an instrument panel's view model into a ScottPlot <see cref="Plot"/> (spec §5.1): curves against log observer
/// time (x = log10 t, with labelled round times), log or linear y (magnitudes brighter-up), levels and time lines, preset
/// facts as markers, the GW spectrogram on a log colour scale, the schematic spectrum, and the pulse train. The "now"
/// cursor is not drawn here: the view overlays it, so moving the time never re-renders the plot.
/// </summary>
public static class PanelPlot
{
    public static readonly string[] Palette = { "#6CB6FF", "#FFD84D", "#F2706A", "#4CC26A", "#C792EA", "#FFB060" };
    private const string LimitColor = "#B8C2D6", FactColor = "#FFFFFF", BandColor = "#6CB6FF";

    /// <summary>A log-y plot shows at most this many decades below its maximum.</summary>
    private const double MaxDecades = 12;

    private static readonly (double Seconds, string Label)[] TimeTicks =
    {
        (1e-3, "1 ms"), (1e-2, "10 ms"), (0.1, "0.1 s"), (1, "1 s"), (10, "10 s"), (60, "1 min"), (600, "10 min"),
        (3600, "1 h"), (PhysicalConstants.Day, "1 d"), (10 * PhysicalConstants.Day, "10 d"), (100 * PhysicalConstants.Day, "100 d"),
        (PhysicalConstants.Year, "1 yr"), (10 * PhysicalConstants.Year, "10 yr"), (100 * PhysicalConstants.Year, "100 yr"),
        (TimeAxis.MaxS, "1,000 yr"),
    };

    /// <summary>Colours the plot for the dark theme.</summary>
    public static void Style(Plot plot)
    {
        plot.FigureBackground.Color = Color.FromHex("#111827");
        plot.DataBackground.Color = Color.FromHex("#0B101C");
        plot.Axes.Color(Color.FromHex("#8792A8"));
        plot.Grid.MajorLineColor = Color.FromHex("#1E2840");
        foreach (var axis in new IAxis[] { plot.Axes.Bottom, plot.Axes.Left })
        {
            axis.TickLabelStyle.FontSize = 10;
            axis.Label.FontSize = 10.5f;
            axis.Label.Bold = false;
        }
        plot.Axes.Right.IsVisible = false;
        plot.Axes.Right.MinimumSize = 0;
        plot.Axes.Top.IsVisible = false;
    }

    /// <summary>Redraws the plot for the panel's current view; returns its legend entries.</summary>
    public static IReadOnlyList<LegendEntry> Draw(Plot plot, InstrumentPanelViewModel vm)
    {
        plot.Clear();
        Style(plot);
        // Clear() keeps the axes' tick generators, so each view sets its own (a log-time axis must not leak into the
        // spectrum's wavelengths).
        plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic { LabelFormatter = LinearLabel };
        plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic { LabelFormatter = LinearLabel };
        plot.Axes.Bottom.Label.Text = "";
        plot.Axes.Left.Label.Text = vm.YLabel;
        var legend = new List<LegendEntry>();
        switch (vm.Kind)
        {
            case PlotKind.Spectrogram:
                DrawSpectrogram(plot, vm);
                break;
            case PlotKind.Spectrum:
                DrawSpectrum(plot, vm, legend);
                break;
            case PlotKind.PulseTrain:
                DrawPulses(plot, vm);
                break;
            default:
                DrawTimeSeries(plot, vm, legend);
                break;
        }
        return legend;
    }

    /// <summary>The plot x of an observer time on this panel's view, or null where the view has no time axis.</summary>
    public static double? CursorX(InstrumentPanelViewModel vm, double observerTimeS) => vm.Kind switch
    {
        PlotKind.TimeSeries => Math.Log10(observerTimeS),
        PlotKind.Spectrogram => observerTimeS,
        _ => null,
    };

    // ---------------------------------------------------------------- curves against log time

    private static void DrawTimeSeries(Plot plot, InstrumentPanelViewModel vm, List<LegendEntry> legend)
    {
        bool logY = vm.LogY;
        double Y(double v) => logY ? Math.Log10(v) : v;
        var colours = new Dictionary<string, string>();
        string ColourOf(string label)
        {
            if (!colours.TryGetValue(label, out var c)) colours[label] = c = Palette[colours.Count % Palette.Length];
            return c;
        }

        double xMin = double.PositiveInfinity, xMax = double.NegativeInfinity;
        double yMin = double.PositiveInfinity, yMax = double.NegativeInfinity;
        void Extend(double t, double y)
        {
            double x = Math.Log10(t);
            xMin = Math.Min(xMin, x); xMax = Math.Max(xMax, x);
            yMin = Math.Min(yMin, y); yMax = Math.Max(yMax, y);
        }

        foreach (var band in vm.Bands)
        {
            var xs = band.ObserverTimesS.Select(Math.Log10).ToArray();
            var fill = plot.Add.FillY(xs, band.Low.Select(Y).ToArray(), band.High.Select(Y).ToArray());
            fill.FillColor = Color.FromHex(BandColor).WithAlpha(0.28);
            fill.LineWidth = 0;
            for (int i = 0; i < xs.Length; i++) { Extend(band.ObserverTimesS[i], Y(band.Low[i])); Extend(band.ObserverTimesS[i], Y(band.High[i])); }
            legend.Add(new LegendEntry(band.Label, Brush(BandColor, 0.45), false, false));
        }

        foreach (var s in vm.Series)
        {
            var xs = s.ObserverTimesS.Select(Math.Log10).ToArray();
            var ys = s.Values.Select(Y).ToArray();
            string colour = ColourOf(s.Label);
            if (s.IsDots)
            {
                var m = plot.Add.Markers(xs, ys, MarkerShape.FilledCircle, 2.5f, Color.FromHex(colour).WithAlpha(0.8));
                _ = m;
            }
            else if (xs.Length == 1)
            {
                plot.Add.Markers(xs, ys, MarkerShape.FilledCircle, 4, Color.FromHex(colour));
            }
            else
            {
                var line = plot.Add.SignalXY(xs, ys);
                line.Color = Color.FromHex(colour);
                line.LineWidth = 1.5f;
            }
            for (int i = 0; i < xs.Length; i++) Extend(s.ObserverTimesS[i], ys[i]);
        }
        foreach (var label in colours.Keys)
        {
            bool dots = vm.Series.Any(s => s.Label == label && s.IsDots);
            legend.Add(new LegendEntry(label, Brush(colours[label]), false, dots));
        }

        foreach (var f in vm.FactMarkers)
        {
            if (f.ObserverTimeS is not double t) continue;
            double y = Y(f.Fact.Value);
            if (f.Fact.IsUpperLimit)
            {
                // An upper limit (R52 review I2): a bar over the span it covers, a downward triangle at its middle, and
                // its "Upper limit: < …" label.
                double x0 = Math.Log10(t), x1 = f.ObserverTimeEndS is double end ? Math.Log10(end) : x0;
                if (x1 > x0)
                {
                    var bar = plot.Add.Line(x0, y, x1, y);
                    bar.Color = Color.FromHex(FactColor);
                    bar.LineWidth = 2;
                    Extend(f.ObserverTimeEndS!.Value, y);
                }
                double xm = 0.5 * (x0 + x1);
                plot.Add.Markers(new[] { xm }, new[] { y }, MarkerShape.FilledTriangleDown, 10, Color.FromHex(FactColor));
                var text = plot.Add.Text(f.Label, xm, y);
                text.LabelFontSize = 9.5f;
                text.LabelFontColor = Color.FromHex(FactColor);
                text.LabelAlignment = Alignment.LowerCenter;
                text.OffsetY = -8;
                Extend(t, y);
                continue;
            }
            plot.Add.Markers(new[] { Math.Log10(t) }, new[] { y }, MarkerShape.FilledDiamond, 9, Color.FromHex(FactColor));
            Extend(t, y);
        }
        if (vm.FactMarkers.Any(f => !f.Fact.IsUpperLimit))
            legend.Add(new LegendEntry("Observed (preset)", Brush(FactColor), false, true));
        if (vm.FactMarkers.Any(f => f.Fact.IsUpperLimit))
            legend.Add(new LegendEntry("Upper limit (preset): not detected", Brush(FactColor), false, true));

        bool hasData = double.IsFinite(xMin);
        // A time line (a black hole forming) belongs in view even where the curves have stopped.
        if (hasData)
            foreach (var line in vm.ReferenceLines.Where(l => l.IsTime && l.Value >= TimeAxis.MinS && l.Value <= TimeAxis.MaxS))
            {
                xMin = Math.Min(xMin, Math.Log10(line.Value));
                xMax = Math.Max(xMax, Math.Log10(line.Value));
            }
        if (!hasData)
        {
            xMin = Math.Log10(TimeAxis.MinS);
            xMax = Math.Log10(TimeAxis.MaxS);
            yMin = logY ? 0 : 0;
            yMax = logY ? 1 : 1;
        }
        if (xMax - xMin < 0.5) { double c = (xMin + xMax) / 2; xMin = c - 0.25; xMax = c + 0.25; }
        if (logY) yMin = Math.Max(yMin, yMax - MaxDecades);

        foreach (var line in vm.ReferenceLines)
        {
            if (line.IsTime)
            {
                double x = Math.Log10(line.Value);
                if (x < xMin - 0.01 || x > xMax + 0.01) continue;
                var v = plot.Add.VerticalLine(x);
                v.Color = Color.FromHex("#E8508A");
                v.LineWidth = 1;
                v.LinePattern = LinePattern.Dashed;
                legend.Add(new LegendEntry(line.Label, Brush("#E8508A"), true, false));
                continue;
            }
            double yl = Y(line.Value);
            // Levels compare with curves: with nothing drawn, an instrument's limit alone would only mislead.
            if (!double.IsFinite(yl) || !hasData) continue;
            bool observed = line.Label.StartsWith("Observed", StringComparison.Ordinal);
            // A limit far outside the data would flatten the curves: it is drawn only within 3 decades (or 8 mag) of them.
            // An observed level was already placed by the view model under the same rule (its row says when it is not).
            double slack = logY ? InstrumentPanelViewModel.LevelSlackDecades : InstrumentPanelViewModel.LevelSlackLinear;
            if (!observed && (yl < yMin - slack || yl > yMax + slack)) continue;
            yMin = Math.Min(yMin, yl);
            yMax = Math.Max(yMax, yl);
            var h = plot.Add.HorizontalLine(yl);
            string colour = observed ? FactColor : LimitColor;
            h.Color = Color.FromHex(colour).WithAlpha(observed ? 0.8 : 0.65);
            h.LineWidth = 1;
            h.LinePattern = observed ? LinePattern.Dotted : LinePattern.Dashed;
            legend.Add(new LegendEntry(line.Label, Brush(colour), true, false));
        }

        double pad = (yMax - yMin) * 0.06;
        if (!(pad > 0)) pad = logY ? 0.5 : Math.Max(Math.Abs(yMax) * 0.1, 1e-30);
        double xPad = (xMax - xMin) * 0.02;
        plot.Axes.SetLimitsX(xMin - xPad, xMax + xPad);
        if (vm.InvertY) plot.Axes.SetLimitsY(yMax + pad, yMin - pad);
        else plot.Axes.SetLimitsY(yMin - pad, yMax + pad);

        plot.Axes.Bottom.TickGenerator = TimeTickGenerator(xMin - xPad, xMax + xPad);
        if (logY) plot.Axes.Left.TickGenerator = DecadeTickGenerator(yMin - pad, yMax + pad);
    }

    /// <summary>Round times (1 ms, 1 s, 1 h, 1 d, 1 yr, …) inside the range, thinned to at most 7 labels; minor ticks per decade.</summary>
    private static ScottPlot.TickGenerators.NumericManual TimeTickGenerator(double xMin, double xMax)
    {
        var tg = new ScottPlot.TickGenerators.NumericManual();
        var inside = TimeTicks.Where(t => Math.Log10(t.Seconds) >= xMin && Math.Log10(t.Seconds) <= xMax).ToList();
        int step = Math.Max(1, (int)Math.Ceiling(inside.Count / 7.0));
        for (int i = 0; i < inside.Count; i++)
            if (i % step == 0) tg.AddMajor(Math.Log10(inside[i].Seconds), inside[i].Label);
        for (int d = (int)Math.Floor(xMin); d <= (int)Math.Ceiling(xMax); d++)
            if (d >= xMin && d <= xMax) tg.AddMinor(d);
        return tg;
    }

    /// <summary>Powers of ten as 10ⁿ, every k-th decade so at most 6 are labelled.</summary>
    private static ScottPlot.TickGenerators.NumericManual DecadeTickGenerator(double yMin, double yMax)
    {
        var tg = new ScottPlot.TickGenerators.NumericManual();
        int lo = (int)Math.Ceiling(yMin), hi = (int)Math.Floor(yMax);
        if (hi - lo < 1)
        {
            // Less than two whole decades in view: label 1, 2 and 5 times each power of ten.
            for (int d = (int)Math.Floor(yMin); d <= (int)Math.Ceiling(yMax); d++)
            foreach (double k in new[] { 1.0, 2, 5 })
            {
                double y = d + Math.Log10(k);
                if (y >= yMin && y <= yMax) tg.AddMajor(y, ScaledPower(k, d));
            }
            return tg;
        }
        int step = Math.Max(1, (int)Math.Ceiling((hi - lo + 1) / 6.0));
        for (int d = lo; d <= hi; d++)
        {
            if ((d - lo) % step == 0) tg.AddMajor(d, PowerOfTen(d));
            else tg.AddMinor(d);
        }
        return tg;
    }

    /// <summary>A linear tick label: "0", plain figures, or 2×10⁻²¹ for very small or large values (the strain).</summary>
    private static string LinearLabel(double v)
    {
        if (v == 0) return "0";
        double a = Math.Abs(v);
        if (a >= 1e-3 && a < 1e5) return v.ToString("#,0.###", CultureInfo.InvariantCulture);
        int exp = (int)Math.Floor(Math.Log10(a));
        double mant = v / Math.Pow(10, exp);
        string m = Math.Round(mant, 1).ToString("0.#", CultureInfo.InvariantCulture);
        return m == "1" ? PowerOfTen(exp) : m == "-1" ? "−" + PowerOfTen(exp) : $"{m.Replace("-", "−")}×{PowerOfTen(exp)}";
    }

    /// <summary>k × 10^d as plain figures near 1 (0.002 to 5,000), otherwise as k×10ⁿ.</summary>
    private static string ScaledPower(double k, int d)
    {
        if (d >= -3 && d <= 3) return (k * Math.Pow(10, d)).ToString("#,0.###", CultureInfo.InvariantCulture);
        return k == 1 ? PowerOfTen(d) : $"{k:0}×{PowerOfTen(d)}";
    }

    private static string PowerOfTen(int n)
    {
        if (n == 0) return "1";
        if (n == 1) return "10";
        const string digits = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        var s = new System.Text.StringBuilder("10");
        if (n < 0) s.Append('⁻');
        foreach (char ch in Math.Abs(n).ToString(CultureInfo.InvariantCulture)) s.Append(digits[ch - '0']);
        return s.ToString();
    }

    // ---------------------------------------------------------------- the other views

    private static void DrawSpectrogram(Plot plot, InstrumentPanelViewModel vm)
    {
        plot.Axes.Bottom.Label.Text = "Observer time after core bounce (s)";
        if (vm.Spectrogram is not { } s)
        {
            plot.Axes.SetLimits(0, 2, 0, 2048);
            return;
        }
        int nt = s.Log10Power.GetLength(0), nf = s.Log10Power.GetLength(1);
        // ScottPlot draws row 0 at the top: rows are frequencies from the highest down, columns are times.
        var data = new double[nf, nt];
        for (int i = 0; i < nt; i++)
        for (int k = 0; k < nf; k++)
            data[nf - 1 - k, i] = s.Log10Power[i, k];
        var hm = plot.Add.Heatmap(data);
        hm.Colormap = new ScottPlot.Colormaps.Inferno();
        hm.Extent = new CoordinateRect(0, s.MaxObserverTimeS, 0, s.MaxFrequencyHz);
        hm.ManualRange = new ScottPlot.Range(-SpectrogramView.FloorDecades, 0);
        plot.Axes.SetLimits(0, s.MaxObserverTimeS, 0, s.MaxFrequencyHz);
    }

    private static void DrawSpectrum(Plot plot, InstrumentPanelViewModel vm, List<LegendEntry> legend)
    {
        plot.Axes.Bottom.Label.Text = "Rest-frame wavelength (Å)";
        // The last tick label is centred on 10,000 Å near the right edge, where the hidden right axis leaves it no room:
        // show that axis bare (no ticks, no frame) with a minimum width, as padding.
        plot.Axes.Right.IsVisible = true;
        plot.Axes.Right.TickGenerator = new ScottPlot.TickGenerators.NumericManual();
        plot.Axes.Right.FrameLineStyle.Width = 0;
        plot.Axes.Right.MinimumSize = RightPaddingPx;
        if (vm.Spectrum is not { } s)
        {
            plot.Axes.SetLimits(2850, 10150, 0, 1.1);
            return;
        }
        var line = plot.Add.ScatterLine(s.WavelengthsAngstrom, s.RelativeFlux);
        line.Color = Color.FromHex(Palette[0]);
        line.LineWidth = 1.5f;
        legend.Add(new LegendEntry("Schematic spectrum (relative flux)", Brush(Palette[0]), false, false));
        // Label the strongest lines at their blueshifted absorption, one row of short labels.
        double c = PhysicalConstants.C / 1e5;
        var strongest = s.Lines.OrderByDescending(l => l.Strength).GroupBy(l => ShortLabel(l.Label)).Select(g => g.First())
            .Take(7).ToList();
        int row = 0;
        foreach (var l in strongest.OrderBy(l => l.RestWavelengthAngstrom))
        {
            double x = l.RestWavelengthAngstrom * (1 - s.VelocityKmS / c);
            if (x < 3000 || x > 10000) continue;
            var tick = plot.Add.VerticalLine(x);
            tick.Color = Color.FromHex("#8792A8").WithAlpha(0.45);
            tick.LineWidth = 1;
            tick.LinePattern = LinePattern.Dotted;
            var text = plot.Add.Text(ShortLabel(l.Label), x, 1.18 - 0.07 * (row++ % 2));
            text.LabelFontSize = 9.5f;
            text.LabelFontColor = Color.FromHex("#D8DEE9");
            text.LabelAlignment = Alignment.UpperCenter;
        }
        // A little room either side, so the end labels (3,000 and 10,000 Å) are not cut.
        plot.Axes.SetLimits(2850, 10150, 0, 1.22);
    }

    /// <summary>Room kept right of the spectrum's data area for its last tick label, "10,000", px.</summary>
    private const float RightPaddingPx = 24;

    private static string ShortLabel(string label)
    {
        int space = label.LastIndexOf(' ');
        return space > 0 && double.TryParse(label[(space + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            ? label[..space]
            : label;
    }

    private static void DrawPulses(Plot plot, InstrumentPanelViewModel vm)
    {
        plot.Axes.Bottom.Label.Text = "Time from the first pulse (s)";
        plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericManual();
        if (vm.PulseTrain is not { } p || p.PulseOffsetsS.Count == 0)
        {
            plot.Axes.SetLimits(0, 1, 0, 1.1);
            return;
        }
        // A schematic pulse: a narrow triangle about 3% of the period wide (the duty cycle of a typical pulsar).
        double w = Math.Max(0.015 * p.PeriodS, 2e-4);
        var xs = new List<double> { 0 };
        var ys = new List<double> { 0 };
        foreach (double t in p.PulseOffsetsS)
        {
            xs.AddRange(new[] { t - w, t, t + w });
            ys.AddRange(new[] { 0.0, 1.0, 0.0 });
        }
        xs.Add(p.WindowS);
        ys.Add(0);
        var line = plot.Add.ScatterLine(xs.ToArray(), ys.ToArray());
        line.Color = Color.FromHex(Palette[1]);
        line.LineWidth = 1.2f;
        plot.Axes.SetLimits(-0.01, p.WindowS + 0.01, 0, 1.15);
    }

    public static System.Windows.Media.SolidColorBrush Brush(string hex, double alpha = 1)
    {
        var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        c.A = (byte)Math.Round(255 * alpha);
        var b = new System.Windows.Media.SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
