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
/// <item>Flash: Waxman &amp; Katz 2017 breakout velocity, density, energy E_BO and diffusion time t_bo; observed duration
/// max(R/c, t_bo); luminosity E_BO / duration (§3.2, "peak luminosity for the app").</item>
/// <item>Cooling: Sapir &amp; Waxman 2017 for hydrogen envelopes (with its late suppression), Rabinak &amp; Waxman 2011 for
/// helium envelopes (§3.3), each inside its stated validity.</item>
/// </list>
/// <para>Channels: <c>lum</c> (bolometric erg/s at the source), <c>temp</c> (colour temperature, K: the blackbody
/// temperature whose νL_ν peaks where the spectrum does), and per detector <c>flux@id</c>: Swift XRT (0.3–10 keV) and
/// Einstein Probe WXT (0.5–4 keV) in erg cm⁻² s⁻¹ after absorption by the interstellar gas, and ULTRASAT (230–290 nm) as
/// an AB magnitude including dust extinction. A bare <c>flux</c> is Swift XRT.</para>
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

    /// <summary>Stefan-Boltzmann constant, erg cm⁻² s⁻¹ K⁻⁴ (CODATA 2018; not in signals §0.2).</summary>
    private const double StefanBoltzmann = 5.670374419e-5;

    /// <summary>Boltzmann constant, eV/K (CODATA 2018, exact).</summary>
    private const double BoltzmannEv = 8.617333262e-5;

    /// <summary>h c, eV nm (CODATA 2018, exact).</summary>
    private const double HcEvNm = 1239.84198;

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

    private const int BandIntervals = 200;

    // ---------------------------------------------------------------- bands and detectors

    /// <summary>Swift XRT 0.3–10 keV (signals §3.2); Einstein Probe WXT 0.5–4 keV (§3.3); ULTRASAT 230–290 nm (§3.3).</summary>
    private const double XrtLowEv = 300, XrtHighEv = 10_000, EpLowEv = 500, EpHighEv = 4_000;

    private const double UltrasatShortNm = 230, UltrasatLongNm = 290;
    private static readonly double UltrasatLowEv = HcEvNm / UltrasatLongNm, UltrasatHighEv = HcEvNm / UltrasatShortNm;

    /// <summary>ULTRASAT's band width in Hz: c (1/230 nm − 1/290 nm).</summary>
    private static readonly double UltrasatBandHz = C * 1e7 * (1 / UltrasatShortNm - 1 / UltrasatLongNm);

    /// <summary>
    /// [APPROX] Photoelectric absorption by the interstellar gas, σ(E) = 2.4e-22 cm² (E / 1 keV)^(−8/3) per H atom: a
    /// power-law approximation of Morrison &amp; McCammon 1983 (solar abundances), not in the research. The column is
    /// N_H = 2.2e21 A_V (<see cref="Extinction.NhFromAv"/>).
    /// </summary>
    private const double PhotoCrossSection1KeV = 2.4e-22, PhotoCrossSectionSlope = -8.0 / 3;

    /// <summary>
    /// [DERIVED, APPROX] Swift XRT's threshold for a breakout flash: SN 2008D (peak L_X = 6.1e43 erg/s, about 5 minutes)
    /// was detectable to about 200 Mpc (signals §3.2, §11; Soderberg et al. 2008): 6.1e43 / (4π (200 Mpc)²) =
    /// 1.2745e-11 erg cm⁻² s⁻¹ for a 300-s flash.
    /// </summary>
    private static readonly double XrtFlashThresholdAt300S = 6.1e43 / (4 * Math.PI * Math.Pow(200 * PhysicalConstants.Mpc, 2));

    private const double XrtFlashReferenceS = 300, EpReferenceS = 1000;

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
        new("temp", "Colour temperature", "K", true),
        new(ChannelId.Compose("flux", "swift-xrt"), "Swift XRT flux (0.3–10 keV, after gas absorption)", "erg cm⁻² s⁻¹", true),
        new(ChannelId.Compose("flux", "einstein-probe"), "Einstein Probe WXT flux (0.5–4 keV, after gas absorption)",
            "erg cm⁻² s⁻¹", true),
        new(ChannelId.Compose("flux", "ultrasat"), "ULTRASAT near-UV brightness (230–290 nm, after dust)", "AB mag", false,
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
        new("Thresholds: Swift XRT calibrated on SN 2008D seen to ~200 Mpc (Soderberg et al. 2008, Nature 453, 469); " +
            "Einstein Probe WXT 2.5e-11 erg cm⁻² s⁻¹ in 1,000 s; both ∝ 1/√t for shorter flashes [APPROX]; ULTRASAT " +
            "22.5 AB mag (Shvartzvald et al. 2024, ApJ 964, 74)", "signals §3.3, §6.2, §11"),
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
            "flux" => detectorId == "ultrasat"
                ? UltrasatMagnitude(s, observer, withDust: true)
                : XrayFlux(s, observer, detectorId!, absorbed: true),
            _ => throw new InvalidOperationException(),
        };
    }

    public Detection Detect(Outcome outcome, ObserverSetup observer, double sourceTimeS, Detector detector)
    {
        if (!Applies(outcome)) return new Detection(DetectionStatus.NotProduced, NotProducedReason(outcome));
        if (detector.Messenger != Messenger.XRaysUv)
            return new Detection(DetectionStatus.NotModeled, $"{detector.Name} does not observe X-rays or ultraviolet light.");
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
    public static double KistlerDelayS(double n, double energyErg, double ejectaMass, double radiusRsun, double remnantMass)
    {
        double x = remnantMass / ejectaMass;
        double scale = Math.Sqrt(1e51 / energyErg) * Math.Sqrt(ejectaMass / 10) * (radiusRsun / 50);
        return n < 2
            ? 7226 * scale * (1 - 0.738 * Math.Pow(x, 0.80) + 0.467 * Math.Pow(x, 1.20))
            : 6851 * scale * (1 - 0.407 * Math.Pow(x, 0.81) + 0.285 * Math.Pow(x, 1.12));
    }

    /// <summary>
    /// [APPROX] The flux (erg cm⁻² s⁻¹, in the detector's band) a breakout flash of this observed duration needs to be
    /// detected. Short exposures are taken as background-limited, ∝ 1/√t (the research gives no scaling). Swift XRT:
    /// 1.2745e-11 at 300 s, from SN 2008D seen to about 200 Mpc (§11). Einstein Probe WXT: 2.5e-11 at 1,000 s (§3.3),
    /// no deeper for a longer flash (its survey exposure). Assumes the instrument is already pointed at the star.
    /// </summary>
    public static double FlashThreshold(Detector detector, double observedDurationS)
    {
        if (!(observedDurationS > 0)) throw new ArgumentOutOfRangeException(nameof(observedDurationS));
        return detector.Id switch
        {
            "swift-xrt" => XrtFlashThresholdAt300S * Math.Sqrt(XrtFlashReferenceS / observedDurationS),
            "einstein-probe" => detector.Threshold * Math.Sqrt(EpReferenceS / Math.Min(observedDurationS, EpReferenceS)),
            _ => throw new ArgumentException($"'{detector.Id}' has no breakout-flash threshold.", nameof(detector)),
        };
    }

    // ---------------------------------------------------------------- the breakout and its evolution

    /// <summary>The emission at one moment: bolometric L (erg/s), the thermal floor kT (eV) and the νL_ν peak (eV).</summary>
    private readonly record struct State(double Luminosity, double ThermalEv, double PeakEv);

    /// <summary>
    /// One outcome's breakout. Times are seconds after breakout (τ) except <see cref="DelayS"/>, which is after bounce.
    /// The flash lasts <see cref="FlashS"/>; a power-law bridge in log L, log T (the planar phase, for which the research
    /// gives no formula [APPROX]) joins it to the cooling formula, valid from <see cref="CoolStartS"/> to
    /// <see cref="EndS"/>.
    /// </summary>
    private sealed record Breakout(double DelayS, double FlashS, State Flash, double CoolStartS, double EndS,
        bool HydrogenRich, double PolytropeIndex, double EnergyE51, double EjectaMass, double RadiusCm, double EnvelopeMass)
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

            var flash = FlashOf(n, e, m, r, kappa, out double flashS);
            double e51 = e / 1e51;
            double envelope = hRich ? star.HydrogenEnvelopeMass.Value : star.HeliumEnvelopeMass.Value;
            var b = new Breakout(delay, flashS, flash, 0, 0, hRich, n, e51, m, r, envelope);

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
            if (tau >= CoolStartS) return Cooling(tau);
            // The bridge: log-linear in log τ from the flash's end to the start of cooling.
            var end = Cooling(CoolStartS);
            double f = Math.Log(tau / FlashS) / Math.Log(CoolStartS / FlashS);
            return new State(LogLerp(Flash.Luminosity, end.Luminosity, f), LogLerp(Flash.ThermalEv, end.ThermalEv, f),
                LogLerp(Flash.PeakEv, end.PeakEv, f));
        }

        /// <summary>The cooling emission τ seconds after breakout: a blackbody at T_col = 1.2 T_ph (§3.3).</summary>
        private State Cooling(double tau)
        {
            double l, tPhEv;
            if (HydrogenRich)
            {
                // Sapir & Waxman 2017, constant κ_0.34 = 1 (hydrogen), f_ρ = 1.
                double v = 1.05 * Math.Sqrt(EnergyE51 * 1e51 / (EjectaMass * MSun)) / Math.Pow(10, 8.5);
                double td = tau / PhysicalConstants.Day, r13 = RadiusCm / 1e13;
                bool n32 = PolytropeIndex < 2;
                double eps2 = n32 ? 0.086 : 0.175, eps1 = n32 ? 0.027 : 0.016;
                double lrw = (n32 ? 2.0e42 : 2.1e42) * Math.Pow(v * td * td / (FRho * EjectaMass), -eps2) * v * v * r13;
                double ttr = 19.5 * Math.Sqrt(EnvelopeMass / v);
                (double a, double aa, double alpha) = n32 ? (0.94, 1.67, 0.8) : (0.79, 4.57, 0.73);
                l = lrw * a * Math.Exp(-Math.Pow(aa * td / ttr, alpha));
                tPhEv = (n32 ? 1.61 : 1.69) * Math.Pow(v * v * td * td / (FRho * EjectaMass), eps1) * Math.Pow(r13, 0.25)
                        / Math.Sqrt(td);
            }
            else
            {
                // Rabinak & Waxman 2011, helium envelope.
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
    /// (L = 4πR²σT⁴) and the W&amp;K peak photon energy hν_peak.
    /// </summary>
    private static State FlashOf(double n, double energyErg, double ejectaMass, double radiusCm, double kappa, out double flashS)
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
        double v9 = vbo / 1e9, rho9 = rhoBo / 1e-9;
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

    /// <summary>The fraction of L emitted between two photon energies (eV), each photon weighted by e^(−τ(ε)).</summary>
    private static double BandFraction(State s, double lowEv, double highEv, double nH)
    {
        double norm = PlanckWidth + Math.Log(s.PeakEv / (PlanckPeakX * s.ThermalEv));
        double a = Math.Log(lowEv), h = (Math.Log(highEv) - a) / BandIntervals, sum = 0;
        for (int i = 0; i <= BandIntervals; i++)
        {
            double ev = Math.Exp(a + i * h);
            double w = i == 0 || i == BandIntervals ? 1 : i % 2 == 1 ? 4 : 2;
            double absorption = nH > 0 ? Math.Exp(-nH * PhotoCrossSection1KeV * Math.Pow(ev / 1000, PhotoCrossSectionSlope)) : 1;
            sum += w * Shape(ev, s.ThermalEv, s.PeakEv) * absorption;
        }
        return sum * h / 3 / norm;
    }

    private static double XrayFlux(State s, ObserverSetup obs, string detectorId, bool absorbed)
    {
        var (lo, hi) = detectorId == "einstein-probe" ? (EpLowEv, EpHighEv) : (XrtLowEv, XrtHighEv);
        double d = obs.LuminosityDistanceCm;
        return s.Luminosity * BandFraction(s, lo, hi, absorbed ? Extinction.NhFromAv(obs.Av) : 0) / (4 * Math.PI * d * d);
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

    private static Detection DetectXray(Breakout b, ObserverSetup obs, double tau, Detector detector)
    {
        string pointed = detector.Id == "swift-xrt"
            ? "This assumes Swift is already pointed at the star (it caught SN 2008D by luck)."
            : "This assumes the star is inside the field of view.";
        double flashObservedS = b.FlashS * obs.TimeDilation;
        double flashThreshold = FlashThreshold(detector, flashObservedS);
        double flashFlux = XrayFlux(b.Flash, obs, detector.Id, absorbed: true);
        double flashBare = XrayFlux(b.Flash, obs, detector.Id, absorbed: false);
        var flashStatus = Verdict(flashFlux, flashBare, flashThreshold, brighterIsSmaller: false);
        string flashText = $"The breakout flash gives {FormatFlux(flashFlux)} erg cm⁻² s⁻¹ for about " +
                           $"{UnitFormat.Time(flashObservedS)}; a flash that short needs {FormatFlux(flashThreshold)} " +
                           (detector.Id == "swift-xrt"
                               ? "(set so that SN 2008D's 5-minute flash would be seen to about 200 Mpc, and scaled " +
                                 "as 1/√t; approximate)"
                               : "(its 2.5e-11 in 1,000 s, scaled as 1/√t for a shorter flash; approximate)");
        if (tau <= b.FlashS)
            return new Detection(flashStatus, $"{detector.Name}: {flashText}{DustNote(flashStatus)}. {pointed}");

        // After the flash: can a normal exposure still see the cooling emission now? (Its X-rays are usually negligible.)
        var now = b.At(tau);
        double flux = XrayFlux(now, obs, detector.Id, absorbed: true), bare = XrayFlux(now, obs, detector.Id, absorbed: false);
        var status = Verdict(flux, bare, detector.Threshold, brighterIsSmaller: false);
        return new Detection(status,
            $"The breakout flash is over (it was {Past(flashStatus)}). Now {detector.Name} would receive {FormatFlux(flux)} " +
            $"erg cm⁻² s⁻¹ and needs {detector.Threshold.ToString("0.#e0", CultureInfo.InvariantCulture)} " +
            $"{detector.ThresholdUnit}{DustNote(status)}. {pointed}");
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
            return new Detection(DetectionStatus.TooFaint, $"{detector.Name} receives no near-ultraviolet light now.");
        var status = Verdict(m, bare, detector.Threshold, brighterIsSmaller: true);
        var inv = CultureInfo.InvariantCulture;
        return new Detection(status,
            $"{detector.Name} would see the star at AB magnitude {m.ToString("0.#", inv)} in 230–290 nm; it reaches " +
            $"{detector.Threshold.ToString("0.#", inv)} in 900 s{DustNote(status)}. This assumes the star is inside the " +
            "field of view (blackbodies overstate the ultraviolet a little; metal lines absorb some of it).");
    }

    /// <summary>Detected; BlockedByDust when only the dust or gas makes it too faint; otherwise TooFaint.</summary>
    private static DetectionStatus Verdict(double value, double withoutDust, double threshold, bool brighterIsSmaller)
    {
        bool Passes(double v) => brighterIsSmaller ? v <= threshold : v >= threshold;
        if (Passes(value)) return DetectionStatus.Detected;
        return Passes(withoutDust) ? DetectionStatus.BlockedByDust : DetectionStatus.TooFaint;
    }

    private static string DustNote(DetectionStatus s)
        => s == DetectionStatus.BlockedByDust ? " (without the dust and gas in the way it would be bright enough)" : "";

    private static string Past(DetectionStatus s) => s switch
    {
        DetectionStatus.Detected => "detected",
        DetectionStatus.BlockedByDust => "hidden by the dust and gas",
        _ => "too faint to detect",
    };

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
                if (!DetectorIds.Contains(detector))
                    throw new ArgumentException($"'{detector}' is not a breakout-flash detector.", nameof(channelId));
                return (id.Base, detector);
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
