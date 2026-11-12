using System.Globalization;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>
/// Optical light curves (signals §4, §11): the bolometric luminosity, absolute and apparent V magnitude, and the margin
/// against the naked eye, ZTF and Rubin.
/// <list type="bullet">
/// <item>Heating (§4.1): ⁵⁶Ni → ⁵⁶Co → ⁵⁶Fe with gamma-ray trapping D(t) = 1 − exp(−(T0/t)²), positrons fully trapped;
/// plus a magnetar's spin-down power (E_p / t_p) / (1 + t/t_p)² when the outcome has one (§4.4; Kasen &amp; Bildsten 2010,
/// fully trapped).</item>
/// <item>Diffusion (§4.2): every heating source goes through Arnett's kernel, L(t) = ∫ P(t') (2t'/τ_m²)
/// exp((t'² − t²)/τ_m²) dt' (Inserra et al. 2013, Eq. D1), τ_m = (2κM/(βcv))^(1/2), v = √(10E/3M), β = 13.8. Types
/// Ia, Ib, Ic, Ic-BL, IIb, SLSN-I and stripped pair-instability supernovae are this alone.</item>
/// <item>Hydrogen-rich stars with at least 1 M☉ of envelope (§4.3): the Goldberg, Bildsten &amp; Paxton 2019 plateau L50
/// (<see cref="PlateauModel"/>, the one the engine states, ruling R33c) held until t_p, then a Fermi-Dirac drop onto
/// the deposited ⁵⁶Ni power, L_dep (§4.3 step 4, ruling R33d); before the drop the diffused heating is shown wherever
/// it is brighter. [APPROX] A compact blue supergiant has a faint plateau (L50 ∝ R^0.76), so its diffused ⁵⁶Ni makes
/// the slow dome of SN 1987A, peaking near day 60–85 across the opacity slider (§4.6 gives ~85 d). The research has
/// no blue-supergiant recipe: this is G19 extrapolated to R500 ≈ 0.1 plus §4.2's diffusion with the core-collapse
/// slider κ on hydrogen-rich ejecta.</item>
/// <item>Shock cooling (§3.3) from <see cref="ShockBreakoutModel.Cooling"/>, fading past its validity
/// (<see cref="ShockBreakoutModel.CoolingPhase.FadingLuminosityErgS"/>); the brighter of it and the rest is shown.</item>
/// <item>V magnitude (§4.5, <see cref="BolometricCorrection"/>): Lyman et al. 2014 bolometric corrections with colours of
/// a blackbody at the shock-cooling T_col, 6,000 K for hydrogen-rich photospheres, 10,000 K for hydrogen-free ones, and
/// the Lira relation for Type Ia after peak. m_V = M_V + μ + A_V (§4.7). [APPROX] The research has fits only for
/// stripped-envelope and Type II samples: Type Ia/Iax, SLSN-I and pair-instability supernovae use the stripped-envelope
/// fit, and Iax and 91bg-like events take the normal-Ia Lira relation.</item>
/// </list>
/// Source time throughout, from core bounce (or ignition for a Type Ia); valid from the start of shock cooling (1 hour
/// for a Type Ia) to 3 years (§11). Speculative (ruling R24/R27 reasoning): the formulas are published, but the opacity,
/// the colour temperatures, the plateau assembly and the survey depths are [APPROX] choices.
/// </summary>
public sealed class LightCurveModel : ISignalModel
{
    // ---------------------------------------------------------------- constants

    private const double Day = PhysicalConstants.Day, MSun = PhysicalConstants.MSun;

    /// <summary>⁵⁶Ni and ⁵⁶Co mean lives, s (Nadyozhin 1994, as written by Stritzinger &amp; Leibundgut 2005; §4.1).</summary>
    public const double NickelMeanLifeS = 8.8 * Day, CobaltMeanLifeS = 111.3 * Day;

    /// <summary>
    /// §4.1 decay energy per gram per second, one consistent set (Arnett, Fryer &amp; Matheson 2017): ε_Ni = 3.9805e10;
    /// ε_Co split into gamma rays 6.4552e9 and positron kinetic energy 2.1458e8.
    /// </summary>
    private const double EpsNi = 3.9805e10, EpsCoGamma = 6.4552e9, EpsCoPositron = 2.1458e8;

