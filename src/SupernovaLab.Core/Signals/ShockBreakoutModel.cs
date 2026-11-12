using System.Globalization;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>
/// Shock breakout and shock cooling (signals §3): the X-ray/UV flash when the shock reaches the star's surface, and the
/// cooling glow of the expanding envelope over the following hours to days.
/// <list type="bullet">
/// <item>Delay after bounce: Kistler, Haxton &amp; Yüksel 2013, Eq. 5 (§3.1).</item>
/// <item>Flash: Waxman &amp; Katz 2017 breakout velocity (capped at 0.3c, ruling R28), density, energy E_BO and
/// diffusion time t_bo; observed duration max(R/c, t_bo); luminosity E_BO / duration (§3.2, "peak luminosity for the
/// app").</item>
/// <item>Cooling: Sapir &amp; Waxman 2017 for hydrogen envelopes (with its late suppression), Rabinak &amp; Waxman 2011 for
/// helium envelopes (§3.3), each inside its stated validity; <see cref="Cooling"/> exposes this phase alone.</item>
/// </list>
/// <para>Channels: <c>lum</c> (bolometric erg/s at the source); <c>temp</c> (characteristic temperature, K: the
/// blackbody temperature whose νL_ν peaks where the spectrum does; only in the cooling phase is it the colour temperature
/// T_col); <c>flux@swift-xrt</c> (0.3–10 keV) and <c>flux@einstein-probe</c> (0.5–4 keV) in erg cm⁻² s⁻¹ after
/// absorption by the interstellar gas (a bare <c>flux</c> is Swift XRT); and <c>mag@ultrasat</c>, the 230–290 nm AB
/// magnitude including dust extinction.</para>
/// <para>The model is valid from breakout to the end of the cooling formula's validity (a few hours for a compact star,
/// up to about 3 weeks for a red supergiant); the light-curve model takes over after that. Source time throughout;
/// the timeline stretches it by (1 + z). [APPROX] Band fluxes use the luminosity distance with no K-correction (spec
/// §2.2).</para>
/// <para>Speculative (ruling R24/R27 reasoning): the formulas are published, but the spectral shape, the planar-phase
/// bridge, the envelope classification and the detection thresholds are [APPROX] choices.</para>
/// Only an exploding star with an envelope has a breakout: core-collapse and pair-instability supernovae. Failed
/// supernovae, white dwarfs and thermonuclear explosions (Type Ia, Iax) do not apply. A breakout inside dense
/// circumstellar gas (Type IIn, Ibn, Icn) applies but is not modeled.
/// </summary>
public sealed class ShockBreakoutModel : ISignalModel
{
    // ---------------------------------------------------------------- physical constants

    private const double C = PhysicalConstants.C, MSun = PhysicalConstants.MSun, RSun = PhysicalConstants.RSun;

    private const double StefanBoltzmann = PhysicalConstants.StefanBoltzmann, BoltzmannEv = PhysicalConstants.BoltzmannEv,
        HcEvNm = PhysicalConstants.HcEvNm;

    /// <summary>
    /// [APPROX] Ruling R28: the Waxman &amp; Katz breakout formulas are non-relativistic, so the breakout velocity used for
    /// the flash's energy, diffusion time and peak photon energy is capped at 0.3c. Faster breakouts (very compact,
    /// energetic stars such as SN 2008D's parameters, 0.58c, or magnetar-powered stripped stars) are flagged in the
    /// Detect reason and are a rough sketch only; pair production, not in the research, would regulate their spectra.
    /// </summary>
    public const double MaxBreakoutBeta = 0.3;

    // ---------------------------------------------------------------- envelope classification

    /// <summary>
    /// [APPROX] Electron-scattering opacity of the breakout layer, κ = 0.2 (1 + X) cm²/g: 0.34 for a hydrogen-rich
    /// envelope (the W&amp;K normalization, §3.2) and 0.2 for a hydrogen-free (He or C/O) one. The research gives only κ_0.34.
    /// </summary>
    private const double KappaHydrogen = 0.34, KappaHeliumOrHeavier = 0.20;

    /// <summary>
    /// [APPROX] A hydrogen-rich star at least this large (R☉) has a convective envelope, n = 3/2 (a red or yellow
    /// supergiant); smaller ones and every hydrogen-free star are radiative, n = 3 (§3.1). It is the geometric mean of
    /// the research's red-supergiant (500 R☉) and blue-supergiant (50 R☉) examples; the research gives no boundary.
    /// </summary>
    private static readonly double ConvectiveMinRadiusRsun = Math.Sqrt(500 * 50);

    /// <summary>[APPROX] f_ρ = 1: "of order 1" (§3.3). With it, v_s* = 1.05 √(E/M) needs no β.</summary>
    private const double FRho = 1;

    // ---------------------------------------------------------------- spectrum

    /// <summary>x* where a blackbody's νL_ν peaks: the root of 4(1 − e^−x) = x.</summary>
    private static readonly double PlanckPeakX = SolvePlanckPeak();

    /// <summary>A blackbody's ∫ νL_ν d ln ν in units of its peak νL_ν: (π⁴/15) / g(x*), g(x) = x⁴/(e^x − 1). About 1.3586.</summary>
    private static readonly double PlanckWidth = Math.Pow(Math.PI, 4) / 15 / PlanckG(PlanckPeakX);

    /// <summary>Simpson intervals across a band in log photon energy: 200 for a sampled value, 64 inside fluence integrals.</summary>
    private const int BandIntervals = 200, FluenceBandIntervals = 64;

    // ---------------------------------------------------------------- bands and detectors

