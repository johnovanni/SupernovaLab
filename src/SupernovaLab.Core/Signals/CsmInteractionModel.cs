using System.Globalization;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>
/// Radio and X-rays from the supernova's shock running into the wind its star blew before dying (signals §6, §11 "Radio"
/// and "X-ray (interaction)"), from about a day to 30 years.
/// <list type="bullet">
/// <item>Wind (§0.4): ρ = A/r², A_* = (Ṁ / 1e-5 M☉/yr) / (v_w / 1000 km/s), from the progenitor's mass-loss rate and
/// wind speed.</item>
/// <item>Radio (§6.1, Chevalier 1998 synchrotron self-absorption as given by Chevalier &amp; Fransson 2006, p = 3,
/// inverted): B(t) = √(8π × 0.052 ε_B,-1 A_*) / t_10 G, R = v t, L_ν,p = 1.197e24 [R B / 4.4e14]^(19/7) erg/s/Hz,
/// ν_p = 5 GHz (B / 1.1 G)(L_ν,p / 1.197e24)^(2/19), with equipartition ε_e = ε_B = 0.1 (α = 1) and filling factor
/// f = 0.5 [APPROX]; spectrum: the research's smooth join (s = 1) rescaled so that its maximum is L_p at ν_p (ruling
/// R42; the CF06 relations are defined at the observed peak), L_ν = N L_p / ((x0 x)^(−5/2) + x0 x) with x = ν/ν_p,
/// x0 = 2.5^(2/7) and N = x0^(−5/2) + x0 = 1.81897. Since u_B ∝ ρv² ∝ A/t², L_p stays constant and ν_p falls as
/// 1/t.</item>
/// <item>Shock speed (§6.1 [APPROX]): R/t from the observed ranges, scaled with √(E/M_ej): hydrogen-poor supernovae
/// (Ib, Ic, Ic-BL, SLSN-I, compact IIb) 58,000 km/s at 1e51 erg and 3 M☉ — the geometric middle of CF06's
/// 32,000–105,000 km/s — and hydrogen-rich ones (II-P, II-L, SLSN-II) 10,000 km/s at 1e51 erg and 10 M☉. A IIb whose star
/// kept an extended envelope (R ≥ 1e13 cm, an "eIIb" after Chevalier &amp; Soderberg 2010, outcomes §3.2) takes the
/// hydrogen-rich scale: it is the same split that gives it a red-supergiant wind, and CF06's fast group holds only compact
/// stars. Constant in time (no
/// deceleration), and capped at Γβ = 1 (v = c/√2), the low end of SN 1998bw's mildly relativistic Γβ ≈ 1–2
/// (presets §7): the formulas are Newtonian, so above 0.3c (ruling R28's limit) the result is only a rough guide.</item>
/// <item>X-rays (§6.2): thermal free-free from the reverse shock, CF06 Eq. 30 with n = 10, β = 1, ζ₂ = 1 [APPROX]:
/// L_ff = 2.3625e36 A_*² t_10⁻¹ erg/s; plus inverse Compton (CF06 Eq. 31) on the optical light,
/// E dL/dE = 8.8e36 γ_min ε_r,-1 A_* v_s4 (L_bol/1e42) t_10⁻¹ with γ_min = 1, ε_r = ε_e = 0.1, flat in ln E (p = 3),
/// L_bol from <see cref="LightCurveModel"/>. [APPROX] <c>lx</c> is their sum in 0.3–10 keV, both spread evenly in ln E
/// (the research gives no thermal temperature); <c>fluxX</c> is lx / 4πD_L² after the interstellar gas
/// (<see cref="Extinction.XrayTransmission"/>). Absorption by the wind itself is not modeled.</item>
/// <item>Detection: radio against the VLA and MeerKAT's 0.03 mJy and the SKA's 0.01 mJy (ruling R40), at the brightest
/// of the panel frequencies each covers [APPROX: VLA 1–50 GHz, MeerKAT 0.58–3.5 GHz, SKA-Mid 0.35–15.4 GHz]; X-rays
/// against Chandra's and XMM-Newton's 1e-15 (~100 ks) and Swift XRT's 1e-13 erg cm⁻² s⁻¹ (~10 ks) [APPROX].</item>
/// </list>
/// Not modeled: Type Ia supernovae, whose surroundings are clean (SN 2011fe and SN 2014J showed no radio or X-rays,
/// Ṁ ≲ 1e-9 M☉/yr; presets §4–5), so <see cref="Applies"/> is false and no circumstellar gas is invented; dense
/// circumstellar gas (Type IIn, Ibn, Icn, the dense-CSM input, pulsational pair-instability shells), which applies but
/// returns NaN and NotModeled; free-free absorption by a dense wind, which can dominate early for II-P (§6.1, so the
/// early low-frequency radio is overestimated). The SN 1987A ring (§6.3) is drawn only for an outcome a preset gives it
/// (<see cref="Outcome.HasEquatorialRing"/>, ruling R41): channel <c>ringLx</c> is <see cref="RingXrayLuminosityErgS"/>,
/// the 0.5–2 keV luminosity as observed, already absorbed by the gas toward the LMC, so no absorption is applied to it; it
/// is NaN for every other outcome. The X-ray verdict adds the ring's flux to the shock's (ruling R47a). Source time
/// throughout.
/// Speculative (rulings R24/R27): the microphysics, shock speeds and spectral shapes are [APPROX].
/// </summary>
public sealed class CsmInteractionModel : ISignalModel
{
    // ---------------------------------------------------------------- constants

