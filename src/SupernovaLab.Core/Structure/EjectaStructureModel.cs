using System.Globalization;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Structure;

/// <summary>
/// The ejecta's layers, velocity profile and mixing reach for the 3D view (structure §S1, §S2, §S3 and §S10 App blocks;
/// rulings P1–P3, R-P2-12, R-P2-13). Pure and GL-free: <see cref="ParticleSampler"/> turns the result into particles.
/// </summary>
public static class EjectaStructureModel
{
    private const double MSun = PhysicalConstants.MSun, KmS = 1e5;

    /// <summary>Ruling P1: particles are truncated at the speed enclosing this fraction of the mass.</summary>
    public const double TruncationFraction = 0.999;

    /// <summary>
    /// Ruling R-P2-18 (after Project 1's R28): above this fraction of c the Newtonian profile's v_max gets a scene note;
    /// it is not clamped (inconsistency #42).
    /// </summary>
    public const double RelativisticNoteBeta = 0.3;

    /// <summary>Ruling P1: the director frames the radius enclosing this fraction of the particle mass.</summary>
    public const double FrameFraction = 0.99;

    /// <summary>The inner slope δ for every class (§S1: Chevalier &amp; Fransson 2017, Kasen 2010).</summary>
    public const double InnerSlope = 1;

    /// <summary>§S2.2 (Mazzali et al. 2007): stable iron group (⁵⁴Fe + ⁵⁸Ni) in a near-Chandrasekhar Type Ia, M☉.</summary>
    public const double StableIronGroupMsun = 0.20;

    /// <summary>§S2.2 (Mazzali et al. 2007): total burned mass (NSE + IME) of a near-Chandrasekhar Type Ia, M☉.</summary>
    public const double BurnedMassMsun = 1.05;

    /// <summary>§S2 App block: a Type Ia at least this heavy (M☉) is near-Chandrasekhar.</summary>
    public const double NearChandrasekharMinMsun = 1.3;

    private static readonly Source ProfileSource = new(
        "Broken power law: Chevalier & Fransson 2017 §2.1; Matzner & McKee 1999 (n = 11.73 → 12, 10.18); Kasen 2010, "
        + "ApJ 708, 1025 (Type Ia n = 10)", "structure §S1");

    private const string ShellLabel = "weak envelope ejection, Lovegrove & Woosley 2013; Fernández et al. 2018";

    // ---------------------------------------------------------------- class and profile (§S1)

    /// <summary>
    /// The §S1 class: thermonuclear → <see cref="EjectaClass.TypeIa"/>; otherwise the breakout model's test,
    /// hydrogen-rich (<see cref="ShockBreakoutModel.IsHydrogenRich"/>) and R ≥ 158 R☉ → red supergiant, other
    /// hydrogen-rich → blue supergiant, not hydrogen-rich → stripped. The outcome must have a star unless thermonuclear.
    /// </summary>
    public static EjectaClass ClassOf(Outcome o)
    {
        if (IsThermonuclear(o)) return EjectaClass.TypeIa;
        if (!ShockBreakoutModel.IsHydrogenRich(o)) return EjectaClass.StrippedEnvelope;
        return o.Star!.RadiusCm.Value / PhysicalConstants.RSun >= ShockBreakoutModel.ConvectiveMinRadiusRsun
            ? EjectaClass.RedSupergiant
            : EjectaClass.BlueSupergiant;
    }

    /// <summary>The outer slope n by class (§S1 table): 12, 10.18, 10.18, 10.</summary>
    public static double OuterSlope(EjectaClass c) => c switch
    {
        EjectaClass.RedSupergiant => 12,
        EjectaClass.TypeIa => 10,
        _ => 10.18,
    };

    /// <summary>
    /// §S1.2: v_t = [2 (5−δ)(n−5) / ((3−δ)(n−3)) × E/M]^{1/2}, cm/s, for E in erg and M in M☉.
    /// </summary>
    public static double TransitionVelocityCmS(double delta, double n, double energyErg, double massMsun)
        => Math.Sqrt(2 * (5 - delta) * (n - 5) / ((3 - delta) * (n - 3)) * energyErg / (massMsun * MSun));