    /// <summary>Swift XRT 0.3–10 keV (signals §3.2); Einstein Probe WXT 0.5–4 keV (§3.3); ULTRASAT 230–290 nm (§3.3).</summary>
    private const double XrtLowEv = 300, XrtHighEv = 10_000, EpLowEv = 500, EpHighEv = 4_000;

    private const double UltrasatShortNm = 230, UltrasatLongNm = 290;
    private static readonly double UltrasatLowEv = HcEvNm / UltrasatLongNm, UltrasatHighEv = HcEvNm / UltrasatShortNm;

    /// <summary>ULTRASAT's band width in Hz: c (1/230 nm − 1/290 nm).</summary>
    private static readonly double UltrasatBandHz = C * 1e7 * (1 / UltrasatShortNm - 1 / UltrasatLongNm);

    /// <summary>
    /// [APPROX] Ruling R29: the X-ray detectors' registry exposures, t_reg. Swift XRT's 1e-13 erg cm⁻² s⁻¹ is a ~10 ks
    /// figure (signals §6.2); Einstein Probe WXT's 2.5e-11 is for 1,000 s (§3.3, §6.2).
    /// </summary>
    private const double XrtExposureS = 10_000, EpExposureS = 1_000;

    /// <summary>Simpson intervals per segment of the fluence integral, in log time.</summary>
    private const int FluenceIntervals = 48;

    /// <summary>AB magnitudes: m_AB = −2.5 log10 f_ν − 48.60, f_ν in erg cm⁻² s⁻¹ Hz⁻¹ (Oke &amp; Gunn 1983; signals §4.5).</summary>
    private const double AbZeroPoint = 48.60;

    /// <summary>The detectors whose flux this model gives.</summary>
    public static IReadOnlyList<string> DetectorIds { get; } = new[] { "swift-xrt", "einstein-probe", "ultrasat" };

    private const string DefaultDetectorId = "swift-xrt";

    // ---------------------------------------------------------------- ISignalModel

    public string Id => "breakout";
    public Messenger Messenger => Messenger.XRaysUv;
    public Confidence Confidence => Confidence.Speculative;

    public IReadOnlyList<SignalChannel> Channels { get; } = new SignalChannel[]
    {
        new("lum", "Breakout and cooling luminosity (bolometric)", "erg/s", true),
        new("temp", "Characteristic temperature (spectral peak)", "K", true),
        new(ChannelId.Compose("flux", "swift-xrt"), "Swift XRT flux (0.3–10 keV, after gas absorption)", "erg cm⁻² s⁻¹", true),
        new(ChannelId.Compose("flux", "einstein-probe"), "Einstein Probe WXT flux (0.5–4 keV, after gas absorption)",
            "erg cm⁻² s⁻¹", true),
        new(ChannelId.Compose("mag", "ultrasat"), "ULTRASAT near-UV brightness (230–290 nm, after dust)", "AB mag", false,
            MayBeNegative: true),
    };

    public IReadOnlyList<Source> Sources { get; } = new Source[]
    {
        new("Kistler, Haxton & Yüksel 2013, ApJ 778, 81, Eq. 5 (breakout delay; 10–20% against full models)", "signals §3.1"),
        new("Waxman & Katz 2017, Handbook of Supernovae, arXiv:1607.01293 (breakout velocity, density, energy, duration, " +
            "peak photon energy); Nakar & Sari 2010, ApJ 725, 904 (temperatures, durations)", "signals §3.2"),
        new("Rabinak & Waxman 2011, ApJ 728, 63; Sapir & Waxman 2017, ApJ 838, 130 (shock cooling)", "signals §3.3"),
        new("Spectrum: a blackbody at the equilibrium temperature with a flat Comptonized νL_ν plateau up to the W&K peak " +
            "[APPROX]; gas absorption σ ∝ E^(−8/3) (Morrison & McCammon 1983) with N_H = 2.2e21 A_V [APPROX]",
            "signals §3.2, §8.4"),
        new("Breakout velocity capped at 0.3c (the formulas are non-relativistic); faster, relativistic breakouts — " +
            "outside the Waxman & Katz formulas' range — are shown as a rough sketch [APPROX]", "signals §3.2"),
        new("Hydrogen-free stars are drawn as bare-star breakouts. Observed flashes such as SN 2008D lasted minutes and " +
            "carried ~2e46 erg — likely breakout in a dense stellar wind, which this model does not include (Soderberg et " +
            "al. 2008, Nature 453, 469). Ic/Ic-BL/SLSN-I (C/O) envelopes use the helium cooling formula [APPROX]",
            "signals §3.2, §3.3, §12"),
        new("X-ray detection: the fluence over the last registry exposure since breakout must reach the registry flux × " +
            "exposure (Swift XRT 1e-13 × 10 ks; Einstein Probe WXT 2.5e-11 × 1,000 s), photon-count limited [APPROX]. " +
            "The observed SN 2008D flash (≈6e43 erg/s for ≈300 s) then reaches ≈390 Mpc with XRT (research: ~200 Mpc) " +
            "and ≈78 Mpc with Einstein Probe (research §11's [DERIVED] peak-flux figure: ~140 Mpc). ULTRASAT 22.5 AB mag " +
            "(Shvartzvald et al. 2024, ApJ 964, 74)", "signals §3.3, §6.2, §11"),
    };

    public bool Applies(Outcome outcome) => AppliesTo(outcome);

    private static bool AppliesTo(Outcome outcome)
        => outcome.Fate is Fate.Supernova or Fate.PairInstabilitySupernova
           && outcome.Type is not (SupernovaType.Ia or SupernovaType.Ia91T or SupernovaType.Ia91bg or SupernovaType.Iax)
           && outcome.Star is not null
           && outcome.ExplosionEnergyErg?.Value > 0 && outcome.EjectaMass?.Value > 0;

