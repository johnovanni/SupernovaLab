using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Text;

namespace SupernovaLab.App.ViewModels;

/// <summary>
/// Gamma rays (spec §5.1; signals §5, §9.1): the gamma-ray burst's prompt emission, the ⁵⁶Ni 158/812 keV and ⁵⁶Co
/// 847/1238 keV lines, and the ⁴⁴Ti lines over centuries. Instruments: Fermi GBM, Swift BAT, INTEGRAL/SPI, COSI, NuSTAR.
/// Until the user picks a view, the panel shows the burst (with GBM) when the outcome has one, else the lines (with SPI),
/// so the tile's badge speaks of the same signal as its sparkline.
/// </summary>
public sealed class GammaPanelViewModel : InstrumentPanelViewModel
{
    public const string GrbTab = "Gamma-ray burst", LinesTab = "⁵⁶Ni and ⁵⁶Co lines", TitaniumTab = "⁴⁴Ti lines";

    private readonly GrbModel _grb = new();
    private readonly GammaLineModel _lines = new();
    private bool _userChose;

    public GammaPanelViewModel()
        : base(new[] { GrbTab, LinesTab, TitaniumTab },
            GrbModel.DetectorIds.Concat(GammaLineModel.DetectorIds).Select(Detectors.Get).ToList())
    {
        Preselect(LinesTab, Detectors.Get("spi"));
    }

    public override Messenger Messenger => Messenger.GammaRays;
    public override string Title => "Gamma rays";
    public override string TitleToolTip => Glossary.GammaRayLines;

    protected override void OnUserChoice() => _userChose = true;

    protected override void OnContextChanged()
    {
        if (_userChose) return;
        if (_grb.Applies(Outcome)) Preselect(GrbTab, Detectors.Get("gbm"));
        else Preselect(LinesTab, Detectors.Get("spi"));
    }

    /// <summary>The tile shows the prompt burst when the outcome has one, else the 847 keV line.</summary>
    protected override IReadOnlyList<PlotSeries> BuildMainSeries()
        => Outcome.HasGrb ? SampleChannel(_grb, "prompt", extraSourceTimesS: BurstTimes()) : SampleChannel(_lines, "line847");

    protected override PanelContent BuildTab(string subTab, Detector instrument)
    {
        switch (subTab)
        {
            case GrbTab:
                return new PanelContent
                {
                    Series = SampleChannel(_grb, "prompt", extraSourceTimesS: BurstTimes()), LogY = true,
                    YLabel = "Energy flux (erg cm⁻² s⁻¹)",
                    Note = "The bolometric flux (1–10⁴ keV in the burst's frame). GBM and BAT trigger on their own bands, which " +
                           "the badge uses; their limits are therefore not drawn here.",
                };
            case LinesTab:
                return new PanelContent
                {
                    Series = new[] { "line158", "line812", "line847", "line1238" }.SelectMany(c => SampleChannel(_lines, c)).ToList(),
                    Lines = LineLimit(instrument, "line847"), LogY = true, YLabel = "Line flux (photons cm⁻² s⁻¹)",
                    Note = LimitNote(instrument, ti44: false),
                };
            default:
                return new PanelContent
                {
                    Series = new[] { "ti44_68", "ti44_78", "ti44_1157" }.SelectMany(c => SampleChannel(_lines, c)).ToList(),
                    Lines = LineLimit(instrument, instrument.Id == "nustar" ? "ti44_68" : "ti44_1157"), LogY = true,
                    YLabel = "Line flux (photons cm⁻² s⁻¹)",
                    Note = LimitNote(instrument, ti44: true),
                };
        }
    }

    protected override IReadOnlyList<ISignalModel> TabModels(string subTab)
        => subTab == GrbTab ? new ISignalModel[] { _grb } : new ISignalModel[] { _lines };

    protected override IReadOnlyList<ISignalModel> DetectorModels(Detector instrument)
        => GrbModel.DetectorIds.Contains(instrument.Id) ? new ISignalModel[] { _grb } : new ISignalModel[] { _lines };