    /// <summary>
    /// §S1.3: the fraction of the mass moving slower than <paramref name="vCmS"/>, for the untruncated profile:
    /// m_t (v/v_t)^(3−δ) inside v_t, 1 − (3−δ)/(n−δ) (v/v_t)^(3−n) outside, with m_t = (n−3)/(n−δ). For a §S10 shell, 0
    /// below v_s and 1 from v_s.
    /// </summary>
    public static double EnclosedMassFraction(EjectaStructure s, double vCmS)
    {
        if (s.IsEnvelopeShell) return vCmS >= s.MaxCmS ? 1 : 0;
        if (!(vCmS > 0)) return 0;
        double d = s.Delta, n = s.N, x = vCmS / s.TransitionCmS;
        return x <= 1
            ? (n - 3) / (n - d) * Math.Pow(x, 3 - d)
            : 1 - (3 - d) / (n - d) * Math.Pow(x, 3 - n);
    }

    /// <summary>
    /// §S1.3's inverse: the speed (cm/s) enclosing a mass fraction q, v_t (q/m_t)^{1/(3−δ)} for q ≤ m_t and
    /// v_t [(1−q)(n−δ)/(3−δ)]^{−1/(n−3)} above. 0 for q ≤ 0, +∞ for q ≥ 1. For a §S10 shell, v_s.
    /// </summary>
    public static double VelocityAtMassFraction(EjectaStructure s, double q)
    {
        if (s.IsEnvelopeShell) return s.MaxCmS;
        if (!(q > 0)) return 0;
        if (q >= 1) return double.PositiveInfinity;
        double d = s.Delta, n = s.N, mt = (n - 3) / (n - d);
        return q <= mt
            ? s.TransitionCmS * Math.Pow(q / mt, 1 / (3 - d))
            : s.TransitionCmS * Math.Pow((1 - q) * (n - d) / (3 - d), -1 / (n - 3));
    }

    // ---------------------------------------------------------------- the structure

    [ThreadStatic] private static int t_buildCount;

    /// <summary>
    /// How many times <see cref="For"/> has run on the calling thread (a test hook, final review I2: building a structure bisects a
    /// 20,000-cell grid, so the 3D view's per-frame <c>ExplosionScene.At</c> must never call it). Per thread, so parallel tests do
    /// not disturb one another.
    /// </summary>
    public static int BuildCountOnThisThread => t_buildCount;

    /// <summary>
    /// The ejecta's structure, or null without ejecta (not dead yet, a white dwarf, the pulses of pulsational pair
    /// instability). A failed supernova gives §S10's weak envelope shell (see <see cref="EjectaStructure"/>).
    /// <para>Frame99CmS is computed, not measured on a particle set: it is the expectation, over the seeded finger
    /// directions and the particles' u, of the speed enclosing 99% of the particle mass after mixing (particle i sits at
    /// v(0.999 q_i), so without mixing it is v(0.999 × 0.99)). The sampled set's own 99% speed agrees to 0.3% at 200,000
    /// particles (ParticleSamplerTests).</para>
    /// </summary>
    public static EjectaStructure? For(Outcome o)
    {
        ArgumentNullException.ThrowIfNull(o);
        t_buildCount++;
        if (o.Fate == Fate.FailedSupernova) return EnvelopeShell(o);
        if (!EjectaKinematics.HasEjecta(o)) return null;
        if (!IsThermonuclear(o) && o.Star is null) return null;

        double energy = o.ExplosionEnergyErg!.Value, ejecta = o.EjectaMass!.Value;
        var cls = ClassOf(o);
        double n = OuterSlope(cls), vt = TransitionVelocityCmS(InnerSlope, n, energy, ejecta);
        double vmax = vt * Math.Pow((3 - InnerSlope) / ((n - InnerSlope) * (1 - TruncationFraction)), 1 / (n - 3));

        var notes = new List<string>();
        if (vmax > RelativisticNoteBeta * PhysicalConstants.C)
            notes.Add(string.Format(CultureInfo.InvariantCulture,
                "The fastest debris would move at {0:0.00}c; the lab's profile is not relativistic", vmax / PhysicalConstants.C));
        double scale;
        IReadOnlyList<LayerMass> layers = cls == EjectaClass.TypeIa
            ? TypeIaLayers(ejecta, o.NickelMass?.Value ?? 0, notes, out scale)
            : CoreCollapseLayers(o, ejecta, notes, out scale);

        var s = new EjectaStructure(layers, ejecta, energy, InnerSlope, n, vt, vmax, double.NaN, MixingFor(o, cls, vt), scale,
            notes.ToArray()) { Class = cls };
        return s with { Frame99CmS = MixedFrameSpeedCmS(s) };
    }