    public (double StartS, double EndS) ValidRange(Outcome outcome)
        => Breakout.Of(outcome) is { } b ? (b.DelayS, b.DelayS + b.EndS) : (0, 0);

    public double Sample(Outcome outcome, ObserverSetup observer, double sourceTimeS, string channelId)
    {
        var (channel, detectorId) = ParseChannel(channelId);
        if (Breakout.Of(outcome) is not { } b) return double.NaN;
        double tau = sourceTimeS - b.DelayS;
        if (!(tau >= 0 && tau <= b.EndS)) return double.NaN;

        var s = b.At(tau);
        return channel switch
        {
            "lum" => s.Luminosity,
            "temp" => s.PeakEv / (PlanckPeakX * BoltzmannEv),
            "flux" => XrayFlux(s, observer, detectorId!, absorbed: true),
            "mag" => UltrasatMagnitude(s, observer, withDust: true),
            _ => throw new InvalidOperationException(),
        };
    }

    public Detection Detect(Outcome outcome, ObserverSetup observer, double sourceTimeS, Detector detector)
    {
        if (!Applies(outcome)) return new Detection(DetectionStatus.NotProduced, NotProducedReason(outcome));
        if (detector.Messenger != Messenger.XRaysUv)
            return new Detection(DetectionStatus.NotModeled, $"{detector.Name} is not modeled in this view.");
        if (!DetectorIds.Contains(detector.Id))
            return new Detection(DetectionStatus.NotModeled,
                $"{detector.Name} is not modeled for the breakout flash; its X-rays come later, from the interaction with " +
                "the gas around the star.");
        if (Breakout.Of(outcome) is not { } b)
            return new Detection(DetectionStatus.NotModeled,
                "The shock breaks out inside the dense gas around the star; that breakout is not modeled here.");

        double tau = sourceTimeS - b.DelayS;
        if (!(tau >= 0))
            return new Detection(DetectionStatus.NotYet,
                $"The shock has not reached the star's surface yet: breakout comes " +
                $"{UnitFormat.Time(b.DelayS * observer.TimeDilation)} after core collapse.");
        if (tau > b.EndS)
            return new Detection(DetectionStatus.NotModeled,
                $"The shock-cooling model ends {UnitFormat.Time(b.EndS * observer.TimeDilation)} after breakout; the " +
                "light-curve model takes over.");

        return detector.Id == "ultrasat" ? DetectUv(b, observer, tau, detector) : DetectXray(b, observer, tau, detector);
    }

    // ---------------------------------------------------------------- public formulas and markers

    /// <summary>
    /// The breakout time, s after core bounce in source time: Kistler et al. 2013's delay (§3.1), for the timeline marker.
    /// NaN when there is no modeled breakout: the model does not apply (a failed supernova, a white dwarf, a Type Ia or
    /// Iax), or the shock breaks out inside dense circumstellar gas (not modeled).
    /// </summary>
    public double BreakoutTimeS(Outcome o) => Breakout.Of(o)?.DelayS ?? double.NaN;

    /// <summary>
    /// Kistler, Haxton &amp; Yüksel 2013 Eq. 5 (signals §3.1): the delay (s) from bounce to breakout for a polytrope of
    /// index n = 1.5 (red supergiant) or 3 (blue supergiant, Wolf-Rayet). E in erg, masses in M☉, R in R☉.
    /// </summary>
    /// <remarks>
    /// [APPROX] x = M_remnant / M_ej is clamped to 0–1: the fit's polytropes have ejecta heavier than the remnant (the
    /// research's examples span x = 0.1–0.47), and ultra-stripped explosions (x up to ~12) would otherwise stretch the
    /// delay by an extrapolated bracket.
    /// </remarks>
    public static double KistlerDelayS(double n, double energyErg, double ejectaMass, double radiusRsun, double remnantMass)
    {
        double x = Math.Clamp(remnantMass / ejectaMass, 0, 1);
        double scale = Math.Sqrt(1e51 / energyErg) * Math.Sqrt(ejectaMass / 10) * (radiusRsun / 50);
        return n < 2
            ? 7226 * scale * (1 - 0.738 * Math.Pow(x, 0.80) + 0.467 * Math.Pow(x, 1.20))
            : 6851 * scale * (1 - 0.407 * Math.Pow(x, 0.81) + 0.285 * Math.Pow(x, 1.12));
    }

    /// <summary>
    /// [APPROX] Ruling R29: the fluence (erg cm⁻², in the detector's band) an X-ray detector must collect within its
    /// registry exposure: the registry flux × exposure. Swift XRT 1e-13 × 10 ks = 1e-9; Einstein Probe WXT
    /// 2.5e-11 × 1,000 s = 2.5e-8. Photon-count limited, so a short flash needs the same fluence.
    /// </summary>
    public static double FluenceThreshold(Detector detector) => detector.Threshold * ExposureS(detector);

    /// <summary>The registry exposure (observer s) of an X-ray detector: Swift XRT 10 ks, Einstein Probe WXT 1,000 s.</summary>
    public static double ExposureS(Detector detector) => detector.Id switch
    {
        "swift-xrt" => XrtExposureS,
        "einstein-probe" => EpExposureS,
        _ => throw new ArgumentException($"'{detector.Id}' is not a breakout X-ray detector.", nameof(detector)),
    };

    /// <summary>
    /// The shock-cooling phase alone (signals §3.3), for the light-curve model (ruling R28): the bolometric luminosity
    /// and the colour temperature T_col = 1.2 T_ph over the cooling formula's validity, never the flash or the planar
    /// bridge. Null when there is no modeled breakout or no valid cooling phase.
    /// </summary>
    public CoolingPhase? Cooling(Outcome o)
        => Breakout.Of(o) is { } b && b.EndS > b.CoolStartS ? new CoolingPhase(b) : null;