    /// <summary>
    /// Ruling R50: GBM and BAT trigger on the prompt burst. Once it is over (the drawn prompt curve ends three T90 after
    /// onset), their verdict is a past event: "Over", with whether it triggered, never a current "Detected". The trigger
    /// is judged at the end of the prompt burst, not now, so the verdict stands after the model's own end (Task 15
    /// re-review N1).
    /// </summary>
    protected override Detection VerdictNow(ISignalModel model, Detector instrument, double sourceTimeS)
    {
        if (!ReferenceEquals(model, _grb) || !GrbModel.DetectorIds.Contains(instrument.Id) || !BurstIsOver(sourceTimeS))
            return base.VerdictNow(model, instrument, sourceTimeS);
        var d = TriggerAtPromptEnd(model, instrument);
        if (d.Status is not (DetectionStatus.Detected or DetectionStatus.TooFaint))
            return d;
        return new Detection(DetectionStatus.NotModeled,
            $"The burst is over: it lasted about {Core.Units.UnitFormat.Time(_grb.T90S(Outcome) * Observer.TimeDilation)} (T90). " +
            $"At its peak, {LowerFirst(d.Reason)}");
    }

    /// <summary>The text with its first letter in lower case; empty stays empty (Task 15 re-review N2).</summary>
    public static string LowerFirst(string text)
        => string.IsNullOrEmpty(text) ? "" : char.ToLowerInvariant(text[0]) + text[1..];

    protected override (string Full, string Short) Badge(ISignalModel model, Detection d)
    {
        if (ReferenceEquals(model, _grb) && GrbModel.DetectorIds.Contains(SelectedInstrument.Id) && BurstIsOver(SourceTimeS)
            && d.Status == DetectionStatus.NotModeled && Observer.BeamTowardEarth)
            return OverWords(TriggerAtPromptEnd(model, SelectedInstrument).Status == DetectionStatus.Detected);
        return base.Badge(model, d);
    }

    /// <summary>
    /// The trigger verdict at the burst's peak (Task 15 re-review N1; R52 review M7: the restated text says "At its peak",
    /// so it is judged there, whatever the model does later).
    /// </summary>
    private Detection TriggerAtPromptEnd(ISignalModel model, Detector instrument)
        => base.VerdictNow(model, instrument, _grb.PeakTimeS(Outcome));

    private bool BurstIsOver(double sourceTimeS) => _grb.PromptEndS(Outcome) is var end && double.IsFinite(end) && sourceTimeS > end;

    protected override (ISignalModel Model, string ChannelId)? PrimaryChannel(string subTab, Detector instrument) => subTab switch
    {
        GrbTab => (_grb, "prompt"),
        LinesTab => (_lines, "line847"),
        _ => (_lines, "ti44_68"),
    };

    /// <summary>
    /// The line instrument's 3σ limit for this view's main line, as the badge uses it once the observing window is full:
    /// scaled to the default window and raised for the line's width (ruling R52; R52 review M1), so a curve above the
    /// line is one the badge can call detected. Nothing when the instrument does not see that line.
    /// </summary>
    private IReadOnlyList<ReferenceLine> LineLimit(Detector d, string lineChannel)
    {
        double limit = GammaLineModel.DrawnLimit(Outcome, d.Id, lineChannel);
        if (!double.IsFinite(limit)) return Array.Empty<ReferenceLine>();
        string line = _lines.Channels.First(c => c.Id == lineChannel).Label.Replace(" line flux", "");
        string window = Core.Units.UnitFormat.Time(GammaLineModel.ObservingWindowS(d.Id, lineChannel.StartsWith("ti44")));
        double width = GammaLineModel.LineWidthFactor(d.Id, GammaLineModel.LineKeV(lineChannel), GammaLineModel.LineFwhmKmS(Outcome));
        string broad = width > 1
            ? $", raised {width.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}× for the broad line"
            : "";
        return new[] { LimitLine(d, limit, $"limit for {line} ({window} average{broad})") };
    }

    private static string? LimitNote(Detector d, bool ti44)
    {
        if (!GammaLineModel.DetectorIds.Contains(d.Id))
            return $"{d.Name} watches for bursts, not lines; pick INTEGRAL/SPI, COSI or NuSTAR to see a line limit.";
        if (d.Id == "nustar" && !ti44) return "NuSTAR sees only the ⁴⁴Ti lines at 68 and 78 keV.";
        return "The dashed limit is for the main line averaged over the instrument's observing window, raised for the line's " +
               "width: a supernova's expanding ejecta broaden its lines, so they stand on more background (several-fold " +
               "for INTEGRAL/SPI). The badge compares the line's average over that window with this limit.";
    }

    /// <summary>Extra samples through the prompt burst: onset plus 1 ms to 300 s, log-spaced.</summary>
    private IEnumerable<double> BurstTimes()
    {
        double onset = _grb.OnsetS(Outcome);
        if (!double.IsFinite(onset)) return Array.Empty<double>();
        return Enumerable.Range(0, 120).Select(i => onset + 1e-3 * Math.Pow(3e5, i / 119.0));
    }
}
