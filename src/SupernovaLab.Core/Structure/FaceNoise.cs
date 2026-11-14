namespace SupernovaLab.Core.Structure;

/// <summary>
/// The small hash-based value noise the cut faces draw their mixing with (Task 16): the ragged edges of the nickel fingers
/// and hydrogen tongues, their wavy centrelines and the smeared layer boundaries (<see cref="CutFaceModel"/>). All single
/// precision, written operation for operation like <c>cutface.frag</c>'s copy (same hash, same lattice, same interpolation
/// <c>a + (b - a) * t</c>), so the C# classification and the GPU's agree except where the GPU rounds differently (fused
/// multiply-adds, its own atan and sqrt): a band of about one float ulp at an edge. Seeds are fixed (cone and layer indices),
/// never the run's.
/// </summary>
public static class FaceNoise
{
    /// <summary>The lattice's seed multiplier (the golden ratio's 32-bit fraction).</summary>
    private const uint SeedStep = 0x9e3779b1u;

    /// <summary>lowbias32 (C. Wellons, 2018): a 32-bit integer hash with good avalanche.</summary>
    public static uint Hash(uint x)
    {
        unchecked
        {
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
        }
        return x;
    }

    /// <summary>The hash of <paramref name="n"/> as a float in [0, 1): its top 24 bits, exact in single precision.</summary>
    public static float Unit(uint n) => (Hash(n) >> 8) * (1f / 16777216f);

    /// <summary>The 1D lattice value's hash at integer <paramref name="i"/> for this seed.</summary>
    public static uint Lattice(uint seed, int i) => unchecked((uint)i + seed * SeedStep);

    /// <summary>The 2D lattice value's hash at (<paramref name="i"/>, <paramref name="j"/>) for this seed.</summary>
    public static uint Lattice2(uint seed, int i, int j) => unchecked((uint)i + Hash((uint)j + seed * SeedStep));

    /// <summary>1D value noise in [-1, 1]: lattice values at the integers, smoothstep-interpolated.</summary>
    public static float Value1(uint seed, float x)
    {
        float fl = MathF.Floor(x);
        int i = (int)fl;
        float f = x - fl;
        float t = f * f * (3f - 2f * f);
        float a = 2f * Unit(Lattice(seed, i)) - 1f;
        float b = 2f * Unit(Lattice(seed, i + 1)) - 1f;
        return a + (b - a) * t;
    }

    /// <summary>2D value noise in [-1, 1]: bilinear in smoothstep weights between the four lattice values.</summary>
    public static float Value2(uint seed, float x, float y)
    {
        float fx = MathF.Floor(x), fy = MathF.Floor(y);
        int i = (int)fx, j = (int)fy;
        float f = x - fx;
        float tx = f * f * (3f - 2f * f);
        f = y - fy;
        float ty = f * f * (3f - 2f * f);
        float a = 2f * Unit(Lattice2(seed, i, j)) - 1f;
        float b = 2f * Unit(Lattice2(seed, i + 1, j)) - 1f;
        float c = 2f * Unit(Lattice2(seed, i, j + 1)) - 1f;
        float d = 2f * Unit(Lattice2(seed, i + 1, j + 1)) - 1f;
        float lo = a + (b - a) * tx;
        float hi = c + (d - c) * tx;
        return lo + (hi - lo) * ty;
    }

    /// <summary>Fractal sum of <paramref name="octaves"/> octaves of <see cref="Value1"/> (frequency x2, amplitude x0.5 each, seed +1), normalised to [-1, 1].</summary>
    public static float Fbm1(uint seed, float x, int octaves)
    {
        float sum = 0f, amp = 1f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * Value1(seed + (uint)o, x);
            norm += amp;
            x *= 2f;
            amp *= 0.5f;
        }
        return sum / norm;
    }

    /// <summary>Fractal sum of <paramref name="octaves"/> octaves of <see cref="Value2"/>, as <see cref="Fbm1"/>.</summary>
    public static float Fbm2(uint seed, float x, float y, int octaves)
    {
        float sum = 0f, amp = 1f, norm = 0f;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * Value2(seed + (uint)o, x, y);
            norm += amp;
            x *= 2f;
            y *= 2f;
            amp *= 0.5f;
        }
        return sum / norm;
    }
}
