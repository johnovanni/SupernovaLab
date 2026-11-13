namespace SupernovaLab.Core.Structure;

/// <summary>
/// The particles the 3D view draws for an ejecta structure. <see cref="Velocity"/> is interleaved x, y, z in units of
/// <see cref="MaxCmS"/> (cm/s); <see cref="Layer"/> is the <see cref="ElementLayer"/> as a float; <see cref="Seed"/> is
/// the particle's u in [0, 1), the same u the mixing used. Each particle carries M_ej / <see cref="Count"/>.
/// </summary>
public sealed record ParticleSet(float[] Velocity, float[] Layer, float[] Seed, int Count, double MaxCmS);

/// <summary>
/// Draws the particle set (rulings P1, P3, R-P2-13; structure §S1 and §S3 App blocks): particle i of N sits at the mass
/// coordinate q_i = (i + 0.5)/N with speed v(0.999 q_i) and the layer that contains q_i, so each layer's particle count is
/// its mass fraction × N to within one particle; directions are seeded and isotropic; then <see cref="MixingModel"/>
/// moves nickel and hydrogen in the finger cones.
/// <para>Determinism: the cone axes come from <c>new System.Random(seed)</c> and the particles from
/// <c>new System.Random(seed + 1,000,003)</c>. Seeded <c>System.Random</c> uses .NET's fixed legacy algorithm, so the
/// set is the same on every run (Global Constraints: the fixed seed 1987, never the run number).</para>
/// </summary>
public static class ParticleSampler
{
    /// <summary>The fixed seed, as Project 1's ejecta points.</summary>
    public const int DefaultSeed = 1987;

    private const int ParticleStreamOffset = 1_000_003;

    public static ParticleSet Sample(EjectaStructure s, int count, int seed = DefaultSeed)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        var speed = new double[count];
        var layer = new ElementLayer[count];
        EjectaStructureModel.CellSpeeds(s, speed, layer);

        var random = new Random(unchecked(seed + ParticleStreamOffset));
        var u = new double[count];
        double[] dx = new double[count], dy = new double[count], dz = new double[count];
        for (int i = 0; i < count; i++)
        {
            (dx[i], dy[i], dz[i]) = MixingModel.IsotropicDirection(random);
            u[i] = random.NextDouble();
        }

        if (s.Mixing.HasFingers && !s.IsEnvelopeShell)
            MixingModel.Apply(s.Mixing, MixingModel.Cones(s.Mixing, seed), speed, layer, u, dx, dy, dz, random);

        double max = s.MaxCmS;
        foreach (double v in speed) max = Math.Max(max, v);

        var velocity = new float[3 * count];
        var layerTag = new float[count];
        var seedOut = new float[count];
        for (int i = 0; i < count; i++)
        {
            double r = speed[i] / max;
            velocity[3 * i] = (float)(r * dx[i]);
            velocity[3 * i + 1] = (float)(r * dy[i]);
            velocity[3 * i + 2] = (float)(r * dz[i]);
            layerTag[i] = (float)layer[i];
            seedOut[i] = (float)u[i];
        }
        return new ParticleSet(velocity, layerTag, seedOut, count, max);
    }
}
