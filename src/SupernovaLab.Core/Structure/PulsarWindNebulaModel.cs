using System.Globalization;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Structure;

/// <summary>
/// The pulsar wind nebula (structure §S9 and its App block): the bubble a young pulsar's wind inflates inside the freely
/// expanding debris. Source time throughout (s after core bounce); radii in cm.
/// <list type="bullet">
/// <item><b>Growth law</b> (thin shell, Chevalier &amp; Fransson 1992 eq. 2.6 as given by Chevalier 2005 eq. 26): in ejecta
/// ρ = A t^(m−3) r^(−m) the nebula's radius is R = [(5−m)³(3−m) / ((11−2m)(9−2m)) · L t^(6−m) / (4π A)]^(1/(5−m)), so
/// R ∝ t^((6−m)/(5−m)); for the lab's δ = 1 core (<see cref="EjectaStructure.Delta"/>) m = 1, the coefficient is 128/63 and R ∝ t^1.25.
/// A comes from §S1: A = K v_t with K = M (3−δ)(n−3) / (4π v_t³ (n−δ)) [D], valid while R/t &lt; v_t.</item>
/// <item><b>Luminosity</b>: the energy the pulsar has put out so far divided by the time, L(t) = E_spun(t)/t = Ė₀ τ₀ / (t + τ₀)
/// (the lab's n = 3 spin-down, <see cref="PulsarModel.Spin"/>: E_spun = Ė₀ τ₀ t/(t + τ₀)); an effective constant luminosity.</item>
/// <item><b>Caps</b>: the radius is held to the reverse shock once there are shocks (§S4) and to v_t t.</item>
/// </list>
/// The result is 10–45% below the Crab's measured size (inconsistency #39) because the Crab's own E and M_ej are uncertain by factors
/// of a few and the nebula's expansion is known to have accelerated.
/// </summary>
public static class PulsarWindNebulaModel
{
    /// <summary>The inner density slope m of ρ ∝ r^(−m): §S1's δ = 1 core.</summary>
    public const double InnerSlope = EjectaStructureModel.InnerSlope;

    /// <summary>The overlay note's citation: where the size comes from.</summary>
    public static Source Source { get; } = new(
        "Chevalier & Fransson 1992 (ApJ 395, 540) as given by Chevalier 2005 (ApJ 619, 839) eq. 26, with the debris profile of structure §S1 " +
        "and the lab's pulsar spin-down; the same family as van der Swaluw et al. 2001", "structure §S9");

    /// <summary>The scene note: "Pulsar wind nebula: size from {source}".</summary>
    public static string Note => string.Format(CultureInfo.InvariantCulture, "Pulsar wind nebula: size from {0}", Source.Citation);

    /// <summary>
    /// Whether the outcome has a nebula: a supernova that leaves a neutron star or magnetar with a spin and a field
    /// (<see cref="PulsarModel.Applies"/>) and has ejecta with a smooth profile (not a failed supernova's thin shell).
    /// </summary>
    public static bool Applies(Outcome o)
        => o is not null && new PulsarModel().Applies(o) && EjectaStructureModel.For(o) is { IsEnvelopeShell: false };

    /// <summary>
    /// The nebula's radius (cm) at source time t: NaN unless <see cref="Applies"/>; 0 at t ≤ 0; otherwise §S9's growth law with the
    /// pulsar's spin-down luminosity, held to the reverse shock and to v_t t.
    /// </summary>
    public static double RadiusCm(Outcome o, double sourceTimeS)
    {
        if (!Applies(o)) return double.NaN;
        if (!(sourceTimeS > 0)) return 0;
        var structure = EjectaStructureModel.For(o)!;
        var spin = PulsarModel.Spin.Of(o);
        double r = RadiusCm(structure, EffectiveLuminosityErgS(spin, sourceTimeS), sourceTimeS);
        var shocks = ShockStructureModel.At(o, sourceTimeS);
        if (shocks is not null && shocks.ReverseCm > 0) r = Math.Min(r, shocks.ReverseCm);
        return r;
    }

    /// <summary>The effective constant luminosity L = E_spun(t)/t = Ė₀ τ₀ / (t + τ₀), erg/s.</summary>
    public static double EffectiveLuminosityErgS(PulsarModel.Spin spin, double sourceTimeS)
        => spin.SpinDownLuminosity(0) * spin.Tau0 / (sourceTimeS + spin.Tau0);

    /// <summary>
    /// The growth law for a profile (uncapped by shocks; capped at v_t t): [128/63 · L t⁵ / (4π A)]^(1/4) for δ = 1, A = K v_t.
    /// <paramref name="luminosityErgS"/> is taken as constant over [0, t].
    /// </summary>
    public static double RadiusCm(EjectaStructure s, double luminosityErgS, double sourceTimeS)
    {
        double a = InnerDensityCoefficient(s);
        double r = RadiusForCoefficient(InnerSlope, a, luminosityErgS, sourceTimeS);
        return Math.Min(r, s.TransitionCmS * sourceTimeS);
    }

    /// <summary>
    /// A in ρ = A t^(m−3) r^(−m) for m = δ = 1 (so ρ = A t⁻² r⁻¹; g s² cm⁻⁴ in cgs): A = K v_t, K = M (3−δ)(n−3) / (4π v_t³ (n−δ)).
    /// </summary>
    public static double InnerDensityCoefficient(EjectaStructure s)
    {
        double m = s.EjectaMassMsun * PhysicalConstants.MSun, vt = s.TransitionCmS;
        double k = m * (3 - s.Delta) * (s.N - 3) / (4 * Math.PI * vt * vt * vt * (s.N - s.Delta));
        return k * vt;
    }

    /// <summary>The thin-shell coefficient (5−m)³(3−m) / ((11−2m)(9−2m)); 128/63 for m = 1.</summary>
    public static double Coefficient(double m) => Math.Pow(5 - m, 3) * (3 - m) / ((11 - 2 * m) * (9 - 2 * m));

    /// <summary>R = [coefficient(m) · L t^(6−m) / (4π A)]^(1/(5−m)), for ρ = A t^(m−3) r^(−m); cm.</summary>
    public static double RadiusForCoefficient(double m, double a, double luminosityErgS, double sourceTimeS)
        => Math.Pow(Coefficient(m) * luminosityErgS * Math.Pow(sourceTimeS, 6 - m) / (4 * Math.PI * a), 1 / (5 - m));
}
