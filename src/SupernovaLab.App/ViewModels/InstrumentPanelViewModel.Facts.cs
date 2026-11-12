using System.Globalization;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.App.ViewModels;

/// <summary>The preset's measurements on a panel: markers, levels and one text row per fact (none dropped silently).</summary>
public abstract partial class InstrumentPanelViewModel
{
    /// <summary>
    /// An untimed "Observed" level is drawn only within this many decades (log axis) or magnitudes (linear axis) of the
    /// drawn curves, as <c>PanelPlot</c> draws it; farther away it would flatten the curves, so it is a text row only.
    /// </summary>
    public const double LevelSlackDecades = 3, LevelSlackLinear = 8;

    /// <summary>
    /// Where each of this messenger's facts goes: a marker on a drawn curve of its channel at its time, a labelled level
    /// when it has no time (if it lies near the curves), and always exactly one text row, flagged ◆ when it is drawn and
    /// otherwise carrying the lab's own value where there is one. Without the lab's values (after a model error) the rows
    /// are the plain descriptions.
    /// </summary>
    private (List<FactMarker> Markers, List<ReferenceLine> Levels, List<FactNote> Notes) PlaceFacts(PanelContent content,
        bool withLabValues)
    {
        var markers = new List<FactMarker>();
        var levels = new List<ReferenceLine>();
        var notes = new List<FactNote>();
        if (Facts.Count > 0 && !_factsMeasuredHere)
            notes.Add(new FactNote(
                "The observer has been moved from where this supernova was really seen, so its measurements (made from " +
                "Earth) are listed but not marked on the plot.",
                "Ruling R49: an observer edit keeps the preset's explosion values; its measurements belong to the real distance and dust."));

        var drawn = content.Series;
        double Y(double v) => content.LogY ? Math.Log10(v) : v;
        var ys = drawn.SelectMany(s => s.Values).Select(Y).Where(double.IsFinite).ToList();
        double slack = content.LogY ? LevelSlackDecades : LevelSlackLinear;

        foreach (var f in Facts)
        {
            var series = drawn.FirstOrDefault(s => s.ChannelId == f.ChannelId && s.Unit == f.Unit);
            bool marked = false;
            string farNote = "";
            if (_factsMeasuredHere && series is not null)
            {
                string value = FormatValue(f.Value, f.Unit);
                string label = f.IsUpperLimit ? $"Upper limit: < {value}" : $"Observed: {value}";
                if (f.SourceTimeS is double t)
                {
                    markers.Add(new FactMarker(f, t * _observer.TimeDilation, label, f.SourceTimeEndS * _observer.TimeDilation));
                    marked = true;
                }
                else if (ys.Count > 0 && Y(f.Value) >= ys.Min() - slack && Y(f.Value) <= ys.Max() + slack)
                {
                    levels.Add(new ReferenceLine(label, f.Value, IsTime: false));
                    marked = true;
                }
                else farNote = " (Not drawn: it lies far outside the lab's curve.)";
            }
            string lab = marked || !withLabValues ? "" : LabComparison(f);
            notes.Add(new FactNote($"{(marked ? "◆ " : "")}{f.Description}{lab}{farNote}",
                $"{f.Source.Citation} ({f.Source.ResearchRef})"));
        }
        return (markers, levels, notes);
    }

    /// <summary>
    /// The lab's own value beside a fact that is not marked: a channel of one of this panel's models at the fact's time,
    /// a detector the lab models (Kamiokande-II), or an outcome member (⁴⁴Ti mass).
    /// </summary>
    private string LabComparison(ObservedFact f)
    {
        var model = SubTabs.SelectMany(TabModels).Distinct().FirstOrDefault(m => m.Channels.Any(c => c.Id == f.ChannelId));
        if (model is not null)
        {
            var channel = model.Channels.First(c => c.Id == f.ChannelId);
            if (f.SourceTimeS is not double t) return "";
            if (f.SourceTimeEndS is double end)
            {
                // A span: the lab's mean over it (17 evenly spaced samples, trapezoid rule).
                var vs = Enumerable.Range(0, 17).Select(i => SampleOne(model, f.ChannelId, t + (end - t) * i / 16.0)).ToList();
                double mean = (vs.Sum() - 0.5 * (vs[0] + vs[^1])) / 16;
                return double.IsFinite(mean) && channel.Unit == f.Unit
                    ? $" The lab averages {FormatValue(mean, channel.Unit)} over that time."
                    : "";
            }
            double v = SampleOne(model, f.ChannelId, t);
            return double.IsFinite(v) && channel.Unit == f.Unit
                ? $" The lab gives {FormatValue(v, channel.Unit)} then."
                : "";
        }
        if (f.ChannelId == "cumulative@kamiokande-ii" && Messenger == Messenger.Neutrinos)
            return $" The lab expects about {KamiokandeIIExpected().ToString("0.#", CultureInfo.InvariantCulture)} " +
                   "Kamiokande-II events here.";
        if (f.ChannelId is "cumulative@imb" or "cumulative@baksan")
            return " (The lab has no model of this detector.)";
        if (f.ChannelId == nameof(Core.Outcomes.Outcome.Ti44Mass))
            return Outcome.Ti44Mass is { } ti
                ? $" The lab uses {UnitFormat.Scientific(ti.Value, "M☉")}{(ti.Published ? " (the published value)" : "")}."
                : " The lab makes no ⁴⁴Ti for this outcome.";
        return "";
    }

    /// <summary>The lab's Kamiokande-II expectation (a model call, so a failure is a model error of this panel).</summary>
    private double KamiokandeIIExpected()
    {
        try
        {
            return NeutrinoModel.KamiokandeIIEvents(Outcome, _observer);
        }
        catch (Exception e)
        {
            throw new ModelErrorException("neutrino", $"{e.GetType().Name}: {e.Message}", e);
        }
    }
}
