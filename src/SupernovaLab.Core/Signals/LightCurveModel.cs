using System.Globalization;
using System.Runtime.CompilerServices;
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
/// held until t_p, then a Fermi-Dirac drop onto the deposited ⁵⁶Ni power; the diffused heating is shown wherever it
/// is brighter. A compact blue supergiant has a faint plateau (L50 ∝ R^0.76), so its diffused ⁵⁶Ni makes the slow
/// dome of SN 1987A, peaking near day 70–90 (§4.6).</item>
/// <item>Shock cooling (§3.3) from <see cref="ShockBreakoutModel.Cooling"/>, fading past its validity
/// (<see cref="ShockBreakoutModel.CoolingPhase.FadingLuminosityErgS"/>); the brighter of it and the rest is shown.</item>
/// <item>V magnitude (§4.5, <see cref="BolometricCorrection"/>): Lyman et al. 2014 bolometric corrections with colours of
/// a blackbody at the shock-cooling T_col, 6,000 K for hydrogen-rich photospheres, 10,000 K for hydrogen-free ones, and
/// the Lira relation for Type Ia after peak. m_V = M_V + μ + A_V (§4.7).</item>
/// </list>
/// Source time throughout, from core bounce (or ignition for a Type Ia); valid from the start of shock cooling (1 hour
/// for a Type Ia) to 3 years (§11). Speculative (ruling R24/R27 reasoning): the formulas are published, but the opacity,
/// the colour temperatures, the plateau assembly and the survey depths are [APPROX] choices.
/// </summary>
public sealed class LightCurveModel : ISignalModel
{
    // ---------------------------------------------------------------- constants

    private const double Day = PhysicalConstants.Day, MSun = PhysicalConstants.MSun, C = PhysicalConstants.C;

    /// <summary>⁵⁶Ni and ⁵⁶Co mean lives, s (Nadyozhin 1994, as written by Stritzinger &amp; Leibundgut 2005; §4.1).</summary>
    public const double NickelMeanLifeS = 8.8 * Day, CobaltMeanLifeS = 111.3 * Day;

    /// <summary>
    /// §4.1 decay energy per gram per second: ε_Ni = 3.9e10 (Nadyozhin 1994); ε_Co split into gamma rays 6.4552e9 and
    /// positron kinetic energy 2.1458e8 (Arnett, Fryer &amp; Matheson 2017).
    /// </summary>
    private const double EpsNi = 3.9e10, EpsCoGamma = 6.4552e9, EpsCoPositron = 2.1458e8;

    /// <summary>
    /// §4.1 gamma-ray trapping time T0 = √(C κ_γ M² / E): C = 0.05 for a power-law density profile (Clocchiatti &amp;
    /// Wheeler 1997); [APPROX] κ_γ = 0.03 cm²/g, the top of the research's 0.025–0.03 range and the value its worked
    /// examples use.
    /// </summary>
    private const double TrappingC = 0.05, KappaGamma = 0.03;

    /// <summary>Arnett's β = 13.8 (§4.2).</summary>
    private const double ArnettBeta = 13.8;

    /// <summary>
    /// [APPROX] The Arnett opacity for thermonuclear (Type Ia, Iax) ejecta, cm²/g. The research gives κ only for
    /// stripped-envelope supernovae (0.05–0.1, the lab's slider); with it, a 1.38 M☉, 1.35e51 erg Ia peaks 11–14 days
    /// after explosion, well short of the observed 17–19 day rise (Hayden et al. 2010; Firth et al. 2015; §4.2). 0.19 is
    /// the value at which the default near-Chandrasekhar Ia (0.6 M☉ of ⁵⁶Ni) peaks at 18 days, the middle of that range.
    /// The opacity slider does not change it.
    /// </summary>
    public const double ThermonuclearOpacity = 0.19;

