using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Structure;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>Task 10: the pulsar wind nebula (structure §S9).</summary>
public class PulsarWindNebulaTests
{
    private const double Year = PhysicalConstants.Year, Pc = PhysicalConstants.Pc;

    /// <summary>§S9 "The Crab as a check": the age used there, 954 yr, and the measured mean radius (7′ × 5′ at 2.0 kpc = 4.1 × 2.9 pc).</summary>
    private const double CrabAgeYr = 954, MeasuredMeanRadiusPc = 1.7;

    private static Outcome CrabPreset()
    {
        var p = PresetCatalog.Get("sn1054");
        return TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
    }

    [Fact]
    public void Nebula_RadiusFollowsTheGrowthLaw_AndMatchesTheCrab()
    {
        // The coefficient for m = 1 is 128/63 (§S9's App block), and R scales as t^((6-m)/(5-m)) = t^1.25 at constant L.
        Assert.Equal(128.0 / 63, PulsarWindNebulaModel.Coefficient(1), 12);
        double a = 1e-3;
        double r1 = PulsarWindNebulaModel.RadiusForCoefficient(1, a, 1e38, 1e9), r2 = PulsarWindNebulaModel.RadiusForCoefficient(1, a, 1e38, 2e9);
        Assert.Equal(Math.Pow(2, 1.25), r2 / r1, 10);
        Assert.Equal(r1, Math.Pow(128.0 / 63 * 1e38 * Math.Pow(1e9, 5) / (4 * Math.PI * a), 0.25), 4);
        // The general exponent for another m: Chevalier 2005's m = 1.06 gives t^1.254.
        double q1 = PulsarWindNebulaModel.RadiusForCoefficient(1.06, a, 1e38, 1e9), q2 = PulsarWindNebulaModel.RadiusForCoefficient(1.06, a, 1e38, 2e9);
        Assert.Equal(Math.Pow(2, 4.94 / 3.94), q2 / q1, 10);

        // §S9's Crab check: P0 = 16.845 ms, B = 3.794e12 G, E = 1e50 erg, M_ej = 4.6 Msun, the RSG profile (n = 12): 1.21 pc at 954 yr.
        var preset = CrabPreset();
        var spin = PulsarModel.Spin.Of(preset);
        double t = CrabAgeYr * Year;
        Assert.InRange(spin.SpinDownLuminosity(0), 6.85e39, 6.95e39);          // Ė₀ = 6.9e39 erg/s
        Assert.InRange(spin.Tau0 / Year, 319, 321);                              // τ₀ = 320 yr
        double effective = PulsarWindNebulaModel.EffectiveLuminosityErgS(spin, t);
        Assert.InRange(effective, 1.72e39, 1.74e39);                             // L = 1.73e39 erg/s
        Assert.InRange(effective * t, 5.15e49, 5.25e49);                         // E_spun = 5.2e49 erg
        var nominal = preset with { ExplosionEnergyErg = TestOutcomes.Q(1e50, "erg"), EjectaMass = TestOutcomes.Q(4.6, "M☉") };
        double nominalPc = PulsarWindNebulaModel.RadiusCm(nominal, t) / Pc;
        Assert.InRange(nominalPc, 1.21 * 0.99, 1.21 * 1.01);

        // The lab's own Crab (E = 1e50 erg, but M_ej = 7.39 Msun from the 9.1 Msun star) gives 0.96 pc: §S9 says the laws are 10-45% small
        // against the measured 1.7 pc (inconsistency #39), so it must lie in that band, below the measurement.
        double labPc = PulsarWindNebulaModel.RadiusCm(preset, t) / Pc;
        Assert.InRange(labPc / MeasuredMeanRadiusPc, 1 - 0.45, 1 - 0.10);
        Assert.InRange(labPc, 0.95, 0.97);
    }

    [Fact]
    public void Nebula_GrowsAsTheOneAndAQuarterPower_WhileThePulsarPowerIsConstant()
    {
        var o = CrabPreset();
        var st = EjectaStructureModel.For(o)!;
        var spin = PulsarModel.Spin.Of(o);
        double l = spin.SpinDownLuminosity(0);
        double r1 = PulsarWindNebulaModel.RadiusCm(st, l, 1 * Year), r8 = PulsarWindNebulaModel.RadiusCm(st, l, 8 * Year);
        Assert.Equal(Math.Pow(8, 1.25), r8 / r1, 8);
        // The profile's A is K v_t with K = M (3-δ)(n-3) / (4π v_t³ (n-δ)).
        double m = st.EjectaMassMsun * PhysicalConstants.MSun, vt = st.TransitionCmS;
        double k = m * 2 * (st.N - 3) / (4 * Math.PI * vt * vt * vt * (st.N - 1));
        Assert.Equal(1.0, PulsarWindNebulaModel.InnerDensityCoefficient(st) / (k * vt), 12);
    }

