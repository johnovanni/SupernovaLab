using System.Globalization;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>
/// Gamma-ray lines from radioactive decay (signals §5, §11): ⁵⁶Ni (158, 812 keV), ⁵⁶Co (847, 1238 keV) and ⁴⁴Ti
/// (68, 78, 1157 keV) photon fluxes at Earth, and how INTEGRAL/SPI, COSI and NuSTAR see them.
/// <list type="bullet">
/// <item>Flux (§5.2): F_line(t) = [N(t)/τ] I_line f_esc(t) / (4π D²), with the Bateman ⁵⁶Ni → ⁵⁶Co chain and the ⁴⁴Ti
/// decay. Line escape (ruling R37): a line photon is lost on any Compton scatter, so f_esc = exp(−(T0_line/t)²) with
/// T0_line = T0 √(κ_KN(E_line)/κ_γ), where T0 and κ_γ = 0.03 are the optical tail's (§4.1,
/// <see cref="LightCurveModel.GammaRayTrappingTimeS"/>) and κ_KN is the Klein–Nishina cross-section per gram for
/// Y_e = 0.5. [APPROX] Photo-absorption of the 68/78 keV lines is ignored.</item>
/// <item>⁴⁴Ti (ruling R35): the outcome's <see cref="Outcome.Ti44Mass"/>, for core-collapse supernovae only. Type Ia,
/// Iax and pair-instability supernovae get no ⁴⁴Ti lines: their yields are not in the research.</item>
/// <item>Detection (§5.4): the line flux averaged over the instrument's observing window is compared with its 3σ
/// narrow-line sensitivity scaled as (T_obs / T_ref)^(−1/2) [APPROX, background-dominated]. The windows are stated
/// defaults (<see cref="ObservingWindowS"/>); the best of the lines the instrument covers decides.</item>
/// </list>
/// Source time throughout (from core bounce, or ignition for a Type Ia), 1 day to 1,000 years. Speculative (rulings
/// R24/R27): the decay physics is established, but the escape fraction, the ⁴⁴Ti rule and the sensitivities are [APPROX].
/// </summary>
public sealed class GammaLineModel : ISignalModel
{
    // ---------------------------------------------------------------- constants

    private const double Day = PhysicalConstants.Day, Year = PhysicalConstants.Year;

    /// <summary>⁴⁴Ti mean life, s: half-life 58.9 ± 0.3 yr (Ahmad et al. 2006; §5.1) / ln 2 = 84.97 yr.</summary>
    public const double Ti44MeanLifeS = 58.9 / 0.69314718055994531 * Year;

    /// <summary>
    /// [APPROX] Start of the model: one day. The lines escape only once the ejecta thin out (§11: ⁵⁶Ni lines from about
    /// 10 d); before a day f_esc ≈ 0 for every outcome (T0 is at least about a week).
    /// </summary>
    public const double StartS = Day;

    /// <summary>The end of the timeline, 1,000 years (§11: ⁴⁴Ti over 1–1,000 yr).</summary>
    public const double EndS = 1000 * Year;

    private enum Isotope { Ni56, Co56, Ti44 }

    /// <summary>A line: its channel, energy (keV), photons per decay and parent isotope (§5.1).</summary>
    private sealed record Line(string Channel, double KeV, double Intensity, Isotope Isotope)
    {
        public string Name => $"{KeV.ToString("0", CultureInfo.InvariantCulture)} keV ({IsotopeName(Isotope)})";
    }

    /// <summary>
    /// §5.1 (Junde et al. 2011, NNDC; Churazov et al. 2014; Ahmad et al. 2006). The 847 keV and 1157 keV lines are
    /// "about 100%" per decay and are taken as 1.
    /// </summary>
    private static readonly Line[] Lines =
    {
        new("line158", 158.38, 0.988, Isotope.Ni56),
        new("line812", 811.85, 0.860, Isotope.Ni56),
        new("line847", 846.77, 1.00, Isotope.Co56),
        new("line1238", 1238.3, 0.66, Isotope.Co56),
        new("ti44_68", 67.87, 0.930, Isotope.Ti44),
        new("ti44_78", 78.32, 0.968, Isotope.Ti44),
        new("ti44_1157", 1157.0, 1.00, Isotope.Ti44),
    };

