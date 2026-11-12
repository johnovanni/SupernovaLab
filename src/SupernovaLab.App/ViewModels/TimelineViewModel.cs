using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Core.Units;

namespace SupernovaLab.App.ViewModels;

/// <summary>A labelled tick under the scrubber, at a position 0..1 on the log axis.</summary>
public sealed record AxisLabel(string Text, double Fraction);

/// <summary>A marker's label placed above the scrubber: its row (0–2), left edge and width in pixels.</summary>
public sealed record MarkerLabel(EventMarker Marker, int Row, double Left, double Width)
{
    public double Top => Row * TimelineViewModel.MarkerRowHeight;
}

/// <summary>
/// The timeline (spec §4): observer time on a log axis from 1 ms to 1,000 years, playback at a speed in decades per
/// second, event markers, and the "light left … ago" line. Changing the outcome never moves the time (Review Focus 4).
/// </summary>
public sealed partial class TimelineViewModel : ObservableObject
{
    public const double MinDecadesPerSecond = 0.1, MaxDecadesPerSecond = 3, DefaultDecadesPerSecond = 1.0 / 3;

    /// <summary>Marker labels: three rows this far apart (px), each label about this wide per character (10.5 px text).</summary>
    public const double MarkerRowHeight = 13, MarkerCharWidth = 5.6, MarkerLabelGap = 8;
    public const int MarkerRows = 3;

    /// <summary>Which labels win when the timeline is crowded (most important first); a label that fits nowhere is hidden.</summary>
    private static readonly string[] LabelPriority =
    {
        "PeakBrightness", "NeutrinoBurst", "GammaRayBurst", "ShockBreakout", "BlackHoleForms", "PlateauEnd",
        "RadioPeak", "GammaRayLines", "GravitationalWaves", "PulsarEmerges", "RemnantResolvable",
    };

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly double AxisDecades = Math.Log10(TimeAxis.MaxS / TimeAxis.MinS);

    private double _observerTimeS = TimeAxis.MinS;

    /// <summary>Observer time, s, held on the axis (1 ms to 1,000 yr).</summary>
    public double ObserverTimeS
    {
        get => _observerTimeS;
        set
        {
            double t = double.IsNaN(value) ? TimeAxis.MinS : Math.Clamp(value, TimeAxis.MinS, TimeAxis.MaxS);
            if (SetProperty(ref _observerTimeS, t))
            {
                OnPropertyChanged(nameof(ScrubFraction));
                OnPropertyChanged(nameof(ObserverTimeText));
            }
        }
    }

