using System.Globalization;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>
/// A long gamma-ray burst from a collapsar (signals §9, §11): the prompt flash seen by Fermi GBM and Swift BAT, and the
/// afterglow in X-rays, visible light and radio. Only outcomes with <see cref="Outcome.HasGrb"/>; only observers inside
/// the jet see anything (<see cref="ObserverSetup.BeamTowardEarth"/>, else <see cref="DetectionStatus.NotPointedAtUs"/>).
/// <list type="bullet">
/// <item>Energetics: a classical long GRB. Beaming-corrected E_γ = 5e50 erg (Frail et al. 2001; §9.2) in two jets of
/// half-angle θ_j = 5° [APPROX, the research's example], so E_iso = E_γ / (1 − cos θ_j) = 1.31e53 erg (§9.1's range
/// 1e48–1e54). Peak energy from the Amati relation, E_p,i = 95 keV (E_iso / 1e52)^0.49 (§9.1). The low-luminosity
/// bursts seen nearby, such as GRB 980425 (8.5e47 erg; §9.4), are Amati outliers and are not this model.</item>
/// <item>Prompt light curve [APPROX]: one fast-rise, exponential-decay pulse — a linear rise over τ_d/3, then
/// e^(−t/τ_d) — whose 5%–95% fluence interval is T90 = 23.3 s (GRB 980425's, the research's one measured T90; §9.1:
/// long bursts last more than 2 s). Fluence E_iso/(4πD²) (§9.1). It starts when the black hole forms plus the light
/// crossing time of the star, R/c [APPROX: the jet head is slower than light, so this is a lower bound].</item>
/// <item>Spectrum: a Band function with α = −1, β = −2.3 [APPROX typical; §9.1], observed E_p = E_p,i/(1 + z), normalized
/// to the bolometric 1–10⁴ keV rest band. GBM compares its 50–300 keV peak photon flux with 0.7 photons cm⁻² s⁻¹; BAT
/// compares its 15–150 keV energy flux with 1e-8 erg cm⁻² s⁻¹ [APPROX: the research gives BAT's limit without a band].</item>
/// <item>Afterglow (§9.3): synchrotron from the forward shock in the progenitor's wind (Chevalier &amp; Li 2000; the
/// research's choice for collapsars), A_* from the star's Ṁ and v_w, ε_e = 0.1, ε_B = 0.01 [APPROX, the low end of
/// 0.01–0.1], p = 2.5 (Sari, Piran &amp; Narayan 1998); slow- or fast-cooling spectrum, self-absorbed below ν_A
/// (F ∝ ν²) [APPROX]. The jet break (§9.2 inverted, with the formula's own η_γ = 0.2, n = 0.1 cm⁻³ [APPROX: an ISM
/// formula]) steepens the decline to t^(−p) above ν_m (Sari, Piran &amp; Halpern 1999), here through their post-break
/// scalings F_max ∝ t⁻¹, ν_m ∝ t⁻², ν_c constant, ν_A ∝ t^(−1/5). Valid from the end of the prompt (about a minute) to
/// 180 days [APPROX, §11: "minutes to months"; later the blast wave is no longer relativistic].</item>
/// </list>
/// Source time throughout, from core bounce. Speculative (rulings R24/R27): the energetics, pulse shape, jet angle,
/// microphysics and both trigger thresholds are [APPROX].
/// </summary>
public sealed class GrbModel : ISignalModel
{
    // ---------------------------------------------------------------- constants

    private const double Day = PhysicalConstants.Day;

    /// <summary>[APPROX] Jet half-opening angle, rad: 5°, the research's worked example (§9.2).</summary>
    public const double JetHalfAngleRad = 5 * Math.PI / 180;

    /// <summary>Beaming-corrected gamma-ray energy, erg: Frail et al. 2001's cluster (§9.2).</summary>
    public const double BeamingCorrectedEnergyErg = 5e50;

    /// <summary>[APPROX] T90, s: GRB 980425's 23.3 s (§9.4), the research's one measured value; long bursts exceed 2 s.</summary>
    public const double T90Seconds = 23.3;

    /// <summary>[APPROX] The pulse's linear rise lasts this fraction of its exponential decay time ("fast rise").</summary>
    private const double RiseOverDecay = 1.0 / 3;

