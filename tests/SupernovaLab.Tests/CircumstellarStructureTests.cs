using SupernovaLab.App.Controllers;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Structure;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>Task 9: the gas around the star (structure §S7) and how the shock lights it.</summary>
public class CircumstellarStructureTests
{
    private const double Year = PhysicalConstants.Year, Day = PhysicalConstants.Day;

    private static Outcome Default() => TestOutcomes.Engine.Compute(LabInputs.Default);

    private static Outcome StrippedIc() => TestOutcomes.Engine.Compute(LabInputs.Default with
    {
        InitialMass = 30, Metallicity = 1, Stripping = Stripping.Full,
    });

    /// <summary>The wind's column at b = 2R*, in the units the view draws it in (R* = 1; the shader's column of ρ = A_*/r², ×R*).</summary>
    private static double ColumnAtTwoStellarRadii(CircumstellarStructure c)
        => c.WindAStar * ColumnDensity.Wind(2, 1, c.WindOuterCm / c.WindInnerCm);

    [Fact]
    public void RedSupergiantWind_IsThickerThanWolfRayet()
    {
        var rsg = CircumstellarStructureModel.For(Default())!;
        var ic = StrippedIc();
        Assert.True(ic.Type is SupernovaType.Ib or SupernovaType.Ic, ic.Type.ToString());
        var wr = CircumstellarStructureModel.For(ic)!;
        Assert.True(rsg.WindAStar > wr.WindAStar, $"A_* {rsg.WindAStar} vs {wr.WindAStar}");
        Assert.True(ColumnAtTwoStellarRadii(rsg) > ColumnAtTwoStellarRadii(wr));
        // The wind starts at the star and ends beyond it.
        Assert.Equal(Default().Star!.RadiusCm.Value, rsg.WindInnerCm);
        Assert.True(rsg.WindOuterCm > rsg.WindInnerCm);
    }

    [Fact]
    public void WindOuterExtent_FollowsRamPressureBalance_S7()
    {
        // §S7: r_w = (Ṁ v_w / (4π p))^½ with p/k = 1e4 cm⁻³ K: ≈ 0.6 pc for Ṁ = 1e-6 M☉/yr at 10 km/s, ≈ 20 pc for 1e-5 at 1,000 km/s.
        Assert.InRange(CircumstellarStructureModel.WindOuterExtentCm(1e-6, 10) / PhysicalConstants.Pc, 0.55, 0.7);
        Assert.InRange(CircumstellarStructureModel.WindOuterExtentCm(1e-5, 1000) / PhysicalConstants.Pc, 18, 22);
        Assert.InRange(CircumstellarStructureModel.WindOuterExtentCm(2e-6, 10) / PhysicalConstants.Pc, 0.7, 1.0);
        Assert.True(double.IsNaN(CircumstellarStructureModel.WindOuterExtentCm(1e-6, 0)));
        Assert.True(double.IsNaN(CircumstellarStructureModel.WindOuterExtentCm(0, 10)));
        // The default star's surroundings use that formula with its own wind.
        var o = Default();
        var c = CircumstellarStructureModel.For(o)!;
        Assert.Equal(CircumstellarStructureModel.WindOuterExtentCm(o.Star!.MassLossRateMsunPerYr.Value, o.Star.WindSpeedKmS.Value), c.WindOuterCm);
        Assert.Null(c.ShellInnerCm);
        Assert.Null(c.ShellOuterCm);
    }

    [Fact]
    public void DenseShell_ExtentFromStructureS7()
    {
        // The dense-gas input on a II-P star: SN 2023ixf's confined gas, 1e15 cm [V].
        var ixf = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.07, 3e13, inputs: LabInputs.Default with { DenseCsm = true });
        var c = CircumstellarStructureModel.For(ixf)!;
        Assert.True(CsmInteractionModel.HasDenseCsm(ixf));
        Assert.Equal(1e15, c.ShellOuterCm);
        Assert.Equal(ixf.Star!.RadiusCm.Value, c.ShellInnerCm);
        Assert.NotEqual(Confidence.Speculative, c.ShellConfidence);
        Assert.Contains("Jacobson-Galán", c.Source.Citation, StringComparison.Ordinal);

        // The engine turns the dense-gas input into a Type IIn: 3e16 cm [D].
        var engine = TestOutcomes.Engine.Compute(PresetCatalog.Get("sn2023ixf").Inputs with { DenseCsm = true });
        Assert.Equal(SupernovaType.IIn, engine.Type);
        Assert.Equal(3e16, CircumstellarStructureModel.For(engine)!.ShellOuterCm);
        var iin = TestOutcomes.Custom(SupernovaType.IIn, 1e51, 10, 0.07, 3e13);
        var cn = CircumstellarStructureModel.For(iin)!;
        Assert.Equal(3e16, cn.ShellOuterCm);
        Assert.NotEqual(Confidence.Speculative, cn.ShellConfidence);

