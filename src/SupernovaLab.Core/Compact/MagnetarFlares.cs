using System.Globalization;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Compact;

/// <summary>
/// One giant flare of a magnetar (compact §C6): when it happens (source time, s after bounce), the spike's isotropic energy and
/// duration, and the peak luminosity (energy ÷ spike duration).
/// </summary>
public sealed record GiantFlare(double SourceTimeS, double EnergyErg, double PeakLuminosityErgS, double SpikeS);

/// <summary>
/// A magnetar's giant flares (spec §5, §7.3; compact §C6). They are rare display events, Poisson-drawn over the timeline from the
/// run number alone (Re-roll changes them; display randomness, not physics), at the per-magnetar rate the research derives from the
/// three seen so far (5 March 1979, 27 August 1998, 27 December 2004: Mereghetti 2008). Rate and energies are Speculative.
/// <para>The drawn flash (<see cref="DrawnIntensity"/>) is one rise and one fade, stretched to about half a second of playback at
/// the default speed, brightness capped at 1: a presentation of an event whose spike really lasts a third of a second
/// (flash safety, spec §1 criterion 6).</para>
/// </summary>
public static class MagnetarFlares
{
    /// <summary>
    /// §C6: 3 giant flares from the Galaxy and the LMC in the ≈ 47.7 yr since March 1979, among ≈ 30 known magnetars:
    /// 3 / (47.7 × 30) = 2.096 × 10⁻³ per magnetar per year (about 1 per 480 yr; Gehrels 1σ for n = 3: 0.96–4.1 × 10⁻³; Burns et
    /// al. 2021's extragalactic rate gives 1.27 × 10⁻³). Speculative.
    /// </summary>
    public static double RatePerYear { get; } = 3.0 / (47.7 * 30);

    /// <summary>§C6: the Gehrels 1σ range for 3 events, per magnetar per year.</summary>
    public const double RateLowPerYear = 0.96e-3, RateHighPerYear = 4.1e-3;

    /// <summary>§C6 App: the spike lasts 0.35 s (observed 0.25–0.5 s; Mereghetti 2008 Table 2).</summary>
    public const double SpikeDurationS = 0.35;

    /// <summary>§C6 App [APPROX]: the spike's energy is log-uniform over the observed range, 1.5 × 10⁴⁴ erg (1979, 1998) to 2 × 10⁴⁶ erg (2004 at 8.7 kpc).</summary>
    public const double EnergyLowErg = 1.5e44, EnergyHighErg = 2e46;

    /// <summary>The caption's comparison, the 2004 flare (Mereghetti 2008 Table 2).</summary>
    public const string Comparison2004 = "For comparison: SGR 1806−20's flare of 27 December 2004";

    /// <summary>The 2004 flare's numbers the comparison quotes (§C6; the later 8.7 kpc distance would make the spike about 2 × 10⁴⁶ erg).</summary>
    public const string Comparison2004Numbers = "a spike of (1.6–5) × 10⁴⁶ erg (isotropic) lasting 0.5 s, at 15 kpc";

    /// <summary>The source of every flare number (Speculative).</summary>
    public static Source FlareSource { get; } = new(
        "Mereghetti 2008 (the 1979, 1998 and 2004 giant flares); rate 3 flares / (47.7 yr × 30 magnetars) [DERIVED], Gehrels 1σ " +
        "0.96–4.1 × 10⁻³ per magnetar per year; Burns et al. 2021 cross-check 1.3 × 10⁻³; energy log-uniform over the observed range " +
        "[APPROX]; the observed magnetars are 10³–10⁵ yr old, so a newborn's rate is unknown",
        "compact §C6");

    // ---- The drawn flash (presentation): one rise and one fade, in decades of time, so its length in playback is the same at every time ----

    /// <summary>The timeline's default playback speed, decades of time per second (<c>TimelineViewModel.DefaultDecadesPerSecond</c>).</summary>
    public const double ReferenceDecadesPerSecond = 1.0 / 3;

    /// <summary>The drawn rise, decades of time before the flare: 0.06 decade, 0.18 s of default playback (at least 0.15 s).</summary>
    public const double RiseDecades = 0.06;

    /// <summary>The drawn fade, decades of time after the flare: 0.10 decade, 0.30 s of default playback (rise and fade about 0.5 s).</summary>
    public const double FadeDecades = 0.10;

    /// <summary>The rise's length in seconds of playback at the default speed.</summary>
    public static double RisePlaybackS => RiseDecades / ReferenceDecadesPerSecond;

    /// <summary>Whether the outcome leaves a magnetar (the only object that flares).</summary>
    public static bool Applies(Outcome o)
        => o.Fate == Fate.Supernova && o.Remnant.Kind == RemnantKind.Magnetar;