    /// <summary>[APPROX] The prompt channel is drawn for three T90 after onset (the pulse is then below 2e-4 of its peak).</summary>
    private const double PromptEndT90s = 3;

    /// <summary>§9.1 Band function indices [APPROX typical values].</summary>
    private const double BandAlpha = -1, BandBeta = -2.3;

    /// <summary>Rest-frame bolometric band for E_iso, keV (the Amati relation's 1–10⁴ keV).</summary>
    private const double BolometricLowKeV = 1, BolometricHighKeV = 1e4;

    /// <summary>Instrument bands, keV: GBM 50–300 (its threshold's band, §9.1); BAT 15–150 [APPROX].</summary>
    private const double GbmLowKeV = 50, GbmHighKeV = 300, BatLowKeV = 15, BatHighKeV = 150;

    /// <summary>§9.3 microphysics [APPROX]: ε_e ≈ 0.1, ε_B in 0.01–0.1 (low end), p ≈ 2.5.</summary>
    private const double EpsilonE = 0.1, EpsilonB = 0.01, ElectronIndex = 2.5;

    /// <summary>§9.2's normalization, used when inverting it for the jet-break time [APPROX].</summary>
    private const double EtaGamma = 0.2, DensityCm3 = 0.1;

    /// <summary>[APPROX] A_* when the star has no wind data: 1, a typical Wolf-Rayet wind (§0.4).</summary>
    private const double DefaultAStar = 1;

    /// <summary>[APPROX] The afterglow formulas hold to "months" (§11).</summary>
    public const double AfterglowEndS = 180 * Day;

    /// <summary>Afterglow bands: radio 5 GHz [APPROX, the radio panel's middle band]; V at 550 nm (AB); X-rays 0.3–10 keV.</summary>
    private const double RadioHz = 5e9, VWavelengthNm = 550, XrayLowKeV = 0.3, XrayHighKeV = 10;

    private const double AbZeroPoint = 48.60, MilliJansky = 1e-26;

    /// <summary>Integration intervals in log energy (Simpson).</summary>
    private const int SpectrumIntervals = 400;

    /// <summary>The detectors with a trigger channel.</summary>
    public static IReadOnlyList<string> DetectorIds { get; } = new[] { "gbm", "bat" };

    // ---------------------------------------------------------------- ISignalModel

    public string Id => "grb";
    public Messenger Messenger => Messenger.GammaRays;
    public Confidence Confidence => Confidence.Speculative;

    public IReadOnlyList<SignalChannel> Channels { get; } = new SignalChannel[]
    {
        new("prompt", "Prompt gamma-ray flux (1–10⁴ keV)", "erg cm⁻² s⁻¹", true),
        new("afterglowX", "X-ray afterglow (0.3–10 keV, after gas absorption)", "erg cm⁻² s⁻¹", true),
        new("afterglowOpt", "Optical afterglow (AB magnitude at 550 nm, with dust)", "mag", false, MayBeNegative: true),
        new("afterglowRadio", "Radio afterglow at 5 GHz", "mJy", true),
        new(ChannelId.Compose("detect", "gbm"), "Fermi GBM 50–300 keV photon flux over its trigger threshold (≥ 1: triggers)",
            "", true),
        new(ChannelId.Compose("detect", "bat"), "Swift BAT 15–150 keV flux over its trigger threshold (≥ 1: triggers)", "", true),
    };

