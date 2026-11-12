using CommunityToolkit.Mvvm.ComponentModel;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Units;

namespace SupernovaLab.App.ViewModels;

/// <summary>
/// One displayed number with its confidence badge and source (global constraint: nothing is shown without one).
/// <see cref="Quantity"/> is the outcome's own Quantity, or one made for a value read off a drawn curve.
/// </summary>
public sealed record QuantityRow(string Group, string Label, string GlossaryKey, Quantity Quantity, string ValueText)
{
    public double Value => Quantity.Value;
    public string? RangeText => OutcomeTexts.FormatRange(Quantity);
    public Confidence Confidence => Quantity.Confidence;
    public bool Published => Quantity.Published;
    public string Citation => Quantity.Source.Citation;
    public string ResearchRef => Quantity.Source.ResearchRef;
    public string SourceText => $"{Citation} ({ResearchRef})";

    /// <summary>The confidence badge's tooltip: what the confidence means, then the source (a string, so it shows).</summary>
    public string BadgeToolTip => $"{ConfidenceText(Confidence)}{Environment.NewLine}{Environment.NewLine}Source: {SourceText}";

    /// <summary>The "Published" badge's tooltip: what a published value is, then its paper.</summary>
    public string PublishedToolTip => $"{Glossary.PublishedValue}{Environment.NewLine}{Environment.NewLine}Source: {SourceText}";

    /// <summary>
    /// <see cref="ValueText"/> split into its number and its unit, so the view can draw the unit (e.g. "M☉") at normal
    /// weight: the unit is the last word when it has no digit ("1.1 × 10⁵¹" + "erg"); otherwise there is no unit
    /// ("M_V −15.81").
    /// </summary>
    public string ValueNumber => SplitUnit(ValueText).Number;

    /// <inheritdoc cref="ValueNumber"/>
    public string ValueUnit => SplitUnit(ValueText).Unit;

    private static (string Number, string Unit) SplitUnit(string text)
    {
        // A unit is a last word without digits that follows a word with digits ("10⁵¹ erg"), not prose ("naked eye").
        int space = text.LastIndexOf(' ');
        if (space <= 0 || text[(space + 1)..].Any(char.IsDigit)) return (text, "");
        int before = text.LastIndexOf(' ', space - 1);
        if (!text[(before + 1)..space].Any(char.IsDigit)) return (text, "");
        return (text[..space], text[space..]);
    }

    /// <summary>
    /// When true, the row shares a source listed once under its heading (the drawn light curve's), so the details window
    /// shows a short pointer instead of repeating it; the badge tooltip still gives the full source.
    /// </summary>
    public bool SharedSource { get; init; }

    /// <summary>The details window's source line: the full source, or a pointer to where a shared source is given.</summary>
    public string SourceLine => Published ? "Published value: the source is given at the top of this window."
        : SharedSource ? "Source: given under this heading."
        : SourceText;

    private static string ConfidenceText(Confidence c) => c switch
    {
        Confidence.Established => Glossary.Established,
        Confidence.ModelDependent => Glossary.ModelDependent,
        _ => Glossary.Speculative,
    };

    public static QuantityRow From(OutcomeQuantity q) => new(q.Group, q.Label, q.GlossaryKey, q.Quantity, OutcomeTexts.Format(q.Quantity));
}

/// <summary>A run of the outcome map with one fate and remnant, from <see cref="Start"/> to <see cref="End"/> (0..1, log mass).</summary>
public sealed record MapSegment(double Start, double End, Fate Fate, RemnantKind Remnant, string ToolTip)
{
    public double Span => End - Start;
}

/// <summary>A fate shown on the outcome map, for its legend.</summary>
public sealed record MapLegendItem(Fate Fate, string Label)
{
    public string ToolTip => Glossary.Get(OutcomeTexts.FateGlossaryKey(Fate));
}

/// <summary>
/// The outcome summary, details and map (spec §3). <see cref="KeyNumbers"/> are three or four numbers with badges;
/// <see cref="DetailRows"/> every non-null quantity of the outcome; <see cref="CurveRows"/> values read off the drawn
/// light curve. Ruling R34: the stated peak brightness comes from LightCurveModel's drawn curve.
/// </summary>
public sealed partial class OutcomeViewModel : ObservableObject
{
    private static readonly LightCurveModel LightCurve = new();
    // The drawn curve's provenance (ruling R34) followed by the light-curve model's own literature sources.
    private static readonly Source DrawnCurveSource = new(
        "Read off the light curve drawn in the visible-light panel (LightCurveModel), not the engine's classification " +
        "estimate (ruling R34). The curve's sources: " + string.Join("; ", LightCurve.Sources.Select(s => s.Citation)),
        string.Join("; ", LightCurve.Sources.Select(s => s.ResearchRef).Distinct()));

    /// <summary>The drawn light curve's source, given once under its heading in the details window.</summary>
    public string CurveSourceText { get; } = $"Source: {DrawnCurveSource.Citation} ({DrawnCurveSource.ResearchRef})";

    private readonly OutcomeEngine _engine;
    private readonly LabViewModel _lab;
    private LabInputs? _mapTemplate;