    private const double Day = PhysicalConstants.Day, Year = PhysicalConstants.Year;

    /// <summary>§11: about 1 day; the earliest start (see <see cref="StartTimeS"/>).</summary>
    public const double StartS = Day;

    private static readonly ShockBreakoutModel BreakoutModel = new();

    /// <summary>
    /// Ruling R51a: when the model starts for this outcome, source time: the later of 1 day (§11) and the shock breakout
    /// (ShockBreakoutModel's own breakout time, so the two agree): there is no interaction with the wind before the shock
    /// has left the star. A red supergiant's shock breaks out after about 1.3 d (16 M☉) to 2.4 d (Betelgeuse).
    /// </summary>
    public double StartTimeS(Outcome outcome)
    {
        double breakout = BreakoutModel.BreakoutTimeS(outcome);
        return double.IsFinite(breakout) ? Math.Max(StartS, breakout) : StartS;
    }

    /// <summary>§11: "about 10–30 yr (then remnant phase)" for radio, about 30 yr for X-rays; the upper end.</summary>
    public const double EndS = 30 * Year;

    /// <summary>[APPROX] ε_B = 0.1 (the u_B formula's normalization) and equipartition α = ε_e/ε_B = 1 (Chevalier 1998).</summary>
    private const double EpsilonB = 0.1, Alpha = 1;

    /// <summary>§6.1: emitting-volume filling factor f = 0.5.</summary>
    private const double FillingFactor = 0.5;

    /// <summary>§6.1: 1.197e24 erg/s/Hz = 4π (1 Mpc)² × 1 mJy, and the formula constants for p = 3.</summary>
    private const double MpcMjy = 1.197e24, RbConstant = 4.4e14, PeakFieldG = 1.1, PeakFrequencyHz = 5e9;

    /// <summary>[APPROX] §6.1 shock speed R/t, km/s, at the canonical explosion: hydrogen-poor and hydrogen-rich.</summary>
    private const double StrippedSpeedKmS = 58_000, StrippedRefMsun = 3, HydrogenSpeedKmS = 10_000, HydrogenRefMsun = 10;

    private const double RefEnergyErg = 1e51;


    /// <summary>[APPROX] The Newtonian formulas are stretched to Γβ = 1 at most: v ≤ c/√2.</summary>
    private static readonly double MaxSpeedCmS = PhysicalConstants.C / Math.Sqrt(2);

    /// <summary>Ruling R28's limit of the Newtonian shock formulas; above it the reason says the result is rough.</summary>
    private const double NewtonianBeta = 0.3;

    /// <summary>[APPROX] §6.2: outer ejecta density index n = 10, temperature ratio β = 1, ζ₂ = 1.</summary>
    private const double EjectaIndex = 10;

    /// <summary>[APPROX] §6.2 inverse Compton: γ_min = 1, ε_r = ε_e = 0.1.</summary>
    private const double GammaMin = 1, EpsilonR = 0.1;

    /// <summary>[APPROX] The X-ray band, keV (Swift XRT's 0.3–10 keV, as the other X-ray views).</summary>
    private const double XrayLowKeV = 0.3, XrayHighKeV = 10;

    private const int BandIntervals = 64;

    private const double MilliJansky = 1e-26;

    /// <summary>The panel's radio frequencies, Hz, by channel.</summary>
    private static readonly (string Channel, double Hz)[] RadioBands =
    {
        ("radio1_4", 1.4e9), ("radio5", 5e9), ("radio22", 22e9),
    };