    public IReadOnlyList<Source> Sources { get; } = new Source[]
    {
        new("Kouveliotou et al. 1993, ApJ 413, L101 (long bursts, T90 > 2 s); T90 = 23.3 s from GRB 980425 (Galama et al. " +
            "1998) [APPROX]; one FRED pulse, rise τ_d/3 [APPROX]; onset at black-hole formation + R/c [APPROX]",
            "signals §9.1, §9.4"),
        new("Frail et al. 2001, ApJ 562, L55 (E_γ ≈ 5e50 erg); θ_j = 5° [APPROX], f_b = 1 − cos θ_j, so E_iso = 1.3e53 erg " +
            "and only about 0.4% of observers see the burst", "signals §9.2"),
        new("Amati 2006, MNRAS 372, 233 (E_p,i = 95 keV (E_iso/1e52)^0.49); Band et al. 1993, ApJ 413, 281 (α = −1, " +
            "β = −2.3 [APPROX])", "signals §9.1"),
        new("Fermi GBM 0.7 photons cm⁻² s⁻¹ in 50–300 keV, 1 s peak (Meegan et al. 2009) [APPROX]; Swift BAT 1e-8 erg cm⁻² " +
            "s⁻¹, taken in 15–150 keV [APPROX]", "signals §9.1, §11"),
        new("Chevalier & Li 2000, ApJ 536, 195 (wind-medium afterglow; L_ν,max per z = 1 observer day [DERIVED]); Sari, " +
            "Piran & Narayan 1998 (spectrum, p = 2.5); ε_e = 0.1, ε_B = 0.01 [APPROX]; self-absorption F ∝ ν² [APPROX]; " +
            "Sari, Piran & Halpern 1999 (jet break, t_j from the ISM formula [APPROX]). Off-axis \"orphan\" afterglows are " +
            "not modeled", "signals §9.2, §9.3"),
        new("Low-luminosity bursts such as GRB 980425 / SN 1998bw (E_γ 8.5e47 erg, peak 3e-7 erg cm⁻² s⁻¹ at about 38 Mpc, " +
            "an Amati outlier) are not modeled: this is a classical burst", "signals §9.4"),
    };

    public bool Applies(Outcome outcome) => outcome.HasGrb && outcome.Fate == Fate.Supernova;

    public (double StartS, double EndS) ValidRange(Outcome outcome)
        => Applies(outcome) ? (OnsetS(outcome), OnsetS(outcome) + AfterglowEndS) : (0, 0);

    public double Sample(Outcome outcome, ObserverSetup observer, double sourceTimeS, string channelId)
    {
        var id = ParseChannel(channelId);
        if (!Applies(outcome) || !observer.BeamTowardEarth) return double.NaN;
        double onset = OnsetS(outcome), since = sourceTimeS - onset;
        if (!(since >= 0 && since <= AfterglowEndS)) return double.NaN;

        if (id.Base is "prompt" or "detect")
        {
            if (since > PromptEndT90s * T90Seconds) return double.NaN;
            double flux = PromptFlux(since, observer);
            return id.DetectorId is { } d ? TriggerRatio(flux, observer, d) : flux;
        }

        if (since < T90Seconds) return double.NaN;
        var state = Afterglow(outcome, observer, since);
        return id.Base switch
        {
            "afterglowRadio" => FluxDensity(state, RadioHz) / MilliJansky,
            "afterglowOpt" => -2.5 * Math.Log10(FluxDensity(state, PhysicalConstants.C * 1e7 / VWavelengthNm)) - AbZeroPoint
                              + Extinction.A(Band.V, observer.Av),
            _ => XrayFlux(state, observer),
        };
    }

