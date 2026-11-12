using System.Globalization;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>
/// Neutrinos from core collapse (signals §1). Emission: the Pagliaroli et al. 2009 two-component fit (accretion plus
/// cooling) scaled to the neutron star's binding energy, plus the neutronization burst (§1.2); a black hole cuts every
/// flavour off at t_BH (§1.3). Detection: inverse beta decay counts (§1.4) and Super-K's 2-events-in-20-s rule; IceCube
/// by significance (§11).
/// <para>Channels: <c>lum_nue</c>, <c>lum_anue</c>, <c>lum_nux</c> (erg/s at the source, ν_x per species),
/// <c>meanE_anue</c> (MeV at the source), and per detector <c>rate@id</c> (events/s) and <c>cumulative@id</c> (events
/// since bounce); IceCube counts excess photon hits. A bare <c>rate</c> or <c>cumulative</c> is Super-K.</para>
/// Only core collapse makes a detectable burst; white dwarfs, thermonuclear (Type Ia, Iax) and pair-instability
/// explosions have no collapsing core, so the model does not apply.
/// <para>[APPROX] Counts use the luminosity distance (signals §0.3) and are per second of source time; the redshift of
/// neutrino energies and of the rate is ignored. It matters only beyond 50 Mpc, where every detector expects far
/// fewer than one event.</para>
/// </summary>
public sealed class NeutrinoModel : ISignalModel
{
    // ---------------------------------------------------------------- Pagliaroli et al. 2009, model III.3 (signals §1.2)

    private const double TauA = 0.55;             // accretion time scale, s
    private const double TauC = 4.7;              // cooling time scale, s
    private const double Tc = 4.6;                // cooling temperature, MeV
    private const double Ta = 2.4;                // accretion temperature, MeV
    private const double AccretionDecayS = 0.5;   // the 1/(1 + t/0.5 s) factor of L_a(t), §1.2b

    /// <summary>L_c0 = 4π R_c² σ_ν T_c⁴ per species, 6.6e51 erg/s (signals §1.2, derived there from R_c = 16 km).</summary>
    private const double Lc0 = 6.6e51;

    /// <summary>
    /// L_a0 for each of ν_e and anti-ν_e. Signals §1.2 derives 1.1e53 erg/s, but notes that this gives 1.5 times the fit's
    /// published accretion energy E_a = 4.8e52 erg (anti-ν_e + ν_e) and suggests renormalizing. Done here [DERIVED]:
    /// L_a0 = E_a / (2 × 0.324 s), where 0.324 s is the time integral of the L_a shape (§1.2): 7.41e52 erg/s.
    /// </summary>
    private const double La0 = 4.8e52 / (2 * 0.324);

    /// <summary>Mean energy of a zero-degeneracy Fermi-Dirac spectrum, ⟨E⟩ = 3.151 T (Janka 2017; signals §1.2a).</summary>
    private const double FermiDiracMeanOverT = 3.151;

    /// <summary>
    /// [DERIVED] Accretion-phase ⟨E⟩/T_a: signals §1.2 gives ⟨E⟩ ≈ 12.3 MeV for anti-ν_e early in accretion, at
    /// T_a = 2.4 MeV, so ⟨E⟩ = (12.3 / 2.4) T_a(t).
    /// </summary>
    private const double AccretionMeanOverT = 12.3 / 2.4;

    // ---------------------------------------------------------------- neutronization burst (signals §1.1, §1.2d)

    private const double BurstPeakErgS = 3.5e53, BurstPeakS = 5e-3, BurstSigmaS = 2.5e-3;

    // ---------------------------------------------------------------- black-hole formation (signals §1.3)

    // [APPROX] The recommended app model: L_a held flat, anti-ν_e ⟨E⟩ rising linearly from about 12 to about 20 MeV at t_BH.
    private const double FailedMeanStartMeV = 12, FailedMeanAtTbhMeV = 20;

    /// <summary>
    /// Ruling R23 [APPROX, Speculative]: the flat accretion luminosity saturates, L_a = L_a0 min(1, t_sat / t_BH) with
    /// t_sat = 2.0 s, so E(anti-ν_e) = L_a0 min(t_BH, t_sat) ≤ 1.48e53 erg. Signals §1.3's 28-model compilation (Suwa et al.
    /// 2025, Table 1) spans E(anti-ν_e) = 0.27–1.53e53 erg over t_BH = 0.34–6.2 s, and does not grow linearly with t_BH
    /// (Choi 2025, 23 M☉: t_BH 6.23 s, 0.736e53 erg). Held flat at L_a0, a 4.7 s collapse would radiate 3.5e53.
    /// </summary>
    private const double FailedAccretionSaturationS = 2.0;

    /// <summary>[APPROX] t_BH when a black-hole outcome carries none: 1.0 s, as RemnantModel uses (O'Connor & Ott 2011).</summary>
    private const double DefaultTbhS = 1.0;

