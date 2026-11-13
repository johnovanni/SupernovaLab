using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Structure;

/// <summary>
/// The forward shock, contact surface and reverse shock (structure §S4 and its App block; ruling R-P2-24, which supersedes
/// R-P2-10 and the "× Project 1's forward radius" part of R-P2-11; R-P2-4 for the start).
/// <list type="bullet">
/// <item><b>Wind era</b> (an outcome <see cref="CsmInteractionModel"/> applies to, from its start to 3 yr): R_f is the radio
/// model's own shock, R = v_s t (<see cref="CsmInteractionModel.ShockRadiusCm"/>); R_c = R_f/q₁ and R_r = R_f·q₂/q₁ from the
/// s = 2 self-similar ratios of §S4.1 (Chevalier 1982) for the class's n (12 for a red supergiant, 10.18 otherwise).</item>
/// <item><b>Remnant era</b> (from <see cref="RemnantEvolutionModel.StartS"/>, 10 yr): R_f and R_r from Truelove &amp; McKee
/// 1999's trajectories and R_c from Tang &amp; Chevalier 2017's contact fit (§S4.2), for the class's n (7 for a Type Ia, 12
/// for a red supergiant, 10 for a blue supergiant or a stripped star), in characteristic units with the outcome's E, M_ej
/// and n₀ = <see cref="RemnantEvolutionModel.AmbientDensity"/>, ρ₀ = n₀ m_p (<see cref="CharacteristicScales"/>).</item>
/// <item><b>3 → 10 yr</b>: each radius blended geometrically, ln R = (1 − s) ln R_wind + s ln R_rem, with the smoothstep in log t
/// that <see cref="EjectaKinematics.RadiusCm"/> uses. Outcomes with no wind era (Type Ia, Iax, no wind) start at 3 yr
/// (R-P2-4) with the remnant-era solution, which TM99 give from early times.</item>
/// <item>R_c := min(R_c, R_f) and R_r := min(R_r, R_c) (neither binds for the class-n solutions in practice; they guard the
/// joins).</item>
/// <item>The reverse shock's Lagrangian speed for the particles' confinement (ruling P4) is the running minimum over time of
/// R_r(t') / (t' − t_bo) from the start to t (<see cref="ShockRadii.ReverseLagrangianCmS"/>), so a shocked particle stays
/// shocked even where the joined fits move the reverse shock outward through the debris.</item>
/// </list>
/// Project 1's uniform-ejecta remnant model (<see cref="RemnantEvolutionModel"/>, behind the panels) is not changed: from 10
/// yr the 3D view's readout is this forward shock and a note gives Project 1's value (inconsistency #33).
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

    // ---------------------------------------------------------------- §S4.2: Truelove & McKee 1999, Tang & Chevalier 2017

    /// <summary>
    /// The ejecta index whose remnant solution is drawn (§S4.2): 7 for a Type Ia (TM99 Table 7), 12 for a red supergiant and
    /// 10 for a blue supergiant or a stripped star (TM99 Table 6; TM99 has no 10.18 row).
    /// </summary>
    public static int RemnantIndex(EjectaClass c) => c switch
    {
        EjectaClass.TypeIa => 7,
        EjectaClass.RedSupergiant => 12,
        _ => 10,
    };

    /// <summary>
    /// TM99 forward shock R_b* for n = 0 (Table 5), 7 (Table 7), 10 or 12 (§S4.2's rebuild of Table 6): the ejecta-dominated
    /// piece up to t*_ST, then the offset Sedov law (1.42 t* − c)^(2/5).
    /// </summary>
    public static double TmForward(int n, double tStar) => n switch
    {
        0 => tStar <= 0.495 ? 2.01 * tStar * Math.Pow(1 + 1.72 * Math.Pow(tStar, 1.5), -2.0 / 3) : Math.Pow(1.42 * tStar - 0.254, 0.4),
        7 => tStar <= 0.732 ? 1.06 * Math.Pow(tStar, 4.0 / 7) : Math.Pow(1.42 * tStar - 0.312, 0.4),
        10 => tStar <= 0.481 ? 1.147 * Math.Pow(tStar, 0.7) : Math.Pow(1.42 * tStar - 0.2935, 0.4),
        12 => tStar <= 0.424 ? 1.210 * Math.Pow(tStar, 0.75) : Math.Pow(1.42 * tStar - 0.2809, 0.4),
        _ => throw new ArgumentOutOfRangeException(nameof(n), n, "TM99 trajectories for n = 0, 7, 10, 12"),
    };

    /// <summary>
    /// TM99 reverse shock in the lab frame, R_r* (n = 0: Table 5; 7: Table 7; 10, 12: §S4.2's rebuild from Table 6 and eq. 83):
    /// the envelope piece up to t*_core (0.495 for n = 0), then t*(A − B t* − C ln t*); held at 0 once it reaches the centre
    /// (t* = 2.58, 2.68, 2.74, 2.74).
    /// </summary>
    public static double TmReverse(int n, double tStar)
    {
        double Core(double a, double b, double c) => Math.Max(0, tStar * (a - b * tStar - c * Math.Log(tStar)));
        return n switch
        {
            0 => tStar <= 0.495 ? 1.83 * tStar * Math.Pow(1 + 3.26 * Math.Pow(tStar, 1.5), -2.0 / 3) : Core(0.779, 0.106, 0.533),
            7 => tStar <= 0.363 ? 0.841 * Math.Pow(tStar, 4.0 / 7) : Core(0.815, 0.116, 0.511),
            10 => tStar <= 0.220 ? 0.980 * Math.Pow(tStar, 0.7) : Core(0.9506, 0.192, 0.4208),
            12 => tStar <= 0.182 ? 1.052 * Math.Pow(tStar, 0.75) : Core(1.0468, 0.251, 0.3573),
            _ => throw new ArgumentOutOfRangeException(nameof(n), n, "TM99 trajectories for n = 0, 7, 10, 12"),
        };
    }

    /// <summary>TC17 Table 5 (s = 0): α, b, c, ζ_c and the fit's limit t*_lim.</summary>
    private static (double Alpha, double B, double C, double Zeta, double Limit) ContactFit(int n) => n switch
    {
        0 => (0.89, -0.37, 1.66, 1.826, 4),
        7 => (4.28, -0.11, 1.09, 0.89, 4.5),
        10 => (2.92, -0.12, 1.07, 1.01, 4),
        12 => (2.35, -0.16, 1.11, 1.08, 4),
        _ => throw new ArgumentOutOfRangeException(nameof(n), n, "TC17 contact fits for n = 0, 7, 10, 12"),
    };

    /// <summary>TC17's stated validity of the contact fit for this n (4, or 4.5 for n = 7).</summary>
    public static double TcContactLimit(int n) => ContactFit(n).Limit;

    /// <summary>
    /// Tang &amp; Chevalier 2017 contact discontinuity (eq. 32, Table 5, s = 0): R_c* = [(ζ_c t*^((n−3)/n))^(−α) + (c t*^b)^(−α)]^(−1/α),
    /// with t*^1 in place of t*^((n−3)/n) for n = 0 (the uniform ejecta's edge, ζ_c = (10/3)^(1/2)).
    /// </summary>
    public static double TcContact(int n, double tStar)
    {
        var (alpha, b, c, zeta, _) = ContactFit(n);
        double e = n == 0 ? 1 : (n - 3.0) / n;
        return Math.Pow(Math.Pow(zeta * Math.Pow(tStar, e), -alpha) + Math.Pow(c * Math.Pow(tStar, b), -alpha), -1 / alpha);
    }

    /// <summary>
    /// The characteristic radius and time (TM99 eq. 1–3) with the lab's mass-density convention ρ₀ = n₀ m_p (signals §7.1,
    /// which is Tang &amp; Chevalier's, inconsistency #34; TM99 write 1.4 m_H n₀): R_ch = M_ej^(1/3) ρ₀^(−1/3),
    /// t_ch = E^(−1/2) M_ej^(5/6) ρ₀^(−1/3), in cm and s (3.43 pc and 473.4 yr for 1 M☉, 10⁵¹ erg, n₀ = 1). Computed from the
    /// constants rather than Project 1's rounded 3.4 pc and 473 yr.
    /// </summary>
    public static (double RadiusCm, double TimeS) CharacteristicScales(double energyErg, double ejectaMsun, double ambientDensity)
    {
        double m = ejectaMsun * PhysicalConstants.MSun, rho = ambientDensity * PhysicalConstants.ProtonMass;
        return (Math.Cbrt(m / rho), Math.Pow(m, 5.0 / 6) / Math.Sqrt(energyErg) / Math.Cbrt(rho));
    }

    // ---------------------------------------------------------------- the drawn shocks

    /// <summary>
    /// When the shocks start for this outcome, source time (ruling R-P2-4): the circumstellar model's start where it applies,
    /// otherwise <see cref="EjectaKinematics.BlendStartS"/> (3 yr). NaN for an outcome with no ejecta.
    /// </summary>
    public static double StartTimeS(Outcome o)
    {
        if (!EjectaKinematics.HasEjecta(o) || o.Star is null) return double.NaN;
        return Csm.Applies(o) ? Csm.StartTimeS(o) : EjectaKinematics.BlendStartS;
    }

    /// <summary>Whether the outcome has a wind era (the circumstellar model applies and there are ejecta).</summary>
    public static bool HasWindEra(Outcome o) => EjectaKinematics.HasEjecta(o) && o.Star is not null && Csm.Applies(o);

    /// <summary>One outcome's fixed inputs to the radii, worked out once per call.</summary>
    private readonly record struct Setup(bool Wind, double ShockSpeedCmS, double QcWind, double QrWind, int N,
        double RchCm, double TchS, double BreakoutS, double StartS);

    private static Setup? SetupFor(Outcome o)
    {
        double start = StartTimeS(o);
        if (double.IsNaN(start)) return null;
        var cls = EjectaStructureModel.ClassOf(o);
        bool wind = Csm.Applies(o);
        var (qc, qr) = wind ? WindRatios(cls) : (double.NaN, double.NaN);
        var (rch, tch) = CharacteristicScales(o.ExplosionEnergyErg!.Value, o.EjectaMass!.Value, RemnantEvolutionModel.AmbientDensity);
        double tbo = EjectaKinematics.BreakoutTimeS(o);
        return new Setup(wind, wind ? CsmInteractionModel.ShockSpeedCmS(o) : double.NaN, qc, qr, RemnantIndex(cls), rch, tch,
            double.IsFinite(tbo) ? tbo : 0, start);
    }

    /// <summary>The wind era's radii at t (cm): R_f = v_s t, then §S4.1's ratios.</summary>
    private static (double F, double C, double R) WindRadii(in Setup p, double t)
    {
        double f = p.ShockSpeedCmS * t;
        return (f, p.QcWind * f, p.QrWind * f);
    }

    /// <summary>
    /// The remnant era's radii at t (cm): TM99 forward and reverse, TC17 contact; past TC17's t*_lim the contact is held at its
    /// ratio to the forward shock there [APPROX].
    /// </summary>
    private static (double F, double C, double R) RemnantRadii(in Setup p, double t)
    {
        double ts = t / p.TchS, lim = TcContactLimit(p.N);
        double f = TmForward(p.N, ts);
        double c = ts <= lim ? TcContact(p.N, ts) : f * TcContact(p.N, lim) / TmForward(p.N, lim);
        return (f * p.RchCm, c * p.RchCm, TmReverse(p.N, ts) * p.RchCm);
    }

    /// <summary>The drawn radii at t: wind era, remnant era, or their geometric blend; then R_c ≤ R_f and R_r ≤ R_c.</summary>
    private static (double F, double C, double R) Radii(in Setup p, double t)
    {
        (double F, double C, double R) r;
        if (!p.Wind || t >= RemnantEvolutionModel.StartS) r = RemnantRadii(p, t);
        else if (t <= EjectaKinematics.BlendStartS) r = WindRadii(p, t);
        else
        {
            var a = WindRadii(p, t);
            var b = RemnantRadii(p, t);
            double s = BlendWeight(t);
            r = (Geometric(a.F, b.F, s), Geometric(a.C, b.C, s), Geometric(a.R, b.R, s));
        }
        double contact = Math.Min(r.C, r.F);
        return (r.F, contact, Math.Min(r.R, contact));
    }

    /// <summary>The smoothstep in log t over 3 → 10 yr (as <see cref="EjectaKinematics.RadiusCm"/>): 0 at 3 yr, 1 at 10 yr.</summary>
    public static double BlendWeight(double sourceTimeS)
    {
        double x = Math.Clamp(Math.Log(sourceTimeS / EjectaKinematics.BlendStartS)
                              / Math.Log(RemnantEvolutionModel.StartS / EjectaKinematics.BlendStartS), 0, 1);
        return x * x * (3 - 2 * x);
    }

    /// <summary>exp((1 − s) ln a + s ln b); 0 when either end is 0 (the reverse shock at the centre).</summary>
    private static double Geometric(double a, double b, double s)
        => a > 0 && b > 0 ? Math.Exp((1 - s) * Math.Log(a) + s * Math.Log(b)) : 0;

    /// <summary>The running minimum's grid: this many log-spaced nodes per decade from the start.</summary>
    public const int LagrangianNodesPerDecade = 64;

    /// <summary>
    /// The reverse shock's Lagrangian speed for the confinement (ruling R-P2-24): the running minimum over [start, t] of
    /// v(t') = R_r(t') / (t' − t_bo), on a log grid fixed by the start (<see cref="LagrangianNodesPerDecade"/> nodes per decade),
    /// interpolated as a power law (log v linear in log t) between nodes. A running minimum of a continuous function, so it never
    /// rises; it can lag the present value only by the interpolation error between two nodes.
    /// +∞ while nothing has been shocked yet (t' ≤ t_bo).
    /// </summary>
    private static double LagrangianSpeed(Setup p, double t)
    {
        double V(double tp)
        {
            double since = tp - p.BreakoutS;
            return since > 0 ? Radii(p, tp).R / since : double.PositiveInfinity;
        }

        double step = Math.Log(10) / LagrangianNodesPerDecade;
        double min = double.PositiveInfinity, prev = V(p.StartS), prevT = p.StartS;
        min = Math.Min(min, prev);
        for (int k = 1; ; k++)
        {
            double tk = p.StartS * Math.Exp(k * step);
            double vk = V(tk);
            if (tk >= t)
            {
                double frac = Math.Log(t / prevT) / Math.Log(tk / prevT);
                double vt = !double.IsFinite(prev) || !double.IsFinite(vk) ? double.PositiveInfinity
                    : prev > 0 && vk > 0 ? Math.Exp(Math.Log(prev) + (Math.Log(vk) - Math.Log(prev)) * frac)
                    : prev + (vk - prev) * frac;
                return Math.Min(min, vt);
            }
            min = Math.Min(min, vk);
            prev = vk;
            prevT = tk;
        }
    }

    /// <summary>
    /// The three radii at this source time (cm), with ruling R-P2-4's confinement weight and the reverse shock's running-minimum
    /// Lagrangian speed; null before <see cref="StartTimeS"/> or for an outcome with no ejecta.
    /// </summary>
    public static ShockRadii? At(Outcome o, double sourceTimeS)
    {
        if (SetupFor(o) is not { } p || !(sourceTimeS >= p.StartS)) return null;
        var (f, c, r) = Radii(p, sourceTimeS);
        if (!(f > 0)) return null;
        return new ShockRadii(f, c, r)
        {
            ConfinementWeight = ConfinementWeight(sourceTimeS, p.StartS),
            ReverseLagrangianCmS = LagrangianSpeed(p, sourceTimeS),
        };
    }

    /// <summary>Ruling R-P2-4: w = clamp(log10(t / t_start), 0, 1).</summary>
    public static double ConfinementWeight(double sourceTimeS, double startS)
        => startS > 0 ? Math.Clamp(Math.Log10(sourceTimeS / startS), 0, 1) : 1;
}