    public OutcomeViewModel(OutcomeEngine engine, LabViewModel lab)
    {
        _engine = engine;
        _lab = lab;
    }

    [ObservableProperty] private string _typeText = "";
    [ObservableProperty] private string? _typeToolTip;
    [ObservableProperty] private string _fateText = "";
    [ObservableProperty] private string _fateToolTip = "";
    [ObservableProperty] private string _fateSeparator = "";
    [ObservableProperty] private string _remnantText = "";
    [ObservableProperty] private string? _remnantToolTip;
    [ObservableProperty] private string _summaryText = "";
    [ObservableProperty] private bool _usesPublishedValues;
    [ObservableProperty] private string? _publishedText;
    [ObservableProperty] private IReadOnlyList<QuantityRow> _keyNumbers = Array.Empty<QuantityRow>();
    [ObservableProperty] private IReadOnlyList<QuantityRow> _detailRows = Array.Empty<QuantityRow>();
    [ObservableProperty] private IReadOnlyList<QuantityRow> _curveRows = Array.Empty<QuantityRow>();
    [ObservableProperty] private IReadOnlyList<string> _notes = Array.Empty<string>();
    [ObservableProperty] private IReadOnlyList<string> _inputNotes = Array.Empty<string>();
    [ObservableProperty] private IReadOnlyList<MapSegment> _mapSegments = Array.Empty<MapSegment>();
    [ObservableProperty] private IReadOnlyList<MapLegendItem> _mapLegend = Array.Empty<MapLegendItem>();
    [ObservableProperty] private double _mapMarkerFraction;
    [ObservableProperty] private bool _isMapForThisStar = true;

    /// <summary>Refreshes every text and row for a new outcome or observer.</summary>
    internal void Update(Outcome o, ObserverSetup obs)
    {
        // Without a supernova type the fate is the headline ("Failed supernova"), and the line below names the remnant.
        bool typed = o.Type != SupernovaType.None;
        TypeText = OutcomeTexts.TypeName(o.Type) ?? OutcomeTexts.FateName(o.Fate);
        TypeToolTip = OutcomeTexts.TypeGlossaryKey(o.Type) is { } tk ? Glossary.Get(tk) : Glossary.Get(OutcomeTexts.FateGlossaryKey(o.Fate));
        FateText = typed ? OutcomeTexts.FateName(o.Fate) : "";
        FateSeparator = typed ? " · " : "";
        FateToolTip = Glossary.Get(OutcomeTexts.FateGlossaryKey(o.Fate));
        // The remnant's mass is a key number (with its badge), so the headline names only the kind.
        RemnantText = OutcomeTexts.RemnantName(o.Remnant.Kind);
        RemnantToolTip = OutcomeTexts.RemnantGlossaryKey(o.Remnant.Kind) is { } rk ? Glossary.Get(rk) : Glossary.Remnant;
        SummaryText = OutcomeTexts.Summary(o);
        UsesPublishedValues = o.UsesPublishedValues;
        PublishedText = o.UsesPublishedValues && _lab.Pins is { } pins
            ? $"Published values in use: {pins.Source.Citation} ({pins.Source.ResearchRef})"
            : null;

        var details = OutcomeTexts.Quantities(o).Select(QuantityRow.From).ToList();
        DetailRows = details;
        CurveRows = DrawnCurveRows(o, obs);
        KeyNumbers = ChooseKeyNumbers(o, details, CurveRows);
        Notes = o.Notes;
        InputNotes = o.Notes.Where(OutcomeTexts.IsInputNote).ToList();

        UpdateMap(o.Inputs);
    }

    // ---------------------------------------------------------------- the drawn light curve (ruling R34)

    /// <summary>The peak of the drawn V-band light curve: absolute, as seen from here, and when (observer time).</summary>
    private static IReadOnlyList<QuantityRow> DrawnCurveRows(Outcome o, ObserverSetup obs)
    {
        double tPeak = LightCurve.PeakTimeS(o);
        if (!double.IsFinite(tPeak)) return Array.Empty<QuantityRow>();
        double absV = LightCurve.Sample(o, obs, tPeak, "absV");
        double appV = LightCurve.Sample(o, obs, tPeak, "appV");
        if (!double.IsFinite(absV)) return Array.Empty<QuantityRow>();

        var conf = LightCurve.Confidence;
        const string group = "From the drawn light curve";
        var rows = new List<QuantityRow>
        {
            new(group, "Peak brightness (absolute V)", nameof(Glossary.PeakBrightness),
                new Quantity(absV, "mag", conf, DrawnCurveSource), $"M_V {OutcomeTexts.Magnitude(absV)}") { SharedSource = true },
        };
        if (double.IsFinite(appV))
            rows.Add(new(group, "Peak brightness seen from here (V, with dust)", nameof(Glossary.ApparentMagnitude),
                new Quantity(appV, "mag", conf, DrawnCurveSource),
                $"m_V {OutcomeTexts.Magnitude(appV)}: {Comparisons.ForApparentMagnitude(appV)}") { SharedSource = true });
        double tObs = tPeak * obs.TimeDilation;
        rows.Add(new(group, "Time of the peak, as seen from here", nameof(Glossary.ObserverTime),
            new Quantity(tObs, "s", conf, DrawnCurveSource), UnitFormat.Time(tObs)) { SharedSource = true });
        return rows;
    }

