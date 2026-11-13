namespace SupernovaLab.Core.Structure;

/// <summary>
/// The finger cones of one particle set: <see cref="Axes"/> are unit vectors, every cone has the same
/// <see cref="HalfAngleRad"/>. Empty (no axes, half-angle 0) when the class has no fingers.
/// </summary>
public sealed record FingerCones(IReadOnlyList<(double X, double Y, double Z)> Axes, double HalfAngleRad)
{
    /// <summary>
    /// Slack on the cone test's cosine, so a direction drawn on a cone's rim still counts as inside after the particle's
    /// velocity is stored as float (about 3 × 10⁻⁶ rad at 18°).
    /// </summary>
    public const double RimToleranceCos = 1e-6;

    public static FingerCones Empty { get; } = new(Array.Empty<(double, double, double)>(), 0);

    /// <summary>The index of the cone whose axis is nearest this direction (largest cosine), or −1 with no cones.</summary>
    public int Nearest(double x, double y, double z) => Nearest(x, y, z, out _);

    /// <summary>Whether this unit direction lies inside any cone.</summary>
    public bool Contains(double x, double y, double z)
        => Nearest(x, y, z, out double cos) >= 0 && cos >= Math.Cos(HalfAngleRad) - RimToleranceCos;

    private int Nearest(double x, double y, double z, out double bestCos)
    {
        int best = -1;
        bestCos = double.NegativeInfinity;
        for (int k = 0; k < Axes.Count; k++)
        {
            var a = Axes[k];
            double c = a.X * x + a.Y * y + a.Z * z;
            if (c > bestCos) { bestCos = c; best = k; }
        }
        return best;
    }
}

/// <summary>
/// Rayleigh–Taylor mixing as finger cones (structure §S3 App block, rulings R-P2-3 and R-P2-13):
/// <list type="bullet">
/// <item>K cone axes are seeded and isotropic; the common half-angle θ is set so the union of the cones covers the
/// fraction f of the sphere, measured on a <see cref="CoverageGridSize"/>-point Fibonacci grid of directions.</item>
/// <item>Nickel: a fraction F_Ni of the nickel particles, chosen by seed (exactly round(F_Ni × count)), is redirected
/// into the nearest cone (direction redrawn uniformly inside it) and pushed outward, v' = v + (NickelMax − v)·u², u being
/// the particle's seed. A particle already faster than NickelMax keeps its speed.</item>
/// <item>Hydrogen [APPROX, the mirror of the nickel rule]: a hydrogen particle inside a cone and faster than HydrogenMin
/// is pulled inward, v' = v − (v − HydrogenMin)·u².</item>
/// </list>
/// Mixing changes directions and speeds only, never a particle's layer, so the layer masses are unchanged.
/// </summary>
public static class MixingModel
{
    /// <summary>Directions on which the cones' union coverage is measured.</summary>
    public const int CoverageGridSize = 20_000;

    /// <summary>
    /// The finger cones for this mixing and seed: K isotropic axes from <c>new Random(seed)</c>, and the half-angle whose
    /// union coverage on the grid is f. <see cref="FingerCones.Empty"/> without fingers.
    /// </summary>
    public static FingerCones Cones(MixingReach mixing, int seed)
    {
        ArgumentNullException.ThrowIfNull(mixing);
        if (!mixing.HasFingers) return FingerCones.Empty;

        var random = new Random(seed);
        var axes = new (double X, double Y, double Z)[mixing.FingerCount];
        for (int k = 0; k < axes.Length; k++) axes[k] = IsotropicDirection(random);

        // The largest cosine to any axis, per grid direction; the (f × G)-th largest is the cone's cosine.
        var maxCos = new double[CoverageGridSize];
        double golden = Math.PI * (3 - Math.Sqrt(5));
        for (int i = 0; i < CoverageGridSize; i++)
        {
            double z = 1 - (2.0 * i + 1) / CoverageGridSize, r = Math.Sqrt(1 - z * z), phi = golden * i;
            double x = r * Math.Cos(phi), y = r * Math.Sin(phi), best = -1;
            foreach (var a in axes) best = Math.Max(best, a.X * x + a.Y * y + a.Z * z);
            maxCos[i] = best;
        }
        Array.Sort(maxCos);
        int inside = Math.Clamp((int)Math.Round(Math.Min(mixing.CoveredFraction, 1) * CoverageGridSize), 1, CoverageGridSize);
        double cosTheta = Math.Clamp(maxCos[CoverageGridSize - inside], -1, 1);
        return new FingerCones(axes, Math.Acos(cosTheta));
    }

