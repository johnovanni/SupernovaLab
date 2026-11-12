using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Outcomes;

/// <summary>
/// Magnetar spin-down (outcomes §5.3, Kasen & Bildsten 2010 eq. 1, 2, 12 and 14; I = 10⁴⁵ g cm², R = 10 km,
/// sin²α = 1/2) and the birth spin and field that outcomes §8.2 step 7b assigns from the rotation input. Periods in s,
/// fields in G, energies in erg, times in s, masses in M☉. Pure.
/// </summary>
public static class MagnetarModel
{
    /// <summary>
    /// KB10's opacity, κ ≈ 0.2 cm² g⁻¹ (outcomes §5.3): the Arnett diffusion time of every magnetar-powered light curve
    /// (ruling R32), in the outcome engine and the light-curve model alike.
    /// </summary>
    public const double Opacity = 0.2;

    private static readonly Source Kb10 =
        new("Kasen & Bildsten 2010, ApJ 717, 245 (eq. 1, 2; I = 10^45 g cm², R = 10 km)", "outcomes §5.3, §8.2 step 7b");
    private static readonly Source N17 =
        new("Nicholl, Guillochon & Berger 2017, ApJ 850, 55 (SLSN-I magnetar fits, median and 1σ range)", "outcomes §5.3, §8.2 step 7b");

    /// <summary>Rotational energy E_p = 2×10⁵² P_ms⁻² erg (KB10 eq. 1).</summary>
    public static double SpinDownEnergyErg(double periodS) => 2e52 / Math.Pow(periodS / 1e-3, 2);

    /// <summary>Spin-down time t_p = 1.3 yr × B14⁻² P10² (KB10 eq. 2).</summary>
    public static double SpinDownTimeS(double periodS, double fieldG)
        => 1.3 * PhysicalConstants.Year * Math.Pow(periodS / 1e-2, 2) / Math.Pow(fieldG / 1e14, 2);

    /// <summary>Dipole spin-down power L_p(t) = (E_p / t_p) / (1 + t / t_p)² (KB10 eq. 14).</summary>
    public static double LuminosityErgS(double timeS, double energyErg, double timescaleS)
        => energyErg / timescaleS / Math.Pow(1 + timeS / timescaleS, 2);

    /// <summary>
    /// Ruling R32: the spin-down power diffusing out of the ejecta, erg/s, at time t (s): Arnett's kernel (signals §4.2,
    /// Inserra et al. 2013 Eq. D1, as signals §4.4 prescribes) over L_p, with τ_m = (2κM/(βcv))^(1/2), v = √(10E/3M),
    /// κ = <see cref="Opacity"/> and E the ejecta's kinetic energy. Full trapping is assumed. The light-curve model draws
    /// exactly this as its magnetar part.
    /// </summary>
    public static double DiffusedLuminosityErgS(double timeS, double energyErg, double timescaleS, double ejectaMsun,
        double kineticEnergyErg)
        => ArnettDiffusion.LuminosityErgS(t => LuminosityErgS(t, energyErg, timescaleS),
            ArnettDiffusion.DiffusionTimeS(Opacity, ejectaMsun, kineticEnergyErg), timeS);

    /// <summary>
    /// Ruling R32: THE peak luminosity of a magnetar-powered supernova, erg/s: the maximum of
    /// <see cref="DiffusedLuminosityErgS"/>, searched over 0.01–20 τ_m. The outcome engine's SLSN-II cut (ruling R22) and
    /// stated peak use it, and the light curve draws the same function, so the label matches the drawn light.
    /// [APPROX] the ⁵⁶Ni heating (~10⁴² erg/s) is not included.
    /// </summary>
    public static double PeakLuminosityErgS(double energyErg, double timescaleS, double ejectaMsun, double kineticEnergyErg)
    {
        double tau = ArnettDiffusion.DiffusionTimeS(Opacity, ejectaMsun, kineticEnergyErg);
        return ArnettDiffusion.Maximize(t => DiffusedLuminosityErgS(t, energyErg, timescaleS, ejectaMsun, kineticEnergyErg),
            0.01 * tau, 20 * tau).Value;
    }

    /// <summary>
    /// Step 7b: the magnetar a strong core field makes of this neutron star. Spin period: extreme rotation 1.2 ms (1–2 ms,
    /// N17 1σ low end), fast 2.4 ms (~2–5 ms, N17 median), slow the ordinary neutron-star value (§6). Field: the N17
    /// median 0.8×10¹⁴ G for fast or extreme rotation, otherwise 5×10¹⁴ G (KB10; Beniamini et al. 2019). The mapping from
    /// the star to P and B is Speculative (outcomes §8.3).
    /// </summary>
    public static Remnant Magnetar(Remnant neutronStar, Rotation rotation)
    {
        var period = rotation switch
        {
            Rotation.Extreme => new Quantity(1.2e-3, "s", Confidence.Speculative,
                N17 with { Citation = N17.Citation + "; 1σ low end 1.2 ms for extreme rotation" }, 1e-3, 2e-3),
            Rotation.Fast => new Quantity(2.4e-3, "s", Confidence.Speculative,
                N17 with { Citation = N17.Citation + "; median 2.4 ms for fast rotation" }, 2e-3, 5e-3),
            _ => neutronStar.SpinPeriodS,
        };
        var field = rotation == Rotation.Slow
            ? new Quantity(5e14, "G", Confidence.Speculative,
                new Source("Kasen & Bildsten 2010, ApJ 717, 245; Beniamini et al. 2019, MNRAS 487, 1426 (birth fields " +
                           "3×10^14–10^15 G)", "outcomes §5.3, §6, §8.2 step 7b"), 3e14, 1e15)
            : new Quantity(0.8e14, "G", Confidence.Speculative, N17, 0.2e14, 1.8e14);
        return neutronStar with { Kind = RemnantKind.Magnetar, SpinPeriodS = period, MagneticFieldG = field };
    }

    /// <summary>E_p of this magnetar as a Quantity (KB10 eq. 1); no more certain than its spin period.</summary>
    public static Quantity SpinDownEnergy(Quantity period)
        => new(SpinDownEnergyErg(period.Value), "erg", RemnantModel.Weakest(period.Confidence, Confidence.ModelDependent), Kb10);

    /// <summary>t_p of this magnetar as a Quantity (KB10 eq. 2); no more certain than its spin period and field.</summary>
    public static Quantity SpinDownTime(Quantity period, Quantity field)
        => new(SpinDownTimeS(period.Value, field.Value), "s",
            RemnantModel.Weakest(period.Confidence, field.Confidence, Confidence.ModelDependent), Kb10);
}