    private static readonly Dictionary<string, Line> LineById = Lines.ToDictionary(l => l.Channel, StringComparer.Ordinal);

    /// <summary>The detectors whose sensitivity this model compares against, each with a channel.</summary>
    public static IReadOnlyList<string> DetectorIds { get; } = new[] { "spi", "cosi", "nustar" };

    /// <summary>
    /// The lines each instrument covers: SPI 20 keV–8 MeV, with the research's sensitivity quoted near 1 MeV, so its
    /// ⁵⁶Ni/⁵⁶Co lines and ⁴⁴Ti's 1157 keV; COSI 0.2–5 MeV (not 158 keV or the hard X-ray ⁴⁴Ti lines); NuSTAR up to 79
    /// keV, so ⁴⁴Ti's 68 and 78 keV lines only (§5.4).
    /// </summary>
    private static Line[] LinesSeenBy(string detectorId) => detectorId switch
    {
        "spi" => Lines.Where(l => l.Channel is "line158" or "line812" or "line847" or "line1238" or "ti44_1157").ToArray(),
        "cosi" => Lines.Where(l => l.Channel is "line812" or "line847" or "line1238" or "ti44_1157").ToArray(),
        "nustar" => Lines.Where(l => l.Isotope == Isotope.Ti44 && l.KeV < 100).ToArray(),
        _ => Array.Empty<Line>(),
    };

    /// <summary>
    /// The exposure the registry sensitivity refers to, s of observer time: SPI 10⁶ s (Roques et al. 2003); COSI the 2-yr
    /// survey (Tomsick et al. 2023); NuSTAR 2.6 Ms, the depth of its SN 1987A detection (ruling R38 [APPROX]).
    /// </summary>
    private static double ReferenceExposureS(string detectorId) => detectorId switch
    {
        "spi" => 1e6,
        "cosi" => 2 * Year,
        "nustar" => 2.6e6,
        _ => throw new ArgumentException($"No line sensitivity for '{detectorId}'.", nameof(detectorId)),
    };

    /// <summary>
    /// [APPROX] The default observing window, s of observer time, ending at the moment shown. SPI: one 10⁶ s pointing,
    /// the reference exposure. COSI (a survey; the exposure fraction cancels when its sensitivity is scaled by elapsed
    /// survey time): about 100 days of survey for the ⁵⁶Ni/⁵⁶Co lines (§5.4's own figure), the full 2-yr survey for the
    /// slowly decaying ⁴⁴Ti. NuSTAR: its 2.6 Ms reference exposure (ruling R38).
    /// </summary>
    public static double ObservingWindowS(string detectorId, bool ti44) => detectorId switch
    {
        "spi" => 1e6,
        "cosi" => ti44 ? 2 * Year : 100 * Day,
        "nustar" => 2.6e6,
        _ => throw new ArgumentException($"No line observing window for '{detectorId}'.", nameof(detectorId)),
    };

    /// <summary>Simpson intervals for a window average.</summary>
    private const int WindowIntervals = 64;

    // ---------------------------------------------------------------- ISignalModel

    public string Id => "gammalines";
    public Messenger Messenger => Messenger.GammaRays;
    public Confidence Confidence => Confidence.Speculative;

    public IReadOnlyList<SignalChannel> Channels { get; } = BuildChannels();

    private static IReadOnlyList<SignalChannel> BuildChannels()
    {
        const string unit = "photons cm⁻² s⁻¹";
        var list = Lines.Select(l => new SignalChannel(l.Channel, $"{l.Name} line flux", unit, true)).ToList();
        foreach (string id in DetectorIds)
            list.Add(new(ChannelId.Compose("detect", id),
                $"Brightest line over {Detectors.Get(id).Name}'s limit for its observing window (≥ 1: detected)", "", true));
        return list;
    }

