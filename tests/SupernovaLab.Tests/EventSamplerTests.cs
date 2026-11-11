using SupernovaLab.Core.Signals;

namespace SupernovaLab.Tests;

public class EventSamplerTests
{
    [Fact]
    public void SameSeed_GivesTheSameTimes_AndADifferentSeedDoesNot()
    {
        Func<double, double> rate = t => 50 * Math.Exp(-t / 3);
        var a = EventSampler.SampleTimes(rate, 0, 10, 7);
        var b = EventSampler.SampleTimes(rate, 0, 10, 7);
        var c = EventSampler.SampleTimes(rate, 0, 10, 8);
        Assert.NotEmpty(a);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void ConstantRate_100PerSecondOver10s_Gives1000Events()
    {
        // Poisson mean 1000, σ ≈ 32: the ±100 band is about 3σ.
        var times = EventSampler.SampleTimes(_ => 100, 0, 10, 1);
        Assert.InRange(times.Count, 900, 1100);
    }

    [Fact]
    public void Times_AreSorted_AndInsideTheWindow()
    {
        var times = EventSampler.SampleTimes(t => 200 / (1 + t), 2, 12, 3);
        Assert.NotEmpty(times);
        for (int i = 0; i < times.Count; i++)
        {
            Assert.InRange(times[i], 2, 12);
            if (i > 0) Assert.True(times[i] >= times[i - 1]);
        }
    }

    [Fact]
    public void ANarrowPeak_IsSampledAtItsFullRate()
    {
        // A 2.5 ms wide Gaussian (like the neutronization burst) inside a 100 s window. Its integral is
        // 5e4 × 0.0025 × √(2π) = 313.3 events, less the 2.3% before t = 0 (2σ): 306. Poisson σ ≈ 17.5; allow ±4σ.
        Func<double, double> rate = t => 5e4 * Math.Exp(-Math.Pow(t - 0.005, 2) / (2 * 0.0025 * 0.0025));
        var times = EventSampler.SampleTimes(rate, 0, 100, 11);
        Assert.InRange(times.Count, 236, 376);
    }

    [Fact]
    public void ZeroOrNaNRate_AndAnEmptyWindow_GiveNoEvents()
    {
        Assert.Empty(EventSampler.SampleTimes(_ => 0, 0, 10, 1));
        Assert.Empty(EventSampler.SampleTimes(_ => double.NaN, 0, 10, 1));
        Assert.Empty(EventSampler.SampleTimes(_ => 100, 5, 5, 1));
        Assert.Empty(EventSampler.SampleTimes(_ => 100, 5, 1, 1));
    }
}
