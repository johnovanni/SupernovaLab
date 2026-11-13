using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Structure;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>Task 16: what the 3D view draws at each time (spec §6), without OpenGL.</summary>
public class ExplosionSceneTests
{
    private const double Year = PhysicalConstants.Year, Day = PhysicalConstants.Day;

    private static Outcome Exploding() => TestOutcomes.Custom(SupernovaType.IIP, 1e51, 3, 0.07, 3e12);

    [Fact]
    public void BeforeBreakout_ShowsTheStarOnly()
    {
        var s = new ExplosionScene(Exploding()).At(1e-3);
        Assert.True(s.ShowStar);
        Assert.False(s.ShowEjecta);
        Assert.Equal(SceneMarker.None, s.Marker);
        Assert.Equal(3e12, s.StarRadiusCm);
        Assert.Equal(3e12, s.FrameRadiusCm);
        Assert.Contains(s.Notes, n => n.Contains("crossing the star", StringComparison.Ordinal));
    }

    [Fact]
    public void BeforeBreakout_TheShockAndTheStarsLayersAreGiven_AndAfterBreakoutTheyAreNot()
    {
        var o = Exploding();
        var scene = new ExplosionScene(o);
        double tbo = new SupernovaLab.Core.Signals.ShockBreakoutModel().ShockAtSurfaceS(o);

        var s = scene.At(0.4 * tbo);
        Assert.Equal(SupernovaLab.Core.Structure.StarInterior.ShockRadiusCm(o, 0.4 * tbo), s.ShockInStarRadiusCm);
        Assert.InRange(s.ShockInStarRadiusCm, 0.1 * 3e12, 0.9 * 3e12);
        Assert.Equal(SupernovaLab.Core.Structure.StarInterior.Layers(o), s.StarLayers);
        Assert.Equal(3e12, s.StarLayers[^1].Item2);
        Assert.Contains(s.Notes, n => n.StartsWith("The shock is crossing the star: it reaches the surface ", StringComparison.Ordinal)
                                       && n.EndsWith(" after core bounce", StringComparison.Ordinal));

        var after = scene.At(Day);
        Assert.True(double.IsNaN(after.ShockInStarRadiusCm));
        Assert.Empty(after.StarLayers);
    }

    [Fact]
    public void TypeIa_HasNoStarLayersOrShockToDraw()
    {
        var s = new ExplosionScene(TestOutcomes.TypeIa()).At(1.0);
        Assert.Empty(s.StarLayers);
        Assert.True(double.IsNaN(s.ShockInStarRadiusCm));
    }

    [Fact]
    public void AfterBreakout_ShowsTheEjectaAndTheNeutronStar()
    {
        var o = Exploding();
        var s = new ExplosionScene(o).At(Day);
        Assert.False(s.ShowStar);
        Assert.True(s.ShowEjecta);
        Assert.Equal(SceneMarker.NeutronStar, s.Marker);
        Assert.Equal(EjectaKinematics.RadiusCm(o, Day), s.EjectaRadiusCm);
        // Ruling P1: the framing radius is the larger of Project 1's radius and the 99%-of-mass particle radius.
        Assert.Equal(Math.Max(s.EjectaRadiusCm, s.Frame99RadiusCm), s.FrameRadiusCm);
    }

    [Fact]
    public void EjectaColour_IsTheLightCurvesColourTemperature()
    {
        // The schematic spectrum uses the same temperature, so the view and the spectrum agree.
        var o = Exploding();
        var s = new ExplosionScene(o).At(20 * Day);
        Assert.Equal(new LightCurveModel().ColourTemperatureK(o, 20 * Day), s.EjectaTemperatureK);
        Assert.Equal(BlackbodyColor.FromTemperature(s.EjectaTemperatureK), s.EjectaColor);
    }

    [Fact]
    public void EjectaDim_AsTheLightFades()
    {
        var scene = new ExplosionScene(Exploding());
        Assert.True(scene.At(30 * Day).EjectaBrightness > scene.At(2 * Year).EjectaBrightness);
        Assert.InRange(scene.At(100 * Year).EjectaBrightness, 0.1f, 0.5f);
    }

    [Fact]
    public void ColoursAreLabelledSchematic()
    {
        foreach (double t in new[] { 1e-3, Day, 100 * Year })
            Assert.Contains(new ExplosionScene(Exploding()).At(t).Notes, n => n.Contains("schematic", StringComparison.Ordinal));
    }

