using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>
/// Plan ruling P8: the shell impostors integrate an analytic column density along each line of sight. <see cref="ColumnDensity"/>
/// is the C# mirror of <c>shell.frag</c>; b is the impact parameter (distance of the line of sight from the centre).
/// </summary>
public class ColumnDensityTests
{
    [Fact]
    public void ColumnDensity_Shell_AtCentre_IsTwiceTheThickness()
    {
        Assert.Equal(2 * (1.0 - 0.8), ColumnDensity.Shell(0, 0.8, 1.0), 12);
        // Off centre, inside the hole: 2(sqrt(1 - b^2) - sqrt(0.64 - b^2)); b = 0.6: 2(0.8 - sqrt(0.28)) = 0.5416995.
        Assert.Equal(2 * (0.8 - Math.Sqrt(0.28)), ColumnDensity.Shell(0.6, 0.8, 1.0), 12);
        // Through the shell only (b between the radii): the full chord 2 sqrt(1 - b^2); b = 0.9: 2 sqrt(0.19).
        Assert.Equal(2 * Math.Sqrt(0.19), ColumnDensity.Shell(0.9, 0.8, 1.0), 12);
        Assert.Equal(0, ColumnDensity.Shell(1.0, 0.8, 1.0));
        Assert.Equal(0, ColumnDensity.Shell(1.5, 0.8, 1.0));
    }

    [Fact]
    public void ColumnDensity_Shell_IsLimbBrightened()
    {
        // The brightest line of sight grazes the inner surface: 2 sqrt(r_out^2 - r_in^2) against 2(r_out - r_in) at the centre.
        double centre = ColumnDensity.Shell(0, 0.8, 1.0), limb = ColumnDensity.Shell(0.8, 0.8, 1.0);
        Assert.Equal(2 * 0.6, limb, 12);
        Assert.Equal(3.0, limb / centre, 12);
        Assert.Equal(limb, ColumnDensity.ShellPeak(0.8, 1.0), 12);
    }

    [Fact]
    public void ColumnDensity_Ball_AtCentre_IsDiameter()
    {
        Assert.Equal(2.0, ColumnDensity.Ball(0, 1.0), 12);
        Assert.Equal(2 * Math.Sqrt(1 - 0.25), ColumnDensity.Ball(0.5, 1.0), 12);
        Assert.Equal(0, ColumnDensity.Ball(1.2, 1.0));
    }

    [Theory]
    [InlineData(0.0, 0.2, 1.0)]
    [InlineData(0.1, 0.2, 1.0)]
    [InlineData(0.5, 0.2, 1.0)]
    [InlineData(0.95, 0.2, 1.0)]
    [InlineData(1e-7, 0.3, 2.0)]
    public void ColumnDensity_Wind_MatchesNumericalIntegral(double b, double rIn, double rOut)
    {
        // rho = 1/r^2 between r_in and r_out, integrated along the chord with a 1,000-step midpoint rule.
        double zMax = Math.Sqrt(rOut * rOut - b * b), sum = 0;
        const int steps = 1000;
        // The integrand is sharp near z = 0 when b < r_in is small, so integrate each side's shell part separately.
        double zIn = Math.Sqrt(Math.Max(rIn * rIn - b * b, 0));
        double dz = (zMax - zIn) / steps;
        for (int i = 0; i < steps; i++)
        {
            double z = zIn + (i + 0.5) * dz;
            sum += dz / (b * b + z * z);
        }
        double expected = 2 * sum;
        Assert.Equal(1, ColumnDensity.Wind(b, rIn, rOut) / expected, 3);
    }

    [Fact]
    public void ColumnDensity_Wind_AtCentre_Is2TimesInverseRadiiDifference()
    {
        // b -> 0: 2 (1/r_in - 1/r_out).
        Assert.Equal(2 * (1 / 0.2 - 1 / 1.0), ColumnDensity.Wind(0, 0.2, 1.0), 9);
        Assert.Equal(0, ColumnDensity.Wind(1.0, 0.2, 1.0));
    }

    [Theory]
    [InlineData(ShellKind.Shell)]
    [InlineData(ShellKind.Ball)]
    [InlineData(ShellKind.Wind)]
    public void ColumnDensity_Segment_SumsToTheFullChord(ShellKind kind)
    {
        // The cutaway removes part of each line of sight: the segment integral over (-inf, s) plus (s, inf) is the whole chord,
        // and by symmetry each half is half of it.
        double b = 0.4, rIn = 0.7, rOut = 1.0;
        double full = ColumnDensity.Full(kind, b, rIn, rOut);
        Assert.Equal(full, ColumnDensity.Segment(kind, b, rIn, rOut, double.NegativeInfinity, double.PositiveInfinity), 12);
        Assert.Equal(full / 2, ColumnDensity.Segment(kind, b, rIn, rOut, double.NegativeInfinity, 0), 12);
        foreach (double s in new[] { -0.9, -0.5, 0.1, 0.65, 2 })
            Assert.Equal(full, ColumnDensity.Segment(kind, b, rIn, rOut, double.NegativeInfinity, s)
                               + ColumnDensity.Segment(kind, b, rIn, rOut, s, double.PositiveInfinity), 12);
        Assert.Equal(0, ColumnDensity.Segment(kind, b, rIn, rOut, 0.5, 0.2));   // an empty interval
    }

    [Fact]
    public void ColumnDensity_Segment_OfAShell_IsTheLengthInsideIt()
    {
        // b = 0 through a shell 0.7..1: the shell occupies s in [-1, -0.7] and [0.7, 1]; [0, 0.8] holds 0.1 of it.
        Assert.Equal(0.1, ColumnDensity.Segment(ShellKind.Shell, 0, 0.7, 1, 0, 0.8), 12);
        Assert.Equal(0.4, ColumnDensity.Segment(ShellKind.Shell, 0, 0.7, 1, -0.8, 2), 12);
    }
}
