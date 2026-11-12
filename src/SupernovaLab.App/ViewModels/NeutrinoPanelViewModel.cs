using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Text;

namespace SupernovaLab.App.ViewModels;

/// <summary>
/// Neutrinos (spec §5.1; signals §1): the event rate and the cumulative events in the selected detector, with one
/// simulated run of individual events drawn as dots (Poisson, seeded by the run number); the luminosity of each flavour;
/// the anti-ν_e mean energy; and the cutoff when a black hole forms. Instruments: Super-K, Hyper-K, IceCube, JUNO, DUNE.
/// </summary>
public sealed class NeutrinoPanelViewModel : InstrumentPanelViewModel
{
    public const string RateTab = "Event rate", CumulativeTab = "Cumulative events", LuminosityTab = "Luminosity",
        MeanEnergyTab = "Mean energy";

    /// <summary>
    /// At most about this many dots are drawn. A nearby supernova gives millions of events (Betelgeuse: about 10⁷ in
    /// Super-K), and EventSampler has no cap, so each dot then stands for N events: the rate is divided by N before
    /// sampling, and the label says "1 dot = N events".
    /// </summary>
    public const int MaxDots = 5000;

    private readonly NeutrinoModel _nu = new();

    public NeutrinoPanelViewModel()
        : base(new[] { RateTab, CumulativeTab, LuminosityTab, MeanEnergyTab }, NeutrinoModel.DetectorIds.Select(Detectors.Get).ToList()) { }

    public override Messenger Messenger => Messenger.Neutrinos;
    public override string Title => "Neutrinos";
    public override string TitleToolTip => Glossary.NeutrinoBurst;

    /// <summary>How many events each drawn dot stands for (1 when every event is drawn).</summary>
    public int EventsPerDot { get; private set; } = 1;

    protected override IReadOnlyList<PlotSeries> BuildMainSeries()
        => SampleChannel(_nu, ChannelId.Compose("rate", "superk"), extraSourceTimesS: BurstTimes());

    protected override PanelContent BuildTab(string subTab, Detector instrument)
    {
        var lines = BlackHoleLine();
        switch (subTab)
        {
            case RateTab:
                var rate = SampleChannel(_nu, ChannelId.Compose("rate", instrument.Id), extraSourceTimesS: BurstTimes());
                return new PanelContent
                {
                    Series = rate, Lines = lines, LogY = true, YLabel = $"{Unit(rate, "events/s")}",
                    Note = instrument.Id == "icecube"
                        ? "IceCube sees no single neutrinos, only a rise in the light its sensors record (excess photon hits)."
                        : null,
                };
            case CumulativeTab:
                var cumulative = SampleChannel(_nu, ChannelId.Compose("cumulative", instrument.Id), extraSourceTimesS: BurstTimes());
                var dots = SampleDots(instrument);
                return new PanelContent
                {
                    Series = cumulative.Concat(dots).ToList(), Lines = lines, LogY = true, YLabel = Unit(cumulative, "events"),
                    Note = dots.Count == 0
                        ? null
                        : EventsPerDot == 1
                            ? $"Dots: one simulated run (#{RunNumber}) of individual events, each dot one {Word(instrument)}; Re-roll draws another."
                            : $"Dots: one simulated run (#{RunNumber}); 1 dot = {EventsPerDot:N0} {Word(instrument)}s. Re-roll draws another.",
                };
            case LuminosityTab:
                return new PanelContent
                {
                    Series = new[] { "lum_nue", "lum_anue", "lum_nux" }
                        .SelectMany(c => SampleChannel(_nu, c, extraSourceTimesS: BurstTimes())).ToList(),
                    Lines = lines, LogY = true, YLabel = "Luminosity (erg/s)",
                };
            default:
                return new PanelContent
                {
                    Series = SampleChannel(_nu, "meanE_anue", extraSourceTimesS: BurstTimes()), Lines = lines,
                    YLabel = "Mean energy (MeV)",
                    Note = "The model gives the mean energy of anti-ν_e, the flavour that water and scintillator detectors " +
                           "mostly see through inverse beta decay; it does not give the other flavours' mean energies.",
                };
        }
    }