    /// <summary>
    /// [APPROX] A black hole formed by fallback after an explosion: the proto-neutron star grows until it reaches the
    /// maximum neutron-star mass, then collapses. Its binding energy sets the luminosity before t_BH. Radius 12 km
    /// (outcomes §6, §8.2 step 8).
    /// </summary>
    private const double FallbackProtoNeutronStarMass = RemnantModel.MaxNeutronStarMass, CanonicalRadiusKm = 12;

    /// <summary>[APPROX] Mass for a neutron-star remnant that carries no mass: the canonical 1.4 M☉ (signals §1.1).</summary>
    private const double CanonicalNeutronStarMass = 1.4;

    // ---------------------------------------------------------------- detection (signals §1.2e, §1.4)

    /// <summary>
    /// MSW-only anti-ν_e survival at Earth, P = cos²θ12 cos²θ13 ≈ 0.7 (normal ordering), so
    /// F(anti-ν_e) = P F0(anti-ν_e) + (1 − P) F0(ν_x) (Pagliaroli et al. 2009; signals §1.2e).
    /// </summary>
    private const double AntiNueSurvival = 0.7;

    /// <summary>Vogel & Beacom 1999: σ ≈ 9.52e-44 (E_e p_e / MeV²) cm², with E_e ≈ E_ν − 1.293 MeV (signals §1.4).</summary>
    private const double IbdSigma0Cm2 = 9.52e-44, IbdDeltaMeV = 1.293;

    /// <summary>
    /// [DERIVED] ⟨E²⟩/⟨E⟩² of a zero-degeneracy Fermi-Dirac spectrum: (F4/F2)/(F3/F2)² with F_n = Γ(n+1) η(n+1):
    /// F2 = 1.803086, F3 = 5.682198, F4 = 23.33088 give 12.9394 / 3.15137² = 1.30292.
    /// </summary>
    private const double FermiDiracSecondMomentRatio = 1.30292;

    /// <summary>Free protons per kiloton: water 6.69e31 [DERIVED, 2 H per H₂O]; liquid scintillator about 7e31 [APPROX].</summary>
    private const double ProtonsPerKtWater = 6.69e31, ProtonsPerKtScintillator = 7e31;

    /// <summary>
    /// [APPROX] IceCube photon hits per Super-K event: the ratio of the midpoints of SNEWS 2.0 Table 4's ranges at 10 kpc,
    /// IceCube 3.2e5–8.2e5 hits and Super-K 4,000–7,800 events: 5.7e5 / 5,900.
    /// </summary>
    private const double IceCubeHitsPerSuperKEvent = 5.7e5 / 5900;

    /// <summary>
    /// [DERIVED] IceCube significance per excess hit, anchored to "about 6σ at 50 kpc" (signals §11) for the typical
    /// 5.7e5 hits at 10 kpc: 6 / (5.7e5 × (10/50)²).
    /// </summary>
    private const double IceCubeSigmaPerHit = 6 / (5.7e5 * 0.04);

    /// <summary>
    /// [APPROX] ν_e mean energies for DUNE's ν_e + ⁴⁰Ar rate: 12.5 MeV in the burst (12–13 MeV, signals §1.1) and 11.5 MeV
    /// otherwise (10–13 MeV, §1.2e). The cross-section is taken ∝ E².
    /// </summary>
    private const double BurstNueMeanMeV = 12.5, NueMeanMeV = 11.5;

    /// <summary>
    /// Ruling R25 [APPROX]: ν_e survival at Earth with MSW conversion in the normal ordering, P_ee ≈ sin²θ13 ≈ 0.022
    /// (global oscillation fits), so F(ν_e at Earth) = 0.022 F0(ν_e) + 0.978 F0(ν_x). The ν_x take the anti-ν_e cooling
    /// mean energy, as for inverse beta decay.
    /// </summary>
    private const double NueSurvival = 0.022;

    /// <summary>[APPROX] DUNE's count at 10 kpc for a typical supernova: about 3,000 ν_e charged-current events (SNEWS 2.0).</summary>
    private const double DuneTypicalEventsAt10Kpc = 3000;

    /// <summary>
    /// Super-K's distant-supernova trigger counts events with positron energy E_e+ ≥ 17 MeV (Abdikamalov et al. 2020;
    /// signals §1.4; ruling R25), i.e. E_ν ≥ 17 + 1.293 MeV.
    /// </summary>
    private const double SuperKTriggerPositronMeV = 17;

    /// <summary>Complete Fermi integrals F_n(0) = Γ(n+1) η(n+1) for n = 2, 3, 4 (see <see cref="FermiDiracSecondMomentRatio"/>).</summary>
    private const double F2 = 1.803086, F3 = 5.682198, F4 = 23.33088;

    /// <summary>Super-K's distant-supernova trigger window (Abdikamalov et al. 2020; signals §1.4).</summary>
    private const double TriggerWindowS = 20;