    /// <summary>[APPROX, instrument specifications not in the research] The frequency ranges each telescope covers, Hz.</summary>
    private static (double Low, double High) Coverage(string detectorId) => detectorId switch
    {
        "vla" => (1e9, 50e9),
        "meerkat" => (0.58e9, 3.5e9),
        _ => (0.35e9, 15.4e9), // SKA-Mid
    };

    /// <summary>The radio continuum detectors this model compares against (ruling R40).</summary>
    public static IReadOnlyList<string> RadioDetectorIds { get; } = new[] { "vla", "meerkat", "ska" };

    /// <summary>The X-ray detectors this model compares against.</summary>
    public static IReadOnlyList<string> XrayDetectorIds { get; } = new[] { "chandra", "xmm", "swift-xrt" };

    /// <summary>
    /// §6.3 [APPROX]: SN 1987A's ring, 0.5–2 keV, zero until the shock reaches it at 3,000 d, then a logistic rise (5% at
    /// 3,000 d, 95% at 9,000 d) to the observed plateau.
    /// </summary>
    private const double RingPlateauFlux = 8e-12, RingDistancePc = 51_400, RingRiseStartDays = 3000, RingRiseEndDays = 9000;

    private static readonly LightCurveModel Optical = new();

    // ---------------------------------------------------------------- ISignalModel

    public string Id => "csm";
    public Messenger Messenger => Messenger.Radio;
    public Confidence Confidence => Confidence.Speculative;

    public IReadOnlyList<SignalChannel> Channels { get; } = new SignalChannel[]
    {
        new("radio1_4", "Radio flux density at 1.4 GHz", "mJy", true),
        new("radio5", "Radio flux density at 5 GHz", "mJy", true),
        new("radio22", "Radio flux density at 22 GHz", "mJy", true),
        new("nuPeakGHz", "Radio peak (self-absorption) frequency", "GHz", true),
        new("lx", "X-ray luminosity, 0.3–10 keV (thermal + inverse Compton)", "erg/s", true),
        new("fluxX", "X-ray flux, 0.3–10 keV (after gas absorption)", "erg cm⁻² s⁻¹", true),
        new("ringLx", "X-ray luminosity of the equatorial ring, 0.5–2 keV, as observed (already absorbed)", "erg/s", true),
    };

    public IReadOnlyList<Source> Sources { get; } = new Source[]
    {
        new("Chevalier 1998, ApJ 499, 810; Chevalier & Fransson 2006, ApJ 651, 381 (synchrotron self-absorption, p = 3, " +
            "f = 0.5, inverted to a forward model [DERIVED]); ε_e = ε_B = 0.1 [APPROX]; smooth spectral join [APPROX], " +
            "normalized so the drawn spectrum peaks at L_p at ν_p (ruling R42); check: SN 1994I's R = 1.17e16 cm, " +
            "B = 0.51 G give a drawn peak of 1.42e27 erg/s/Hz at 4.88 GHz (observed 1.40e27)", "signals §6.1"),
        new("Shock R/t [APPROX]: hydrogen-poor 58,000 km/s at 1e51 erg / 3 M☉ (middle of 32,000–105,000, CF06 Table 3); " +
            "hydrogen-rich 10,000 km/s at 1e51 erg / 10 M☉ (Chevalier, Fransson & Nymark 2006), also for IIb with an " +
            "extended envelope (Chevalier & Soderberg 2010); ∝ √(E/M_ej), constant, capped at Γβ = 1. Wind A_* from Ṁ " +
            "and v_w (Chevalier & Li 2000)", "signals §0.4, §6.1; outcomes §3.2"),
        new("Population check: peak 2–10 GHz luminosity mean log L = 25.3 (II), 25.4 (Ib/c), 26.5 (IIn), σ 1.1–1.7 dex; " +
            "only 31% detected (Bietenholz et al. 2021, ApJ 908, 75)", "signals §6.1"),
        new("X-rays: CF06 Eq. 30 reverse-shock free-free (n = 10, β = 1, ζ₂ = 1 [APPROX]) and Eq. 31 inverse Compton " +
            "(γ_min = 1, ε_r = 0.1 [APPROX]) on the light curve's L_bol; both spread evenly over 0.3–10 keV [APPROX]. " +
            "Observed: II-P up to ~3e39 erg/s (Dwarkadas 2014), SN 2008D 1e39 erg/s at ~10 d (Soderberg et al. 2008)",
            "signals §6.2"),
        new("Detection: VLA and MeerKAT 0.03 mJy (5σ), SKA 0.01 mJy (ruling R40); Chandra and XMM-Newton 1e-15, Swift XRT " +
            "1e-13 erg cm⁻² s⁻¹, for exposures of ~100 ks and ~10 ks [APPROX]; telescope frequency ranges [APPROX]",
            "signals §6.1, §6.2, §11"),
        new("Not modeled: dense circumstellar gas (IIn, Ibn, Icn; the brightest radio and X-ray supernovae), free-free " +
            "absorption in dense winds (early II-P radio is overestimated), and Type Ia supernovae, whose surroundings are " +
            "clean (SN 2011fe, SN 2014J: Ṁ ≲ 1e-9 M☉/yr)", "signals §6.1; presets §4, §5"),
        new("SN 1987A's equatorial ring (ruling R41, for the preset only): 0.5–2 keV as Chandra observed it, 8e-12 erg cm⁻² " +
            "s⁻¹ at 51.4 kpc on the plateau, already absorbed, so not absorbed again; zero until the shock reaches the ring " +
            "at about 3,000 d, then a logistic rise to the plateau by 9,000 d [APPROX]", "signals §6.3; presets §1"),
    };

