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
    public void Scientific_TakesAnyUnit() => Assert.Equal("4.2 × 10⁴² erg/s", UnitFormat.Scientific(4.2e42, "erg/s"));

    [Fact]
    public void Scientific_NegativeExponent() => Assert.Equal("1.5 × 10⁻⁴ M☉", UnitFormat.Scientific(1.5e-4, "M☉"));

    [Fact]
    public void Number_ThreeFiguresWithSeparators() => Assert.Equal("12,300", UnitFormat.Number(12_345));

    [Fact]
    public void Distance_KpcWithLightYears() => Assert.Equal("51 kpc (166,000 light-years)", UnitFormat.Distance(51 * 3.0857e21));

    [Fact]
    public void Time_TopOfTimeline_NoScientificNotation() => Assert.Equal("1,000 years", UnitFormat.Time(3.156e10));

    [Fact]
    public void Time_RoundsUpToNextUnit() => Assert.Equal("1 min", UnitFormat.Time(59.96));

    [Fact]
    public void Time_MicrosecondsRoundUpToMs() => Assert.Equal("1 ms", UnitFormat.Time(999.9e-6));

    [Fact]
    public void Distance_OneGpc()
        // 1e9 pc = 1,000 Mpc; 1e9 x 3.0857e18 / (2.99792458e10 x 3.156e7) = 3.2614e9 ly -> 3.26e9 (by hand).
        => Assert.Equal("1,000 Mpc (3,260,000,000 light-years)", UnitFormat.Distance(1e9 * 3.0857e18));

    [Fact]
    public void Time_OneDay_IsSingular() => Assert.Equal("1 day", UnitFormat.Time(86400));

    [Fact]
    public void Time_OneYear_IsSingular()
        // 3.16e7 s is 365.7 days, which rounds to 366, past the day unit's 365.28-day limit (Year / Day), so it is "1 year" (by hand).
        => Assert.Equal("1 year", UnitFormat.Time(3.16e7));

    [Fact]
    public void Time_ExactlyOneYear_ReadsOneYear_NotDays()
    {
        // Task 17: exactly 1 yr is 3.156e7 s = 365.28 days, which used to round to "365 days".
        Assert.Equal("1 year", UnitFormat.Time(PhysicalConstants.Year));
        Assert.Equal("1 year", UnitFormat.Time(3.156e7));
        Assert.Equal("364 days", UnitFormat.Time(364 * 86400));   // clearly under a year stays in days
        Assert.Equal("1.5 years", UnitFormat.Time(1.5 * 3.156e7));
        // Both sides of the boundary: 0.9995 yr (3.15442e7 s, 365.1 days) is the lowest value that rounds to 1 yr.
        Assert.Equal("365 days", UnitFormat.Time(365 * 86400));
        Assert.Equal("365 days", UnitFormat.Time(3.1543e7));      // just below the 0.05% band
        Assert.Equal("1 year", UnitFormat.Time(365.1 * 86400));
    }

    [Fact]
    public void Time_RoundingUpToOneDay_IsSingular() => Assert.Equal("1 day", UnitFormat.Time(86399.9));

    [Fact]
    public void Distance_StarSized_InKm()
        // A 43 R☉ star: 3e12 cm = 30,000,000 km (by hand), under 1 AU.
        => Assert.Equal("30,000,000 km", UnitFormat.Distance(3e12));

    [Fact]
    public void Distance_AFewAu()
        // 6.5e13 cm / 1.495978707e13 cm = 4.34498 AU (by hand), three figures 4.34.
        => Assert.Equal("4.34 AU", UnitFormat.Distance(6.5e13));

    [Fact]
    public void Distance_UnderALightYear_KeepsThreeFigures()
        // 0.77 pc = 2.376e18 cm; / 9.4614e17 cm per light-year = 2.511 ly (by hand).
        => Assert.Equal("0.77 pc (2.51 light-years)", UnitFormat.Distance(0.77 * 3.0857e18));

    [Fact]
    public void Mass_Thousand() => Assert.Equal("1,000 M☉", UnitFormat.Mass(1000));

    [Theory]
    [InlineData(0.31773, "0.318")]
    [InlineData(14.4855, "14.486")]
    [InlineData(7.94e-4, "7.94e-4")]
    [InlineData(2.5e4, "2.5e4")]
    public void Figure_IsPlainBetweenAHundredthAndTenThousand_ElseScientific(double v, string expected)
        => Assert.Equal(expected, UnitFormat.Figure(v));

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