    /// <summary>
    /// §4.1 gamma-ray trapping time T0 = √(C κ_γ M² / E): C = 0.05 for a power-law density profile (Clocchiatti &amp;
    /// Wheeler 1997); [APPROX] κ_γ = 0.03 cm²/g, the top of the research's 0.025–0.03 range and the value its worked
    /// examples use.
    /// </summary>
    private const double TrappingC = 0.05;

    /// <summary>§4.1's effective gray gamma-ray opacity κ_γ, cm²/g [APPROX]; the line model scales from it (ruling R37).</summary>
    public const double KappaGamma = 0.03;

    /// <summary>
    /// [APPROX] The Arnett opacity for thermonuclear (Type Ia, Iax) ejecta, cm²/g. The research gives κ only for
    /// stripped-envelope supernovae (0.05–0.1, the lab's slider); with it, a 1.38 M☉, 1.35e51 erg Ia peaks 11–14 days
    /// after explosion, well short of the observed 17–19 day rise (Hayden et al. 2010; Firth et al. 2015; §4.2). 0.19 is
    /// the value at which the default near-Chandrasekhar Ia (0.6 M☉ of ⁵⁶Ni) peaks at 18 days, the middle of that range.
    /// The opacity slider does not change it.
    /// </summary>
    public const double ThermonuclearOpacity = 0.19;

    /// <summary>[APPROX] The light curve runs to about 1–3 years (§11: the ⁵⁶Co tail; late positrons and ⁵⁷Co ignored).</summary>
    public const double EndS = 3 * PhysicalConstants.Year;

    /// <summary>The detectors whose limiting magnitude this model compares against; the first three have channels.</summary>
    public static IReadOnlyList<string> DetectorIds { get; } = new[] { "eye", "ztf", "rubin", "spectrograph" };

    // ---------------------------------------------------------------- ISignalModel

    public string Id => "lightcurve";
    public Messenger Messenger => Messenger.VisibleLight;
    public Confidence Confidence => Confidence.Speculative;

    public IReadOnlyList<SignalChannel> Channels { get; } = new SignalChannel[]
    {
        new("lbol", "Bolometric luminosity", "erg/s", true),
        new("absV", "Absolute V magnitude", "mag", false, MayBeNegative: true),
        new("appV", "Apparent V magnitude (with dust)", "mag", false, MayBeNegative: true),
        new(ChannelId.Compose("detect", "eye"), "Margin below the naked-eye limit (≥ 0: visible)", "mag", false,
            MayBeNegative: true),
        new(ChannelId.Compose("detect", "ztf"), "Margin below ZTF's limit (≥ 0: detected)", "mag", false, MayBeNegative: true),
        new(ChannelId.Compose("detect", "rubin"), "Margin below Rubin's limit (≥ 0: detected)", "mag", false,
            MayBeNegative: true),
    };