    [Fact]
    public void FailedSupernova_StarUntilTheBlackHoleForms_ThenOnlyTheBlackHole()
    {
        var failed = TestOutcomes.Custom(SupernovaType.None, null, null, null, 5e13,
            TestOutcomes.BlackHole(10, 1.5), Fate.FailedSupernova);
        var scene = new ExplosionScene(failed);
        var before = scene.At(1.0);
        Assert.True(before.ShowStar);
        Assert.Equal(SceneMarker.None, before.Marker);
        var after = scene.At(10.0);
        Assert.False(after.ShowStar);
        Assert.False(after.ShowEjecta);
        Assert.Equal(SceneMarker.BlackHole, after.Marker);
        Assert.Equal(5e13, after.FrameRadiusCm);
    }

    [Fact]
    public void TypeIa_HasNoStar_AWhiteDwarfPointAtFirst_ThenEjecta()
    {
        var scene = new ExplosionScene(TestOutcomes.TypeIa());
        var first = scene.At(1e-3);
        Assert.False(first.ShowStar);
        Assert.Equal(SceneMarker.WhiteDwarf, first.Marker);
        var later = scene.At(Day);
        Assert.True(later.ShowEjecta);
        Assert.Equal(SceneMarker.None, later.Marker);
    }

    [Fact]
    public void GammaRayBurst_DrawsJets()
    {
        var o = TestOutcomes.Collapsar();
        Assert.True(o.HasGrb);
        var s = new ExplosionScene(o).At(Day);
        Assert.True(s.ShowJets);
        Assert.Equal(GrbModel.JetHalfAngleRad, s.JetHalfAngleRad);
        Assert.Equal(SceneMarker.BlackHole, s.Marker);
        Assert.False(new ExplosionScene(Exploding()).At(Day).ShowJets);
    }

    [Fact]
    public void Pulsar_SweepsItsBeamsOnceThePulsesEmerge_SlowedDown()
    {
        var o = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 3, 0.07, 3e12, TestOutcomes.Pulsar(0.02, 3e12));
        var early = new ExplosionScene(o).At(Year);
        Assert.Equal(SceneMarker.NeutronStar, early.Marker);
        var late = new ExplosionScene(o).At(100 * Year);
        Assert.Equal(SceneMarker.Pulsar, late.Marker);
        Assert.Equal(new PulsarModel().Sample(o, Core.Observer.ObserverSetup.Default, 100 * Year, "periodS"), late.PulsarPeriodS);
        Assert.Contains(late.Notes, n => n.Contains("slowed down", StringComparison.Ordinal));
        // Review I2: the period is the lab's model's, not a measurement.
        Assert.Contains(late.Notes, n => n.Contains("the lab's model period", StringComparison.Ordinal));
        Assert.DoesNotContain(late.Notes, n => n.Contains("real period", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- fix round 1

    /// <summary>The dense-gas input on a II-P star: SN 2023ixf's confined gas, 1e15 cm. (The engine itself turns the input into a Type IIn.)</summary>
    private static Outcome DenseCsm()
        => TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.07, 3e13, inputs: LabInputs.Default with { DenseCsm = true });

    [Fact]
    public void DenseShell_IsInTheState_BeforeTheExplosion_AndFramed()
    {
        // Task 9 (structure §S7): a dense shell out to SN 2023ixf's 1e15 cm; a dense shell may drive the framing, as Project 1's gas shell did.
        var s = new ExplosionScene(DenseCsm()).At(1e-3);
        Assert.True(s.ShowStar);
        var c = s.Surroundings!;
        Assert.Equal(1e15, c.ShellOuterCm);
        Assert.Equal(1e15, s.FrameRadiusCm);
        Assert.Equal(s.StarRadiusCm, s.RadiusCm);
        Assert.Equal(0f, s.SurroundingsGlow);   // the shock has not reached the star's surface
    }

    [Fact]
    public void Haze_DoesNotDriveTheFraming_ButADenseShellDoes()
    {
        // The wind extends to parsecs: the director frames the star (or the debris) instead.
        var plain = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 3, 0.07, 3e13);
        var s = new ExplosionScene(plain).At(1e-3);
        Assert.NotNull(s.Surroundings);
        Assert.True(s.Surroundings!.WindOuterCm > 1e18);
        Assert.Equal(3e13, s.FrameRadiusCm);
        var later = new ExplosionScene(plain).At(Year);
        Assert.Equal(Math.Max(later.EjectaRadiusCm, later.Frame99RadiusCm), later.FrameRadiusCm);
        Assert.True(later.FrameRadiusCm < later.Surroundings!.WindOuterCm / 10);
        // A dense shell does, while the debris are inside it.
        var scene = new ExplosionScene(DenseCsm());
        var early = scene.At(Day);
        Assert.True(early.EjectaRadiusCm < 1e15);
        Assert.Equal(1e15, early.FrameRadiusCm);
    }