    /// <summary>
    /// The shock-cooling phase of one outcome. Times are source time in seconds after bounce;
    /// <see cref="LuminosityErgS"/> and <see cref="ColourTemperatureK"/> are NaN outside [<see cref="StartS"/>,
    /// <see cref="EndS"/>].
    /// </summary>
    public sealed class CoolingPhase
    {
        private readonly Breakout _b;

        internal CoolingPhase(Breakout b) => _b = b;

        /// <summary>When the cooling formula becomes valid, s after bounce.</summary>
        public double StartS => _b.DelayS + _b.CoolStartS;

        /// <summary>When it stops being valid, s after bounce.</summary>
        public double EndS => _b.DelayS + _b.EndS;

        /// <summary>Bolometric luminosity, erg/s.</summary>
        public double LuminosityErgS(double sourceTimeS)
            => InRange(sourceTimeS) ? _b.CoolingAt(sourceTimeS - _b.DelayS).Luminosity : double.NaN;

        /// <summary>Colour temperature T_col = 1.2 T_ph, K.</summary>
        public double ColourTemperatureK(double sourceTimeS)
            => InRange(sourceTimeS) ? _b.CoolingAt(sourceTimeS - _b.DelayS).ThermalEv / BoltzmannEv : double.NaN;

        /// <summary>
        /// [APPROX] The cooling luminosity (erg/s) continued past <see cref="EndS"/>, where the formula's validity ends: its
        /// value at EndS carried on by the formula's own power law in time, (τ/τ_end)^(−p) (p = 2ε2 = 0.172 for n = 3/2 and
        /// 0.35 for n = 3, Sapir &amp; Waxman 2017's L_RW; 0.03 for Rabinak &amp; Waxman's helium formula), times Sapir &amp;
        /// Waxman's late suppression exp(−(a τ / t_tr)^α) relative to its value at EndS (§3.3: a = 4.57, α = 0.73 for
        /// n = 3; a = 1.67, α = 0.8 for n = 3/2), with t_tr = 19.5 d (κ_0.34 M_env / v_s*,8.5)^(1/2) and M_env the
        /// hydrogen envelope (Type II) or the helium envelope (hydrogen-free stars), as §3.3 says. τ is the time since
        /// breakout. The research gives the formulas inside their validity only; carrying them past the end, and the
        /// suppression onto the helium formula, lets the cooling glow fade instead of stopping. Inside
        /// [<see cref="StartS"/>, EndS] this is <see cref="LuminosityErgS"/>; NaN before StartS.
        /// </summary>
        public double FadingLuminosityErgS(double sourceTimeS)
        {
            if (!(sourceTimeS >= StartS)) return double.NaN;
            if (sourceTimeS <= EndS) return LuminosityErgS(sourceTimeS);
            double ttr = _b.TransparencyTimeS;
            if (!(ttr > 0)) return 0;
            double tau = sourceTimeS - _b.DelayS;
            return LuminosityErgS(EndS) * Math.Pow(tau / _b.EndS, -_b.CoolingPowerLaw)
                   * Math.Exp(_b.SuppressionExponent(_b.EndS, ttr) - _b.SuppressionExponent(tau, ttr));
        }

        /// <summary>
        /// [APPROX] The colour temperature (K) continued past <see cref="EndS"/> as T_col(EndS) (τ / τ_end)^(−1/2), τ the
        /// time since breakout: the t^(−1/2) of Sapir &amp; Waxman 2017's T_ph (§3.3) carried past its validity. Inside
        /// [<see cref="StartS"/>, EndS] this is <see cref="ColourTemperatureK"/>; NaN before StartS.
        /// </summary>
        public double FadingColourTemperatureK(double sourceTimeS)
        {
            if (!(sourceTimeS >= StartS)) return double.NaN;
            if (sourceTimeS <= EndS) return ColourTemperatureK(sourceTimeS);
            return ColourTemperatureK(EndS) / Math.Sqrt((sourceTimeS - _b.DelayS) / _b.EndS);
        }

        private bool InRange(double t) => t >= StartS && t <= EndS;
    }

    // ---------------------------------------------------------------- the breakout and its evolution

    /// <summary>The emission at one moment: bolometric L (erg/s), the thermal floor kT (eV) and the νL_ν peak (eV).</summary>
    internal readonly record struct State(double Luminosity, double ThermalEv, double PeakEv);