    public Detection Detect(Outcome outcome, ObserverSetup observer, double sourceTimeS, Detector detector)
    {
        if (!Applies(outcome)) return new Detection(DetectionStatus.NotProduced, NotProducedReason(outcome));
        if (!DetectorIds.Contains(detector.Id))
            return new Detection(DetectionStatus.NotModeled, $"{detector.Name} does not watch for gamma-ray bursts.");
        if (!observer.BeamTowardEarth)
            return new Detection(DetectionStatus.NotPointedAtUs,
                "The jets point away from Earth, so there is no gamma-ray burst to see: its light is beamed into two cones " +
                $"of about {(JetHalfAngleRad * 180 / Math.PI).ToString("0", CultureInfo.InvariantCulture)}°, which only " +
                $"{(BeamingFraction(JetHalfAngleRad) * 100).ToString("0.0", CultureInfo.InvariantCulture)}% of directions " +
                "see. A late \"orphan\" afterglow may appear as the jets slow and spread, but it is not modeled.");

        var inv = CultureInfo.InvariantCulture;
        double z1 = observer.TimeDilation, onset = OnsetS(outcome), since = sourceTimeS - onset;
        if (!(since >= 0))
            return new Detection(DetectionStatus.NotYet,
                $"The burst begins about {UnitFormat.Time(onset * z1)} after core collapse, once the black hole has formed " +
                "and its jets have drilled out of the star.");

        // The trigger looks at the brightest moment so far: the pulse peaks at the end of its rise.
        double peakSince = RiseS, seen = Math.Min(since, peakSince);
        double flux = PromptFlux(seen, observer), ratio = TriggerRatio(flux, observer, detector.Id);
        var status = ratio >= 1 ? DetectionStatus.Detected : DetectionStatus.TooFaint;
        string measure = detector.Id == "gbm"
            ? $"{FormatNumber(ratio * detector.Threshold)} photons cm⁻² s⁻¹ in 50–300 keV against its trigger threshold " +
              $"of about {detector.Threshold.ToString("0.#", inv)}"
            : $"{FormatNumber(ratio * detector.Threshold)} erg cm⁻² s⁻¹ in 15–150 keV against its trigger threshold of " +
              $"about {detector.Threshold.ToString("0.#e0", inv)}";
        string when = since < peakSince ? "so far" : "at its peak";
        string tail = since > PromptEndT90s * T90Seconds
            ? $" The burst lasted about {UnitFormat.Time(T90Seconds * z1)}; its afterglow fades over the following months."
            : $" It lasts about {UnitFormat.Time(T90Seconds * z1)} (T90).";
        return new Detection(status,
            $"{detector.Name} receives {measure} {when}: {(status == DetectionStatus.Detected ? "it triggers" : "too faint to trigger")}." +
            $" This is a classical long burst of {UnitFormat.Energy(IsotropicEnergyErg)} (isotropic equivalent) peaking " +
            $"near {AmatiPeakEnergyKeV(IsotropicEnergyErg).ToString("0", inv)} keV; it assumes the instrument is watching " +
            $"that part of the sky.{tail}");
    }

    // ---------------------------------------------------------------- markers

    /// <summary>T90 of the prompt burst, s (source time); NaN when the outcome has no GRB.</summary>
    public double T90S(Outcome o) => Applies(o) ? T90Seconds : double.NaN;

    /// <summary>
    /// When the burst starts, s after bounce (source time): black-hole formation (t_BH, else 1 s [APPROX]) plus the star's
    /// light-crossing time R/c [APPROX]. NaN when the outcome has no GRB.
    /// </summary>
    public double OnsetS(Outcome o)
    {
        if (!Applies(o)) return double.NaN;
        double tBh = o.Remnant.FormationTimeS?.Value ?? 1.0;
        double r = o.Star?.RadiusCm.Value ?? 0;
        return tBh + r / PhysicalConstants.C;
    }

    /// <summary>The prompt peak, s after bounce (source time); NaN when the outcome has no GRB.</summary>
    public double PeakTimeS(Outcome o) => OnsetS(o) + RiseS;

    // ---------------------------------------------------------------- public formulas (tested against signals §9)

    /// <summary>§9.2: the fraction of the sky inside two jets of half-angle θ_j, f_b = 1 − cos θ_j.</summary>
    public static double BeamingFraction(double halfAngleRad) => 1 - Math.Cos(halfAngleRad);

    /// <summary>The isotropic-equivalent energy, erg: E_γ / f_b (§9.2).</summary>
    public static double IsotropicEnergyErg { get; } = BeamingCorrectedEnergyErg / BeamingFraction(JetHalfAngleRad);

    /// <summary>§9.1 Amati 2006: rest-frame peak energy E_p,i = 95 keV (E_iso / 1e52 erg)^0.49.</summary>
    public static double AmatiPeakEnergyKeV(double isotropicEnergyErg) => 95 * Math.Pow(isotropicEnergyErg / 1e52, 0.49);

    /// <summary>
    /// §9.2 (Sari, Piran &amp; Halpern 1999 as used by Frail et al. 2001) solved for the jet-break time, observer days:
    /// t_j = (θ_j / 0.057)^(8/3) ((1+z)/2) (E_iso/1e53)^(1/3) (η_γ/0.2)^(−1/3) (n/0.1)^(−1/3), with η_γ = 0.2 and
    /// n = 0.1 cm⁻³ [APPROX].
    /// </summary>
    public static double JetBreakDays(double isotropicEnergyErg, double redshift)
        => Math.Pow(JetHalfAngleRad / 0.057, 8.0 / 3) * ((1 + redshift) / 2) * Math.Cbrt(isotropicEnergyErg / 1e53)
           * Math.Cbrt(0.2 / EtaGamma) * Math.Cbrt(0.1 / DensityCm3);

