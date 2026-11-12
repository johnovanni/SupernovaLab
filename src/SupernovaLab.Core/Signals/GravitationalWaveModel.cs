using System.Globalization;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>
/// Gravitational waves from core collapse (signals §2). The strain is the energy-normalized stochastic chirp of §2.4: a
/// prompt-convection packet near 100 Hz, a weak SASI band, and the proto-neutron star's rising g/f-mode track, each drawn
/// as band-limited Gaussian noise from a fixed internal seed, so it is deterministic. A Fast or Extreme rotator adds the
/// rotating-bounce ring-down of §2.2 (Speculative). A black hole ends the signal at t_BH with a short ring-down (§2.4
/// step 3).
/// <para>Channels: <c>strain</c> (dimensionless h at Earth, signed) and, per detector, <c>snr@id</c>: the optimal
/// signal-to-noise ratio accumulated since bounce, SNR² = 4∫|h̃(f)|²/S_n(f) df evaluated along the track (each piece is
/// narrow-band, so it is ∫ 2⟨h²⟩/S_n(f(t)) dt, §2.4 step 2). A bare <c>snr</c> is aligo, which stands for
/// the Advanced LIGO/Virgo design network of three detectors, whose quoted range (about 5.5 kpc, §2.3) is a network
/// figure.</para>
/// Only core collapse makes this signal; white dwarfs, thermonuclear (Type Ia, Iax) and pair-instability explosions have
/// no bouncing core, so the model does not apply.
/// <para>[APPROX] Like the neutrino model, the strain uses the luminosity distance (h ∝ 1/D, signals §0.3) and ignores
/// the redshift of frequencies and durations. It matters only beyond 50 Mpc, where no detector comes close.</para>
/// </summary>
public sealed class GravitationalWaveModel : ISignalModel
{
    // ---------------------------------------------------------------- energy normalization (signals §2.4 step 1)

    /// <summary>
    /// E_GW of an exploding star's stochastic signal, erg: the recipe's default, 1e45 (range 1e43–1e46 for non-rotating
    /// cases; 3D models give 7.2e43 at 9 M☉ to about 2e45 at 13 M☉, §2.1).
    /// </summary>
    public const double DefaultEnergyErg = 1e45;

    // ---------------------------------------------------------------- the time-frequency track (signals §2.1, §2.4 step 3)

    // Prompt convection: a noise-like packet near 100 Hz at 10-50 ms after bounce (§2.1, §2.4 step 3).
    private const double PromptStartS = 0.010, PromptEndS = 0.050, PromptHz = 100;
    // [APPROX] Its rms amplitude relative to the main track; the research gives none. Half the main track's makes it
    // visible but weaker, as in the 3D waveforms §2.1 summarizes; it carries ~5e-5 of E_GW (low frequency, short).
    private const double PromptRelativeAmplitude = 0.5;

    // SASI: "a weaker band at 100-200 Hz (for example 0.15-0.4 s)" (§2.4 step 3), drawn at the band's middle, 150 Hz.
    private const double SasiStartS = 0.15, SasiEndS = 0.40, SasiHz = 150;
    // [APPROX] "Weak compared with the main track" (§2.1); 0.3 of its rms amplitude. The research gives no criterion for
    // when SASI is active, so the band is always drawn.
    private const double SasiRelativeAmplitude = 0.3;

    // Main g/f-mode track: f(t) = 400 Hz + 1,556 Hz/s (t − 0.15 s), capped at 1.1-2 kHz (§2.4 step 3). The cap is taken at
    // 2 kHz, "late times reach up to about 2 kHz" (§2.1); an exploding star's track ends before it is reached.
    private const double TrackStartS = 0.15, TrackStartHz = 400, TrackSlopeHzPerS = 1556, TrackCapHz = 2000;
    private const double TrackCapS = TrackStartS + (TrackCapHz - TrackStartHz) / TrackSlopeHzPerS; // 1.178 s

    /// <summary>
    /// [APPROX] The track of an exploding star ends at 1.0 s: §2.1 gives a duration of about 0.3 to more than 1 s. The
    /// rms amplitude rises (w² linear) over the first 50 ms and fades (w² linear) over the last 0.2 s. A black hole formed
    /// after an explosion (fallback, collapsar) keeps this shape, cut at t_BH if that comes first (ruling R27). Only a
    /// failed supernova (no explosion) runs the track at full strength until t_BH: the proto-neutron star keeps accreting
    /// and ringing until it collapses (as NeutrinoModel's accretion phase does).
    /// </summary>
    private const double ExplosionTrackEndS = 1.0, TrackRampUpS = 0.05, TrackRampDownS = 0.2;

    /// <summary>The noise bandwidth (FWHM of the power spectrum) as a fraction of f(t): "about 20-30%" (§2.4 step 3).</summary>
    private const double BandwidthFraction = 0.25;

    /// <summary>
    /// [DERIVED] The low-pass Gaussian kernel's width in cycles of the carrier. A kernel exp(−u²/2σ_u²) in cycles has a
    /// power spectrum of FWHM √(ln 2)/(π σ_u) × f, so σ_u = √(ln 2)/(π × 0.25) = 1.060 cycles.
    /// </summary>
    private static readonly double KernelSigmaCycles = Math.Sqrt(Math.Log(2)) / (Math.PI * BandwidthFraction);

