using SupernovaLab.Core.Text;

namespace SupernovaLab.Tests;

public class ComparisonsTests
{
    [Fact]
    public void MinusFourPointFive_IsAsBrightAsVenus()
        => Assert.Equal("as bright as Venus", Comparisons.ForApparentMagnitude(-4.5));

    [Theory]
    // Checked in order (the brief's ranges overlap): < −12.7 Moon; −4.9…−3.5 Venus; < −4 daylight; −1.5…1.5 brightest
    // stars; ≤ 6 naked eye; ≤ 9 binoculars; else telescope.
    [InlineData(-26.7, "brighter than the full Moon")]
    [InlineData(-12.71, "brighter than the full Moon")]
    [InlineData(-12.7, "visible in daylight")]
    [InlineData(-4.91, "visible in daylight")]
    [InlineData(-4.9, "as bright as Venus")]
    [InlineData(-4.0, "as bright as Venus")]
    [InlineData(-3.5, "as bright as Venus")]
    [InlineData(-3.49, "visible to the naked eye")]
    [InlineData(-1.51, "visible to the naked eye")]
    [InlineData(-1.5, "as bright as the brightest stars")]
    [InlineData(0, "as bright as the brightest stars")]
    [InlineData(1.5, "as bright as the brightest stars")]
    [InlineData(1.51, "visible to the naked eye")]
    [InlineData(6, "visible to the naked eye")]
    [InlineData(6.01, "needs binoculars")]
    [InlineData(9, "needs binoculars")]
    [InlineData(9.01, "needs a telescope")]
    [InlineData(25, "needs a telescope")]
    public void Boundaries(double m, string expected) => Assert.Equal(expected, Comparisons.ForApparentMagnitude(m));

    [Fact]
    public void NaN_MeansNoVisibleLight() => Assert.Equal("no visible light", Comparisons.ForApparentMagnitude(double.NaN));
}