    /// <summary>
    /// Three or four key numbers for the summary card: for an explosion the energy, the ⁵⁶Ni mass, the drawn peak
    /// brightness and the remnant's mass; for a collapse without one, the black hole and the star; topped up from the
    /// details when fewer are available.
    /// </summary>
    private static IReadOnlyList<QuantityRow> ChooseKeyNumbers(Outcome o, IReadOnlyList<QuantityRow> details,
        IReadOnlyList<QuantityRow> curve)
    {
        QuantityRow? Find(Quantity? q) => q is null ? null : details.FirstOrDefault(r => ReferenceEquals(r.Quantity, q));
        QuantityRow? peak = curve.FirstOrDefault(r => r.GlossaryKey == nameof(Glossary.PeakBrightness));

        IEnumerable<QuantityRow?> preferred = o.Fate switch
        {
            Fate.Supernova or Fate.PairInstabilitySupernova => new[]
            {
                Find(o.ExplosionEnergyErg), Find(o.NickelMass), peak, Find(o.Remnant.Mass) ?? Find(o.EjectaMass),
            },
            Fate.FailedSupernova or Fate.PulsationalPairInstability => new[]
            {
                Find(o.Remnant.Mass), Find(o.Remnant.FormationTimeS), Find(o.Star?.FinalMass), Find(o.Star?.HydrogenEnvelopeMass),
            },
            Fate.WhiteDwarf => new[] { Find(o.Remnant.Mass), Find(o.Star?.FinalMass), Find(o.Star?.CoCoreMass) },
            _ => new[] { Find(o.Star?.FinalMass), Find(o.Star?.RadiusCm), Find(o.Star?.SurfaceTemperatureK) },
        };
        var chosen = preferred.OfType<QuantityRow>().Distinct().Take(4).ToList();
        foreach (var row in details)
        {
            if (chosen.Count >= 3) break;
            if (!chosen.Contains(row)) chosen.Add(row);
        }
        return chosen;
    }

    // ---------------------------------------------------------------- the outcome map (spec §3)

    private static readonly double LogMin = Math.Log(LabInputs.MinInitialMass), LogSpan = Math.Log(LabInputs.MaxInitialMass) - LogMin;

    /// <summary>Mass ticks under the map strip (M☉).</summary>
    public IReadOnlyList<AxisLabel> MapAxisLabels { get; } = new[] { 3.0, 10, 30, 100 }
        .Select(m => new AxisLabel(m.ToString(System.Globalization.CultureInfo.InvariantCulture), (Math.Log(m) - LogMin) / LogSpan))
        .ToList();

    /// <summary>Recomputes the strip only when an input other than the initial mass changed; always moves the marker.</summary>
    private void UpdateMap(LabInputs inputs)
    {
        IsMapForThisStar = inputs.Kind == StarKind.MassiveStar;
        MapMarkerFraction = Math.Clamp((Math.Log(inputs.InitialMass) - LogMin) / LogSpan, 0, 1);
        var template = inputs with { InitialMass = LabInputs.Default.InitialMass, Kind = StarKind.MassiveStar };
        if (template == _mapTemplate) return;
        _mapTemplate = template;

        var cells = OutcomeMap.Strip(_engine, template);
        int n = cells.Count;
        var segments = new List<MapSegment>();
        int start = 0;
        for (int i = 1; i <= n; i++)
        {
            if (i < n && cells[i].Fate == cells[start].Fate && cells[i].Remnant == cells[start].Remnant) continue;
            var first = cells[start];
            var last = cells[i - 1];
            double from = Math.Max(0, (start - 0.5) / (n - 1)), to = Math.Min(1, (i - 0.5) / (n - 1));
            string remnant = first.Remnant == RemnantKind.None ? "no remnant" : OutcomeTexts.RemnantName(first.Remnant).ToLowerInvariant();
            segments.Add(new MapSegment(from, to, first.Fate, first.Remnant,
                $"{UnitFormat.Mass(first.InitialMass)} – {UnitFormat.Mass(last.InitialMass)}: " +
                $"{OutcomeTexts.FateName(first.Fate)}, {remnant}"));
            start = i;
        }
        MapSegments = segments;
        MapLegend = segments.Select(s => s.Fate).Distinct().OrderBy(f => f)
            .Select(f => new MapLegendItem(f, OutcomeTexts.FateName(f))).ToList();
    }

    /// <summary>Clicking the strip at a position 0..1 sets the initial mass there (rounded to three figures).</summary>
    public void SetMassFromMapFraction(double fraction)
    {
        double m = Math.Exp(LogMin + Math.Clamp(fraction, 0, 1) * LogSpan);
        _lab.InitialMass = Math.Clamp(LabViewModel.RoundSig(m), LabInputs.MinInitialMass, LabInputs.MaxInitialMass);
    }
}
