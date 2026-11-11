using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Outcomes;

/// <summary>
/// Outcomes §1.5, §6 and §8.2 step 8: compact-remnant masses and birth properties. The engine is deterministic, so a
/// distribution is shown by its median or mean, with its spread as the Quantity's range.
/// </summary>
public static class RemnantModel
{
    private const string MSun = "M☉";

    /// <summary>
    /// §8.2 step 8: a neutron star heavier than ~2.2 M☉ (gravitational) becomes a black hole. The observed lower bound on
    /// the maximum is 2.08 ± 0.07 M☉ (Fonseca et al. 2021).
    /// </summary>
    public const double MaxNeutronStarMass = 2.2;

    private static readonly string LattimerYahil = "Lattimer & Yahil 1989, ApJ 340, 426 (via Fryer et al. 2012, ApJ 749, 91, eq. 13)";

    /// <summary>Lattimer & Yahil inverse (outcomes §1.5): M_grav = (−1 + √(1 + 0.3 M_bary)) / 0.15.</summary>
    public static double GravitationalMass(double baryonicMass) => (-1 + Math.Sqrt(1 + 0.3 * baryonicMass)) / 0.15;

    /// <summary>Lattimer & Yahil (outcomes §1.5): M_bary = M_grav + 0.075 M_grav².</summary>
    public static double BaryonicMass(double gravitationalMass) => gravitationalMass + 0.075 * gravitationalMass * gravitationalMass;

    /// <summary>
    /// The remnant of a proto-neutron star with this baryonic mass: a neutron star, or a black hole above
    /// <see cref="MaxNeutronStarMass"/> (step 8).
    /// </summary>
    public static Remnant FromBaryonicMass(Quantity baryonicMass, bool smallKick = false)
    {
        var grav = new Quantity(GravitationalMass(baryonicMass.Value), MSun, baryonicMass.Confidence,
            new Source($"{baryonicMass.Source.Citation}; gravitational mass from {LattimerYahil}",
                $"{baryonicMass.Source.ResearchRef}; outcomes §1.5"));
        return NeutronStar(grav, smallKick);
    }

    /// <summary>
    /// A neutron star of this gravitational mass with the §6 birth properties, or a black hole when the mass is above
    /// <see cref="MaxNeutronStarMass"/>. <paramref name="smallKick"/> is for electron-capture and ultra-stripped
    /// neutron stars (§6: the slow σ = 77 km/s component only).
    /// </summary>
    public static Remnant NeutronStar(Quantity gravitationalMass, bool smallKick = false)
    {
        if (gravitationalMass.Value > MaxNeutronStarMass)
        {
            // The black hole keeps the baryonic mass, as S16 quote their black-hole masses (outcomes §1.5).
            double bary = BaryonicMass(gravitationalMass.Value);
            var mass = new Quantity(bary, MSun, Weakest(gravitationalMass.Confidence, Confidence.ModelDependent),
                new Source($"{gravitationalMass.Source.Citation}; above the neutron-star maximum " +
                           "(Fonseca et al. 2021, ApJL 915, L12)", "outcomes §1.5, §6, §8.2 step 8"));
            return BlackHole(mass, FallbackFormationTime(), BlackHoleKick(bary, directCollapse: false));
        }

        return new Remnant(RemnantKind.NeutronStar, gravitationalMass, NeutronStarRadius(), SpinPeriod(), MagneticField(),
            NeutronStarKick(smallKick), null);
    }

    /// <summary>A black hole of this mass, formed at <paramref name="formationTime"/> after bounce.</summary>
    public static Remnant BlackHole(Quantity mass, Quantity formationTime, Quantity kick)
        => new(RemnantKind.BlackHole, mass, null, null, null, kick, formationTime);

    /// <summary>A white dwarf: the star itself, its radius in km.</summary>
    public static Remnant WhiteDwarf(PresupernovaStar star)
    {
        var r = star.RadiusCm;
        return new Remnant(RemnantKind.WhiteDwarf, star.FinalMass,
            new Quantity(r.Value / 1e5, "km", r.Confidence, r.Source, r.Low / 1e5, r.High / 1e5), null, null, null, null);
    }

    // ---------------------------------------------------------------- black-hole formation time (signals §1.3)

    private const double OConnorOttMin = 0.23, OConnorOttMax = 3.4;

    /// <summary>
    /// t_BH for a core that collapses without exploding: the free-fall estimate t_ff = 0.241 ξ_2.5^−1.5 s (O'Connor & Ott
    /// 2011), good to about a factor of 2, when the compactness is known. Its confidence is no better than
    /// <paramref name="atBest"/>, the confidence of the core structure it came from. Otherwise 1.0 s.
    /// </summary>
    public static Quantity DirectCollapseTime(double? compactness, Confidence atBest = Confidence.ModelDependent)
    {
        if (compactness is double xi && xi > 0)
        {
            double t = 0.241 * Math.Pow(xi, -1.5);
            return new Quantity(t, "s", Weakest(atBest, Confidence.ModelDependent),
                new Source("O'Connor & Ott 2011, ApJ 730, 70 (t_ff = 0.241 ξ_2.5^-1.5 s, ±×2)", "signals §1.3"), t / 2, t * 2);
        }
        // [APPROX] No compactness for this core: 1.0 s, inside the 0.23–3.4 s simulated range of O'Connor & Ott 2011.
        return new Quantity(1.0, "s", Confidence.Speculative,
            new Source("O'Connor & Ott 2011, ApJ 730, 70 (simulated range 0.23–3.4 s) [APPROX]", "signals §1.3"),
            OConnorOttMin, OConnorOttMax);
    }

