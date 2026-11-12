using System.Globalization;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>
/// The newborn pulsar (signals §8.1–8.3, §11 "Pulsar timing"): spin period, spin-down power and the 1.4 GHz radio flux of
/// its pulses, for a neutron star or magnetar left by a supernova.
/// <list type="bullet">
/// <item>Spin-down (§8.1; vacuum dipole, Lorimer &amp; Kramer 2005, I = 10⁴⁵ g cm²): Ṗ0 = B² / (3.2e19)² / P0,
/// τ0 = P0 / (2Ṗ0), and for braking index n = 3, P(t) = P0 (1 + t/τ0)^½ and Ė(t) = Ė0 (1 + t/τ0)⁻², with
/// Ė = 4π² I Ṗ / P³. A magnetar instead uses the engine's Kasen &amp; Bildsten spin-down time t_p
/// (<see cref="MagnetarModel.SpinDownTimeS"/>) as τ0, so its spin-down matches the energy its light curve already
/// received; §8.1 notes the two dipole conventions differ (by a factor of 8 in τ0 for the same B).</item>
/// <item>Radio (§8.3): S = L_1400 / D², with L_1400 the median of Faucher-Giguère &amp; Kaspi's log-normal luminosity
/// function, 10^(−1.1) ≈ 0.079 mJy kpc² (σ = 0.9 dex, a factor of 8 either way; the Crab's 56 mJy kpc² is about 3σ
/// bright). The model is deterministic: no luminosity is drawn.</item>
/// <item>Beaming (§8.3): only observers inside the beam see pulses. Whether the beam sweeps Earth is the observer's
/// <see cref="ObserverSetup.BeamTowardEarth"/> (rolled, if at all, outside the model); when it does not, the verdict is
/// <see cref="DetectionStatus.NotPointedAtUs"/>. <see cref="BeamingFraction"/> gives the chance (Tauris &amp; Manchester
/// 1998 with its short-period extension).</item>
/// <item>Visibility (§8.3 [APPROX]): the young ejecta absorb radio for years to decades; the research says to show the
/// pulses after about 10–30 yr. The model starts at <see cref="EmergenceTimeS"/> = 10 yr and runs to 1,000 yr.</item>
/// <item>Detection (ruling R40): pulsar searches only — FAST (0.01 mJy), Parkes and MeerKAT's pulsar mode (0.1 mJy),
/// all [APPROX].</item>
/// </list>
/// Source time throughout (from core bounce). Speculative (rulings R24/R27): the spin-down law is established, but the
/// emergence time, the median luminosity and the thresholds are [APPROX].
/// </summary>
public sealed class PulsarModel : ISignalModel
{
    // ---------------------------------------------------------------- constants

    private const double Year = PhysicalConstants.Year;

    /// <summary>§8.1: the neutron star's moment of inertia, g cm².</summary>
    public const double MomentOfInertia = 1e45;

    /// <summary>§8.1: B = 3.2e19 √(P Ṗ) G (Lorimer &amp; Kramer 2005).</summary>
    private const double DipoleConstant = 3.2e19;

    /// <summary>
    /// [APPROX] §8.3: the ejecta are opaque to radio for "years to decades"; "show 'pulses visible' after about 10–30 yr".
    /// The low end, 10 yr, for every outcome: the research does not model the opacity time.
    /// </summary>
    public const double EmergenceYears = 10;

    /// <summary>The end of the timeline, 1,000 yr (§11: "about 10 yr (ejecta transparency) to 1,000 yr").</summary>
    public const double EndS = 1000 * Year;

    /// <summary>§8.3: the median radio luminosity at 1.4 GHz, mJy kpc²: 10^⟨log10 L⟩ with ⟨log10 L⟩ = −1.1.</summary>
    public static double MedianLuminosityMjyKpc2 { get; } = Math.Pow(10, -1.1);

    /// <summary>§8.3: the luminosity function's spread, dex.</summary>
    private const double LuminositySigmaDex = 0.9;

    /// <summary>[APPROX] PulseTimes refuses a window holding more than this many pulses.</summary>
    public const int MaxPulses = 100_000;

    /// <summary>The pulsar-search detectors (ruling R40), each with a channel.</summary>
    public static IReadOnlyList<string> DetectorIds { get; } = new[] { "fast", "parkes", "meerkat-psr" };

    // ---------------------------------------------------------------- ISignalModel

    public string Id => "pulsar";
    public Messenger Messenger => Messenger.Radio;
    public Confidence Confidence => Confidence.Speculative;

    public IReadOnlyList<SignalChannel> Channels { get; } = BuildChannels();

