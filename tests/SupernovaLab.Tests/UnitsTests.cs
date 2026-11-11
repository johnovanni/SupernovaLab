using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

public class UnitsTests
{
    [Fact]
    public void Time_Milliseconds() => Assert.Equal("3 ms", UnitFormat.Time(0.003));

    [Fact]
    public void Time_Days() => Assert.Equal("40 days", UnitFormat.Time(86400 * 40));

    [Fact]
    public void Time_Years() => Assert.Equal("320 years", UnitFormat.Time(3.156e7 * 320));

    [Fact]
    public void Time_Hours() => Assert.Equal("2.5 h", UnitFormat.Time(2.5 * 3600));

    [Fact]
    public void Energy_UsesSuperscriptExponent() => Assert.Equal("1.2 × 10⁵¹ erg", UnitFormat.Energy(1.2e51));

    [Fact]
    public void Distance_KpcWithLightYears() => Assert.Equal("51 kpc (166,000 light-years)", UnitFormat.Distance(51 * 3.0857e21));

    [Fact]
    public void MSun_IsSignalsValue() => Assert.Equal(1.989e33, PhysicalConstants.MSun);

    [Fact]
    public void Quantity_DefaultsUnpublished()
    {
        var q = new Quantity(1, "erg", Confidence.Established, new Source("c", "r"));
        Assert.False(q.Published);
        Assert.Null(q.Low);
    }
}