    public bool Applies(Outcome outcome) => AppliesTo(outcome);

    public (double StartS, double EndS) ValidRange(Outcome outcome) => AppliesTo(outcome) ? (StartTimeS(outcome), EndS) : (0, 0);

    public double Sample(Outcome outcome, ObserverSetup observer, double sourceTimeS, string channelId)
    {
        if (channelId is not ("nuPeakGHz" or "lx" or "fluxX" or "ringLx") && RadioBands.All(b => b.Channel != channelId))
            throw new ArgumentException($"The circumstellar-interaction model has no channel '{channelId}'.", nameof(channelId));
        if (!AppliesTo(outcome) || HasDenseCsm(outcome) || !(sourceTimeS >= StartTimeS(outcome) && sourceTimeS <= EndS)) return double.NaN;
        // Ruling R41: the ring's luminosity is as observed (absorbed once already), so it is returned as it is.
        if (channelId == "ringLx") return outcome.HasEquatorialRing ? RingXrayLuminosityErgS(sourceTimeS) : double.NaN;

        var shock = Shock.Of(outcome);
        double z1 = observer.TimeDilation;
        switch (channelId)
        {
            case "nuPeakGHz": return shock.Peak(sourceTimeS).FrequencyHz / z1 / 1e9;
            case "lx": return XrayLuminosity(outcome, shock, sourceTimeS);
            case "fluxX": return XrayFlux(XrayLuminosity(outcome, shock, sourceTimeS), observer, absorbed: true);
            default:
                double nu = RadioBands.Single(b => b.Channel == channelId).Hz;
                return RadioMjy(shock, observer, sourceTimeS, nu);
        }
    }