    [Fact]
    public void WindSpeedZero_GivesNaNAStar_NotInfinity_AndNoHaze()
    {
        // Task 16 re-review N3: mass loss with a still wind is A_* = Ṁ/0; it must be NaN, and the haze must not be drawn.
        var plain = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 3, 0.07, 3e13);
        var still = TestOutcomes.WithWind(plain, 1e-4, 0);
        Assert.True(double.IsNaN(CsmInteractionModel.WindAStar(still)));
        var c = new ExplosionScene(still).At(1e-3).Surroundings!;
        Assert.True(double.IsNaN(c.WindAStar));
        Assert.Equal(c.WindInnerCm, c.WindOuterCm);
        Assert.True(double.IsNaN(CsmInteractionModel.WindAStar(TestOutcomes.WithWind(plain, 1e-4, double.NaN))));
        Assert.Equal(1000, CsmInteractionModel.WindAStar(TestOutcomes.WithWind(plain, 1e-4, 10)), 6);
    }

    [Fact]
    public void Surroundings_AreTheModels_AndAbsentForATypeIa()
    {
        var o = Exploding();
        var s = new ExplosionScene(o).At(Day);
        Assert.Equal(CircumstellarStructureModel.For(o), s.Surroundings);
        Assert.Equal(CircumstellarStructureModel.Glow(o, Day), s.SurroundingsGlow);
        Assert.Null(new ExplosionScene(TestOutcomes.TypeIa()).At(Day).Surroundings);
        Assert.Equal(0f, new ExplosionScene(TestOutcomes.TypeIa()).At(Day).SurroundingsGlow);
        // No star, no surroundings.
        var wd = TestOutcomes.Engine.Compute(LabInputs.Default with { InitialMass = 0.5 });
        if (wd.Star is null) Assert.Null(new ExplosionScene(wd).At(Day).Surroundings);
    }

    [Fact]
    public void GasNote_KeepsProject1sWording_ForTheFallbackExtent_ElseNamesItsSource()
    {
        // Ibn: §S7 gives no extent, so the 1e15 cm fallback and Project 1's note; the II-P dense-gas case names its source.
        var ibn = TestOutcomes.Custom(SupernovaType.Ibn, 1e51, 3, 0.07, 1e12);
        var fallback = new ExplosionScene(ibn).At(1e-3);
        Assert.Equal(Confidence.Speculative, fallback.Surroundings!.ShellConfidence);
        Assert.Contains(ExplosionScene.GasNote, fallback.Notes);
        Assert.Equal("Gas around the star (schematic): its extent is not in the research; drawn to 1e15 cm, like SN 2023ixf's", ExplosionScene.GasNote);

        var s = new ExplosionScene(DenseCsm()).At(1e-3);
        Assert.DoesNotContain(ExplosionScene.GasNote, s.Notes);
        Assert.Contains($"Gas around the star: extent from {s.Surroundings!.Source.Citation}", s.Notes);
        // An ordinary star's haze is named too.
        var plain = new ExplosionScene(Exploding()).At(1e-3);
        Assert.Contains($"Gas around the star: extent from {plain.Surroundings!.Source.Citation}", plain.Notes);
        Assert.Null(plain.Surroundings.ShellOuterCm);
    }

    [Fact]
    public void DenseShell_StopsFramingOnceTheEjectaPassIt()
    {
        var o = DenseCsm();
        double passed = 0;
        for (double t = 1; t < 1e9; t *= 1.1)
            if (EjectaKinematics.RadiusCm(o, t) >= 1e15) { passed = t; break; }
        Assert.True(passed > 0);
        var scene = new ExplosionScene(o);
        Assert.Equal(1e15, scene.At(passed / 2).FrameRadiusCm);
        Assert.True(scene.At(passed * 1.2).FrameRadiusCm >= EjectaKinematics.RadiusCm(o, passed * 1.2));
    }

    // ---------------------------------------------------------------- Task 8 review carry-over: the readout's label at the join

    [Fact]
    public void ForwardShockLabel_StaysTheJoinLabel_WhileTheHeldJoinMaximumIsDrawn_Sn1998bw()
    {
        // TM99's forward shock for n = 10 reaches SN 1998bw's held peak (0.591 pc, held from 4.08 yr) only at ≈ 13.5 yr: until then
        // the drawn R_f is the held join maximum, so the readout keeps the join label (it is not yet the steep-ejecta solution).
        var preset = PresetCatalog.Get("sn1998bw");
        var o = TestOutcomes.Engine.Compute(preset.Inputs, preset.Pins);
        var scene = new ExplosionScene(o);
        var held = scene.At(11 * Year);
        double tm = TmForwardCm(o, 11 * Year);
        Assert.True(held.Shocks!.ForwardCm > tm * 1.001, $"R_f {held.Shocks.ForwardCm} vs TM99 {tm}");
        Assert.Equal(ExplosionScene.BlendShockLabel, held.ForwardShockLabel);
        var late = scene.At(20 * Year);
        Assert.InRange(late.Shocks!.ForwardCm / TmForwardCm(o, 20 * Year), 0.9999, 1.0001);
        Assert.Equal(ExplosionScene.ForwardShockLabel, late.ForwardShockLabel);
    }

    private static double TmForwardCm(Outcome o, double t)
    {
        var (rch, tch) = ShockStructureModel.CharacteristicScales(o.ExplosionEnergyErg!.Value, o.EjectaMass!.Value,
            RemnantEvolutionModel.AmbientDensity);
        return ShockStructureModel.TmForward(ShockStructureModel.RemnantIndex(EjectaStructureModel.ClassOf(o)), t / tch) * rch;
    }

    [Fact]
    public void OrdinaryStar_HasNoDenseShell() => Assert.Null(new ExplosionScene(Exploding()).At(1e-3).Surroundings!.ShellOuterCm);

    [Fact]
    public void EjectaRadius_IsLabelledAsTheUniformSphereEdge()
    {
        // Review I4: the stated radius is the spec's uniform-density edge; the spectrum's line speeds are faster.
        var s = new ExplosionScene(Exploding()).At(Day);
        Assert.Equal("Ejecta radius", s.RadiusLabel);
        Assert.Contains(s.Notes, n => n.Contains("uniform ball", StringComparison.Ordinal) && n.Contains("spectrum", StringComparison.Ordinal));
        Assert.DoesNotContain(new ExplosionScene(Exploding()).At(100 * Year).Notes, n => n.Contains("uniform ball", StringComparison.Ordinal));
    }

    [Fact]
    public void BlendToTheBlastWave_IsNotCalledAUniformBall()
    {
        // Task 16 re-review N1: between 3 and 10 yr the radius is a blend, so the uniform-ball note must not show.
        var scene = new ExplosionScene(Exploding());
        foreach (double years in new[] { 3.5, 6, 9.9 })
        {
            var s = scene.At(years * Year);
            Assert.Equal("Outer radius", s.RadiusLabel);
            Assert.DoesNotContain(ExplosionScene.UniformSphereNote, s.Notes);
            Assert.Contains(ExplosionScene.BlendNote, s.Notes);
        }
        Assert.Contains(ExplosionScene.UniformSphereNote, scene.At(2.9 * Year).Notes);
        Assert.DoesNotContain(ExplosionScene.BlendNote, scene.At(2.9 * Year).Notes);
        Assert.DoesNotContain(ExplosionScene.BlendNote, scene.At(10.1 * Year).Notes);
    }

    [Fact]
    public void LowLuminosityBurst_JetsOnlyDuringTheModeledBurst_NoAngleStated()
    {
        // Review I3: SN 1998bw's pinned low-luminosity burst. The GRB model draws its prompt burst only (ruling R39).
        var preset = PresetCatalog.Get("sn1998bw");
        var o = TestOutcomes.Engine.Compute(preset.Inputs, preset.Pins);
        var grb = new GrbModel();
        Assert.True(grb.IsLowLuminosity(o));
        var scene = new ExplosionScene(o);
        var during = scene.At(grb.OnsetS(o) + 1);
        Assert.True(during.ShowJets);
        Assert.Contains(during.Notes, n => n.Contains("jet geometry schematic", StringComparison.Ordinal));
        Assert.DoesNotContain(during.Notes, n => n.Contains("°", StringComparison.Ordinal));
        var after = scene.At(Day);
        Assert.False(after.ShowJets);
        Assert.Contains(after.Notes, n => n.Contains("no longer drawn", StringComparison.Ordinal));
        Assert.False(scene.At(grb.PromptEndS(o) * 1.01).ShowJets);
    }

    [Fact]
    public void ClassicalBurst_JetsWhileTheGrbModelIsValid()
    {
        var o = TestOutcomes.Collapsar();
        var grb = new GrbModel();
        Assert.False(grb.IsLowLuminosity(o));
        var (start, end) = grb.ValidRange(o);
        var scene = new ExplosionScene(o);
        Assert.False(scene.At(start * 0.99).ShowJets);
        Assert.True(scene.At(end * 0.999).ShowJets);
        Assert.False(scene.At(end * 1.001).ShowJets);
        Assert.Contains(scene.At(Day).Notes, n => n.Contains("5° half-angle", StringComparison.Ordinal));
    }

    [Fact]
    public void FallbackBlackHole_BeforeItForms_IsLabelled()
    {
        // Review M7: the only unlabelled marker. The neutron star collapses to the black hole at t_BH.
        var o = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 3, 0.07, 3e10, TestOutcomes.BlackHole(5, 1e4));
        var s = new ExplosionScene(o).At(5e3);
        Assert.True(s.ShowEjecta);
        Assert.Equal(SceneMarker.NeutronStar, s.Marker);
        Assert.Contains(s.Notes, n => n.Contains("black hole", StringComparison.Ordinal));
    }

    [Fact]
    public void StarThatHasNotDied_StaysAStar()
    {
        var o = TestOutcomes.Star(0.85);
        Assert.Equal(Fate.NotDeadYet, o.Fate);
        var s = new ExplosionScene(o).At(100 * Year);
        Assert.True(s.ShowStar);
        Assert.False(s.ShowEjecta);
    }

    // ---------------------------------------------------------------- Project 2, Task 6: layered particles

    [Fact]
    public void Scene_FrameRadius_IsAtLeastTheFrame99Radius()
    {
        var o = Exploding();
        var scene = new ExplosionScene(o);
        foreach (double t in new[] { Day, 30 * Day, Year })
        {
            var s = scene.At(t);
            Assert.True(s.Frame99RadiusCm > 0);
            Assert.True(s.FrameRadiusCm >= s.Frame99RadiusCm);
            Assert.True(s.FrameRadiusCm >= s.EjectaRadiusCm);
            Assert.True(s.MaxRadiusCm >= s.Frame99RadiusCm);
        }
        // The radius readout keeps Project 1's value and label (ruling P1).
        Assert.Equal(EjectaKinematics.RadiusCm(o, Day), scene.At(Day).RadiusCm);
        Assert.Equal("Ejecta radius", scene.At(Day).RadiusLabel);
    }

    [Fact]
    public void Scene_NoEjecta_HasNoParticles_AndStillFrames()
    {
        var o = TestOutcomes.Star(0.85);
        var scene = new ExplosionScene(o);
        Assert.Null(scene.Structure);
        Assert.Null(scene.Particles);
        var s = scene.At(100 * Year);
        Assert.False(s.ShowParticles);
        Assert.True(s.FrameRadiusCm > 0);
        Assert.Equal(o.Star!.RadiusCm.Value, s.FrameRadiusCm);
    }

    [Fact]
    public void Scene_Particles_AreBuiltOnce_PerOutcome()
    {
        var scene = new ExplosionScene(Exploding());
        var first = scene.Particles;
        Assert.NotNull(first);
        scene.At(Day);
        scene.At(Year);
        Assert.Same(first, scene.Particles);
        Assert.Equal((int)QualityLevels.Default, first!.Count);
        // Another count is built once and kept too.
        var low = scene.ParticlesFor((int)QualityLevel.Low);
        Assert.Equal((int)QualityLevel.Low, low!.Count);
        Assert.Same(low, scene.ParticlesFor((int)QualityLevel.Low));
    }

    [Fact]
    public void Scene_Particles_AreShownFromBreakout_WithTheMixingNote()
    {
        var scene = new ExplosionScene(Exploding());
        Assert.False(scene.At(1e-3).ShowParticles);
        var s = scene.At(Day);
        Assert.True(s.ShowParticles);
        Assert.True(s.SinceBreakoutS > 0);
        bool fingers = scene.Structure!.Mixing.HasFingers;
        Assert.Equal(fingers, s.Notes.Contains(ExplosionScene.MixingNote));
        Assert.Equal("Mixing fingers: shapes schematic; their reach is from 3D simulations (structure §S3)", ExplosionScene.MixingNote);
    }

    [Fact]
    public void TypeIa_Particles_StartAtTheExplosion()
    {
        var scene = new ExplosionScene(TestOutcomes.TypeIa());
        var s = scene.At(Day);
        Assert.True(s.ShowParticles);
        Assert.Equal(Day, s.SinceBreakoutS, 6);
        Assert.DoesNotContain(ExplosionScene.MixingNote, s.Notes);
    }

    [Fact]
    public void At_NeverBuildsAParticleSet()
    {
        var scene = new ExplosionScene(Exploding());
        foreach (double t in new[] { 1e-3, Day, Year }) scene.At(t);
        Assert.Equal(0, scene.ParticleBuildCount);
        scene.ParticlesFor((int)QualityLevel.Low);
        scene.At(Day);
        scene.At(Year);
        Assert.Equal(1, scene.ParticleBuildCount);
    }

    [Fact]
    public void MaxRadius_UsesTheBuiltSetsMaxSpeed_AtANonDefaultQuality()
    {
        var o = Exploding();
        var scene = new ExplosionScene(o);
        var set = scene.ParticlesFor((int)QualityLevel.Low)!;
        var s = scene.At(30 * Day);
        double r0 = o.Star!.RadiusCm.Value;
        // Task 8: the radii as drawn, confined behind the reverse shock (the shocks have started by 30 d).
        Assert.NotNull(s.Shocks);
        Assert.Equal(ParticleKinematics.RadiusCm(set.MaxCmS, s.SinceBreakoutS, r0, set.MaxCmS, s.Shocks), s.MaxRadiusCm, 3);
        double f99 = scene.Structure!.Frame99CmS;
        Assert.Equal(ParticleKinematics.RadiusCm(f99, s.SinceBreakoutS, r0, set.MaxCmS, s.Shocks), s.Frame99RadiusCm, 3);
    }

    [Fact]
    public void FailedSupernova_EnvelopeShell_StartsAtItsLaunch_R_P2_19()
    {
        var failed = TestOutcomes.Custom(SupernovaType.None, null, null, null, 5e13,
            TestOutcomes.BlackHole(10, 1.5), Fate.FailedSupernova);
        var scene = new ExplosionScene(failed);
        double launch = scene.Structure!.EnvelopeShellLaunchS!.Value;
        Assert.True(launch > 1.5);
        Assert.False(scene.At(launch * 0.5).ShowParticles);
        double t = launch * 1.5;
        var s = scene.At(t);
        Assert.True(s.ShowParticles);
        Assert.Equal(t - launch, s.SinceBreakoutS, 6);
        Assert.Equal(EjectaKinematics.TemperatureFloorK, s.EjectaTemperatureK);
        Assert.Contains(s.Notes, n => scene.Structure.Notes.Contains(n));
        Assert.Contains(s.Notes, n => n.Contains("weak outer envelope", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- Task 8: shocks

    [Fact]
    public void Shocks_AreInTheState_WithTheirNotes_FromTheirStart()
    {
        var o = Exploding();
        var scene = new ExplosionScene(o);
        double start = ShockStructureModel.StartTimeS(o);
        var before = scene.At(0.9 * start);
        Assert.Null(before.Shocks);
        Assert.DoesNotContain(ExplosionScene.ShockNote, before.Notes);

        var s = scene.At(30 * Year);
        Assert.Equal(ShockStructureModel.At(o, 30 * Year), s.Shocks);
        Assert.Contains(ExplosionScene.ShockNote, s.Notes);
        Assert.StartsWith("Shocks: radii from the radio model's shock with Chevalier 1982's ratios", ExplosionScene.ShockNote);
        Assert.Contains("Truelove & McKee 1999 and Tang & Chevalier 2017", ExplosionScene.ShockNote);

        // Ruling R-P2-25: the first readout line stays Project 1's debris radius; a second line gives the drawn forward shock,
        // labelled by era; from 10 yr a note gives Project 1's uniform-ejecta value (R-P2-24).
        double p1 = EjectaKinematics.RadiusCm(o, 30 * Year);
        Assert.Equal(p1, s.RadiusCm);
        Assert.Equal("Blast-wave radius", s.RadiusLabel);
        Assert.Equal(s.Shocks!.ForwardCm, s.ForwardShockCm);
        Assert.Equal("Forward shock (steep-ejecta solution)", s.ForwardShockLabel);
        Assert.Contains($"The remnant model behind the panels assumes uniform ejecta and gives {UnitFormat.Distance(p1)}; see scientific inconsistency #33", s.Notes);
        Assert.True(s.FrameRadiusCm >= s.Shocks.ForwardCm && s.FrameRadiusCm >= p1);

        // The wind era (radio model's shock) and the 3-10 yr blend (a neutral label); the readout's first line is Project 1's.
        var early = scene.At(Year);
        Assert.NotNull(early.Shocks);
        Assert.Equal(EjectaKinematics.RadiusCm(o, Year), early.RadiusCm);
        Assert.Equal("Ejecta radius", early.RadiusLabel);
        Assert.Equal("Forward shock (radio model's shock)", early.ForwardShockLabel);
        Assert.Equal(early.Shocks!.ForwardCm, early.ForwardShockCm);
        Assert.Equal(Math.Max(early.EjectaRadiusCm, early.Frame99RadiusCm), early.FrameRadiusCm);   // before 3 yr: the debris frame
        var blend = scene.At(5 * Year);
        Assert.Equal("Forward shock (join between the wind and remnant models; schematic)", blend.ForwardShockLabel);
        Assert.Equal("Outer radius", blend.RadiusLabel);

        // Before the shocks start there is no second line.
        Assert.True(double.IsNaN(before.ForwardShockCm));
        Assert.Equal("", before.ForwardShockLabel);
    }

    [Fact]
    public void FrameRadius_BlendsToIncludeTheForwardShock_WithoutJumping()
    {
        // Ruling R-P2-25: geometric blend from the debris frame to max(debris frame, R_f) over 3-10 yr, smoothstep in log t.
        var outcomes = new[]
        {
            TestOutcomes.Engine.Compute(LabInputs.Default), Exploding(), TestOutcomes.TypeIa(),
            TestOutcomes.Engine.Compute(PresetCatalog.Get("casa").Inputs, PresetCatalog.Get("casa").Pins),
        };
        foreach (var o in outcomes)
        {
            var scene = new ExplosionScene(o);
            foreach (double edge in new[] { EjectaKinematics.BlendStartS, RemnantEvolutionModel.StartS })
            {
                double a = scene.At(edge * (1 - 1e-9)).FrameRadiusCm, b = scene.At(edge * (1 + 1e-9)).FrameRadiusCm;
                Assert.True(Math.Abs(b / a - 1) < 1e-6, $"{o.Type} frame at {edge / Year} yr: {a} -> {b}");
            }
            foreach (double yr in new[] { 10.0, 30, 100, 300, 1000 })
            {
                var s = scene.At(yr * Year);
                Assert.True(s.FrameRadiusCm >= s.Shocks!.ForwardCm * (1 - 1e-12), $"{o.Type} at {yr} yr");
            }
            // Midway (√30 yr, weight 1/2) the frame is the geometric mean of the debris frame and max(debris frame, R_f).
            var m = scene.At(Math.Sqrt(30) * Year);
            double debris = Math.Max(m.EjectaRadiusCm, double.IsNaN(m.Frame99RadiusCm) ? 0 : m.Frame99RadiusCm);
            Assert.Equal(1, m.FrameRadiusCm / Math.Sqrt(debris * Math.Max(debris, m.Shocks!.ForwardCm)), 9);
        }
    }

    [Fact]
    public void HotGasTint_IsTheBlackbodyColourAt1e7K()
    {
        Assert.Equal(BlackbodyColor.FromTemperature(1e7), ExplosionScene.HotGasColor);
        var c = ExplosionScene.HotGasColor;
        Assert.Equal(1f, c.B);              // normalized: the brightest channel is 1 (blue for gas this hot)
        Assert.True(c.R < c.G && c.G < c.B);
    }
}
