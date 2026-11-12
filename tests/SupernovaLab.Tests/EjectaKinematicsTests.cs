using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>Task 16: the 3D view's ejecta speed, radius and photosphere temperature (signals §0.4, §7.2).</summary>
public class EjectaKinematicsTests
{
    private const double Year = PhysicalConstants.Year, Day = PhysicalConstants.Day;

    private static readonly ShockBreakoutModel Breakout = new();

    /// <summary>A hydrogen-rich explosion of 1e51 erg and 3 M☉ of ejecta from a 3e12 cm (43 R☉) star.</summary>
    private static Outcome Worked(double radiusCm = 3e12)
        => TestOutcomes.Custom(SupernovaType.IIP, 1e51, 3, 0.07, radiusCm);

    [Fact]
    public void OuterVelocity_MatchesTheSignalsWorkedExample()
    {
        // signals §0.4: E = 1e51 erg, M_ej = 3 M☉ gives v_ej = 7.5e8 cm/s.
        Assert.Equal(7.5e8, EjectaKinematics.OuterVelocityCmS(Worked()), 7.5e8 * 0.01);
    }

    [Fact]
    public void OuterVelocity_IsNaNWithoutAnExplosion()
    {
        var failed = TestOutcomes.Custom(SupernovaType.None, null, null, null, 5e13,
            TestOutcomes.BlackHole(10, 1.5), Fate.FailedSupernova);
        Assert.True(double.IsNaN(EjectaKinematics.OuterVelocityCmS(failed)));
    }

    [Fact]
    public void RadiusBeforeBreakout_IsTheStarsRadius()
    {
        var o = Worked();
        double tbo = Breakout.BreakoutTimeS(o);
        Assert.True(tbo > 0);
        Assert.Equal(3e12, EjectaKinematics.RadiusCm(o, 1e-3));
        Assert.Equal(3e12, EjectaKinematics.RadiusCm(o, 0.5 * tbo));
    }

    [Fact]
    public void RadiusBeforeBreakout_IsThePinnedRadius()
    {
        // SN 1987A's preset pins its progenitor's radius; the engine puts it on the star.
        var preset = Core.Presets.PresetCatalog.Get("sn1987a");
        var o = TestOutcomes.Engine.Compute(preset.Inputs, preset.Pins);
        Assert.Equal(preset.Pins!.RadiusCm!.Value, EjectaKinematics.RadiusCm(o, 1.0), 1e-6 * preset.Pins.RadiusCm.Value);
    }

    [Fact]
    public void RadiusAtOneYear_IsVelocityTimesTime()
    {
        // v·t = 7.5e8 cm/s × 3.156e7 s = 2.367e16 cm (signals §0.4's velocity; by hand).
        Assert.Equal(2.367e16, EjectaKinematics.RadiusCm(Worked(), Year), 2.367e16 * 0.01);
    }

    [Fact]
    public void Radius_IsContinuousAtBreakout()
    {
        var o = Worked(5e13);
        double tbo = Breakout.BreakoutTimeS(o);
        Assert.Equal(5e13, EjectaKinematics.RadiusCm(o, tbo * (1 + 1e-9)), 5e13 * 1e-6);
    }

    [Fact]
    public void Radius_GrowsWithoutJumps_AndJoinsTheRemnantModel()
    {
        // From 1 ms to 1,000 yr on a fine log grid: never shrinks, never steps by more than a few grid ratios (a jump of
        // v·t_bo at breakout would double it), and from 10 yr (the remnant model's start) equals its blast wave to 0.1%
        // (the drawn radius adds the star's radius, ≤ 1e14 cm, to the model's).
        foreach (var o in new[] { Worked(5e13), TestOutcomes.Custom(SupernovaType.Ic, 1e52, 0.1, 0.01, 3e10) })
        {
            double ratio = Math.Pow(10, 0.01);
            double previous = EjectaKinematics.RadiusCm(o, 1e-3);
            for (double t = 1e-3 * ratio; t <= 1000 * Year; t *= ratio)
            {
                double r = EjectaKinematics.RadiusCm(o, t);
                Assert.True(r >= previous * (1 - 1e-12), $"radius shrank at t = {t:G3} s");
                Assert.True(r <= previous * Math.Pow(ratio, 3), $"radius jumped at t = {t:G3} s");
                previous = r;
            }
            var remnant = new RemnantEvolutionModel();
            foreach (double years in new[] { 10.0, 30, 100, 1000 })
            {
                double rem = remnant.Sample(o, ObserverSetup.Default, years * Year, "radiusPc") * PhysicalConstants.Pc;
                Assert.Equal(rem, EjectaKinematics.RadiusCm(o, years * Year), rem * 1e-3);
            }
        }
    }

    [Fact]
    public void Remnant_TakesOverWhereItIsSmallerThanFreeExpansion()
    {
        // 0.1 M☉ thrown out at 1e52 erg decelerates within a few years (Truelove & McKee t_ch ≈ 22 yr): by 5 yr the
        // drawn radius is already below free expansion.
        var o = TestOutcomes.Custom(SupernovaType.Ic, 1e52, 0.1, 0.01, 3e10);
        double free = 3e10 + EjectaKinematics.OuterVelocityCmS(o) * (5 * Year - Breakout.BreakoutTimeS(o));
        Assert.True(EjectaKinematics.RadiusCm(o, 5 * Year) < 0.99 * free);
    }

    [Fact]
    public void FailedSupernova_KeepsTheStarsRadius()
    {
        var failed = TestOutcomes.Custom(SupernovaType.None, null, null, null, 5e13,
            TestOutcomes.BlackHole(10, 1.5), Fate.FailedSupernova);
        Assert.Equal(5e13, EjectaKinematics.RadiusCm(failed, Year));
    }

    [Fact]
    public void TypeIa_ExpandsFromIgnition()
    {
        // No breakout delay for a white dwarf: R = R_WD + v t; at 1 day v·t dominates.
        var ia = TestOutcomes.TypeIa();
        double v = EjectaKinematics.OuterVelocityCmS(ia);
        double expected = ia.Star!.RadiusCm.Value + v * Day;
        Assert.Equal(expected, EjectaKinematics.RadiusCm(ia, Day), expected * 1e-9);
    }

    [Fact]
    public void PhotosphereTemperature_IsTheStarsBeforeBreakout()
    {
        var o = Worked();
        Assert.Equal(o.Star!.SurfaceTemperatureK.Value, EjectaKinematics.PhotosphereTemperatureK(o, 1e-3));
    }

    [Fact]
    public void PhotosphereTemperature_IsStefanBoltzmannOfTheLightCurve()
    {
        // At 20 days: T = (L / (4π σ R²))^(1/4), with L from the light curve and R = R* + v (t − t_bo), v = 7.47e8 cm/s
        // (signals §0.4 for 1e51 erg and 3 M☉: √(10e51 / (3 × 3 × 1.989e33)) = 7.474e8).
        var o = Worked();
        double t = 20 * Day;
        double l = new LightCurveModel().Sample(o, ObserverSetup.Default, t, "lbol");
        double r = 3e12 + 7.474e8 * (t - Breakout.BreakoutTimeS(o));
        double expected = Math.Max(3000, Math.Pow(l / (4 * Math.PI * 5.670374419e-5 * r * r), 0.25));
        Assert.Equal(expected, EjectaKinematics.PhotosphereTemperatureK(o, t), expected * 1e-3);
    }

    [Fact]
    public void PhotosphereTemperature_HasAFloorOf3000K()
    {
        Assert.Equal(3000, EjectaKinematics.PhotosphereTemperatureK(Worked(), 2 * Year));
    }
}
