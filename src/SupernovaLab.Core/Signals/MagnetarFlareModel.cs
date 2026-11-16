using System.Globalization;
using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>
/// A magnetar's giant flares on the gamma-ray panel (spec §5, §7.3; compact §C6): a spike of <see cref="GiantFlare.SpikeS"/> at each
/// flare's time, so no view shows a flare that no panel records. The flares are <see cref="MagnetarFlares.For"/>'s, drawn from the
/// run number (the panel gives it; the default is run 1). Applies only for a magnetar. Detection runs through the panel's existing
/// instruments, Fermi GBM and Swift BAT, on the same Band-spectrum trigger test as the gamma-ray burst (<see cref="GrbModel.TriggerRatio(double, double, ObserverSetup, string)"/>).
/// <list type="bullet">
/// <item>Channel <c>spike</c>: the bolometric flux (rest 1–10⁴ keV) in the GRB tab's units, erg cm⁻² s⁻¹: L_peak / (4π D_L²) while the
/// spike lasts (0.35 s of source time), 0 between flares. Source time throughout; the panel and the markers put it in observer
/// time, × (1 + z).</item>
/// <item>Giant flares have been observed (three, 1979–2004), so the audit's "Expected" wording does not apply
/// (<see cref="NeverObserved"/>). The flare is not beamed: the jets checkbox does not matter.</item>
/// </list>
/// Speculative: the rate, the energies and the spectrum are [APPROX] (compact §C6).
/// </summary>
public sealed class MagnetarFlareModel : ISignalModel
{
    /// <summary>[APPROX] The rest-frame peak energy of the flare's Band spectrum, keV. Not in compact §C6: an assumed hard spectrum.</summary>
    public const double AssumedPeakEnergyKeV = 500;

    private readonly int _runNumber;

    /// <summary>The model for run 1 (the contract tests' constructor).</summary>
    public MagnetarFlareModel() : this(1) { }

    /// <summary>The model for this run: its flares are <see cref="MagnetarFlares.For"/>(outcome, runNumber).</summary>
    public MagnetarFlareModel(int runNumber) => _runNumber = runNumber;

    public string Id => "magnetar-flare";
    public Messenger Messenger => Messenger.GammaRays;
    public Confidence Confidence => Confidence.Speculative;

    public IReadOnlyList<SignalChannel> Channels { get; } = new SignalChannel[]
    {
        new("spike", "Giant-flare spike flux (bolometric, rest-frame 1–10⁴ keV)", "erg cm⁻² s⁻¹", true),
        new(ChannelId.Compose("detect", "gbm"), "Fermi GBM 50–300 keV photon flux over its trigger threshold (≥ 1: triggers)", "", true),
        new(ChannelId.Compose("detect", "bat"), "Swift BAT 15–150 keV flux over its trigger threshold (≥ 1: triggers)", "", true),
    };

    public IReadOnlyList<Source> Sources { get; } = new Source[]
    {
        new("Mereghetti 2008 (Table 2: the giant flares of 5 March 1979, " +
            "27 August 1998 and 27 December 2004; spikes of 0.25–0.5 s, energies 1.5 × 10⁴⁴–5 × 10⁴⁶ erg isotropic); the lab's spike " +
            "lasts 0.35 s and its energy is log-uniform over 1.5 × 10⁴⁴–2 × 10⁴⁶ erg [APPROX]", "compact §C6"),
        new("Rate per magnetar: 3 flares / (47.7 yr × 30 magnetars) = 2.1 × 10⁻³ per year [DERIVED], Gehrels 1σ 0.96–4.1 × 10⁻³; " +
            "cross-check 1.3 × 10⁻³ from Burns et al. 2021. The observed magnetars are 10³–10⁵ yr old: a newborn's rate is unknown",
            "compact §C6"),
        new("The flare's spectrum is assumed to be a Band function with E_p,i = 500 keV [APPROX; not in compact §C6], tested against the " +
            "burst's own Fermi GBM and Swift BAT thresholds (signals §9.1, §11); the flare is taken as isotropic", "signals §9.1, §11"),
    };

    public bool Applies(Outcome outcome) => MagnetarFlares.Applies(outcome);

    public (double StartS, double EndS) ValidRange(Outcome outcome)
        => Applies(outcome) ? (Magnetosphere.MagnetosphereFormsS(outcome) is var s && double.IsFinite(s) ? s : 0, TimeAxis.MaxS) : (0, 0);

    /// <summary>The run's flares for this outcome (empty unless a magnetar).</summary>
    public IReadOnlyList<GiantFlare> Flares(Outcome outcome) => MagnetarFlares.For(outcome, _runNumber);

    /// <summary>
    /// Source times to sample so each short spike is caught: its start and end for every flare (the 400-point grid would miss a third of
    /// a second at any time past a few seconds).
    /// </summary>
    public IEnumerable<double> SpikeTimesS(Outcome outcome)
        => Flares(outcome).SelectMany(f => new[] { f.SourceTimeS, f.SourceTimeS + f.SpikeS / 2 });

