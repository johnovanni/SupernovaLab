using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Structure;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Core.Units;

namespace SupernovaLab.App.Tour;

/// <summary>
/// Builds the guided tour of an explosion (Project 2, Task 12) from the outcome's own events (<see cref="EventMarkers"/>)
/// and the preset's history lines, all in OBSERVER time (ruling R-P2-6).
/// <list type="bullet">
/// <item>The first stop is at 1 ms (<see cref="TimeAxis.MinS"/>): "Core collapse", "Ignition" for a Type Ia, or the star's
/// own fate when there is no explosion.</item>
/// <item>Each event marker is a stop. A stop before the shock breakout is a cutaway whose camera sways (Task 15). The breakout
/// highlights X-rays/UV, the neutrino burst the neutrinos, peak light the visible light (the gravitational waves, gamma-ray
/// burst and lines, radio peak and pulsar highlight their own panels). A peak-light marker before the breakout is left out
/// (ruling R-P2-33, final review I10: Project 1's IIn light curve peaks before its shock reaches the surface, inconsistency #51).</item>
/// <item>The mixing (ruling R-P2-33, final review I7): when the debris have Rayleigh–Taylor fingers, one stop after the breakout,
/// at peak light (or 30 days when there is no peak marker), is a cutaway with the sway and says what the slice shows. It
/// merges like any stop (so it is usually the peak-light stop itself, now cut away).</item>
/// <item>A preset line becomes a stop at its source time × (1 + z). Lines outside the timeline are left out.</item>
/// <item>Merging: stops are sorted by time; a stop less than 0.05 decades after the earliest stop of its group (measured
/// from that stop, so a chain cannot drift) joins the group. (Each stop's Notes carry the source and confidence of every caption sentence; history sentences are prefixed "Observed:" and model sentences "In the lab," or, for a signal never observed from any supernova, "Expected:".) The group keeps the earliest time and that stop's cutaway
/// flag, the first non-null highlight, and its captions joined with a space in time order.</item>
/// </list>
/// Every stop dwells for <see cref="DwellS"/> seconds.
/// </summary>
public static class TourBuilder
{
    public const double DwellS = 4;

    /// <summary>Two stops closer than this many decades (log10 of the time ratio) are one stop.</summary>
    public const double MergeDecades = 0.05;

    /// <summary>The mixing stop's time when the outcome has no peak-light marker after its breakout (ruling R-P2-33): 30 days.</summary>
    public const double MixingFallbackSourceS = 30 * PhysicalConstants.Day;

    /// <summary>A stop to be: <paramref name="Cutaway"/> forces the cutaway (the mixing stop); otherwise only a pre-breakout stop has it.</summary>
    private sealed record Candidate(double TimeS, string Caption, Messenger? Highlight, TourSourceNote? Note = null, bool Cutaway = false, bool CloseUp = false);

    /// <summary>The bounce stop's source time: 10 ms, the Project 2 neutrino-burst and gravitational-wave marker time (the lab's start of the signal).</summary>
    public const double BounceStopSourceS = 0.01;

    private static readonly PulsarModel Pulsar = new();

    private static readonly Source CrustSource = new(
        "The crust crystallises about 1 day after bounce, the same for every neutron star (range 1 hour to 1 month) [D/APPROX]; Zdunik, Fortin & Haensel 2017 for the layers",
        "compact §C5");

    private static readonly Source PulseSource = new(
        "Pulses get out of the thinning ejecta after about 10 years (research: 10–30 yr) [APPROX]; the beam's reach from the lab's geometry", "signals §8.3");

