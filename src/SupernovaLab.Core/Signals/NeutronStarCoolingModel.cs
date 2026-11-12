using System.Globalization;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>
/// Thermal X-rays from the young neutron star's surface (signals §8.4, §11 "Neutron star thermal X-ray"): its observed
/// surface temperature T_s^∞ and luminosity L^∞, and whether Chandra or XMM-Newton sees it.
/// <list type="bullet">
/// <item>Interior (§8.4, slow modified-Urca cooling, Yakovlev &amp; Pethick 2004): T_i ≈ 1e9 K (t/yr)^(−1/6), taken as
/// the temperature T_b at the bottom of the envelope (the research's worked example does the same).</item>
/// <item>Envelope (Gudmundsson, Pethick &amp; Epstein 1983): T_b = 1.288e8 (T_s6⁴ / g14)^0.455 K, inverted for T_s, with
/// g14 = 2.4 [APPROX, as the research gives it and uses in its worked example].</item>
/// <item>Observed values: T_s^∞ = T_s √(1 − r_g/R) and L^∞ = 4πR²σT_s⁴ (1 − r_g/R), a blackbody at T_s^∞ seen with
/// radius R^∞ = R / √(1 − r_g/R) (§8.4 "Display"), with the remnant's own mass and radius (r_g = 2GM/c²).</item>
/// <item>Band (§8.4: "before about 100 yr ... show a band, not a line"): <see cref="SurfaceTemperatureAt"/> and
/// <see cref="LuminosityAt"/> carry a range before 100 yr [APPROX]: from the 100-yr value (a crust holding its heat
/// until it relaxes) to the relaxed-interior value at that age, which is the plotted line.</item>
/// <item>Detection: the blackbody's 0.3–10 keV flux [APPROX band] after absorption by the interstellar gas
/// (<see cref="Extinction.XrayTransmission"/>), against Chandra's and XMM-Newton's 1e-15 erg cm⁻² s⁻¹ [APPROX].</item>
/// </list>
/// Not modeled: fast (direct Urca) cooling of heavy neutron stars, which drops below 3e5 K by 1e4 yr (§8.4), and any
/// extra heat a magnetar's decaying field may supply. Valid from 30 yr to 1,000 yr (§11). Source time throughout.
/// Speculative (rulings R24/R27): T_i(t) is a single-power inversion, and g14, the band and the thresholds are [APPROX].
/// </summary>
public sealed class NeutronStarCoolingModel : ISignalModel
{
    // ---------------------------------------------------------------- constants

    private const double Year = PhysicalConstants.Year;

    /// <summary>§11: the model starts at about 30 yr.</summary>
    public const double StartS = 30 * Year;

    /// <summary>§11: to 1,000 yr.</summary>
    public const double EndS = 1000 * Year;

    /// <summary>§8.4: before about 100 yr the surface depends on the unrelaxed crust (a band).</summary>
    public const double RelaxedS = 100 * Year;

    /// <summary>[APPROX] §8.4: g14 ≈ 2.4 (1.4 M☉, 10 km), the value the research's worked example uses.</summary>
    public const double SurfaceGravity14 = 2.4;

    /// <summary>[APPROX] The X-ray band, keV, as the other X-ray views use (Swift XRT's 0.3–10 keV).</summary>
    private const double BandLowKeV = 0.3, BandHighKeV = 10;

    /// <summary>Simpson intervals across the band in ln E.</summary>
    private const int BandIntervals = 200;

    /// <summary>The detectors whose sensitivity this model compares against.</summary>
    public static IReadOnlyList<string> DetectorIds { get; } = new[] { "chandra", "xmm" };

    // ---------------------------------------------------------------- ISignalModel

    public string Id => "nscooling";
    public Messenger Messenger => Messenger.XRaysUv;
    public Confidence Confidence => Confidence.Speculative;

    public IReadOnlyList<SignalChannel> Channels { get; } = new SignalChannel[]
    {
        new("tempK", "Surface temperature seen from afar, T_s^∞ (a band before ~100 yr)", "K", true),
        new("lum", "Thermal luminosity seen from afar, L^∞ (a band before ~100 yr)", "erg/s", true),
    };

    private static readonly Source CoolingSource = new(
        "Yakovlev & Pethick 2004, ARA&A 42, 169 (slow cooling, T_i ≈ 1e9 K (t/yr)^(−1/6) [DERIVED]); Gudmundsson, Pethick " +
        "& Epstein 1983 envelope, g14 = 2.4 [APPROX]; T_s^∞ = T_s √(1 − r_g/R). Before ~100 yr a band from the 100-yr " +
        "value to the relaxed-interior value [APPROX]. Check: Cas A's neutron star (~340 yr) is 2.0–2.1e6 K (Heinke & Ho 2010)",
        "signals §8.4");