    /// <summary>
    /// One outcome's breakout. Times are seconds after breakout (τ) except <see cref="DelayS"/>, which is after bounce.
    /// The flash lasts <see cref="FlashS"/>; a power-law bridge in log L, log T (the planar phase, for which the research
    /// gives no formula [APPROX]) joins it to the cooling formula, valid from <see cref="CoolStartS"/> to
    /// <see cref="EndS"/>.
    /// </summary>
    internal sealed record Breakout(double DelayS, double FlashS, State Flash, double CoolStartS, double EndS,
        bool HydrogenRich, double PolytropeIndex, double EnergyE51, double EjectaMass, double RadiusCm, double EnvelopeMass,
        double BetaBo)
    {
        public static Breakout? Of(Outcome o)
        {
            if (!AppliesTo(o) || o.Inputs.DenseCsm) return null;
            if (o.Type is SupernovaType.IIn or SupernovaType.Ibn or SupernovaType.Icn) return null;

            var star = o.Star!;
            double e = o.ExplosionEnergyErg!.Value, m = o.EjectaMass!.Value, r = star.RadiusCm.Value;
            bool hRich = IsHydrogenRich(o);
            double n = hRich && r / RSun >= ConvectiveMinRadiusRsun ? 1.5 : 3;
            double kappa = hRich ? KappaHydrogen : KappaHeliumOrHeavier;
            double remnant = o.Remnant.Mass?.Value ?? 0;
            double delay = KistlerDelayS(n, e, m, r / RSun, remnant);

            var flash = FlashOf(n, e, m, r, kappa, out double flashS, out double beta);
            double e51 = e / 1e51;
            double envelope = hRich ? star.HydrogenEnvelopeMass.Value : star.HeliumEnvelopeMass.Value;
            var b = new Breakout(delay, flashS, flash, 0, 0, hRich, n, e51, m, r, envelope, beta);

            // Cooling validity (§3.3): from 1.2 M^0.4 E51^-0.5 R13^1.3 h (Rabinak & Waxman 2011) ...
            double coolStart = 1.2 * Math.Pow(m, 0.4) / Math.Sqrt(e51) * Math.Pow(r / 1e13, 1.3) * 3600;
            // ... to T > 0.7 eV, t < 7.4 (R13/κ_0.34)^0.55 d (Sapir & Waxman 2017), or for helium to T_ph > 1.07 eV and
            // t < 1.2 κ_0.34^0.5 M E51^-0.5 d (Rabinak & Waxman 2011).
            double coolEnd = hRich
                ? 7.4 * Math.Pow(r / 1e13 / (kappa / 0.34), 0.55) * PhysicalConstants.Day
                : Math.Min(Math.Pow(1.33 * Math.Pow(r / 1e12, 0.20) / 1.07, 1 / 0.38) * 1e5,
                    1.2 * Math.Sqrt(kappa / 0.34) * m / Math.Sqrt(e51) * PhysicalConstants.Day);
            double start = Math.Max(coolStart, flashS);
            return coolEnd > start
                ? b with { CoolStartS = start, EndS = coolEnd }
                : b with { CoolStartS = flashS, EndS = flashS }; // no valid cooling phase: the flash only
        }

        public State At(double tau)
        {
            if (tau <= FlashS) return Flash;
            if (tau >= CoolStartS) return CoolingAt(tau);
            // The bridge: log-linear in log τ from the flash's end to the start of cooling.
            var end = CoolingAt(CoolStartS);
            double f = Math.Log(tau / FlashS) / Math.Log(CoolStartS / FlashS);
            return new State(LogLerp(Flash.Luminosity, end.Luminosity, f), LogLerp(Flash.ThermalEv, end.ThermalEv, f),
                LogLerp(Flash.PeakEv, end.PeakEv, f));
        }

        /// <summary>
        /// ∫ F dτ (erg cm⁻² per source second of τ) of the detector's absorbed or bare band flux over [from, to] ∩
        /// [0, EndS], τ in source seconds after breakout: exact over the flat flash, Simpson in log τ over the bridge and
        /// the cooling phase.
        /// </summary>
        public double BandFluence(ObserverSetup obs, string detectorId, bool absorbed, double from, double to)
        {
            from = Math.Max(from, 0);
            to = Math.Min(to, EndS);
            if (!(to > from)) return 0;
            double sum = 0, flashEnd = Math.Min(to, FlashS);
            if (flashEnd > from) sum += XrayFlux(Flash, obs, detectorId, absorbed, FluenceBandIntervals) * (flashEnd - from);
            foreach (var (lo, hi) in new[] { (FlashS, CoolStartS), (CoolStartS, EndS) })
            {
                double s0 = Math.Max(from, lo), s1 = Math.Min(to, hi);
                if (!(s1 > s0)) continue;
                double u0 = Math.Log(s0), h = (Math.Log(s1) - u0) / FluenceIntervals, acc = 0;
                for (int i = 0; i <= FluenceIntervals; i++)
                {
                    double t = Math.Exp(u0 + i * h);
                    double w = i == 0 || i == FluenceIntervals ? 1 : i % 2 == 1 ? 4 : 2;
                    acc += w * XrayFlux(At(t), obs, detectorId, absorbed, FluenceBandIntervals) * t;
                }
                sum += acc * h / 3;
            }
            return sum;
        }

        /// <summary>
        /// Sapir &amp; Waxman 2017's t_tr = 19.5 d (κ_0.34 M_env / v_s*,8.5)^(1/2) (§3.3), s, with κ_0.34 = 1 for a hydrogen
        /// envelope and 0.2 / 0.34 for a hydrogen-free one, and v_s* = 1.05 √(E/M) (f_ρ = 1). 0 without an envelope.
        /// </summary>
        internal double TransparencyTimeS
        {
            get
            {
                if (!(EnvelopeMass > 0)) return 0;
                double kappa34 = (HydrogenRich ? KappaHydrogen : KappaHeliumOrHeavier) / 0.34;
                double v = 1.05 * Math.Sqrt(EnergyE51 * 1e51 / (EjectaMass * MSun)) / Math.Pow(10, 8.5);
                return 19.5 * PhysicalConstants.Day * Math.Sqrt(kappa34 * EnvelopeMass / v);
            }
        }

        /// <summary>(a τ / t_tr)^α of Sapir &amp; Waxman 2017's late suppression, for this breakout's polytrope (§3.3).</summary>
        internal double SuppressionExponent(double tau, double ttr)
        {
            (double a, double alpha) = PolytropeIndex < 2 ? (1.67, 0.8) : (4.57, 0.73);
            return Math.Pow(a * tau / ttr, alpha);
        }

        /// <summary>Sapir &amp; Waxman 2017's ε2 (§3.3): 0.086 for n = 3/2, 0.175 for n = 3.</summary>
        private double Eps2 => PolytropeIndex < 2 ? 0.086 : 0.175;

        /// <summary>
        /// The cooling luminosity's power law in time, L ∝ τ^(−p): p = 2ε2 for Sapir &amp; Waxman's L_RW (hydrogen), 0.03
        /// for Rabinak &amp; Waxman's helium formula (§3.3).
        /// </summary>
        internal double CoolingPowerLaw => HydrogenRich ? 2 * Eps2 : 0.03;

        /// <summary>
        /// The cooling emission τ seconds after breakout: a blackbody at T_col = 1.2 T_ph (§3.3). The cooling formula
        /// itself, whatever phase τ falls in (<see cref="CoolingPhase"/> calls it directly, so at its start, where the
        /// cooling phase begins as the flash ends, it never returns the flash: Task 8 review N1).
        /// </summary>
        internal State CoolingAt(double tau)
        {
            double l, tPhEv;
            if (HydrogenRich)
            {
                // Sapir & Waxman 2017, constant κ_0.34 = 1 (hydrogen), f_ρ = 1.
                double v = 1.05 * Math.Sqrt(EnergyE51 * 1e51 / (EjectaMass * MSun)) / Math.Pow(10, 8.5);
                double td = tau / PhysicalConstants.Day, r13 = RadiusCm / 1e13;
                bool n32 = PolytropeIndex < 2;
                double eps1 = n32 ? 0.027 : 0.016;
                double lrw = (n32 ? 2.0e42 : 2.1e42) * Math.Pow(v * td * td / (FRho * EjectaMass), -Eps2) * v * v * r13;
                // Late suppression L/L_RW = A exp(−(a t / t_tr)^α): A = 0.94 [0.79] for n = 3/2 [3]; a, α and t_tr are
                // shared with the fade past the formula's end.
                l = lrw * (n32 ? 0.94 : 0.79) * Math.Exp(-SuppressionExponent(tau, TransparencyTimeS));
                tPhEv = (n32 ? 1.61 : 1.69) * Math.Pow(v * v * td * td / (FRho * EjectaMass), eps1) * Math.Pow(r13, 0.25)
                        / Math.Sqrt(td);
            }
            else
            {
                // Rabinak & Waxman 2011, helium envelope. [APPROX] Ic, Ic-BL and SLSN-I (C/O-rich outer layers) use it too:
                // the research gives no C/O-envelope formula, and the helium one is the closest hydrogen-free case.
                double t5 = tau / 1e5, r12 = RadiusCm / 1e12;
                l = 3.3e42 * Math.Pow(EnergyE51, 0.84) * Math.Pow(r12, 0.85) / (Math.Pow(FRho, 0.15) * Math.Pow(EjectaMass, 0.67))
                    * Math.Pow(t5, -0.03);
                tPhEv = 1.33 * Math.Pow(r12, 0.20) * Math.Pow(t5, -0.38);
            }
            // Colour temperature: "about 1.2 T_ph for t up to about 1 day" (§3.3); [APPROX] the same factor is kept later.
            double kTcol = 1.2 * tPhEv;
            return new State(l, kTcol, PlanckPeakX * kTcol);
        }
    }

