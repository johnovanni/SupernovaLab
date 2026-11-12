using System.Globalization;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>
/// The young supernova remnant (signals §7, §11 "Remnant"): the blast wave's radius, its size on the sky and its radio
/// brightness at 1 GHz, from about 10 to 1,000 years.
/// <list type="bullet">
/// <item>Radius (§7.1–7.2): Truelove &amp; McKee's scales R_ch = 3.4 pc M_ej^(1/3) n0^(−1/3) and t_ch = 473 yr E51^(−1/2)
/// M_ej^(5/6) n0^(−1/3), with Leahy &amp; Williams' uniform-ejecta (n = 0) interpolation from free expansion to
/// Sedov–Taylor, R* = [(2.01 t*)^(−2.5) + (2.026 t*²)^(−0.5)]^(−0.4). [APPROX] A uniform ambient density n0 = 1 cm⁻³
/// (ρ0 = n0 m_p): the lab has no ambient-density input, and young core-collapse remnants such as Cas A are really still
/// inside the progenitor's wind (§7.2; the wind solutions are not transcribed).</item>
/// <item>Size on the sky (§11): θ = 2R / D_A. <see cref="ResolvableTimeS"/> is when θ reaches 0.5″ (Chandra).</item>
/// <item>Radio (§11): calibrated to Cas A, 2,300 Jy at 1 GHz at 3.4 kpc (§7.3), with its spectral index 0.77, for every
/// remnant at every age [APPROX]. Cas A is the brightest young remnant: S D² from §7.3's table puts Tycho, Kepler,
/// SN 1006 and G1.9+0.3 about 30 to 600 times lower at 1 GHz [DERIVED], so this is an upper-end estimate. Cas A's present fading (about 1% per year at 1 GHz,
/// Baars et al. 1977) is not extrapolated.</item>
/// <item>Detection: the radio continuum against the VLA's and MeerKAT's 0.03 mJy and the SKA's 0.01 mJy (ruling R40).</item>
/// </list>
/// Applies to every explosion that throws out ejecta, Type Ia included (Tycho, Kepler and SN 1006 are Type Ia remnants).
/// Source time throughout. Speculative (rulings R24/R27): n0 and the radio calibration are [APPROX].
/// </summary>
public sealed class RemnantEvolutionModel : ISignalModel
{
    // ---------------------------------------------------------------- constants

    private const double Year = PhysicalConstants.Year, Pc = PhysicalConstants.Pc;

    /// <summary>§11: the remnant view starts at about 10 yr.</summary>
    public const double StartS = 10 * Year;

    /// <summary>§11: to 1,000 yr.</summary>
    public const double EndS = 1000 * Year;

    /// <summary>[APPROX] Ambient density, H atoms per cm³: n0 = 1, the research's reference value.</summary>
    public const double AmbientDensity = 1;

    /// <summary>Proton mass, g (signals §0.2); ρ0 = n0 m_p as Leahy &amp; Williams write it (§7.1).</summary>
    private const double ProtonMass = 1.6726e-24;

    /// <summary>§7.2 n = 0 constants (Leahy &amp; Williams 2017, Tables 1 and 3, s = 0).</summary>
    private const double LambdaB = 2.01, Xi = 2.026, Alpha = 1.25;

    /// <summary>§7.2: ξ^(1/5) = 1.152, the Sedov–Taylor coefficient.</summary>
    private const double SedovCoefficient = 1.152;

    /// <summary>§11: a remnant is resolvable above about 0.5″ (Chandra).</summary>
    public const double ResolvableArcsec = 0.5;

    /// <summary>Cas A (§7.3, Green 2024): 2,300 Jy at 1 GHz, 3.4 kpc (Reed et al. 1995), S ∝ ν^(−0.77).</summary>
    private const double CasAJy = 2300, CasADistancePc = 3400, CasASpectralIndex = 0.77;

    private const double ArcsecPerRadian = 180 / Math.PI * 3600, Jansky = 1e-23;

    /// <summary>The continuum radio detectors (ruling R40).</summary>
    public static IReadOnlyList<string> DetectorIds { get; } = new[] { "vla", "meerkat", "ska" };

    // ---------------------------------------------------------------- ISignalModel

    public string Id => "remnant";
    public Messenger Messenger => Messenger.Radio;
    public Confidence Confidence => Confidence.Speculative;

    public IReadOnlyList<SignalChannel> Channels { get; } = new SignalChannel[]
    {
        new("radiusPc", "Blast-wave radius", "pc", true),
        new("angularSizeArcsec", "Angular diameter on the sky", "arcsec", true),
        new("radio1GHzJy", "Radio flux density at 1 GHz (calibrated to Cas A)", "Jy", true),
    };