    public Detection Detect(Outcome outcome, ObserverSetup observer, double sourceTimeS, Detector detector)
    {
        if (!AppliesTo(outcome)) return new Detection(DetectionStatus.NotProduced, NotProducedReason(outcome));
        bool radio = RadioDetectorIds.Contains(detector.Id), xray = XrayDetectorIds.Contains(detector.Id);
        if (!radio && !xray)
            return new Detection(DetectionStatus.NotModeled,
                $"{detector.Name} is not used for the shock's radio or X-ray glow here.");
        if (HasDenseCsm(outcome))
            return new Detection(DetectionStatus.NotModeled,
                "The shock runs into dense gas the star threw off shortly before dying. Such collisions make the brightest " +
                "radio and X-ray supernovae (Type IIn average about 3×10²⁶ erg/s/Hz at radio peak, over ten times a normal " +
                "Type II), but the dense gas is not modeled here.");
        double z1 = observer.TimeDilation;
        double start = StartTimeS(outcome);
        if (!(sourceTimeS >= start))
            return new Detection(DetectionStatus.NotYet, start > StartS
                ? $"The shock has not yet broken out of the star (it does so {UnitFormat.Time(start * z1)} after core collapse); " +
                  "its radio and X-ray glow in the gas around the star starts then."
                : $"The shock's radio and X-ray glow is modeled from {UnitFormat.Time(StartS * z1)} after the explosion.");
        if (sourceTimeS > EndS)
            return new Detection(DetectionStatus.NotModeled,
                $"The model ends {UnitFormat.Time(EndS * z1)} after the explosion; the remnant view takes over.");

        var shock = Shock.Of(outcome);
        var inv = CultureInfo.InvariantCulture;
        string caveat = (shock.SpeedCmS / PhysicalConstants.C > NewtonianBeta
                            ? $" The shock moves at about {(shock.SpeedCmS / PhysicalConstants.C).ToString("0.0#", inv)} of the " +
                              "speed of light, faster than these slow-shock formulas are meant for, so this is a rough guide."
                            : "")
                        + (shock.HydrogenRich && radio
                            ? " Early on, a dense red-supergiant wind can also absorb the low-frequency radio (not modeled), " +
                              "so early low-frequency fluxes may be overestimated."
                            : "");
        if (radio)
        {
            var (lo, hi) = Coverage(detector.Id);
            var best = RadioBands.Where(b => b.Hz >= lo && b.Hz <= hi)
                .Select(b => (b.Hz, Mjy: RadioMjy(shock, observer, sourceTimeS, b.Hz)))
                .MaxBy(b => b.Mjy);
            var status = best.Mjy >= detector.Threshold ? DetectionStatus.Detected : DetectionStatus.TooFaint;
            // R42: the drawn spectrum's maximum is L_p at ν_p, so the stated peak is where the curve peaks.
            double peakHz = shock.Peak(sourceTimeS).FrequencyHz / z1;
            return new Detection(status,
                $"{detector.Name} receives about {UnitFormat.Figure(best.Mjy)} mJy at {(best.Hz / 1e9).ToString("0.#", inv)} GHz " +
                $"against its limit of about {detector.Threshold.ToString("0.##", inv)} mJy: " +
                $"{(status == DetectionStatus.Detected ? "detected" : "too faint")}. The radio spectrum now peaks at about " +
                $"{UnitFormat.Figure(RadioMjy(shock, observer, sourceTimeS, peakHz))} mJy near " +
                $"{(peakHz / 1e9).ToString("0.##", inv)} GHz; below that the glowing gas absorbs its own radio waves, and " +
                $"the peak moves to lower frequencies as the shell expands.{caveat}");
        }

        double lx = XrayLuminosity(outcome, shock, sourceTimeS);
        // Ruling R47a: SN 1987A's ring counts in the verdict. Its 0.5–2 keV flux is as observed (already absorbed, so not
        // absorbed again) and a lower bound on its 0.3–10 keV flux.
        double ringLx = outcome.HasEquatorialRing ? RingXrayLuminosityErgS(sourceTimeS) : 0;
        double ring = ringLx / (4 * Math.PI * Math.Pow(observer.LuminosityDistanceCm, 2));
        double absorbed = XrayFlux(lx, observer, absorbed: true) + ring, clear = XrayFlux(lx, observer, absorbed: false) + ring;
        string flux = ring > 0
            ? $"{detector.Name} would receive {FormatFlux(absorbed)} erg cm⁻² s⁻¹: the shock in the wind gives " +
              $"{FormatFlux(absorbed - ring)} in 0.3–10 keV (from {FormatFlux(lx)} erg/s), and the equatorial ring " +
              $"{FormatFlux(ring)} in 0.5–2 keV as Chandra measured it (already absorbed; at least that much in 0.3–10 keV). " +
              $"Its limit is {FormatFlux(detector.Threshold)} in {XrayExposure(detector.Id)}"
            : $"{detector.Name} would receive {FormatFlux(absorbed)} erg cm⁻² s⁻¹ in 0.3–10 keV (from " +
              $"{FormatFlux(lx)} erg/s) against the {FormatFlux(detector.Threshold)} it reaches in " +
              $"{XrayExposure(detector.Id)}";
        if (absorbed >= detector.Threshold)
            return new Detection(DetectionStatus.Detected, $"{flux}: detected.{caveat}");
        if (clear >= detector.Threshold)
            return new Detection(DetectionStatus.BlockedByDust,
                $"{flux}: the gas along the line of sight (A_V = {observer.Av.ToString("0.#", inv)}) absorbs the soft X-rays; " +
                $"without it {FormatFlux(clear)} would arrive, enough to detect.{caveat}");
        return new Detection(DetectionStatus.TooFaint, $"{flux}: too faint.{caveat}");
    }

    // ---------------------------------------------------------------- public formulas (tested against signals §6)