    public IReadOnlyList<Source> Sources { get; } = new Source[]
    {
        new("Junde et al. 2011, Nucl. Data Sheets 112, 1513 (⁵⁶Ni lines); Churazov et al. 2014, Nature 512, 406 (⁵⁶Co " +
            "lines); Ahmad et al. 2006, PRC 74, 065803 (⁴⁴Ti, 58.9 yr)", "signals §5.1"),
        new("F = [N(t)/τ] I f_esc / (4πD²), f_esc = exp(−(T0_line/t)²), T0_line = T0 √(κ_KN(E)/κ_γ): the light curve's T0 " +
            "(Clocchiatti & Wheeler 1997, κ_γ = 0.03 [APPROX]) scaled to each line's Klein–Nishina opacity (Y_e = 0.5), " +
            "since any Compton scatter removes a photon from the line (ruling R37 [APPROX]); supernova lines are Doppler-broadened " +
            "(about 4,000 km/s for SN 2014J), which makes them harder to detect than the narrow lines the sensitivities " +
            "assume (not modeled)", "signals §5.2, §5.3"),
        new("⁴⁴Ti mass = 2×10⁻³ × ⁵⁶Ni mass for core-collapse supernovae [APPROX], from SN 1987A and Cas A, unless a preset " +
            "pins it; not modeled for Type Ia/Iax or pair-instability supernovae (not in the research)",
            "signals §5.3; ruling R35"),
        new("Line width (ruling R52 [APPROX]): the narrow-line limits are raised by √max(1, ΔE_line / ΔE_instr), the line's " +
            "FWHM from SN 2014J's ≈4,100 km/s rms for Type Ia (× 2.355, a Gaussian), or a velocity scale taken directly as " +
            "the FWHM: 5,000 km/s for Type Iax (R45c), the ejecta velocity √(10E/3M) otherwise (a uniform sphere's FWHM " +
            "would be ~1.4× that edge speed, but ⁵⁶Co lies inside it); against " +
            "SPI's ≈2 keV and COSI's 6–9 keV resolution; NuSTAR's ⁴⁴Ti lines are taken as narrow. INTEGRAL saw none of " +
            "SN 2011fe's lines (Isern et al. 2013; 830–875 keV < 1.39e-4, 2σ, days 45–88, ATel 3822), which its authors " +
            "put down to the lines' 20–40 keV width", "signals §5.3, §5.4; ruling R52"),
        new("INTEGRAL/SPI 3e-5 for 10⁶ s (Roques et al. 2003; operations ended 2025), its 1 MeV sensitivity used for every " +
            "line [APPROX]; COSI 3.0e-6 for its 2-yr survey " +
            "(Tomsick et al. 2023, projected), used for every line; NuSTAR 2e-6 for 2.6 Ms, anchored to its Cas A and " +
            "SN 1987A detections (Boggs et al. 2015; ruling R38) [APPROX, verify]. Sensitivity scaled as (T/T_ref)^(−1/2); " +
            "windows: SPI 10⁶ s, COSI 100 d (⁵⁶Co) or 2 yr (⁴⁴Ti), NuSTAR 2.6 Ms [APPROX]", "signals §5.4, §11"),
    };

    public bool Applies(Outcome outcome) => AppliesTo(outcome);

    private static bool AppliesTo(Outcome o)
        => o.Fate is Fate.Supernova or Fate.PairInstabilitySupernova
           && o.NickelMass?.Value > 0 && o.EjectaMass?.Value > 0 && o.ExplosionEnergyErg?.Value > 0;

    public (double StartS, double EndS) ValidRange(Outcome outcome) => AppliesTo(outcome) ? (StartS, EndS) : (0, 0);

    public double Sample(Outcome outcome, ObserverSetup observer, double sourceTimeS, string channelId)
    {
        var id = ParseChannel(channelId);
        if (!AppliesTo(outcome) || !(sourceTimeS >= StartS && sourceTimeS <= EndS)) return double.NaN;
        if (id.DetectorId is { } detectorId) return BestLine(outcome, observer, sourceTimeS, detectorId)?.Ratio ?? double.NaN;
        return LineFlux(outcome, observer, LineById[id.Base], sourceTimeS);
    }

