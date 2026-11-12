using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
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
        Assert.Contains(s.Notes, n => n.Contains("inside the star", StringComparison.Ordinal));
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
        Assert.Equal(s.EjectaRadiusCm, s.FrameRadiusCm);
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
}
