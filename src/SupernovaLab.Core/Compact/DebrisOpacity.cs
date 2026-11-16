using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Compact;

/// <summary>
/// Task 20 (inconsistency #71, the owner's question 2026-11-16): how opaque the expanding debris is, seen from the remnant at its centre.
/// The camera looks out through the whole ejecta column, which stays opaque long after the shock breaks out of the star's surface.
/// <list type="bullet">
/// <item>Opaque (veil 1) from breakout until <see cref="OpaqueUntilS"/>: for a star whose light curve has a plateau, its end (the lab's
/// <see cref="LightCurve.PlateauEndS"/>: the hydrogen recombination front reaches the envelope's base, Kasen &amp; Woosley 2009); never
/// earlier than <see cref="MinOpaqueS"/>, 100 d, the low end of the 100–200 d at which stripped-envelope and Type Ia ejecta turn
/// nebular (Jerkstrand 2017, Handbook of Supernovae, arXiv:1702.06702).</item>
/// <item>Then clearing, smoothly in log time, to fully clear (veil 0) at <see cref="ClearByS"/> = 2 × that: for a typical Type IIP,
/// 120 d → 240 d, inside the 150–300 d at which the centre turns thin in red and near-infrared light.</item>
/// </list>
/// Approximate: the opacity is an optical-continuum one; blue and ultraviolet light stay blocked by lines for years, and dust from about
/// 1.5 yr dims the centre again in patches (SN 1987A); neither is drawn (#71). NaN without ejecta (a failed supernova).
/// </summary>
public static class DebrisOpacity
{
    /// <summary>[APPROX] The shortest opaque phase, s: 100 d (Jerkstrand 2017: stripped-envelope and Type Ia ejecta, 100–200 d).</summary>
    public const double MinOpaqueS = 100 * PhysicalConstants.Day;

    /// <summary>The clearing ends at this multiple of <see cref="OpaqueUntilS"/>.</summary>
    public const double ClearFactor = 2;

    public static readonly Source Source = new(
        "Seen from the centre the debris stays opaque until its light curve's plateau ends (Kasen & Woosley 2009, ApJ 703, 2205), at least " +
        "100 d (stripped-envelope and Type Ia ejecta turn nebular at 100–200 d: Jerkstrand 2017, Handbook of Supernovae), and is clear in red " +
        "light by twice that", "scientific-inconsistencies #71");

    /// <summary>Until when (s after bounce) the debris is fully opaque from the centre; NaN without ejecta.</summary>
    public static double OpaqueUntilS(Outcome o)
    {
        if (!EjectaKinematics.HasEjecta(o)) return double.NaN;
        double plateau = LightCurve.Of(o) is { } c && c.PlateauEndS > 0 && double.IsFinite(c.PlateauEndS) ? c.PlateauEndS : 0;
        return Math.Max(plateau, MinOpaqueS);
    }

    /// <summary>When (s after bounce) the debris is clear from the centre; NaN without ejecta.</summary>
    public static double ClearByS(Outcome o) => ClearFactor * OpaqueUntilS(o);

    /// <summary>
    /// The veil over the surroundings, 0 (clear) to 1 (opaque), at this source time: 1 until <see cref="OpaqueUntilS"/> (the time before
    /// breakout included; the Close-up draws that as the star's inside), a smoothstep in log time to 0 at <see cref="ClearByS"/>. 0 without
    /// ejecta.
    /// </summary>
    public static double VeilAt(Outcome o, double sourceTimeS)
    {
        double opaque = OpaqueUntilS(o);
        if (double.IsNaN(opaque) || double.IsNaN(sourceTimeS)) return 0;
        if (sourceTimeS <= opaque) return 1;
        double x = Math.Log(sourceTimeS / opaque) / Math.Log(ClearFactor);
        if (x >= 1) return 0;
        return 1 - x * x * (3 - 2 * x);
    }
}