    public IReadOnlyList<Source> Sources { get; } = new Source[]
    {
        CoolingSource,
        new("Blackbody at T_s^∞ with R^∞ = R/√(1 − r_g/R), 0.3–10 keV [APPROX], absorbed by N_H = 2.2e21 A_V (Güver & " +
            "Özel 2009 [APPROX]); Chandra and XMM-Newton about 1e-15 erg cm⁻² s⁻¹ in ~100 ks [APPROX]", "signals §6.2, §8.4, §11"),
        new("Not modeled: fast (direct Urca) cooling, below 3e5 K by 1e4 yr; magnetar heating", "signals §8.4"),
    };

    public bool Applies(Outcome outcome) => AppliesTo(outcome);

    public (double StartS, double EndS) ValidRange(Outcome outcome) => AppliesTo(outcome) ? (StartS, EndS) : (0, 0);

    public double Sample(Outcome outcome, ObserverSetup observer, double sourceTimeS, string channelId)
    {
        if (channelId is not ("tempK" or "lum"))
            throw new ArgumentException($"The neutron-star cooling model has no channel '{channelId}'.", nameof(channelId));
        if (!AppliesTo(outcome) || !(sourceTimeS >= StartS && sourceTimeS <= EndS)) return double.NaN;
        var s = Surface.Of(outcome, sourceTimeS);
        return channelId == "tempK" ? s.TemperatureInfK : s.LuminosityInf;
    }

    public Detection Detect(Outcome outcome, ObserverSetup observer, double sourceTimeS, Detector detector)
    {
        if (!AppliesTo(outcome)) return new Detection(DetectionStatus.NotProduced, NotProducedReason(outcome));
        if (!DetectorIds.Contains(detector.Id))
            return new Detection(DetectionStatus.NotModeled,
                $"{detector.Name} is not used for the neutron star's faint thermal X-rays here; Chandra and XMM-Newton are.");
        double z1 = observer.TimeDilation;
        if (!(sourceTimeS >= StartS))
            return new Detection(DetectionStatus.NotYet,
                $"The cooling model starts {UnitFormat.Time(StartS * z1)} after core collapse; earlier, the debris around the " +
                "newborn neutron star hides its surface.");
        if (sourceTimeS > EndS)
            return new Detection(DetectionStatus.NotModeled, $"The model ends {UnitFormat.Time(EndS * z1)} after core collapse.");

        var s = Surface.Of(outcome, sourceTimeS);
        double absorbed = BandFlux(s, observer, absorbed: true), clear = BandFlux(s, observer, absorbed: false);
        var inv = CultureInfo.InvariantCulture;
        string what = $"The neutron star's surface is about {(s.TemperatureInfK / 1e6).ToString("0.0", inv)} million K as seen " +
                      $"from afar, glowing at {FormatFlux(s.LuminosityInf)} erg/s";
        string flux = $"{detector.Name} would receive {FormatFlux(absorbed)} erg cm⁻² s⁻¹ in 0.3–10 keV against about " +
                      $"{FormatFlux(detector.Threshold)} in a long exposure";
        string band = sourceTimeS < RelaxedS
            ? " Before about 100 years the surface still depends on the young crust, so the temperature is a range."
            : "";
        if (absorbed >= detector.Threshold)
            return new Detection(DetectionStatus.Detected, $"{what}. {flux}: detected.{band}");
        if (clear >= detector.Threshold)
            return new Detection(DetectionStatus.BlockedByDust,
                $"{what}. {flux}: the gas along the line of sight (A_V = {observer.Av.ToString("0.#", inv)}) absorbs these soft " +
                $"X-rays; without it {FormatFlux(clear)} would arrive, enough to detect.{band}");
        return new Detection(DetectionStatus.TooFaint, $"{what}. {flux}: too faint at this distance.{band}");
    }

    // ---------------------------------------------------------------- public formulas (tested against signals §8.4)

    /// <summary>§8.4 slow cooling: the interior (envelope-bottom) temperature T_i = 1e9 K (t/yr)^(−1/6), t in s.</summary>
    public static double InteriorTemperatureK(double timeS) => 1e9 * Math.Pow(timeS / Year, -1.0 / 6);

    /// <summary>
    /// §8.4 envelope relation inverted: T_s = 1e6 K × [g14 (T_b / 1.288e8 K)^(1/0.455)]^(1/4), the local surface temperature.
    /// </summary>
    public static double SurfaceTemperatureK(double envelopeBottomK, double gravity14)
        => 1e6 * Math.Pow(gravity14 * Math.Pow(envelopeBottomK / 1.288e8, 1 / 0.455), 0.25);

    /// <summary>
    /// T_s^∞ (K) at this source time, with its band before 100 yr (§8.4 [APPROX]: Low is the 100-yr value, High the
    /// relaxed-interior value at this age); no range after 100 yr. NaN when the model does not apply.
    /// </summary>
    public Quantity SurfaceTemperatureAt(Outcome o, double sourceTimeS)
        => Banded(o, sourceTimeS, s => s.TemperatureInfK, "K");