    /// <summary>Signals §11: the neutrino view runs from bounce to about 100 s.</summary>
    private const double EndS = 100;

    private const double DistanceAt10KpcCm = 10 * PhysicalConstants.Kpc;
    private static double Sq(double x) => x * x;

    // ---------------------------------------------------------------- normalizations, computed once from the formulas

    // Integration segments, declared before the normalizations below because static fields initialize in order. The
    // ends sit around the features: the 5 ms burst, the 0.55 s accretion phase and the 4.7 s cooling decay.
    private static readonly double[] Breaks =
        { 0, 0.002, 0.004, 0.006, 0.008, 0.01, 0.015, 0.02, 0.05, 0.1, 0.2, 0.3, 0.5, 0.75, 1, 1.5, 2, 3, 5, 7, 10, 15, 20, 30, 50, 75, 100 };
    private const int SimpsonIntervals = 16;

    /// <summary>
    /// The unscaled fit's own total energy, ∫(2 L_a + 6 L_c) dt over 0–100 s, about 2.37e53 erg. Signals §1.2 scales all
    /// luminosities by E_ν,tot / 2.2e53 erg, the fit's quoted total. Implemented as written, the combined curve (§1.2c,
    /// cooling delayed by τ_a) integrates to 8% more than 2.2e53, so the scale divides by this integral instead: the six
    /// flavours then radiate exactly the binding energy (the burst comes on top).
    /// </summary>
    private static readonly double FitEnergyErg = Integrate(t =>
    {
        var e = EmissionAt(Profile.Reference, t);
        return 2 * e.AccL + 6 * e.CoolXL;
    }, 0, EndS, null);

    /// <summary>DUNE's κ in N = κ ∫ Σ L ⟨E⟩ dt / (4π D²), set so the unscaled fit at 10 kpc gives 3,000 events.</summary>
    private static readonly double DuneKappa = DuneTypicalEventsAt10Kpc /
        Integrate(t => DuneRawRate(EmissionAt(Profile.Reference, t), DistanceAt10KpcCm), 0, EndS, null);

    // ---------------------------------------------------------------- ISignalModel

    /// <summary>The detectors whose counts this model gives: Super-K, Hyper-K, IceCube, JUNO and DUNE.</summary>
    public static IReadOnlyList<string> DetectorIds { get; } = new[] { "superk", "hyperk", "icecube", "juno", "dune" };

    private const string DefaultDetectorId = "superk";

    public string Id => "neutrino";
    public Messenger Messenger => Messenger.Neutrinos;
    public Confidence Confidence => Confidence.ModelDependent;

    public IReadOnlyList<SignalChannel> Channels { get; } = BuildChannels();

    private static IReadOnlyList<SignalChannel> BuildChannels()
    {
        var list = new List<SignalChannel>
        {
            new("lum_nue", "ν_e luminosity", "erg/s", true),
            new("lum_anue", "anti-ν_e luminosity", "erg/s", true),
            new("lum_nux", "ν_x luminosity (each of ν_μ, ν_τ and their antineutrinos)", "erg/s", true),
            new("meanE_anue", "anti-ν_e mean energy", "MeV", false),
        };
        foreach (string id in DetectorIds)
        {
            string name = Detectors.Get(id).Name;
            bool hits = id == "icecube";
            list.Add(new(ChannelId.Compose("rate", id), $"{name} {(hits ? "excess photon hits" : "event rate")}",
                hits ? "hits/s" : "events/s", true));
            list.Add(new(ChannelId.Compose("cumulative", id), $"{name} {(hits ? "excess photon hits" : "events")} since bounce",
                hits ? "hits" : "events", true));
        }
        return list;
    }

    public IReadOnlyList<Source> Sources { get; } = new Source[]
    {
        new("Pagliaroli, Vissani, Costantini & Ianni 2009, Astropart. Phys. 31, 163 (model III.3)", "signals §1.2"),
        new("Kachelriess et al. 2005, PRD 71, 063003; Janka 2017, arXiv:1702.08713 (neutronization burst)", "signals §1.1, §1.2d"),
        new("Lattimer & Prakash 2001, ApJ 550, 426 (binding energy)", "signals §1.1"),
        new("O'Connor & Ott 2011, ApJ 730, 70; Suwa et al. 2025, arXiv:2504.19510 (black-hole cutoff) [APPROX]", "signals §1.3"),
        new("Vogel & Beacom 1999, PRD 60, 053003 (inverse beta decay)", "signals §1.4"),
        new("Al Kharusi et al. 2021, NJP 23, 031201 (SNEWS 2.0 detector counts); Abdikamalov et al. 2020 (Super-K trigger)",
            "signals §1.4, §11"),
    };

    public bool Applies(Outcome outcome) => AppliesTo(outcome);

    private static bool AppliesTo(Outcome outcome)
        => outcome.Fate is Fate.Supernova or Fate.FailedSupernova or Fate.PulsationalPairInstability
           && outcome.Remnant.Kind is RemnantKind.NeutronStar or RemnantKind.Magnetar or RemnantKind.BlackHole;