    /// <summary>After the brief signal: "Over", and whether the instrument caught it.</summary>
    protected override (string Full, string Short) Badge(Detection d) => OverBadge(_nu, d) ?? base.Badge(d);

    protected override IReadOnlyList<ISignalModel> TabModels(string subTab) => new ISignalModel[] { _nu };
    protected override IReadOnlyList<ISignalModel> DetectorModels(Detector instrument) => new ISignalModel[] { _nu };

    protected override (ISignalModel Model, string ChannelId)? PrimaryChannel(string subTab, Detector instrument) => subTab switch
    {
        RateTab => (_nu, ChannelId.Compose("rate", instrument.Id)),
        CumulativeTab => (_nu, ChannelId.Compose("cumulative", instrument.Id)),
        LuminosityTab => (_nu, "lum_anue"),
        _ => (_nu, "meanE_anue"),
    };

    /// <summary>
    /// One run of individual events from <see cref="EventSampler"/> (seed = run number), drawn on the cumulative curve:
    /// dot k at (t_k, k × N). The expected count is checked first and the rate divided by N = ⌈count / 5,000⌉ when it
    /// exceeds <see cref="MaxDots"/>.
    /// </summary>
    private IReadOnlyList<PlotSeries> SampleDots(Detector instrument)
    {
        EventsPerDot = 1;
        if (!_nu.Applies(Outcome)) return Array.Empty<PlotSeries>();
        double end = _nu.ValidRange(Outcome).EndS;
        string cumulative = ChannelId.Compose("cumulative", instrument.Id), rate = ChannelId.Compose("rate", instrument.Id);
        double expected = SampleOne(_nu, cumulative, end);
        if (!(expected > 0)) return Array.Empty<PlotSeries>();
        int perDot = expected > MaxDots ? (int)Math.Ceiling(expected / MaxDots) : 1;
        EventsPerDot = perDot;

        var times = EventSampler.SampleTimes(t => SampleOne(_nu, rate, t) / perDot, 0, end, RunNumber);
        if (times.Count == 0) return Array.Empty<PlotSeries>();
        double z1 = Observer.TimeDilation;
        var obs = times.Select(t => Math.Max(t * z1, Core.Timeline.TimeAxis.MinS)).ToArray();
        var counts = Enumerable.Range(1, times.Count).Select(k => (double)k * perDot).ToArray();
        string word = Word(instrument);
        string label = perDot == 1
            ? $"Simulated {word}s (run #{RunNumber})"
            : $"Simulated {word}s (run #{RunNumber}; 1 dot = {perDot:N0} {word}s)";
        return new[] { new PlotSeries(label, obs, counts, _nu.Channels.First(c => c.Id == cumulative).Unit, true) { IsDots = true } };
    }

    private static string Word(Detector d) => d.Id == "icecube" ? "hit" : "event";

    private static string Unit(IReadOnlyList<PlotSeries> s, string fallback) => s.Count > 0 ? s[0].Unit : fallback;

    /// <summary>The neutronization burst peaks 5 ms after bounce with σ 2.5 ms: extra samples every 0.5 ms to 30 ms.</summary>
    private static IEnumerable<double> BurstTimes() => Enumerable.Range(1, 60).Select(i => i * 5e-4);

    /// <summary>Signals §1.3: every flavour stops when the black hole forms.</summary>
    private IReadOnlyList<ReferenceLine> BlackHoleLine()
        => Outcome.Remnant is { Kind: RemnantKind.BlackHole, FormationTimeS: { } t }
            ? new[] { new ReferenceLine("Black hole forms: the neutrinos stop", t.Value * Observer.TimeDilation, IsTime: true) }
            : Array.Empty<ReferenceLine>();
}
