using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>The magnetosphere's geometry (spec §4.3; compact §C4; rulings R-P3-4, P3-3).</summary>
public class MagnetosphereTests
{
    private static readonly Outcome Default = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.03, 3.5e13);
    private static readonly Outcome Crab = Of("sn1054");

    private static Outcome Of(string id)
    {
        var p = PresetCatalog.Get(id);
        return TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
    }

    private static ObserverSetup Obs(bool beamTowardEarth) => ObserverSetup.Default with { BeamTowardEarth = beamTowardEarth };

    private static Outcome Jets()
        => TestOutcomes.Custom(SupernovaType.IcBL, 2.5e52, 6, 0.4, 1e11, TestOutcomes.BlackHole(5, 1), hasGrb: true);

    [Fact]
    public void DrawnPeriod_NeverUnder2s_CrabAbout60x()
    {
        Assert.Equal(2, Magnetosphere.DrawnPeriodS(0.0334));
        Assert.Equal(5, Magnetosphere.DrawnPeriodS(5));
        Assert.Equal("Shown 60× slower", Magnetosphere.SpeedLabel(0.0334));
        Assert.Equal("Shown at real speed", Magnetosphere.SpeedLabel(5));
        Assert.Equal("Shown at real speed", Magnetosphere.SpeedLabel(2));
        // Two significant figures: 2 / 0.0017 = 1176 shown as 1,200.
        Assert.Equal("Shown 1,200× slower", Magnetosphere.SpeedLabel(0.0017));
    }

    [Fact]
    public void LightCylinder_IsCPOver2Pi()
        => Assert.Equal(PhysicalConstants.C * 1.0 / (2 * Math.PI) / 1e5, Magnetosphere.LightCylinderKm(1.0), 6);

    [Theory]
    [InlineData(0.0334, CompactResearch.C4.CrabBeamDeg)]
    [InlineData(0.1, CompactResearch.C4.Beam0p1sDeg)]
    [InlineData(1, CompactResearch.C4.Beam1sDeg)]
    [InlineData(5, CompactResearch.C4.Beam5sDeg)]
    public void BeamHalfAngle_IsBiggs1990(double periodS, double expectedDeg)
        => Assert.Equal(expectedDeg, Magnetosphere.BeamHalfAngleRad(periodS) * 180 / Math.PI, 0.05);

    [Fact]
    public void BeamHalfAngle_CapsAt90Degrees()
    {
        Assert.Equal(Math.PI / 2, Magnetosphere.BeamHalfAngleRad(0.003), 12);   // 5.6° / √0.003 = 102°
        Assert.Equal(Math.PI / 2, Magnetosphere.BeamHalfAngleRad(0), 12);
    }

    [Fact]
    public void Tilt_Is45ByDefault_And62ForTheCrab()
    {
        Assert.Equal(45 * Math.PI / 180, Magnetosphere.TiltRad(Default), 12);
        Assert.Equal(CompactResearch.C4.CrabInclinationDeg * Math.PI / 180, Magnetosphere.TiltRad(Crab), 12);
        Assert.Equal(CompactResearch.C4.CrabInclinationDeg, Crab.MagneticInclinationDeg);
        Assert.Contains("Romani", Crab.MagneticInclinationSource!.Citation);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EarthAngle_HonoursBeamCheckbox(bool toward)
    {
        Assert.Equal(toward, Magnetosphere.BeamCanCrossEarth(Default, Obs(toward)));
        Assert.Equal(toward, Magnetosphere.BeamCanCrossEarth(Crab, Obs(toward)));
        Assert.Equal(toward, Magnetosphere.BeamCanCrossEarth(Jets(), Obs(toward)));
    }

    [Fact]
    public void EarthAngle_OnIsTheTilt_JetsZero_OffIsOutsideTheCone()
    {
        Assert.Equal(Magnetosphere.TiltRad(Default), Magnetosphere.EarthAngleRad(Default, Obs(true)), 12);
        Assert.Equal(62 * Math.PI / 180, Magnetosphere.EarthAngleRad(Crab, Obs(true)), 12);
        Assert.Equal(0, Magnetosphere.EarthAngleRad(Jets(), Obs(true)));
        // Off: α + ρ + 10°, with ρ at the birth period (0.1 s for the test star).
        double rho = Magnetosphere.BeamHalfAngleRad(0.1);
        Assert.Equal(Magnetosphere.TiltRad(Default) + rho + 10 * Math.PI / 180, Magnetosphere.EarthAngleRad(Default, Obs(false)), 12);
    }

    [Fact]
    public void EarthDirection_IsAtAngleZetaFromTheSpinAxis_AzimuthMinusY()
    {
        var e = Magnetosphere.EarthDirection(Crab, Obs(true));
        double zeta = 62 * Math.PI / 180;
        Assert.Equal(0, e.X, 12);
        Assert.Equal(-Math.Sin(zeta), e.Y, 12);
        Assert.Equal(Math.Cos(zeta), e.Z, 12);
        Assert.Equal(1, e.Length, 12);
    }

    [Fact]
    public void MagnetosphereForms_At30s_NaNWithoutANeutronStar()
    {
        Assert.Equal(CompactResearch.C4.MagnetosphereFormsS, Magnetosphere.MagnetosphereFormsS(Default));
        var bh = TestOutcomes.Custom(SupernovaType.IIP, null, null, null, 3.5e13, TestOutcomes.BlackHole(10, 1), Fate.FailedSupernova);
        Assert.True(double.IsNaN(Magnetosphere.MagnetosphereFormsS(bh)));
    }
}
