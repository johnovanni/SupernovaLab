using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Structure;

namespace SupernovaLab.Tests;

/// <summary>
/// Task 5: the particle set drawn from an ejecta structure (rulings P1, P3, R-P2-13; structure §S1 and §S3's App
/// blocks). Speeds in the set are in units of <see cref="ParticleSet.MaxCmS"/>.
/// </summary>
public class ParticleSamplerTests
{
    private const double KmS = 1e5;
    private const int Count = 200_000;

    private static EjectaStructure Structure(string presetId)
    {
        var p = PresetCatalog.Get(presetId);
        return EjectaStructureModel.For(TestOutcomes.Engine.Compute(p.Inputs, p.Pins))!;
    }

    private static double Speed(ParticleSet p, int i)
    {
        double x = p.Velocity[3 * i], y = p.Velocity[3 * i + 1], z = p.Velocity[3 * i + 2];
        return Math.Sqrt(x * x + y * y + z * z) * p.MaxCmS;
    }

    private static (double X, double Y, double Z) Direction(ParticleSet p, int i)
    {
        double x = p.Velocity[3 * i], y = p.Velocity[3 * i + 1], z = p.Velocity[3 * i + 2];
        double r = Math.Sqrt(x * x + y * y + z * z);
        return (x / r, y / r, z / r);
    }

    private static int[] Indices(ParticleSet p, ElementLayer layer)
        => Enumerable.Range(0, p.Count).Where(i => (int)p.Layer[i] == (int)layer).ToArray();

    [Fact]
    public void Sampler_LayerCountsMatchMasses_ToOneParticle()
    {
        foreach (var s in new[] { Structure("sn1987a"), Structure("sn1998bw"), EjectaStructureModel.For(TestOutcomes.TypeIa())! })
        {
            var p = ParticleSampler.Sample(s, Count);
            Assert.Equal(Count, p.Count);
            Assert.Equal(3 * Count, p.Velocity.Length);
            Assert.Equal(Count, p.Layer.Length);
            Assert.Equal(Count, p.Seed.Length);
            foreach (var l in s.Layers)
            {
                double expected = Count * l.MassMsun / s.EjectaMassMsun;
                Assert.InRange(Indices(p, l.Layer).Length, expected - 1, expected + 1);
            }
            Assert.All(p.Seed, u => Assert.InRange(u, 0f, 1f));
        }
    }

    [Fact]
    public void Sampler_UnmixedSpeeds_FollowTheProfile_AtStratifiedMassCoordinates()
    {
        // Type Ia: no mixing, so particle i moves at v(0.999 (i + 0.5)/N) (rulings P1, P3) and the fastest is below
        // v_max; the layers run centre outward.
        var s = EjectaStructureModel.For(TestOutcomes.TypeIa())!;
        var p = ParticleSampler.Sample(s, 10_000);
        Assert.Equal(s.MaxCmS, p.MaxCmS);
        foreach (int i in new[] { 0, 1, 4999, 9999 })
        {
            double v = EjectaStructureModel.VelocityAtMassFraction(s, 0.999 * (i + 0.5) / 10_000);
            Assert.Equal(1, Speed(p, i) / v, 6);
        }
        Assert.Equal((float)ElementLayer.NickelIron, p.Layer[0]);
        Assert.Equal((float)ElementLayer.UnburnedCarbonOxygen, p.Layer[9999]);
    }

    [Fact]
    public void Sampler_IsDeterministic()
    {
        var s = Structure("sn1987a");
        var a = ParticleSampler.Sample(s, 20_000);
        var b = ParticleSampler.Sample(s, 20_000);
        Assert.Equal(a.Velocity, b.Velocity);
        Assert.Equal(a.Layer, b.Layer);
        Assert.Equal(a.Seed, b.Seed);
        Assert.Equal(1987, ParticleSampler.DefaultSeed);
        var c = ParticleSampler.Sample(s, 20_000, seed: 7);
        Assert.NotEqual(a.Velocity, c.Velocity);
    }

    [Fact]
    public void Sampler_DirectionsAreIsotropic()
    {
        var s = EjectaStructureModel.For(TestOutcomes.TypeIa())!;
        var p = ParticleSampler.Sample(s, Count);
        double mx = 0, my = 0, mz = 0, zz = 0;
        for (int i = 0; i < p.Count; i++)
        {
            var d = Direction(p, i);
            mx += d.X; my += d.Y; mz += d.Z; zz += d.Z * d.Z;
        }
        // Mean direction ≈ 0 (σ = 1/√(3N) ≈ 0.0013) and ⟨z²⟩ = 1/3.
        Assert.InRange(mx / Count, -0.006, 0.006);
        Assert.InRange(my / Count, -0.006, 0.006);
        Assert.InRange(mz / Count, -0.006, 0.006);
        Assert.InRange(zz / Count, 1.0 / 3 - 0.005, 1.0 / 3 + 0.005);
    }