    /// <summary>
    /// The Close-up's stops (Project 3, Task 13; spec §2.4), each at its model's time × (1 + z): bounce, stall, revival or black-hole
    /// formation (the core's phase transitions in <see cref="CoreCollapseHistory"/>), crust forms, black hole settles, pulses emerge,
    /// and the run's first giant flare (only when <paramref name="runNumber"/> is given and the run has one). None for an outcome with
    /// no neutron star, magnetar or black hole.
    /// </summary>
    private static IEnumerable<Candidate> CloseUpCandidates(Outcome o, ObserverSetup obs, int? runNumber)
    {
        if (CoreCollapseHistory.TrackFor(o) is null) yield break;
        double z1 = obs.TimeDilation;
        bool InRange(double t) => double.IsFinite(t) && t >= TimeAxis.MinS && t <= TimeAxis.MaxS;
        Candidate Make(double t, string caption, Source source) => new(t, caption, null, new TourSourceNote(TourSourceKind.LabModel, source), CloseUp: true);

        // 10 ms after bounce (where the neutrino and gravitational-wave markers sit, so it usually shares their stop): the shock is born.
        double bounce = BounceStopSourceS * z1;
        yield return Make(bounce, TourCaptions.CloseUpBounce(o, obs, bounce), CoreCollapseHistory.TrackSource);

        double stall = CoreCollapseHistory.StalledFromS * z1;
        if (InRange(stall) && CoreCollapseHistory.At(o, CoreCollapseHistory.StalledFromS).Phase == CorePhase.Stalled)
            yield return Make(stall, TourCaptions.CloseUpStall(o, obs, stall), CoreCollapseHistory.TrackSource);

        double revival = CoreCollapseHistory.RevivalTimeS(o) * z1;
        if (InRange(revival) && CoreCollapseHistory.At(o, CoreCollapseHistory.RevivalTimeS(o)).Phase == CorePhase.Revived)
            yield return Make(revival, TourCaptions.CloseUpRevival(o, obs, revival), CoreCollapseHistory.TrackSource);

        double bh = CoreCollapseHistory.BlackHoleFormsS(o) * z1;
        if (InRange(bh))
            yield return Make(bh, TourCaptions.CloseUpBlackHole(o, obs, bh), CoreCollapseHistory.TrackSource);

        if (o.Remnant.Kind is RemnantKind.NeutronStar or RemnantKind.Magnetar)
        {
            double crust = NeutronStarLayers.CrustFormationTimeS(o) * z1;
            if (InRange(crust)) yield return Make(crust, TourCaptions.CloseUpCrust(crust), CrustSource);
            double pulses = Pulsar.EmergenceTimeS(o) * z1;
            if (InRange(pulses)) yield return Make(pulses, TourCaptions.CloseUpPulses(o, obs, pulses), PulseSource);
        }

        if (o.Remnant.Kind == RemnantKind.BlackHole)
        {
            double settled = BlackHoleModel.SettleEndS(o) * z1;
            if (InRange(settled) && BlackHoleModel.SettleEndS(o) > 0)
                yield return Make(settled, TourCaptions.CloseUpSettled(o, settled), BlackHoleModel.SpinSource);
        }

        // The run's first giant flare (one stop; a run may have several, the timeline's markers show them all).
        if (runNumber is int run && MagnetarFlares.For(o, run) is { Count: > 0 } flares)
        {
            double t = flares[0].SourceTimeS * z1;
            if (InRange(t)) yield return Make(t, TourCaptions.CloseUpFlare(flares[0], t), MagnetarFlares.FlareSource);
        }
    }