    public IReadOnlyList<Source> Sources { get; } = new Source[]
    {
        new("Truelove & McKee 1999, ApJS 120, 299 (R_ch, t_ch); Leahy & Williams 2017, AJ 153, 239 (uniform-ejecta " +
            "interpolation, λ = 2.01, ξ = 2.026, α = 1.25); ambient n0 = 1 cm⁻³ [APPROX]; young core-collapse remnants " +
            "really expand into the progenitor's wind (Chevalier & Oishi 2003, not modeled)", "signals §7.1, §7.2"),
        new("θ = 2R/D; resolvable above about 0.5″ (Chandra)", "signals §11"),
        new("Radio calibrated to Cas A: 2,300 Jy at 1 GHz, 3.4 kpc, α = 0.77 (Green 2024; Reed et al. 1995), for every " +
            "remnant [APPROX] — an upper-end value: Tycho, Kepler, SN 1006 and G1.9+0.3 are about 30–600 times less luminous " +
            "(S D² from the same table [DERIVED])", "signals §7.3, §11"),
        new("VLA and MeerKAT 0.03 mJy (5σ), SKA 0.01 mJy (ruling R40) [APPROX]", "signals §6.1, §11"),
    };

    public bool Applies(Outcome outcome) => AppliesTo(outcome);

    public (double StartS, double EndS) ValidRange(Outcome outcome) => AppliesTo(outcome) ? (StartS, EndS) : (0, 0);

    public double Sample(Outcome outcome, ObserverSetup observer, double sourceTimeS, string channelId)
    {
        if (channelId is not ("radiusPc" or "angularSizeArcsec" or "radio1GHzJy"))
            throw new ArgumentException($"The remnant model has no channel '{channelId}'.", nameof(channelId));
        if (!AppliesTo(outcome) || !(sourceTimeS >= StartS && sourceTimeS <= EndS)) return double.NaN;
        return channelId switch
        {
            "radiusPc" => RadiusCm(outcome, sourceTimeS) / Pc,
            "angularSizeArcsec" => AngularSizeArcsec(outcome, observer, sourceTimeS),
            _ => RadioJy(observer),
        };
    }

    public Detection Detect(Outcome outcome, ObserverSetup observer, double sourceTimeS, Detector detector)
    {
        if (!AppliesTo(outcome)) return new Detection(DetectionStatus.NotProduced, NotProducedReason(outcome));
        double z1 = observer.TimeDilation;
        if (!DetectorIds.Contains(detector.Id))
        {
            double resolvable = ResolvableTimeS(outcome, observer);
            string when = double.IsPositiveInfinity(resolvable)
                ? "it stays smaller than 0.5″ on the sky for 1,000 years at this distance"
                : $"it grows past 0.5″ on the sky, enough for Chandra to resolve it, about {UnitFormat.Time(resolvable * z1)} " +
                  "after the explosion";
            return new Detection(DetectionStatus.NotModeled,
                $"The remnant's brightness for {detector.Name} is not modeled; {when}.");
        }
        if (!(sourceTimeS >= StartS))
            return new Detection(DetectionStatus.NotYet,
                $"The remnant view starts {UnitFormat.Time(StartS * z1)} after the explosion; before that, the shock's radio " +
                "glow is in the circumstellar-interaction view.");
        if (sourceTimeS > EndS)
            return new Detection(DetectionStatus.NotModeled, $"The model ends {UnitFormat.Time(EndS * z1)} after the explosion.");

        double mJy = RadioJy(observer) * 1000;
        var status = mJy >= detector.Threshold ? DetectionStatus.Detected : DetectionStatus.TooFaint;
        var inv = CultureInfo.InvariantCulture;
        return new Detection(status,
            $"{detector.Name} receives about {UnitFormat.Figure(mJy)} mJy at 1 GHz against its limit of about " +
            $"{detector.Threshold.ToString("0.##", inv)} mJy: {(status == DetectionStatus.Detected ? "detected" : "too faint")}. " +
            "The brightness assumes a remnant as radio-bright as Cas A, the brightest young remnant; most are tens to " +
            $"hundreds of times fainter. It is about {FormatArcsec(AngularSizeArcsec(outcome, observer, sourceTimeS))} across.");
    }

    // ---------------------------------------------------------------- markers

    /// <summary>
    /// When the remnant's angular diameter reaches 0.5″ (§11, Chandra), s after the explosion (source time), solved on
    /// the §7.2 radius (bisection in log t). +∞ when it stays smaller through 1,000 yr; NaN when the model does not apply.
    /// </summary>
    public double ResolvableTimeS(Outcome o, ObserverSetup obs)
    {
        if (!AppliesTo(o)) return double.NaN;
        double lo = 1, hi = EndS;
        if (AngularSizeArcsec(o, obs, hi) < ResolvableArcsec) return double.PositiveInfinity;
        if (AngularSizeArcsec(o, obs, lo) >= ResolvableArcsec) return lo;
        for (int i = 0; i < 100; i++)
        {
            double mid = Math.Sqrt(lo * hi);
            if (AngularSizeArcsec(o, obs, mid) < ResolvableArcsec) lo = mid;
            else hi = mid;
        }
        return hi;
    }