    /// <summary>
    /// [APPROX] A magnetar-powered outcome uses Kasen &amp; Bildsten 2010's κ = 0.2 cm²/g (outcomes §5.3), the opacity of the
    /// outcome engine's magnetar peak and of the SLSN-II cut (ruling R22), instead of the slider.
    /// </summary>
    private const double MagnetarOpacity = MagnetarModel.Opacity;

    /// <summary>§4.3 step 5: below about 1 M☉ of hydrogen envelope there is no plateau, only an Arnett peak [APPROX].</summary>
    private const double PlateauMinEnvelope = 1.0;

    /// <summary>
    /// Goldberg et al. 2019's plateau-length fit uses models with M_Ni ≳ 0.03 M☉ (§4.3). [APPROX] Below that the length
    /// follows Kasen &amp; Woosley 2009's nickel extension (1 + C_f M_Ni E51^−1/2 M10^−1/2 R500^−1)^(1/6), C_f ≈ 87,
    /// scaled to meet the fit at 0.03 M☉.
    /// </summary>
    private const double G19MinNickel = 0.03, KwNickelCf = 87;

    /// <summary>[APPROX] The width of the Fermi-Dirac drop at the plateau's end: "a free display choice" (§4.3).</summary>
    private const double PlateauDropWidthS = 3 * Day;

    /// <summary>
    /// [APPROX] Photospheric colour temperatures (§4.5): hydrogen recombination, 5,000–7,000 K, for hydrogen-rich
    /// photospheres; 8,000–12,000 K for Ia/Ibc near peak. The middles of the ranges, held through the tail (the research
    /// gives no later colours, except the Lira relation for Type Ia).
    /// </summary>
    private const double HydrogenPhotosphereK = 6_000, HotPhotosphereK = 10_000;

    /// <summary>The Lira relation holds 30–90 days after V maximum (§4.5).</summary>
    private const double LiraStartDays = 30, LiraEndDays = 90;

    /// <summary>[APPROX] §11: the light curve runs from about 1 hour (the start for a Type Ia, which has no breakout) ...</summary>
    private const double EarliestStartS = 3600;

    /// <summary>[APPROX] ... to about 1–3 years (the ⁵⁶Co tail; late positrons and ⁵⁷Co ignored).</summary>
    public const double EndS = 3 * PhysicalConstants.Year;

    /// <summary>Simpson intervals for Arnett's kernel.</summary>
    private const int KernelIntervals = 200;

    /// <summary>The kernel's weight exp((t'² − t²)/τ²) is cut below e^−40.</summary>
    private const double KernelCutoff = 40;

    /// <summary>The detectors whose limiting magnitude this model compares against; the first three have channels.</summary>
    public static IReadOnlyList<string> DetectorIds { get; } = new[] { "eye", "ztf", "rubin", "spectrograph" };