    /// <summary>The scrubber's position 0..1 on the log axis.</summary>
    public double ScrubFraction
    {
        get => TimeAxis.ToFraction(ObserverTimeS);
        set => ObserverTimeS = TimeAxis.FromFraction(value);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseText))]
    private bool _isPlaying;

    public string PlayPauseText => IsPlaying ? "Pause" : "Play";

    private double _decadesPerSecond = DefaultDecadesPerSecond;

    /// <summary>Playback speed in decades of time per second (0.1–3; default 1/3, about 40 s for the whole axis).</summary>
    public double DecadesPerSecond
    {
        get => _decadesPerSecond;
        set
        {
            if (SetProperty(ref _decadesPerSecond, Math.Clamp(value, MinDecadesPerSecond, MaxDecadesPerSecond)))
                OnPropertyChanged(nameof(SpeedText));
        }
    }

    /// <summary>"0.33 decades/s (whole timeline in 41 s)".</summary>
    public string SpeedText =>
        $"{DecadesPerSecond.ToString("0.##", Inv)} decades/s (whole timeline in {(AxisDecades / DecadesPerSecond).ToString("0", Inv)} s)";

    private IReadOnlyList<EventMarker> _markers = Array.Empty<EventMarker>();

    /// <summary>The event markers for the current outcome and observer.</summary>
    public IReadOnlyList<EventMarker> Markers
    {
        get => _markers;
        private set
        {
            if (SetProperty(ref _markers, value)) LayoutMarkerLabels(_trackWidth);
        }
    }

    private double _trackWidth;
    private IReadOnlyList<MarkerLabel> _markerLabels = Array.Empty<MarkerLabel>();

    /// <summary>The markers' labels as laid out for the last track width; every marker keeps its tick either way.</summary>
    public IReadOnlyList<MarkerLabel> MarkerLabels
    {
        get => _markerLabels;
        private set => SetProperty(ref _markerLabels, value);
    }

    /// <summary>
    /// Places the marker labels over a track this many pixels wide: each label is centered on its marker (held inside
    /// the track) in the first of three rows where it overlaps no other label, in order of importance. A label with no
    /// free row is hidden; its marker's tick and tooltip remain.
    /// </summary>
    public void LayoutMarkerLabels(double trackWidth)
    {
        _trackWidth = trackWidth;
        if (!(trackWidth > 0))
        {
            MarkerLabels = Array.Empty<MarkerLabel>();
            return;
        }
        var rows = Enumerable.Range(0, MarkerRows).Select(_ => new List<(double From, double To)>()).ToArray();
        var placed = new List<MarkerLabel>();
        foreach (var m in Markers.OrderBy(m => Priority(m.GlossaryKey)).ThenBy(m => m.ObserverTimeS))
        {
            double width = Math.Min(trackWidth, m.Label.Length * MarkerCharWidth + 6);
            double left = Math.Clamp(m.Fraction * trackWidth - width / 2, 0, trackWidth - width);
            for (int r = 0; r < MarkerRows; r++)
            {
                if (rows[r].Any(x => left < x.To + MarkerLabelGap && x.From < left + width + MarkerLabelGap)) continue;
                rows[r].Add((left, left + width));
                placed.Add(new MarkerLabel(m, r, left, width));
                break;
            }
        }
        MarkerLabels = placed;
    }

    private static int Priority(string key)
    {
        int i = Array.IndexOf(LabelPriority, key);
        return i < 0 ? LabelPriority.Length : i;
    }

    /// <summary>Ticks under the scrubber at round times.</summary>
    public IReadOnlyList<AxisLabel> AxisLabels { get; } = new[]
    {
        new AxisLabel("1 ms", TimeAxis.ToFraction(1e-3)),
        new AxisLabel("1 s", TimeAxis.ToFraction(1)),
        new AxisLabel("1 min", TimeAxis.ToFraction(60)),
        new AxisLabel("1 h", TimeAxis.ToFraction(3600)),
        new AxisLabel("1 day", TimeAxis.ToFraction(PhysicalConstants.Day)),
        new AxisLabel("1 month", TimeAxis.ToFraction(30 * PhysicalConstants.Day)),
        new AxisLabel("1 yr", TimeAxis.ToFraction(PhysicalConstants.Year)),
        new AxisLabel("10 yr", TimeAxis.ToFraction(10 * PhysicalConstants.Year)),
        new AxisLabel("100 yr", TimeAxis.ToFraction(100 * PhysicalConstants.Year)),
        new AxisLabel("1,000 yr", TimeAxis.ToFraction(TimeAxis.MaxS)),
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ObserverTimeText))]
    private string _origin = "core bounce";

    /// <summary>"3.2 days after core bounce" in observer time (spec §4; UnitFormat.Time).</summary>
    public string ObserverTimeText => $"{UnitFormat.Time(ObserverTimeS)} after {Origin}";

    [ObservableProperty] private string _lightLeftText = "";

    public IRelayCommand PlayPauseCommand { get; }
    public IRelayCommand<EventMarker> JumpToMarkerCommand { get; }

    public TimelineViewModel()
    {
        PlayPauseCommand = new RelayCommand(() =>
        {
            if (!IsPlaying && ObserverTimeS >= TimeAxis.MaxS) ObserverTimeS = TimeAxis.MinS;
            IsPlaying = !IsPlaying;
        });
        JumpToMarkerCommand = new RelayCommand<EventMarker>(m =>
        {
            if (m is not null) ObserverTimeS = m.ObserverTimeS;
        });
    }

    /// <summary>
    /// Advances playback by this much wall-clock time: the observer time is multiplied by 10^(speed × seconds). Playback
    /// stops at the end of the axis. Does nothing while paused.
    /// </summary>
    public void Tick(TimeSpan elapsed)
    {
        if (!IsPlaying || elapsed <= TimeSpan.Zero) return;
        double t = ObserverTimeS * Math.Pow(10, DecadesPerSecond * elapsed.TotalSeconds);
        if (t >= TimeAxis.MaxS)
        {
            ObserverTimeS = TimeAxis.MaxS;
            IsPlaying = false;
            return;
        }
        ObserverTimeS = t;
    }

    /// <summary>Called by MainViewModel when the outcome or observer changes. The time itself is kept.</summary>
    internal void Update(IReadOnlyList<EventMarker> markers, ObserverSetup observer, string origin)
    {
        Markers = markers;
        Origin = origin;
        LightLeftText = $"Light from this moment left the star {LightTravel(observer)} before it reached Earth.";
    }

    /// <summary>
    /// The light travel time: distance ÷ c, or above 50 Mpc (z &gt; 0) the lookback time of the flat ΛCDM cosmology.
    /// "163,000 years", "3.05 billion years".
    /// </summary>
    private static string LightTravel(ObserverSetup observer)
    {
        double z = observer.Redshift;
        double years = z > 0 ? CosmologyModel.Default.LookbackTimeGyr(z) * 1e9 : observer.LightTravelYears;
        if (years >= 1e9) return $"{UnitFormat.Number(years / 1e9)} billion years";
        if (years >= 1e6) return $"{UnitFormat.Number(years / 1e6)} million years";
        return UnitFormat.Time(years * PhysicalConstants.Year);
    }
}