    public (double StartS, double EndS) ValidRange(Outcome outcome) => Applies(outcome) ? (0, EndS) : (0, 0);

    public double Sample(Outcome outcome, ObserverSetup observer, double sourceTimeS, string channelId)
    {
        var (channel, detectorId) = ParseChannel(channelId);
        if (!Applies(outcome) || !(sourceTimeS >= 0 && sourceTimeS <= EndS)) return double.NaN;

        var p = Profile.Of(outcome);
        var e = EmissionAt(p, sourceTimeS);
        double d = observer.LuminosityDistanceCm;
        return channel switch
        {
            "lum_nue" => e.BurstL + e.AccL + e.CoolEL,
            "lum_anue" => e.AccL + e.CoolEL,
            "lum_nux" => e.CoolXL,
            "meanE_anue" => e.AntiNueMeanEnergy,
            "rate" => Rate(e, detectorId!, d),
            "cumulative" => Cumulative(p, detectorId!, d, sourceTimeS),
            _ => throw new InvalidOperationException(),
        };
    }

    public Detection Detect(Outcome outcome, ObserverSetup observer, double sourceTimeS, Detector detector)
    {
        if (!Applies(outcome)) return new Detection(DetectionStatus.NotProduced, NotProducedReason(outcome));
        if (detector.Messenger != Messenger.Neutrinos || !DetectorIds.Contains(detector.Id))
            return new Detection(DetectionStatus.NotModeled, $"{detector.Name} does not detect neutrinos.");
        if (sourceTimeS < 0)
            return new Detection(DetectionStatus.NotYet, "The core has not collapsed yet: the neutrino burst starts at core bounce.");

        var p = Profile.Of(outcome);
        double d = observer.LuminosityDistanceCm;
        bool icecube = detector.Id == "icecube";
        // Ruling R25: Super-K's trigger counts only events above 17 MeV; the plotted channels count every IBD event.
        bool superK = detector.Id == "superk";
        double window = Cumulative(p, detector.Id, d, TriggerWindowS, superK);
        // IceCube's figure of merit is the significance of its excess hits; the others count events.
        double Merit(double count) => icecube ? count * IceCubeSigmaPerHit : count;
        double total = Merit(window);
        string Expected(string verb) => icecube
            ? $"{detector.Name} {verb} its photon hits over the first 20 s to rise {FormatSigma(total)} above the background"
            : $"{detector.Name} {verb} about {Format(window)} events{(superK ? " at 17 MeV or above" : "")} in the first 20 s";
        string expected = Expected("expects");
        string rule = icecube
            ? $"a detection needs {FormatSigma(detector.Threshold)}"
            : $"its trigger needs {Format(detector.Threshold)} within 20 s";

        if (!double.IsNaN(sourceTimeS) && sourceTimeS > EndS)
            return new Detection(DetectionStatus.NotModeled,
                $"The neutrino burst is over (the model runs to {EndS:0} s). {Expected("expected")}, so it was " +
                (total >= detector.Threshold ? "detected." : "too faint to detect."));
        if (!(total >= detector.Threshold))
            return new Detection(DetectionStatus.TooFaint, $"{expected}; {rule}.");

        double now = Merit(Cumulative(p, detector.Id, d, Math.Min(sourceTimeS, TriggerWindowS), superK));
        if (!(now >= detector.Threshold))
            return new Detection(DetectionStatus.NotYet, $"The first neutrinos are arriving. {expected}; {rule}.");
        return new Detection(DetectionStatus.Detected, $"{expected}; {rule}.");
    }

    // ---------------------------------------------------------------- public formulas (tested against signals §1)

    /// <summary>
    /// Neutron-star binding energy, erg (Lattimer & Prakash 2001; signals §1.1): BE = 0.6β/(1 − 0.5β) M c², with
    /// β = G M / (R c²). M is the gravitational mass in M☉, R in km. 1.4 M☉ and 12 km give about 2.8e53 erg.
    /// </summary>
    public static double BindingEnergyErg(double gravitationalMassMsun, double radiusKm)
    {
        double mc2 = gravitationalMassMsun * PhysicalConstants.MSun * PhysicalConstants.C * PhysicalConstants.C;
        double beta = PhysicalConstants.G * gravitationalMassMsun * PhysicalConstants.MSun /
                      (radiusKm * 1e5 * PhysicalConstants.C * PhysicalConstants.C);
        return 0.6 * beta / (1 - 0.5 * beta) * mc2;
    }

