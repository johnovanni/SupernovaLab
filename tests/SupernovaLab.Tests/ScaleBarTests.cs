using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>Task 16: the 3D view's scale bar (spec §6: km, AU, light-years or parsecs).</summary>
public class ScaleBarTests
{
    private const double Km = 1e5, Au = 1.495978707e13, Ly = 2.99792458e10 * 3.156e7, Pc = 3.0857e18;

    [Theory]
    // max length = cm/px × 150 px; the bar is the largest 1, 2 or 5 × 10^k of the largest unit that fits once.
    [InlineData(1e6, "1,000 km", 1e8)]                 // 1,500 km fits
    [InlineData(2.5e7 * Km / 150, "20 million km", 2e12)] // 25 million km fits (under 1 AU)
    [InlineData(3 * Au / 150, "2 AU", 2 * Au)]
    [InlineData(8000 * Au / 150, "5,000 AU", 5000 * Au)]  // under 10,000 AU: AU, as the overlay's radius
    [InlineData(0.6 * Ly / 150, "0.5 light-years", 0.5 * Ly)] // 0.6 ly is 38,000 AU: light-years, as the overlay (review M6)
    [InlineData(1.5 * Ly / 150, "1 light-year", Ly)]
    [InlineData(3 * Ly / 150, "2 light-years", 2 * Ly)]
    [InlineData(120 * Pc / 150, "100 pc", 100 * Pc)]
    public void Choose_PicksTheLargestUnitAndARoundLength(double cmPerPx, string label, double lengthCm)
    {
        var bar = ScaleBar.Choose(cmPerPx, 150)!;
        Assert.Equal(label, bar.Label);
        Assert.Equal(lengthCm, bar.LengthCm, lengthCm * 1e-9);
        Assert.Equal(lengthCm / cmPerPx, bar.LengthPx, 1e-6);
        Assert.InRange(bar.LengthPx, 150 / 5.0, 150);
    }

    [Fact]
    public void Choose_NothingForABadScale()
    {
        Assert.Null(ScaleBar.Choose(double.NaN, 150));
        Assert.Null(ScaleBar.Choose(0, 150));
    }
}