    private static bool IsHydrogenRich(Outcome o) => o.Type switch
    {
        SupernovaType.Ib or SupernovaType.Ic or SupernovaType.IcBL or SupernovaType.Ibn or SupernovaType.Icn
            or SupernovaType.SlsnI => false,
        SupernovaType.Pisn or SupernovaType.None => o.Star!.HydrogenEnvelopeMass.Value > 0,
        _ => true,
    };

    /// <summary>
    /// Waxman &amp; Katz 2017 (signals §3.2): the flash's luminosity E_BO / max(R/c, t_bo), its equilibrium temperature
    /// (L = 4πR²σT⁴) and the W&amp;K peak photon energy hν_peak, with v_bo capped at <see cref="MaxBreakoutBeta"/> c.
    /// </summary>
    private static State FlashOf(double n, double energyErg, double ejectaMass, double radiusCm, double kappa, out double flashS,
        out double betaBo)
    {
        double k = kappa / 0.34;
        double vStar = Math.Sqrt(energyErg / (ejectaMass * MSun));
        double v85 = vStar / Math.Pow(10, 8.5), m10 = ejectaMass / 10;
        double vbo, rhoBo;
        if (n < 2)
        {
            double r13 = radiusCm / 1e13;
            vbo = vStar * 4.5 * Math.Pow(m10, 0.13) * Math.Pow(v85, 0.13) * Math.Pow(r13, -0.26) * Math.Pow(k, 0.13);
            rhoBo = 2.2e-9 * Math.Pow(m10, 0.32) * Math.Pow(v85, -0.68) * Math.Pow(r13, -1.64);
        }
        else
        {
            double r12 = radiusCm / 1e12;
            vbo = vStar * 13 * Math.Pow(m10, 0.16) * Math.Pow(v85, 0.16) * Math.Pow(r12, -0.32) * Math.Pow(k, 0.16);
            rhoBo = 8e-9 * Math.Pow(m10, 0.13) * Math.Pow(v85, -0.87) * Math.Pow(r12, -1.26);
        }
        // Ruling R28: the uncapped β is kept for the warning; the formulas get at most 0.3c.
        betaBo = vbo / C;
        double v9 = Math.Min(vbo, MaxBreakoutBeta * C) / 1e9, rho9 = rhoBo / 1e-9;
        double energyBo = 2.2e47 * Math.Pow(radiusCm / 1e13, 2) * v9 / k;
        double tBo = 90 / k / rho9 / (v9 * v9);
        flashS = Math.Max(radiusCm / C, tBo);
        double l = energyBo / flashS;
        double tEq = Math.Pow(l / (4 * Math.PI * radiusCm * radiusCm * StefanBoltzmann), 0.25);
        double peakEv = Math.Pow(10, 1.4 + Math.Sqrt(v9) + (0.25 - 0.05 * Math.Sqrt(v9)) * Math.Log10(rho9));
        double thermalEv = BoltzmannEv * tEq;
        return new State(l, thermalEv, Math.Max(peakEv, PlanckPeakX * thermalEv));
    }