    /// <summary>
    /// §S2 App block, core collapse and pair instability (rulings P2, R-P2-12): H, He from the star; remnant baryonic
    /// mass by kind; CO/Si = max(0, M_CO − rem − Ni); the non-nickel layers scaled by one factor s so the total is M_ej.
    /// </summary>
    private static IReadOnlyList<LayerMass> CoreCollapseLayers(Outcome o, double ejecta, List<string> notes, out double scale)
    {
        var star = o.Star!;
        double rem = o.Remnant.Kind switch
        {
            RemnantKind.NeutronStar or RemnantKind.Magnetar => RemnantModel.BaryonicMass(o.Remnant.Mass!.Value),
            RemnantKind.BlackHole => o.Remnant.Mass!.Value,
            _ => 0,
        };
        double ni = Math.Max(0, o.NickelMass?.Value ?? 0);
        double h = Math.Max(0, star.HydrogenEnvelopeMass.Value), he = Math.Max(0, star.HeliumEnvelopeMass.Value);
        double co = Math.Max(0, star.CoCoreMass.Value - rem - ni);

        if (ni >= ejecta)
        {
            scale = 0;
            return new[] { new LayerMass(ElementLayer.NickelIron, ejecta) };
        }
        double rest = h + he + co;
        if (!(rest > 0))
        {
            // [APPROX] Only reachable with pinned masses: the star leaves nothing outside the remnant but nickel, yet the
            // published ejecta are heavier. The excess is drawn as the metal core.
            co = 1;
            rest = 1;
            notes.Add("Layer masses: the star leaves no layer outside the remnant but nickel; the rest of the published "
                + "ejecta mass is drawn as oxygen/silicon [APPROX]");
        }
        scale = (ejecta - ni) / rest;
        if (scale < 0.9 || scale > 1.1)
            notes.Add(string.Format(CultureInfo.InvariantCulture,
                "Layer masses scaled by ×{0:0.00} to match the published ejecta mass", scale));

        var layers = new List<LayerMass>(4);
        Add(layers, ElementLayer.NickelIron, ni);
        Add(layers, ElementLayer.CarbonOxygenCore, scale * co);
        Add(layers, ElementLayer.Helium, scale * he);
        Add(layers, ElementLayer.Hydrogen, scale * h);
        return layers;
    }

