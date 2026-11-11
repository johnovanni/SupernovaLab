using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Timeline;

namespace SupernovaLab.Tests;

public class TimeAxisTests
{
    [Theory]
    [InlineData(1e-3)]
    [InlineData(0.0123)]
    [InlineData(1)]
    [InlineData(86400)]
    [InlineData(3.156e10)]
    public void FromFraction_InvertsToFraction(double t)
    {
        double back = TimeAxis.FromFraction(TimeAxis.ToFraction(t));
        Assert.True(Math.Abs(back - t) <= 1e-12 * t, $"{t} came back as {back}");
    }

    [Fact]
    public void LogGrid_Has400LogSpacedPoints_FromMinToMax()
    {
        double[] g = TimeAxis.LogGrid();
        Assert.Equal(400, g.Length);
        Assert.Equal(TimeAxis.MinS, g[0]);
        Assert.Equal(TimeAxis.MaxS, g[399]);
        // Equal steps in log10: (log10 3.156e10 − log10 1e-3) / 399 = (10.49914 + 3) / 399 = 0.0338324 per step.
        for (int i = 1; i < g.Length; i++)
            Assert.Equal(0.0338324, Math.Log10(g[i] / g[i - 1]), 6);
    }

    [Fact]
    public void Fraction_IsZeroAndOneAtTheEnds_AndClampedOutside()
    {
        Assert.Equal(0, TimeAxis.ToFraction(TimeAxis.MinS), 12);
        Assert.Equal(1, TimeAxis.ToFraction(TimeAxis.MaxS), 12);
        Assert.Equal(0, TimeAxis.ToFraction(0));
        Assert.Equal(0, TimeAxis.ToFraction(-5));
        Assert.Equal(1, TimeAxis.ToFraction(1e12));
        Assert.Equal(TimeAxis.MinS, TimeAxis.FromFraction(-0.5));
        Assert.Equal(TimeAxis.MaxS, TimeAxis.FromFraction(2));
    }

    [Fact]
    public void ToSource_DividesByOnePlusZ()
    {
        Assert.Equal(100, TimeAxis.ToSource(100, ObserverSetup.Default)); // z = 0 below 50 Mpc
        var far = new ObserverSetup(1e9, 0, true);
        Assert.True(far.Redshift > 0.1);
        Assert.Equal(100 / (1 + far.Redshift), TimeAxis.ToSource(100, far), 12);
    }
}
