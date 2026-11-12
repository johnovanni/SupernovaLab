using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Timeline;

namespace SupernovaLab.App.ViewModels;

/// <summary>
/// Gravitational waves (spec §5.1; signals §2): the strain h(t) and its spectrogram over the first ~2 s, including the
/// rotating-bounce signal for Fast or Extreme rotation. Instruments: the LIGO/Virgo network, Einstein Telescope, Cosmic
/// Explorer.
/// </summary>
public sealed class GravitationalWavePanelViewModel : InstrumentPanelViewModel
{
    public const string StrainTab = "Strain", SpectrogramTab = "Spectrogram";

    /// <summary>
    /// The strain is a waveform (up to 2 kHz, the black hole's ring-down at 3 kHz), so it is sampled every 1/8,192 s rather
    /// than on the 400-point grid, which would alias it; at most this many points.
    /// </summary>
    private const double StrainSampleRateHz = 8192;
    private const int MaxStrainPoints = 40_000;

    /// <summary>The spectrogram's bins: 256 in time, 128 in frequency up to the model's default 2,048 Hz.</summary>
    private const int SpectrogramTimeBins = 256, SpectrogramFreqBins = 128;
    private const double SpectrogramMaxHz = 2048;

    private readonly GravitationalWaveModel _gw = new();
    private IReadOnlyList<PlotSeries> _strain = Array.Empty<PlotSeries>();

    public GravitationalWavePanelViewModel()
        : base(new[] { StrainTab, SpectrogramTab }, GravitationalWaveModel.DetectorIds.Select(Detectors.Get).ToList()) { }

    public override Messenger Messenger => Messenger.GravitationalWaves;
    public override string Title => "Gravitational waves";
    public override string TitleToolTip => Glossary.GravitationalWaves;

    protected override IReadOnlyList<PlotSeries> BuildMainSeries() => _strain = SampleStrain();

    protected override PanelContent BuildTab(string subTab, Detector instrument)
    {
        if (subTab == StrainTab)
            return new PanelContent
            {
                Series = _strain,
                YLabel = "Strain h at Earth",
                Note = "The first seconds after core bounce: convection, the standing-shock wobble and the ringing " +
                       "proto-neutron star" + (Outcome.Inputs.Rotation is Core.Progenitors.Rotation.Fast or Core.Progenitors.Rotation.Extreme
                           ? ", with the spike of the rotating core's bounce at the start."
                           : "."),
            };

        double maxS = Math.Max(2.0, _gw.ValidRange(Outcome).EndS);
        SpectrogramView? view = null;
        if (_gw.Applies(Outcome))
        {
            var power = _gw.Spectrogram(Outcome, Observer, SpectrogramTimeBins, SpectrogramFreqBins, maxS, SpectrogramMaxHz);
            var log = new double[SpectrogramTimeBins, SpectrogramFreqBins];
            for (int i = 0; i < SpectrogramTimeBins; i++)
            for (int k = 0; k < SpectrogramFreqBins; k++)
            {
                double p = power[i, k];
                if (double.IsInfinity(p)) throw new ModelErrorException(_gw.Id, "the spectrogram has an infinite bin", null);
                log[i, k] = p > 0 ? Math.Max(Math.Log10(p), -SpectrogramView.FloorDecades) : -SpectrogramView.FloorDecades;
            }
            view = new SpectrogramView(log, maxS * Observer.TimeDilation, SpectrogramMaxHz);
        }
        string note = "Colour on a log scale over 6 decades: the rotating bounce is about 10⁵ times stronger than the " +
                      "track, so a linear scale would show only the bounce.";
        if (Outcome.Remnant.Kind == RemnantKind.BlackHole)
            note += " The black hole's ring-down, near 3 kHz, lies above this plot's 2,048 Hz range; it is in the strain view.";
        return new PanelContent { Kind = PlotKind.Spectrogram, Spectrogram = view, YLabel = "Frequency (Hz)", Note = note };
    }

    /// <summary>After the brief signal: "Over", and whether the instrument caught it.</summary>
    protected override (string Full, string Short) Badge(Detection d) => OverBadge(_gw, d) ?? base.Badge(d);

    protected override IReadOnlyList<ISignalModel> TabModels(string subTab) => new ISignalModel[] { _gw };
    protected override IReadOnlyList<ISignalModel> DetectorModels(Detector instrument) => new ISignalModel[] { _gw };

    /// <summary>The readout is the selected instrument's signal-to-noise ratio accumulated so far.</summary>
    protected override (ISignalModel Model, string ChannelId)? PrimaryChannel(string subTab, Detector instrument)
        => (_gw, ChannelId.Compose("snr", instrument.Id));

    protected override string? InstantViewNote()
        => Spectrogram is null ? NotDrawnReason(_gw) : SourceTimeS > _gw.ValidRange(Outcome).EndS ? NotDrawnReason(_gw) : null;

    /// <summary>The strain every 1/8,192 s from the axis start to the end of the signal (observer time).</summary>
    private IReadOnlyList<PlotSeries> SampleStrain()
    {
        if (!_gw.Applies(Outcome)) return Array.Empty<PlotSeries>();
        double z1 = Observer.TimeDilation, end = _gw.ValidRange(Outcome).EndS;
        double start = TimeAxis.MinS / z1;
        double dt = Math.Max(1 / StrainSampleRateHz, (end - start) / MaxStrainPoints);
        int n = (int)Math.Floor((end - start) / dt) + 1;
        var obs = new double[n];
        var values = new double[n];
        for (int i = 0; i < n; i++)
        {
            double t = start + i * dt;
            obs[i] = t * z1;
            values[i] = SampleOne(_gw, "strain", t);
        }
        return Split("Strain h at Earth", obs, values, "", logY: false, "strain");
    }
}