    /// <summary>
    /// §S2 App block, Type Ia (Mazzali et al. 2007): near-Chandrasekhar (M ≥ 1.3 M☉) Fe+Ni = min(M, 0.20 + Ni),
    /// IME = max(0, min(M − Fe+Ni, 1.05 − Fe+Ni)), CO the rest; sub-Chandrasekhar and Iax [APPROX] Fe+Ni =
    /// min(M, 0.145 M + Ni), CO = min(M − Fe+Ni, 0.24 M), IME the rest.
    /// </summary>
    private static IReadOnlyList<LayerMass> TypeIaLayers(double m, double ni, List<string> notes, out double scale)
    {
        scale = 1;
        ni = Math.Max(0, ni);
        double fe, ime, co;
        if (m >= NearChandrasekharMinMsun)
        {
            fe = Math.Min(m, StableIronGroupMsun + ni);
            ime = Math.Max(0, Math.Min(m - fe, BurnedMassMsun - fe));
            co = m - fe - ime;
        }
        else
        {
            // The App block's rounded fractions: 0.20/1.38 = 0.145 and 0.33/1.38 = 0.24.
            fe = Math.Min(m, 0.145 * m + ni);
            co = Math.Min(m - fe, 0.24 * m);
            ime = m - fe - co;
            notes.Add("Layer masses for sub-Chandrasekhar and Type Iax explosions are schematic: the near-Chandrasekhar "
                + "fractions of the ejecta mass (structure §S2.2) [APPROX]");
        }
        var layers = new List<LayerMass>(3);
        Add(layers, ElementLayer.NickelIron, fe);
        Add(layers, ElementLayer.IntermediateMass, ime);
        Add(layers, ElementLayer.UnburnedCarbonOxygen, co);
        return layers;
    }

    private static void Add(List<LayerMass> layers, ElementLayer layer, double mass)
    {
        if (mass > 0) layers.Add(new LayerMass(layer, mass));
    }

    // ---------------------------------------------------------------- mixing (§S3)

    private static readonly Source RsgMixing = new(
        "Wongwathanarat, Müller & Janka 2015, A&A 577, A48: Ni fingers to 0.82–1.18 v_t, H below 0.22 v_t [derived]; "
        + "K = 8, f = 0.2 and F_Ni = 0.5 [APPROX]", "structure §S3");

    private static readonly Source BsgMixing = new(
        "SN 1987A's fast Ni clump at 1.43 v_t (Utrobin, Chugai & Andronova 1995) and Hammer, Janka & Müller 2010 (1.38 v_t); "
        + "H to ≤ 0.21 v_t (Kozma & Fransson 1998), via Utrobin et al. 2019, A&A 624, A116 [derived]; K = 4 and F_Ni = 0.5 "
        + "(Wongwathanarat, Müller & Janka 2015); f = 0.1 [APPROX]", "structure §S3");

    private static readonly Source StrippedMixing = new(
        "Kifonidis et al. 2003, A&A 408, 621 (one 2D Type Ib-like model): Ni to ≈ 0.4 v_t [derived; APPROX for the class]; "
        + "K = 10 (≈ 10 cusps), f = 0.2 and F_Ni = 0.5 [APPROX]", "structure §S3");

    /// <summary>
    /// Ruling R-P2-17: no Rayleigh–Taylor fingers in a pair-instability supernova. Joggerst &amp; Whalen 2011 find none in
    /// compact progenitors and in most extended ones; two red models mix (the text puts Si reached in u200 and penetrated in u225),
    /// but ⁵⁶Ni never reaches the outer layers (inconsistency #41, disclosed rather than drawn).
    /// </summary>
    private static readonly MixingReach PairInstabilityMixing = MixingReach.None with
    {
        Confidence = Confidence.ModelDependent,
        Source = new Source("Joggerst & Whalen 2011, ApJ 728, 129, arXiv:1010.4360: no mixing in compact and most extended "
            + "pair-instability supernovae; partial mixing in two red models is not drawn", "structure §S3"),
    };

    private static readonly Source Sn1987AMixing = new(
        "SN 1987A observed: a ⁵⁶Ni clump at 4,700 km/s (Utrobin, Chugai & Andronova 1995), hydrogen down to ≤ 700 km/s "
        + "(Kozma & Fransson 1998), via Utrobin et al. 2019, A&A 624, A116; K = 4 and F_Ni = 0.5 (Wongwathanarat, Müller "
        + "& Janka 2015); f = 0.1 [APPROX]", "structure §S3");