    [Fact]
    public void Cones_UnionCovers_TheCoveredFraction()
    {
        // Ruling R-P2-3: the half-angle is set so the union of the K seeded cones covers f, checked here on an independent
        // random set of directions (σ ≈ 0.001), to 0.02.
        foreach (var s in new[] { Structure("sn1987a"), EjectaStructureModel.For(TestOutcomes.Star(16))! })
        {
            var cones = MixingModel.Cones(s.Mixing, ParticleSampler.DefaultSeed);
            Assert.Equal(s.Mixing.FingerCount, cones.Axes.Count);
            var random = new Random(42);
            int inside = 0;
            const int n = 200_000;
            for (int i = 0; i < n; i++)
            {
                double z = 2 * random.NextDouble() - 1, phi = 2 * Math.PI * random.NextDouble(), r = Math.Sqrt(1 - z * z);
                if (cones.Contains(r * Math.Cos(phi), r * Math.Sin(phi), z)) inside++;
            }
            Assert.InRange((double)inside / n, s.Mixing.CoveredFraction - 0.02, s.Mixing.CoveredFraction + 0.02);
        }
        // SN 1987A's four cones would have a half-angle of 18.19° if they did not overlap (4 (1 − cos θ)/2 = 0.1);
        // overlap can only widen them.
        var bsg = MixingModel.Cones(Structure("sn1987a").Mixing, ParticleSampler.DefaultSeed);
        Assert.True(bsg.HalfAngleRad >= Math.Acos(1 - 2 * 0.1 / 4) - 1e-9);
    }

    [Fact]
    public void Mixing_NickelReachesTheResearchSpeed_HydrogenFallsToIt()
    {
        // SN 1987A preset: NickelMax = 4,700 km/s (the fast clump), HydrogenMin = 700 km/s (Kozma & Fransson 1998),
        // K = 4, f = 0.1, F_Ni = 0.5 (structure §S3, ruling R-P2-13).
        var s = Structure("sn1987a");
        var mixed = ParticleSampler.Sample(s, Count);
        var plain = ParticleSampler.Sample(s with { Mixing = MixingReach.None }, Count);
        var cones = MixingModel.Cones(s.Mixing, ParticleSampler.DefaultSeed);

        var nickel = Indices(mixed, ElementLayer.NickelIron);
        var hydrogen = Indices(mixed, ElementLayer.Hydrogen);
        Assert.Equal(nickel, Indices(plain, ElementLayer.NickelIron));   // mixing never changes the layer masses
        Assert.Equal(hydrogen, Indices(plain, ElementLayer.Hydrogen));

        // Nickel: the moved fraction is F_Ni ± 0.03 and every moved particle lies inside a cone.
        var moved = nickel.Where(i => mixed.Velocity[3 * i] != plain.Velocity[3 * i]
                                      || mixed.Velocity[3 * i + 1] != plain.Velocity[3 * i + 1]
                                      || mixed.Velocity[3 * i + 2] != plain.Velocity[3 * i + 2]).ToArray();
        Assert.InRange((double)moved.Length / nickel.Length, 0.5 - 0.03, 0.5 + 0.03);
        foreach (int i in moved)
        {
            var d = Direction(mixed, i);
            Assert.True(cones.Contains(d.X, d.Y, d.Z), $"nickel particle {i} outside every cone");
        }

        // The fastest nickel reaches the tip speed within 5%, and never passes it.
        double maxNi = nickel.Max(i => Speed(mixed, i));
        Assert.InRange(maxNi, 0.95 * 4700 * KmS, 4700 * KmS * (1 + 1e-6));

        // Hydrogen falls to HydrogenMin within 5%, never below it.
        double minH = hydrogen.Min(i => Speed(mixed, i));
        Assert.InRange(minH, 700 * KmS * (1 - 1e-6), 1.05 * 700 * KmS);
        // Hydrogen moves only inside a cone and only inward.
        foreach (int i in hydrogen)
        {
            double v = Speed(mixed, i), v0 = Speed(plain, i);
            if (Math.Abs(v - v0) > 1e-4 * v0)
            {
                Assert.True(v < v0);
                var d = Direction(mixed, i);
                Assert.True(cones.Contains(d.X, d.Y, d.Z));
            }
        }
    }

    [Fact]
    public void Mixing_Frame99_AgreesWithTheSampledSet()
    {
        // Frame99CmS is the expected speed enclosing 99% of the particle mass after mixing; the sampled set's own 99%
        // speed must agree to 0.3% (the expectation is taken over 20,000 mass cells).
        foreach (var s in new[] { Structure("sn1987a"), EjectaStructureModel.For(TestOutcomes.Star(16))! })
        {
            var p = ParticleSampler.Sample(s, Count);
            var speeds = Enumerable.Range(0, p.Count).Select(i => Speed(p, i)).OrderBy(v => v).ToArray();
            double sampled99 = speeds[(int)(0.99 * Count) - 1];
            Assert.Equal(1, s.Frame99CmS / sampled99, 0.003);
        }
    }

    [Fact]
    public void Sampler_EnvelopeShell_PutsEveryParticleAtTheShellSpeed()
    {
        var p = PresetCatalog.Get("n6946bh1");
        var s = EjectaStructureModel.For(TestOutcomes.Engine.Compute(p.Inputs, p.Pins))!;
        var set = ParticleSampler.Sample(s, 1000);
        Assert.Equal(75 * KmS, set.MaxCmS);
        for (int i = 0; i < set.Count; i++)
        {
            Assert.Equal(1, Speed(set, i) / (75 * KmS), 6);
            Assert.Equal((float)ElementLayer.Hydrogen, set.Layer[i]);
        }
    }
}