    /// <summary>[DERIVED] The Gaussian line's σ_f as a fraction of f: FWHM / (2√(2 ln 2)) = 0.25 / 2.3548 = 0.1062.</summary>
    private static readonly double LineSigmaFraction = BandwidthFraction / (2 * Math.Sqrt(2 * Math.Log(2)));

    /// <summary>The fixed internal seed of the noise realization. It never depends on the run number.</summary>
    private const ulong Seed = 0x5EED_6A11_0D1E_2026;

    // ---------------------------------------------------------------- rotating bounce (signals §2.2, §2.4 step 4)

    /// <summary>
    /// [DERIVED/APPROX] §2.2's worked example: an inner core of M = 0.6 M☉ and R = 15 km at bounce, in
    /// hD ~ (G M)² / (c⁴ R) × T/|W|.
    /// </summary>
    private const double InnerCoreMass = 0.6, InnerCoreRadiusKm = 15;

    /// <summary>
    /// [APPROX] T/|W| at bounce. The lab's Rotation input is not T/|W|. Fast takes §2.2's worked example, 0.05 (hD ≈ 260
    /// cm); Extreme takes 0.09, the top of the range over which hD grows linearly (0.06-0.09) before it saturates at a few
    /// hundred cm (Abdikamalov et al. 2020, Fig. 9), giving about 470 cm.
    /// </summary>
    private const double FastTOverW = 0.05, ExtremeTOverW = 0.09;

    /// <summary>Bounce frequencies of the example models: 750 Hz (rapid) and 372 Hz (extreme rotation) (§2.2) [SOURCE].</summary>
    private const double FastBounceHz = 750, ExtremeBounceHz = 372;

    /// <summary>[APPROX] Damping time of the bounce ring-down: 4 ms, the middle of §2.4 step 4's 3-5 ms.</summary>
    private const double BounceDampingS = 0.004;

    // ---------------------------------------------------------------- black hole (signals §2.4 step 3)

    /// <summary>
    /// [APPROX] The black hole's ring-down: "a short ring-down of a few kHz" (§2.4 step 3). 3 kHz, damping 0.2 ms, drawn
    /// at the main track's rms amplitude and cut 2 ms (10 damping times) after t_BH. The research gives no amplitude.
    /// </summary>
    private const double RingdownHz = 3000, RingdownDampingS = 2e-4, RingdownWindowS = 10 * RingdownDampingS;

    /// <summary>[APPROX] t_BH when a black-hole outcome carries none: 1.0 s, as the neutrino model and RemnantModel use.</summary>
    private const double DefaultTbhS = 1.0;

    // ---------------------------------------------------------------- noise curves (signals §2.3, §2.4 step 2)

    /// <summary>
    /// [APPROX] Advanced LIGO design amplitude spectral density, Hz^-1/2: "about 4e-24 near 200-300 Hz, rising roughly as
    /// f to about 1e-23 at 1 kHz" (§2.4 step 2; official curve LIGO-T1800044, not bundled). Flat at 4e-24 to 300 Hz, then
    /// a power law through 1e-23 at 1 kHz: exponent ln 2.5 / ln(10/3) = 0.761.
    /// </summary>
    private const double ALigoFloorAsd = 4e-24, ALigoKneeHz = 300, ALigoAsdAt1kHz = 1e-23;
    private static readonly double ALigoHighSlope = Math.Log(ALigoAsdAt1kHz / ALigoFloorAsd) / Math.Log(1000 / ALigoKneeHz);

    /// <summary>
    /// [APPROX] The low-frequency (seismic) wall, not in the research: ASD × √(1 + (50 Hz / f)⁴). Every component of this
    /// model lies at 100 Hz or above, where it adds at most 3%.
    /// </summary>
    private const double SeismicWallHz = 50;

    /// <summary>[APPROX] ET-D: "about 10 times lower" than Advanced LIGO (§2.4 step 2).</summary>
    private const double EtAsdFactor = 0.1;

    /// <summary>
    /// [APPROX] Cosmic Explorer (supernova-optimized): 30 times lower than Advanced LIGO. The research gives "about 10 times
    /// lower" for ET-D and CE together, but also that CE reaches "only up to about a hundred kpc" (Srivastava et al. 2019,
    /// §2.3; §11 "CE about 100 kpc"). Ten times lower would put CE at ET's ~35 kpc for the default signal; 30 puts it at
    /// ~100 kpc, so the factor is calibrated to the cited range.
    /// </summary>
    private const double CeAsdFactor = 1.0 / 30;

    /// <summary>
    /// [APPROX] The aligo entry is the Advanced LIGO/Virgo design network: three detectors, taken as identical at
    /// Advanced LIGO's sensitivity and optimally oriented, so SNR²_network = 3 SNR²_single. §2.3 quotes about 5.5 kpc for
    /// this 3-detector network.
    /// </summary>
    private const int ALigoNetworkDetectors = 3;

