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
    public void HoldFraction_EveryPullBackCycle_MeetsTheCriterion(double decadesPerSecond, double minimum)
    {
        double worst = double.MaxValue, shortest = double.MaxValue;
        var ratios = new HashSet<double>();
        // Start offsets over a full decade of ring values, so the x5, x5 and x4 step ratios all occur.
        for (int k = 0; k < 24; k++)
        {
            double r0 = 3e10 * Math.Pow(10, k / 24.0);
            foreach (var (hold, ratio) in CycleHolds(decadesPerSecond, r0))
            {
                if (ratio < 3.9) { shortest = Math.Min(shortest, hold); continue; } // unit-boundary skips (e.g. 1e8 km to 1 AU), see report
                worst = Math.Min(worst, hold);
                ratios.Add(Math.Round(ratio));
            }
        }
        output.WriteLine($"worst per-cycle hold {worst:0.0000} at {decadesPerSecond:0.###} decades/s; step ratios {string.Join(",", ratios.OrderBy(x => x))}; worst boundary-skip cycle {shortest:0.0000}");
        Assert.Contains(4.0, ratios);
        Assert.Contains(5.0, ratios);
        Assert.True(worst >= minimum, $"worst cycle hold {worst:0.000} at {decadesPerSecond} decades/s");
        // Ruling R-P2-8: every cycle, unit-boundary steps included, holds at least 2/3 at the default playback speed.
        if (decadesPerSecond < 0.5)
            Assert.True(Math.Min(worst, shortest) >= 2.0 / 3, $"worst cycle hold (all steps) {Math.Min(worst, shortest):0.000} at {decadesPerSecond} decades/s");
    }

    /// <summary>
    /// Plays back at a log-time rate with radius ∝ t over four decades, 1/60 s frames. Returns the fraction of frames not
    /// easing in each complete cycle (from one step-out to the next) and that cycle's step ratio.
    /// </summary>
    private static List<(double Hold, double Ratio)> CycleHolds(double decadesPerSecond, double r0)
    {
        const double dt = 1.0 / 60;
        var d = new CameraDirector();
        d.Jump(r0);
        var result = new List<(double, double)>();
        double logR = Math.Log10(r0), end = logR + 4;
        double ringBefore = d.OuterRingCm, cycleStartRing = 0;
        int frames = 0, holding = 0;
        bool started = false;
        while (logR < end)
        {
            logR += decadesPerSecond * dt;
            bool was = d.IsEasing;
            d.Update(Math.Pow(10, logR), dt);
            if (!was && d.IsEasing)
            {
                if (started) result.Add(((double)holding / frames, ringBefore / cycleStartRing));
                started = true;
                cycleStartRing = ringBefore;
                frames = 0; holding = 0;
                ringBefore = d.OuterRingCm;
            }
            frames++;
            if (!d.IsEasing) holding++;
        }
        return result;
    }

    [Theory]
    [InlineData(1e9)]
    [InlineData(1e12)]
    [InlineData(1e15)]
    [InlineData(1e17)]
    [InlineData(1e19)]
    public void Rungs_OuterRingIsTheDirectorsRing_AcrossUnitBoundaries(double startCm)
    {
        // Jump sweep, then slow playback outward and back, across the km/AU, AU/light-year, light-year/pc boundaries.
        for (int k = 0; k < 120; k++)
        {
            var j = new CameraDirector();
            j.Jump(startCm * Math.Pow(10, k / 60.0));
            AssertOuterRing(j);
        }
        var d = new CameraDirector();
        d.Jump(startCm);
        AssertOuterRing(d);
        double logR = Math.Log10(startCm);
        for (int dir = 0; dir < 2; dir++)
            for (int i = 0; i < 4000; i++)
            {
                logR += (dir == 0 ? 1 : -1) * 0.001;
                d.Update(Math.Pow(10, logR), 1.0);
                AssertOuterRing(d);
            }
    }

    [Fact]
    public void Rungs_OuterRingIsTheDirectorsRing_AtTheNamedThresholds()
    {
        // Frame radii that put H just below and just above 1 AU, 1e4 AU and 1 pc (and the km path).
        foreach (double edge in new[] { Au, 1e4 * Au, PhysicalConstants.Pc, 1e5 })
            foreach (double f in new[] { 0.3, 0.5, 0.54, 0.55, 0.6, 0.7, 0.9, 1.0, 1.2, 2.0 })
            {
                var d = new CameraDirector();
                d.Jump(edge * f);
                AssertOuterRing(d);
            }
    }

    private static void AssertOuterRing(CameraDirector d)
    {
        if (d.IsEasing) return;
        var rungs = DistanceRings.Rungs(d.HalfHeightCm);
        Assert.NotEmpty(rungs);
        Assert.Equal(d.OuterRingCm, rungs[0].RadiusCm, d.OuterRingCm * 1e-9);
        Assert.Equal(1f, rungs[0].Opacity);
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