    /// <summary>
    /// [APPROX] t_BH when the black hole forms by fallback after an explosion: 3.0 s, inside the 0.23–3.4 s range of
    /// O'Connor & Ott 2011 and after the shock has been revived, so the neutrino cooling signal is cut late.
    /// </summary>
    public static Quantity FallbackFormationTime() => new(3.0, "s", Confidence.Speculative,
        new Source("O'Connor & Ott 2011, ApJ 730, 70 (simulated range 0.23–3.4 s) [APPROX, fallback]", "signals §1.3"),
        OConnorOttMin, OConnorOttMax);

    // ---------------------------------------------------------------- kicks (outcomes §6)

    // Verbunt, Igoshev & Cator 2017: two Maxwellians, weight 0.42 with σ = 77 km/s and 0.58 with σ = 320 km/s. The mean
    // 3D speed of a Maxwellian is σ √(8/π), so the mixture's mean is √(8/π) (0.42 × 77 + 0.58 × 320) = 347.78 km/s.
    private const double KickSigmaSlow = 77, KickSigmaFast = 320, KickWeightSlow = 0.42;
    private static readonly double MaxwellMeanFactor = Math.Sqrt(8 / Math.PI);
    private static double MeanNeutronStarKick
        => MaxwellMeanFactor * (KickWeightSlow * KickSigmaSlow + (1 - KickWeightSlow) * KickSigmaFast);

    private static Quantity NeutronStarKick(bool small)
    {
        double slow = MaxwellMeanFactor * KickSigmaSlow, fast = MaxwellMeanFactor * KickSigmaFast;
        if (small)
            // [APPROX] §6: electron-capture and ultra-stripped neutron stars get smaller kicks; draw from the slow
            // component only, shown by its mean.
            return new Quantity(slow, "km/s", Confidence.Speculative,
                new Source("Verbunt, Igoshev & Cator 2017, A&A 608, A57 (σ = 77 km/s component); Podsiadlowski et al. " +
                           "2004, ApJ 612, 1044 [APPROX]", "outcomes §6"));
        return new Quantity(MeanNeutronStarKick, "km/s", Confidence.Established,
            new Source("Verbunt, Igoshev & Cator 2017, A&A 608, A57 (mean of the two-Maxwellian fit)", "outcomes §6, §8.2 step 8"),
            slow, fast);
    }

    /// <summary>
    /// [APPROX] outcomes §6: momentum-conserving v_BH ≈ v_NS (1.4 M☉ / M_BH), with v_NS the mean neutron-star kick; zero
    /// for a direct collapse with no ejecta.
    /// </summary>
    public static Quantity BlackHoleKick(double blackHoleMass, bool directCollapse)
    {
        var src = new Source("Momentum-scaled neutron-star kick (Fryer et al. 2012-based population synthesis; " +
                             "Verbunt et al. 2017) [APPROX]", "outcomes §6, §8.2 step 8");
        double v = directCollapse || blackHoleMass <= 0 ? 0 : MeanNeutronStarKick * 1.4 / blackHoleMass;
        return new Quantity(v, "km/s", Confidence.Speculative, src);
    }

    // ---------------------------------------------------------------- radius, spin and field (outcomes §6)

    // Raaijmakers et al. 2021: R_1.4 = 12.33 (+0.76 / −0.81) km, 95% credible; §6 and step 8 recommend 12 km.
    private static Quantity NeutronStarRadius() => new(12, "km", Confidence.Established,
        new Source("Raaijmakers et al. 2021, ApJL 918, L29 (NICER + multimessenger)", "outcomes §6, §8.2 step 8"), 11.52, 13.09);

    // Igoshev et al. 2022 log-normals: ⟨log10(P0/s)⟩ = −1.04, σ = 0.53 (median ≈ 91 ms); ⟨log10(B/G)⟩ = 12.44, σ = 0.44.
    // Shown by the median, with the ±1σ range.
    private static readonly Source Igoshev = new("Igoshev et al. 2022, MNRAS 514 (arXiv:2205.06823), log-normal median ±1σ",
        "outcomes §6, §8.2 step 8");

    private static Quantity SpinPeriod()
        => new(Math.Pow(10, -1.04), "s", Confidence.Established, Igoshev, Math.Pow(10, -1.04 - 0.53), Math.Pow(10, -1.04 + 0.53));

    private static Quantity MagneticField()
        => new(Math.Pow(10, 12.44), "G", Confidence.Established, Igoshev, Math.Pow(10, 12.44 - 0.44), Math.Pow(10, 12.44 + 0.44));

    /// <summary>The least certain of the given confidences.</summary>
    internal static Confidence Weakest(params Confidence[] confidences) => confidences.Max();
}
