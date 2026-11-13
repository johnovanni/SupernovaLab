using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Timeline;

namespace SupernovaLab.App.Tour;

/// <summary>
/// Builds the guided tour of an explosion (Project 2, Task 12) from the outcome's own events (<see cref="EventMarkers"/>)
/// and the preset's history lines, all in OBSERVER time (ruling R-P2-6).
/// <list type="bullet">
/// <item>The first stop is at 1 ms (<see cref="TimeAxis.MinS"/>): "Core collapse", "Ignition" for a Type Ia, or the star's
/// own fate when there is no explosion.</item>
/// <item>Each event marker is a stop. A stop before the shock breakout is a cutaway with a 10°/s orbit. The breakout
/// highlights X-rays/UV, the neutrino burst the neutrinos, peak light the visible light (the gravitational waves, gamma-ray
/// burst and lines, radio peak and pulsar highlight their own panels).</item>
/// <item>A preset line becomes a stop at its source time × (1 + z). Lines outside the timeline are left out.</item>
/// <item>Merging: stops are sorted by time; a stop less than 0.05 decades after the earliest stop of its group (measured
/// from that stop, so a chain cannot drift) joins the group. The group keeps the earliest time and that stop's cutaway
/// flag, the first non-null highlight, and its captions joined with a space in time order.</item>
/// </list>
/// Every stop dwells for <see cref="DwellS"/> seconds.
/// </summary>
public static class TourBuilder
{
    public const double DwellS = 4;
    public const double CutawayOrbitDegPerS = 10;

    /// <summary>Two stops closer than this many decades (log10 of the time ratio) are one stop.</summary>
    public const double MergeDecades = 0.05;

    private sealed record Candidate(double TimeS, string Caption, Messenger? Highlight);

    public static IReadOnlyList<TourStop> For(Outcome o, ObserverSetup obs, Preset? preset)
    {
        ArgumentNullException.ThrowIfNull(o);
        ArgumentNullException.ThrowIfNull(obs);
        var all = new List<Candidate> { new(TimeAxis.MinS, TourCaptions.Begin(o), null) };

        foreach (var m in EventMarkers.Guarded(() => EventMarkers.For(o, obs)))
            all.Add(new Candidate(m.ObserverTimeS, TourCaptions.ForMarker(m, o, obs), TourCaptions.HighlightFor(m.GlossaryKey)));

        if (preset is not null)
        {
            foreach (var line in preset.TourLines)
            {
                double t = line.SourceTimeS * obs.TimeDilation;
                if (double.IsFinite(t) && t > TimeAxis.MinS && t <= TimeAxis.MaxS)
                    all.Add(new Candidate(t, line.Text, line.Highlight));
            }
        }

        // OrderBy is stable: the first stop stays first, and at equal times a marker comes before a preset line.
        var sorted = all.OrderBy(c => c.TimeS).ToList();

        double breakout = TourCaptions.BreakoutObserverTimeS(o, obs);
        var stops = new List<TourStop>();
        int i = 0;
        while (i < sorted.Count)
        {
            var anchor = sorted[i];
            var group = new List<Candidate> { anchor };
            int j = i + 1;
            while (j < sorted.Count && Math.Log10(sorted[j].TimeS / anchor.TimeS) < MergeDecades) group.Add(sorted[j++]);
            i = j;

            bool cutaway = anchor.TimeS >= TimeAxis.MinS && double.IsFinite(breakout) && breakout > 0 && anchor.TimeS < breakout;
            stops.Add(new TourStop(anchor.TimeS, string.Join(" ", group.Select(g => g.Caption)), cutaway, DwellS,
                cutaway ? CutawayOrbitDegPerS : 0, group.Select(g => g.Highlight).FirstOrDefault(h => h is not null)));
        }
        return stops;
    }
}