        // Where §S7 gives no extent (Ibn, Icn, pulsational pair instability) it falls back to 1e15 cm, Speculative.
        foreach (var type in new[] { SupernovaType.Ibn, SupernovaType.Icn })
        {
            var f = CircumstellarStructureModel.For(TestOutcomes.Custom(type, 1e51, 3, 0.07, 1e12))!;
            Assert.Equal(1e15, f.ShellOuterCm);
            Assert.Equal(Confidence.Speculative, f.ShellConfidence);
        }
        var ppi = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.07, 3e13, fate: Fate.PulsationalPairInstability);
        var cp = CircumstellarStructureModel.For(ppi)!;
        Assert.Equal(1e15, cp.ShellOuterCm);
        Assert.Equal(Confidence.Speculative, cp.ShellConfidence);

        // No dense gas, no shell.
        Assert.Null(CircumstellarStructureModel.For(Default())!.ShellOuterCm);
    }

    [Fact]
    public void Glow_IsZeroBeforeTheShockArrives_AndPeaksAtOne()
    {
        var o = Default();
        var csm = new CsmInteractionModel();
        double start = csm.StartTimeS(o);
        Assert.Equal(0f, CircumstellarStructureModel.Glow(o, 0.5 * start));
        Assert.Equal(0f, CircumstellarStructureModel.Glow(o, 0));
        Assert.Equal(1f, CircumstellarStructureModel.Glow(o, start));
        // Mapped logarithmically over 4 decades below the maximum (L ∝ 1/t): half-way at 100 times the start; zero at 10⁴.
        Assert.Equal(0.5f, CircumstellarStructureModel.Glow(o, 100 * start), 3);
        Assert.Equal(0f, CircumstellarStructureModel.Glow(o, 1e4 * start), 3);
        Assert.Equal(0f, CircumstellarStructureModel.Glow(o, 1e6 * start));
        float previous = 1;
        for (double t = start; t < 1e5 * start; t *= 1.5)
        {
            float g = CircumstellarStructureModel.Glow(o, t);
            Assert.InRange(g, 0f, previous);
            previous = g;
        }
        // The same function of the circumstellar model's thermal X-ray luminosity.
        double a = CsmInteractionModel.WindAStar(o);
        double expected = 1 + Math.Log10(CsmInteractionModel.ThermalXrayLuminosity(a, 10 * start)
                                         / CsmInteractionModel.ThermalXrayLuminosity(a, start)) / 4;
        Assert.Equal((float)expected, CircumstellarStructureModel.Glow(o, 10 * start), 5);
        // Nothing to light for an outcome the circumstellar model does not apply to.
        Assert.Equal(0f, CircumstellarStructureModel.Glow(TestOutcomes.TypeIa(), Day));
    }

    [Fact]
    public void TypeIa_HasNoSurroundings()
    {
        Assert.Null(CircumstellarStructureModel.For(TestOutcomes.TypeIa()));
        Assert.Null(CircumstellarStructureModel.For(TestOutcomes.Engine.Compute(LabInputs.Default with { Kind = StarKind.WhiteDwarf })));
        Assert.Equal(0f, CircumstellarStructureModel.Glow(TestOutcomes.TypeIa(), Year));
    }

    [Fact]
    public void HazeBrightness_IsALogScaleOfAStar_AndFaint()
    {
        // The haze's brightness grows with A_* on a log scale (1 at the lab's ordinary red-supergiant wind, A_* = 10), clamped.
        Assert.Equal(1, SceneController.HazeScale(10), 9);
        Assert.True(SceneController.HazeScale(1000) > SceneController.HazeScale(100));
        Assert.True(SceneController.HazeScale(100) > 1);
        Assert.Equal(SceneController.HazeMinScale, SceneController.HazeScale(1e-6));
        Assert.Equal(SceneController.HazeMaxScale, SceneController.HazeScale(1e12));
        Assert.Equal(0, SceneController.HazeScale(double.NaN));
        // Faint: well below the shocks' own brightest lines of sight.
        Assert.True(SceneController.HazeIntensityAtTenAStar * SceneController.HazeMaxScale
                    < SceneController.ForwardShellIntensity / 4);
    }

    // ---------------------------------------------------------------- ruling R-P2-27: SN 2023ixf's pinned confined gas

    private static Outcome Ixf(PresetPins? pins = null)
    {
        var p = PresetCatalog.Get("sn2023ixf");
        return TestOutcomes.Engine.Compute(p.Inputs, pins ?? p.Pins);
    }

    [Fact]
    public void Sn2023ixf_CarriesTheConfinedGasPin_AndStaysATypeII_WithEveryValueUnchanged()
    {
        var p = PresetCatalog.Get("sn2023ixf");
        Assert.Equal(1e15, p.Pins!.ConfinedCsmOuterCm);
        var with = Ixf();
        var without = TestOutcomes.Engine.Compute(p.Inputs);
        Assert.Equal(1e15, with.ConfinedCsmOuterCm);
        Assert.Null(without.ConfinedCsmOuterCm);
        Assert.Equal(SupernovaType.IIP, with.Type);
        Assert.Equal(without.Type, with.Type);
        Assert.False(CsmInteractionModel.HasDenseCsm(with));
        Assert.Equal(without.ExplosionEnergyErg, with.ExplosionEnergyErg);
        Assert.Equal(without.EjectaMass, with.EjectaMass);
        Assert.Equal(without.NickelMass, with.NickelMass);
        Assert.Equal(without.Star, with.Star);
        // No signal model reads the pin: samples agree with and without it.
        var obs = Core.Observer.ObserverSetup.Default;
        foreach (var (model, channel) in new (ISignalModel, string)[]
                 { (new CsmInteractionModel(), "radio5"), (new CsmInteractionModel(), "lx"), (new LightCurveModel(), "lbol") })
            foreach (double t in new[] { 3 * Day, 30 * Day, Year })
                Assert.Equal(model.Sample(without, obs, t, channel), model.Sample(with, obs, t, channel));
    }

    [Fact]
    public void Sn2023ixf_ShellIsDrawnAt1e15Cm_AndAFlareFollowsTheSweep()
    {
        var o = Ixf();
        var c = CircumstellarStructureModel.For(o)!;
        Assert.Equal(1e15, c.ShellOuterCm);
        Assert.Equal(o.Star!.RadiusCm.Value, c.ShellInnerCm);
        Assert.Contains("Jacobson-Galán", c.Source.Citation, StringComparison.Ordinal);
        var scene = new ExplosionScene(o);
        var early = scene.At(5 * Day);
        Assert.Equal(1e15, early.Surroundings!.ShellOuterCm);
        Assert.True(early.Frame99RadiusCm < 1e15);
        Assert.Equal(1f, early.ShellFlare);                       // on while the debris sweep through the shell
        Assert.Contains(ExplosionScene.ConfinedGasNote, early.Notes);
        Assert.Contains("flare is schematic", ExplosionScene.ConfinedGasNote, StringComparison.Ordinal);
        Assert.Equal(0f, scene.At(0.5 * Day).ShellFlare);          // before the debris are launched
        // Fading over one decade of time after the debris pass the shell, then off.
        double exit = 0;
        for (double t = Day; t < 1e8; t *= 1.02)
            if (scene.At(t).Frame99RadiusCm >= 1e15) { exit = t; break; }
        Assert.True(exit > 5 * Day);
        var later = scene.At(exit * 3);
        Assert.InRange(later.ShellFlare, 0.01f, 0.99f);
        Assert.Equal(0f, scene.At(exit * 20).ShellFlare);
        Assert.True(scene.At(exit * 3).ShellFlare > scene.At(exit * 6).ShellFlare);
    }

    [Fact]
    public void LabOutcomeWithoutThePin_HasNoConfinedShell_AndKeepsTheHazeNoteRule()
    {
        var o = Default();
        Assert.Null(o.ConfinedCsmOuterCm);
        Assert.Null(CircumstellarStructureModel.For(o)!.ShellOuterCm);
        var scene = new ExplosionScene(o);
        Assert.DoesNotContain(ExplosionScene.ConfinedGasNote, scene.At(5 * Day).Notes);
        // The haze note only while the view is at most 100 R* wide; a dense-shell note whenever a shell is drawn (M1).
        double r0 = o.Star!.RadiusCm.Value;
        Assert.Contains(scene.At(1e-3).Notes, n => n.StartsWith("Gas around the star", StringComparison.Ordinal));
        var wide = scene.At(Year);
        Assert.True(wide.FrameRadiusCm > 100 * r0);
        Assert.DoesNotContain(wide.Notes, n => n.StartsWith("Gas around the star", StringComparison.Ordinal));
        var iin = new ExplosionScene(TestOutcomes.Custom(SupernovaType.IIn, 1e51, 10, 0.07, 3e13)).At(1e-3);
        Assert.Contains(iin.Notes, n => n.StartsWith("Gas around the star", StringComparison.Ordinal));
    }
}