    /// <summary>
    /// [DERIVED] The inverse-beta-decay cross-section averaged over a zero-degeneracy Fermi-Dirac spectrum of this mean
    /// energy, cm²: 9.52e-44 ⟨(E − 1.293 MeV)²⟩ with ⟨E²⟩ = 1.30292 ⟨E⟩² (p_e ≈ E_e). Reproduces signals §1.4's table:
    /// 1.5e-41 (12 MeV), 2.1e-41 (14), 2.4e-41 (15), 2.8e-41 (16).
    /// </summary>
    public static double MeanIbdCrossSectionCm2(double meanEnergyMeV)
        => IbdSigma0Cm2 * (FermiDiracSecondMomentRatio * Sq(meanEnergyMeV) - 2 * IbdDeltaMeV * meanEnergyMeV + Sq(IbdDeltaMeV));

    /// <summary>
    /// [DERIVED] The fraction of inverse-beta-decay events with positron energy E_e+ = E_ν − 1.293 MeV ≥ 17 MeV (Super-K's
    /// distant trigger, ruling R25), for a zero-degeneracy Fermi-Dirac spectrum of this mean energy. Events follow
    /// E² (E − 1.293)² / (1 + e^{E/T}), T = ⟨E⟩/3.151, the same weighting as <see cref="MeanIbdCrossSectionCm2"/>: the
    /// whole-spectrum integral is T⁵F4 − 2ΔT⁴F3 + Δ²T³F2, and the part above threshold is integrated numerically.
    /// ⟨E⟩ = 12, 15, 20 MeV give 0.520, 0.700, 0.864.
    /// </summary>
    public static double SuperKTriggerFraction(double meanEnergyMeV)
    {
        if (!(meanEnergyMeV > 0)) return 0;
        double t = meanEnergyMeV / FermiDiracMeanOverT, delta = IbdDeltaMeV;
        double whole = Math.Pow(t, 5) * F4 - 2 * delta * Math.Pow(t, 4) * F3 + delta * delta * Math.Pow(t, 3) * F2;
        double eMin = SuperKTriggerPositronMeV + delta, eMax = eMin + 60 * t;
        const int n = 200;
        double h = (eMax - eMin) / n, sum = 0;
        for (int k = 0; k <= n; k++)
        {
            double e = eMin + k * h, w = k == 0 || k == n ? 1 : k % 2 == 1 ? 4 : 2;
            sum += w * e * e * Sq(e - delta) / (1 + Math.Exp(e / t));
        }
        return Math.Clamp(sum * h / 3 / whole, 0, 1);
    }

    // ---------------------------------------------------------------- Kamiokande-II (the SN 1987A check, spec §8)

    /// <summary>
    /// Kamiokande-II's sensitive water mass for the SN 1987A burst, kt: 2,140 t (Hirata et al. 1987, PRL 58, 1490; as used
    /// by Fiorillo et al. 2023, PRD 108, 083040, Sec. III and Table 5). Verified in docs/research/presets-verification.md.
    /// </summary>
    public const double KamiokandeIIKilotons = 2.14;

    /// <summary>
    /// Kamiokande-II's detection efficiency for a positron of this energy (MeV): η = 0.932 / √(1 + (34 / (12 − 7x + x²))²)
    /// for x = E_e+/MeV &gt; 4, else 0 (Fiorillo et al. 2023, PRD 108, 083040, Eq. 8, a fit to the published Kam-II trigger
    /// efficiency curve). It is 0.39 at the "traditional" 7.5 MeV threshold (signals §1.5), 0.72 at 10 MeV and 0.92 at 20
    /// MeV. Verified in docs/research/presets-verification.md.
    /// </summary>
    public static double KamiokandeIIEfficiency(double positronMeV)
    {
        if (!(positronMeV > 4)) return 0;
        double x = positronMeV, q = 34 / (12 - 7 * x + x * x);
        return 0.932 / Math.Sqrt(1 + q * q);
    }

    /// <summary>
    /// The expected number of SN 1987A-like events in Kamiokande-II: inverse beta decays on the free protons of 2.14 kt of
    /// water over the first 20 s, each counted with <see cref="KamiokandeIIEfficiency"/> of its positron energy.
    /// SN 1987A gave 11 events above threshold (Hirata et al. 1987; signals §1.5).
    /// </summary>
    public static double KamiokandeIIEvents(Outcome outcome, ObserverSetup observer)
        => WaterCherenkovIbdEvents(outcome, observer, KamiokandeIIKilotons, KamiokandeIIEfficiency, TriggerWindowS);