    /// <summary>The flare that started most recently at or before this source time, or null before the first.</summary>
    public GiantFlare? LastStartedBy(Outcome outcome, double sourceTimeS)
    {
        GiantFlare? last = null;
        foreach (var f in Flares(outcome))
            if (f.SourceTimeS <= sourceTimeS) last = f;
        return last;
    }

    public double Sample(Outcome outcome, ObserverSetup observer, double sourceTimeS, string channelId)
    {
        var id = ParseChannel(channelId);
        if (!Applies(outcome)) return double.NaN;
        var (start, end) = ValidRange(outcome);
        if (!(sourceTimeS >= start && sourceTimeS <= end)) return double.NaN;
        double flux = 0;
        if (LastStartedBy(outcome, sourceTimeS) is { } f && sourceTimeS <= f.SourceTimeS + f.SpikeS) flux = Flux(f, observer);
        return id.DetectorId is { } d ? GrbModel.TriggerRatio(flux, AssumedPeakEnergyKeV, observer, d) : flux;
    }

    public Detection Detect(Outcome outcome, ObserverSetup observer, double sourceTimeS, Detector detector)
    {
        if (!Applies(outcome))
            return new Detection(DetectionStatus.NotProduced, "No giant flare: only a magnetar flares, and this outcome leaves none.");
        if (!GrbModel.DetectorIds.Contains(detector.Id))
            return new Detection(DetectionStatus.NotModeled, $"{detector.Name} does not watch for gamma-ray bursts, so it does not judge giant flares.");

        var inv = CultureInfo.InvariantCulture;
        double z1 = observer.TimeDilation;
        var flares = Flares(outcome);
        if (flares.Count == 0)
            return new Detection(DetectionStatus.NotModeled,
                $"This run draws no giant flare in the {UnitFormat.Time(TimeAxis.MaxS)} of the timeline: the lab's rate is about 1 per " +
                $"{(1 / MagnetarFlares.RatePerYear).ToString("0", inv)} years per magnetar (speculative), so most runs have one or two at most. " +
                "Re-roll draws again.");
        if (!(sourceTimeS >= flares[0].SourceTimeS))
            return new Detection(DetectionStatus.NotYet,
                $"This run's first giant flare comes {UnitFormat.Time(flares[0].SourceTimeS * z1)} after core collapse.");

        var f = LastStartedBy(outcome, sourceTimeS)!;
        double flux = Flux(f, observer), ratio = GrbModel.TriggerRatio(flux, AssumedPeakEnergyKeV, observer, detector.Id);
        var status = ratio >= 1 ? DetectionStatus.Detected : DetectionStatus.TooFaint;
        string measure = detector.Id == "gbm"
            ? $"{Number(ratio * detector.Threshold)} photons cm⁻² s⁻¹ in 50–300 keV against its trigger threshold of about {detector.Threshold.ToString("0.#", inv)}"
            : $"{Number(ratio * detector.Threshold)} erg cm⁻² s⁻¹ in 15–150 keV against its trigger threshold of about {detector.Threshold.ToString("0.#e0", inv)}";
        string flare = $"The flare's spike is {UnitFormat.Energy(f.EnergyErg)} (isotropic) in {f.SpikeS.ToString("0.00", inv)} s, which would last " +
                       $"{(f.SpikeS * z1).ToString("0.00", inv)} s here; a spectrum peaking near {AssumedPeakEnergyKeV.ToString("0", inv)} keV is assumed " +
                       "[APPROX]; it assumes the instrument is watching that part of the sky.";
        if (sourceTimeS > f.SourceTimeS + f.SpikeS)
            return new Detection(DetectionStatus.NotModeled,
                $"The giant flare {UnitFormat.Time(f.SourceTimeS * z1)} after core collapse is over (its spike lasted under a second). At its peak " +
                $"{detector.Name} received {measure}: {(status == DetectionStatus.Detected ? "it triggered" : "too faint to trigger")}. {flare}");
        return new Detection(status,
            $"{detector.Name} receives {measure} from the spike: {(status == DetectionStatus.Detected ? "it triggers" : "too faint to trigger")}. {flare}");
    }

    /// <summary>The spike's bolometric flux at Earth, erg cm⁻² s⁻¹: L_peak / (4π D_L²).</summary>
    private static double Flux(GiantFlare f, ObserverSetup observer)
        => f.PeakLuminosityErgS / (4 * Math.PI * Math.Pow(observer.LuminosityDistanceCm, 2));

    private static string Number(double v) => v.ToString(v >= 0.01 && v < 1e4 ? "0.##" : "0.##e0", CultureInfo.InvariantCulture);

    private static ChannelId ParseChannel(string channelId)
    {
        var id = ChannelId.Parse(channelId);
        bool ok = id.DetectorId is null ? id.Base == "spike" : id.Base == "detect" && GrbModel.DetectorIds.Contains(id.DetectorId);
        return ok ? id : throw new ArgumentException($"The magnetar-flare model has no channel '{channelId}'.", nameof(channelId));
    }
}
