using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;
using Xunit;

namespace SupernovaLab.Tests;

public class DistanceRingsTests
{
    private const double Au = PhysicalConstants.Au;

    [Fact]
    public void Rungs_At5AuOuterRing_Are_5_2_1_AndHalfAu()
    {
        // Ruling R-P2-1: the outer rung sits at 0.9 H, so H = 5/0.9 AU.
        var rungs = DistanceRings.Rungs(5 / 0.9 * Au);
        Assert.Equal(new[] { "5 AU", "2 AU", "1 AU", "0.5 AU" }, rungs.Select(r => r.Label).ToArray());
        Assert.Equal(5 * Au, rungs[0].RadiusCm, 6);
        Assert.Equal(1f, rungs[0].Opacity);
        Assert.Equal(1f, rungs[1].Opacity);
        Assert.Equal(1f, rungs[2].Opacity);
        // 0.5 AU = 0.09 H: four fifths of the way up the fade from 0.05 H to 0.1 H.
        Assert.Equal(0.8f, rungs[3].Opacity, 3);
    }

    [Fact]
    public void Rungs_UseTheScaleBarsUnitSwitches()
    {
        var ly = DistanceRings.Rungs(2e4 * Au);
        Assert.Equal("0.2 light-years", ly[0].Label);
        var pc = DistanceRings.Rungs(2 * PhysicalConstants.Pc);
        Assert.EndsWith(" pc", pc[0].Label);
        Assert.Equal("1 pc", pc[0].Label);
    }

    [Fact]
    public void Rungs_FadeToNothingAtFiveHundredthsOfTheHalfHeight()
    {
        double h = 5 / 0.9 * Au;
        Assert.All(DistanceRings.Rungs(h), r => Assert.InRange(r.RadiusCm / h, 0.05, 0.9 + 1e-9));
    }

    [Fact]
    public void Rung_TwoUp_From1_2_5()
    {
        Assert.Equal(5 * Au, DistanceRings.Rung(1 * Au, 2, "AU"), 3);
        Assert.Equal(10 * Au, DistanceRings.Rung(2 * Au, 2, "AU"), 3);
        Assert.Equal(20 * Au, DistanceRings.Rung(5 * Au, 2, "AU"), 3);
        Assert.Equal(1 * Au, DistanceRings.Rung(5 * Au, -2, "AU"), 3);
        Assert.Equal(0.5 * Au, DistanceRings.Rung(2 * Au, -2, "AU"), 3);
    }
}