    // ---------------------------------------------------------------- validity

    /// <summary>Signals §11: the view runs from bounce to about 1-2 s. A later t_BH extends it to the ring-down's end.</summary>
    private const double PanelEndS = 2.0;

    private const double G = PhysicalConstants.G, C = PhysicalConstants.C;

    /// <summary>G / (π² c³): E_GW = D² f² h_rss² / this (Sutton 2013; signals §2.4 step 1).</summary>
    private static readonly double EnergyToStrain = G / (Math.PI * Math.PI * C * C * C);

    private static double Sq(double x) => x * x;

    // ---------------------------------------------------------------- normalization, computed once

    private const int SimpsonIntervals = 64;

    /// <summary>
    /// [DERIVED] ∫ Σ r² w² f² dt over an exploding star's shape (Hz² s): the integral that turns E_GW into the main
    /// track's rms hD. About 8.14e5 Hz² s.
    /// </summary>
    private static readonly double ReferenceShapeIntegral = ShapeIntegral(Shape.Reference);

    /// <summary>∫ Σ r² w² f² dt (Hz² s) over a shape's whole stochastic signal.</summary>
    private static double ShapeIntegral(Shape shape) => Integrate(t =>
    {
        double sum = 0;
        foreach (var c in Stochastic(shape, t)) sum += c.W2 * c.Hz * c.Hz;
        return sum;
    }, 0, shape.SignalEndS, shape);

    /// <summary>
    /// The main track's rms strain × distance in the flat part of the track, cm: √(G E / (π² c³ I)), 0.555 cm.
    /// §2.4 step 1 writes E_GW = (π² c³ / G) D² f0² h_rss² for one frequency f0; a chirp sweeps 100 Hz to 1.7 kHz, so
    /// each piece is weighted by its own f(t)², E_GW = (π² c³ / G) D² ∫ Σ⟨h²⟩ f² dt. The resulting ∫⟨h²⟩ dt equals §2.4's
    /// h_rss² at the energy-weighted frequency f0_eff = √(∫⟨h²⟩f² dt / ∫⟨h²⟩ dt) ≈ 1,051 Hz; normalizing at the worked
    /// example's 800 Hz instead would overstate the amplitude by 31%.
    /// </summary>
    private static readonly double ReferenceRmsHdCm = Math.Sqrt(EnergyToStrain * DefaultEnergyErg / ReferenceShapeIntegral);

    // ---------------------------------------------------------------- ISignalModel

    /// <summary>The detectors whose SNR this model gives: the LIGO/Virgo network, Einstein Telescope and Cosmic Explorer.</summary>
    public static IReadOnlyList<string> DetectorIds { get; } = new[] { "aligo", "et", "ce" };

    private const string DefaultDetectorId = "aligo";

    public string Id => "gw";
    public Messenger Messenger => Messenger.GravitationalWaves;
    /// <summary>
    /// Speculative (ruling R27): the strain rests on the research's [APPROX] track and noise curves (signals §2.4) and on
    /// this model's own [APPROX] envelope choices, and E_GW itself spans about 3 orders of magnitude (§2.1, §12.2).
    /// </summary>
    public Confidence Confidence => Confidence.Speculative;

    public IReadOnlyList<SignalChannel> Channels { get; } = BuildChannels();

    private static IReadOnlyList<SignalChannel> BuildChannels()
    {
        var list = new List<SignalChannel> { new("strain", "Strain h at Earth", "", false, MayBeNegative: true) };
        foreach (string id in DetectorIds)
        {
            list.Add(new(ChannelId.Compose("snr", id), $"{Detectors.Get(id).Name} signal-to-noise ratio since bounce", "", false));
        }
        return list;
    }

    private static readonly Source EnergySource = new(
        "E_GW is the recipe's default, 1e45 erg; across 3D models it spans about 3 orders of magnitude, 7.2e43 erg " +
        "(9 M☉) to about 2e45 erg (13 M☉) (Abdikamalov et al. 2020; Radice et al. 2019) [APPROX]", "signals §2.1, §2.4, §12");

    public IReadOnlyList<Source> Sources { get; } = new Source[]
    {
        EnergySource,
        new("Abdikamalov, Pagliaroli & Radice 2020, arXiv:2010.04356; Morozova et al. 2018, ApJ 861, 10; Radice et al. " +
            "2019, ApJL 876, L9 (time-frequency structure, energies)", "signals §2.1"),
        new("Sutton 2013, arXiv:1304.0210 (energy-strain relation for bursts)", "signals §2.4"),
        new("Dimmelmeier et al. 2008, PRD 78, 064056; Richers et al. 2017, PRD 95, 063019; Abdikamalov et al. 2020 " +
            "(rotating bounce) [APPROX amplitudes and damping]", "signals §2.2, §2.4"),
        new("Gossan et al. 2016, PRD 93, 042002; Srivastava et al. 2019, PRD 100, 043026 (ranges)", "signals §2.3, §11"),
        new("Advanced LIGO design noise, LIGO-T1800044, as approximated in the research file [APPROX]", "signals §2.4"),
    };