    private static double LogLerp(double a, double b, double f) => Math.Exp(Math.Log(a) + f * (Math.Log(b) - Math.Log(a)));

    // ---------------------------------------------------------------- spectrum and bands

    /// <summary>
    /// [APPROX] The spectral shape νL_ν(ε), relative to its peak: a blackbody at the thermal floor kT below
    /// x_th = x* kT, flat from x_th to the peak (Comptonized; §3.2's "non-thermal" breakouts), and a blackbody tail at
    /// x_pk / x* above it. A pure blackbody when the peak equals x_th. ∫ = <see cref="PlanckWidth"/> + ln(x_pk / x_th).
    /// </summary>
    private static double Shape(double ev, double thermalEv, double peakEv)
    {
        double xTh = PlanckPeakX * thermalEv, g0 = PlanckG(PlanckPeakX);
        if (ev <= xTh) return PlanckG(ev / thermalEv) / g0;
        if (ev <= peakEv) return 1;
        return PlanckG(ev / (peakEv / PlanckPeakX)) / g0;
    }

    /// <summary>
    /// The fraction of L emitted between two photon energies (eV), each photon weighted by the gas transmission e^(−τ(ε))
    /// for this visual extinction (<see cref="Extinction.XrayTransmission"/>; 0: none).
    /// </summary>
    private static double BandFraction(State s, double lowEv, double highEv, double av, int intervals = BandIntervals)
    {
        double norm = PlanckWidth + Math.Log(s.PeakEv / (PlanckPeakX * s.ThermalEv));
        double a = Math.Log(lowEv), h = (Math.Log(highEv) - a) / intervals, sum = 0;
        for (int i = 0; i <= intervals; i++)
        {
            double ev = Math.Exp(a + i * h);
            double w = i == 0 || i == intervals ? 1 : i % 2 == 1 ? 4 : 2;
            double absorption = Extinction.XrayTransmission(ev / 1000, av);
            sum += w * Shape(ev, s.ThermalEv, s.PeakEv) * absorption;
        }
        return sum * h / 3 / norm;
    }

    private static double XrayFlux(State s, ObserverSetup obs, string detectorId, bool absorbed,
        int intervals = BandIntervals)
    {
        var (lo, hi) = detectorId == "einstein-probe" ? (EpLowEv, EpHighEv) : (XrtLowEv, XrtHighEv);
        double d = obs.LuminosityDistanceCm;
        return s.Luminosity * BandFraction(s, lo, hi, absorbed ? obs.Av : 0, intervals)
               / (4 * Math.PI * d * d);
    }

    /// <summary>The band-averaged AB magnitude in ULTRASAT's band, m = −2.5 log10 f̄_ν − 48.60 (+ A_UV); NaN if no flux.</summary>
    private static double UltrasatMagnitude(State s, ObserverSetup obs, bool withDust)
    {
        double d = obs.LuminosityDistanceCm;
        double fNu = s.Luminosity * BandFraction(s, UltrasatLowEv, UltrasatHighEv, 0) / (4 * Math.PI * d * d * UltrasatBandHz);
        if (!(fNu > 0)) return double.NaN;
        return -2.5 * Math.Log10(fNu) - AbZeroPoint + (withDust ? Extinction.A(Band.Uv, obs.Av) : 0);
    }

    // ---------------------------------------------------------------- detection

    /// <summary>
    /// Ruling R29: one continuous, photon-count-limited criterion for the flash and what follows. Detected when the
    /// fluence collected over the last registry exposure of observer time since breakout, ∫F dt over
    /// [max(t_bo, t − t_reg), t], reaches F_reg · t_reg. The band flux only falls after breakout, so the largest fluence
    /// is that of the first exposure: below the threshold the verdict is TooFaint (or BlockedByDust) throughout; above
    /// it, NotYet while the first exposure is still filling, then Detected, then TooFaint once the emission has faded.
    /// </summary>
    private static Detection DetectXray(Breakout b, ObserverSetup obs, double tau, Detector detector)
    {
        var inv = CultureInfo.InvariantCulture;
        double exposureObs = ExposureS(detector), threshold = FluenceThreshold(detector);
        double window = exposureObs / obs.TimeDilation; // the exposure in source time
        double z1 = obs.TimeDilation;                   // ∫F dt_obs = (1 + z) ∫F dt_source
        double Fluence(double from, double to, bool absorbed) => z1 * b.BandFluence(obs, detector.Id, absorbed, from, to);

        double firstEnd = Math.Min(window, b.EndS);
        bool gas = obs.Av > 0;
        double best = Fluence(0, firstEnd, true), bestBare = gas ? Fluence(0, firstEnd, false) : best;
        double now = Fluence(Math.Max(0, tau - window), tau, true);
        double nowBare = gas ? Fluence(Math.Max(0, tau - window), tau, false) : now;

        DetectionStatus status;
        string state;
        if (now >= threshold)
        {
            status = DetectionStatus.Detected;
            state = "enough to detect it";
        }
        else if (best >= threshold && tau < firstEnd)
        {
            status = DetectionStatus.NotYet;
            state = $"still building up: the whole first exposure would collect {FormatFlux(best)}";
        }
        else if (nowBare >= threshold || (tau < firstEnd && bestBare >= threshold))
        {
            status = DetectionStatus.BlockedByDust;
            state = "too little, but without the gas in the way it would be enough";
        }
        else
        {
            status = DetectionStatus.TooFaint;
            state = best >= threshold ? "too little now: the emission has faded" : "too little";
        }

        string pointed = detector.Id == "swift-xrt"
            ? "This assumes Swift is already pointed at the star (it caught SN 2008D by luck)."
            : "This assumes the star is inside the field of view.";
        string band = detector.Id == "einstein-probe" ? "0.5–4 keV" : "0.3–10 keV";
        string reason =
            $"{detector.Name} would collect {FormatFlux(now)} erg cm⁻² ({band}) over the last " +
            $"{UnitFormat.Time(Math.Min(tau * z1, exposureObs))} since breakout: {state}. A detection needs " +
            $"{FormatFlux(threshold)} erg cm⁻² ({detector.Threshold.ToString("0.#e0", inv)} erg cm⁻² s⁻¹ × " +
            $"{UnitFormat.Time(exposureObs)}; photon-count limited, approximate). The flash itself lasts about " +
            $"{UnitFormat.Time(b.FlashS * z1)}. {pointed}{Caveats(b)}";
        return new Detection(status, reason);
    }

