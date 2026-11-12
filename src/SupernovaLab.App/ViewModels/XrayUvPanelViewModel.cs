using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Core.Units;

namespace SupernovaLab.App.ViewModels;

/// <summary>
/// X-rays and ultraviolet (spec §5.1; signals §3, §6.2, §8.4, §9.3): the shock-breakout flash and cooling, the X-rays of
/// the shock running into the star's old wind (and SN 1987A's ring), the GRB's X-ray afterglow, and the neutron star's
/// cooling surface. Instruments: Swift XRT, Einstein Probe, ULTRASAT, Chandra, XMM-Newton.
/// <para>The circumstellar model's Messenger is Radio, but its X-ray channels (lx, fluxX and ringLx) belong here: this
/// panel picks them by model and channel, not by Messenger.</para>
/// </summary>
public sealed class XrayUvPanelViewModel : InstrumentPanelViewModel
{
    public const string BreakoutTab = "Shock breakout", CsmTab = "Shock in the wind", AfterglowTab = "GRB afterglow",
        CoolingTab = "Neutron star cooling";

    /// <summary>The badge when an X-ray instrument's first exposure after breakout is still collecting photons (R29).</summary>
    public const string ExposureFillingText = "Not yet: first exposure still filling";

    private readonly ShockBreakoutModel _breakout = new();
    private readonly CsmInteractionModel _csm = new();
    private readonly GrbModel _grb = new();
    private readonly NeutronStarCoolingModel _cooling = new();

    public XrayUvPanelViewModel()
        : base(new[] { BreakoutTab, CsmTab, AfterglowTab, CoolingTab },
            new[] { "swift-xrt", "einstein-probe", "ultrasat", "chandra", "xmm" }.Select(Detectors.Get).ToList()) { }

    public override Messenger Messenger => Messenger.XRaysUv;
    public override string Title => "X-rays & UV";
    public override string TitleToolTip => Glossary.ShockBreakout;

    protected override IReadOnlyList<PlotSeries> BuildMainSeries() => SampleChannel(_breakout, "lum", extraSourceTimesS: FlashTimes());

    protected override PanelContent BuildTab(string subTab, Detector instrument)
    {
        switch (subTab)
        {
            case BreakoutTab:
                return new PanelContent
                {
                    Series = SampleChannel(_breakout, "lum", extraSourceTimesS: FlashTimes()), LogY = true,
                    YLabel = "Luminosity, all wavelengths (erg/s)",
                    Note = "The flash as the shock reaches the surface, then the cooling glow. The curve is the total " +
                           "luminosity; the badge judges the selected instrument's own band.",
                };
            case CsmTab:
                var csm = SampleChannel(_csm, "lx").ToList();
                if (Outcome.HasEquatorialRing) csm.AddRange(SampleChannel(_csm, "ringLx"));
                return new PanelContent
                {
                    Series = csm, LogY = true, YLabel = "X-ray luminosity (erg/s)",
                    Note = "The shock running into the star's old wind (0.3–10 keV)" +
                           (Outcome.HasEquatorialRing ? ", and SN 1987A's ring as Chandra measured it (0.5–2 keV)" : "") +
                           ". The badge compares the flux after the gas in the way with the instrument's limit.",
                };
            case AfterglowTab:
                return new PanelContent
                {
                    Series = SampleChannel(_grb, "afterglowX"), LogY = true, YLabel = "X-ray flux, 0.3–10 keV (erg cm⁻² s⁻¹)",
                    Lines = instrument.Id is "swift-xrt" or "chandra" or "xmm"
                        ? new[] { LimitLine(instrument, instrument.Threshold, "limit (0.3–10 keV)") }
                        : Array.Empty<ReferenceLine>(),
                    Note = "Drawn only where the forward-shock formulas apply (ruling R39).",
                };
            default:
                return CoolingContent();
        }
    }

    protected override IReadOnlyList<ISignalModel> TabModels(string subTab) => subTab switch
    {
        BreakoutTab => new ISignalModel[] { _breakout },
        CsmTab => new ISignalModel[] { _csm },
        AfterglowTab => new ISignalModel[] { _grb },
        _ => new ISignalModel[] { _cooling },
    };