    /// <summary>A synchrotron self-absorption peak: L_ν,p (erg/s/Hz) at ν_p (Hz).</summary>
    public readonly record struct SsaPeak(double LuminosityErgSHz, double FrequencyHz);

    /// <summary>
    /// §6.1 forward model, from the shell radius R (cm) and field B (G): L_ν,p = 1.197e24 [R B / (4.4e14 α^(−5/19)
    /// (f/0.5)^(−5/19))]^(19/7) and ν_p = 5 GHz (B / 1.1 G) α^(4/19) (f/0.5)^(4/19) (L_ν,p / 1.197e24)^(2/19).
    /// </summary>
    public static SsaPeak SynchrotronPeak(double radiusCm, double fieldG)
    {
        double micro = Alpha * (FillingFactor / 0.5);
        double lp = MpcMjy * Math.Pow(radiusCm * fieldG / (RbConstant * Math.Pow(micro, -5.0 / 19)), 19.0 / 7);
        double nu = PeakFrequencyHz * (fieldG / PeakFieldG) * Math.Pow(micro, 4.0 / 19) * Math.Pow(lp / MpcMjy, 2.0 / 19);
        return new SsaPeak(lp, nu);
    }

    /// <summary>§6.1: B = √(8π u_B) with u_B = 0.052 ε_B,-1 A_* t_10⁻² erg/cm³ (ε_B = 0.1), G; t in s.</summary>
    public static double MagneticFieldG(double aStar, double timeS)
        => Math.Sqrt(8 * Math.PI * 0.052 * (EpsilonB / 0.1) * aStar) / (timeS / (10 * Day));

    /// <summary>R42: x0 = 2.5^(2/7) = 1.29926, where 1/(y^(−5/2) + y) peaks.</summary>
    private static readonly double JoinPeakX = Math.Pow(2.5, 2.0 / 7);

    /// <summary>R42: N = x0^(−5/2) + x0 = 1.81897, which makes the join's maximum exactly 1.</summary>
    private static readonly double JoinNorm = Math.Pow(JoinPeakX, -2.5) + JoinPeakX;

    /// <summary>
    /// §6.1 with ruling R42: L_ν = N L_p / ((x0 x)^(−5/2) + x0 x), erg/s/Hz, x = ν/ν_p, x0 = 2.5^(2/7),
    /// N = x0^(−5/2) + x0 = 1.81897 (R42 quotes 1.81907, from x0 rounded up to 1.29935). The research's smooth join
    /// (s = 1), shifted and rescaled so that its maximum is L_p exactly at ν_p, where the CF06 relations define them;
    /// the asymptotes keep their ν^(5/2) and ν⁻¹ slopes.
    /// </summary>
    public static double SpectralLuminosity(SsaPeak peak, double nuHz)
    {
        double y = JoinPeakX * nuHz / peak.FrequencyHz;
        return JoinNorm * peak.LuminosityErgSHz / (Math.Pow(y, -2.5) + y);
    }

    /// <summary>
    /// §6.2 CF06 Eq. 30: L_ff = 3e35 [(n−3)(n−4)²/(4(n−2))] β^½ ζ₂⁻¹ A_*² t_10⁻¹ erg/s, with n = 10, β = 1, ζ₂ = 1
    /// [APPROX]: 2.3625e36 A_*² t_10⁻¹.
    /// </summary>
    public static double ThermalXrayLuminosity(double aStar, double timeS)
    {
        const double n = EjectaIndex;
        return 3e35 * ((n - 3) * (n - 4) * (n - 4) / (4 * (n - 2))) * aStar * aStar / (timeS / (10 * Day));
    }

    /// <summary>
    /// §6.2 CF06 Eq. 31 over 0.3–10 keV: E dL/dE = 8.8e36 γ_min ε_r,-1 A_* v_s4 (L_bol / 1e42) t_10⁻¹ erg/s, flat in
    /// ln E (p = 3), times ln(10/0.3). v in cm/s (v_s4 = v / 1e4 km/s), L_bol in erg/s, t in s.
    /// </summary>
    public static double InverseComptonLuminosity(double aStar, double shockSpeedCmS, double bolometricErgS, double timeS)
        => 8.8e36 * GammaMin * (EpsilonR / 0.1) * aStar * (shockSpeedCmS / 1e9) * (bolometricErgS / 1e42) / (timeS / (10 * Day))
           * Math.Log(XrayHighKeV / XrayLowKeV);