    public static IReadOnlyList<TourStop> For(Outcome o, ObserverSetup obs, Preset? preset, int? runNumber = null)
    {
        ArgumentNullException.ThrowIfNull(o);
        ArgumentNullException.ThrowIfNull(obs);
        var all = new List<Candidate> { new(TimeAxis.MinS, TourCaptions.Begin(o), null) };

        double breakout = TourCaptions.BreakoutObserverTimeS(o, obs);
        bool hasBreakout = double.IsFinite(breakout) && breakout > 0;
        double peakS = double.NaN;
        foreach (var m in EventMarkers.Guarded(() => EventMarkers.For(o, obs)))
        {
            bool peak = m.GlossaryKey == nameof(Glossary.PeakBrightness);
            // Ruling R-P2-33 (I10): no peak-light stop before the shock has reached the surface (a IIn's light curve, #51).
            if (peak && hasBreakout && m.ObserverTimeS < breakout) continue;
            if (peak) peakS = m.ObserverTimeS;
            all.Add(new Candidate(m.ObserverTimeS, TourCaptions.ForMarker(m, o, obs), TourCaptions.HighlightFor(m.GlossaryKey),
                new TourSourceNote(TourSourceKind.LabModel, new Source(m.Basis, m.Label))));
        }

        // Ruling R-P2-33 (I7): the mixing fingers, cut away, at peak light (or 30 days), after the breakout.
        if (Mixing(o) is { } mixing)
        {
            double t = double.IsFinite(peakS) ? peakS : MixingFallbackSourceS * obs.TimeDilation;
            if (!hasBreakout || t > breakout)
                all.Add(new Candidate(t, TourCaptions.Mixing(o, mixing), null,
                    new TourSourceNote(TourSourceKind.LabModel, mixing.Source), Cutaway: true));
        }

        // Project 3, Task 13 (spec §2.4): the Close-up's stops, for an outcome with a compact remnant.
        all.AddRange(CloseUpCandidates(o, obs, runNumber));

        if (preset is not null)
        {
            double breakoutSource = EjectaKinematics.BreakoutTimeS(o);
            foreach (var line in preset.TourLines)
            {
                // A line given as a delay after first light is placed at the lab's own breakout plus that delay (R-P2-31).
                double source = line.SourceTimeS + (line.FromBreakout && double.IsFinite(breakoutSource) && breakoutSource > 0 ? breakoutSource : 0);
                double t = source * obs.TimeDilation;
                if (double.IsFinite(t) && t > TimeAxis.MinS && t <= TimeAxis.MaxS)
                    all.Add(new Candidate(t, TourCaptions.Observed(line.Text, line.Recalled), line.Highlight,
                        new TourSourceNote(line.Recalled ? TourSourceKind.ObservedRecalled : TourSourceKind.ObservedChecked, line.Source)));
            }
        }

        // OrderBy is stable: the first stop stays first, and at equal times a marker comes before a preset line.
        var sorted = all.OrderBy(c => c.TimeS).ToList();

        var stops = new List<TourStop>();
        int i = 0;
        while (i < sorted.Count)
        {
            var anchor = sorted[i];
            var group = new List<Candidate> { anchor };
            int j = i + 1;
            while (j < sorted.Count && Math.Log10(sorted[j].TimeS / anchor.TimeS) < MergeDecades) group.Add(sorted[j++]);
            i = j;

            bool cutaway = anchor.TimeS >= TimeAxis.MinS && hasBreakout && anchor.TimeS < breakout || group.Any(g => g.Cutaway);
            stops.Add(new TourStop(anchor.TimeS, string.Join(" ", group.Select(g => g.Caption)), cutaway, DwellS,
                cutaway, group.Select(g => g.Highlight).FirstOrDefault(h => h is not null), group.Any(g => g.CloseUp))
            {
                Notes = group.Where(g => g.Note is not null).Select(g => g.Note!).ToList(),
                CloseUpOnly = group.All(g => g.CloseUp),
            });
        }
        return stops;
    }

    /// <summary>The debris' mixing when they have Rayleigh–Taylor fingers (not a failed supernova's shell); null otherwise.</summary>
    private static MixingReach? Mixing(Outcome o)
    {
        try
        {
            return EjectaStructureModel.For(o) is { IsEnvelopeShell: false, Mixing: { HasFingers: true } m } ? m : null;
        }
        catch (Exception e)
        {
            System.Diagnostics.Trace.TraceError($"Tour mixing stop: {e}");
            return null;
        }
    }
}