    private static IReadOnlyList<SignalChannel> BuildChannels()
    {
        var list = new List<SignalChannel>
        {
            new("periodS", "Spin period", "s", true),
            new("pdot", "Spin-down rate Ṗ", "s/s", true),
            new("edot", "Spin-down power Ė", "erg/s", true),
            new("fluxMjy", "Pulsed radio flux at 1.4 GHz (median pulsar luminosity)", "mJy", true),
        };
        foreach (string id in DetectorIds)
            list.Add(new(ChannelId.Compose("detect", id), $"Pulsed flux over {Detectors.Get(id).Name}'s limit (≥ 1: detected)",
                "", true));
        return list;
    }

    public IReadOnlyList<Source> Sources { get; } = new Source[]
    {
        new("Lorimer & Kramer 2005, Handbook of Pulsar Astronomy (vacuum dipole: Ė = 4π²IṖ/P³, B = 3.2e19 √(PṖ) G, " +
            "τ_c = P/2Ṗ, n = 3); magnetars use Kasen & Bildsten 2010's t_p, the engine's convention", "signals §8.1"),
        new("Crab check: P = 33.4 ms, Ṗ = 4.21e-13, Ė = 4.5e38 erg/s, n = 2.509 (Lyne et al. 1993; ATNF, Manchester et al. " +
            "2005)", "signals §8.2; presets §2"),
        new("Beaming fraction f(P) = 0.09 [log10(P/s) − 1]² + 0.03 (Tauris & Manchester 1998), below 0.1 s the linear " +
            "extension −0.255 log10(P/s) + 0.135 capped at 0.9 (Titus et al. 2020)", "signals §8.3"),
        new("Radio luminosity at 1.4 GHz: log-normal, ⟨log10 L⟩ = −1.1, σ = 0.9 (L in mJy kpc²; Faucher-Giguère & Kaspi " +
            "2006, ApJ 643, 332); the median is shown", "signals §8.3"),
        new("Pulses shown from 10 yr: the ejecta are opaque to radio for years to decades, \"pulses visible after about " +
            "10–30 yr\" [APPROX]; no pulsar is yet seen in SN 1987A (Fransson et al. 2024)", "signals §8.3"),
        new("Pulsar-search thresholds (ruling R40): FAST 0.01 mJy (\"large telescopes reach about 10 µJy\"), Parkes and " +
            "MeerKAT 0.1 mJy (Manchester et al. 2001) [APPROX]", "signals §8.3, §11"),
    };

    public bool Applies(Outcome outcome) => AppliesTo(outcome);

    public (double StartS, double EndS) ValidRange(Outcome outcome)
        => AppliesTo(outcome) ? (EmergenceYears * Year, EndS) : (0, 0);

    public double Sample(Outcome outcome, ObserverSetup observer, double sourceTimeS, string channelId)
    {
        var id = ParseChannel(channelId);
        if (!AppliesTo(outcome) || !InRange(sourceTimeS)) return double.NaN;
        var spin = Spin.Of(outcome);
        switch (id.Base)
        {
            case "periodS": return spin.PeriodS(sourceTimeS);
            case "pdot": return spin.PeriodDerivative(sourceTimeS);
            case "edot": return spin.SpinDownLuminosity(sourceTimeS);
        }
        if (!observer.BeamTowardEarth) return double.NaN;
        double flux = FluxMjy(observer);
        return id.DetectorId is { } d ? flux / Detectors.Get(d).Threshold : flux;
    }

