using System.Globalization;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Units;

namespace SupernovaLab.App.ViewModels;

/// <summary>
/// Visible light (spec §5.1; signals §4, §10): the light curve in apparent V magnitude (brighter up), with dust and the
/// naked-eye line at +6, the GRB's optical afterglow when there is one, and a schematic spectrum at the current time.
/// Instruments: the naked eye, ZTF, Rubin, a typical spectrograph. The peak and current magnitudes come with a plain
/// comparison (Comparisons.ForApparentMagnitude); the peak is the drawn curve's (ruling R34).
/// </summary>
public sealed class VisiblePanelViewModel : InstrumentPanelViewModel
{
    public const string LightCurveTab = "Light curve", SpectrumTab = "Schematic spectrum";

    /// <summary>The naked-eye limit drawn on the light curve (spec §5.1; signals §4.7, ≈ 6 [APPROX]).</summary>
    public const double NakedEyeLimit = 6;

    /// <summary>The spectrum is recomputed when the time has moved by more than this factor (it changes over days).</summary>
    private const double SpectrumTimeStep = 1.01;

    private readonly LightCurveModel _lc = new();
    private readonly GrbModel _grb = new();
    private readonly SpectrumModel _spectrum = new();
    private double _spectrumTimeS = double.NaN;

    public VisiblePanelViewModel()
        : base(new[] { LightCurveTab, SpectrumTab }, LightCurveModel.DetectorIds.Select(Detectors.Get).ToList()) { }

    public override Messenger Messenger => Messenger.VisibleLight;
    public override string Title => "Visible light";
    public override string TitleToolTip => Glossary.ApparentMagnitude;
    public override bool MainSeriesInvertY => true;

    protected override IReadOnlyList<PlotSeries> BuildMainSeries() => SampleChannel(_lc, "appV");

    protected override PanelContent BuildTab(string subTab, Detector instrument)
    {
        if (subTab == SpectrumTab) return new PanelContent { Kind = PlotKind.Spectrum, YLabel = "Relative flux" };

        var series = SampleChannel(_lc, "appV", "Supernova (V, with dust)").ToList();
        if (_grb.Applies(Outcome)) series.AddRange(SampleChannel(_grb, "afterglowOpt", "GRB optical afterglow (AB at 550 nm)"));
        var lines = new List<ReferenceLine> { new("Naked eye (dark sky), V 6", NakedEyeLimit, IsTime: false) };
        if (instrument.Id != "eye")
            lines.Add(LimitLine(instrument, instrument.Threshold,
                $"limit, {instrument.Threshold.ToString("0.#", CultureInfo.InvariantCulture)}"));
        return new PanelContent
        {
            Series = series, Lines = lines, InvertY = true, YLabel = "Apparent V magnitude (brighter up)",
            Note = Outcome.Type == SupernovaType.IIn || Outcome.Type == SupernovaType.Ibn || Outcome.Type == SupernovaType.Icn
                ? "The dense gas around the star is not in the light-curve model (see the badge's reason)."
                : null,
        };
    }

    /// <summary>The light curve tab also draws the GRB's optical afterglow (when there is one), against the same limits.</summary>
    protected override IReadOnlyList<ISignalModel> TabModels(string subTab)
        => subTab == LightCurveTab && HasOutcome && _grb.Applies(Outcome) ? new ISignalModel[] { _lc, _grb } : new ISignalModel[] { _lc };

    /// <summary>Ruling R50: GrbModel judges the drawn optical afterglow against the same V limits.</summary>
    protected override IReadOnlyList<ISignalModel> DetectorModels(Detector instrument) => new ISignalModel[] { _lc, _grb };

    protected override (ISignalModel Model, string ChannelId)? PrimaryChannel(string subTab, Detector instrument) => (_lc, "appV");

    protected override Confidence TabConfidence(string subTab)
        => subTab == SpectrumTab ? _spectrum.Confidence : base.TabConfidence(subTab);

    protected override IEnumerable<Source> TabSources(string subTab)
        => subTab == SpectrumTab ? _spectrum.Sources : base.TabSources(subTab);

    /// <summary>The schematic spectrum at the current time (SpectrumModel.At), when that sub-tab is shown.</summary>
    protected override void RefreshInstantView(bool force)
    {
        if (SelectedSubTab != SpectrumTab)
        {
            Spectrum = null;
            _spectrumTimeS = double.NaN;
            return;
        }
        double t = SourceTimeS;
        if (!force && double.IsFinite(_spectrumTimeS) && t / _spectrumTimeS < SpectrumTimeStep && _spectrumTimeS / t < SpectrumTimeStep)
            return;
        _spectrumTimeS = t;
        Spectrum s;
        try
        {
            s = _spectrum.At(Outcome, t);
        }
        catch (Exception e)
        {
            throw new ModelErrorException("spectrum", $"{e.GetType().Name}: {e.Message}", e);
        }
        if (s.WavelengthsAngstrom.Length == 0)
        {
            Spectrum = null;
            return;
        }
        if (s.RelativeFlux.Any(double.IsInfinity)) throw new ModelErrorException("spectrum", "the flux is infinite", null);
        var inv = CultureInfo.InvariantCulture;
        bool interacting = Outcome.Type is SupernovaType.IIn or SupernovaType.Ibn or SupernovaType.Icn;
        string velocity = interacting
            ? $"the underlying ejecta move at about {s.PhotosphereVelocityKmS.ToString("N0", inv)} km/s, which the narrow lines from the gas around the star do not show"
            : $"the lines are blueshifted by about {s.PhotosphereVelocityKmS.ToString("N0", inv)} km/s";
        Spectrum = new SpectrumView(s.WavelengthsAngstrom, s.RelativeFlux, s.Lines, s.PhotosphereVelocityKmS,
            $"Schematic spectrum at {UnitFormat.Time(TimeS)}: a blackbody at {s.ColorTemperatureK.ToString("N0", inv)} K with " +
            $"the identifying lines{(s.Nebular ? " of the nebular phase" : "")}; {velocity}. Drawn from the line list, not a " +
            $"measured spectrum; line strengths are approximate. {SpectrumModel.VelocityCaption}");
    }

    protected override string? InstantViewNote()
        => Spectrum is null
            ? "No schematic spectrum now: it is drawn from 1 day to 1 year after the explosion, for outcomes with a modeled " +
              "light curve."
            : null;

    /// <summary>The peak (from the drawn curve, R34) and the current magnitude, each with its plain comparison.</summary>
    protected override string? BuildSummary()
    {
        var inv = CultureInfo.InvariantCulture;
        double tp = _lc.PeakTimeS(Outcome);
        string peak = "";
        if (double.IsFinite(tp))
        {
            double m = SampleOne(_lc, "appV", tp);
            if (double.IsFinite(m))
                peak = $"Peak V {m.ToString("0.0", inv)} at {UnitFormat.Time(tp * Observer.TimeDilation)} " +
                       $"({Comparisons.ForApparentMagnitude(m)}). ";
        }
        double now = SampleOne(_lc, "appV", SourceTimeS);
        string current = double.IsFinite(now)
            ? $"Now V {now.ToString("0.0", inv)} ({Comparisons.ForApparentMagnitude(now)})."
            : "Now: no modeled visible light.";
        return peak + current;
    }
}
