using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>Which of Lyman, Bersier &amp; James 2014's supernova samples a bolometric correction comes from.</summary>
public enum BcSample
{
    /// <summary>Stripped-envelope supernovae (IIb, Ib, Ic, Ic-BL): BC_B from B−V, valid for B−V = 0.0–1.3.</summary>
    StrippedEnvelope,
    /// <summary>Hydrogen-rich Type II supernovae: BC_B from B−V, valid for B−V = 0.0–1.6.</summary>
    TypeII,
}

/// <summary>
/// Bolometric luminosity to V magnitude (signals §4.5). Units: L in erg/s, temperatures in K, colours and magnitudes in
/// mag. Pure.
/// <list type="bullet">
/// <item>M_bol = −2.5 log10(L / 3.0128e35 erg/s) (IAU 2015 Resolution B2).</item>
/// <item>Lyman, Bersier &amp; James 2014 (MNRAS 437, 3848), Tables 2–3: BC_B = c0 + c1 (B−V) + c2 (B−V)², with
/// BC_B = M_bol − M_B; then M_V = M_B − (B−V), so BC_V = M_bol − M_V = BC_B + (B−V).</item>
/// <item>Colours from a blackbody at the colour temperature (§4.5, "or use a blackbody at T_col"), with the V zero point
/// f_λ,0 = 3.63e-9 erg s⁻¹ cm⁻² Å⁻¹ at λ_eff = 5,450 Å (Bessell, Castelli &amp; Plez 1998). [APPROX] The B zero point,
/// 6.32e-9 erg s⁻¹ cm⁻² Å⁻¹ at 4,380 Å, is from the same table but not in the research files; both bands are taken as
/// monochromatic at λ_eff. A 5,772 K blackbody then has B−V = 0.63 and BC_V = −0.12 (the Sun: 0.65 and about −0.07).</item>
/// <item>Type Ia late colour, the Lira relation: B−V = 0.725 − 0.0118 (t_V − 60 d), 30–90 d after V maximum
/// (Phillips et al. 1999, AJ 118, 1766).</item>
/// </list>
/// [APPROX] Outside a fit's colour range (the hot early phases, B−V &lt; 0) the correction is continued from the range's
/// edge with the blackbody's own change in BC_V, so it stays continuous and keeps the ultraviolet's share of the light out
/// of V. Lyman et al.'s fits are good to about 0.1 mag only inside their ranges (signals §12).
/// </summary>
public static class BolometricCorrection
{
    // ---------------------------------------------------------------- constants

    /// <summary>The second radiation constant h c / k_B, nm K.</summary>
    private const double C2NmK = PhysicalConstants.HcEvNm / PhysicalConstants.BoltzmannEv;

    /// <summary>V (research §4.5) and B ([APPROX], not in the research) effective wavelengths, nm, and zero points.</summary>
    private const double VLambdaNm = 545.0, VZeroPoint = 3.63e-9, BLambdaNm = 438.0, BZeroPoint = 6.32e-9;

    /// <summary>10 pc in cm, for absolute magnitudes.</summary>
    private const double TenParsecsCm = 10 * PhysicalConstants.Pc;

    /// <summary>The temperatures between which colours are inverted, K. A blackbody's B−V runs from −0.35 (hot) upward.</summary>
    private const double MinTemperatureK = 1_000, MaxTemperatureK = 1e6;

    private readonly record struct Fit(double C0, double C1, double C2, double Low, double High);

    // Lyman, Bersier & James 2014, Tables 2–3 (signals §4.5): B−V gives BC_B.
    private static readonly Fit StrippedFit = new(-0.083, -0.139, -0.691, 0.0, 1.3);
    private static readonly Fit TypeIIFit = new(-0.138, -0.013, -0.649, 0.0, 1.6);

    private static Fit FitOf(BcSample s) => s == BcSample.TypeII ? TypeIIFit : StrippedFit;

    // ---------------------------------------------------------------- public formulas

    /// <summary>M_bol = −2.5 log10(L / 3.0128e35 erg/s) (IAU 2015 B2; signals §0.2, §4.5). NaN unless L &gt; 0.</summary>
    public static double AbsoluteBolometricMagnitude(double luminosityErgS)
        => luminosityErgS > 0 ? -2.5 * Math.Log10(luminosityErgS / PhysicalConstants.MbolZeroPointLum) : double.NaN;

    /// <summary>Lyman et al. 2014's quadratic BC_B = c0 + c1 (B−V) + c2 (B−V)², with no range check.</summary>
    public static double LymanBcB(BcSample sample, double bMinusV)
    {
        var f = FitOf(sample);
        return f.C0 + f.C1 * bMinusV + f.C2 * bMinusV * bMinusV;
    }

    /// <summary>The B−V range over which <see cref="LymanBcB"/> was fitted.</summary>
    public static (double Low, double High) LymanColourRange(BcSample sample) => (FitOf(sample).Low, FitOf(sample).High);