    public Detection Detect(Outcome outcome, ObserverSetup observer, double sourceTimeS, Detector detector)
    {
        if (!AppliesTo(outcome)) return new Detection(DetectionStatus.NotProduced, NotProducedReason(outcome));
        if (!DetectorIds.Contains(detector.Id))
            return new Detection(DetectionStatus.NotModeled,
                $"{detector.Name} is not used as a pulsar search here; the pulsar view uses FAST, Parkes and MeerKAT's pulsar mode.");

        var inv = CultureInfo.InvariantCulture;
        var spin = Spin.Of(outcome);
        double z1 = observer.TimeDilation;
        if (!observer.BeamTowardEarth)
        {
            double p = spin.PeriodS(Math.Clamp(sourceTimeS, EmergenceYears * Year, EndS));
            return new Detection(DetectionStatus.NotPointedAtUs,
                "The pulsar's radio beams sweep past without crossing Earth, so no pulses arrive: a pulsar spinning every " +
                $"{UnitFormat.Time(p)} beams toward about {(BeamingFraction(p) * 100).ToString("0", inv)}% of directions " +
                "(Tauris & Manchester 1998). Its wind can still light up a nebula around it.");
        }
        if (!(sourceTimeS >= EmergenceYears * Year))
            return new Detection(DetectionStatus.NotYet,
                "The expanding debris still absorbs the pulsar's radio waves; pulses are expected to show through after " +
                $"about {UnitFormat.Time(EmergenceYears * Year * z1)} (10–30 years; approximate).");
        if (sourceTimeS > EndS)
            return new Detection(DetectionStatus.NotModeled, $"The model ends {UnitFormat.Time(EndS * z1)} after core collapse.");

        double flux = FluxMjy(observer), ratio = flux / detector.Threshold;
        var status = ratio >= 1 ? DetectionStatus.Detected : DetectionStatus.TooFaint;
        return new Detection(status,
            $"{detector.Name} receives about {FormatMjy(flux)} mJy of pulsed radio at 1.4 GHz against its pulsar-search " +
            $"limit of about {FormatMjy(detector.Threshold)} mJy: {(status == DetectionStatus.Detected ? "detected" : "too faint")}. " +
            $"This assumes a median pulsar ({MedianLuminosityMjyKpc2.ToString("0.###", inv)} mJy kpc²); real pulsars range about " +
            $"{Math.Pow(10, LuminositySigmaDex).ToString("0", inv)} times fainter to as many times brighter (one standard " +
            "deviation), and the young Crab pulsar is about 700 times the median. " +
            $"It spins every {UnitFormat.Time(spin.PeriodS(sourceTimeS))}.");
    }

    // ---------------------------------------------------------------- markers and pulses

    /// <summary>
    /// When the pulses become visible, s after bounce (source time): the ejecta turn transparent to radio after about
    /// 10–30 yr (§8.3 [APPROX]); 10 yr here. NaN when there is no neutron star.
    /// </summary>
    public double EmergenceTimeS(Outcome o) => AppliesTo(o) ? EmergenceYears * Year : double.NaN;

    /// <summary>
    /// The times (s after bounce, source time) at which a pulse is emitted inside [windowStartS, windowStartS +
    /// windowLengthS], for drawing the pulse train: pulse k is where the rotational phase φ(t) = (2τ0/P0)((1 + t/τ0)^½ − 1)
    /// reaches the whole number k. Empty when there is no pulsar, or outside the model's range (before
    /// <see cref="EmergenceTimeS"/> or after 1,000 yr). Whether the beam points at Earth is the caller's
    /// (<see cref="Detect"/> says NotPointedAtUs). Throws when the window would hold more than <see cref="MaxPulses"/>.
    /// </summary>
    public IReadOnlyList<double> PulseTimes(Outcome o, double windowStartS, double windowLengthS)
    {
        if (!(windowLengthS >= 0)) throw new ArgumentOutOfRangeException(nameof(windowLengthS));
        double end = windowStartS + windowLengthS;
        if (!AppliesTo(o) || !InRange(windowStartS) || !InRange(end)) return Array.Empty<double>();
        var spin = Spin.Of(o);
        double first = Math.Ceiling(spin.Phase(windowStartS)), last = Math.Floor(spin.Phase(end));
        if (last - first + 1 > MaxPulses)
            throw new ArgumentOutOfRangeException(nameof(windowLengthS), $"The window holds more than {MaxPulses} pulses.");
        var times = new List<double>();
        for (double k = first; k <= last; k++) times.Add(Math.Clamp(spin.TimeAtPhase(k), windowStartS, end));
        return times;
    }

    // ---------------------------------------------------------------- public formulas (tested against signals §8)

    /// <summary>§8.1: Ṗ = B² / (3.2e19)² / P, from the period (s) and surface dipole field (G).</summary>
    public static double PeriodDerivative(double periodS, double fieldG)
        => fieldG * fieldG / (DipoleConstant * DipoleConstant) / periodS;

    /// <summary>§8.1: Ė = 4π² I Ṗ / P³, erg/s, I = 10⁴⁵ g cm².</summary>
    public static double SpinDownLuminosity(double periodS, double periodDerivative)
        => 4 * Math.PI * Math.PI * MomentOfInertia * periodDerivative / (periodS * periodS * periodS);

    /// <summary>§8.1: the characteristic age τ_c = P / (2Ṗ), s.</summary>
    public static double CharacteristicAgeS(double periodS, double periodDerivative) => periodS / (2 * periodDerivative);