    /// <summary>
    /// §S3 App block, one row per class (ruling R-P2-13). SN 1987A-like outcomes (<see cref="Outcome.HasEquatorialRing"/>)
    /// take the observed reach. Every core-collapse row has an [APPROX] covered fraction, so its confidence is
    /// Speculative. Pair instability has no fingers whatever its envelope (ruling R-P2-17: Joggerst &amp; Whalen 2011).
    /// </summary>
    private static MixingReach MixingFor(Outcome o, EjectaClass cls, double vt)
    {
        if (o.Fate == Fate.PairInstabilitySupernova || o.Type == SupernovaType.Pisn) return PairInstabilityMixing;
        if (o.HasEquatorialRing && cls != EjectaClass.TypeIa)
            return new MixingReach(4700 * KmS, 700 * KmS, 4, 0.1, 0.5, Confidence.Speculative, Sn1987AMixing);
        return cls switch
        {
            EjectaClass.RedSupergiant => new MixingReach(1.0 * vt, 0.2 * vt, 8, 0.2, 0.5, Confidence.Speculative, RsgMixing),
            EjectaClass.BlueSupergiant => new MixingReach(1.4 * vt, 0.2 * vt, 4, 0.1, 0.5, Confidence.Speculative, BsgMixing),
            EjectaClass.StrippedEnvelope => new MixingReach(0.4 * vt, double.NaN, 10, 0.2, 0.5, Confidence.Speculative,
                StrippedMixing),
            _ => MixingReach.None,
        };
    }

    /// <summary>Mass cells for the expected post-mixing 99% speed.</summary>
    private const int FrameCells = 20_000;

    /// <summary>
    /// The expected speed enclosing <see cref="FrameFraction"/> of the particle mass after mixing. Particles sit at
    /// v(0.999 q) on stratified q (ruling P1, P3). Mixing (§S3, R-P2-13), with u uniform in [0, 1):
    /// a nickel particle (chance F_Ni of being moved) at v &lt; NickelMax stays below w with probability
    /// √((w − v)/(NickelMax − v)) for v &lt; w &lt; NickelMax; a hydrogen particle inside a cone (chance f, the cones'
    /// union coverage) at v &gt; HydrogenMin falls below w &gt; HydrogenMin with probability 1 − √((v − w)/(v − HydrogenMin)).
    /// The enclosed fraction is averaged over <see cref="FrameCells"/> stratified cells and solved for w by bisection.
    /// </summary>
    private static double MixedFrameSpeedCmS(EjectaStructure s)
    {
        double unmixed = VelocityAtMassFraction(s, TruncationFraction * FrameFraction);
        var m = s.Mixing;
        if (!m.HasFingers) return unmixed;

        var v = new double[FrameCells];
        var layer = new ElementLayer[FrameCells];
        CellSpeeds(s, v, layer);
        double nickelMax = m.NickelMaxCmS, hMin = m.HydrogenMinCmS;
        bool mixNickel = m.NickelFingerFraction > 0 && double.IsFinite(nickelMax);
        bool mixHydrogen = double.IsFinite(hMin);

        // The cells' speeds rise with q, so the unmixed count below w is a binary search; nickel (the innermost layer)
        // can only leave from below w, hydrogen only arrive from above it.
        double Enclosed(double w)
        {
            int below = Array.BinarySearch(v, w);
            below = below >= 0 ? FirstAtLeast(v, w, below) : ~below;
            double sum = below;
            if (mixNickel && w < nickelMax)
                for (int j = 0; j < below && layer[j] == ElementLayer.NickelIron; j++)
                    sum -= m.NickelFingerFraction * (1 - Math.Sqrt((w - v[j]) / (nickelMax - v[j])));
            if (mixHydrogen && w > hMin)
                for (int j = below; j < FrameCells; j++)
                    if (layer[j] == ElementLayer.Hydrogen)
                        sum += m.CoveredFraction * (1 - Math.Sqrt((v[j] - w) / (v[j] - hMin)));
            return sum / FrameCells;
        }

        double lo = 0, hi = Math.Max(s.MaxCmS, mixNickel ? nickelMax : 0);
        for (int it = 0; it < 50 && hi - lo > 1e-7 * hi; it++)
        {
            double mid = 0.5 * (lo + hi);
            if (Enclosed(mid) >= FrameFraction) hi = mid; else lo = mid;
        }
        return hi;
    }