    public Detection Detect(Outcome outcome, ObserverSetup observer, double sourceTimeS, Detector detector)
    {
        if (!AppliesTo(outcome)) return new Detection(DetectionStatus.NotProduced, NotProducedReason(outcome));
        if (!DetectorIds.Contains(detector.Id))
            return new Detection(DetectionStatus.NotModeled, $"{detector.Name} does not observe gamma-ray lines.");
        if (LinesSeenBy(detector.Id).All(l => !Modeled(outcome, l)))
            return new Detection(DetectionStatus.NotModeled,
                $"{detector.Name} sees only the hard X-ray lines of ⁴⁴Ti here, and {Ti44NotModeledReason(outcome)}");

        double z1 = observer.TimeDilation;
        string origin = Thermonuclear(outcome) ? "ignition" : "core collapse";
        if (!(sourceTimeS >= StartS))
            return new Detection(DetectionStatus.NotYet,
                $"The ejecta are still opaque to gamma rays; the lines start to escape days to weeks after {origin}.");
        if (sourceTimeS > EndS)
            return new Detection(DetectionStatus.NotModeled,
                $"The model ends {UnitFormat.Time(EndS * z1)} after {origin}.");

        var best = BestLine(outcome, observer, sourceTimeS, detector.Id)!;
        var status = best.Ratio >= 1 ? DetectionStatus.Detected : DetectionStatus.TooFaint;
        double escape = LineEscapeFraction(TrappingTimeS(outcome), best.Line.KeV, sourceTimeS);
        double reference = ReferenceExposureS(detector.Id);
        string scaled = Math.Abs(best.WindowObsS - reference) < 1e-6 * reference
            ? "approximate"
            : $"its {FormatFlux(detector.Threshold)} for {UnitFormat.Time(reference)} scaled as 1/√time; approximate";
        string broad = best.WidthFactor > 1
            ? $", raised {best.WidthFactor.ToString("0.0", CultureInfo.InvariantCulture)}-fold because the line is broad: the " +
              $"expanding ejecta smear it over about {best.LineWidthKeV.ToString("0", CultureInfo.InvariantCulture)} keV, " +
              $"against the instrument's {InstrumentResolutionKeV(detector.Id, best.Line.KeV).ToString("0.#", CultureInfo.InvariantCulture)} keV " +
              "resolution, so it stands on more background"
            : "";
        string reason =
            $"{detector.Name}'s best line is {best.Line.Name}: {FormatFlux(best.AverageFlux)} photons cm⁻² s⁻¹ on average " +
            $"over the last {UnitFormat.Time(best.WindowObsS)} of observing, against the {FormatFlux(best.Limit)} a 3σ " +
            $"detection needs in that time ({scaled}{broad}). " +
            (status == DetectionStatus.Detected ? "Enough to detect it. " : "Too faint. ") +
            $"{Percent(escape)} of the line photons escape the ejecta now.";
        if (!Modeled(outcome, Lines.First(l => l.Isotope == Isotope.Ti44)) && LinesSeenBy(detector.Id).Any(l => l.Isotope == Isotope.Ti44))
            reason += $" ⁴⁴Ti is left out: {Ti44NotModeledReason(outcome)}";
        if (observer.Av > 0) reason += " Gamma rays pass through dust unaffected.";
        return new Detection(status, reason);
    }

    // ---------------------------------------------------------------- public formulas (tested against signals §5)

    private const double SpeedOfLightKmS = PhysicalConstants.C / 1e5;

    /// <summary>
    /// Ruling R52 [APPROX]: a Type Ia's line FWHM, km/s: SN 2014J's ≈4,100 km/s rms line width (Churazov et al. 2014;
    /// signals §5.3) × 2.355.
    /// </summary>
    public const double ThermonuclearLineFwhmKmS = 4100 * 2.355;