    /// <summary>The afterglow's break frequencies (observer frame, Hz) and peak flux density (erg cm⁻² s⁻¹ Hz⁻¹).</summary>
    public readonly record struct AfterglowState(double NuAHz, double NuMHz, double NuCHz, double FMaxCgs);

    /// <summary>
    /// §9.3 wind medium (Chevalier &amp; Li 2000; x = (1+z)/2, t_d in observer days), with ε_e = 0.1, ε_B = 0.01:
    /// ν_m = 5e12 x^½ (ε_e/0.1)² (ε_B/0.1)^½ E52^½ t_d^(−3/2); ν_c = 2e12 x^(−3/2) (ε_B/0.1)^(−3/2) E52^½ A_*⁻² t_d^½;
    /// ν_A = 1e11 x^(−2/5) (ε_e/0.1)⁻¹ (ε_B/0.1)^(1/5) E52^(−2/5) A_*^(6/5) t_d^(−3/5).
    /// F_ν,max = (1+z) L_ν,max / (4π D_L²), with the research's L_ν,max ≈ 3.5e32 (ε_B/0.1)^½ E52^½ A_* t_d^(−½) erg/s/Hz,
    /// whose t_d is the z = 1 observer day, i.e. 2 rest-frame days [DERIVED]: F_ν,max = 7.0e32 x^(3/2) (…) t_d^(−½) / (4π D_L²).
    /// After the jet break t_j (days; +∞ for none) the values at t_j scale as F_max ∝ t⁻¹, ν_m ∝ t⁻², ν_c ∝ t⁰,
    /// ν_A ∝ t^(−1/5) (Sari, Piran &amp; Halpern 1999). <paramref name="distanceCm"/> is the comoving distance D.
    /// </summary>
    public static AfterglowState WindAfterglow(double isotropicEnergyErg, double aStar, double tObsDays, double redshift,
        double distanceCm, double jetBreakDays)
    {
        double t = Math.Min(tObsDays, jetBreakDays), after = tObsDays / t;
        double x = (1 + redshift) / 2, e52 = isotropicEnergyErg / 1e52, ee = EpsilonE / 0.1, eb = EpsilonB / 0.1;
        double nuM = 5e12 * Math.Sqrt(x) * ee * ee * Math.Sqrt(eb) * Math.Sqrt(e52) * Math.Pow(t, -1.5);
        double nuC = 2e12 * Math.Pow(x, -1.5) * Math.Pow(eb, -1.5) * Math.Sqrt(e52) / (aStar * aStar) * Math.Sqrt(t);
        double nuA = 1e11 * Math.Pow(x, -0.4) / ee * Math.Pow(eb, 0.2) * Math.Pow(e52, -0.4) * Math.Pow(aStar, 1.2) * Math.Pow(t, -0.6);
        double dl = distanceCm * (1 + redshift);
        double fMax = 7.0e32 * Math.Pow(x, 1.5) * Math.Sqrt(eb) * Math.Sqrt(e52) * aStar / Math.Sqrt(t) / (4 * Math.PI * dl * dl);
        return new AfterglowState(nuA * Math.Pow(after, -0.2), nuM / (after * after), nuC, fMax / after);
    }

    /// <summary>
    /// §9.3 synchrotron spectrum (Sari, Piran &amp; Narayan 1998), erg cm⁻² s⁻¹ Hz⁻¹ at frequency ν (Hz), p = 2.5. Slow
    /// cooling (ν_m &lt; ν_c): ν^(1/3), ν^(−(p−1)/2), ν^(−p/2); fast cooling: ν^(1/3), ν^(−1/2), ν^(−p/2). Below ν_A the
    /// thin spectrum is multiplied by (ν/ν_A)^(5/3), giving F ∝ ν² below ν_A &lt; ν_m [APPROX].
    /// </summary>
    public static double FluxDensity(AfterglowState s, double nuHz)
    {
        const double p = ElectronIndex;
        double shape;
        if (s.NuMHz < s.NuCHz)
            shape = nuHz < s.NuMHz ? Math.Cbrt(nuHz / s.NuMHz)
                : nuHz < s.NuCHz ? Math.Pow(nuHz / s.NuMHz, -(p - 1) / 2)
                : Math.Pow(s.NuCHz / s.NuMHz, -(p - 1) / 2) * Math.Pow(nuHz / s.NuCHz, -p / 2);
        else
            shape = nuHz < s.NuCHz ? Math.Cbrt(nuHz / s.NuCHz)
                : nuHz < s.NuMHz ? Math.Pow(nuHz / s.NuCHz, -0.5)
                : Math.Pow(s.NuMHz / s.NuCHz, -0.5) * Math.Pow(nuHz / s.NuMHz, -p / 2);
        if (nuHz < s.NuAHz) shape *= Math.Pow(nuHz / s.NuAHz, 5.0 / 3);
        return s.FMaxCgs * shape;
    }

