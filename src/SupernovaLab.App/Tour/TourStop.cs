using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.App.Tour;

/// <summary>Where a sentence of a tour caption comes from, which sets how firm it is (criterion 4).</summary>
public enum TourSourceKind
{
    /// <summary>A number or statement from the lab's own model.</summary>
    LabModel,
    /// <summary>An observation checked against its paper.</summary>
    ObservedChecked,
    /// <summary>An observation recalled in docs/research/presets.md and not re-opened ([R]).</summary>
    ObservedRecalled,
}

/// <summary>One source of a stop's caption: its kind (the confidence) and its <see cref="Source"/>.</summary>
public sealed record TourSourceNote(TourSourceKind Kind, Source Source)
{
    public string KindText => Kind switch
    {
        TourSourceKind.LabModel => "In the lab (model)",
        TourSourceKind.ObservedChecked => "Observed (checked against the paper)",
        _ => "Observed (recalled, not re-checked)",
    };
}

/// <summary>
/// One stop of the guided tour (Project 2, Task 12). <see cref="ObserverTimeS"/> is an OBSERVER time (source time × (1 + z),
/// ruling R-P2-6), the timeline's own clock; the first stop is at 1 ms, the timeline's start. A <see cref="Cutaway"/> stop
/// shows the star's interior (between bounce and breakout) and the camera orbits it at <see cref="OrbitDegPerS"/>.
/// <see cref="Highlight"/> names the instrument panel (and strip tile) to brighten while the stop is shown, or null.
/// <see cref="Notes"/> are the sources and confidence of every sentence in the caption (merged stops carry all of them);
/// <see cref="SourceText"/> is the ready-made tooltip.
/// </summary>
public sealed record TourStop(double ObserverTimeS, string Caption, bool Cutaway, double DwellS = 4,
    double OrbitDegPerS = 0, Messenger? Highlight = null)
{
    public IReadOnlyList<TourSourceNote> Notes { get; init; } = Array.Empty<TourSourceNote>();

    /// <summary>The sources of the caption's sentences, in caption order.</summary>
    public IReadOnlyList<Source> Sources => Notes.Select(n => n.Source).ToList();

    /// <summary>One line per source: "Observed (recalled, not re-checked): citation [reference]"; empty when the caption has none.</summary>
    public string SourceText => string.Join(Environment.NewLine,
        Notes.Select(n => $"{n.KindText}: {n.Source.Citation} [{n.Source.ResearchRef}]"));
}