    public bool Applies(Outcome outcome)
        => outcome.Fate is Fate.Supernova or Fate.FailedSupernova or Fate.PulsationalPairInstability
           && outcome.Remnant.Kind is RemnantKind.NeutronStar or RemnantKind.Magnetar or RemnantKind.BlackHole;

    public (double StartS, double EndS) ValidRange(Outcome outcome)
        => Applies(outcome) ? (0, Math.Max(PanelEndS, Shape.Of(outcome).SignalEndS)) : (0, 0);

    public double Sample(Outcome outcome, ObserverSetup observer, double sourceTimeS, string channelId)
    {
        var (channel, detectorId) = ParseChannel(channelId);
        if (!Applies(outcome)) return double.NaN;
        var (start, end) = ValidRange(outcome);
        if (!(sourceTimeS >= start && sourceTimeS <= end)) return double.NaN;

        var shape = Shape.Of(outcome);
        double d = observer.LuminosityDistanceCm;
        return channel switch
        {
            "strain" => StrainHd(shape, sourceTimeS) / d,
            "snr" => Snr(shape, detectorId!, d, sourceTimeS),
            _ => throw new InvalidOperationException(),
        };
    }

    public Detection Detect(Outcome outcome, ObserverSetup observer, double sourceTimeS, Detector detector)
    {
        if (!Applies(outcome)) return new Detection(DetectionStatus.NotProduced, NotProducedReason(outcome));
        if (detector.Messenger != Messenger.GravitationalWaves || !DetectorIds.Contains(detector.Id))
            return new Detection(DetectionStatus.NotModeled, $"{detector.Name} does not detect gravitational waves.");
        if (!(sourceTimeS >= 0))
            return new Detection(DetectionStatus.NotYet,
                "The core has not collapsed yet: the gravitational waves start at core bounce.");

        var shape = Shape.Of(outcome);
        double d = observer.LuminosityDistanceCm;
        double end = ValidRange(outcome).EndS;
        double total = Snr(shape, detector.Id, d, end);
        string expected = $"{detector.Name} expects a signal-to-noise ratio of {FormatSnr(total)} over the whole signal";
        string rule = $"a detection needs {FormatSnr(detector.Threshold)}";

        if (sourceTimeS > end)
            return new Detection(DetectionStatus.NotModeled,
                $"The gravitational-wave signal is over (the model runs to {end.ToString("0.##", CultureInfo.InvariantCulture)} s). " +
                $"{expected}, so it was " +
                (total >= detector.Threshold ? "detected." : "too faint to detect."));
        if (!(total >= detector.Threshold))
            return new Detection(DetectionStatus.TooFaint, $"{expected}; {rule}.");
        if (!(Snr(shape, detector.Id, d, sourceTimeS) >= detector.Threshold))
            return new Detection(DetectionStatus.NotYet, $"The signal is still building up. {expected}; {rule}.");
        return new Detection(DetectionStatus.Detected, $"{expected}; {rule}.");
    }

    // ---------------------------------------------------------------- public formulas (tested against signals §2)

    /// <summary>
    /// h_rss (Hz^-1/2) of a quasi-monochromatic burst at f0 radiating E_GW isotropically (Sutton 2013; signals §2.4 step 1):
    /// h_rss = √(G E / (π² c³)) / (D f0). E = 2e45 erg, f0 = 800 Hz, D = 10 kpc give about 2.9e-23.
    /// </summary>
    public static double HrssForEnergy(double energyErg, double distanceCm, double frequencyHz)
        => Math.Sqrt(EnergyToStrain * energyErg) / (distanceCm * frequencyHz);

    /// <summary>
    /// [APPROX] One detector's amplitude spectral density √S_n(f), Hz^-1/2 (signals §2.4 step 2): Advanced LIGO design,
    /// with ET-D 10 and Cosmic Explorer 30 times lower. For "aligo" this is a single detector of the network.
    /// </summary>
    public static double AmplitudeSpectralDensity(string detectorId, double frequencyHz)
    {
        double factor = detectorId switch
        {
            "aligo" => 1,
            "et" => EtAsdFactor,
            "ce" => CeAsdFactor,
            _ => throw new ArgumentException($"'{detectorId}' is not a gravitational-wave detector.", nameof(detectorId)),
        };
        if (!(frequencyHz > 0)) throw new ArgumentOutOfRangeException(nameof(frequencyHz), "The frequency must be positive.");
        double highFrequency = Math.Max(1, Math.Pow(frequencyHz / ALigoKneeHz, ALigoHighSlope));
        double seismic = Math.Sqrt(1 + Math.Pow(SeismicWallHz / frequencyHz, 4));
        return factor * ALigoFloorAsd * highFrequency * seismic;
    }

    /// <summary>
    /// Narrow-band optimal SNR of one detector, ρ = h_rss √(2 / S_n(f0)) (signals §2.4 step 2, from the matched filter).
    /// </summary>
    public static double NarrowBandSnr(double hrss, string detectorId, double frequencyHz)
        => hrss * Math.Sqrt(2) / AmplitudeSpectralDensity(detectorId, frequencyHz);

