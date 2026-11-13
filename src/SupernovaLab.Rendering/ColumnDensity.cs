namespace SupernovaLab.Rendering;

/// <summary>The density laws the shell impostor integrates (plan ruling P8).</summary>
public enum ShellKind
{
    /// <summary>Uniform density between r_in and r_out (a shocked shell).</summary>
    Shell,
    /// <summary>Uniform density inside r_out (r_in ignored).</summary>
    Ball,
    /// <summary>ρ ∝ 1/r² between r_in and r_out (a steady wind).</summary>
    Wind,
}

/// <summary>
/// The column density through a spherical shell along a straight line of sight, as an analytic integral (plan ruling P8: the
/// shells are camera-facing impostors that integrate it per pixel, which gives true limb brightening with no mesh). b is the
/// impact parameter, the line's distance from the centre; s is the distance along the line from its closest approach, so the
/// line is at radius √(b² + s²). Density 1 (or 1/r²) in the units of the radii. <c>shell.frag</c> mirrors these functions:
/// change one, change the other.
/// </summary>
public static class ColumnDensity
{
    /// <summary>2(√(r_out² − b²) − √(max(r_in² − b², 0))) for b &lt; r_out, else 0.</summary>
    public static double Shell(double b, double rIn, double rOut)
    {
        if (!(b < rOut)) return 0;
        return 2 * (Math.Sqrt(rOut * rOut - b * b) - Math.Sqrt(Math.Max(rIn * rIn - b * b, 0)));
    }

    /// <summary>The largest column through a shell, along the line grazing its inner surface: 2√(r_out² − r_in²).</summary>
    public static double ShellPeak(double rIn, double rOut) => 2 * Math.Sqrt(Math.Max(rOut * rOut - rIn * rIn, 0));

    /// <summary>The chord through a uniform ball: 2√(r² − b²) for b &lt; r, else 0.</summary>
    public static double Ball(double b, double r) => b < r ? 2 * Math.Sqrt(r * r - b * b) : 0;

    /// <summary>
    /// ∫ ds / (b² + s²) over the part of the chord between r_in and r_out: (2/b)[atan(z_out/b) − atan(z_in/b)], with
    /// z = √(r² − b²) (z_in = 0 inside the hole); at b = 0, 2(1/r_in − 1/r_out).
    /// </summary>
    public static double Wind(double b, double rIn, double rOut) => Full(ShellKind.Wind, b, rIn, rOut);

    /// <summary>The whole line's column for this kind.</summary>
    public static double Full(ShellKind kind, double b, double rIn, double rOut) => kind switch
    {
        ShellKind.Shell => Shell(b, rIn, rOut),
        ShellKind.Ball => Ball(b, rOut),
        _ => Segment(kind, b, rIn, rOut, double.NegativeInfinity, double.PositiveInfinity),
    };

    /// <summary>
    /// The column over the part of the line with s in [<paramref name="s1"/>, <paramref name="s2"/>] (0 when s2 ≤ s1). The
    /// material lies at s in [−z_out, −z_in] and [z_in, z_out]. The cutaway subtracts the segment inside the removed quarter.
    /// </summary>
    public static double Segment(ShellKind kind, double b, double rIn, double rOut, double s1, double s2)
    {
        if (!(b < rOut) || !(s2 > s1)) return 0;
        double zOut = Math.Sqrt(rOut * rOut - b * b);
        double zIn = kind == ShellKind.Ball ? 0 : Math.Sqrt(Math.Max(rIn * rIn - b * b, 0));
        return Piece(kind, b, Math.Max(s1, -zOut), Math.Min(s2, -zIn)) + Piece(kind, b, Math.Max(s1, zIn), Math.Min(s2, zOut));
    }

    /// <summary>The integral over [a, c] inside the material (0 when c ≤ a): its length, or ∫ ds/(b² + s²) for a wind.</summary>
    private static double Piece(ShellKind kind, double b, double a, double c)
    {
        if (!(c > a)) return 0;
        if (kind != ShellKind.Wind) return c - a;
        // atan(c/b) − atan(a/b) = atan2(b(c − a), b² + ac): exact (the difference lies in (−π, π)) and stable as b → 0.
        if (b > 0) return Math.Atan2(b * (c - a), b * b + a * c) / b;
        return 1 / a - 1 / c;   // b = 0: the interval never contains s = 0 (r_in > 0)
    }

    /// <summary>
    /// The largest column over all lines of sight, to normalize a shell's brightness: <see cref="ShellPeak"/> for a shell, the
    /// diameter for a ball, and for a wind the largest of 256 impact parameters across (0, r_out).
    /// </summary>
    public static double Peak(ShellKind kind, double rIn, double rOut)
    {
        if (kind == ShellKind.Shell) return ShellPeak(rIn, rOut);
        if (kind == ShellKind.Ball) return 2 * rOut;
        double peak = 0;
        for (int i = 0; i < 256; i++) peak = Math.Max(peak, Wind(rOut * (i + 0.5) / 256, rIn, rOut));
        return peak;
    }
}