    /// <summary>
    /// §6.3 [APPROX]: the X-ray luminosity of SN 1987A's equatorial ring in the 0.5–2 keV band (not <c>lx</c>'s 0.3–10
    /// keV), erg/s, at this age (s). Zero before 3,000 d, when the forward shock reaches the ring; then a logistic rise
    /// from 5% at 3,000 d to 95% at 9,000 d onto the plateau Chandra measured from about 9,500 d, 8e-12 erg cm⁻² s⁻¹ at
    /// 51.4 kpc (presets §1) = 2.53e36 erg/s. That flux is as observed, already absorbed by the gas toward the LMC: a
    /// channel built on this (Task 13's <c>ringLx</c>, ruling R41) must not apply the interstellar absorption again, and
    /// should be labelled "0.5–2 keV, as observed". The slow decline after about 10,000 d is not included (the research
    /// gives no rate, and it begins at the very end of this model's 30 years).
    /// </summary>
    public static double RingXrayLuminosityErgS(double ageS)
    {
        if (!(ageS >= RingRiseStartDays * Day)) return 0; // the shock has not reached the ring
        double plateau = RingPlateauFlux * 4 * Math.PI * Math.Pow(RingDistancePc * PhysicalConstants.Pc, 2);
        double mid = (RingRiseStartDays + RingRiseEndDays) / 2 * Day;
        double width = (RingRiseEndDays - RingRiseStartDays) / 2 / Math.Log(19) * Day; // 5% → 95% over the rise
        return plateau / (1 + Math.Exp(-(ageS - mid) / width));
    }

    // ---------------------------------------------------------------- the model

    private static bool Thermonuclear(Outcome o)
        => o.Type is SupernovaType.Ia or SupernovaType.Ia91T or SupernovaType.Ia91bg or SupernovaType.Iax;

    private static bool AppliesTo(Outcome o)
    {
        if (Thermonuclear(o) || o.Star is not { MassLossRateMsunPerYr.Value: > 0, WindSpeedKmS.Value: > 0 }) return false;
        if (o.Fate == Fate.PulsationalPairInstability) return o.Type != SupernovaType.None; // colliding shells
        return o.Fate is Fate.Supernova or Fate.PairInstabilitySupernova
               && o.ExplosionEnergyErg is { Value: > 0 } && o.EjectaMass is { Value: > 0 };
    }

    /// <summary>
    /// Dense circumstellar gas, which applies but is not modeled: the lab's dense-gas input, pulsational pair instability's
    /// shells, or an interacting type (IIn, Ibn, Icn). Public for the 3D view's gas shell (Task 16).
    /// </summary>
    public static bool HasDenseCsm(Outcome o)
        => o.Inputs.DenseCsm || o.Fate == Fate.PulsationalPairInstability
           || o.Type is SupernovaType.IIn or SupernovaType.Ibn or SupernovaType.Icn;

    /// <summary>
    /// The progenitor's wind density parameter A_* = (Ṁ / 1e-5 M☉/yr) / (v_w / 1,000 km/s) (signals §0.4); NaN when there
    /// is no star, no wind, or a wind speed of zero (never Infinity: Task 16 re-review N3).
    /// </summary>
    public static double WindAStar(Outcome o)
    {
        if (o.Star is not { } star) return double.NaN;
        double speedKmS = star.WindSpeedKmS.Value;
        if (!(speedKmS > 0)) return double.NaN;
        double a = star.MassLossRateMsunPerYr.Value / 1e-5 / (speedKmS / 1000);
        return double.IsFinite(a) ? a : double.NaN;
    }

    /// <summary>
    /// The shock's radius R = v_s t (cm) at source time t, the radius the radio model's synchrotron peak uses (a read-only
    /// accessor for the 3D view's forward shock, ruling R-P2-24). For an outcome the model applies to.
    /// </summary>
    public static double ShockRadiusCm(Outcome o, double sourceTimeS) => Shock.Of(o).SpeedCmS * sourceTimeS;

    /// <summary>The shock speed R/t (cm/s) the radio model uses (<see cref="ShockRadiusCm"/> / t).</summary>
    public static double ShockSpeedCmS(Outcome o) => Shock.Of(o).SpeedCmS;