    /// <summary>L^∞ (erg/s) at this source time, banded as <see cref="SurfaceTemperatureAt"/>.</summary>
    public Quantity LuminosityAt(Outcome o, double sourceTimeS) => Banded(o, sourceTimeS, s => s.LuminosityInf, "erg/s");

    // ---------------------------------------------------------------- the model

    private static bool AppliesTo(Outcome o)
        => o.Fate == Fate.Supernova && o.Remnant.Kind is RemnantKind.NeutronStar or RemnantKind.Magnetar
           && o.Remnant.Mass is { Value: > 0 } && o.Remnant.RadiusKm is { Value: > 0 };

    private Quantity Banded(Outcome o, double t, Func<Surface, double> value, string unit)
    {
        if (!AppliesTo(o)) return new Quantity(double.NaN, unit, Confidence.Speculative, CoolingSource);
        double v = value(Surface.Of(o, t));
        if (t >= RelaxedS) return new Quantity(v, unit, Confidence.Speculative, CoolingSource);
        double relaxed = value(Surface.Of(o, RelaxedS));
        return new Quantity(v, unit, Confidence.Speculative, CoolingSource, Math.Min(v, relaxed), Math.Max(v, relaxed));
    }

    /// <summary>
    /// The surface at one time, as seen from afar: T_s^∞ (K) and L^∞ (erg/s), which equals 4π(R^∞)²σ(T_s^∞)⁴ with
    /// R^∞ = R/√(1 − r_g/R).
    /// </summary>
    private readonly record struct Surface(double TemperatureInfK, double LuminosityInf)
    {
        public static Surface Of(Outcome o, double t)
        {
            double r = o.Remnant.RadiusKm!.Value * 1e5;
            double rg = 2 * PhysicalConstants.G * o.Remnant.Mass!.Value * PhysicalConstants.MSun
                        / (PhysicalConstants.C * PhysicalConstants.C);
            double redshift = 1 - rg / r;
            double ts = SurfaceTemperatureK(InteriorTemperatureK(t), SurfaceGravity14);
            double local = 4 * Math.PI * r * r * PhysicalConstants.StefanBoltzmann * Math.Pow(ts, 4);
            return new Surface(ts * Math.Sqrt(redshift), local * redshift);
        }
    }

    /// <summary>
    /// The blackbody's 0.3–10 keV energy flux at Earth, erg cm⁻² s⁻¹: L^∞ / (4π D_L²) times the band's share of a Planck
    /// spectrum at T_s^∞ (redshifted by 1 + z), each energy weighted by the gas transmission when <paramref name="absorbed"/>.
    /// </summary>
    private static double BandFlux(Surface s, ObserverSetup obs, bool absorbed)
    {
        double kT = PhysicalConstants.BoltzmannEv * s.TemperatureInfK / 1000 / obs.TimeDilation; // keV, observed
        double a = Math.Log(BandLowKeV), h = (Math.Log(BandHighKeV) - a) / BandIntervals, sum = 0;
        for (int i = 0; i <= BandIntervals; i++)
        {
            double e = Math.Exp(a + i * h), x = e / kT;
            double w = i == 0 || i == BandIntervals ? 1 : i % 2 == 1 ? 4 : 2;
            double planck = x < 700 ? Math.Pow(x, 4) / (Math.Exp(x) - 1) : 0; // x⁴/(e^x − 1) per ln E
            sum += w * planck * (absorbed ? Extinction.XrayTransmission(e, obs.Av) : 1);
        }
        double share = sum * h / 3 / (Math.Pow(Math.PI, 4) / 15);
        return s.LuminosityInf / (4 * Math.PI * Math.Pow(obs.LuminosityDistanceCm, 2)) * share;
    }

    // ---------------------------------------------------------------- text

    private static string FormatFlux(double f) => f.ToString("0.##e0", CultureInfo.InvariantCulture);

    private static string NotProducedReason(Outcome o) => o.Fate switch
    {
        Fate.NotDeadYet => "The star has not died yet, so there is no neutron star.",
        Fate.WhiteDwarf => "The star ends as a white dwarf, not a neutron star.",
        Fate.FailedSupernova or Fate.PulsationalPairInstability =>
            "No neutron star: the core collapses to a black hole, which has no hot surface to glow.",
        Fate.PairInstabilitySupernova => "No neutron star: a pair-instability explosion blows the whole star apart.",
        _ => o.Remnant.Kind switch
        {
            RemnantKind.BlackHole => "No neutron star: the explosion leaves a black hole, which has no hot surface to glow.",
            RemnantKind.None => "No neutron star: a thermonuclear (Type Ia) explosion destroys the white dwarf completely.",
            _ => "This outcome leaves no neutron star.",
        },
    };
}
