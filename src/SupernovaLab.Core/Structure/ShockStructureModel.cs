using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Structure;

/// <summary>
/// The forward shock, contact surface and reverse shock (structure §S4 and its App block; rulings R-P2-4, R-P2-10, R-P2-11).
/// One rule for all three radii:
/// <code>
/// R_f(t) = EjectaKinematics.RadiusCm(o, t)       the drawn forward shock = Project 1's radius, always
/// R_c(t) = q_c(t) · R_f(t)
/// R_r(t) = min(q_r(t) · R_f(t), R_c(t))
/// </code>
/// <list type="bullet">
/// <item>Start: the circumstellar model's start for an outcome <see cref="CsmInteractionModel"/> applies to (a star with a
/// wind); otherwise (Type Ia, Iax, or no wind) <see cref="EjectaKinematics.BlendStartS"/> (3 yr). None without ejecta.</item>
/// <item>Wind era (Chevalier 1982, s = 2, §S4.1, with the class's n from §S1): q_c = 1/q₁, q_r = q₂/q₁ from the self-similar
/// ratios R₁/R_c and R₂/R_c computed in §S4.1 (n = 12 for a red supergiant, 10.18 otherwise).</item>
/// <item>Remnant era (uniform ejecta, n = 0, for every class: R-P2-10), each the ratio to its own paper's forward shock:
/// q_c = R_c/R_b from Tang &amp; Chevalier 2017 (eq. 32, Table 5; R_b from Tables 1 and 3, which is Project 1's form), held at
/// its t* = 4 value beyond TC17's limit; q_r = R_r/R_b from Truelove &amp; McKee 1999 Table 5. t* = t / t_ch in the lab's
/// units (473 yr E51^(−1/2) M_ej^(5/6) n₀^(−1/3), n₀ = 1, as Project 1).</item>
/// <item>Blend over Project 1's own window, 3 → 10 yr, geometric with the smoothstep in log t that
/// <see cref="EjectaKinematics.RadiusCm"/> uses: ln q = (1 − s) ln q_wind + s ln q_rem. Outcomes with no wind era use q_rem
/// from the start.</item>
/// </list>
/// The class's own steep-ejecta index is not drawn in the remnant era (inconsistency #33, disclosed in the overlay).
/// </summary>
public static class ShockStructureModel
{
    private const double Year = PhysicalConstants.Year;

    private static readonly CsmInteractionModel Csm = new();

    // ---------------------------------------------------------------- §S4.1: Chevalier 1982, s = 2 (computed in §S4.1 [D])

    /// <summary>§S4.1 table, s = 2: R_forward/R_c and R_reverse/R_c for n = 12 (red supergiant).</summary>
    private const double ForwardOverContactN12 = 1.2259, ReverseOverContactN12 = 0.9874;

    /// <summary>§S4.1 table, s = 2: R_forward/R_c and R_reverse/R_c for n = 10.18 (blue supergiant, stripped envelope).</summary>
    private const double ForwardOverContactN10 = 1.2374, ReverseOverContactN10 = 0.9840;

    /// <summary>
    /// The wind era's ratios to the forward shock (§S4 App block): q_c = 1/q₁ and q_r = q₂/q₁ for the class's n (12 for a red
    /// supergiant, 10.18 for a blue supergiant or a stripped star). NaN for a Type Ia, which has no wind era.
    /// </summary>
    public static (double Contact, double Reverse) WindRatios(EjectaClass c) => c switch
    {
        EjectaClass.RedSupergiant => (1 / ForwardOverContactN12, ReverseOverContactN12 / ForwardOverContactN12),
        EjectaClass.BlueSupergiant or EjectaClass.StrippedEnvelope
            => (1 / ForwardOverContactN10, ReverseOverContactN10 / ForwardOverContactN10),
        _ => (double.NaN, double.NaN),
    };

    // ---------------------------------------------------------------- §S4.2: the n = 0 trajectories (TM99 Table 5; TC17)

    /// <summary>TM99 Table 5's split between the ejecta-dominated and Sedov–Taylor pieces, t*_ST = 0.495.</summary>
    public const double TmSedovStart = 0.495;

    /// <summary>TC17's stated validity of the n = 0 contact fit, t*_lim = 4; q_c is held at its value there beyond.</summary>
    public const double TcContactLimit = 4;

    /// <summary>
    /// Truelove &amp; McKee 1999 Table 5 (n = 0) forward shock, R_b* = 2.01 t* (1 + 1.72 t*^1.5)^(−2/3) for t* ≤ 0.495, then
    /// (1.42 t* − 0.254)^(2/5).
    /// </summary>
    public static double TmForwardN0(double tStar)
        => tStar <= TmSedovStart
            ? 2.01 * tStar * Math.Pow(1 + 1.72 * Math.Pow(tStar, 1.5), -2.0 / 3)
            : Math.Pow(1.42 * tStar - 0.254, 0.4);

    /// <summary>
    /// Truelove &amp; McKee 1999 Table 5 (n = 0) reverse shock in the lab frame, R_r* = 1.83 t* (1 + 3.26 t*^1.5)^(−2/3) for
    /// t* ≤ 0.495, then t* (0.779 − 0.106 t* − 0.533 ln t*), held at 0 once it reaches the centre (t* = 2.58).
    /// </summary>
    public static double TmReverseN0(double tStar)
        => tStar <= TmSedovStart
            ? 1.83 * tStar * Math.Pow(1 + 3.26 * Math.Pow(tStar, 1.5), -2.0 / 3)
            : Math.Max(0, tStar * (0.779 - 0.106 * tStar - 0.533 * Math.Log(tStar)));

