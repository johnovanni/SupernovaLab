using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Timeline;

namespace SupernovaLab.App.ViewModels;

/// <summary>
/// One tile of the instrument strip (spec §5.0): its panel's title, a sparkline of the panel's main series over the whole
/// timeline, the time cursor and the panel's current detection as a badge. Clicking it opens the Instruments window on
/// that panel. It reads the shared panel view model, so the two always agree.
/// </summary>
public sealed class InstrumentTileViewModel : ObservableObject
{
    public InstrumentTileViewModel(InstrumentPanelViewModel panel, IRelayCommand<Messenger?> showInstruments)
    {
        Panel = panel ?? throw new ArgumentNullException(nameof(panel));
        ArgumentNullException.ThrowIfNull(showInstruments);
        OpenCommand = new RelayCommand(() => showInstruments.Execute(panel.Messenger));
        panel.PropertyChanged += OnPanelChanged;
    }

    public InstrumentPanelViewModel Panel { get; }

    public Messenger Messenger => Panel.Messenger;
    public string Title => Panel.Title;

    /// <summary>The sparkline: the panel's main series (gravitational waves: strain; neutrinos: Super-K's rate; …).</summary>
    public IReadOnlyList<PlotSeries> MainSeries => Panel.MainSeries;

    /// <summary>Whether the sparkline runs brighter-up (the visible light's magnitudes).</summary>
    public bool InvertY => Panel.MainSeriesInvertY;

    /// <summary>The cursor's position 0..1 on the log timeline.</summary>
    public double CursorFraction => TimeAxis.ToFraction(Panel.CursorTimeS);

    /// <summary>The badge: the panel's current detection.</summary>
    public Detection Badge => Panel.CurrentDetection;

    public string BadgeText => Panel.DetectionShortText;

    /// <summary>The badge status for its colour; null when a model failed.</summary>
    public DetectionStatus? BadgeStatus => Panel.ErrorText is null ? Panel.CurrentDetection.Status : null;

    public string ToolTip =>
        $"{Title}: {Panel.DetectionText} ({Panel.SelectedInstrument.Name}).{Environment.NewLine}{Environment.NewLine}" +
        $"{Panel.ErrorText ?? Panel.CurrentDetection.Reason}{Environment.NewLine}{Environment.NewLine}Click to open the panel.";

    public IRelayCommand OpenCommand { get; }

    private void OnPanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(InstrumentPanelViewModel.MainSeries):
                OnPropertyChanged(nameof(MainSeries));
                break;
            case nameof(InstrumentPanelViewModel.CursorTimeS):
                OnPropertyChanged(nameof(CursorFraction));
                break;
            case nameof(InstrumentPanelViewModel.CurrentDetection):
            case nameof(InstrumentPanelViewModel.DetectionShortText):
            case nameof(InstrumentPanelViewModel.ErrorText):
            case nameof(InstrumentPanelViewModel.SelectedInstrument):
                OnPropertyChanged(nameof(Badge));
                OnPropertyChanged(nameof(BadgeText));
                OnPropertyChanged(nameof(BadgeStatus));
                OnPropertyChanged(nameof(ToolTip));
                break;
        }
    }
}
