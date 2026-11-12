using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

public class BolometricCorrectionTests
{
    [Fact]
    public void AbsoluteBolometricMagnitude_UsesTheIauZeroPoint()
    {
        // IAU 2015 B2: M_bol = −2.5 log10(L / 3.0128e35); the Sun (3.828e33) is 2.5 log10(78.7042) = 4.7400.
        Assert.Equal(0, BolometricCorrection.AbsoluteBolometricMagnitude(3.0128e35), 1e-12);
        Assert.Equal(4.7400, BolometricCorrection.AbsoluteBolometricMagnitude(PhysicalConstants.LSun), 1e-4);
        Assert.True(double.IsNaN(BolometricCorrection.AbsoluteBolometricMagnitude(0)));
    }

    [Fact]
    public void LymanFits_MatchTheTablesByHand()
    {
        // Stripped envelope, B−V = 0.5: −0.083 − 0.139 × 0.5 − 0.691 × 0.25 = −0.32525.
        // Type II, B−V = 1.0: −0.138 − 0.013 − 0.649 = −0.800.
        Assert.Equal(-0.32525, BolometricCorrection.LymanBcB(BcSample.StrippedEnvelope, 0.5), 1e-12);
        Assert.Equal(-0.800, BolometricCorrection.LymanBcB(BcSample.TypeII, 1.0), 1e-12);
        Assert.Equal((0.0, 1.3), BolometricCorrection.LymanColourRange(BcSample.StrippedEnvelope));
        Assert.Equal((0.0, 1.6), BolometricCorrection.LymanColourRange(BcSample.TypeII));
        // Inside the range BC_V = BC_B + (B−V): 0.5 − 0.32525 = 0.17475.
        Assert.Equal(0.17475, BolometricCorrection.BcV(BcSample.StrippedEnvelope, 0.5), 1e-12);
    }

    [Fact]
    public void LiraRelation_MatchesPhillips1999()
    {
        // B−V = 0.725 − 0.0118 (t_V − 60): 0.725 at 60 d, 1.079 at 30 d, 0.371 at 90 d.
        Assert.Equal(0.725, BolometricCorrection.LiraBMinusV(60), 1e-12);
        Assert.Equal(1.079, BolometricCorrection.LiraBMinusV(30), 1e-12);
        Assert.Equal(0.371, BolometricCorrection.LiraBMinusV(90), 1e-12);
    }

    [Fact]
    public void Blackbody_ColourAndCorrection_AreSunLike_AndInvert()
    {
        // By hand, monochromatic at 4,380 and 5,450 Å with Bessell et al.'s zero points: a 5,772 K blackbody has
        // B−V = 0.6362 (the Sun: 0.65) and BC_V = −0.1213 (the Sun: about −0.07); 10,000 K: 0.1547 and −0.3393.
        Assert.Equal(0.6362, BolometricCorrection.BlackbodyBMinusV(5772), 1e-4);
        Assert.Equal(-0.1213, BolometricCorrection.BlackbodyBcV(5772), 1e-4);
        Assert.Equal(0.1547, BolometricCorrection.BlackbodyBMinusV(10_000), 1e-4);
        Assert.Equal(-0.3393, BolometricCorrection.BlackbodyBcV(10_000), 1e-4);
        Assert.Equal(20_000, BolometricCorrection.BlackbodyTemperatureForBMinusV(BolometricCorrection.BlackbodyBMinusV(20_000)), 0.01);
    }

    [Fact]
    public void OutsideTheFittedColours_TheCorrectionContinuesSmoothly()
    {
        // Continuous at the blue edge (B−V = 0), and hot colours put light into the ultraviolet, out of V: a 20,000 K
        // blackbody (B−V = −0.1304) has BC_V lower than the edge's by the blackbody's change from the 13,503 K blackbody
        // whose B−V is 0: −1.6594 − (−0.8147) = −0.8447, by hand.
        foreach (var s in new[] { BcSample.StrippedEnvelope, BcSample.TypeII })
        {
            double edge = BolometricCorrection.BcV(s, 0);
            Assert.Equal(edge, BolometricCorrection.BcV(s, -1e-9), 1e-6);
            Assert.Equal(-0.8447, BolometricCorrection.BcV(s, BolometricCorrection.BlackbodyBMinusV(20_000)) - edge, 1e-3);
        }
    }
}