    // ---------------------------------------------------------------- public formulas (tested against signals §7)

    /// <summary>§7.1: R_ch = 3.4 pc (M_ej/M☉)^(1/3) n0^(−1/3).</summary>
    public static double CharacteristicRadiusPc(double ejectaMsun, double ambientDensity)
        => 3.4 * Math.Cbrt(ejectaMsun / ambientDensity);

    /// <summary>§7.1: t_ch = 473 yr (E/1e51 erg)^(−1/2) (M_ej/M☉)^(5/6) n0^(−1/3), in s.</summary>
    public static double CharacteristicTimeS(double energyErg, double ejectaMsun, double ambientDensity)
        => 473 * Year / Math.Sqrt(energyErg / 1e51) * Math.Pow(ejectaMsun, 5.0 / 6) / Math.Cbrt(ambientDensity);

    /// <summary>§7.2 (Leahy &amp; Williams, n = 0): R* = [(λ_b t*)^(−2α) + (ξ t*²)^(−2α/5)]^(−1/(2α)).</summary>
    public static double ScaledRadius(double scaledTime)
        => Math.Pow(Math.Pow(LambdaB * scaledTime, -2 * Alpha) + Math.Pow(Xi * scaledTime * scaledTime, -2 * Alpha / 5),
            -1 / (2 * Alpha));

    /// <summary>§7.2 Sedov–Taylor limit: R = 1.152 (E t² / ρ0)^(1/5), ρ0 = n0 m_p; returned in pc (E in erg, t in s).</summary>
    public static double SedovRadiusPc(double energyErg, double ambientDensity, double timeS)
        => SedovCoefficient * Math.Pow(energyErg * timeS * timeS / (ambientDensity * ProtonMass), 0.2) / Pc;

    // ---------------------------------------------------------------- the model

    private static bool AppliesTo(Outcome o)
        => o.Fate is Fate.Supernova or Fate.PairInstabilitySupernova
           && o.ExplosionEnergyErg is { Value: > 0 } && o.EjectaMass is { Value: > 0 };

    /// <summary>
    /// The blast wave's radius, cm, at source time t (s), from the §7.2 interpolation with n0 = <see cref="AmbientDensity"/>,
    /// for an outcome the model applies to (<see cref="Applies"/>); the 3D view uses this same radius (Task 16).
    /// </summary>
    public static double RadiusCm(Outcome o, double t)
    {
        double m = o.EjectaMass!.Value, e = o.ExplosionEnergyErg!.Value;
        return CharacteristicRadiusPc(m, AmbientDensity) * Pc * ScaledRadius(t / CharacteristicTimeS(e, m, AmbientDensity));
    }

    /// <summary>θ = 2R / D_A, arcsec, with the angular-diameter distance D_A = D / (1 + z) (D the comoving distance).</summary>
    private static double AngularSizeArcsec(Outcome o, ObserverSetup obs, double t)
        => 2 * RadiusCm(o, t) / (obs.DistancePc * Pc / obs.TimeDilation) * ArcsecPerRadian;

    /// <summary>
    /// The 1 GHz flux density at Earth, Jy: Cas A's L_ν = 2,300 Jy × 4π (3.4 kpc)² at 1 GHz, S ∝ ν^(−0.77), observed at
    /// 1 GHz from (1 + z) GHz in the source frame: S = (1 + z) L_ν((1 + z) GHz) / (4π D_L²).
    /// </summary>
    private static double RadioJy(ObserverSetup obs)
    {
        double lnu = CasAJy * Jansky * 4 * Math.PI * Math.Pow(CasADistancePc * Pc, 2);
        double z1 = obs.TimeDilation;
        return z1 * lnu * Math.Pow(z1, -CasASpectralIndex) / (4 * Math.PI * Math.Pow(obs.LuminosityDistanceCm, 2)) / Jansky;
    }

    // ---------------------------------------------------------------- text

    private static string FormatArcsec(double arcsec)
    {
        var inv = CultureInfo.InvariantCulture;
        return arcsec >= 120 ? $"{(arcsec / 60).ToString("0.#", inv)}′"
            : arcsec >= 0.1 ? $"{arcsec.ToString("0.##", inv)}″"
            : $"{arcsec.ToString("0.##e0", inv)}″";
    }

    private static string NotProducedReason(Outcome o) => o.Fate switch
    {
        Fate.NotDeadYet => "The star has not died yet, so there is no remnant.",
        Fate.WhiteDwarf => "The star ends as a white dwarf with no explosion, so no supernova remnant forms.",
        Fate.FailedSupernova => "No explosion: the core collapses straight to a black hole, so no debris spreads out to make a remnant.",
        Fate.PulsationalPairInstability => "The pulses throw off shells but there is no explosion to drive a remnant's blast wave.",
        _ => "This outcome throws out no debris to make a remnant.",
    };
}