    public IReadOnlyList<Source> Sources { get; } = new Source[]
    {
        new("Nadyozhin 1994, ApJS 92, 527; Arnett, Fryer & Matheson 2017, ApJ 846, 33 (⁵⁶Ni/⁵⁶Co heating); Clocchiatti & " +
            "Wheeler 1997, ApJ 491, 375 (gamma-ray trapping, κ_γ = 0.03 [APPROX])", "signals §4.1"),
        new("Arnett 1982, ApJ 253, 785; Valenti et al. 2008, MNRAS 383, 1485; Inserra et al. 2013, ApJ 770, 128 (diffusion " +
            "kernel)", "signals §4.2"),
        new("Goldberg, Bildsten & Paxton 2019, ApJ 879, 3 (plateau L50 and length); Kasen & Woosley 2009, ApJ 703, 2205 " +
            "(nickel extension); Fermi-Dirac drop, 3-day width (Olivares et al. 2010) [APPROX], onto the ⁵⁶Ni tail L_dep; " +
            "under 1 M☉ of hydrogen envelope, no plateau [APPROX]; a blue supergiant's ⁵⁶Ni dome (no recipe in the " +
            "research) [APPROX]", "signals §4.3"),
        new("Kasen & Bildsten 2010, ApJ 717, 245; Inserra et al. 2013 (magnetar heating through the Arnett kernel). Full " +
            "trapping is assumed, so the late tail is overestimated", "signals §4.4"),
        new("Lyman, Bersier & James 2014, MNRAS 437, 3848 (bolometric corrections; the stripped-envelope fit also for " +
            "Type Ia, SLSN-I and pair-instability supernovae [APPROX]); blackbody colours at 6,000 K (hydrogen) and " +
            "10,000 K (hydrogen-free) [APPROX]; Bessell, Castelli & Plez 1998 zero points", "signals §4.5"),
        new("m_V = M_V + μ + A_V (Cardelli, Clayton & Mathis 1989); naked eye V ≈ 6 [APPROX]; ZTF ≈ 20.5 and Rubin ≈ 24 " +
            "per visit, in their own bands, compared with V here [APPROX]", "signals §4.7, §11"),
        new("Circumstellar interaction (Type IIn, Ibn, Icn) and the transients of pulsational pair instability are not " +
            "modeled: an interacting supernova is drawn without the collision's light (IIn mean peak M_B −18.6, II-P " +
            "−16.8; Richardson et al. 2014, AJ 147, 118) and with an ordinary photosphere's colour (6,000 K with " +
            "hydrogen, 10,000 K without), redder than the hot, blue early spectra of interacting supernovae",
            "signals §4.6; outcomes §2.3"),
        new("Type Ia: opacity 0.19 cm²/g, set so the default Ia rises in 18 d (observed 17–19 d) [APPROX]; Lira relation " +
            "(Phillips et al. 1999, AJ 118, 1766; on Iax and 91bg-like events [APPROX])", "signals §4.2, §4.5"),
    };

    /// <summary>
    /// Review M10: the <see cref="Sources"/> behind this outcome's drawn curve: the heating, diffusion, colour and
    /// magnitude sources always; the plateau's only with a plateau, the magnetar's only with a magnetar, and the
    /// interaction caveat only for an interacting supernova. All of them when the outcome has no modeled curve.
    /// </summary>
    public IReadOnlyList<Source> SourcesFor(Outcome o)
    {
        if (LightCurve.Of(o) is not { } c) return Sources;
        bool thermonuclear = o.Type is SupernovaType.Ia or SupernovaType.Ia91T or SupernovaType.Ia91bg or SupernovaType.Iax;
        bool[] keep = { true, true, c.Plateau, c.HasMagnetar, true, true, c.Interacting, thermonuclear };
        return Sources.Where((_, i) => keep[i]).ToList();
    }

    public bool Applies(Outcome outcome) => LightCurve.Of(outcome) is not null || IsUnmodeledTransient(outcome);

    public (double StartS, double EndS) ValidRange(Outcome outcome)
        => LightCurve.Of(outcome) is { } c ? (c.StartS, EndS) : (0, 0);

    public double Sample(Outcome outcome, ObserverSetup observer, double sourceTimeS, string channelId)
    {
        var (channel, detectorId) = ParseChannel(channelId);
        if (LightCurve.Of(outcome) is not { } c || !(sourceTimeS >= c.StartS && sourceTimeS <= EndS)) return double.NaN;

        if (channel == "lbol") return c.Luminosity(sourceTimeS);
        double absV = c.AbsoluteV(sourceTimeS);
        if (channel == "absV") return absV;
        double appV = Apparent(absV, observer);
        return channel == "appV" ? appV : Detectors.Get(detectorId!).Threshold - appV;
    }