    /// <summary>
    /// Ruling R52 [APPROX]: the γ-ray line FWHM, km/s. Type Ia: <see cref="ThermonuclearLineFwhmKmS"/>, a measured rms
    /// (Gaussian σ) × 2.355. Type Iax: the R45c velocity scale 5,000 km/s (<see cref="SpectrumModel.IaxKmS"/>). Otherwise
    /// the ejecta velocity scale v_ej = √(10 E / 3 M_ej) (signals §0.4). The two velocity scales are taken directly as the
    /// FWHM: for a uniform sphere, whose edge is v_ej, the true FWHM is about 1.4 v_ej, but the ⁵⁶Co lies inside the edge,
    /// so the error runs both ways. NaN without ejecta.
    /// </summary>
    public static double LineFwhmKmS(Outcome o)
    {
        if (o.Type == SupernovaType.Iax) return SpectrumModel.IaxKmS;
        if (Thermonuclear(o)) return ThermonuclearLineFwhmKmS;
        if (o.EjectaMass is not { Value: > 0 } m || o.ExplosionEnergyErg is not { Value: > 0 } e) return double.NaN;
        return ArnettDiffusion.EjectaVelocityCmS(e.Value, m.Value) / 1e5;
    }

    /// <summary>
    /// The instrument's energy resolution (FWHM, keV) at a line energy (§5.4): SPI about 2 keV at 1 MeV, used at every
    /// energy [APPROX]; COSI 6.0 keV at 511 keV and 9.0 keV at 1,157 keV, linear between and held outside [APPROX]. NaN for
    /// NuSTAR, whose ⁴⁴Ti lines keep the narrow-line figure (ruling R52).
    /// </summary>
    public static double InstrumentResolutionKeV(string detectorId, double lineKeV) => detectorId switch
    {
        "spi" => 2.0,
        "cosi" => 6.0 + 3.0 * Math.Clamp((lineKeV - 511) / (1157 - 511), 0, 1),
        _ => double.NaN,
    };

    /// <summary>A line channel's energy, keV (§5.1). Throws <see cref="ArgumentException"/> for another channel.</summary>
    public static double LineKeV(string lineChannel)
        => LineById.TryGetValue(lineChannel, out var l) ? l.KeV : throw new ArgumentException($"No line '{lineChannel}'.", nameof(lineChannel));

    /// <summary>
    /// The limit to draw against a line's curve (review M1): the instrument's 3σ narrow-line sensitivity scaled to its
    /// default observing window, S_ref (T_ref / T_window)^(1/2), and raised for the line's width (ruling R52): the limit
    /// the badge compares the window-averaged flux with, once the supernova is older than the window. NaN when the
    /// instrument does not see the line or the outcome does not make it.
    /// </summary>
    public static double DrawnLimit(Outcome outcome, string detectorId, string lineChannel)
    {
        if (!AppliesTo(outcome) || !LineById.TryGetValue(lineChannel, out var line) || !DetectorIds.Contains(detectorId))
            return double.NaN;
        if (!LinesSeenBy(detectorId).Contains(line) || !Modeled(outcome, line)) return double.NaN;
        double window = ObservingWindowS(detectorId, line.Isotope == Isotope.Ti44);
        return Detectors.Get(detectorId).Threshold * Math.Sqrt(ReferenceExposureS(detectorId) / window)
               * LineWidthFactor(detectorId, line.KeV, LineFwhmKmS(outcome));
    }

    /// <summary>
    /// Ruling R52 [APPROX, Speculative]: the factor by which a broad line raises the narrow-line, background-limited
    /// threshold, √max(1, ΔE_line / ΔE_instr), with ΔE_line = E × FWHM / c. 1 for NuSTAR (its ⁴⁴Ti lines are taken as
    /// narrow) and for a line no wider than the instrument's resolution.
    /// </summary>
    public static double LineWidthFactor(string detectorId, double lineKeV, double fwhmKmS)
    {
        double resolution = InstrumentResolutionKeV(detectorId, lineKeV);
        if (!double.IsFinite(resolution) || !(fwhmKmS > 0)) return 1;
        return Math.Sqrt(Math.Max(1, lineKeV * fwhmKmS / SpeedOfLightKmS / resolution));
    }

