using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>The dipole field lines the Close-up draws (km, in the magnetic frame: the magnetic axis is +z): closed loops and open lines.</summary>
public sealed record FieldLineSet(IReadOnlyList<Vector3d[]> Closed, IReadOnlyList<Vector3d[]> Open);

/// <summary>
/// Plan Task 6 (spec §4.3): a neutron star's dipole field lines, r = L sin²θ with θ from the magnetic axis, the accepted shape near
/// the star. The number of lines is schematic: <see cref="ShellCount"/> L-shells, one more per decade of field, starting at 1.5 radii
/// and doubling outward, each drawn in <see cref="Planes"/> planes round the magnetic axis. A shell whose L exceeds the light cylinder
/// R_L is open: drawn from its footpoints only out to r = R_L. The lines leaving the polar caps (inside
/// <see cref="PolarCapRad"/>, L &gt; R_L by definition) are always shown open, so every pulsar shows where its beams' field lines
/// leave. Pure geometry, no OpenGL: <see cref="FieldLineRenderer"/> draws it.
/// </summary>
public static class FieldLines
{
    /// <summary>The planes round the magnetic axis each shell is drawn in.</summary>
    public const int Planes = 6;

    /// <summary>The points along each line (an odd number, so a closed line has a point at its top, θ = 90°).</summary>
    public const int Points = 65;

    /// <summary>The innermost shell's L, in stellar radii; each further shell doubles it.</summary>
    public const double InnermostShellRadii = 1.5;

    /// <summary>The polar-cap lines' footpoints, as fractions of the polar cap's angular radius.</summary>
    private static readonly double[] CapFootpoints = { 0.5 };

    /// <summary>The number of L-shells: round(log₁₀ B) − 9, from 1 to 6 (2 at 10¹¹ G, 4 for the Crab's 3.8 × 10¹² G, 6 for a magnetar).</summary>
    public static int ShellCount(double fieldG)
        => fieldG > 0 && double.IsFinite(fieldG) ? Math.Clamp((int)Math.Round(Math.Log10(fieldG)) - 9, 1, 6) : 1;

    /// <summary>The shells' L values, km: 1.5 R, 3 R, 6 R, ...</summary>
    public static IReadOnlyList<double> ShellsKm(double radiusKm, double fieldG)
        => Enumerable.Range(0, ShellCount(fieldG)).Select(k => InnermostShellRadii * radiusKm * Math.Pow(2, k)).ToArray();

    /// <summary>The polar cap's angular radius, asin √(R / R_L): the field lines leaving inside it reach the light cylinder.</summary>
    public static double PolarCapRad(double radiusKm, double lightCylinderKm)
        => lightCylinderKm > radiusKm && radiusKm > 0 ? Math.Asin(Math.Sqrt(radiusKm / lightCylinderKm)) : Math.PI / 2;

    /// <summary>The lines for a star of this radius and field with this light cylinder (all km).</summary>
    public static FieldLineSet For(double radiusKm, double fieldG, double lightCylinderKm)
    {
        var closed = new List<Vector3d[]>();
        var open = new List<Vector3d[]>();
        if (!(radiusKm > 0)) return new FieldLineSet(closed, open);
        double rl = lightCylinderKm > radiusKm ? lightCylinderKm : double.PositiveInfinity;
        var shells = ShellsKm(radiusKm, fieldG).ToList();
        double cap = PolarCapRad(radiusKm, rl);
        if (double.IsFinite(rl))
            foreach (double f in CapFootpoints)
                shells.Add(radiusKm / Math.Pow(Math.Sin(f * cap), 2));
        foreach (double l in shells)
        {
            double foot = Math.Asin(Math.Sqrt(radiusKm / l));
            for (int k = 0; k < Planes; k++)
            {
                double phi = 2 * Math.PI * k / Planes;
                if (l <= rl)
                {
                    closed.Add(Line(l, phi, foot, Math.PI - foot));
                    continue;
                }
                double top = Math.Asin(Math.Sqrt(rl / l));   // where r = L sin²θ reaches R_L
                open.Add(Line(l, phi, foot, top));
                open.Add(Line(l, phi, Math.PI - foot, Math.PI - top));
            }
        }
        return new FieldLineSet(closed, open);
    }

    /// <summary>Points on r = L sin²θ in the plane at azimuth φ, θ from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private static Vector3d[] Line(double l, double phi, double from, double to)
    {
        var points = new Vector3d[Points];
        double c = Math.Cos(phi), s = Math.Sin(phi);
        for (int i = 0; i < Points; i++)
        {
            double theta = from + (to - from) * i / (Points - 1);
            double st = Math.Sin(theta), r = l * st * st;
            points[i] = new Vector3d(r * st * c, r * st * s, r * Math.Cos(theta));
        }
        return points;
    }
}
