using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;
using Xunit;

namespace SupernovaLab.Tests;

public class CameraDirectorTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private const double Au = PhysicalConstants.Au;

    [Fact]
    public void Jump_PicksSmallestRungAboveFrameOver0_6()
    {
        var d = new CameraDirector();
        d.Jump(3 * Au);
        Assert.Equal(5 * Au, d.OuterRingCm, 3);
        Assert.Equal(5 / 0.9 * Au, d.HalfHeightCm, 1e-9 * Au);
        Assert.False(d.IsEasing);
    }

    [Fact]
    public void Update_StepsOutWhenTheDebrisReachTheOuterRing_ThenEases()
    {
        var d = new CameraDirector();
        d.Jump(3 * Au);
        d.Update(5.0 * Au, 0.0);
        Assert.True(d.IsEasing);
        Assert.Equal(20 * Au, d.OuterRingCm, 3);
        d.Update(5.0 * Au, 0.15);
        double from = Math.Log(5 / 0.9 * Au), to = Math.Log(20 / 0.9 * Au);
        double mid = (Math.Log(d.HalfHeightCm) - from) / (to - from);
        Assert.InRange(mid, 0.4, 0.6);
        d.Update(5.0 * Au, 0.15);
        Assert.Equal(20 / 0.9 * Au, d.HalfHeightCm, 1e-9 * Au);
        Assert.False(d.IsEasing);
    }

    [Fact]
    public void Update_StepsInBelow0_8OfTwoRungsDown_WithHysteresis()
    {
        var d = new CameraDirector();
        d.Jump(10 * Au); // ring 20 AU
        Assert.Equal(20 * Au, d.OuterRingCm, 3);
        d.Update(4.1 * Au, 1.0);
        Assert.Equal(20 * Au, d.OuterRingCm, 3);
        d.Update(3.9 * Au, 1.0);
        Assert.Equal(5 * Au, d.OuterRingCm, 3);
        d.Update(4.5 * Au, 1.0);
        Assert.Equal(5 * Au, d.OuterRingCm, 3);
    }

    [Fact]
    public void Update_MidEaseStepStartsFromTheCurrentHalfHeight()
    {
        var d = new CameraDirector();
        d.Jump(3 * Au);
        d.Update(5.0 * Au, 0.15);
        double h = d.HalfHeightCm;
        d.Update(20.0 * Au, 0.0);
        Assert.Equal(h, d.HalfHeightCm, 3);
        Assert.True(d.IsEasing);
    }

    [Theory]
    [InlineData(1.0 / 3, 0.80)]
    [InlineData(0.675, 0.66)]
    public void HoldFraction_AtPlaybackSpeed_MeetsTheCriterion(double decadesPerSecond, double minimum)
    {
        double hold = HoldFraction(decadesPerSecond);
        output.WriteLine($"hold fraction {hold:0.0000} at {decadesPerSecond} decades/s");
        Assert.True(hold >= minimum, $"hold fraction {hold:0.000} at {decadesPerSecond} decades/s");
    }

    /// <summary>Playback from 1 day to 1 yr at a given log-time rate, radius ∝ t, 1/60 s frames.</summary>
    internal static double HoldFraction(double decadesPerSecond)
    {
        const double day = 86400, v = 1e9, dt = 1.0 / 60;
        double decades = Math.Log10(365.25), logT = 0;
        var d = new CameraDirector();
        d.Jump(v * day);
        int frames = 0, holding = 0;
        while (logT < decades)
        {
            logT += decadesPerSecond * dt;
            d.Update(v * day * Math.Pow(10, logT), dt);
            frames++;
            if (!d.IsEasing) holding++;
        }
        return (double)holding / frames;
    }

    [Fact]
    public void Update_WithNaNOrZeroRadius_KeepsTheLastScale()
    {
        var d = new CameraDirector();
        d.Jump(3 * Au);
        double h = d.HalfHeightCm;
        d.Update(double.NaN, 0.1);
        d.Update(0, 0.1);
        d.Update(-1, 0.1);
        d.Update(double.PositiveInfinity, 0.1);
        Assert.Equal(h, d.HalfHeightCm);
        Assert.False(d.IsEasing);
        Assert.Equal(5 * Au, d.OuterRingCm, 3);
    }
}
