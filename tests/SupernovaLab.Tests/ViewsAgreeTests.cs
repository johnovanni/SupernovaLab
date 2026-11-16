using System.Windows;
using SupernovaLab.App.Controllers;
using SupernovaLab.App.Services;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>
/// Plan Task 6, criterion 4: the main 3D view and the Close-up agree on the remnant (spec §4.3, §4.6, §7.2): the same position (the
/// kick's drift), the same beam tilt and half-width, the same spin period, and one beam phase from the shared <see cref="BeamClock"/>.
/// </summary>
public class ViewsAgreeTests
{
    private const double Year = PhysicalConstants.Year;

    /// <summary>The Crab's age in 1991, when its 33.39 ms period was measured (the preset's fact).</summary>
    private const double CrabAgeS = 2.9568e10;

    private static Outcome Preset(string id)
    {
        var p = PresetCatalog.Get(id);
        return TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
    }

    private static readonly Outcome Crab = Preset("sn1054");
    private static readonly ObserverSetup CrabObs = PresetCatalog.Get("sn1054").Observer;

    private static IEnumerable<double> LogTimes(double from, double to, int n)
    {
        for (int i = 0; i < n; i++) yield return from * Math.Pow(to / from, i / (double)(n - 1));
    }

    // ---- Brief tests (plan Task 6, Step 1) ----

    [Theory, InlineData("sn1054", 1), InlineData("casa", 1), InlineData("sn1987a", 3)]
    public void BothViews_SameRemnantPosition(string id, int run)   // criterion 4
    {
        var o = Preset(id);
        var obs = PresetCatalog.Get(id).Observer;
        var main = new ExplosionScene(o, obs.TimeDilation, obs, run);
        foreach (var t in LogTimes(1, 3.15e10, 40))
        {
            Assert.Equal(KickDrift.OffsetCm(o, obs, run, t), main.At(t).RemnantOffsetCm);
            if (NeutronStarModel.At(o, obs, run, t) is { } ns) Assert.Equal(ns.OffsetCm, main.At(t).RemnantOffsetCm);
        }
    }

    [Fact]
    public void BothViews_SameBeamTiltAndWidth()
    {
        var s = new ExplosionScene(Crab, 1, CrabObs, 1).At(CrabAgeS);
        Assert.Equal(SceneMarker.Pulsar, s.Marker);
        Assert.Equal(Magnetosphere.TiltRad(Crab), s.BeamTiltRad);
        Assert.Equal(62 * Math.PI / 180, s.BeamTiltRad, 12);   // the Crab's published inclination (C5), not the schematic 45°
        Assert.Equal(Magnetosphere.BeamHalfAngleRad(s.PulsarPeriodS), s.BeamHalfAngleRad);

        var close = new CloseUpScene(Crab, CrabObs, 1).At(CrabAgeS);
        Assert.Equal(s.BeamTiltRad, close.BeamTiltRad);
        Assert.Equal(s.BeamHalfAngleRad, close.BeamHalfAngleRad);
        Assert.Equal(s.PulsarPeriodS, close.NeutronStar!.PeriodS);
        Assert.Equal(Magnetosphere.DrawnPeriodS(s.PulsarPeriodS), close.DrawnPeriodS);
    }

    [Fact]
    public void BothViews_SamePhaseFromBeamClock()
    {
        double wall = 7.3;
        var clock = new BeamClock(() => wall);
        var main = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        main.Lab.SelectedPreset = PresetCatalog.Get("sn1054");
        main.Timeline.ObserverTimeS = CrabAgeS * main.CurrentObserver.TimeDilation;
        var scene = new SceneController(main, () => new NullHost(), beamClock: clock);
        var closeUp = new CloseUpSceneController(new CloseUpViewModel(main), () => new NullHost(), clock);
        Assert.Equal(SceneMarker.Pulsar, scene.State.Marker);
        Assert.True(closeUp.ShowsBeams);
        Assert.Equal(2 * Math.PI * 7.3 / 2, scene.BeamPhaseRad, 9);   // the Crab is drawn at 2 s a turn in both views
        Assert.Equal(scene.BeamPhaseRad, closeUp.BeamPhaseRad);
        wall = 9.05;
        Assert.Equal(scene.BeamPhaseRad, closeUp.BeamPhaseRad);
        Assert.Equal(2 * Math.PI * 9.05 / 2, closeUp.BeamPhaseRad, 9);
        scene.Dispose();
        closeUp.Dispose();
    }