    /// <summary>
    /// The rotating-bounce peak strain × distance, cm (Abdikamalov et al. 2020, Eq. 20; signals §2.2):
    /// hD ~ (G M)² / (c⁴ R) × T/|W|, with the inner core's mass in M☉ and radius in km.
    /// </summary>
    public static double BounceStrainDistanceCm(double innerCoreMass, double innerCoreRadiusKm, double tOverW)
        => Sq(G * innerCoreMass * PhysicalConstants.MSun) / (Sq(C * C) * innerCoreRadiusKm * 1e5) * tOverW;

    /// <summary>
    /// The rotating bounce's peak hD (cm) for a Fast or Extreme rotator, Speculative (signals §2.2; the T/|W| for each
    /// rotation setting is an app choice); null when the star rotates slowly or the model does not apply.
    /// </summary>
    public Quantity? BounceAmplitudeCm(Outcome outcome)
    {
        if (!Applies(outcome)) return null;
        var shape = Shape.Of(outcome);
        return shape.BounceHdCm > 0
            ? new Quantity(shape.BounceHdCm, "cm", Confidence.Speculative,
                new Source("Abdikamalov et al. 2020, Eq. 20: inner core 0.6 M☉, 15 km; T/|W| = " +
                           (outcome.Inputs.Rotation == Rotation.Extreme ? ExtremeTOverW : FastTOverW)
                           .ToString("0.00", CultureInfo.InvariantCulture) + " [APPROX]", "signals §2.2"))
            : null;
    }

    /// <summary>
    /// E_GW (erg) radiated by the stochastic part (prompt convection, SASI and the main track) of this outcome's
    /// waveform: (π² c³ / G) D² ∫ Σ⟨h²⟩ f² dt, i.e. the default 1e45 erg scaled by this shape's integral over the
    /// exploding star's. The rotating bounce and the black hole's ring-down are not included. Speculative (ruling R27);
    /// null when the model does not apply.
    /// </summary>
    public Quantity? StochasticEnergyErg(Outcome outcome)
    {
        if (!Applies(outcome)) return null;
        var shape = Shape.Of(outcome);
        return new Quantity(DefaultEnergyErg * ShapeIntegral(shape) / ReferenceShapeIntegral, "erg", Confidence.Speculative,
            EnergySource);
    }

    /// <summary>
    /// The expected rms strain of the stochastic part (prompt convection, SASI and the main track) at this source time,
    /// at Earth: the envelope the noise realization follows. NaN where <see cref="Sample"/> would be.
    /// </summary>
    public double RmsStrain(Outcome outcome, ObserverSetup observer, double sourceTimeS)
    {
        if (!Applies(outcome)) return double.NaN;
        var (start, end) = ValidRange(outcome);
        if (!(sourceTimeS >= start && sourceTimeS <= end)) return double.NaN;
        double w2 = 0;
        foreach (var c in Stochastic(Shape.Of(outcome), sourceTimeS)) w2 += c.W2;
        return ReferenceRmsHdCm * Math.Sqrt(w2) / observer.LuminosityDistanceCm;
    }

    /// <summary>
    /// The model's power spectrogram over source time 0 to <paramref name="maxTimeS"/> and frequency 0 to
    /// <paramref name="maxFreqHz"/>, as [timeBin, freqBin], in arbitrary units normalized to a maximum of 1. It is the
    /// expected power (∫⟨h²⟩ dt per bin, spread over each component's spectral line), not a noisy estimate from one
    /// realization, so it is pure and smooth. All zeros when the outcome makes no gravitational waves.
    /// </summary>
    public double[,] Spectrogram(Outcome o, ObserverSetup obs, int timeBins, int freqBins, double maxTimeS = 2.0,
        double maxFreqHz = 2048)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeBins, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(freqBins, 1);
        if (!(maxTimeS > 0)) throw new ArgumentOutOfRangeException(nameof(maxTimeS), "The time range must be positive.");
        if (!(maxFreqHz > 0)) throw new ArgumentOutOfRangeException(nameof(maxFreqHz), "The frequency range must be positive.");
        ArgumentNullException.ThrowIfNull(obs);

        var power = new double[timeBins, freqBins];
        if (!Applies(o)) return power;

        var shape = Shape.Of(o);
        double dt = maxTimeS / timeBins, df = maxFreqHz / freqBins;
        // At least 8 sub-steps per time bin and no coarser than 0.5 ms, so the 40 ms prompt packet is resolved.
        int subSteps = Math.Max(8, (int)Math.Ceiling(dt / 5e-4));
        double h = dt / subSteps;
        var z = new double[freqBins + 1];