    /// <summary>§5.2: ⁵⁶Ni decays per second, N0 e^(−t/τ_Ni) / τ_Ni, N0 = M_Ni / (56 m_u). M in M☉, t in s.</summary>
    public static double NickelDecayRate(double nickelMsun, double timeS)
        => Atoms(nickelMsun, 56) * Math.Exp(-timeS / LightCurveModel.NickelMeanLifeS) / LightCurveModel.NickelMeanLifeS;

    /// <summary>
    /// §5.2: ⁵⁶Co decays per second, N_Co/τ_Co with N_Co = N0 τ_Co/(τ_Co − τ_Ni) (e^(−t/τ_Co) − e^(−t/τ_Ni)); the mean
    /// lives are the light curve's (8.8 d, 111.3 d; §4.1).
    /// </summary>
    public static double CobaltDecayRate(double nickelMsun, double timeS)
    {
        double tauNi = LightCurveModel.NickelMeanLifeS, tauCo = LightCurveModel.CobaltMeanLifeS;
        double n = Atoms(nickelMsun, 56) * tauCo / (tauCo - tauNi) * (Math.Exp(-timeS / tauCo) - Math.Exp(-timeS / tauNi));
        return n / tauCo;
    }

    /// <summary>⁴⁴Ti decays per second, N0 e^(−t/τ) / τ, N0 = M / (44 m_u), τ = 84.97 yr (§5.1). ⁴⁴Sc follows within hours.</summary>
    public static double Ti44DecayRate(double ti44Msun, double timeS)
        => Atoms(ti44Msun, 44) * Math.Exp(-timeS / Ti44MeanLifeS) / Ti44MeanLifeS;

    /// <summary>§5.2: the escape fraction f_esc = exp(−(T0/t)²); 0 at t ≤ 0.</summary>
    public static double EscapeFraction(double trappingTimeS, double timeS)
        => timeS > 0 ? Math.Exp(-Math.Pow(trappingTimeS / timeS, 2)) : 0;

    /// <summary>
    /// The Klein–Nishina total cross-section per gram, cm²/g, at photon energy E (keV) for Y_e = 0.5 electrons per
    /// nucleon (ruling R37): κ = σ_KN Y_e / m_u with x = E / m_e c² and
    /// σ_KN = 2π r_e² {(1+x)/x² [2(1+x)/(1+2x) − ln(1+2x)/x] + ln(1+2x)/(2x) − (1+3x)/(1+2x)²}. Below x = 10⁻³ the
    /// series σ_T (1 − 2x + 26x²/5) avoids cancellation.
    /// </summary>
    public static double KleinNishinaOpacity(double energyKeV)
    {
        const double ye = 0.5;
        double re = PhysicalConstants.ClassicalElectronRadius, x = energyKeV / PhysicalConstants.ElectronRestEnergyKeV;
        double sigma;
        if (x < 1e-3)
            sigma = 8 * Math.PI / 3 * re * re * (1 - 2 * x + 26.0 / 5 * x * x);
        else
        {
            double l = Math.Log(1 + 2 * x);
            sigma = 2 * Math.PI * re * re * ((1 + x) / (x * x) * (2 * (1 + x) / (1 + 2 * x) - l / x) + l / (2 * x)
                                             - (1 + 3 * x) / Math.Pow(1 + 2 * x, 2));
        }
        return sigma * ye / PhysicalConstants.AtomicMassUnit;
    }

    /// <summary>
    /// Ruling R37: a line's escape fraction exp(−(T0_line/t)²), T0_line = T0 √(κ_KN(E)/κ_γ), from §4.1's trapping time
    /// T0 (s), the line energy (keV) and the time (s).
    /// </summary>
    public static double LineEscapeFraction(double trappingTimeS, double lineKeV, double timeS)
        => EscapeFraction(trappingTimeS * Math.Sqrt(KleinNishinaOpacity(lineKeV) / LightCurveModel.KappaGamma), timeS);