    /// <summary>Tang &amp; Chevalier 2017 (Tables 1 and 3, n = 0) forward shock: exactly Project 1's form (<see cref="RemnantEvolutionModel.ScaledRadius"/>).</summary>
    public static double TcForwardN0(double tStar) => RemnantEvolutionModel.ScaledRadius(tStar);

    /// <summary>
    /// Tang &amp; Chevalier 2017 (eq. 32, Table 5, n = 0) contact discontinuity: R_c* = [(1.826 t*)^(−0.89) + (1.66 t*^(−0.37))^(−0.89)]^(−1/0.89),
    /// with ζ_c = (10/3)^(1/2) = 1.826 (the uniform ejecta's edge).
    /// </summary>
    public static double TcContactN0(double tStar)
        => Math.Pow(Math.Pow(1.826 * tStar, -0.89) + Math.Pow(1.66 * Math.Pow(tStar, -0.37), -0.89), -1 / 0.89);

    /// <summary>q_c,rem(t*) = R_c,TC / R_b,TC, held at its t* = 4 value (0.4208) beyond TC17's limit [APPROX].</summary>
    public static double RemnantContactRatio(double tStar)
    {
        double t = Math.Min(tStar, TcContactLimit);
        return TcContactN0(t) / TcForwardN0(t);
    }

    /// <summary>q_r,rem(t*) = R_r,TM / R_b,TM (TM99 Table 5); 0 from t* = 2.58, when the reverse shock reaches the centre.</summary>
    public static double RemnantReverseRatio(double tStar) => TmReverseN0(tStar) / TmForwardN0(tStar);

    // ---------------------------------------------------------------- the drawn shocks

    /// <summary>
    /// When the shocks start for this outcome, source time (ruling R-P2-4): the circumstellar model's start
    /// (<see cref="CsmInteractionModel.StartTimeS"/>) where it applies, otherwise <see cref="EjectaKinematics.BlendStartS"/>
    /// (3 yr). NaN for an outcome with no ejecta.
    /// </summary>
    public static double StartTimeS(Outcome o)
    {
        if (!EjectaKinematics.HasEjecta(o) || o.Star is null) return double.NaN;
        return HasWindEra(o) ? Csm.StartTimeS(o) : EjectaKinematics.BlendStartS;
    }

    /// <summary>Whether the outcome has a wind era (the circumstellar model applies to it).</summary>
    private static bool HasWindEra(Outcome o) => Csm.Applies(o);

    /// <summary>
    /// The ratios (q_c, q_r) to the forward shock at source time t (§S4 App block): the wind era's below 3 yr, the remnant era's
    /// from 10 yr, blended geometrically in between; the remnant era's throughout for an outcome with no wind era. NaN without ejecta.
    /// </summary>
    public static (double Contact, double Reverse) Ratios(Outcome o, double sourceTimeS)
    {
        if (!EjectaKinematics.HasEjecta(o)) return (double.NaN, double.NaN);
        double tch = RemnantEvolutionModel.CharacteristicTimeS(o.ExplosionEnergyErg!.Value, o.EjectaMass!.Value,
            RemnantEvolutionModel.AmbientDensity);
        double tStar = sourceTimeS / tch;
        double qcRem = RemnantContactRatio(tStar), qrRem = RemnantReverseRatio(tStar);
        if (!HasWindEra(o) || sourceTimeS >= RemnantEvolutionModel.StartS) return (qcRem, qrRem);

        var (qcWind, qrWind) = WindRatios(EjectaStructureModel.ClassOf(o));
        if (sourceTimeS <= EjectaKinematics.BlendStartS) return (qcWind, qrWind);
        double x = Math.Log(sourceTimeS / EjectaKinematics.BlendStartS)
                   / Math.Log(RemnantEvolutionModel.StartS / EjectaKinematics.BlendStartS);
        double s = x * x * (3 - 2 * x);
        return (Geometric(qcWind, qcRem, s), Geometric(qrWind, qrRem, s));
    }

    /// <summary>exp((1 − s) ln a + s ln b); 0 when either end is 0 (the reverse shock at the centre).</summary>
    private static double Geometric(double a, double b, double s)
        => a > 0 && b > 0 ? Math.Exp((1 - s) * Math.Log(a) + s * Math.Log(b)) : 0;

    /// <summary>
    /// The three radii at this source time (cm), with ruling R-P2-4's confinement weight; null before
    /// <see cref="StartTimeS"/> or for an outcome with no ejecta.
    /// </summary>
    public static ShockRadii? At(Outcome o, double sourceTimeS)
    {
        double start = StartTimeS(o);
        if (!(sourceTimeS >= start)) return null;
        double forward = EjectaKinematics.RadiusCm(o, sourceTimeS);
        if (!(forward > 0)) return null;
        var (qc, qr) = Ratios(o, sourceTimeS);
        double contact = qc * forward;
        double reverse = Math.Min(qr * forward, contact);
        return new ShockRadii(forward, contact, reverse) { ConfinementWeight = ConfinementWeight(sourceTimeS, start) };
    }

    /// <summary>Ruling R-P2-4: w = clamp(log10(t / t_start), 0, 1).</summary>
    public static double ConfinementWeight(double sourceTimeS, double startS)
        => startS > 0 ? Math.Clamp(Math.Log10(sourceTimeS / startS), 0, 1) : 1;
}
