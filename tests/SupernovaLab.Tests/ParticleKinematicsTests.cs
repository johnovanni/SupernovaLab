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
}
