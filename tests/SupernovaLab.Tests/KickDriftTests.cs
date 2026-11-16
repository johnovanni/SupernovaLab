using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>The neutron star's drift from its kick (spec §4.6; compact §C4 App, the position-angle mapping).</summary>
public class KickDriftTests
{
    private const double Year = PhysicalConstants.Year, Pc = PhysicalConstants.Pc;

    private static Outcome Of(string id)
    {
        var p = PresetCatalog.Get(id);
        return TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
    }

    private static readonly Outcome CasA = Of("casa"), Crab = Of("sn1054");
    private static readonly ObserverSetup Obs = ObserverSetup.Default;

    private static Outcome Kicked(double kmS)
        => TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.03, 3.5e13,
            TestOutcomes.NeutronStar(1.4) with { KickKmS = TestOutcomes.Q(kmS, "km/s") });

    [Fact]
    public void Kick_400kms_1000yr_About0p41pc()
        => Assert.InRange(KickDrift.OffsetCm(Kicked(400), Obs, 1, 1000 * Year).Length / Pc, 0.40, 0.42);

    [Fact]
    public void Kick_DirectionDependsOnlyOnRunNumber()
    {
        var o = Kicked(400);
        Assert.Equal(KickDrift.Direction(o, Obs, 3), KickDrift.Direction(o, Obs, 3));
        Assert.NotEqual(KickDrift.Direction(o, Obs, 3), KickDrift.Direction(o, Obs, 4));
        // ... and not on the outcome.
        Assert.Equal(KickDrift.Direction(o, Obs, 3), KickDrift.Direction(Kicked(100), Obs, 3));
        Assert.Equal(1, KickDrift.Direction(o, Obs, 3).Length, 12);
    }

    [Fact]
    public void Kick_SeededDirections_AreIsotropic()
    {
        // 4,000 runs: the mean vector is near zero, and each hemisphere gets about half.
        Vec3 sum = default;
        int up = 0;
        for (int run = 1; run <= 4000; run++)
        {
            var d = KickDrift.Direction(Kicked(400), Obs, run);
            sum += d;
            if (d.Z > 0) up++;
        }
        Assert.True(sum.Length / 4000 < 0.05, $"mean vector {sum.Length / 4000}");
        Assert.InRange(up, 1900, 2100);
    }

    [Fact]
    public void Kick_PresetDirection_LiesInSkyPlane()
    {
        foreach (var (o, obs) in new[] { (CasA, PresetCatalog.Get("casa").Observer), (Crab, PresetCatalog.Get("sn1054").Observer) })
        {
            var d = KickDrift.Direction(o, obs, 1);
            Assert.Equal(0, d.Dot(Magnetosphere.EarthDirection(o, obs)), 9);
            Assert.Equal(1, d.Length, 12);
        }
    }

    [Fact]
    public void Kick_PresetDirection_DoesNotDependOnTheRunNumber()
        => Assert.Equal(KickDrift.Direction(CasA, Obs, 1), KickDrift.Direction(CasA, Obs, 99));

    [Fact]
    public void Kick_Crab_MatchesTheResearchDirection()
    {
        // §C4 App: with the checkbox on, ζ = α = 62° and d = (+0.247, +0.455, +0.856).
        var d = KickDrift.Direction(Crab, PresetCatalog.Get("sn1054").Observer, 1);
        Assert.Equal(CompactResearch.C4.CrabKickDirection.X, d.X, 0.001);
        Assert.Equal(CompactResearch.C4.CrabKickDirection.Y, d.Y, 0.001);
        Assert.Equal(CompactResearch.C4.CrabKickDirection.Z, d.Z, 0.001);
    }

    [Fact]
    public void Kick_PositionAngleMapping_EastIsMinusX_NorthIsProjectedSpinAxis()
    {
        // θ = 0 is scene north N = (0, cos ζ, sin ζ); θ = 90° is scene east, −x (§C4 App).
        var o0 = Kicked(400) with { KickPositionAngleDeg = 0 };
        var o90 = Kicked(400) with { KickPositionAngleDeg = 90 };
        var obs = Obs with { BeamTowardEarth = true };
        double zeta = Magnetosphere.EarthAngleRad(o0, obs);
        var north = KickDrift.Direction(o0, obs, 1);
        Assert.Equal(0, north.X, 12);
        Assert.Equal(Math.Cos(zeta), north.Y, 12);
        Assert.Equal(Math.Sin(zeta), north.Z, 12);
        var east = KickDrift.Direction(o90, obs, 1);
        Assert.Equal(-1, east.X, 12);
        Assert.Equal(0, east.Y, 12);
        Assert.Equal(0, east.Z, 12);
    }

    [Fact]
    public void DirectCollapseBlackHole_NoKick()
    {
        var bh = TestOutcomes.Custom(SupernovaType.IIP, null, null, null, 3.5e13, TestOutcomes.BlackHole(10, 1), Fate.FailedSupernova);
        Assert.Equal(0, KickDrift.OffsetCm(bh, Obs, 1, 1e9).Length);
    }

    [Fact]
    public void FallbackBlackHole_DriftsByItsKick()
    {
        var r = TestOutcomes.BlackHole(10, 1) with { KickKmS = TestOutcomes.Q(50, "km/s") };
        var bh = TestOutcomes.Custom(SupernovaType.IIP, null, null, null, 3.5e13, r, Fate.FailedSupernova);
        Assert.InRange(KickDrift.OffsetCm(bh, Obs, 1, 1000 * Year).Length / (50e5 * 1000 * Year), 0.999999, 1.000001);
    }

    [Fact]
    public void Offset_IsZeroAtZeroTime_AndGrowsLinearly()
    {
        var o = Kicked(400);
        Assert.Equal(0, KickDrift.OffsetCm(o, Obs, 1, 0).Length);
        Assert.InRange(KickDrift.OffsetCm(o, Obs, 1, 200 * Year).Length / KickDrift.OffsetCm(o, Obs, 1, 100 * Year).Length, 1.999999, 2.000001);
    }

    [Fact]
    public void LineOfSightNote_StatesTheSkyPlaneAssumption()
        => Assert.Equal("Kick: direction measured on the sky; its line-of-sight part is unknown and set to zero", KickDrift.LineOfSightNote);

    [Fact]
    public void PresetPins_CarryTheirSources()
    {
        Assert.Equal(151, CasA.KickPositionAngleDeg);
        Assert.Contains("Holland-Ashford", CasA.KickPositionAngleSource!.Citation);
        Assert.Equal(-14.3, Crab.KickPositionAngleDeg);
        Assert.Contains("Kaplan", Crab.KickPositionAngleSource!.Citation);
        Assert.Null(Of("sn1987a").KickPositionAngleDeg);
        Assert.Null(Of("sn1987a").MagneticInclinationDeg);
        Assert.Null(CasA.MagneticInclinationDeg);   // no pulsations, so no inclination (§C4.3)
    }
}
