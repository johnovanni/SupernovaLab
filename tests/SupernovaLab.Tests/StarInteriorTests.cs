using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Structure;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>Task 7: the shock inside the star (structure §S5) and the star's layer radii (§S6).</summary>
public class StarInteriorTests
{
    private const double RSun = PhysicalConstants.RSun;
    private static readonly ShockBreakoutModel Breakout = new();

    /// <summary>A hydrogen-rich red supergiant (500 R☉, so the convective n = 3/2 row).</summary>
    private static Outcome Rsg() => TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.05, 500 * RSun);

    /// <summary>A hydrogen-rich blue supergiant (SN 1987A's B15 radius, 3.9e12 cm = 56 R☉: the radiative n = 3 row).</summary>
    private static Outcome Bsg() => TestOutcomes.Custom(SupernovaType.IIP, 1.5e51, 15, 0.07, 3.9e12);

    /// <summary>A stripped (hydrogen-free) star: a Type Ib on a compact helium star.</summary>
    private static Outcome Stripped() => TestOutcomes.Custom(SupernovaType.Ib, 1e51, 3, 0.07, 1e11);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ShockInStar_ReachesTheSurface_AtTheBreakoutModelsTime(int which)
    {
        var o = which switch { 0 => Rsg(), 1 => Bsg(), _ => Stripped() };
        double tbo = Breakout.ShockAtSurfaceS(o), r = o.Star!.RadiusCm.Value;

        Assert.True(double.IsFinite(tbo) && tbo > 0);
        Assert.Equal(r, StarInterior.ShockRadiusCm(o, tbo), r * 1e-9);
        Assert.Equal(0, StarInterior.ShockRadiusCm(o, 0));
        Assert.True(double.IsNaN(StarInterior.ShockRadiusCm(o, tbo * 1.0000001)));

        double previous = 0;
        for (int i = 1; i <= 200; i++)
        {
            double now = StarInterior.ShockRadiusCm(o, tbo * i / 200);
            Assert.True(now > previous, $"not increasing at step {i}");
            Assert.True(now <= r * (1 + 1e-12));
            previous = now;
        }
    }

    [Fact]
    public void ShockInStar_FollowsTheS5Table()
    {
        // structure §S5: R_s/R* at t/t_bo = 0.5 is 0.551 for n = 3/2 (RSG) and 0.433 for n = 3 (BSG, WR); 0.05 -> 0.110 and 0.079.
        var rsg = Rsg();
        var bsg = Bsg();
        double tRsg = Breakout.ShockAtSurfaceS(rsg), tBsg = Breakout.ShockAtSurfaceS(bsg);
        double rRsg = rsg.Star!.RadiusCm.Value, rBsg = bsg.Star!.RadiusCm.Value;
        Assert.Equal(0.551, StarInterior.ShockRadiusCm(rsg, 0.5 * tRsg) / rRsg, 9);
        Assert.Equal(0.110, StarInterior.ShockRadiusCm(rsg, 0.05 * tRsg) / rRsg, 9);
        Assert.Equal(0.433, StarInterior.ShockRadiusCm(bsg, 0.5 * tBsg) / rBsg, 9);
        Assert.Equal(0.079, StarInterior.ShockRadiusCm(bsg, 0.05 * tBsg) / rBsg, 9);
        // Linear between rows: tau = 0.75 for the RSG is the middle of 0.709 and 0.790.
        Assert.Equal(0.7495, StarInterior.ShockRadiusCm(rsg, 0.75 * tRsg) / rRsg, 9);
    }

    [Fact]
    public void ShockInStar_IsNaN_WhenThereIsNoShockToDraw()
    {
        Assert.True(double.IsNaN(StarInterior.ShockRadiusCm(TestOutcomes.TypeIa(), 1)));
        var failed = TestOutcomes.Custom(SupernovaType.None, null, null, null, 5e13, TestOutcomes.BlackHole(10, 1.5), Fate.FailedSupernova);
        Assert.True(double.IsNaN(StarInterior.ShockRadiusCm(failed, 1)));
    }

    [Fact]
    public void StarLayers_AreNested_AndEndAtTheStarsRadius()
    {
        // structure §S6: r_CO = min(4.2e9 cm, R*/2); r_He = min(3.6e10 cm, R*) for hydrogen-rich stars, R* for stripped ones.
        var rsg = StarInterior.Layers(Rsg());
        Assert.Equal(new[] { ElementLayer.CarbonOxygenCore, ElementLayer.Helium, ElementLayer.Hydrogen }, rsg.Select(l => l.Layer));
        Assert.Equal(4.2e9, rsg[0].OuterRadiusCm);
        Assert.Equal(3.6e10, rsg[1].OuterRadiusCm);
        Assert.Equal(500 * RSun, rsg[2].OuterRadiusCm);
        Assert.InRange(rsg[1].OuterRadiusCm / rsg[2].OuterRadiusCm, 9e-4, 1.2e-3);   // 3.6e10 / 3.5e13: about 1e-3 R* (§S6's RSG15 fraction is 7e-4 of 5e13)

        var bsg = StarInterior.Layers(Bsg());
        Assert.Equal(3.9e12, bsg[2].OuterRadiusCm);
        Assert.InRange(bsg[0].OuterRadiusCm / 3.9e12, 9.1e-4, 1.2e-3);   // §S6: CO 9.1e-4 R* for B15 (3.55e9 cm); 4.2e9 gives 1.08e-3
        Assert.InRange(bsg[1].OuterRadiusCm / 3.9e12, 8e-3, 1.8e-2);     // 3.6e10 / 3.9e12 = 9.2e-3; B15's measured He is 1.8e-2

        var stripped = StarInterior.Layers(Stripped());
        Assert.Equal(new[] { ElementLayer.CarbonOxygenCore, ElementLayer.Helium }, stripped.Select(l => l.Layer));
        Assert.Equal(4.2e9, stripped[0].OuterRadiusCm);
        Assert.Equal(1e11, stripped[1].OuterRadiusCm);

        foreach (var layers in new[] { rsg, bsg, stripped })
            for (int i = 1; i < layers.Count; i++)
                Assert.True(layers[i].OuterRadiusCm > layers[i - 1].OuterRadiusCm);
    }

    [Fact]
    public void StarLayers_CapTheCoreAtHalfAVerySmallStar_AndTheHeCoreAtTheStar()
    {
        var tiny = StarInterior.Layers(TestOutcomes.Custom(SupernovaType.Ic, 1e51, 2, 0.05, 4e9));
        Assert.Equal(2e9, tiny[0].OuterRadiusCm);   // min(4.2e9, 0.5 R*)
        Assert.Equal(4e9, tiny[^1].OuterRadiusCm);

        var small = StarInterior.Layers(TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.05, 2e10));
        Assert.Equal(2e10, small[1].OuterRadiusCm);   // r_He = min(3.6e10, R*): helium reaches the surface of a star smaller than that
        Assert.Equal(2e10, small[^1].OuterRadiusCm);
    }

    [Fact]
    public void StarLayers_AreEmpty_WithoutAStar()
        => Assert.Empty(StarInterior.Layers(TestOutcomes.TypeIa()));
}
