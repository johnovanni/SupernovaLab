using SupernovaLab.Core.Progenitors;

namespace SupernovaLab.Core.Outcomes;

/// <summary>One sample of the outcome map: the fate, remnant and class of a star of this initial mass (M☉).</summary>
public sealed record MapCell(double InitialMass, Fate Fate, RemnantKind Remnant, SupernovaType Type);

/// <summary>
/// The outcome map (spec §3): how the outcome changes with initial mass when every other input is held fixed.
/// </summary>
public static class OutcomeMap
{
    /// <summary>
    /// <paramref name="samples"/> masses log-spaced from <see cref="LabInputs.MinInitialMass"/> (0.8 M☉) to
    /// <see cref="LabInputs.MaxInitialMass"/> (300 M☉), both ends included, each computed with the
    /// <paramref name="template"/>'s other inputs. The strip maps the massive-star path, so a white-dwarf template is
    /// read as a massive star.
    /// </summary>
    public static IReadOnlyList<MapCell> Strip(OutcomeEngine engine, LabInputs template, int samples = 400)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentOutOfRangeException.ThrowIfLessThan(samples, 2);

        var star = template with { Kind = StarKind.MassiveStar };
        double logLow = Math.Log(LabInputs.MinInitialMass), logHigh = Math.Log(LabInputs.MaxInitialMass);
        var cells = new MapCell[samples];
        for (int i = 0; i < samples; i++)
        {
            // The ends are set exactly so that rounding cannot push them outside the slider range.
            double m = i == 0 ? LabInputs.MinInitialMass
                     : i == samples - 1 ? LabInputs.MaxInitialMass
                     : Math.Exp(logLow + (logHigh - logLow) * i / (samples - 1));
            var o = engine.Compute(star with { InitialMass = m });
            cells[i] = new MapCell(m, o.Fate, o.Remnant.Kind, o.Type);
        }
        return cells;
    }
}