    /// <summary>
    /// Applies the mixing in place to particles with these speeds (cm/s), layers, seeds u and unit directions. The
    /// nickel selection and the redrawn directions come from <paramref name="random"/>.
    /// </summary>
    internal static void Apply(MixingReach m, FingerCones cones, double[] speed, ElementLayer[] layer, double[] u,
        double[] dx, double[] dy, double[] dz, Random random)
    {
        if (!m.HasFingers || cones.Axes.Count == 0) return;
        double cosTheta = Math.Cos(cones.HalfAngleRad);

        if (double.IsFinite(m.HydrogenMinCmS))
        {
            double hMin = m.HydrogenMinCmS;
            for (int i = 0; i < speed.Length; i++)
                if (layer[i] == ElementLayer.Hydrogen && speed[i] > hMin && cones.Contains(dx[i], dy[i], dz[i]))
                    speed[i] -= (speed[i] - hMin) * u[i] * u[i];
        }

        if (m.NickelFingerFraction > 0 && double.IsFinite(m.NickelMaxCmS))
        {
            var nickel = new List<int>();
            for (int i = 0; i < speed.Length; i++)
                if (layer[i] == ElementLayer.NickelIron) nickel.Add(i);
            int moved = (int)Math.Round(Math.Min(m.NickelFingerFraction, 1) * nickel.Count);

            // A partial Fisher–Yates shuffle picks exactly `moved` of them.
            for (int k = 0; k < moved; k++)
            {
                int j = random.Next(k, nickel.Count);
                (nickel[k], nickel[j]) = (nickel[j], nickel[k]);
                int i = nickel[k];

                var axis = cones.Axes[cones.Nearest(dx[i], dy[i], dz[i])];
                (dx[i], dy[i], dz[i]) = InsideCone(axis, cosTheta, random);
                if (speed[i] < m.NickelMaxCmS) speed[i] += (m.NickelMaxCmS - speed[i]) * u[i] * u[i];
            }
        }
    }

    /// <summary>A direction uniform on the sphere: z uniform in [−1, 1), φ uniform.</summary>
    internal static (double X, double Y, double Z) IsotropicDirection(Random random)
    {
        double z = 2 * random.NextDouble() - 1, phi = 2 * Math.PI * random.NextDouble(), r = Math.Sqrt(1 - z * z);
        return (r * Math.Cos(phi), r * Math.Sin(phi), z);
    }

    /// <summary>A direction uniform inside the cone of this unit axis and cosine: cos α uniform in (cos θ, 1].</summary>
    private static (double X, double Y, double Z) InsideCone((double X, double Y, double Z) a, double cosTheta, Random random)
    {
        double cosA = 1 - random.NextDouble() * (1 - cosTheta), sinA = Math.Sqrt(Math.Max(0, 1 - cosA * cosA));
        double phi = 2 * Math.PI * random.NextDouble();

        // An orthonormal pair perpendicular to the axis.
        var helper = Math.Abs(a.X) < 0.9 ? (1.0, 0.0, 0.0) : (0.0, 1.0, 0.0);
        var e1 = Normalize(Cross(a, helper));
        var e2 = Cross(a, e1);
        double c = Math.Cos(phi), s = Math.Sin(phi);
        return (cosA * a.X + sinA * (c * e1.X + s * e2.X),
                cosA * a.Y + sinA * (c * e1.Y + s * e2.Y),
                cosA * a.Z + sinA * (c * e1.Z + s * e2.Z));
    }

    private static (double X, double Y, double Z) Cross((double X, double Y, double Z) a, (double X, double Y, double Z) b)
        => (a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    private static (double X, double Y, double Z) Normalize((double X, double Y, double Z) v)
    {
        double n = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        return (v.X / n, v.Y / n, v.Z / n);
    }
}