    /// <summary>
    /// [DERIVED] Expected inverse-beta-decay events in a water Cherenkov detector of this mass (kt) from bounce to
    /// <paramref name="windowS"/> (s), each event weighted by the detector's efficiency for its positron energy (MeV),
    /// E_e+ = E_ν − 1.293 MeV, over the zero-degeneracy Fermi-Dirac spectra the model uses (as for Super-K's trigger
    /// fraction, <see cref="SuperKTriggerFraction"/>). 0 when the model does not apply.
    /// </summary>
    public static double WaterCherenkovIbdEvents(Outcome outcome, ObserverSetup observer, double kilotons,
        Func<double, double> positronEfficiency, double windowS)
    {
        if (!AppliesTo(outcome) || !(windowS > 0)) return 0;
        var p = Profile.Of(outcome);
        double d = observer.LuminosityDistanceCm, protons = kilotons * ProtonsPerKtWater;
        double upper = Math.Min(windowS, Math.Min(EndS, p.CutoffS ?? EndS));
        double Events(double l, double meanE)
            => Number(l, meanE) is var n && n > 0 ? n * MeanIbdCrossSectionCm2(meanE) * DetectedFraction(meanE, positronEfficiency) : 0;
        return Integrate(t =>
        {
            var e = EmissionAt(p, t);
            double antiNue = Events(e.AccL, e.AccMeanE) + Events(e.CoolEL, e.CoolMeanE);
            double nuX = Events(e.CoolXL, e.CoolMeanE);
            return protons * Fluence(d) * (AntiNueSurvival * antiNue + (1 - AntiNueSurvival) * nuX);
        }, 0, upper, p.CutoffS);
    }

    /// <summary>
    /// [DERIVED] The efficiency-weighted fraction of inverse-beta-decay events for a zero-degeneracy Fermi-Dirac spectrum of
    /// this mean energy: ∫ η(E − 1.293) E² (E − 1.293)² / (1 + e^{E/T}) dE over the whole-spectrum integral
    /// T⁵F4 − 2ΔT⁴F3 + Δ²T³F2 (the weighting of <see cref="MeanIbdCrossSectionCm2"/>), Simpson over E_e+ from 0 to 60T.
    /// </summary>
    private static double DetectedFraction(double meanEnergyMeV, Func<double, double> positronEfficiency)
    {
        if (!(meanEnergyMeV > 0)) return 0;
        double t = meanEnergyMeV / FermiDiracMeanOverT, delta = IbdDeltaMeV;
        double whole = Math.Pow(t, 5) * F4 - 2 * delta * Math.Pow(t, 4) * F3 + delta * delta * Math.Pow(t, 3) * F2;
        double eMin = delta, eMax = eMin + 60 * t;
        const int n = 400;
        double h = (eMax - eMin) / n, sum = 0;
        for (int k = 0; k <= n; k++)
        {
            double e = eMin + k * h, w = k == 0 || k == n ? 1 : k % 2 == 1 ? 4 : 2;
            sum += w * positronEfficiency(e - delta) * e * e * Sq(e - delta) / (1 + Math.Exp(e / t));
        }
        return Math.Clamp(sum * h / 3 / whole, 0, 1);
    }

    // SuperKTriggerFraction tabulated on a log grid of ⟨E⟩ from 1 to 1,000 MeV, so Detect's 20 s integrals do not
    // integrate a spectrum at every step. Below 1 MeV the fraction is ~e^-57, taken as 0; above 1,000 MeV it is ~1.
    private const double TriggerTableMinMeV = 1, TriggerTableMaxMeV = 1000;
    private const int TriggerTableSize = 513;
    private static readonly double[] TriggerTable = Enumerable.Range(0, TriggerTableSize)
        .Select(i => SuperKTriggerFraction(TriggerTableMinMeV *
                                           Math.Pow(TriggerTableMaxMeV / TriggerTableMinMeV, (double)i / (TriggerTableSize - 1))))
        .ToArray();

    private static double TriggerFractionFast(double meanEnergyMeV)
    {
        if (!(meanEnergyMeV > TriggerTableMinMeV)) return 0;
        if (meanEnergyMeV >= TriggerTableMaxMeV) return 1;
        double x = Math.Log(meanEnergyMeV / TriggerTableMinMeV) / Math.Log(TriggerTableMaxMeV / TriggerTableMinMeV)
                   * (TriggerTableSize - 1);
        int i = Math.Min((int)x, TriggerTableSize - 2);
        return TriggerTable[i] + (x - i) * (TriggerTable[i + 1] - TriggerTable[i]);
    }

    // ---------------------------------------------------------------- emission

    /// <summary>How an outcome emits: the luminosity scale, whether the shock failed, and the black-hole cutoff.</summary>
    private sealed record Profile(double Scale, bool Failed, double? CutoffS)
    {
        /// <summary>The unscaled fit: SN 1987A's neutron star, no black hole.</summary>
        public static Profile Reference { get; } = new(1, false, null);

        public static Profile Of(Outcome o)
        {
            var r = o.Remnant;
            if (r.Kind == RemnantKind.BlackHole)
            {
                double tBh = r.FormationTimeS?.Value ?? DefaultTbhS;
                // A collapse with no explosion (failed supernova, or the core of a pulsational pair-instability star):
                // the accretion phase lasts until the black hole forms (§1.3 recommended app model).
                if (o.Fate != Fate.Supernova) return new Profile(1, true, tBh);
                // An explosion that leaves a black hole (fallback, collapsar): the full curve, truncated at t_BH (§1.3).
                return new Profile(BindingEnergyErg(FallbackProtoNeutronStarMass, CanonicalRadiusKm) / FitEnergyErg, false, tBh);
            }
            double m = r.Mass?.Value ?? CanonicalNeutronStarMass, radius = r.RadiusKm?.Value ?? CanonicalRadiusKm;
            // Signals §1.2: E_ν,tot from the binding energy; about 99% of it leaves as neutrinos (§1.1), taken as all.
            return new Profile(BindingEnergyErg(m, radius) / FitEnergyErg, false, null);
        }
    }