    // ---- Further checks ----

    /// <summary>
    /// Flash safety (spec C2): the drawn spin never turns faster than once per 2 s, even while the period changes (a magnetar scrubbed
    /// through its spin-down). The clock integrates the phase, so a new period changes the rate, never the phase already turned.
    /// </summary>
    [Fact]
    public void BeamClock_IntegratesThePhase_SoAChangingPeriodNeverSpeedsItUp()
    {
        double wall = 600;   // ten minutes into the session
        var clock = new BeamClock(() => wall);
        double previous = clock.PhaseRad(5);
        Assert.Equal(2 * Math.PI * 600 / 5, previous, 9);
        double period = 5;
        for (int frame = 1; frame <= 600; frame++)
        {
            wall += 1.0 / 60;
            period *= 1.002;   // the period grows by 2% each tenth of a second
            double phase = clock.PhaseRad(Magnetosphere.DrawnPeriodS(period));
            double rate = (phase - previous) * 60;   // rad per wall second
            Assert.InRange(rate, 0, 2 * Math.PI / Magnetosphere.MinDrawnPeriodS + 1e-9);
            previous = phase;
        }
        Assert.Equal(previous, clock.PhaseRad(period), 12);   // reading again at the same instant changes nothing
        Assert.Equal(0, clock.PhaseRad(double.NaN));
    }

    [Fact]
    public void MainView_MarkerDrifts_ButTheFramingIsTheDebris()   // spec §7.2
    {
        var o = Preset("casa");
        var obs = PresetCatalog.Get("casa").Observer;
        var drifting = new ExplosionScene(o, 1, obs, 1).At(340 * Year);
        var still = new ExplosionScene(o, 1).At(340 * Year);
        Assert.True(drifting.RemnantOffsetCm.Length > 0.05 * drifting.FrameRadiusCm, "the marker is visibly off-centre");
        Assert.True(drifting.RemnantOffsetCm.Length < drifting.FrameRadiusCm, "still inside the debris");
        Assert.Equal(still.FrameRadiusCm, drifting.FrameRadiusCm);   // the director frames the debris, not the marker
        // About 433 km/s for 340 yr (Holland-Ashford et al. 2024; §C4.4).
        Assert.Equal(o.Remnant.KickKmS!.Value * 1e5 * 340 * Year, drifting.RemnantOffsetCm.Length, 1e-6 * drifting.RemnantOffsetCm.Length);
    }

    [Fact]
    public void MainView_ReRoll_MovesAnUnpinnedKick()
    {
        var o = Preset("sn1987a");
        var obs = PresetCatalog.Get("sn1987a").Observer;
        Assert.Null(o.KickPositionAngleDeg);
        var a = new ExplosionScene(o, 1, obs, 1).At(30 * Year).RemnantOffsetCm;
        var b = new ExplosionScene(o, 1, obs, 2).At(30 * Year).RemnantOffsetCm;
        Assert.NotEqual(a, b);
        Assert.Equal(a.Length, b.Length, 1e-6 * a.Length);
    }

    [Fact]
    public void MainViewController_FollowsReRoll_AsTheCloseUpDoes()
    {
        var main = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        main.Lab.SelectedPreset = PresetCatalog.Get("sn1987a");
        main.Timeline.ObserverTimeS = 30 * Year * main.CurrentObserver.TimeDilation;
        var scene = new SceneController(main, () => new NullHost());
        var closeUp = new CloseUpViewModel(main);
        var before = scene.State.RemnantOffsetCm;
        Assert.Equal(closeUp.State.NeutronStar!.OffsetCm, before);
        main.RerollCommand.Execute(null);
        Assert.NotEqual(before, scene.State.RemnantOffsetCm);
        Assert.Equal(closeUp.State.NeutronStar!.OffsetCm, scene.State.RemnantOffsetCm);
        closeUp.Dispose();
        scene.Dispose();
    }

    /// <summary>A GL host that never draws (the controllers' state only).</summary>
    private sealed class NullHost : IRenderHost
    {
        public bool Animating { get; set; }
        public event Action<string>? ContextReady { add { } remove { } }
        public event Action<TimeSpan, Size>? Draw { add { } remove { } }
        public event Action<Exception>? FrameFailed { add { } remove { } }
        public void Start() { }
        public void RequestRender() { }
        public bool Shutdown(Action release) => false;
    }
}