    /// <summary>
    /// [APPROX] ULTRASAT reaches 22.5 AB mag at 5σ in 900 s (§3.3). The instantaneous magnitude is compared with it: the
    /// cooling emission changes over hours, much longer than an exposure. No exposure scaling for shorter flashes.
    /// </summary>
    private static Detection DetectUv(Breakout b, ObserverSetup obs, double tau, Detector detector)
    {
        var s = b.At(tau);
        double m = UltrasatMagnitude(s, obs, withDust: true), bare = UltrasatMagnitude(s, obs, withDust: false);
        if (double.IsNaN(bare))
            return new Detection(DetectionStatus.TooFaint, $"{detector.Name} receives no near-ultraviolet light now.{Caveats(b)}");
        var status = Verdict(m, bare, detector.Threshold, brighterIsSmaller: true);
        var inv = CultureInfo.InvariantCulture;
        return new Detection(status,
            $"{detector.Name} would see the star at AB magnitude {m.ToString("0.#", inv)} in 230–290 nm; it reaches " +
            $"{detector.Threshold.ToString("0.#", inv)} in 900 s{DustNote(status)}. This assumes the star is inside the " +
            $"field of view (blackbodies overstate the ultraviolet a little; metal lines absorb some of it).{Caveats(b)}");
    }

    /// <summary>The plain-language limits that apply to this breakout (rulings R28, R30).</summary>
    private static string Caveats(Breakout b)
    {
        string text = "";
        if (b.BetaBo > MaxBreakoutBeta)
            text += $" Relativistic breakout (v ≈ {b.BetaBo.ToString("0.##", CultureInfo.InvariantCulture)}c) — outside the " +
                    "Waxman & Katz formulas' range: the velocity is capped at 0.3c, so the flash's energy and spectrum " +
                    "here are a rough sketch.";
        if (!b.HydrogenRich)
            text += " Observed flashes from stars like this, such as SN 2008D, lasted minutes and carried ~2e46 erg — " +
                    "likely breakout in a dense stellar wind, which this model does not include; this bare-star flash " +
                    "is probably too short and too faint.";
        return text;
    }

    /// <summary>Detected; BlockedByDust when only the dust makes it too faint; otherwise TooFaint.</summary>
    private static DetectionStatus Verdict(double value, double withoutDust, double threshold, bool brighterIsSmaller)
    {
        bool Passes(double v) => brighterIsSmaller ? v <= threshold : v >= threshold;
        if (Passes(value)) return DetectionStatus.Detected;
        return Passes(withoutDust) ? DetectionStatus.BlockedByDust : DetectionStatus.TooFaint;
    }

    private static string DustNote(DetectionStatus s)
        => s == DetectionStatus.BlockedByDust ? " (without the dust in the way it would be bright enough)" : "";

    private static string FormatFlux(double f) => f.ToString("0.##e0", CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- helpers

    private static double PlanckG(double x) => x * x * x * x / ExpM1(x);

    /// <summary>e^x − 1 without cancellation for small x.</summary>
    private static double ExpM1(double x) => Math.Abs(x) < 1e-3 ? x * (1 + x / 2 * (1 + x / 3)) : Math.Exp(x) - 1;

    private static double SolvePlanckPeak()
    {
        double x = 3.9;
        for (int i = 0; i < 100; i++) x = 4 * (1 - Math.Exp(-x));
        return x;
    }

    private static (string Channel, string? DetectorId) ParseChannel(string channelId)
    {
        var id = ChannelId.Parse(channelId);
        switch (id.Base)
        {
            case "lum" or "temp" when id.DetectorId is null:
                return (id.Base, null);
            case "flux":
                string detector = id.DetectorId ?? DefaultDetectorId;
                if (detector is not ("swift-xrt" or "einstein-probe"))
                    throw new ArgumentException($"'{detector}' is not a breakout X-ray detector.", nameof(channelId));
                return (id.Base, detector);
            case "mag" when id.DetectorId is "ultrasat":
                return (id.Base, id.DetectorId);
            default:
                throw new ArgumentException($"The shock-breakout model has no channel '{channelId}'.", nameof(channelId));
        }
    }

    private static string NotProducedReason(Outcome o) => o.Fate switch
    {
        Fate.NotDeadYet => "The star has not died yet, so there is no shock breakout.",
        Fate.WhiteDwarf => "The star ends as a white dwarf with no explosion, so there is no shock breakout.",
        Fate.FailedSupernova => "The core collapses straight to a black hole with no explosion, so no shock reaches the surface.",
        Fate.PulsationalPairInstability => "The core collapses to a black hole without a supernova shock, so there is no " +
                                           "breakout flash from the star's surface.",
        _ => "A thermonuclear explosion has no stellar envelope to break out of: a white dwarf is so small that its " +
             "breakout is a faint millisecond flash, which this view does not include.",
    };
}