    /// <summary>
    /// §8.3: the fraction of directions a pulsar's beams sweep, f(P) = 0.09 [log10(P/s) − 1]² + 0.03 for P ≥ 0.1 s
    /// (Tauris &amp; Manchester 1998); below, the linear extension −0.255 log10(P/s) + 0.135, capped at 0.9. The two meet
    /// at 0.1 s (0.39).
    /// </summary>
    public static double BeamingFraction(double periodS)
    {
        double lp = Math.Log10(periodS);
        return periodS >= 0.1 ? 0.09 * (lp - 1) * (lp - 1) + 0.03 : Math.Min(-0.255 * lp + 0.135, 0.9);
    }

    // ---------------------------------------------------------------- the model

    private static bool AppliesTo(Outcome o)
        => o.Fate == Fate.Supernova && o.Remnant.Kind is RemnantKind.NeutronStar or RemnantKind.Magnetar
           && o.Remnant.SpinPeriodS is { Value: > 0 } && o.Remnant.MagneticFieldG is { Value: > 0 };

    private static bool InRange(double t) => t >= EmergenceYears * Year && t <= EndS;

    /// <summary>S = L_1400 / D², mJy, with the median luminosity and D in kpc.</summary>
    private static double FluxMjy(ObserverSetup obs)
    {
        double dKpc = obs.DistancePc / 1000;
        return MedianLuminosityMjyKpc2 / (dKpc * dKpc);
    }

    /// <summary>The n = 3 spin-down of one remnant: birth period P0 (s) and spin-down time τ0 (s).</summary>
    private readonly record struct Spin(double P0, double Tau0)
    {
        public static Spin Of(Outcome o)
        {
            double p0 = o.Remnant.SpinPeriodS!.Value, b = o.Remnant.MagneticFieldG!.Value;
            double tau0 = o.Remnant.Kind == RemnantKind.Magnetar
                ? MagnetarModel.SpinDownTimeS(p0, b)
                : CharacteristicAgeS(p0, PulsarModel.PeriodDerivative(p0, b));
            return new Spin(p0, tau0);
        }

        public double PeriodS(double t) => P0 * Math.Sqrt(1 + t / Tau0);

        /// <summary>Ṗ = dP/dt = P0 / (2τ0 √(1 + t/τ0)).</summary>
        public double PeriodDerivative(double t) => P0 / (2 * Tau0 * Math.Sqrt(1 + t / Tau0));

        public double SpinDownLuminosity(double t) => PulsarModel.SpinDownLuminosity(PeriodS(t), PeriodDerivative(t));

        /// <summary>φ(t) = ∫ dt/P = (2τ0/P0)((1 + x)^½ − 1), x = t/τ0, written as x/((1 + x)^½ + 1) for precision.</summary>
        public double Phase(double t)
        {
            double x = t / Tau0;
            return 2 * Tau0 / P0 * (x / (Math.Sqrt(1 + x) + 1));
        }

        /// <summary>The inverse of <see cref="Phase"/>: t = φ P0 + φ² P0² / (4τ0).</summary>
        public double TimeAtPhase(double phase) => phase * P0 + phase * phase * P0 * P0 / (4 * Tau0);
    }

    // ---------------------------------------------------------------- text

    private static string FormatMjy(double v) => v.ToString(v >= 0.01 && v < 1e4 ? "0.###" : "0.##e0", CultureInfo.InvariantCulture);

    private static string NotProducedReason(Outcome o) => o.Fate switch
    {
        Fate.NotDeadYet => "The star has not died yet, so there is no pulsar.",
        Fate.WhiteDwarf => "The star ends as a white dwarf, not a neutron star, so there is no pulsar.",
        Fate.FailedSupernova or Fate.PulsationalPairInstability =>
            "No pulsar: the core collapses to a black hole, which has no surface or magnetic field to make pulses.",
        Fate.PairInstabilitySupernova => "No pulsar: a pair-instability explosion blows the whole star apart, leaving nothing.",
        _ => o.Remnant.Kind switch
        {
            RemnantKind.BlackHole => "No pulsar: the explosion leaves a black hole, which has no surface or magnetic field to make pulses.",
            RemnantKind.None => "No pulsar: a thermonuclear (Type Ia) explosion destroys the white dwarf completely.",
            _ => "This outcome leaves no neutron star, so there is no pulsar.",
        },
    };

    private static ChannelId ParseChannel(string channelId)
    {
        var id = ChannelId.Parse(channelId);
        bool ok = id.DetectorId is null
            ? id.Base is "periodS" or "pdot" or "edot" or "fluxMjy"
            : id.Base == "detect" && DetectorIds.Contains(id.DetectorId);
        return ok ? id : throw new ArgumentException($"The pulsar model has no channel '{channelId}'.", nameof(channelId));
    }
}