    public Detection Detect(Outcome outcome, ObserverSetup observer, double sourceTimeS, Detector detector)
    {
        if (!Applies(outcome)) return new Detection(DetectionStatus.NotProduced, NotProducedReason(outcome));
        if (detector.Messenger != Messenger.VisibleLight || !DetectorIds.Contains(detector.Id))
            return new Detection(DetectionStatus.NotModeled, $"{detector.Name} does not observe visible light.");
        if (LightCurve.Of(outcome) is not { } c)
            return new Detection(DetectionStatus.NotModeled,
                "The shells thrown off by the pulses collide and glow (weeks to millennia, about 10^41 to more than 10^44 " +
                "erg/s; Woosley 2017), but that light is not modeled here.");

        double z1 = observer.TimeDilation;
        string origin = c.Thermonuclear ? "ignition" : "core collapse";
        if (!(sourceTimeS >= c.StartS))
        {
            if (c.Thermonuclear || !double.IsFinite(c.BreakoutS))
                return new Detection(DetectionStatus.NotYet, $"The light curve starts {UnitFormat.Time(c.StartS * z1)} after {origin}.");
            // Ruling R53 (I4): before breakout nothing has reached the surface; between breakout and the start of the
            // cooling formulas the star already shines, but that light is not modeled here.
            if (sourceTimeS < c.BreakoutS)
                return new Detection(DetectionStatus.NotYet,
                    $"The shock has not yet broken out through the star's surface: that happens {UnitFormat.Time(c.BreakoutS * z1)} " +
                    $"after {origin}, and the visible light curve is drawn from {UnitFormat.Time(c.StartS * z1)}.");
            return new Detection(DetectionStatus.NotModeled,
                $"The shock broke out through the star's surface {UnitFormat.Time(c.BreakoutS * z1)} after {origin}, so the star " +
                "is already shining, but its visible light in the first hours and days after breakout is not modeled here: " +
                $"the cooling formulas the light curve uses apply from {UnitFormat.Time(c.StartS * z1)}. See the X-ray & UV " +
                "panel for the breakout flash and the early ultraviolet.");
        }
        if (sourceTimeS > EndS)
            return new Detection(DetectionStatus.NotModeled,
                $"The light-curve model ends {UnitFormat.Time(EndS * z1)} after {origin}; later light comes from the remnant " +
                "and the gas around it.");

        var inv = CultureInfo.InvariantCulture;
        double absV = c.AbsoluteV(sourceTimeS);
        double av = Extinction.A(Band.V, observer.Av);
        double m = Apparent(absV, observer), bare = m - av;
        string limit = detector.Id switch
        {
            "eye" => $"the naked eye sees to about V {detector.Threshold.ToString("0.#", inv)} under a dark sky",
            "spectrograph" => $"a typical spectrograph needs about V {detector.Threshold.ToString("0.#", inv)} to classify it " +
                              "(about 10 times more light than detecting it)",
            _ => $"{detector.Name} reaches about magnitude {detector.Threshold.ToString("0.#", inv)} per visit (in its own " +
                 "bands; compared with V here)",
        };
        if (double.IsNaN(m))
            return new Detection(DetectionStatus.TooFaint, $"The supernova gives no visible light now; {limit}.{Caveats(c)}");

        DetectionStatus status = m <= detector.Threshold ? DetectionStatus.Detected
            : bare <= detector.Threshold ? DetectionStatus.BlockedByDust
            : DetectionStatus.TooFaint;
        string now = $"The supernova is at V {m.ToString("0.0", inv)} now ({Comparisons.ForApparentMagnitude(m)})";
        string dust = av > 0
            ? $", dimmed {av.ToString("0.#", inv)} mag by dust" +
              (status == DetectionStatus.BlockedByDust ? " (without the dust it would be bright enough)" : "")
            : "";
        string peak = c.PeakTimeS is var tp && !double.IsNaN(tp)
            ? $" At its peak, {UnitFormat.Time(tp * z1)} after {origin}, it reaches V " +
              $"{Apparent(c.PeakAbsoluteV, observer).ToString("0.0", inv)}."
            : "";
        return new Detection(status, $"{now}{dust}; {limit}.{peak}{Caveats(c)}");
    }

    // ---------------------------------------------------------------- markers

    /// <summary>
    /// The time of the V-band peak (brightest absolute V), s after bounce (ignition for a Type Ia) in source time, for the
    /// timeline marker. NaN when there is no modeled light curve.
    /// </summary>
    public double PeakTimeS(Outcome o) => LightCurve.Of(o)?.PeakTimeS ?? double.NaN;

    /// <summary>
    /// The colour temperature (K) behind the light curve's V magnitudes at this source time (the blackbody with the
    /// light curve's blended B−V, §4.5); the schematic spectrum uses it so the two agree. NaN when there is no
    /// modeled light curve.
    /// </summary>
    public double ColourTemperatureK(Outcome o, double sourceTimeS) => LightCurve.Of(o)?.ColourTemperatureK(sourceTimeS) ?? double.NaN;

