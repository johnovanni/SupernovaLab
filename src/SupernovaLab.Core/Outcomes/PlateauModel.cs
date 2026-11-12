using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Outcomes;

/// <summary>
/// The Type II-P plateau of Goldberg, Bildsten &amp; Paxton 2019 (ApJ 879, 3; signals §4.3, outcomes §8.2 step 6), shared
/// by the outcome engine's stated plateau luminosity and the light-curve model (ruling R33c): the UI states the plateau it
/// draws. E in erg, masses in M☉, radius in cm, times in s. Pure.
/// </summary>
public static class PlateauModel
{
    /// <summary>
    /// [APPROX] Signals §4.3 step 5: below about 1 M☉ of hydrogen envelope there is no plateau, only an Arnett peak.
    /// </summary>
    public const double MinEnvelopeMsun = 1.0;

    /// <summary>
    /// Goldberg et al. 2019's plateau-length fit uses models with M_Ni ≳ 0.03 M☉ (§4.3). [APPROX] Below that the length
    /// follows Kasen &amp; Woosley 2009's nickel extension (1 + C_f M_Ni E51^−1/2 M10^−1/2 R500^−1)^(1/6), C_f ≈ 87,
    /// scaled to meet the fit at 0.03 M☉.
    /// </summary>
    private const double G19MinNickel = 0.03, KwNickelCf = 87;

    /// <summary>
    /// The day-50 plateau luminosity, erg/s: log10(L50 / erg s⁻¹) = 42.16 − 0.40 log M10 + 0.74 log E51 + 0.76 log R500,
    /// with M10 = M_ej / 10 M☉, E51 = E / 1e51 erg and R500 = R / 500 R☉ (Goldberg et al. 2019; signals §4.3).
    /// </summary>
    public static double LuminosityErgS(double energyErg, double ejectaMsun, double radiusCm)
        => Math.Pow(10, 42.16 - 0.40 * Math.Log10(ejectaMsun / 10) + 0.74 * Math.Log10(energyErg / 1e51)
                        + 0.76 * Math.Log10(radiusCm / (500 * PhysicalConstants.RSun)));

    /// <summary>
    /// The plateau length t_p, s after explosion (§4.3): Goldberg et al. 2019, log10(t_p / d) = 2.184 + 0.134 log M_Ni +
    /// 0.411 log M10 − 0.282 log E51, for M_Ni ≥ 0.03 M☉; below that, [APPROX] the fit at 0.03 M☉ scaled by Kasen &amp;
    /// Woosley's nickel extension relative to its value at 0.03 M☉.
    /// </summary>
    public static double DurationS(double energyErg, double ejectaMsun, double nickelMsun, double radiusCm)
    {
        double e51 = energyErg / 1e51, m10 = ejectaMsun / 10, r500 = radiusCm / (500 * PhysicalConstants.RSun);
        double G19(double ni) => Math.Pow(10, 2.184 + 0.134 * Math.Log10(ni) + 0.411 * Math.Log10(m10) - 0.282 * Math.Log10(e51));
        if (nickelMsun >= G19MinNickel) return G19(nickelMsun) * PhysicalConstants.Day;
        double Extension(double ni) => Math.Pow(1 + KwNickelCf * ni / (Math.Sqrt(e51) * Math.Sqrt(m10) * r500), 1.0 / 6);
        return G19(G19MinNickel) * Extension(Math.Max(nickelMsun, 0)) / Extension(G19MinNickel) * PhysicalConstants.Day;
    }
}
