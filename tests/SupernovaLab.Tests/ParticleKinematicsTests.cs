using SupernovaLab.Core.Structure;

namespace SupernovaLab.Tests;

/// <summary>Plan ruling P4: r = R*·(v / v_max) + v·(t − t_bo) before any interaction.</summary>
public class ParticleKinematicsTests
{
    private const double Rstar = 3e13, VMax = 3e9;

    [Fact]
    public void ParticleRadius_AtBreakout_IsInsideTheStar_AndHomologousLater()
    {
        // The fastest particle starts at the stellar surface; a slower one starts inside, in proportion.
        Assert.Equal(Rstar, ParticleKinematics.RadiusCm(VMax, 0, Rstar, VMax, null), 6);
        Assert.Equal(0.25 * Rstar, ParticleKinematics.RadiusCm(0.25 * VMax, 0, Rstar, VMax, null), 6);

        // Later the radii are proportional to the speeds (homologous): the stellar offset is itself ∝ v.
        double t = 1e6;
        double fast = ParticleKinematics.RadiusCm(0.8 * VMax, t, Rstar, VMax, null);
        double slow = ParticleKinematics.RadiusCm(0.2 * VMax, t, Rstar, VMax, null);
        Assert.Equal(4.0, fast / slow, 3);
        Assert.Equal(0.8 * VMax * t + 0.8 * Rstar, fast, 3);
    }

    [Fact]
    public void ParticleRadius_BeforeBreakout_IsHeldAtTheBreakoutPosition()
    {
        Assert.Equal(Rstar, ParticleKinematics.RadiusCm(VMax, -5, Rstar, VMax, null), 6);
    }

    // ---------------------------------------------------------------- Task 8: confinement behind the reverse shock (ruling P4, R-P2-4)

    /// <summary>Since breakout, s; the fastest particle's free radius K = R* + v_max t = 3e13 + 3e9 × 1e8 = 3.0003e17 cm.</summary>
    private const double Since = 1e8, K = Rstar + VMax * Since;

    /// <summary>Shocks with R_r = 0.5 K and R_c = 0.6 K (weight 1 unless given).</summary>
    private static ShockRadii Shocks(double weight = 1) => new(0.75 * K, 0.6 * K, 0.5 * K) { ConfinementWeight = weight };

    [Fact]
    public void Particles_PastTheReverseShock_LieBetweenReverseAndContact()
    {
        // Ruling P4: a particle whose free radius lies beyond R_r (speed ratio s > s_r = R_r / K = 0.5) is placed between R_r
        // and R_c in order of speed: r = R_r + (R_c - R_r)(s - s_r)/(1 - s_r). s = 0.75: 0.5 K + 0.1 K × 0.5 = 0.55 K.
        var shocks = Shocks();
        Assert.Equal(0.55, ParticleKinematics.RadiusCm(0.75 * VMax, Since, Rstar, VMax, shocks) / K, 12);
        Assert.Equal(0.6, ParticleKinematics.RadiusCm(VMax, Since, Rstar, VMax, shocks) / K, 12);
        double previous = 0;
        for (int i = 1; i <= 200; i++)
        {
            double v = VMax * i / 200.0;
            double free = ParticleKinematics.RadiusCm(v, Since, Rstar, VMax, null);
            double r = ParticleKinematics.RadiusCm(v, Since, Rstar, VMax, shocks);
            if (free <= shocks.ReverseCm) Assert.Equal(free, r);              // unshocked: free expansion
            else Assert.InRange(r, shocks.ReverseCm, shocks.ContactCm);       // shocked: between the reverse shock and the contact
            Assert.True(r >= previous, $"order of speed kept at v = {v}");
            previous = r;
        }
    }

    [Fact]
    public void Confinement_IsWeightedIn_SoNothingJumps()
    {
        // Ruling R-P2-4: r = lerp(free, confined, w). s = 0.75: free 0.75 K, confined 0.55 K; w = 0.5 -> 0.65 K; w = 0 -> free.
        Assert.Equal(0.65, ParticleKinematics.RadiusCm(0.75 * VMax, Since, Rstar, VMax, Shocks(0.5)) / K, 12);
        Assert.Equal(0.75, ParticleKinematics.RadiusCm(0.75 * VMax, Since, Rstar, VMax, Shocks(0)) / K, 12);
        Assert.Equal(0.5, ParticleKinematics.ShockedWeight(0.75 * VMax, Since, Rstar, VMax, Shocks(0.5)));
        Assert.Equal(0, ParticleKinematics.ShockedWeight(0.25 * VMax, Since, Rstar, VMax, Shocks(0.5)));
        Assert.Equal(0, ParticleKinematics.ShockedWeight(0.75 * VMax, Since, Rstar, VMax, null));
    }

    [Fact]
    public void FreeRadius_InvertsTheConfinement()
    {
        // The cut faces read the free-expansion radius (so the density profile's speed) back from a drawn radius.
        foreach (double w in new[] { 0.0, 0.3, 1.0 })
        foreach (double frac in new[] { 0.1, 0.5, 0.6, 0.8, 1.0 })
        {
            double v = frac * VMax;
            double r = ParticleKinematics.RadiusCm(v, Since, Rstar, VMax, Shocks(w));
            Assert.Equal(1, ParticleKinematics.FreeRadiusCm(r, Since, Rstar, VMax, Shocks(w)) / (frac * K), 12);
        }
        Assert.Equal(0.3, ParticleKinematics.FreeRadiusCm(0.3 * K, Since, Rstar, VMax, null) / K, 12);
    }

    [Fact]
    public void Confinement_WithTheReverseShockAtTheCentre_PacksEveryParticleInsideTheContact()
    {
        var shocks = new ShockRadii(K, 0.5 * K, 0);
        Assert.Equal(0, ParticleKinematics.RadiusCm(0, Since, Rstar, VMax, shocks));
        Assert.Equal(0.25, ParticleKinematics.RadiusCm(0.5 * VMax, Since, Rstar, VMax, shocks) / K, 12);
        Assert.Equal(0.5, ParticleKinematics.RadiusCm(VMax, Since, Rstar, VMax, shocks) / K, 12);
    }
}
