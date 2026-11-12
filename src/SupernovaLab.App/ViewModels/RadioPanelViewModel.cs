using System.Globalization;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Units;

namespace SupernovaLab.App.ViewModels;

/// <summary>
/// Radio (spec §5.1; signals §6.1, §7, §8.3): the 1.4, 5 and 22 GHz light curves of the shock in the star's old wind
/// (and the GRB's 5 GHz afterglow when there is one), one second of pulsar pulses at the current time when a pulsar
/// exists, points at us and the ejecta have cleared, and the remnant's radio brightness. Instruments: VLA, MeerKAT, FAST,
/// SKA, and (ruling R40) Parkes and MeerKAT's pulsar mode for the pulsar view.
/// </summary>
public sealed class RadioPanelViewModel : InstrumentPanelViewModel
{
    public const string ShockTab = "Shock in the wind", PulsarTab = "Pulsar", RemnantTab = "Remnant";

    /// <summary>The pulse train shows this much observer time.</summary>
    public const double PulseWindowS = 1;

    /// <summary>The pulse train is recomputed when the time has moved by more than this factor.</summary>
    private const double PulseTimeStep = 1.002;

    private static readonly string[] ContinuumIds = { "vla", "meerkat", "ska" };

    private readonly CsmInteractionModel _csm = new();
    private readonly PulsarModel _pulsar = new();
    private readonly RemnantEvolutionModel _remnant = new();
    private readonly GrbModel _grb = new();
    private double _pulseTimeS = double.NaN;

    public RadioPanelViewModel()
        : base(new[] { ShockTab, PulsarTab, RemnantTab },
            new[] { "vla", "meerkat", "fast", "ska", "parkes", "meerkat-psr" }.Select(Detectors.Get).ToList()) { }

    public override Messenger Messenger => Messenger.Radio;
    public override string Title => "Radio";
    public override string TitleToolTip => Glossary.RadioPeak;

    protected override IReadOnlyList<PlotSeries> BuildMainSeries() => SampleChannel(_csm, "radio5");

    protected override PanelContent BuildTab(string subTab, Detector instrument)
    {
        bool continuum = ContinuumIds.Contains(instrument.Id);
        switch (subTab)
        {
            case ShockTab:
                var series = new[] { "radio1_4", "radio5", "radio22" }.SelectMany(c => SampleChannel(_csm, c)).ToList();
                if (_grb.Applies(Outcome)) series.AddRange(SampleChannel(_grb, "afterglowRadio", "GRB afterglow at 5 GHz"));
                return new PanelContent
                {
                    Series = series, LogY = true, YLabel = "Flux density (mJy)",
                    Lines = continuum
                        ? new[] { LimitLine(instrument, instrument.Threshold, $"limit, {Figure(instrument.Threshold)} mJy") }
                        : Array.Empty<ReferenceLine>(),
                    Note = continuum ? null : $"{instrument.Name} is a pulsar search here; pick VLA, MeerKAT or SKA for this view.",
                };
            case PulsarTab:
                return new PanelContent
                {
                    Kind = PlotKind.PulseTrain, YLabel = "Pulse",
                    Note = "Pulse shapes are schematic; the period and flux are the model's. Pulses show from about 10 years, " +
                           "once the debris lets radio waves through (signals §8.3, approximate).",
                };
            default:
                return new PanelContent
                {
                    Series = SampleChannel(_remnant, "radio1GHzJy"), LogY = true, YLabel = "Flux density at 1 GHz (Jy)",
                    Lines = continuum
                        ? new[] { LimitLine(instrument, instrument.Threshold / 1000, $"limit, {Figure(instrument.Threshold)} mJy") }
                        : Array.Empty<ReferenceLine>(),
                    Note = "Every remnant here has Cas A's radio brightness, scaled to this distance, so the curve does not change with age (an upper-end estimate; approximate).",
                };
        }
    }

    protected override IReadOnlyList<ISignalModel> TabModels(string subTab) => subTab switch
    {
        ShockTab => HasOutcome && _grb.Applies(Outcome) ? new ISignalModel[] { _csm, _grb } : new ISignalModel[] { _csm },
        PulsarTab => new ISignalModel[] { _pulsar },
        _ => new ISignalModel[] { _remnant },
    };

    /// <summary>Ruling R50: GrbModel judges the drawn 5 GHz afterglow for the continuum instruments whose limits are drawn.</summary>
    protected override IReadOnlyList<ISignalModel> DetectorModels(Detector instrument)
        => ContinuumIds.Contains(instrument.Id) ? new ISignalModel[] { _csm, _remnant, _grb } : new ISignalModel[] { _pulsar };

    protected override (ISignalModel Model, string ChannelId)? PrimaryChannel(string subTab, Detector instrument) => subTab switch
    {
        ShockTab => (_csm, "radio5"),
        RemnantTab => (_remnant, "radio1GHzJy"),
        _ => null,
    };

    /// <summary>
    /// One second (observer time) of pulses from <see cref="PulsarModel.PulseTimes"/> at the current time, in observer time
    /// ((1 + z) × source), from the first pulse. None when the beam misses Earth (the model leaves that to the caller).
    /// </summary>
    protected override void RefreshInstantView(bool force)
    {
        if (SelectedSubTab != PulsarTab)
        {
            PulseTrain = null;
            _pulseTimeS = double.NaN;
            return;
        }
        double t = SourceTimeS, z1 = Observer.TimeDilation;
        if (!force && double.IsFinite(_pulseTimeS) && t / _pulseTimeS < PulseTimeStep && _pulseTimeS / t < PulseTimeStep) return;
        _pulseTimeS = t;
        if (!Observer.BeamTowardEarth || !_pulsar.Applies(Outcome))
        {
            PulseTrain = null;
            return;
        }
        IReadOnlyList<double> times;
        try
        {
            times = _pulsar.PulseTimes(Outcome, t, PulseWindowS / z1);
        }
        catch (Exception e)
        {
            throw new ModelErrorException(_pulsar.Id, $"{e.GetType().Name}: {e.Message}", e);
        }
        if (times.Count == 0)
        {
            PulseTrain = null;
            return;
        }
        var offsets = times.Select(x => (x - times[0]) * z1).ToList();
        double period = SampleOne(_pulsar, "periodS", t) * z1, flux = SampleOne(_pulsar, "fluxMjy", t);
        PulseTrain = new PulseTrainView(offsets, period, PulseWindowS,
            $"One second of pulses at {UnitFormat.Time(TimeS)}: one every {UnitFormat.Time(period)}, " +
            $"{Figure(flux)} mJy of pulsed radio at 1.4 GHz.");
    }

    protected override string? InstantViewNote()
    {
        if (PulseTrain is not null) return null;
        var detector = Instruments.First(d => d.Id == (PulsarModel.DetectorIds.Contains(SelectedInstrument.Id) ? SelectedInstrument.Id : "fast"));
        var d = DetectCached(_pulsar, detector, SourceTimeS);
        return d.Status is DetectionStatus.Detected or DetectionStatus.TooFaint ? "No pulses in this second." : d.Reason;
    }

    private static string Figure(double v) => v.ToString(v >= 0.01 ? "0.###" : "0.###e0", CultureInfo.InvariantCulture);
}