        for (int i = 0; i < timeBins; i++)
        {
            double t0 = i * dt;
            // The stochastic components: ⟨h²⟩ dt on a Gaussian line of FWHM 25% of f.
            for (int s = 0; s < subSteps; s++)
            {
                double t = t0 + (s + 0.5) * h;
                foreach (var c in Stochastic(shape, t))
                {
                    if (c.W2 <= 0) continue;
                    double sigma = LineSigmaFraction * c.Hz;
                    for (int k = 0; k <= freqBins; k++) z[k] = (k * df - c.Hz) / sigma;
                    double weight = c.W2 * h;
                    for (int k = 0; k < freqBins; k++) power[i, k] += weight * GaussianBinFraction(z[k], z[k + 1]);
                }
            }
            // The deterministic damped sinusoids: ∫ A² e^(−2t/τ)/2 dt over the bin, on a Lorentzian line.
            if (shape.BounceHdCm > 0)
                AddDampedSine(power, i, freqBins, df, shape.BounceHdCm / ReferenceRmsHdCm, shape.BounceHz, BounceDampingS,
                    0, shape.CutS, t0, t0 + dt);
            if (shape.TbhS is double tBh)
                AddDampedSine(power, i, freqBins, df, 1, RingdownHz, RingdownDampingS, tBh, tBh + RingdownWindowS, t0, t0 + dt);
        }