    // ---------------------------------------------------------------- the prompt pulse

    /// <summary>
    /// T90 / τ_d for the pulse: with unit peak, the rise holds r/2 of the fluence A = 1 + r/2 (r = τ_r/τ_d), and the
    /// cumulative fluence c is reached at √(2 r c) on the rise or r − ln(1 − (c − r/2)) on the decay.
    /// </summary>
    private static readonly double T90OverDecay = PulseTime(0.95) - PulseTime(0.05);

    private static double PulseTime(double fraction)
    {
        const double r = RiseOverDecay;
        double c = fraction * (1 + r / 2);
        return c <= r / 2 ? Math.Sqrt(2 * r * c) : r - Math.Log(1 - (c - r / 2));
    }

    private static double DecayS => T90Seconds / T90OverDecay;
    private static double RiseS => RiseOverDecay * DecayS;

    /// <summary>The isotropic luminosity at this time after onset, erg/s; E_iso over the pulse.</summary>
    private static double PromptLuminosity(double since)
    {
        double peak = IsotropicEnergyErg / (DecayS * (1 + RiseOverDecay / 2));
        if (since < 0) return 0;
        return since <= RiseS ? peak * since / RiseS : peak * Math.Exp(-(since - RiseS) / DecayS);
    }

    /// <summary>Bolometric prompt flux at Earth, erg cm⁻² s⁻¹: L_iso / (4π D_L²).</summary>
    private static double PromptFlux(double since, ObserverSetup obs)
        => PromptLuminosity(since) / (4 * Math.PI * Math.Pow(obs.LuminosityDistanceCm, 2));

    /// <summary>
    /// The instrument-band flux over its threshold: GBM's 50–300 keV photon flux over 0.7, or BAT's 15–150 keV energy
    /// flux over 1e-8, from the bolometric flux and the Band spectrum at E_p = E_p,i / (1 + z).
    /// </summary>
    private static double TriggerRatio(double bolometricFlux, ObserverSetup obs, string detectorId)
    {
        double z1 = obs.TimeDilation, ep = AmatiPeakEnergyKeV(IsotropicEnergyErg) / z1;
        double bolometric = BandIntegral(ep, BolometricLowKeV / z1, BolometricHighKeV / z1, energyWeighted: true);
        var detector = Detectors.Get(detectorId);
        return detectorId == "gbm"
            ? bolometricFlux * BandIntegral(ep, GbmLowKeV, GbmHighKeV, false) / (bolometric * PhysicalConstants.KeV) / detector.Threshold
            : bolometricFlux * BandIntegral(ep, BatLowKeV, BatHighKeV, true) / bolometric / detector.Threshold;
    }

    /// <summary>
    /// ∫ N(E) dE (or ∫ E N(E) dE) of the Band function over [lo, hi] keV, arbitrary normalization (Simpson in ln E).
    /// N(E) = (E/100)^α e^(−E/E0) below (α − β) E0, else ((α − β) E0/100)^(α−β) e^(β−α) (E/100)^β; E0 = E_p / (2 + α).
    /// </summary>
    private static double BandIntegral(double peakKeV, double lo, double hi, bool energyWeighted)
    {
        double e0 = peakKeV / (2 + BandAlpha), eBreak = (BandAlpha - BandBeta) * e0;
        double highNorm = Math.Pow(eBreak / 100, BandAlpha - BandBeta) * Math.Exp(BandBeta - BandAlpha);
        double N(double e) => e <= eBreak ? Math.Pow(e / 100, BandAlpha) * Math.Exp(-e / e0) : highNorm * Math.Pow(e / 100, BandBeta);
        double a = Math.Log(lo), h = (Math.Log(hi) - a) / SpectrumIntervals, sum = 0;
        for (int i = 0; i <= SpectrumIntervals; i++)
        {
            double e = Math.Exp(a + i * h), w = i == 0 || i == SpectrumIntervals ? 1 : i % 2 == 1 ? 4 : 2;
            sum += w * N(e) * e * (energyWeighted ? e : 1);
        }
        return sum * h / 3;
    }