    protected override IReadOnlyList<ISignalModel> DetectorModels(Detector instrument) => instrument.Id switch
    {
        "swift-xrt" => new ISignalModel[] { _breakout, _csm },
        "einstein-probe" or "ultrasat" => new ISignalModel[] { _breakout },
        _ => new ISignalModel[] { _csm, _cooling },
    };

    protected override (ISignalModel Model, string ChannelId)? PrimaryChannel(string subTab, Detector instrument) => subTab switch
    {
        BreakoutTab => (_breakout, "lum"),
        CsmTab => (_csm, "lx"),
        AfterglowTab => (_grb, "afterglowX"),
        _ => (_cooling, "tempK"),
    };

    /// <summary>
    /// Ruling R29: after breakout, an X-ray instrument's NotYet means its first exposure is still filling, not that the
    /// signal has yet to start.
    /// </summary>
    protected override (string Full, string Short) Badge(Detection d)
    {
        if (d.Status == DetectionStatus.NotYet && SelectedInstrument.Id is "swift-xrt" or "einstein-probe"
            && _breakout.ValidRange(Outcome) is var (start, end) && end > start && SourceTimeS >= start && SourceTimeS <= end)
            return (ExposureFillingText, "Exposure filling");
        return base.Badge(d);
    }

    /// <summary>The afterglow's reasons are the GRB model's own (R39), which no X-ray instrument here asks for.</summary>
    protected override string NotDrawnReason(ISignalModel model)
    {
        if (!ReferenceEquals(model, _grb)) return base.NotDrawnReason(model);
        if (!_grb.Applies(Outcome)) return "This supernova makes no gamma-ray burst, so there is no afterglow.";
        if (!Observer.BeamTowardEarth)
            return "The jets point away from Earth, so no afterglow is drawn (a late \"orphan\" afterglow is not modeled).";
        if (_grb.AfterglowNotModeledReason(Outcome, Observer, SourceTimeS) is { } reason) return reason;
        double onset = _grb.OnsetS(Outcome) * Observer.TimeDilation;
        return TimeS < onset + _grb.T90S(Outcome) * Observer.TimeDilation
            ? "The afterglow is drawn once the prompt burst is over."
            : $"The afterglow is drawn to {UnitFormat.Time(GrbModel.AfterglowEndS * Observer.TimeDilation)} after the burst.";
    }

    /// <summary>
    /// Signals §8.4: the surface temperature, as a band before about 100 yr (from
    /// <see cref="NeutronStarCoolingModel.SurfaceTemperatureAt"/>'s low and high values), then a line.
    /// </summary>
    private PanelContent CoolingContent()
    {
        var line = SampleChannel(_cooling, "tempK");
        var bands = new List<PlotBand>();
        if (_cooling.Applies(Outcome))
        {
            double z1 = Observer.TimeDilation;
            var times = TimeAxis.LogGrid().Where(t => t / z1 >= NeutronStarCoolingModel.StartS && t / z1 <= NeutronStarCoolingModel.RelaxedS)
                .ToList();
            var low = new List<double>();
            var high = new List<double>();
            var used = new List<double>();
            foreach (double t in times)
            {
                var q = _cooling.SurfaceTemperatureAt(Outcome, t / z1);
                if (q is { Low: double lo, High: double hi } && double.IsFinite(lo) && double.IsFinite(hi))
                {
                    used.Add(t);
                    low.Add(lo);
                    high.Add(hi);
                }
            }
            if (used.Count > 1)
                bands.Add(new PlotBand("Before ~100 yr: depends on the young crust", used.ToArray(), low.ToArray(), high.ToArray(), "K"));
        }
        return new PanelContent
        {
            Series = line, Bands = bands, LogY = true, YLabel = "Surface temperature seen from afar (K)",
            Note = "Slow (modified Urca) cooling. Before about 100 years the surface depends on the young crust, so it is a " +
                   "band (signals §8.4); fast cooling of heavy neutron stars is not modeled.",
        };
    }

    /// <summary>Extra samples through the flash: breakout plus 0.1 ms to the end of the cooling model, log-spaced.</summary>
    private IEnumerable<double> FlashTimes()
    {
        var (start, end) = _breakout.ValidRange(Outcome);
        if (!(end > start)) return Array.Empty<double>();
        double span = end - start;
        return Enumerable.Range(0, 160).Select(i => start + 1e-4 * Math.Pow(span / 1e-4, i / 159.0));
    }
}