        double max = power.Cast<double>().Max();
        if (max > 0)
            for (int i = 0; i < timeBins; i++)
            for (int k = 0; k < freqBins; k++)
                power[i, k] /= max;
        return power;
    }

    // ---------------------------------------------------------------- the waveform's shape

    /// <summary>
    /// How an outcome radiates. <see cref="CutS"/> is where every component stops (t_BH, or never); the track of an
    /// exploding star tapers to <see cref="TrackEndS"/>. <see cref="BounceHdCm"/> is 0 for a slow rotator.
    /// </summary>
    private sealed record Shape(double TrackEndS, bool TrackTapers, double? TbhS, double BounceHdCm, double BounceHz)
    {
        public static Shape Reference { get; } = new(ExplosionTrackEndS, true, null, 0, 0);

        public double CutS => TbhS ?? double.PositiveInfinity;

        public double SignalEndS => TbhS is double tBh ? tBh + RingdownWindowS : TrackEndS;

        public static Shape Of(Outcome o)
        {
            double? tBh = o.Remnant.Kind == RemnantKind.BlackHole ? o.Remnant.FormationTimeS?.Value ?? DefaultTbhS : null;
            var (bounceHd, bounceHz) = o.Inputs.Rotation switch
            {
                Rotation.Fast => (BounceStrainDistanceCm(InnerCoreMass, InnerCoreRadiusKm, FastTOverW), FastBounceHz),
                Rotation.Extreme => (BounceStrainDistanceCm(InnerCoreMass, InnerCoreRadiusKm, ExtremeTOverW), ExtremeBounceHz),
                _ => (0.0, 0.0),
            };
            // A failed supernova (or the collapsing core of a pulsational pair-instability star) accretes until t_BH; an
            // explosion keeps its shape, and a later black hole only cuts it (ruling R27, NeutrinoModel.Profile.Of).
            return tBh is double t && o.Fate != Fate.Supernova
                ? new Shape(t, false, t, bounceHd, bounceHz)
                : new Shape(ExplosionTrackEndS, true, tBh, bounceHd, bounceHz);
        }
    }

    /// <summary>One stochastic component at one time: its index, r² w² (relative mean-square amplitude), f (Hz) and phase (cycles).</summary>
    private readonly record struct Component(int Index, double W2, double Hz, double Cycles);

    /// <summary>The three stochastic components at time t (relative to the main track's flat rms amplitude).</summary>
    private static Component[] Stochastic(Shape shape, double t)
    {
        bool alive = t >= 0 && t <= shape.CutS;
        double prompt = alive ? Sq(PromptRelativeAmplitude) * HannSquared(t, PromptStartS, PromptEndS) : 0;
        double sasi = alive ? Sq(SasiRelativeAmplitude) * HannSquared(t, SasiStartS, SasiEndS) : 0;
        double main = alive ? MainTrackW2(shape, t) : 0;
        double s = t - TrackStartS;
        double trackCycles = t <= TrackCapS
            ? TrackStartHz * s + 0.5 * TrackSlopeHzPerS * s * s
            : TrackStartHz * (TrackCapS - TrackStartS) + 0.5 * TrackSlopeHzPerS * Sq(TrackCapS - TrackStartS)
              + TrackCapHz * (t - TrackCapS);
        return new[]
        {
            new Component(0, prompt, PromptHz, PromptHz * (t - PromptStartS)),
            new Component(1, sasi, SasiHz, SasiHz * (t - SasiStartS)),
            new Component(2, main, TrackHz(t), trackCycles),
        };
    }

    private static double TrackHz(double t) => Math.Min(TrackCapHz, TrackStartHz + TrackSlopeHzPerS * Math.Max(0, t - TrackStartS));

    /// <summary>The main track's w²: 0 before 0.15 s, rising linearly over 50 ms, then 1; an exploding star's fades linearly.</summary>
    private static double MainTrackW2(Shape shape, double t)
    {
        if (t < TrackStartS || t > shape.TrackEndS) return 0;
        double w2 = Math.Min(1, (t - TrackStartS) / TrackRampUpS);
        if (shape.TrackTapers) w2 = Math.Min(w2, (shape.TrackEndS - t) / TrackRampDownS);
        return Math.Max(0, w2);
    }

    /// <summary>The square of a Hann window sin²(π x) over [a, b]: the packet's mean-square envelope.</summary>
    private static double HannSquared(double t, double a, double b)
    {
        if (t <= a || t >= b) return 0;
        double s = Math.Sin(Math.PI * (t - a) / (b - a));
        return Sq(s * s);
    }

    // ---------------------------------------------------------------- strain

    /// <summary>h × D (cm) at source time t: the stochastic components, the rotating bounce and the black hole's ring-down.</summary>
    private static double StrainHd(Shape shape, double t)
    {
        double hd = 0;
        foreach (var c in Stochastic(shape, t))
        {
            if (c.W2 <= 0) continue;
            double phase = 2 * Math.PI * c.Cycles;
            double x = LowPassNoise(c.Index, 0, c.Cycles), y = LowPassNoise(c.Index, 1, c.Cycles);
            hd += ReferenceRmsHdCm * Math.Sqrt(c.W2) * (x * Math.Cos(phase) - y * Math.Sin(phase));
        }
        // §2.4 step 4: a negative spike at bounce followed by ringing (Dimmelmeier et al. 2008 morphology).
        if (shape.BounceHdCm > 0 && t >= 0 && t <= shape.CutS)
            hd -= shape.BounceHdCm * Math.Exp(-t / BounceDampingS) * Math.Sin(2 * Math.PI * shape.BounceHz * t);
        if (shape.TbhS is double tBh && t >= tBh && t <= tBh + RingdownWindowS)
        {
            double u = t - tBh;
            hd += ReferenceRmsHdCm * Math.Exp(-u / RingdownDampingS) * Math.Sin(2 * Math.PI * RingdownHz * u);
        }
        return hd;
    }

    /// <summary>
    /// A unit-variance, band-limited Gaussian process in the carrier's cycles u: Σ_j g_j exp(−(u − j σ)² / 2σ²) on a
    /// lattice of spacing σ = <see cref="KernelSigmaCycles"/>, with g_j standard normal from the fixed seed. The sum of the
    /// squared kernels is σ√π / spacing = √π (to 1e-4), hence the π^(−1/4).
    /// </summary>
    private static double LowPassNoise(int component, int quadrature, double cycles)
    {
        double sigma = KernelSigmaCycles;
        long j0 = (long)Math.Floor(cycles / sigma);
        double sum = 0;
        for (long j = j0 - 6; j <= j0 + 6; j++)
            sum += Normal(component, quadrature, j) * Math.Exp(-Sq(cycles - j * sigma) / (2 * sigma * sigma));
        return sum * Math.Pow(Math.PI, -0.25);
    }

    /// <summary>A standard normal number for lattice point j, from the fixed seed (SplitMix64 hashes, Box-Muller).</summary>
    private static double Normal(int component, int quadrature, long j)
    {
        ulong key = Mix(Mix(Mix(Seed ^ (ulong)component) ^ (ulong)quadrature) ^ (ulong)j);
        double u1 = ((Mix(key) >> 11) + 0.5) * (1.0 / (1UL << 53));
        double u2 = (Mix(key + 1) >> 11) * (1.0 / (1UL << 53));
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }

    private static ulong Mix(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    // ---------------------------------------------------------------- signal-to-noise ratio

    /// <summary>
    /// Optimal SNR accumulated from bounce to t at distance D (cm). Each component is narrow-band, so
    /// SNR² = ∫ 2⟨h²⟩ / S_n(f(t)) dt; a damped sinusoid's ⟨h²⟩ is A² e^(−2t/τ) / 2. The aligo network sums three detectors' SNR².
    /// </summary>
    private static double Snr(Shape shape, string detectorId, double distanceCm, double t)
    {
        double upper = Math.Min(t, shape.SignalEndS);
        if (!(upper > 0)) return 0;
        double rms2 = Sq(ReferenceRmsHdCm / distanceCm);

        double rho2 = Integrate(s =>
        {
            double sum = 0;
            foreach (var c in Stochastic(shape, s))
                if (c.W2 > 0) sum += 2 * rms2 * c.W2 / Sq(AmplitudeSpectralDensity(detectorId, c.Hz));
            return sum;
        }, 0, upper, shape);

        if (shape.BounceHdCm > 0)
            rho2 += DampedSineSnr2(shape.BounceHdCm / distanceCm, shape.BounceHz, BounceDampingS,
                Math.Min(upper, shape.CutS), detectorId);
        if (shape.TbhS is double tBh && upper > tBh)
            rho2 += DampedSineSnr2(ReferenceRmsHdCm / distanceCm, RingdownHz, RingdownDampingS,
                Math.Min(upper - tBh, RingdownWindowS), detectorId);

        int detectors = detectorId == "aligo" ? ALigoNetworkDetectors : 1;
        return Math.Sqrt(detectors * rho2);
    }

    /// <summary>∫₀^T 2 (A² e^(−2t/τ) / 2) / S_n(f) dt = A² (τ/2)(1 − e^(−2T/τ)) / S_n(f).</summary>
    private static double DampedSineSnr2(double amplitude, double frequencyHz, double tau, double duration, string detectorId)
        => Sq(amplitude) * tau / 2 * (1 - Math.Exp(-2 * duration / tau)) / Sq(AmplitudeSpectralDensity(detectorId, frequencyHz));

    /// <summary>Simpson's rule between the shape's breakpoints (where envelopes start, end or change slope) from a to b.</summary>
    private static double Integrate(Func<double, double> f, double a, double b, Shape shape)
    {
        var points = new List<double>
        {
            a, b, PromptStartS, PromptEndS, TrackStartS, TrackStartS + TrackRampUpS, SasiEndS, TrackCapS, shape.TrackEndS,
        };
        if (shape.TrackTapers) points.Add(shape.TrackEndS - TrackRampDownS);
        if (shape.TbhS is double tBh) points.Add(tBh);
        double[] edges = points.Where(x => x >= a && x <= b).Distinct().OrderBy(x => x).ToArray();

        double sum = 0;
        for (int i = 1; i < edges.Length; i++)
        {
            double lo = edges[i - 1], h = (edges[i] - lo) / SimpsonIntervals, s = f(lo) + f(edges[i]);
            for (int k = 1; k < SimpsonIntervals; k++) s += (k % 2 == 1 ? 4 : 2) * f(lo + k * h);
            sum += s * h / 3;
        }
        return sum;
    }

    // ---------------------------------------------------------------- spectrogram helpers

    /// <summary>
    /// Adds a damped sinusoid's ∫⟨h²⟩ dt over [t0, t1] ∩ [start, end] to time bin i, spread over its Lorentzian line
    /// (half-width 1/(2πτ)). The amplitude is relative to the main track's rms.
    /// </summary>
    private static void AddDampedSine(double[,] power, int i, int freqBins, double df, double relativeAmplitude,
        double frequencyHz, double tau, double start, double end, double t0, double t1)
    {
        double a = Math.Max(t0, start), b = Math.Min(t1, end);
        if (!(b > a)) return;
        double energy = Sq(relativeAmplitude) / 2 * tau / 2 * (Math.Exp(-2 * (a - start) / tau) - Math.Exp(-2 * (b - start) / tau));
        double gamma = 1 / (2 * Math.PI * tau);
        for (int k = 0; k < freqBins; k++)
            power[i, k] += energy / Math.PI *
                           (Math.Atan(((k + 1) * df - frequencyHz) / gamma) - Math.Atan((k * df - frequencyHz) / gamma));
    }

    /// <summary>The standard normal probability between z = a and z = b (a ≤ b), without cancellation in either tail.</summary>
    private static double GaussianBinFraction(double a, double b)
    {
        if (a >= 0) return UpperTail(a) - UpperTail(b);
        if (b <= 0) return UpperTail(-b) - UpperTail(-a);
        return 1 - UpperTail(-a) - UpperTail(b);
    }

    /// <summary>Q(z) = P(Z > z) = erfc(z/√2)/2, by the Numerical Recipes erfc (relative error below 1.2e-7).</summary>
    private static double UpperTail(double z)
    {
        double x = Math.Abs(z) / Math.Sqrt(2), t = 1 / (1 + 0.5 * x);
        double erfc = t * Math.Exp(-x * x - 1.26551223 + t * (1.00002368 + t * (0.37409196 + t * (0.09678418 +
            t * (-0.18628806 + t * (0.27886807 + t * (-1.13520398 + t * (1.48851587 + t * (-0.82215223 + t * 0.17087277)))))))));
        return z >= 0 ? erfc / 2 : 1 - erfc / 2;
    }

    // ---------------------------------------------------------------- channels and text

    private static (string Channel, string? DetectorId) ParseChannel(string channelId)
    {
        var id = ChannelId.Parse(channelId);
        switch (id.Base)
        {
            case "strain" when id.DetectorId is null:
                return (id.Base, null);
            case "snr":
                string detector = id.DetectorId ?? DefaultDetectorId;
                if (!DetectorIds.Contains(detector))
                    throw new ArgumentException($"'{detector}' is not a gravitational-wave detector.", nameof(channelId));
                return (id.Base, detector);
            default:
                throw new ArgumentException($"The gravitational-wave model has no channel '{channelId}'.", nameof(channelId));
        }
    }

    private static string NotProducedReason(Outcome o) => o.Fate switch
    {
        Fate.NotDeadYet => "The star has not died yet, so there are no gravitational waves.",
        Fate.WhiteDwarf => "The star ends as a white dwarf: its core never collapses, so there is no gravitational-wave burst.",
        Fate.PairInstabilitySupernova => "A pair-instability explosion blows the whole star apart with no core collapse, " +
                                         "so there is no core-bounce gravitational-wave signal.",
        _ => "A thermonuclear explosion has no collapsing core; this gravitational-wave signal comes only from core collapse.",
    };

    private static string FormatSnr(double snr)
        => snr >= 0.1 ? snr.ToString("0.#", CultureInfo.InvariantCulture) : "less than 0.1";
}