    private static readonly ShockBreakoutModel Breakout = new();

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
            "kernel). Type Ia opacity 0.19 cm²/g, set so the default Ia rises in 18 d (observed 17–19 d) [APPROX]",
            "signals §4.2"),
        new("Goldberg, Bildsten & Paxton 2019, ApJ 879, 3 (plateau L50 and length); Kasen & Woosley 2009, ApJ 703, 2205 " +
            "(nickel extension); Fermi-Dirac drop, 3-day width (Olivares et al. 2010) [APPROX]; under 1 M☉ of hydrogen " +
            "envelope, no plateau [APPROX]", "signals §4.3"),
        new("Kasen & Bildsten 2010, ApJ 717, 245; Inserra et al. 2013 (magnetar heating through the Arnett kernel). Full " +
            "trapping is assumed, so the late tail is overestimated", "signals §4.4"),
        new("Lyman, Bersier & James 2014, MNRAS 437, 3848 (bolometric corrections); blackbody colours at 6,000 K (hydrogen) " +
            "and 10,000 K (hydrogen-free) [APPROX]; Lira relation (Phillips et al. 1999, AJ 118, 1766); Bessell, Castelli " +
            "& Plez 1998 zero points", "signals §4.5"),
        new("m_V = M_V + μ + A_V (Cardelli, Clayton & Mathis 1989); naked eye V ≈ 6 [APPROX]; ZTF ≈ 20.5 and Rubin ≈ 24 " +
            "per visit, in their own bands, compared with V here [APPROX]", "signals §4.7, §11"),
        new("Circumstellar interaction (Type IIn, Ibn, Icn) and the transients of pulsational pair instability are not " +
            "modeled: an interacting supernova is drawn without the collision's light (IIn mean peak M_B −18.6, II-P " +
            "−16.8; Richardson et al. 2014, AJ 147, 118)", "signals §4.6; outcomes §2.3"),
    };

    public bool Applies(Outcome outcome) => Curve.Of(outcome) is not null || IsUnmodeledTransient(outcome);

    public (double StartS, double EndS) ValidRange(Outcome outcome)
        => Curve.Of(outcome) is { } c ? (c.StartS, EndS) : (0, 0);

    public double Sample(Outcome outcome, ObserverSetup observer, double sourceTimeS, string channelId)
    {
        var (channel, detectorId) = ParseChannel(channelId);
        if (Curve.Of(outcome) is not { } c || !(sourceTimeS >= c.StartS && sourceTimeS <= EndS)) return double.NaN;

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
        if (Curve.Of(outcome) is not { } c)
            return new Detection(DetectionStatus.NotModeled,
                "The shells thrown off by the pulses collide and glow (weeks to millennia, about 10^41 to more than 10^44 " +
                "erg/s; Woosley 2017), but that light is not modeled here.");

        double z1 = observer.TimeDilation;
        string origin = c.Thermonuclear ? "ignition" : "core collapse";
        if (!(sourceTimeS >= c.StartS))
            return new Detection(DetectionStatus.NotYet,
                $"The light curve starts {UnitFormat.Time(c.StartS * z1)} after {origin}" +
                (c.Thermonuclear ? "." : ", when the shock has broken out through the star's surface."));
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
    public double PeakTimeS(Outcome o) => Curve.Of(o)?.PeakTimeS ?? double.NaN;

    /// <summary>
    /// The end of the plateau t_p (Goldberg et al. 2019), s after bounce in source time, for the timeline marker. Null
    /// when there is no plateau, or when the diffused ⁵⁶Ni light outshines it (a compact blue supergiant such as SN 1987A).
    /// </summary>
    public double? PlateauEndS(Outcome o) => Curve.Of(o) is { HasVisiblePlateau: true } c ? c.PlateauEndS : null;

    // ---------------------------------------------------------------- public formulas (tested against signals §4)

    /// <summary>
    /// Nadyozhin 1994's total ⁵⁶Ni + ⁵⁶Co decay power with full trapping (§4.1, as written by Stritzinger &amp;
    /// Leibundgut 2005): (M_Ni / M☉) [6.45e43 e^(−t/8.8 d) + 1.45e43 e^(−t/111.3 d)] erg/s. t in s after explosion.
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
    {
        double m = ejectaMsun * MSun;
        double v = Math.Sqrt(10 * energyErg / (3 * m));
        return Math.Sqrt(2 * opacity * m / (ArnettBeta * C * v));
    }

    /// <summary>
    /// §4.2 (Inserra et al. 2013, Eq. D1): the luminosity of any heating source P (erg/s) diffusing out of the ejecta,
    /// L(t) = ∫₀ᵗ P(t') (2t'/τ_m²) exp((t'² − t²)/τ_m²) dt', written so the exponent never overflows. Simpson's rule over
    /// t' where the weight exceeds e^−40.
    /// </summary>
    public static double ArnettLuminosityErgS(Func<double, double> heatingErgS, double diffusionTimeS, double timeS)
    {
        if (!(timeS > 0)) return 0;
        double tau2 = diffusionTimeS * diffusionTimeS, t2 = timeS * timeS;
        double lo = Math.Sqrt(Math.Max(0, t2 - KernelCutoff * tau2));
        double h = (timeS - lo) / KernelIntervals, sum = 0;
        for (int i = 0; i <= KernelIntervals; i++)
        {
            double tp = lo + i * h;
            double w = i == 0 || i == KernelIntervals ? 1 : i % 2 == 1 ? 4 : 2;
            sum += w * heatingErgS(tp) * 2 * tp / tau2 * Math.Exp((tp * tp - t2) / tau2);
        }
        return sum * h / 3;
    }

    /// <summary>
    /// Goldberg, Bildsten &amp; Paxton 2019 (§4.3): log10(L50 / erg s⁻¹) = 42.16 − 0.40 log M10 + 0.74 log E51 +
    /// 0.76 log R500, with M10 = M_ej / 10 M☉, E51 = E / 1e51 erg and R500 = R / 500 R☉.
    /// </summary>
    public static double PlateauLuminosityErgS(double energyErg, double ejectaMsun, double radiusCm)
        => Math.Pow(10, 42.16 - 0.40 * Math.Log10(ejectaMsun / 10) + 0.74 * Math.Log10(energyErg / 1e51)
                        + 0.76 * Math.Log10(radiusCm / (500 * PhysicalConstants.RSun)));

    /// <summary>
    /// The plateau length t_p, s after explosion (§4.3): Goldberg et al. 2019, log10(t_p / d) = 2.184 + 0.134 log M_Ni +
    /// 0.411 log M10 − 0.282 log E51, for M_Ni ≥ 0.03 M☉; below that, [APPROX] the fit at 0.03 M☉ scaled by Kasen &amp;
    /// Woosley's nickel extension (1 + 87 M_Ni E51^−1/2 M10^−1/2 R500^−1)^(1/6) relative to its value at 0.03 M☉.
    /// </summary>
    public static double PlateauDurationS(double energyErg, double ejectaMsun, double nickelMsun, double radiusCm)
    {
        double e51 = energyErg / 1e51, m10 = ejectaMsun / 10, r500 = radiusCm / (500 * PhysicalConstants.RSun);
        double G19(double ni) => Math.Pow(10, 2.184 + 0.134 * Math.Log10(ni) + 0.411 * Math.Log10(m10) - 0.282 * Math.Log10(e51));
        if (nickelMsun >= G19MinNickel) return G19(nickelMsun) * Day;
        double Extension(double ni) => Math.Pow(1 + KwNickelCf * ni / (Math.Sqrt(e51) * Math.Sqrt(m10) * r500), 1.0 / 6);
        return G19(G19MinNickel) * Extension(Math.Max(nickelMsun, 0)) / Extension(G19MinNickel) * Day;
    }

    // ---------------------------------------------------------------- the light curve of one outcome

    /// <summary>
    /// One outcome's light curve: its heating, diffusion time, plateau (if any), shock cooling and colours. Built once per
    /// outcome and cached (it is a pure function of the outcome).
    /// </summary>
    internal sealed class Curve
    {
        private static readonly ConditionalWeakTable<Outcome, Holder> Cache = new();

        private sealed record Holder(Curve? Value);

        public bool Thermonuclear { get; private init; }
        public bool Plateau { get; private init; }
        public bool Interacting { get; private init; }
        public bool PairInstability { get; private init; }
        public bool HasMagnetar => _magEnergy > 0;
        public BcSample BcSample { get; private init; }
        public double StartS { get; private init; }
        public double DiffusionTimeS { get; private init; }
        public double PlateauEndS { get; private init; }

        private double _nickel, _trappingS, _magEnergy, _magTimeS, _l50;
        private ShockBreakoutModel.CoolingPhase? _cooling;

        // Derived once, on first use; Lazy keeps concurrent first uses safe.
        private readonly Lazy<double> _mainPeakS;
        private readonly Lazy<(double TimeS, double AbsV)> _peak;
        private readonly Lazy<bool> _visiblePlateau;

        private Curve()
        {
            _mainPeakS = new(() => Maximize(Main, Math.Max(StartS, 0.05 * DiffusionTimeS), Math.Min(EndS, 10 * DiffusionTimeS)).TimeS);
            _peak = new(FindPeak);
            _visiblePlateau = new(() => Plateau && _l50 > Diffused(PlateauEndS));
        }

        public static Curve? Of(Outcome o) => Cache.GetValue(o, x => new Holder(Build(x))).Value;

        private static Curve? Build(Outcome o)
        {
            if (o.Fate is not (Fate.Supernova or Fate.PairInstabilitySupernova)) return null;
            if (o.Star is not { } star || o.ExplosionEnergyErg is not { Value: > 0 } energy ||
                o.EjectaMass is not { Value: > 0 } ejecta) return null;

            double e = energy.Value, m = ejecta.Value;
            bool thermonuclear = o.Type is SupernovaType.Ia or SupernovaType.Ia91T or SupernovaType.Ia91bg or SupernovaType.Iax;
            double envelope = star.HydrogenEnvelopeMass.Value;
            bool hydrogenType = o.Type is SupernovaType.IIP or SupernovaType.IIL or SupernovaType.IIn or SupernovaType.SlsnII
                                || (o.Type == SupernovaType.Pisn && envelope > 0);
            bool plateau = !thermonuclear && hydrogenType && envelope >= PlateauMinEnvelope;
            var cooling = thermonuclear ? null : Breakout.Cooling(o);
            double breakoutS = thermonuclear ? double.NaN : Breakout.BreakoutTimeS(o);
            double start = cooling?.StartS ?? (double.IsFinite(breakoutS) ? Math.Max(breakoutS, EarliestStartS) : EarliestStartS);
            bool magnetar = o.MagnetarEnergyErg is { Value: > 0 } && o.MagnetarTimescaleS is { Value: > 0 };
            double kappa = thermonuclear ? ThermonuclearOpacity : magnetar ? MagnetarOpacity : o.Inputs.Opacity;
            double nickel = Math.Max(o.NickelMass?.Value ?? 0, 0);
            double r = star.RadiusCm.Value;

            return new Curve
            {
                Thermonuclear = thermonuclear,
                Plateau = plateau,
                Interacting = o.Inputs.DenseCsm || o.Type is SupernovaType.IIn or SupernovaType.Ibn or SupernovaType.Icn,
                PairInstability = o.Fate == Fate.PairInstabilitySupernova,
                BcSample = plateau ? BcSample.TypeII : BcSample.StrippedEnvelope,
                StartS = start,
                DiffusionTimeS = ArnettDiffusionTimeS(kappa, m, e),
                PlateauEndS = plateau ? PlateauDurationS(e, m, nickel, r) : double.NaN,
                _nickel = nickel,
                _trappingS = GammaRayTrappingTimeS(m, e),
                _magEnergy = o.MagnetarEnergyErg?.Value ?? 0,
                _magTimeS = o.MagnetarTimescaleS?.Value ?? 0,
                _l50 = plateau ? PlateauLuminosityErgS(e, m, r) : 0,
                _cooling = cooling,
            };
        }

        // ------------------------------------------------ luminosity

        /// <summary>The heating deposited in the ejecta, erg/s: ⁵⁶Ni/⁵⁶Co with trapping, plus the magnetar's spin-down.</summary>
        private double Heating(double t)
            => DepositedNickelHeatingErgS(_nickel, t, _trappingS)
               + (_magEnergy > 0 && _magTimeS > 0 ? MagnetarModel.LuminosityErgS(t, _magEnergy, _magTimeS) : 0);

        private double Diffused(double t) => ArnettLuminosityErgS(Heating, DiffusionTimeS, t);

        /// <summary>
        /// The plateau L50 held until t_p, then a Fermi-Dirac step, 1 / (1 + e^((t − t_p)/w)), onto the deposited ⁵⁶Ni
        /// power (§4.3 steps 2–4).
        /// </summary>
        private double PlateauPart(double t)
        {
            double x = (t - PlateauEndS) / PlateauDropWidthS;
            double s = x > 700 ? 0 : 1 / (1 + Math.Exp(x));
            return _l50 * s + DepositedNickelHeatingErgS(_nickel, t, _trappingS) * (1 - s);
        }

        /// <summary>Everything except shock cooling: the diffused heating, or the plateau where it is brighter.</summary>
        private double Main(double t) => Plateau ? Math.Max(PlateauPart(t), Diffused(t)) : Diffused(t);

        private double Cooling(double t)
            => _cooling is { } c && t >= c.StartS && c.FadingLuminosityErgS(t) is var l && l > 0 ? l : 0;

        /// <summary>Bolometric luminosity at source time t (s), erg/s: the brighter of shock cooling and the rest.</summary>
        public double Luminosity(double t) => Math.Max(Cooling(t), Main(t));

        // ------------------------------------------------ colour and magnitude

        /// <summary>
        /// [APPROX] The photosphere's temperature once shock cooling is over (§4.5): hydrogen recombination for a plateau,
        /// the hydrogen-free peak value otherwise.
        /// </summary>
        private double PhotosphereK => Plateau ? HydrogenPhotosphereK : HotPhotosphereK;

        /// <summary>
        /// The shock-cooling colour temperature (K), continued past the formula's end, but [APPROX] never below
        /// <see cref="PhotosphereK"/>: the cooling envelope's photosphere stops cooling where its gas recombines. NaN
        /// without cooling.
        /// </summary>
        private double CoolingColourK(double t)
            => _cooling is { } c && t >= c.StartS ? Math.Max(c.FadingColourTemperatureK(t), PhotosphereK) : double.NaN;

        /// <summary>
        /// B−V of everything except shock cooling (§4.5). A plateau shares the cooling envelope's photosphere: the shock-
        /// cooling colour temperature until it reaches hydrogen recombination's 6,000 K [APPROX].
        /// </summary>
        private double MainBMinusV(double t)
        {
            if (Plateau)
                return BolometricCorrection.BlackbodyBMinusV(_cooling is null ? PhotosphereK : CoolingColourK(t));
            double peakColour = BolometricCorrection.BlackbodyBMinusV(HotPhotosphereK);
            if (!Thermonuclear) return peakColour;
            // Type Ia: [APPROX] the peak colour until bolometric maximum (taken as V maximum), then linear to the Lira
            // relation's start 30 d later; Lira 30–90 d; held at its 90 d value after.
            double days = (t - MainPeakS) / Day;
            if (days <= 0) return peakColour;
            if (days < LiraStartDays)
                return peakColour + (BolometricCorrection.LiraBMinusV(LiraStartDays) - peakColour) * days / LiraStartDays;
            return BolometricCorrection.LiraBMinusV(Math.Min(days, LiraEndDays));
        }

        /// <summary>
        /// Absolute V magnitude at source time t (s): M_bol of the total luminosity less BC_V at the luminosity-weighted
        /// B−V of shock cooling (a blackbody at T_col, continued past the formula's end) and the rest [APPROX: the
        /// weighting keeps the colour continuous]. NaN when there is no light.
        /// </summary>
        public double AbsoluteV(double t)
        {
            double lc = Cooling(t), lm = Main(t), l = Math.Max(lc, lm);
            if (!(l > 0)) return double.NaN;
            double bv = MainBMinusV(t);
            if (lc > 0)
                bv = (lc * BolometricCorrection.BlackbodyBMinusV(CoolingColourK(t)) + lm * bv) / (lc + lm);
            return BolometricCorrection.AbsoluteV(l, bv, BcSample);
        }

        // ------------------------------------------------ peaks

        /// <summary>The bolometric peak of <see cref="Main"/> (s), by golden-section search; for the Lira relation.</summary>
        private double MainPeakS => _mainPeakS.Value;

        /// <summary>The time (s) of the brightest absolute V, NaN if there is no light.</summary>
        public double PeakTimeS => _peak.Value.TimeS;

        /// <summary>The brightest absolute V.</summary>
        public double PeakAbsoluteV => _peak.Value.AbsV;

        private (double TimeS, double AbsV) FindPeak()
        {
            var (t, negV) = Maximize(t => AbsoluteV(t) is var v && !double.IsNaN(v) ? -v : double.NegativeInfinity, StartS, EndS);
            return double.IsFinite(negV) ? (t, -negV) : (double.NaN, double.NaN);
        }

        /// <summary>
        /// A visible plateau: the plateau, not the diffused heating, sets the light as it ends (false for a compact blue
        /// supergiant, whose ⁵⁶Ni dome outshines its faint plateau).
        /// </summary>
        public bool HasVisiblePlateau => _visiblePlateau.Value;

        /// <summary>
        /// The maximum of f over [lo, hi] (s): the best of 240 log-spaced samples, refined by golden-section search in
        /// log t between its neighbours.
        /// </summary>
        private static (double TimeS, double Value) Maximize(Func<double, double> f, double lo, double hi)
        {
            const int n = 240;
            double a = Math.Log(lo), step = (Math.Log(hi) - a) / (n - 1);
            int best = 0;
            double bestValue = double.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                double v = f(Math.Exp(a + i * step));
                if (v > bestValue) (best, bestValue) = (i, v);
            }
            if (!double.IsFinite(bestValue)) return (double.NaN, bestValue);
            double x0 = a + Math.Max(best - 1, 0) * step, x1 = a + Math.Min(best + 1, n - 1) * step;
            double g = (Math.Sqrt(5) - 1) / 2;
            double c = x1 - g * (x1 - x0), d = x0 + g * (x1 - x0), fc = f(Math.Exp(c)), fd = f(Math.Exp(d));
            for (int i = 0; i < 40; i++)
            {
                if (fc > fd) { x1 = d; d = c; fd = fc; c = x1 - g * (x1 - x0); fc = f(Math.Exp(c)); }
                else { x0 = c; c = d; fc = fd; d = x0 + g * (x1 - x0); fd = f(Math.Exp(d)); }
            }
            double tBest = Math.Exp(0.5 * (x0 + x1)), vBest = f(tBest);
            return vBest >= bestValue ? (tBest, vBest) : (Math.Exp(a + best * step), bestValue);
        }
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>1 − e^(−y) without cancellation for small y.</summary>
    private static double OneMinusExpNeg(double y) => y < 1e-5 ? y * (1 - y / 2) : 1 - Math.Exp(-y);

    private static double Apparent(double absV, ObserverSetup obs)
        => absV + obs.DistanceModulus + Extinction.A(Band.V, obs.Av);

    /// <summary>A pulsational pair-instability event whose shells collide (Type IIn, Ibn, Icn): light, but not modeled.</summary>
    private static bool IsUnmodeledTransient(Outcome o)
        => o.Fate == Fate.PulsationalPairInstability && o.Type is SupernovaType.IIn or SupernovaType.Ibn or SupernovaType.Icn;

    private static string Caveats(Curve c)
    {
        string text = "";
        if (c.Interacting)
            text += " The collision with the dense gas around the star adds light that this model does not include, so it " +
                    "is probably brighter than shown (Type IIn average about −18.6 at peak against −16.8 for II-P).";
        if (c.HasMagnetar)
            text += " The magnetar's power is assumed fully trapped, so the late light is overestimated.";
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
        Fate.FailedSupernova =>
            "No supernova: the core collapses straight to a black hole, so there is no explosion to light up. The star may " +
            "simply vanish, perhaps after a weak, red outburst of about a million Suns as its loosened envelope drifts off " +
            "(Lovegrove & Woosley 2013). The red supergiant N6946-BH1 faded from view this way after 2009 (Adams et al. " +
            "2017), though a merger of two stars hidden in dust is not ruled out.",
        Fate.PulsationalPairInstability =>
            "The pulses throw off too little mass to make a visible transient, and the core collapses to a black hole " +
            "without a supernova.",
        _ => "There is no explosion, so there is no supernova light.",
    };
}