    /// <summary>
    /// The run's giant flares in time order (source time, s after bounce): a Poisson number with mean
    /// <see cref="RatePerYear"/> × the timeline's length, each uniform in time from the magnetosphere forming to the timeline's end
    /// and log-uniform in energy. Empty unless the outcome leaves a magnetar. A function of the run number alone.
    /// </summary>
    public static IReadOnlyList<GiantFlare> For(Outcome o, int runNumber)
    {
        if (!Applies(o)) return Array.Empty<GiantFlare>();
        double from = Magnetosphere.MagnetosphereFormsS(o), to = TimeAxis.MaxS;
        if (!(from < to)) from = 0;
        double mean = RatePerYear * (to - from) / PhysicalConstants.Year;
        // SplitMix64, its start scrambled once so that neighbouring runs' streams do not overlap (runs 1, 2, 3 … are unrelated).
        ulong state = Mix((ulong)(uint)runNumber * 0x9E3779B97F4A7C15UL + 0x4D41474E2545F491UL);
        int n = Poisson(ref state, mean);
        var flares = new List<GiantFlare>(n);
        double logRange = Math.Log(EnergyHighErg / EnergyLowErg);
        for (int i = 0; i < n; i++)
        {
            double t = from + Next(ref state) * (to - from);
            double e = EnergyLowErg * Math.Exp(Next(ref state) * logRange);
            flares.Add(new GiantFlare(t, e, e / SpikeDurationS, SpikeDurationS));
        }
        flares.Sort((a, b) => a.SourceTimeS.CompareTo(b.SourceTimeS));
        return flares;
    }

    /// <summary>
    /// The drawn brightness, 0 to 1, of one flare at this source time: zero before the rise, a smooth single rise over
    /// <see cref="RiseDecades"/> to the peak at the flare's time, then a smooth single fade over <see cref="FadeDecades"/>. The peak
    /// grows with the energy, from 0.5 (the faintest observed) to 1 (the 2004 flare's), capped at 1.
    /// </summary>
    public static double DrawnIntensity(GiantFlare f, double sourceTimeS)
    {
        if (!(f.SourceTimeS > 0) || !(sourceTimeS > 0)) return 0;
        double u = Math.Log10(sourceTimeS / f.SourceTimeS);
        if (u <= -RiseDecades || u >= FadeDecades) return 0;
        double shape;
        if (u < 0)
        {
            double s = u / RiseDecades + 1;
            shape = s * s * (3 - 2 * s);
        }
        else
        {
            double s = u / FadeDecades;
            shape = 1 - s * s * (3 - 2 * s);
        }
        return Math.Min(1, PeakBrightness(f) * shape);
    }

    /// <summary>The drawn peak, 0.5 to 1, by where the energy lies in the observed range (log).</summary>
    public static double PeakBrightness(GiantFlare f)
    {
        double x = Math.Log(Math.Max(f.EnergyErg, EnergyLowErg) / EnergyLowErg) / Math.Log(EnergyHighErg / EnergyLowErg);
        return Math.Min(1, 0.5 + 0.5 * x);
    }

    /// <summary>The brightest drawn flare of the list at this source time (0 when none is on).</summary>
    public static double IntensityAt(IReadOnlyList<GiantFlare> flares, double sourceTimeS)
    {
        double best = 0;
        foreach (var f in flares) best = Math.Max(best, DrawnIntensity(f, sourceTimeS));
        return best;
    }

    /// <summary>The flare that is on at this source time (the brightest), or null.</summary>
    public static GiantFlare? Active(IReadOnlyList<GiantFlare> flares, double sourceTimeS)
    {
        GiantFlare? best = null;
        double bestI = 0;
        foreach (var f in flares)
        {
            double i = DrawnIntensity(f, sourceTimeS);
            if (i > bestI) { bestI = i; best = f; }
        }
        return best;
    }

    /// <summary>The note while a flare is on: its energy and spike, that the drawing is stretched, and the 2004 comparison.</summary>
    public static string Note(GiantFlare f)
    {
        var inv = CultureInfo.InvariantCulture;
        return $"Giant flare (speculative): a spike of {UnitFormat.Energy(f.EnergyErg)} (isotropic) lasting {f.SpikeS.ToString("0.00", inv)} s, drawn as " +
               $"one flash of about half a second of playback. {Comparison2004}: {Comparison2004Numbers}";
    }

    /// <summary>The next uniform number in [0, 1) (SplitMix64; the top 53 bits), as <c>KickDrift</c> draws the kick.</summary>
    private static double Next(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        return (Mix(state) >> 11) * (1.0 / (1UL << 53));
    }

    private static ulong Mix(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>A Poisson draw by multiplying uniforms (Knuth), exact for the small means used here.</summary>
    private static int Poisson(ref ulong state, double mean)
    {
        double limit = Math.Exp(-mean), p = 1;
        int k = 0;
        do
        {
            k++;
            p *= Next(ref state);
        } while (p > limit && k < 1000);
        return k - 1;
    }
}
