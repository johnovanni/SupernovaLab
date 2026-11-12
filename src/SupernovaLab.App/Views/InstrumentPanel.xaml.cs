using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using SupernovaLab.App.ViewModels;

namespace SupernovaLab.App.Views;

/// <summary>
/// One instrument panel (spec §5.1). DataContext: an <see cref="InstrumentPanelViewModel"/>. The plot is redrawn only
/// when the view model resamples or changes its view; the "now" cursor is a WPF line placed over the plot from the
/// last render's axes, so the time can move at playback rate without re-rendering ScottPlot.
/// </summary>
public partial class InstrumentPanel : System.Windows.Controls.UserControl
{
    private InstrumentPanelViewModel? _vm;
    private bool _redrawQueued;

    public InstrumentPanel()
    {
        InitializeComponent();
        PanelPlot.Style(Plot.Plot);
        // Double-click maximizes the panel in the Instruments window, so ScottPlot's double-click benchmark is off.
        Plot.UserInputProcessor.DoubleLeftClickBenchmark(false);
        Plot.Plot.RenderManager.RenderFinished += (_, _) => Dispatcher.BeginInvoke(PlaceCursor, DispatcherPriority.Render);
        // The panel view models live as long as the app; a view listens to one only while it is loaded, so a closed
        // Instruments window's views are released and stop redrawing (review I3).
        DataContextChanged += (_, _) => { if (IsLoaded) Attach(); };
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        SizeChanged += (_, _) => PlaceCursor();
    }

    /// <summary>Listens to the current DataContext's panel (and only it), and redraws.</summary>
    private void Attach()
    {
        Detach();
        _vm = DataContext as InstrumentPanelViewModel;
        if (_vm is null) return;
        _vm.PropertyChanged += OnViewModelChanged;
        QueueRedraw();
    }

    /// <summary>Stops listening (the view was unloaded, or its window closed).</summary>
    public void Detach()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnViewModelChanged;
        _vm = null;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(InstrumentPanelViewModel.CursorTimeS):
                PlaceCursor();
                break;
            case nameof(InstrumentPanelViewModel.Series):
            case nameof(InstrumentPanelViewModel.Bands):
            case nameof(InstrumentPanelViewModel.ReferenceLines):
            case nameof(InstrumentPanelViewModel.FactMarkers):
            case nameof(InstrumentPanelViewModel.Kind):
            case nameof(InstrumentPanelViewModel.Spectrogram):
            case nameof(InstrumentPanelViewModel.Spectrum):
            case nameof(InstrumentPanelViewModel.PulseTrain):
            case nameof(InstrumentPanelViewModel.YLabel):
            case nameof(InstrumentPanelViewModel.LogY):
            case nameof(InstrumentPanelViewModel.InvertY):
                QueueRedraw();
                break;
        }
    }

    /// <summary>One redraw for the burst of property changes a resample raises.</summary>
    private void QueueRedraw()
    {
        if (_redrawQueued) return;
        _redrawQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _redrawQueued = false;
            Redraw();
        }, DispatcherPriority.Background);
    }

    /// <summary>Redraws the plot now (the --snapshot diagnostic calls it so a capture never waits on a queued redraw).</summary>
    public void Redraw()
    {
        if (_vm is null) return;
        Legend.ItemsSource = PanelPlot.Draw(Plot.Plot, _vm);
        Plot.Refresh();
    }

    /// <summary>Places the cursor line over the plot's data area, or an arrow at its edge when "now" is off the view.</summary>
    private void PlaceCursor()
    {
        CursorLine.Visibility = Visibility.Collapsed;
        CursorEdge.Visibility = Visibility.Collapsed;
        if (_vm is null || PanelPlot.CursorX(_vm, _vm.CursorTimeS) is not double x) return;
        var render = Plot.Plot.RenderManager.LastRender;
        var rect = render.DataRect;
        if (!(rect.Width > 0) || !(rect.Height > 0)) return;
        double scale = Plot.DisplayScale > 0 ? Plot.DisplayScale : 1;
        double px = Plot.Plot.GetPixel(new ScottPlot.Coordinates(x, 0)).X;
        double left = rect.Left / scale, right = rect.Right / scale, top = rect.Top / scale, bottom = rect.Bottom / scale;
        double dip = px / scale;
        if (double.IsNaN(dip)) return;
        if (dip < left || dip > right)
        {
            CursorEdge.Text = dip < left ? "◂ now" : "now ▸";
            System.Windows.Controls.Canvas.SetLeft(CursorEdge, dip < left ? left + 3 : right - 38);
            System.Windows.Controls.Canvas.SetTop(CursorEdge, top + 2);
            CursorEdge.Visibility = Visibility.Visible;
            return;
        }
        CursorLine.X1 = CursorLine.X2 = dip;
        CursorLine.Y1 = top;
        CursorLine.Y2 = bottom;
        CursorLine.Visibility = Visibility.Visible;
    }
}
