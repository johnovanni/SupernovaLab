using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Core.Units;

namespace SupernovaLab.App.ViewModels;

/// <summary>An event on the timeline (spec §4), at an observer time, explained by a glossary term.</summary>
public sealed record EventMarker(string Label, double ObserverTimeS, string GlossaryKey)
{
    /// <summary>Position 0..1 on the log timeline.</summary>
    public double Fraction => TimeAxis.ToFraction(ObserverTimeS);

    /// <summary>"Peak brightness: 18.3 days", for the tooltip's first line.</summary>
    public string Caption => $"{Label}: {UnitFormat.Time(ObserverTimeS)}";

    /// <summary>Where the time comes from and how firm it is (every number shown carries its confidence and source).</summary>
    public string Basis { get; init; } = "";

    /// <summary>The caption, the basis of the time and the glossary text, for the tooltip.</summary>
    public string ToolTip => $"{Caption}{Environment.NewLine}{Basis}{Environment.NewLine}{Environment.NewLine}{Glossary.Get(GlossaryKey)}";
}

/// <summary>
/// The timeline's event markers (spec §4) from the models' marker methods, in observer time (source time × (1 + z)). A
/// marker whose model does not apply (NaN, null, +∞ or off the axis) is omitted. The neutrino burst and gravitational
/// waves are fixed at 0.01 s after bounce; the gamma-ray-line and radio peaks are the maxima of <c>line847</c> and
/// <c>radio5</c> on the 400-point grid.
/// </summary>
public static class EventMarkers
{
    public const double NeutrinoAndGwTimeS = 0.01;

    private static readonly NeutrinoModel Neutrinos = new();
    private static readonly GravitationalWaveModel Gw = new();
    private static readonly ShockBreakoutModel Breakout = new();
    private static readonly GrbModel Grb = new();
    private static readonly LightCurveModel LightCurve = new();
    private static readonly GammaLineModel GammaLines = new();
    private static readonly CsmInteractionModel Csm = new();
    private static readonly PulsarModel Pulsar = new();
    private static readonly RemnantEvolutionModel RemnantEvolution = new();

    /// <summary>The markers for this outcome and observer, sorted by time.</summary>
    public static IReadOnlyList<EventMarker> For(Outcome o, ObserverSetup obs)
    {
        double z1 = obs.TimeDilation;
        var list = new List<EventMarker>();
        void Add(string label, double? sourceTimeS, string key, string basis)
        {
            if (sourceTimeS is not double t || !double.IsFinite(t)) return;
            double observed = t * z1;
            if (observed >= TimeAxis.MinS && observed <= TimeAxis.MaxS)
                list.Add(new EventMarker(label, observed, key) { Basis = basis });
        }

        const string fixedStart = "Placed at 10 ms after core bounce to mark the start of the signal.";
        if (Neutrinos.Applies(o)) Add("Neutrino burst", NeutrinoAndGwTimeS, nameof(Glossary.NeutrinoBurst), fixedStart);
        if (Gw.Applies(o)) Add("Gravitational waves", NeutrinoAndGwTimeS, nameof(Glossary.GravitationalWaves), fixedStart);
        if (o.Remnant is { Kind: RemnantKind.BlackHole, FormationTimeS: { } tBh })
            Add("Black hole forms", tBh.Value, nameof(Glossary.BlackHoleForms), Basis(tBh.Confidence, tBh.Source.ResearchRef));
        Add("Shock breakout", Breakout.BreakoutTimeS(o), nameof(Glossary.ShockBreakout), Basis(Breakout));
        // The burst's marker sits at its prompt peak; T90 (its duration) is NaN exactly when the outcome has no burst.
        if (!double.IsNaN(Grb.T90S(o))) Add("Gamma-ray burst", Grb.PeakTimeS(o), nameof(Glossary.GammaRayBurst), Basis(Grb));
        Add("Peak brightness", LightCurve.PeakTimeS(o), nameof(Glossary.PeakBrightness), Basis(LightCurve));
        Add("End of plateau", LightCurve.PlateauEndS(o), nameof(Glossary.PlateauEnd), Basis(LightCurve));
        if (GammaLines.Applies(o))
            Add("Gamma-ray lines peak", GridMaximum(GammaLines, o, obs, "line847"), nameof(Glossary.GammaRayLines), Basis(GammaLines));
        if (Csm.Applies(o)) Add("Radio peak", GridMaximum(Csm, o, obs, "radio5"), nameof(Glossary.RadioPeak), Basis(Csm));
        Add("Pulsar emerges", Pulsar.EmergenceTimeS(o), nameof(Glossary.PulsarEmerges), Basis(Pulsar));
        Add("Remnant resolvable", RemnantEvolution.ResolvableTimeS(o, obs), nameof(Glossary.RemnantResolvable),
            Basis(RemnantEvolution));

        return list.OrderBy(m => m.ObserverTimeS).ToList();
    }

    /// <summary>"Speculative: from the light-curve model (signals §4.1, …)".</summary>
    private static string Basis(ISignalModel model)
        => Basis(model.Confidence, string.Join("; ", model.Sources.Select(s => s.ResearchRef).Distinct().Take(3)));

    private static string Basis(Confidence confidence, string researchRef)
    {
        string word = confidence switch
        {
            Confidence.Established => "Established",
            Confidence.ModelDependent => "Model-dependent",
            _ => "Speculative",
        };
        return $"{word}: from the lab's model ({researchRef}).";
    }

    /// <summary>
    /// The source time of the channel's largest finite, positive value on the 400-point observer grid, or null when it
    /// has none.
    /// </summary>
    private static double? GridMaximum(ISignalModel model, Outcome o, ObserverSetup obs, string channel)
    {
        double best = 0;
        double? at = null;
        foreach (double tObs in TimeAxis.LogGrid())
        {
            double t = TimeAxis.ToSource(tObs, obs);
            double v = model.Sample(o, obs, t, channel);
            if (double.IsFinite(v) && v > best)
            {
                best = v;
                at = t;
            }
        }
        return at;
    }
}
