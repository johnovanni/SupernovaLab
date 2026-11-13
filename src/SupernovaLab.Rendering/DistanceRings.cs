namespace SupernovaLab.Rendering;

/// <summary>A camera-facing distance ring: its radius (cm), its label (e.g. "5 AU") and its opacity (0 to 1).</summary>
public sealed record Ring(double RadiusCm, string Label, float Opacity);

/// <summary>
/// The 3D view's distance-ring ladder (plan ruling P5): rings at 1-2-5 × 10^k of the scale bar's unit, from 0.9 of the view's
/// half-height down to 0.05 of it. Rings are fully opaque from 0.1 of the half-height up and fade linearly to nothing at 0.05.
/// </summary>
public static class DistanceRings
{
    private const double Tol = 1e-9;
    private const double Outer = 0.9, FadeFull = 0.1, FadeNone = 0.05;
    private static readonly double[] Mantissas = { 1, 2, 5 };

    /// <summary>The rings for a view of this half-height (cm), outermost first.</summary>
    public static IReadOnlyList<Ring> Rungs(double halfHeightCm)
    {
        var rings = new List<Ring>();
        if (!(halfHeightCm > 0) || double.IsInfinity(halfHeightCm)) return rings;
        var (unitCm, name) = ScaleBar.UnitFor(halfHeightCm);
        double value = FloorLadder(Outer * halfHeightCm / unitCm);
        for (int i = 0; i < 100; i++)
        {
            double cm = value * unitCm;
            double frac = cm / halfHeightCm;
            if (frac <= FadeNone * (1 + Tol)) break;
            float opacity = (float)Math.Clamp((frac - FadeNone) / (FadeFull - FadeNone), 0, 1);
            rings.Add(new Ring(cm, ScaleBar.Label(value, name), opacity));
            value = Step(value, -1);
        }
        return rings;
    }

    /// <summary>Moves a ring radius (cm) this many rungs up (+) or down (−) the 1-2-5 ladder of the unit.</summary>
    public static double Rung(double cm, int steps, string unitName)
    {
        double unitCm = ScaleBar.UnitCmOf(unitName);
        return Step(FloorLadder(cm / unitCm), steps) * unitCm;
    }

    /// <summary>The smallest ladder value (in the same units as x) that is at least x.</summary>
    internal static double CeilLadder(double x)
    {
        double f = FloorLadder(x);
        return f >= x * (1 - Tol) ? f : Step(f, 1);
    }

    /// <summary>The largest ladder value that is at most x (x &gt; 0), tolerating rounding.</summary>
    private static double FloorLadder(double x)
    {
        int e = (int)Math.Floor(Math.Log10(x) + 1e-12);
        double m = x / Math.Pow(10, e);
        int r = m >= 5 * (1 - Tol) ? 2 : m >= 2 * (1 - Tol) ? 1 : 0;
        return Mantissas[r] * Math.Pow(10, e);
    }

    /// <summary>Moves a ladder value by whole rungs.</summary>
    private static double Step(double value, int steps)
    {
        int e = (int)Math.Floor(Math.Log10(value) + 1e-12);
        double m = value / Math.Pow(10, e);
        int r = m >= 5 * (1 - Tol) ? 2 : m >= 2 * (1 - Tol) ? 1 : 0;
        int k = 3 * e + r + steps;
        int ne = (int)Math.Floor(k / 3.0);
        return Mantissas[k - 3 * ne] * Math.Pow(10, ne);
    }
}