    /// <summary>The first index at or after <paramref name="hint"/>'s run of values equal to w (v sorted ascending).</summary>
    private static int FirstAtLeast(double[] v, double w, int hint)
    {
        while (hint > 0 && v[hint - 1] >= w) hint--;
        return hint;
    }

    /// <summary>Speeds v(0.999 q_j) and layers on q_j = (j + 0.5)/N for N stratified cells (rulings P1, P3).</summary>
    internal static void CellSpeeds(EjectaStructure s, double[] speeds, ElementLayer[] layers)
    {
        int count = speeds.Length;
        var bounds = new double[s.Layers.Count];
        double cumulative = 0;
        for (int k = 0; k < s.Layers.Count; k++)
        {
            cumulative += s.Layers[k].MassMsun;
            bounds[k] = cumulative / s.EjectaMassMsun;
        }
        int layer = 0;
        for (int i = 0; i < count; i++)
        {
            double q = (i + 0.5) / count;
            while (layer < bounds.Length - 1 && q >= bounds[layer]) layer++;
            layers[i] = s.Layers[layer].Layer;
            speeds[i] = VelocityAtMassFraction(s, TruncationFraction * q);
        }
    }

    // ---------------------------------------------------------------- failed supernova (§S10)

    /// <summary>
    /// §S10 App block: the weak envelope ejection of a failed supernova, by §S1 class. Red supergiant
    /// min(4.5 M☉, H envelope) at 75 km/s; blue supergiant (compact) 0.1 M☉ at 700 km/s; stripped (Wolf–Rayet) 10⁻³ M☉ at
    /// 2,300 km/s. E_s = ½ M_s v_s². Launched when Kistler's delay (n = 3/2 for a red supergiant, else 3) for E_s through
    /// the star above the black hole has passed. The shell is the star's outermost layer present: hydrogen when the star
    /// has a hydrogen envelope, else helium, else carbon/oxygen (a Wolf–Rayet star has no hydrogen to throw off).
    /// </summary>
    private static EjectaStructure? EnvelopeShell(Outcome o)
    {
        if (o.Star is not { } star) return null;
        var cls = ClassOf(o);
        double h = star.HydrogenEnvelopeMass.Value;
        (double mass, double speed) = cls switch
        {
            EjectaClass.RedSupergiant => (Math.Min(4.5, h), 75 * KmS),
            EjectaClass.BlueSupergiant => (0.1, 700 * KmS),
            _ => (1e-3, 2300 * KmS),
        };
        if (!(mass > 0)) return null;
        var layer = h > 0 ? ElementLayer.Hydrogen
            : star.HeliumEnvelopeMass.Value > 0 ? ElementLayer.Helium
            : ElementLayer.CarbonOxygenCore;
        double energy = 0.5 * mass * MSun * speed * speed;
        double bh = o.Remnant.Mass?.Value ?? 0;
        double polytrope = cls == EjectaClass.RedSupergiant ? 1.5 : 3;
        double launch = ShockBreakoutModel.KistlerDelayS(polytrope, energy, Math.Max(mass, star.FinalMass.Value - bh),
            star.RadiusCm.Value / PhysicalConstants.RSun, bh);
        return new EjectaStructure(new[] { new LayerMass(layer, mass) }, mass, energy, 0, 0, speed, speed, speed,
            MixingReach.None, 1, new[] { ShellLabel })
        {
            Class = cls,
            EnvelopeShellLaunchS = launch,
        };
    }

    private static bool IsThermonuclear(Outcome o)
        => o.Type is SupernovaType.Ia or SupernovaType.Ia91T or SupernovaType.Ia91bg or SupernovaType.Iax;
}