    /// <summary>
    /// Luminosities (erg/s) and mean energies (MeV) at one time. AccL is in each of ν_e and anti-ν_e; CoolEL in each of
    /// ν_e and anti-ν_e; CoolXL in each ν_x. The burst is ν_e only.
    /// </summary>
    private readonly record struct Emission(double BurstL, double AccL, double AccMeanE, double CoolEL, double CoolXL, double CoolMeanE)
    {
        public double AntiNueMeanEnergy
        {
            get
            {
                double l = AccL + CoolEL;
                return l > 0 ? l / (AccL / AccMeanE + CoolEL / CoolMeanE) : double.NaN;
            }
        }
    }

    private static Emission EmissionAt(Profile p, double t)
    {
        // §1.3: every flavour stops when the black hole forms (a step).
        if (p.CutoffS is double tBh && t > tBh) return default;

        // §1.2d, nearly progenitor-independent, so not scaled.
        double burst = BurstPeakErgS * Math.Exp(-Sq(t - BurstPeakS) / (2 * Sq(BurstSigmaS)));
        // §1.2a and c: cooling starts at about τ_a, L_c(t − τ_a) weighted by 1 − exp(−(t/τ_a)²); ⟨E⟩ = 3.151 T_c(t − τ_a).
        double cool = (1 - Math.Exp(-Sq(t / TauA))) * Lc0 * Math.Exp(-(t - TauA) / TauC);
        double coolE = FermiDiracMeanOverT * Tc * Math.Exp(-(t - TauA) / (4 * TauC));

        if (p.Failed)
        {
            // §1.3 [APPROX]: anti-ν_e and ν_e from accretion held flat, ⟨E⟩ rising linearly to t_BH. The flat level
            // saturates for late black holes (ruling R23). ν_x keep the fit's (lower) thermal emission from the
            // proto-neutron star, unscaled.
            double tBhFailed = p.CutoffS!.Value;
            double flat = La0 * Math.Min(1, FailedAccretionSaturationS / tBhFailed);
            double accE = FailedMeanStartMeV + (FailedMeanAtTbhMeV - FailedMeanStartMeV) * t / tBhFailed;
            return new Emission(burst, flat, accE, 0, cool, coolE);
        }

        // §1.2b: L_a(t) = L_a0 exp(−(t/τ_a)²) / (1 + t/0.5 s), T_a(t) = T_a + (0.6 T_c − T_a)(t/τ_a)².
        double acc = La0 * Math.Exp(-Sq(t / TauA)) / (1 + t / AccretionDecayS);
        double accMean = AccretionMeanOverT * (Ta + (0.6 * Tc - Ta) * Sq(t / TauA));
        return new Emission(burst, p.Scale * acc, accMean, p.Scale * cool, p.Scale * cool, coolE);
    }

    // ---------------------------------------------------------------- detection rates

    private static double Fluence(double distanceCm) => 1 / (4 * Math.PI * distanceCm * distanceCm);

    /// <summary>Number emission rate (s⁻¹) of a component with luminosity L (erg/s) and mean energy E (MeV).</summary>
    private static double Number(double l, double meanE) => l > 0 ? l / (meanE * PhysicalConstants.MeV) : 0;

    /// <summary>
    /// Inverse beta decays per second on <paramref name="protons"/> free protons (signals §1.4):
    /// N_p (L/⟨E⟩) ⟨σ⟩ / (4π D²) summed over the anti-ν_e reaching Earth, P × anti-ν_e + (1 − P) × ν_x. With
    /// <paramref name="aboveTrigger"/>, only events above Super-K's 17 MeV trigger threshold.
    /// </summary>
    private static double IbdRate(Emission e, double protons, double distanceCm, bool aboveTrigger = false)
    {
        double Events(double l, double meanE)
        {
            double n = Number(l, meanE) * MeanIbdCrossSectionCm2(meanE);
            return n > 0 && aboveTrigger ? n * TriggerFractionFast(meanE) : n;
        }
        double antiNue = Events(e.AccL, e.AccMeanE) + Events(e.CoolEL, e.CoolMeanE);
        // [APPROX] ν_x take the anti-ν_e cooling spectrum (signals §1.2e: "slightly softer").
        double nuX = Events(e.CoolXL, e.CoolMeanE);
        return protons * Fluence(distanceCm) * (AntiNueSurvival * antiNue + (1 - AntiNueSurvival) * nuX);
    }