    /// <summary>§5.2: line photon flux, photons cm⁻² s⁻¹: decays/s × I_line × f_esc / (4π D²).</summary>
    public static double LinePhotonFlux(double decaysPerS, double intensity, double escapeFraction, double distanceCm)
        => decaysPerS * intensity * escapeFraction / (4 * Math.PI * distanceCm * distanceCm);

    /// <summary>
    /// Ruling R35: a ⁴⁴Ti line's photon flux, photons cm⁻² s⁻¹, from an initial ⁴⁴Ti mass (M☉) at this age (s) and
    /// distance (cm), with the line's photons per decay and an escape fraction (1 for an old remnant).
    /// </summary>
    public static double Ti44LineFlux(double ti44Msun, double ageS, double distanceCm, double intensity, double escapeFraction = 1)
        => LinePhotonFlux(Ti44DecayRate(ti44Msun, ageS), intensity, escapeFraction, distanceCm);

    /// <summary>
    /// The line's photon flux averaged over source time [fromS, toS] (Simpson's rule), photons cm⁻² s⁻¹ at Earth;
    /// NaN when the outcome does not make this line.
    /// </summary>
    public double AverageFlux(Outcome outcome, ObserverSetup observer, string lineChannel, double fromS, double toS)
    {
        var id = ParseChannel(lineChannel);
        if (id.DetectorId is not null) throw new ArgumentException("Not a line channel.", nameof(lineChannel));
        if (!AppliesTo(outcome)) return double.NaN;
        return Average(outcome, observer, LineById[id.Base], fromS, toS);
    }

    // ---------------------------------------------------------------- the model

    private static double Atoms(double massMsun, int massNumber)
        => massMsun * PhysicalConstants.MSun / (massNumber * PhysicalConstants.AtomicMassUnit);

    private static bool Thermonuclear(Outcome o)
        => o.Type is SupernovaType.Ia or SupernovaType.Ia91T or SupernovaType.Ia91bg or SupernovaType.Iax;

    private static double TrappingTimeS(Outcome o)
        => LightCurveModel.GammaRayTrappingTimeS(o.EjectaMass!.Value, o.ExplosionEnergyErg!.Value);

    /// <summary>Whether this outcome makes the line: ⁵⁶Ni/⁵⁶Co always, ⁴⁴Ti only with an outcome ⁴⁴Ti mass (R35).</summary>
    private static bool Modeled(Outcome o, Line l) => l.Isotope != Isotope.Ti44 || o.Ti44Mass is { Value: > 0 };

    /// <summary>
    /// Photon flux at Earth: the photons emitted per source second spread over 4π D_c² and arriving (1 + z) times more
    /// slowly, i.e. rate (1 + z) / (4π D_L²). Line energies are not redshifted here (z ≤ 0.2 in the lab).
    /// </summary>
    private static double LineFlux(Outcome o, ObserverSetup obs, Line l, double t)
    {
        if (!Modeled(o, l)) return double.NaN;
        double rate = l.Isotope switch
        {
            Isotope.Ni56 => NickelDecayRate(o.NickelMass!.Value, t),
            Isotope.Co56 => CobaltDecayRate(o.NickelMass!.Value, t),
            _ => Ti44DecayRate(o.Ti44Mass!.Value, t),
        };
        double flux = LinePhotonFlux(rate, l.Intensity, LineEscapeFraction(TrappingTimeS(o), l.KeV, t),
            obs.LuminosityDistanceCm);
        return flux * obs.TimeDilation;
    }

    private static double Average(Outcome o, ObserverSetup obs, Line l, double fromS, double toS)
    {
        if (!Modeled(o, l)) return double.NaN;
        if (!(toS > fromS)) return LineFlux(o, obs, l, toS);
        double h = (toS - fromS) / WindowIntervals, sum = 0;
        for (int i = 0; i <= WindowIntervals; i++)
        {
            double w = i == 0 || i == WindowIntervals ? 1 : i % 2 == 1 ? 4 : 2;
            sum += w * LineFlux(o, obs, l, fromS + i * h);
        }
        return sum * h / 3 / (toS - fromS);
    }

