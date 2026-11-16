using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>Task 20 (#71): the debris, seen from the remnant, stays opaque for months after the shock breaks out.</summary>
public class DebrisOpacityTests
{
    private const double Day = PhysicalConstants.Day;

    private static SupernovaLab.Core.Outcomes.Outcome Of(string id)
    {
        var p = PresetCatalog.Get(id);
        return TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
    }

    [Fact]
    public void ATypeIIP_IsOpaqueThroughItsPlateau_AndClearByTwiceThat()
    {
        var o = Of("sn2023ixf");
        double plateau = LightCurve.Of(o)!.PlateauEndS;
        Assert.InRange(plateau / Day, 60, 200);
        double opaque = DebrisOpacity.OpaqueUntilS(o);
        Assert.Equal(Math.Max(plateau, 100 * Day), opaque);
        Assert.Equal(1, DebrisOpacity.VeilAt(o, EjectaKinematics.BreakoutTimeS(o) * 1.01));   // just after breakout: still opaque
        Assert.Equal(1, DebrisOpacity.VeilAt(o, opaque));
        Assert.InRange(DebrisOpacity.VeilAt(o, opaque * Math.Sqrt(2)), 0.45, 0.55);           // halfway in log time
        Assert.Equal(0, DebrisOpacity.VeilAt(o, 2 * opaque));
        Assert.Equal(0, DebrisOpacity.VeilAt(o, 10 * 365 * Day));
    }

    [Fact]
    public void TheVeil_NeverRises_AsTimeGoesOn()
    {
        var o = Of("sn1054");
        double prev = 1;
        for (double t = 1; t < 1e9; t *= 1.1)
        {
            double v = DebrisOpacity.VeilAt(o, t);
            Assert.InRange(v, 0, prev + 1e-12);
            prev = v;
        }
    }

    [Fact]
    public void WithoutAPlateau_ItIsOpaqueFor100Days()
    {
        var o = Of("sn2011fe");   // a Type Ia
        Assert.Equal(100 * Day, DebrisOpacity.OpaqueUntilS(o));
        Assert.Equal(200 * Day, DebrisOpacity.ClearByS(o));
    }

    [Fact]
    public void WithoutEjecta_ThereIsNoVeil()
    {
        var o = Of("n6946bh1");   // a failed supernova: the star collapses whole
        Assert.True(double.IsNaN(DebrisOpacity.OpaqueUntilS(o)));
        Assert.Equal(0, DebrisOpacity.VeilAt(o, 30 * Day));
    }
}
