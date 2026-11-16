using SupernovaLab.App.Services;
using SupernovaLab.Core.Compact;
using static SupernovaLab.Tests.CompactResearch;

namespace SupernovaLab.Tests;

/// <summary>Plan Task 12 (Review Focus 5): <see cref="FlashLimiter"/> keeps a giant flare to one rise per half second of wall time.</summary>
public class FlashLimiterTests
{
    // ---- The flash limiter (Review Focus 5) ----

    [Fact]
    public void FlashLimiter_RapidScrubbing_AtMostTwoRisesPerSecond()
    {
        var f = new GiantFlare(1e8, 2e46, 2e46 / C6.SpikeS, C6.SpikeS);
        var lim = new FlashLimiter();
        var outputs = new List<double>();
        // Scrubbing back and forth across the flare ten times a second for three seconds, drawn at 60 frames a second.
        for (int i = 0; i < 180; i++)
        {
            double wall = i / 60.0;
            double phase = Math.Floor(wall * 10) % 2;               // 10 Hz square-wave scrub: at the peak, then far from the flare
            double t = phase == 0 ? f.SourceTimeS : f.SourceTimeS * 100;
            outputs.Add(lim.Apply(MagnetarFlares.DrawnIntensity(f, t), wall));
        }
        Assert.All(outputs, v => Assert.InRange(v, 0, 1));
        Assert.True(CountRises(outputs) <= 6, $"{CountRises(outputs)} rises in 3 s");
        Assert.True(CountRises(outputs) >= 1);
    }

    [Fact]
    public void FlashLimiter_RapidTriangleScrubbing_AtMostTwoRisesPerSecond()
    {
        var f = new GiantFlare(1e8, 2e46, 2e46 / C6.SpikeS, C6.SpikeS);
        var lim = new FlashLimiter();
        var outputs = new List<double>();
        for (int i = 0; i < 600; i++)                                // 200 Hz sampling, a 8 Hz triangle sweep over the flare
        {
            double wall = i / 200.0;
            double x = Math.Abs(((wall * 8) % 2) - 1) * 0.5 - 0.2;   // −0.2..0.3 decades about the flare
            outputs.Add(lim.Apply(MagnetarFlares.DrawnIntensity(f, f.SourceTimeS * Math.Pow(10, x)), wall));
        }
        Assert.True(CountRises(outputs) <= 6, $"{CountRises(outputs)} rises in 3 s");
    }

    [Fact]
    public void FlashLimiter_AFlareAtNormalSpeed_PassesWholeAndSmooth()
    {
        var f = new GiantFlare(1e8, 2e46, 2e46 / C6.SpikeS, C6.SpikeS);
        var lim = new FlashLimiter();
        var outputs = new List<double>();
        // Playback at the default speed: the flare crosses in about 0.5 s of wall time; 60 frames a second.
        for (int i = 0; i < 90; i++)
        {
            double wall = i / 60.0;
            double decades = (wall - 0.3) * MagnetarFlares.ReferenceDecadesPerSecond;
            outputs.Add(lim.Apply(MagnetarFlares.DrawnIntensity(f, f.SourceTimeS * Math.Pow(10, decades)), wall));
        }
        Assert.Equal(1, CountRises(outputs));
        Assert.True(outputs.Max() > 0.9);
        Assert.True(outputs.Max() <= 1);
    }

    [Fact]
    public void FlashLimiter_ASuddenJump_ClimbsOverAtLeastAFifthOfASecond()
    {
        var lim = new FlashLimiter();
        double first = lim.Apply(0, 0);
        Assert.Equal(0, first);
        double t = 1;
        double v = lim.Apply(1, t);          // jumps to the peak in one frame
        Assert.True(v < 1);
        int frames = 1;
        while (v < 1 && frames < 100) { t += 1.0 / 60; v = lim.Apply(1, t); frames++; }
        Assert.True(frames / 60.0 >= FlashLimiter.MinRiseS - 1e-9, $"{frames} frames");
    }

    [Fact]
    public void FlashLimiter_NonFiniteAndOutOfRangeInputsAreSafe()
    {
        var lim = new FlashLimiter();
        Assert.Equal(0, lim.Apply(double.NaN, 0));
        Assert.Equal(0, lim.Apply(-3, 0.1));
        Assert.InRange(lim.Apply(50, 1), 0, 1);
    }

    /// <summary>The number of separate rises: transitions from not rising to rising by more than a hair.</summary>
    private static int CountRises(IReadOnlyList<double> v)
    {
        int rises = 0;
        bool rising = false;
        for (int i = 1; i < v.Count; i++)
        {
            double d = v[i] - v[i - 1];
            if (d > 1e-3) { if (!rising) rises++; rising = true; }
            else if (d < -1e-3) rising = false;
        }
        return rises;
    }

}
