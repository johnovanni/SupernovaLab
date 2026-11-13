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
        double outerCm = Outer * halfHeightCm;
        var (unitCm, name) = ScaleBar.UnitFor(halfHeightCm);
        int top = IndexAtMost(outerCm);
        if (top >= 0) { unitCm = Table[top].UnitCm; name = Table[top].Unit; }
        double value = top >= 0 ? Table[top].Cm / unitCm : FloorLadder(outerCm / unitCm);
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

    /// <summary>
    /// Every outer-ring value the director may use, ascending: the 1-2-5 values V of each unit for which a view of half-height
    /// V / 0.9 is shown in that same unit. So the unit the director picks and the unit <see cref="Rungs"/> derives agree.
    /// </summary>
    private static readonly (double Cm, double UnitCm, string Unit)[] Table = BuildTable();

    private static (double Cm, double UnitCm, string Unit)[] BuildTable()
    {
        var list = new List<(double, double, string)>();
        foreach (var name in new[] { "km", "AU", "light-year", "pc" })
        {
            double unitCm = ScaleBar.UnitCmOf(name);
            for (int e = -8; e <= 12; e++)
                foreach (double m in Mantissas)
                {
                    double cm = m * Math.Pow(10, e) * unitCm;
                    if (cm < 1e3 || cm > 1e26) continue;
                    if (ScaleBar.UnitFor(cm / Outer).Name == name) list.Add((cm, unitCm, name));
                }
        }
        list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return list.ToArray();
    }

    internal static int TableCount => Table.Length;

    internal static double TableValue(int index) => Table[Math.Clamp(index, 0, Table.Length - 1)].Cm;

    /// <summary>Index of the largest table ring ≤ cm (with rounding tolerance), or −1 if none.</summary>
    internal static int IndexAtMost(double cm)
    {
        int found = -1;
        for (int i = 0; i < Table.Length && Table[i].Cm <= cm * (1 + Tol); i++) found = i;
        return found;
    }

    /// <summary>Index of the smallest table ring ≥ cm (with rounding tolerance); the last one if cm is beyond the table.</summary>
    internal static int IndexAtLeast(double cm)
    {
        for (int i = 0; i < Table.Length; i++)
            if (Table[i].Cm >= cm * (1 - Tol)) return i;
        return Table.Length - 1;
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