    /// <summary>One outcome's shock: R/t (cm/s), the wind's A_*, and whether the ejecta are hydrogen-rich.</summary>
    private readonly record struct Shock(double SpeedCmS, double AStar, bool HydrogenRich)
    {
        public static Shock Of(Outcome o)
        {
            var star = o.Star!;
            // An extended-envelope IIb (eIIb) takes the hydrogen-rich speed: the progenitor model's own test, which also
            // gives it the red-supergiant wind (review M4).
            bool hydrogen = o.Type is SupernovaType.IIP or SupernovaType.IIL or SupernovaType.SlsnII
                            || (o.Type == SupernovaType.Pisn && star.HydrogenEnvelopeMass.Value > 0)
                            || (o.Type == SupernovaType.IIb &&
                                ProgenitorModel.HasExtendedEnvelope(star.HydrogenEnvelopeMass.Value, star.RadiusCm.Value));
            double refSpeed = (hydrogen ? HydrogenSpeedKmS : StrippedSpeedKmS) * 1e5;
            double refMass = hydrogen ? HydrogenRefMsun : StrippedRefMsun;
            double v = refSpeed * Math.Sqrt(o.ExplosionEnergyErg!.Value / o.EjectaMass!.Value / (RefEnergyErg / refMass));
            return new Shock(Math.Min(v, MaxSpeedCmS), WindAStar(o), hydrogen);
        }

        public SsaPeak Peak(double t) => SynchrotronPeak(SpeedCmS * t, MagneticFieldG(AStar, t));
    }

    /// <summary>F_ν at the observed frequency, mJy: (1 + z) L_ν(ν (1 + z)) / (4π D_L²).</summary>
    private static double RadioMjy(Shock shock, ObserverSetup obs, double t, double observedHz)
    {
        double z1 = obs.TimeDilation;
        double lnu = SpectralLuminosity(shock.Peak(t), observedHz * z1);
        return z1 * lnu / (4 * Math.PI * Math.Pow(obs.LuminosityDistanceCm, 2)) / MilliJansky;
    }

    private static double XrayLuminosity(Outcome o, Shock shock, double t)
    {
        double lbol = Optical.Sample(o, ObserverSetup.Default, t, "lbol");
        if (!double.IsFinite(lbol)) lbol = 0; // outside the light curve's range its light is negligible here
        return ThermalXrayLuminosity(shock.AStar, t) + InverseComptonLuminosity(shock.AStar, shock.SpeedCmS, lbol, t);
    }

    /// <summary>
    /// 0.3–10 keV flux at Earth, erg cm⁻² s⁻¹: L / 4πD_L², times (when <paramref name="absorbed"/>) the band-averaged gas
    /// transmission for a spectrum flat in ln E (Simpson in ln E).
    /// </summary>
    private static double XrayFlux(double lx, ObserverSetup obs, bool absorbed)
    {
        double flux = lx / (4 * Math.PI * Math.Pow(obs.LuminosityDistanceCm, 2));
        if (!absorbed || obs.Av <= 0) return flux;
        double transmitted = LogEnergyIntegral.Simpson(e => Extinction.XrayTransmission(e, obs.Av), XrayLowKeV, XrayHighKeV,
            BandIntervals);
        return flux * transmitted / Math.Log(XrayHighKeV / XrayLowKeV);
    }

    // ---------------------------------------------------------------- text

    private static string FormatFlux(double f) => f.ToString("0.##e0", CultureInfo.InvariantCulture);

    /// <summary>The exposure each registry X-ray threshold assumes (Detectors; signals §6.2).</summary>
    internal static string XrayExposure(string detectorId) => detectorId == "swift-xrt" ? "about 10 ks" : "about 100 ks";

    private static string NotProducedReason(Outcome o)
    {
        if (Thermonuclear(o))
            return "A Type Ia supernova explodes in clean surroundings: the nearest ones, SN 2011fe and SN 2014J, showed no " +
                   "radio or X-rays, which limits any wind around them to less than about 10⁻⁹ M☉ per year (Chomiuk et al. " +
                   "2012; Margutti et al. 2014; Pérez-Torres et al. 2014). No circumstellar gas is assumed here (a few, such " +
                   "as Kepler's supernova, did have some).";
        return o.Fate switch
        {
            Fate.NotDeadYet => "The star has not died yet, so there is no shock to light up the gas around it.",
            Fate.WhiteDwarf => "The star ends as a white dwarf with no explosion, so there is no shock.",
            Fate.FailedSupernova => "No explosion: the core collapses straight to a black hole, so no shock runs into the star's wind.",
            Fate.PulsationalPairInstability => "The pulses throw off too little mass to make a collision worth showing.",
            _ => "This outcome has no shock running into a stellar wind.",
        };
    }
}