    /// <summary>
    /// DUNE's ν_e rate before κ: Σ L ⟨E⟩ / (4π D²), a cross-section ∝ E² on the number flux of ν_e reaching Earth
    /// (P_ee ν_e + (1 − P_ee) ν_x, ruling R25) [APPROX].
    /// </summary>
    private static double DuneRawRate(Emission e, double distanceCm)
        => Fluence(distanceCm) * (NueSurvival * (e.BurstL * BurstNueMeanMeV + (e.AccL + e.CoolEL) * NueMeanMeV)
                                  + (1 - NueSurvival) * e.CoolXL * e.CoolMeanE);

    /// <summary>Events (IceCube: hits) per second; <paramref name="aboveTrigger"/> applies Super-K's 17 MeV cut.</summary>
    private static double Rate(Emission e, string detectorId, double distanceCm, bool aboveTrigger = false) => detectorId switch
    {
        // §1.4: Super-K 32 kt and Hyper-K 217 kt (fiducial for supernovae) of water; JUNO 20 kt of scintillator.
        "superk" => IbdRate(e, 32 * ProtonsPerKtWater, distanceCm, aboveTrigger),
        "hyperk" => IbdRate(e, 217 * ProtonsPerKtWater, distanceCm),
        "juno" => IbdRate(e, 20 * ProtonsPerKtScintillator, distanceCm),
        "icecube" => IceCubeHitsPerSuperKEvent * IbdRate(e, 32 * ProtonsPerKtWater, distanceCm),
        "dune" => DuneKappa * DuneRawRate(e, distanceCm),
        _ => throw new ArgumentException($"No neutrino rate for detector '{detectorId}'."),
    };

    /// <summary>Events (or IceCube hits) from bounce to <paramref name="t"/>; nothing arrives after t_BH or the model's end.</summary>
    private static double Cumulative(Profile p, string detectorId, double distanceCm, double t, bool aboveTrigger = false)
    {
        double upper = Math.Min(t, Math.Min(EndS, p.CutoffS ?? EndS));
        return upper > 0
            ? Integrate(s => Rate(EmissionAt(p, s), detectorId, distanceCm, aboveTrigger), 0, upper, p.CutoffS)
            : 0;
    }

    /// <summary>Simpson's rule over the fixed segments (plus the cutoff, where the rate steps to zero) from a to b.</summary>
    private static double Integrate(Func<double, double> f, double a, double b, double? cutoff)
    {
        var points = Breaks.Where(x => x > a && x < b).Append(a).Append(b);
        if (cutoff is double c && c > a && c < b) points = points.Append(c);
        double[] edges = points.Distinct().OrderBy(x => x).ToArray();

        double sum = 0;
        for (int i = 1; i < edges.Length; i++)
        {
            double lo = edges[i - 1], h = (edges[i] - lo) / SimpsonIntervals, s = f(lo) + f(edges[i]);
            for (int k = 1; k < SimpsonIntervals; k++) s += (k % 2 == 1 ? 4 : 2) * f(lo + k * h);
            sum += s * h / 3;
        }
        return sum;
    }

    // ---------------------------------------------------------------- channels and text

    private static (string Channel, string? DetectorId) ParseChannel(string channelId)
    {
        var id = ChannelId.Parse(channelId);
        switch (id.Base)
        {
            case "lum_nue" or "lum_anue" or "lum_nux" or "meanE_anue" when id.DetectorId is null:
                return (id.Base, null);
            case "rate" or "cumulative":
                string detector = id.DetectorId ?? DefaultDetectorId;
                if (!DetectorIds.Contains(detector))
                    throw new ArgumentException($"'{detector}' is not a neutrino detector.", nameof(channelId));
                return (id.Base, detector);
            default:
                throw new ArgumentException($"The neutrino model has no channel '{channelId}'.", nameof(channelId));
        }
    }

    private static string NotProducedReason(Outcome o) => o.Fate switch
    {
        Fate.NotDeadYet => "The star has not died yet, so there is no neutrino burst.",
        Fate.WhiteDwarf => "The star ends as a white dwarf: its core never collapses, so there is no neutrino burst.",
        Fate.PairInstabilitySupernova => "A pair-instability explosion blows the whole star apart with no core collapse, " +
                                         "so there is no neutrino burst like a core-collapse supernova's.",
        _ => "A thermonuclear explosion has no collapsing core; the neutrino burst comes only from core collapse.",
    };

    private static string Format(double n)
    {
        var inv = CultureInfo.InvariantCulture;
        if (n >= 1000)
        {
            double scale = Math.Pow(10, Math.Floor(Math.Log10(n)) - 2);
            return (Math.Round(n / scale) * scale).ToString("N0", inv);
        }
        if (n >= 10) return Math.Round(n).ToString("0", inv);
        if (n >= 0.01) return n.ToString("0.##", inv);
        return "fewer than 0.01";
    }

    private static string FormatSigma(double s)
        => s >= 0.1 ? s.ToString("0.#", CultureInfo.InvariantCulture) + "σ" : "less than 0.1σ";
}