    /// <summary>
    /// The Lira relation for Type Ia supernovae, B−V = 0.725 − 0.0118 (t_V − 60 d), with t_V the days after V maximum
    /// (Phillips et al. 1999). Valid for 30–90 d; no range check here.
    /// </summary>
    public static double LiraBMinusV(double daysAfterVMax) => 0.725 - 0.0118 * (daysAfterVMax - 60);

    /// <summary>
    /// B−V of a blackbody at this temperature, both bands monochromatic at their effective wavelengths:
    /// −2.5 log10[(B_λ(4,380 Å) / 6.32e-9) / (B_λ(5,450 Å) / 3.63e-9)].
    /// </summary>
    public static double BlackbodyBMinusV(double temperatureK)
    {
        // B_λ ∝ λ⁻⁵ / (e^(c2/λT) − 1); the common factor 2hc² cancels.
        double ratio = Math.Pow(VLambdaNm / BLambdaNm, 5) * ExpM1(C2NmK / (VLambdaNm * temperatureK))
                       / ExpM1(C2NmK / (BLambdaNm * temperatureK));
        return -2.5 * Math.Log10(ratio / (BZeroPoint / VZeroPoint));
    }

    /// <summary>
    /// The blackbody temperature (K) whose B−V is this colour, by bisection in log T between 1,000 K and 10⁶ K. Colours
    /// bluer than a 10⁶ K blackbody give 10⁶ K; redder than 1,000 K give 1,000 K.
    /// </summary>
    public static double BlackbodyTemperatureForBMinusV(double bMinusV)
    {
        double lo = Math.Log(MinTemperatureK), hi = Math.Log(MaxTemperatureK);
        if (bMinusV >= BlackbodyBMinusV(MinTemperatureK)) return MinTemperatureK;
        if (bMinusV <= BlackbodyBMinusV(MaxTemperatureK)) return MaxTemperatureK;
        for (int i = 0; i < 80; i++)
        {
            double mid = 0.5 * (lo + hi);
            // B−V falls as T rises.
            if (BlackbodyBMinusV(Math.Exp(mid)) > bMinusV) lo = mid; else hi = mid;
        }
        return Math.Exp(0.5 * (lo + hi));
    }

    /// <summary>
    /// A blackbody's BC_V = M_bol − M_V: the V-band flux at 10 pc, L π B_λ(5,450 Å, T) / (σ T⁴) / (4π (10 pc)²), against
    /// the V zero point, and M_bol against the IAU zero point. Independent of L.
    /// </summary>
    public static double BlackbodyBcV(double temperatureK)
    {
        double x = C2NmK / (VLambdaNm * temperatureK);
        // π B_λ / (σ T⁴) per Å = 15 c2⁴ / (π⁴ λ⁵ T⁴ (e^x − 1)), with λ and c2 in cm, then × 1e-8 cm/Å.
        double lambdaCm = VLambdaNm * 1e-7, c2Cm = C2NmK * 1e-7;
        double fraction = 15 * Math.Pow(c2Cm, 4) / (Math.Pow(Math.PI, 4) * Math.Pow(lambdaCm, 5) * Math.Pow(temperatureK, 4)
                                                    * ExpM1(x)) * 1e-8;
        double perUnitL = fraction / (4 * Math.PI * TenParsecsCm * TenParsecsCm);
        return 2.5 * Math.Log10(perUnitL * PhysicalConstants.MbolZeroPointLum / VZeroPoint);
    }

    /// <summary>
    /// BC_V = M_bol − M_V for a supernova of this B−V: Lyman et al.'s BC_B + (B−V) inside the fit's colour range;
    /// outside it, the edge's value plus the blackbody's change in BC_V from the edge colour to this one [APPROX].
    /// </summary>
    public static double BcV(BcSample sample, double bMinusV)
    {
        var f = FitOf(sample);
        if (bMinusV >= f.Low && bMinusV <= f.High) return LymanBcB(sample, bMinusV) + bMinusV;
        double edge = bMinusV < f.Low ? f.Low : f.High;
        return LymanBcB(sample, edge) + edge
               + BlackbodyBcV(BlackbodyTemperatureForBMinusV(bMinusV)) - BlackbodyBcV(BlackbodyTemperatureForBMinusV(edge));
    }

    /// <summary>M_V = M_bol − BC_V for a supernova of this luminosity (erg/s) and colour. NaN unless L &gt; 0.</summary>
    public static double AbsoluteV(double luminosityErgS, double bMinusV, BcSample sample)
        => AbsoluteBolometricMagnitude(luminosityErgS) - BcV(sample, bMinusV);

    /// <summary>e^x − 1 without cancellation for small x.</summary>
    private static double ExpM1(double x) => Math.Abs(x) < 1e-3 ? x * (1 + x / 2 * (1 + x / 3)) : Math.Exp(x) - 1;
}