    // ---------------------------------------------------------------- the afterglow

    private static AfterglowState Afterglow(Outcome o, ObserverSetup obs, double sinceSourceS)
    {
        double z = obs.Redshift;
        double tDays = sinceSourceS * obs.TimeDilation / Day;
        return WindAfterglow(IsotropicEnergyErg, AStar(o), tDays, z, obs.DistancePc * PhysicalConstants.Pc,
            JetBreakDays(IsotropicEnergyErg, z));
    }

    /// <summary>§0.4: A_* = (Ṁ / 1e-5 M☉/yr) / (v_w / 1000 km/s), from the progenitor's wind.</summary>
    private static double AStar(Outcome o)
    {
        double mdot = o.Star?.MassLossRateMsunPerYr.Value ?? 0, vw = o.Star?.WindSpeedKmS.Value ?? 0;
        return mdot > 0 && vw > 0 ? mdot / 1e-5 / (vw / 1000) : DefaultAStar;
    }

    /// <summary>
    /// 0.3–10 keV energy flux, erg cm⁻² s⁻¹, after photoelectric absorption by the gas column N_H = 2.2e21 A_V with the
    /// shock-breakout model's σ(E) = 2.4e-22 (E / 1 keV)^(−8/3) cm² [APPROX] (Simpson in ln ν).
    /// </summary>
    private static double XrayFlux(AfterglowState s, ObserverSetup obs)
    {
        double nH = Extinction.NhFromAv(obs.Av);
        double keVToHz = 1000 * PhysicalConstants.C * 1e7 / PhysicalConstants.HcEvNm;
        double a = Math.Log(XrayLowKeV), h = (Math.Log(XrayHighKeV) - a) / SpectrumIntervals, sum = 0;
        for (int i = 0; i <= SpectrumIntervals; i++)
        {
            double kev = Math.Exp(a + i * h), nu = kev * keVToHz;
            double absorbed = nH > 0
                ? Math.Exp(-nH * ShockBreakoutModel.PhotoCrossSection1KeV * Math.Pow(kev, ShockBreakoutModel.PhotoCrossSectionSlope))
                : 1;
            double w = i == 0 || i == SpectrumIntervals ? 1 : i % 2 == 1 ? 4 : 2;
            sum += w * FluxDensity(s, nu) * nu * absorbed;
        }
        return sum * h / 3;
    }

    // ---------------------------------------------------------------- text

    private static string FormatNumber(double v) => v.ToString(v >= 0.01 && v < 1e4 ? "0.##" : "0.##e0", CultureInfo.InvariantCulture);

    private static string NotProducedReason(Outcome o) => o.Fate switch
    {
        Fate.NotDeadYet => "The star has not died yet, so there is no gamma-ray burst.",
        Fate.WhiteDwarf => "The star ends as a white dwarf, so there is no gamma-ray burst.",
        Fate.FailedSupernova =>
            "No gamma-ray burst: the core collapses to a black hole, but without the extreme rotation that would wrap it " +
            "in a jet-launching disk.",
        _ => "No gamma-ray burst: that needs a collapsar — a fast-spinning, hydrogen-free star at low metal content " +
             "whose core forms a black hole with a disk that launches jets.",
    };

    private static ChannelId ParseChannel(string channelId)
    {
        var id = ChannelId.Parse(channelId);
        bool ok = id.DetectorId is null
            ? id.Base is "prompt" or "afterglowX" or "afterglowOpt" or "afterglowRadio"
            : id.Base == "detect" && DetectorIds.Contains(id.DetectorId);
        return ok ? id : throw new ArgumentException($"The gamma-ray burst model has no channel '{channelId}'.", nameof(channelId));
    }
}