    private sealed record LineVerdict(Line Line, double AverageFlux, double Limit, double WindowObsS, double WidthFactor,
        double LineWidthKeV)
    {
        public double Ratio => AverageFlux / Limit;
    }

    /// <summary>
    /// §5.4: for each line the instrument covers, the flux averaged over the observing window that ends now (in observer
    /// time; shorter while the supernova is younger than the window) against the sensitivity scaled to that window,
    /// S = S_ref (T_ref / T_obs)^(1/2), raised for a broad line by <see cref="LineWidthFactor"/> (ruling R52). The line
    /// with the best ratio; null when the instrument sees no modeled line.
    /// </summary>
    private static LineVerdict? BestLine(Outcome o, ObserverSetup obs, double t, string detectorId)
    {
        var detector = Detectors.Get(detectorId);
        double z1 = obs.TimeDilation, fwhm = LineFwhmKmS(o);
        LineVerdict? best = null;
        foreach (var l in LinesSeenBy(detectorId).Where(l => Modeled(o, l)))
        {
            double windowObs = Math.Min(ObservingWindowS(detectorId, l.Isotope == Isotope.Ti44), t * z1);
            double from = t - windowObs / z1;
            double width = LineWidthFactor(detectorId, l.KeV, fwhm);
            double limit = detector.Threshold * Math.Sqrt(ReferenceExposureS(detectorId) / windowObs) * width;
            var v = new LineVerdict(l, Average(o, obs, l, from, t), limit, windowObs, width, l.KeV * fwhm / SpeedOfLightKmS);
            if (best is null || v.Ratio > best.Ratio) best = v;
        }
        return best;
    }

    // ---------------------------------------------------------------- text

    private static string IsotopeName(Isotope i) => i switch
    {
        Isotope.Ni56 => "⁵⁶Ni",
        Isotope.Co56 => "⁵⁶Co",
        _ => "⁴⁴Ti",
    };

    private static string FormatFlux(double f) => f.ToString("0.##e0", CultureInfo.InvariantCulture);

    private static string Percent(double fraction)
        => fraction >= 0.995 ? "Nearly all"
            : fraction < 0.005 ? "Almost none"
            : (fraction * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    private static string Ti44NotModeledReason(Outcome o) => Thermonuclear(o)
        ? "⁴⁴Ti yields of thermonuclear (Type Ia) supernovae are not in the research, so they are not modeled."
        : o.Fate == Fate.PairInstabilitySupernova
            ? "⁴⁴Ti yields of pair-instability supernovae are not in the research, so they are not modeled."
            : "this outcome has no ⁴⁴Ti mass.";

    private static string NotProducedReason(Outcome o) => o.Fate switch
    {
        Fate.NotDeadYet => "The star has not died yet, so there are no radioactive gamma-ray lines.",
        Fate.WhiteDwarf => "The star ends as a white dwarf with no explosion, so no radioactive nickel is made.",
        Fate.FailedSupernova =>
            "No supernova: the core collapses straight to a black hole, so no radioactive nickel is thrown out to shine " +
            "in gamma-ray lines.",
        Fate.PulsationalPairInstability =>
            "The pulses throw off the outer layers but make no radioactive nickel, and the core collapses to a black hole.",
        _ => "This explosion throws out no radioactive ⁵⁶Ni, so there are no gamma-ray lines.",
    };

    private static ChannelId ParseChannel(string channelId)
    {
        var id = ChannelId.Parse(channelId);
        bool ok = id.DetectorId is null
            ? LineById.ContainsKey(id.Base)
            : id.Base == "detect" && DetectorIds.Contains(id.DetectorId);
        return ok ? id : throw new ArgumentException($"The gamma-ray line model has no channel '{channelId}'.", nameof(channelId));
    }
}