    /// <summary>
    /// Ruling R53 (I5): a Type Ia's (Ia, Iax, 91T- and 91bg-like) decline rate Δm15(B), mag, measured on the drawn light
    /// curve: absolute B (the drawn V plus the curve's own B−V) 15 days after B maximum less B at maximum, source time.
    /// NaN for other outcomes and when there is no modeled light curve.
    /// </summary>
    public double DeclineRateDm15B(Outcome o) => LightCurve.Of(o) is { Thermonuclear: true } c ? c.DeclineRateDm15B : double.NaN;

    /// <summary>
    /// The end of the plateau t_p (Goldberg et al. 2019), s after bounce in source time, for the timeline marker. Null
    /// when there is no plateau, or when the diffused ⁵⁶Ni light outshines it (a compact blue supergiant such as SN 1987A).
    /// </summary>
    public double? PlateauEndS(Outcome o) => LightCurve.Of(o) is { HasVisiblePlateau: true } c ? c.PlateauEndS : null;

    // ---------------------------------------------------------------- public formulas (tested against signals §4)

    /// <summary>
    /// Nadyozhin 1994's total ⁵⁶Ni + ⁵⁶Co decay power with full trapping (§4.1, as written by Stritzinger &amp;
    /// Leibundgut 2005): (M_Ni / M☉) [6.45e43 e^(−t/8.8 d) + 1.45e43 e^(−t/111.3 d)] erg/s. t in s after explosion.
    /// Reference only (the brief's check of §4.1): the light curve itself uses <see cref="DepositedNickelHeatingErgS"/>.
    /// </summary>
    public static double NickelCobaltDecayErgS(double nickelMsun, double timeS)
        => nickelMsun * (6.45e43 * Math.Exp(-timeS / NickelMeanLifeS) + 1.45e43 * Math.Exp(-timeS / CobaltMeanLifeS));

    /// <summary>
    /// §4.1's deposited power, erg/s: M_Ni [ε_Ni e^(−t/τ_Ni) D + ε_Co,γ Co(t) D + ε_Co,e+ Co(t)], with
    /// Co(t) = τ_Co/(τ_Co − τ_Ni) (e^(−t/τ_Co) − e^(−t/τ_Ni)) and D = 1 − exp(−(T0/t)²). M_Ni in M☉, times in s.
    /// </summary>
    public static double DepositedNickelHeatingErgS(double nickelMsun, double timeS, double trappingTimeS)
    {
        if (!(nickelMsun > 0) || timeS < 0) return 0;
        double d = timeS > 0 ? OneMinusExpNeg(Math.Pow(trappingTimeS / timeS, 2)) : 1;
        double ni = Math.Exp(-timeS / NickelMeanLifeS);
        double co = CobaltMeanLifeS / (CobaltMeanLifeS - NickelMeanLifeS) * (Math.Exp(-timeS / CobaltMeanLifeS) - ni);
        return nickelMsun * MSun * (EpsNi * ni * d + EpsCoGamma * co * d + EpsCoPositron * co);
    }

    /// <summary>§4.1: the gamma-ray trapping time T0 = √(0.05 κ_γ M_ej² / E_K), s, with κ_γ = 0.03 cm²/g [APPROX].</summary>
    public static double GammaRayTrappingTimeS(double ejectaMsun, double energyErg)
        => Math.Sqrt(TrappingC * KappaGamma * Math.Pow(ejectaMsun * MSun, 2) / energyErg);

    /// <summary>§4.2: Arnett's diffusion time τ_m = (2κM / (β c v))^(1/2), v = √(10E / 3M), β = 13.8; s.</summary>
    public static double ArnettDiffusionTimeS(double opacity, double ejectaMsun, double energyErg)
        => ArnettDiffusion.DiffusionTimeS(opacity, ejectaMsun, energyErg);

    /// <summary>
    /// §4.2 (Inserra et al. 2013, Eq. D1): the luminosity of any heating source P (erg/s) diffusing out of the ejecta,
    /// L(t) = ∫₀ᵗ P(t') (2t'/τ_m²) exp((t'² − t²)/τ_m²) dt' (<see cref="ArnettDiffusion.LuminosityErgS"/>).
    /// </summary>
    public static double ArnettLuminosityErgS(Func<double, double> heatingErgS, double diffusionTimeS, double timeS)
        => ArnettDiffusion.LuminosityErgS(heatingErgS, diffusionTimeS, timeS);

