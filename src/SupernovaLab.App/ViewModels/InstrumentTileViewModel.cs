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
public sealed partial class InstrumentTileViewModel : ObservableObject
{
    public InstrumentTileViewModel(InstrumentPanelViewModel panel, IRelayCommand<Messenger?> showInstruments)
    {
        Panel = panel ?? throw new ArgumentNullException(nameof(panel));
        ArgumentNullException.ThrowIfNull(showInstruments);
        OpenCommand = new RelayCommand(() => showInstruments.Execute(panel.Messenger));
        panel.PropertyChanged += OnPanelChanged;
    }

    public InstrumentPanelViewModel Panel { get; }

    /// <summary>Whether the guided tour is pointing at this tile's instrument (Task 12): the tile's border is 2 px in the accent brush.</summary>
    [ObservableProperty] private bool _isHighlighted;

    public Messenger Messenger => Panel.Messenger;
    public string Title => Panel.Title;

    /// <summary>The sparkline: the panel's main series (gravitational waves: strain; neutrinos: Super-K's rate; …).</summary>
    public IReadOnlyList<PlotSeries> MainSeries => Panel.MainSeries;

    /// <summary>Whether the sparkline runs brighter-up (the visible light's magnitudes).</summary>
    public bool InvertY => Panel.MainSeriesInvertY;

    /// <summary>The cursor's position 0..1 on the log timeline.</summary>
    public double CursorFraction => TimeAxis.ToFraction(Panel.CursorTimeS);

    /// <summary>
    /// The badge (ruling R50): the panel's best current-time verdict for its selected instrument over all its signals,
    /// which may differ from the panel's own badge (that one speaks only of the selected sub-tab).
    /// </summary>
    public Detection Badge => Panel.StripDetection;

    public string BadgeText => Panel.StripBadgeText;

    /// <summary>The badge status for its colour; null when a model failed.</summary>
    public DetectionStatus? BadgeStatus => Panel.ErrorText is null ? Panel.StripDetection.Status : null;

    public string ToolTip => Panel.StripToolTip;

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
            case nameof(InstrumentPanelViewModel.StripDetection):
            case nameof(InstrumentPanelViewModel.StripBadgeText):
            case nameof(InstrumentPanelViewModel.StripToolTip):
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