    [Fact]
    public void Nebula_StaysWithinTheCaps()
    {
        var o = CrabPreset();
        var st = EjectaStructureModel.For(o)!;
        foreach (double yr in new[] { 0.1, 1, 10, 100, 954, 1000 })
        {
            double t = yr * Year, r = PulsarWindNebulaModel.RadiusCm(o, t);
            Assert.InRange(r, 0, st.TransitionCmS * t);                                     // the v_t t cap
            if (ShockStructureModel.At(o, t) is { ReverseCm: > 0 } shocks) Assert.True(r <= shocks.ReverseCm * (1 + 1e-12), $"{yr} yr");
        }
        Assert.Equal(0, PulsarWindNebulaModel.RadiusCm(o, 0));
    }

    [Fact]
    public void Nebula_OnlyWithAPulsar()
    {
        var pulsar = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 8, 0.07, 3e12, TestOutcomes.Pulsar(0.02, 3e12));
        var magnetar = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 8, 0.07, 3e12, TestOutcomes.Pulsar(0.005, 1e14, RemnantKind.Magnetar));
        var blackHole = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 8, 0.07, 3e12, TestOutcomes.BlackHole(8, 100));
        var typeIa = TestOutcomes.TypeIa();
        Assert.True(PulsarWindNebulaModel.Applies(pulsar));
        Assert.True(PulsarWindNebulaModel.Applies(magnetar));
        Assert.True(PulsarWindNebulaModel.RadiusCm(pulsar, 100 * Year) > 0);
        Assert.False(PulsarWindNebulaModel.Applies(blackHole));
        Assert.False(PulsarWindNebulaModel.Applies(typeIa));
        Assert.True(double.IsNaN(PulsarWindNebulaModel.RadiusCm(blackHole, 100 * Year)));
        Assert.True(double.IsNaN(PulsarWindNebulaModel.RadiusCm(typeIa, 100 * Year)));
        Assert.True(double.IsNaN(PulsarWindNebulaModel.RadiusCm(TestOutcomes.Collapsar(), 100 * Year)));

        Assert.True(new ExplosionScene(pulsar).At(100 * Year).NebulaRadiusCm > 0);
        Assert.True(double.IsNaN(new ExplosionScene(blackHole).At(100 * Year).NebulaRadiusCm));
        Assert.True(double.IsNaN(new ExplosionScene(typeIa).At(100 * Year).NebulaRadiusCm));
        Assert.True(double.IsNaN(new ExplosionScene(pulsar).At(1e-3).NebulaRadiusCm), "no nebula before breakout");
    }

    [Fact]
    public void Scene_DrawsTheCrabsNebulaInsideTheDebris_WithItsNote_AndTheFramingIsUnchanged()
    {
        var crab = CrabPreset();
        double t = CrabAgeYr * Year;
        var s = new ExplosionScene(crab).At(t);
        Assert.InRange(s.NebulaRadiusCm, 0.5 * Pc, s.EjectaRadiusCm);
        Assert.True(s.NebulaRadiusCm < s.FrameRadiusCm);
        Assert.Contains(s.Notes, n => n.StartsWith("Pulsar wind nebula: size from ", StringComparison.Ordinal) &&
                                      n.Contains("Chevalier", StringComparison.Ordinal));
        // The nebula adds nothing to the frame: the same outcome without the pulsar frames identically.
        var noPulsar = crab with { Remnant = TestOutcomes.BlackHole(1.4, 100) };
        Assert.Equal(1.0, new ExplosionScene(noPulsar).At(t).FrameRadiusCm / s.FrameRadiusCm, 9);
        // No note without a nebula, and none before breakout (the star is still whole).
        Assert.DoesNotContain(new ExplosionScene(noPulsar).At(t).Notes, n => n.StartsWith("Pulsar wind nebula", StringComparison.Ordinal));
        Assert.DoesNotContain(new ExplosionScene(crab).At(1e-3).Notes, n => n.StartsWith("Pulsar wind nebula", StringComparison.Ordinal));
    }
}