    /// <summary>
    /// Goldberg, Bildsten &amp; Paxton 2019's day-50 plateau luminosity, erg/s (§4.3; <see cref="PlateauModel"/>, shared
    /// with the outcome engine by ruling R33c).
    /// </summary>
    public static double PlateauLuminosityErgS(double energyErg, double ejectaMsun, double radiusCm)
        => PlateauModel.LuminosityErgS(energyErg, ejectaMsun, radiusCm);

    /// <summary>The plateau length t_p, s after explosion (§4.3; <see cref="PlateauModel.DurationS"/>).</summary>
    public static double PlateauDurationS(double energyErg, double ejectaMsun, double nickelMsun, double radiusCm)
        => PlateauModel.DurationS(energyErg, ejectaMsun, nickelMsun, radiusCm);

    // ---------------------------------------------------------------- helpers

    /// <summary>1 − e^(−y) without cancellation for small y.</summary>
    private static double OneMinusExpNeg(double y) => y < 1e-5 ? y * (1 - y / 2) : 1 - Math.Exp(-y);

    private static double Apparent(double absV, ObserverSetup obs)
        => absV + obs.DistanceModulus + Extinction.A(Band.V, obs.Av);

    /// <summary>A pulsational pair-instability event whose shells collide (Type IIn, Ibn, Icn): light, but not modeled.</summary>
    private static bool IsUnmodeledTransient(Outcome o)
        => o.Fate == Fate.PulsationalPairInstability && o.Type is SupernovaType.IIn or SupernovaType.Ibn or SupernovaType.Icn;

    private static string Caveats(LightCurve c)
    {
        string text = "";
        if (c.Interacting)
            text += " The collision with the dense gas around the star adds light that this model does not include, so it " +
                    "is probably brighter than shown (Type IIn average about −18.6 at peak against −16.8 for II-P), and " +
                    "bluer: early interacting spectra are hot and blue, while the lab colours it like an ordinary " +
                    "supernova's photosphere (6,000 K with hydrogen, 10,000 K without), too red.";
        if (c.HasMagnetar)
            text += " The magnetar's power is assumed fully trapped, so the late light is overestimated.";
        if (c.HasMagnetar && c.PeakLuminosityErgS > MagnetarModel.BrightestObservedSupernovaErgS)
            text += " Its peak is brighter than any supernova ever observed (ASASSN-15lh, about 2 × 10⁴⁵ erg/s; Dong et " +
                    "al. 2016): full trapping of the fast-spinning magnetar's spin-down power is assumed.";
        if (c.PairInstability)
            text += " A pair-instability supernova is drawn as a nickel-powered light curve; no such explosion has been " +
                    "seen for certain.";
        return text;
    }

    private static (string Channel, string? DetectorId) ParseChannel(string channelId)
    {
        var id = ChannelId.Parse(channelId);
        switch (id.Base)
        {
            case "lbol" or "absV" or "appV" when id.DetectorId is null:
                return (id.Base, null);
            case "detect" when id.DetectorId is "eye" or "ztf" or "rubin":
                return (id.Base, id.DetectorId);
            default:
                throw new ArgumentException($"The light-curve model has no channel '{channelId}'.", nameof(channelId));
        }
    }

    private static string NotProducedReason(Outcome o) => o.Fate switch
    {
        Fate.NotDeadYet => "The star has not died yet, so there is no supernova to see.",
        Fate.WhiteDwarf => "The star ends as a white dwarf with no explosion, so there is no supernova light.",
        Fate.FailedSupernova => o.Star?.HydrogenEnvelopeMass.Value > 0
            ? "No supernova: the core collapses straight to a black hole, so there is no explosion to light up. The star " +
              "may simply vanish, perhaps after a weak, red outburst of about a million Suns as its loosely bound hydrogen " +
              "envelope drifts off (Lovegrove & Woosley 2013). The red supergiant N6946-BH1 faded from view this way after " +
              "2009 (Adams et al. 2017), though a merger of two stars hidden in dust is not ruled out."
            : "No supernova: the core collapses straight to a black hole, so there is no explosion to light up, and with " +
              "no hydrogen envelope to shed the star simply disappears.",
        Fate.PulsationalPairInstability =>
            "The pulses throw off too little mass to make a visible transient, and the core collapses to a black hole " +
            "without a supernova.",
        _ => "There is no explosion, so there is no supernova light.",
    };
}
