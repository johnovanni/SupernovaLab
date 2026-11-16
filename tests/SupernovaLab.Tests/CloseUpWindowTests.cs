using System.IO;
using System.Windows;
using System.Windows.Threading;
using SupernovaLab.App.Controllers;
using SupernovaLab.App.Services;
using SupernovaLab.App.ViewModels;
using SupernovaLab.App.Windows;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>
/// Plan Task 4: the Close-up window's view model, its controller and the window service (spec §2.2, §2.5, §8.4), without OpenGL. The
/// windows are made off-screen on an STA thread (no screen capture), as <see cref="InstrumentsWindowTests"/> does; the real
/// <see cref="WindowService"/> is given a plain window for the Close-up so no GL context is made.
/// </summary>
public class CloseUpWindowTests
{
    /// <summary>A GL host that draws nothing: Tick raises one frame; ContextReady is never raised (no renderers are made).</summary>
    private sealed class FakeHost : IRenderHost
    {
        public int Renders { get; private set; }
        public bool Animating { get; set; }
        public bool ShutDown { get; private set; }
        public event Action<string>? ContextReady;
        public event Action<TimeSpan, Size>? Draw;
        public event Action<Exception>? FrameFailed;
        public Exception? StartFailure { get; init; }
        public void Start() { if (StartFailure is not null) throw StartFailure; }
        public void RequestRender() => Renders++;
        public void Tick() => Draw?.Invoke(TimeSpan.FromSeconds(1.0 / 60), new Size(900, 600));
        public void FirstFrame(string glInfo)
        {
            try { ContextReady?.Invoke(glInfo); }
            catch (Exception e) { FrameFailed?.Invoke(e); }
        }

        /// <summary>As GlHost with its context alive: runs the release with the context current and reports it ran.</summary>
        public bool Shutdown(Action release)
        {
            ShutDown = true;
            release();
            return true;
        }
    }

    private static (MainViewModel Main, FakeWindowService Fake) NewMain()
    {
        var fake = new FakeWindowService();
        return (new MainViewModel(TestOutcomes.Engine, fake), fake);
    }

    /// <summary>
    /// Plan Task 9: the tests of the settled remnant open the Close-up at this observer time, after the core's first second (the
    /// PreCollapse and FirstSecond eras before it are tested in <see cref="CoreCollapseHistoryTests"/> and below).
    /// </summary>
    private const double SettledObserverTimeS = 100;

    private static CloseUpViewModel OpenCloseUp(string preset, out MainViewModel main)
    {
        (main, _) = NewMain();
        main.Lab.SelectedPreset = PresetCatalog.Get(preset);
        main.Timeline.ObserverTimeS = SettledObserverTimeS;
        return new CloseUpViewModel(main);
    }

    private static CloseUpSceneController Controller(FakeHost host, string preset = "n6946bh1")
        => new(OpenCloseUp(preset, out _), () => host);

    // ---- Brief tests (plan Task 4, Step 1) ----

    /// <summary>Ruling S3: the view-model side, two commands make two service calls (the service brings the window to the front).</summary>
    [Fact]
    public void ShowCloseUp_OpensOnce()
    {
        var (vm, fake) = NewMain();
        vm.ShowCloseUpCommand.Execute(null);
        vm.ShowCloseUpCommand.Execute(null);
        Assert.Equal(2, fake.CloseUpCalls);   // service brings to front
    }

    [Fact]
    public void CloseUpViewModel_FollowsOutcomeChange_NoException()      // Review Focus 1
    {
        var cu = OpenCloseUp(preset: "n6946bh1", out var main);
        Assert.Equal(CloseUpEra.BlackHole, cu.State.Era);
        main.Lab.SelectedPreset = PresetCatalog.Get("sn2011fe");
        Assert.NotNull(cu.NoRemnantMessage);
        main.RerollCommand.Execute(null);
        Assert.Equal(CloseUpScene.DestroyedMessage, cu.NoRemnantMessage);
        Assert.Equal(0, cu.ModelErrorCount);
    }

    [Fact]
    public void CloseUpController_StopsDrawingWhenHidden_ReleasesOnClose()   // Review Focus 3, with a fake IRenderHost
    {
        var fakeHost = new FakeHost();
        var c = Controller(fakeHost);
        c.Start();
        c.SetVisible(false);
        int rendersWhileHidden = fakeHost.Renders;
        fakeHost.Tick();
        Assert.Equal(0, c.FramesDrawn);
        Assert.Equal(1, c.SkippedFrames);
        Assert.False(fakeHost.Animating);
        c.ForceAnimating = true;                         // even the bench's every-frame switch does not draw while hidden
        Assert.False(fakeHost.Animating);
        Assert.Equal(rendersWhileHidden, fakeHost.Renders);
        c.SetVisible(true);
        Assert.True(fakeHost.Animating);
        Assert.True(fakeHost.Renders > rendersWhileHidden);   // shown again: a frame is asked for
        c.Dispose();
        Assert.True(c.GlReleased);
        Assert.True(fakeHost.ShutDown);
    }

    // ---- The window service (ruling S3) and the window ----

    /// <summary>
    /// Ruling S3: the real <see cref="WindowService"/> opens the Close-up at most once (a second request activates it), makes a new one
    /// after it closes, gives it its own view model on the shared one, remembers its bounds, and closes it with the main window.
    /// </summary>
    [Fact]
    public void WindowService_OpensTheCloseUpAtMostOnce_AndClosesItWithTheMainWindow()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                InstrumentsWindowTests.EnsureApplication();
                var service = new WindowService(closeUpFactory: _ => OffScreen(new Window()));
                var vm = new MainViewModel(TestOutcomes.Engine, service);
                var owner = OffScreen(new Window { DataContext = vm });
                owner.Show();
                service.Attach(owner);

                vm.ShowCloseUpCommand.Execute(null);
                var first = service.CloseUp;
                vm.ShowCloseUpCommand.Execute(null);
                Pump();
                Assert.NotNull(first);
                Assert.Same(first, service.CloseUp);
                Assert.Equal(1, service.CloseUpWindowsMade);
                Assert.Same(owner, first!.Owner);
                var cu = Assert.IsType<CloseUpViewModel>(first.DataContext);
                Assert.Same(vm, cu.Main);

                first.Width = 777;
                first.Close();
                Pump();
                Assert.Null(service.CloseUp);
                vm.ShowCloseUpCommand.Execute(null);
                var second = service.CloseUp;
                Assert.NotNull(second);
                Assert.NotSame(first, second);
                Assert.Equal(2, service.CloseUpWindowsMade);
                Assert.Equal(777, second!.Width, 0);       // the size is remembered for the session (to the pixel)

                owner.Close();                              // owned windows close with their owner
                Pump();
                Assert.Null(service.CloseUp);
            }
            catch (Exception e)
            {
                error = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(error);
    }

    /// <summary>The window's XAML loads with the theme and binds to its view model (constructed, not shown: no GL context is made).</summary>
    [Fact]
    public void CloseUpWindow_LoadsItsXaml_AndTakesItsTitleFromTheViewModel()
    {
        Exception? error = null;
        string? title = null;
        var thread = new Thread(() =>
        {
            try
            {
                InstrumentsWindowTests.EnsureApplication();
                var cu = OpenCloseUp("n6946bh1", out _);
                var w = new CloseUpWindow { DataContext = cu };
                Pump();
                title = w.Title;
                Assert.Equal(900, w.Width);
                Assert.Equal(640, w.Height);
                Assert.Null(w.Scene);   // made only once the window has loaded
                w.Close();
                cu.Dispose();
            }
            catch (Exception e)
            {
                error = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(error);
        Assert.Equal("Close-up: Black hole", title);
    }

    // ---- The controller and view model ----

    [Fact]
    public void CloseUpController_BelowOpenGl41_NamesTheRequirement()   // spec §8.4
    {
        var host = new FakeHost();
        var c = Controller(host);
        c.Start();
        host.FirstFrame("Mesa | llvmpipe | 3.3 (Core Profile) Mesa 23.0");
        Assert.True(c.IsFailed);
        Assert.Contains("OpenGL 4.1", c.FailureMessage);
        Assert.Contains("readouts", c.FailureMessage);
        Assert.False(c.IsReady);
    }

    [Fact]
    public void CloseUpController_HostThatCannotStart_Fails_AndTheViewModelCarriesOn()
    {
        var cu = OpenCloseUp("sn1998bw", out var main);
        var c = new CloseUpSceneController(cu, () => new FakeHost { StartFailure = new InvalidOperationException("no pixel format") });
        c.Start();
        Assert.True(c.IsFailed);
        main.Timeline.ObserverTimeS = 1e5;
        Assert.Equal(CloseUpEra.BlackHole, cu.State.Era);
        c.Dispose();
    }

    [Fact]
    public void CloseUpController_FramesTheBlackHole_SideOnToTheSpinAxis()
    {
        var cu = OpenCloseUp("n6946bh1", out _);
        var c = new CloseUpSceneController(cu, () => new FakeHost());
        var state = cu.State;
        Assert.Equal(CloseUpSceneController.DefaultPitchDeg, c.Camera.PitchDeg);   // spec §6.4: the camera starts side-on
        Assert.True(c.Director.OuterRingCm >= state.FrameRadiusKm * 1e5 / CameraDirector.JumpFraction * (1 - 1e-9));
        Assert.Equal(SceneController.AutoDistance * c.Director.HalfHeightCm / 1e5, c.Camera.DistanceMpc, 9);   // scene unit 1 km
        c.Zoom(2);
        Assert.False(c.IsAutoFraming);
        c.Home();
        Assert.True(c.IsAutoFraming);
        Assert.Equal(SceneController.AutoDistance * c.Director.HalfHeightCm / 1e5, c.Camera.DistanceMpc, 9);
        c.Dispose();
    }

    /// <summary>
    /// Plan Task 10 (spec §6.3): with the grid on, the view labels the horizon, the photon orbits and the ergosphere; off, nothing. The
    /// lensing pass gets the grid switch and the hole's drawn spin, and its grid colour never asks for a retrace.
    /// </summary>
    [Fact]
    public void CloseUpController_GridLabelsTheHorizonPhotonOrbitsAndErgosphere()
    {
        var cu = OpenCloseUp("n6946bh1", out _);
        var c = new CloseUpSceneController(cu, () => new FakeHost());
        c.SetViewSize(new Size(900, 640));
        Assert.Empty(c.ViewLabels);
        cu.ShowGrid = true;
        var texts = c.ViewLabels.Select(l => l.Text).ToList();
        Assert.Contains(LensOverlay.HorizonLabel, texts);
        Assert.Contains(LensOverlay.ProgradeLabel, texts);   // n6946bh1 settles at spin 0.1
        Assert.DoesNotContain(LensOverlay.ErgosphereLabel, texts);   // fix round 1: labelled from spin 0.3 (at 0.1 it hugs the horizon)
        var u = c.RaysFor(new Size(900, 640));
        Assert.True(u.Grid);
        Assert.Equal((float)cu.State.Spin, u.Spin);
        Assert.True(RayMapPlanner.SameRays(u, u with { GridColor = new OpenTK.Mathematics.Vector3(1, 2, 3) }));
        cu.ShowLabels = false;
        Assert.Empty(c.ViewLabels);
        c.Dispose();
    }

    [Fact]
    public void CloseUpController_GovernorStepsTheLensingLevelDown_AndTheBenchCanHoldIt()   // ruling P3-9
    {
        var c = Controller(new FakeHost());
        Assert.Equal(LensingLevel.Full, c.Level);
        for (int k = 0; k < 200; k++) c.FeedRenderTime(0.020, 1.0 / 60);
        Assert.Equal(LensingLevel.Half, c.Level);
        c.AutoLevel = false;
        for (int k = 0; k < 400; k++) c.FeedRenderTime(0.020, 1.0 / 60);
        Assert.Equal(LensingLevel.Half, c.Level);
        c.SetLevel(LensingLevel.Quarter);
        Assert.Equal(LensingLevel.Quarter, c.Level);
        c.Dispose();
    }

    [Fact]
    public void CloseUpController_DisposedStopsFollowingTheViewModel()
    {
        var cu = OpenCloseUp("n6946bh1", out var main);
        var host = new FakeHost();
        var c = new CloseUpSceneController(cu, () => host);
        c.Start();
        c.Dispose();
        int renders = host.Renders;
        main.Timeline.ObserverTimeS = 1e6;
        main.Lab.SelectedPreset = PresetCatalog.Get("sn1998bw");
        Assert.Equal(renders, host.Renders);
    }

    [Fact]
    public void CloseUpViewModel_DisposedStopsFollowingTheMainViewModel()
    {
        var cu = OpenCloseUp("n6946bh1", out var main);
        int changes = 0;
        cu.StateChanged += (_, _) => changes++;
        cu.Dispose();
        main.Lab.SelectedPreset = PresetCatalog.Get("sn2011fe");
        main.Timeline.ObserverTimeS = 1e6;
        Assert.Equal(0, changes);
        Assert.Equal(CloseUpEra.BlackHole, cu.State.Era);
    }

    [Fact]
    public void CloseUpViewModel_Readouts_CarryTheirBadgesAndSources()
    {
        var cu = OpenCloseUp("n6946bh1", out _);
        Assert.Equal(new[] { "Mass", "Horizon", "Shadow", "Spin", "Kick", "From Earth" }, cu.Readouts.Select(r => r.Label));
        Assert.All(cu.Readouts, r => Assert.Contains("Source:", r.BadgeToolTip));
        Assert.Equal("Close-up: Black hole", cu.WindowTitle);
        Assert.True(cu.HasRemnant);
        Assert.Equal(CloseUpScene.NoTelescopeNote, cu.NoTelescopeNote);
        Assert.True(cu.ShowLabels);
        cu.ToggleFocusCommand.Execute(null);
        Assert.True(cu.IsFocusView);
    }

    [Fact]
    public void CloseUpViewModel_BenchSpin_RebuildsWithThatSpin()
    {
        var cu = OpenCloseUp("sn1998bw", out _);
        bool rebuilt = false;
        cu.StateChanged += (_, r) => rebuilt |= r;
        cu.BenchSpin = 0.9;
        Assert.True(rebuilt);
        Assert.Equal(0.9, cu.State.Spin);
        Assert.Contains(cu.Readouts, r => r.Label == "Spin");
    }

    [Fact]
    public void BeamClock_OneClockGivesBothViewsOnePhase()   // ruling P3-5
    {
        double t = 3;
        var clock = new BeamClock(() => t);
        Assert.Equal(2 * Math.PI * 3 / 2, clock.PhaseRad(2), 12);
        Assert.Equal(0, clock.PhaseRad(0));
        var (main, _) = NewMain();
        var scene = new SceneController(main, () => new FakeHost(), beamClock: clock);
        var closeUp = new CloseUpSceneController(new CloseUpViewModel(main), () => new FakeHost(), clock);
        Assert.Same(scene.BeamClock, closeUp.BeamClock);
        scene.Dispose();
        closeUp.Dispose();
    }

    /// <summary>
    /// Review fix 1: however far the wheel zooms in, the camera stays at least 3 GM/c² from a black hole (outside the ergosphere, so
    /// the tracer's camera assumption holds and the view never goes silently black) and 1.1 radii from a neutron star.
    /// </summary>
    [Fact]
    public void Zoom_NeverPutsTheCameraInsideTheHoleOrTheStar()
    {
        var bh = OpenCloseUp("n6946bh1", out _);
        var c = new CloseUpSceneController(bh, () => new FakeHost());
        double rgKm = bh.State.GravitationalRadiusKm;
        for (int k = 0; k < 20; k++) c.Zoom(10);
        Assert.Equal(3 * rgKm, c.Camera.DistanceMpc, 9);
        Assert.Equal(3 * rgKm, c.MinCameraDistanceKm, 9);
        Assert.True(c.Camera.Eye.Length * 1e5 / Core.Compact.BlackHoleOptics.GravitationalRadiusCm(bh.State.MassMsun) >= 3 - 1e-9);
        c.Zoom(-3);
        Assert.True(c.Camera.DistanceMpc > 3 * rgKm);   // zooming out again is free
        c.Dispose();

        var ns = OpenCloseUp("sn1054", out _);   // the Crab: a neutron star
        Assert.Equal(CloseUpEra.NeutronStar, ns.State.Era);
        var star = new CloseUpSceneController(ns, () => new FakeHost());
        for (int k = 0; k < 20; k++) star.Zoom(10);
        double expected = Math.Max(3 * ns.State.GravitationalRadiusKm, 1.1 * ns.State.SurfaceRadiusKm);
        Assert.Equal(1.1 * ns.State.SurfaceRadiusKm, expected, 9);   // for a neutron star the surface sets the limit
        Assert.Equal(expected, star.Camera.DistanceMpc, 9);
        star.Dispose();
    }

    private static Window OffScreen(Window w)
    {
        w.WindowStartupLocation = WindowStartupLocation.Manual;
        w.Left = -30000;
        w.Top = -30000;
        w.ShowActivated = false;
        w.ShowInTaskbar = false;
        return w;
    }

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    // ---- Task 4b: the light-bending detail (ruling R-P3-9) ----

    [Fact]
    public void LensingDetail_RoundTripsThroughLayoutStore_AndAnUnknownStoredValueIsAuto()
    {
        string dir = Path.Combine(Path.GetTempPath(), "snl-detail-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LayoutStore(dir);
            Assert.Equal(LensingDetail.Auto, store.Load().CloseUpLensingDetail);   // no file: Auto
            foreach (var d in Enum.GetValues<LensingDetail>())
            {
                store.Save(new LayoutSettings { CloseUpLensingDetail = d, FocusView = true });
                Assert.Equal(d, store.Load().CloseUpLensingDetail);
            }
            Assert.Contains("\"CloseUpLensingDetail\": \"Quarter\"", File.ReadAllText(store.Path));   // stored by name
            foreach (string stored in new[] { "\"Ultra\"", "7", "2", "null", "true", "{}", "\"\"" })
            {
                File.WriteAllText(store.Path, $"{{\"FocusView\": true, \"CloseUpLensingDetail\": {stored}, \"KeyFolded\": true}}");
                var loaded = store.Load();
                Assert.Equal(LensingDetail.Auto, loaded.CloseUpLensingDetail);
                Assert.True(loaded.FocusView && loaded.KeyFolded, $"the rest of the layout survives {stored}");
            }
            File.WriteAllText(store.Path, "{\"CloseUpLensingDetail\": \"half\"}");   // names are read case-insensitively
            Assert.Equal(LensingDetail.Half, store.Load().CloseUpLensingDetail);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void CloseUpViewModel_LensingDetail_IsTheSavedLayoutSetting()
    {
        string dir = Path.Combine(Path.GetTempPath(), "snl-detail-" + Guid.NewGuid().ToString("N"));
        try
        {
            var main = new MainViewModel(TestOutcomes.Engine, new FakeWindowService(), new LayoutStore(dir));
            var cu = new CloseUpViewModel(main);
            Assert.Equal(LensingDetail.Auto, cu.LensingDetail);
            var changed = new List<string?>();
            cu.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            cu.LensingDetail = LensingDetail.Half;
            Assert.Contains(nameof(CloseUpViewModel.LensingDetail), changed);
            Assert.Contains(nameof(CloseUpViewModel.Notes), changed);
            Assert.Equal(LensingDetail.Half, main.Layout.CloseUpLensingDetail);
            main.Layout.Flush();
            cu.Dispose();

            var again = new CloseUpViewModel(new MainViewModel(TestOutcomes.Engine, new FakeWindowService(), new LayoutStore(dir)));
            Assert.Equal(LensingDetail.Half, again.LensingDetail);   // remembered between runs
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void CloseUpViewModel_LightBendingNote_NamesTheLevelShownAndWhoChoseIt()
    {
        var cu = OpenCloseUp("n6946bh1", out _);
        Assert.Contains("Light bending: full resolution (automatic)", cu.Notes);
        cu.ShownLensingLevel = LensingLevel.Quarter;   // the governor's level while the camera moves
        Assert.Contains("Light bending: quarter resolution (automatic)", cu.Notes);
        cu.LensingDetail = LensingDetail.Half;
        cu.ShownLensingLevel = LensingLevel.Half;
        Assert.Contains("Light bending: half resolution (your setting)", cu.Notes);
        Assert.Single(cu.Notes, n => n.StartsWith("Light bending:", StringComparison.Ordinal));

        var none = OpenCloseUp("sn2011fe", out _);   // nothing lensed: no note
        Assert.DoesNotContain(none.Notes, n => n.StartsWith("Light bending:", StringComparison.Ordinal));
    }

    [Fact]
    public void CloseUpController_HonoursTheDetail_AutoIsTheGovernor()
    {
        var cu = OpenCloseUp("n6946bh1", out _);
        var c = new CloseUpSceneController(cu, () => new FakeHost());
        Assert.Equal(LensingDetail.Auto, c.Detail);
        Assert.Equal((LensingLevel.Full, LensingLevel.Full), (c.MovingLevel, c.StillLevel));
        for (int k = 0; k < 200; k++) c.FeedRenderTime(0.020, 1.0 / 60);
        Assert.Equal((LensingLevel.Half, LensingLevel.Full), (c.MovingLevel, c.StillLevel));   // Auto: moving governed, still full

        cu.LensingDetail = LensingDetail.Quarter;
        Assert.Equal(LensingDetail.Quarter, c.Detail);
        Assert.Equal((LensingLevel.Quarter, LensingLevel.Quarter), (c.MovingLevel, c.StillLevel));
        cu.LensingDetail = LensingDetail.Full;
        for (int k = 0; k < 400; k++) c.FeedRenderTime(0.050, 1.0 / 60);   // the user's level is never stepped down
        Assert.Equal((LensingLevel.Full, LensingLevel.Full), (c.MovingLevel, c.StillLevel));
        cu.LensingDetail = LensingDetail.Auto;   // back to Auto: the governor starts again from full
        Assert.Equal(LensingLevel.Full, c.MovingLevel);
        c.Dispose();
    }

    /// <summary>R-P3-10 at the controller: the rays it hands the tracer do not change with the time, only with the camera.</summary>
    [Fact]
    public void CloseUpController_TracedRays_DependOnTheCamera_NotOnTheTime()
    {
        var cu = OpenCloseUp("n6946bh1", out var main);
        var c = new CloseUpSceneController(cu, () => new FakeHost());
        var view = new Size(900, 600);
        var before = c.RaysFor(view);
        main.Timeline.ObserverTimeS = 3e7;
        Assert.True(RayMapPlanner.SameRays(before, c.RaysFor(view)));
        c.Rotate(10, 0);
        Assert.False(RayMapPlanner.SameRays(before, c.RaysFor(view)));
        c.Dispose();
    }

    /// <summary>
    /// Task 4b's on-demand drawing: a time step that changes nothing the Close-up draws asks for no frame; one that moves the
    /// environment past the refresh step (ruling P3-6) does.
    /// </summary>
    [Fact]
    public void CloseUpController_ATimeStepThatChangesNothingDrawn_AsksForNoFrame()
    {
        var cu = OpenCloseUp("n6946bh1", out var main);
        var host = new FakeHost();
        var c = new CloseUpSceneController(cu, () => host);
        c.Start();
        main.Timeline.ObserverTimeS = 1e6;
        c.MarkEnvironmentCapturedForTest();   // as if the cube map had just been drawn at this time
        int renders = host.Renders;
        main.Timeline.ObserverTimeS = 1e6 * 1.001;   // 0.0004 in log10: below the 0.005 step
        Assert.Equal(renders, host.Renders);
        main.Timeline.ObserverTimeS = 1e6 * 1.02;    // 0.0086: the surroundings must be redrawn
        Assert.True(host.Renders > renders);
        c.Dispose();
    }

    [Fact]
    public void CloseUpViewModel_DetailMenuItems_AreRadioItems()
    {
        var cu = OpenCloseUp("n6946bh1", out _);
        Assert.True(cu.IsDetailAuto);
        cu.IsDetailQuarter = true;
        Assert.Equal(LensingDetail.Quarter, cu.LensingDetail);
        Assert.True(cu.IsDetailQuarter);
        Assert.False(cu.IsDetailAuto || cu.IsDetailFull || cu.IsDetailHalf);
        cu.IsDetailQuarter = false;   // a click on the checked item keeps it
        Assert.Equal(LensingDetail.Quarter, cu.LensingDetail);
        cu.IsDetailFull = true;
        Assert.Equal(LensingDetail.Full, cu.LensingDetail);
    }

    /// <summary>Ruling proposal R-P3-11 as a pure function: which time steps redraw the Close-up.</summary>
    [Fact]
    public void TimeStepNeedsFrame_OnlyWhenSomethingDrawnChanged()
    {
        Assert.False(CloseUpSceneController.TimeStepNeedsFrame(false, false, true, 3.000, 3.004, true, 1));   // inside the 0.005 step
        Assert.True(CloseUpSceneController.TimeStepNeedsFrame(false, false, true, 3.000, 3.006, true, 1));    // past it
        Assert.False(CloseUpSceneController.TimeStepNeedsFrame(false, false, true, 3.000, 3.006, true, 0.1)); // past it, inside the cap
        Assert.True(CloseUpSceneController.TimeStepNeedsFrame(false, false, true, 3.000, 3.000, false, 1));   // a new outcome
        Assert.False(CloseUpSceneController.TimeStepNeedsFrame(false, false, false, 3.000, 4.000, true, 1));  // nothing lensed
        Assert.True(CloseUpSceneController.TimeStepNeedsFrame(true, false, false, 3.000, 3.000, true, 0));    // a new state record
        Assert.True(CloseUpSceneController.TimeStepNeedsFrame(false, true, false, 3.000, 3.000, true, 0));    // a menu switch
    }

    // ---- Task 4b fix round 1: the controller's per-frame lensing decisions, through the ILensingPass seam ----

    /// <summary>A lensing pass that draws nothing and records each frame's step; bands can be made to fail.</summary>
    private sealed class FakeLensing : ILensingPass
    {
        public List<RayMapStep> Steps { get; } = new();
        public bool FailBands { get; set; }
        public bool FailTraces { get; set; }
        public int DirectFrames { get; private set; }
        public LensingUniforms Last { get; private set; }
        public bool CachesRays => true;
        public bool Render(Size viewportPx, RayMapStep step, in LensingUniforms u, int environmentCubeMap)
        {
            Steps.Add(step);
            Last = u;
            return !(FailBands && step.Work == RayMapWork.RefineStrip) && !(FailTraces && step.Work == RayMapWork.Trace);
        }
        public void RenderDirect(Size viewportPx, LensingLevel level, in LensingUniforms u, int environmentCubeMap)
        {
            DirectFrames++;
            Last = u;
        }
        public void Dispose() { }
    }

    // ---- Plan Task 9: the first second at the core (spec §3) ----

    [Fact]
    public void Controller_FirstSecond_IsUnlensed_Animates_AndKeepsTheCameraOutsideTheCore()   // R-P3-11's condition
    {
        var (main, _) = NewMain();
        main.Lab.SelectedPreset = PresetCatalog.Get("sn1987a");
        main.Timeline.ObserverTimeS = 0.05 * main.CurrentObserver.TimeDilation;
        var vm = new CloseUpViewModel(main);
        double wall = 10;
        var host = new FakeHost();
        var lensing = new FakeLensing();
        var c = new CloseUpSceneController(vm, () => host, wallClock: () => wall, gpuFactory: () => new CloseUpGpu(lensing));
        c.SetViewSize(new Size(900, 600));
        c.Start();
        host.FirstFrame("Intel | Intel(R) Iris(R) Xe Graphics | 4.1.0 - Build 30.0.101.1960");
        Assert.Equal(CloseUpEra.FirstSecond, vm.State.Era);
        Assert.False(vm.State.IsLensed);
        Assert.True(c.CoreMoves);
        Assert.True(c.IsAnimating);
        Assert.True(host.Animating);
        Assert.Equal(CloseUpSceneController.MinCameraSurfaceRadii * vm.State.CoreSphereRadiusKm, c.MinCameraDistanceKm, 9);
        Assert.StartsWith(Core.Compact.CoreCollapseHistory.FromSimulationsLabel, vm.StageLabel);
        Assert.NotEmpty(vm.StageKey);
        host.Tick();
        wall += 0.1;
        host.Tick();
        Assert.Equal(0.1 / CoreAnimation.InfallCycleS(vm.State.Core!), c.InfallCycles, 9);   // accumulated by wall time
        Assert.Empty(lensing.Steps);                                                              // nothing traced: drawn unlensed
        Assert.NotEmpty(c.RingLabels);                                                            // the distance rings still frame it
        main.Timeline.ObserverTimeS = SettledObserverTimeS;                                     // the neutron star: lensed, the core stops
        Assert.False(c.CoreMoves);
        Assert.Null(vm.StageLabel);
        c.Dispose();
    }

    [Fact]
    public void Controller_FallbackCooling_StopsAnimating_WhenNoShockIsDrawn()   // fix round 1: nothing moves at 1-3 s
    {
        var (main, _) = NewMain();
        main.Lab.SelectedPreset = PresetCatalog.Get("sn1987a");
        var fallback = TestOutcomes.Custom(Core.Outcomes.SupernovaType.IIP, 1e51, 10, 0.07, 3e13, TestOutcomes.BlackHole(3, 3.0), Fate.Supernova);
        var vm = new CloseUpViewModel(main);
        var c = new CloseUpSceneController(vm, () => new FakeHost());
        var scene = new CloseUpScene(fallback, main.CurrentObserver, 1);
        Assert.Equal(CloseUpEra.FirstSecond, scene.At(2).Era);
        Assert.True(double.IsNaN(scene.At(2).Core!.ShockRadiusKm));
        // The rule itself, on the view model's own outcome: a shock drawn at 0.05 s keeps the view moving; the neutron star at 100 s does not.
        main.Timeline.ObserverTimeS = 0.05 * main.CurrentObserver.TimeDilation;
        Assert.True(c.CoreMoves);
        Assert.False(CloseUpSceneController.CoreMovesIn(scene.At(2)));
        Assert.True(CloseUpSceneController.CoreMovesIn(scene.At(0.5)));
        c.Dispose();
    }

    [Fact]
    public void Controller_InsideTheStar_UsesTheHaze()   // ruling R-P3-21
    {
        var (main, _) = NewMain();
        main.Lab.SelectedPreset = PresetCatalog.Get("sn1987a");
        main.Timeline.ObserverTimeS = 2 * main.CurrentObserver.TimeDilation;   // the neutron star, before breakout
        var vm = new CloseUpViewModel(main);
        Assert.True(vm.State.InsideStar);
        Assert.True(vm.State.IsLensed);
        var c = new CloseUpSceneController(vm, () => new FakeHost());
        Assert.Equal(CloseUpScene.InsideStarHazeSrgb, c.HazeColor);   // no GL yet: the no-HDR colour
        Assert.Contains(vm.Notes, n => n.StartsWith("Inside the star until the shock breaks out"));
        main.Timeline.ObserverTimeS = SettledObserverTimeS * 1e6;   // long after breakout
        Assert.False(vm.State.InsideStar);
        Assert.DoesNotContain(vm.Notes, n => n.StartsWith("Inside the star"));
        c.Dispose();
    }

    [Fact]
    public void CloseUpViewModel_AtTheTimelinesStart_ShowsTheCoreBeforeCollapse()
    {
        var (main, _) = NewMain();
        main.Lab.SelectedPreset = PresetCatalog.Get("sn1987a");
        main.Timeline.ObserverTimeS = Core.Timeline.TimeAxis.MinS;
        var vm = new CloseUpViewModel(main);
        Assert.Equal(CloseUpEra.PreCollapse, vm.State.Era);
        Assert.Equal("Close-up: Iron core before collapse", vm.WindowTitle);
        Assert.Equal("Before collapse", vm.StageLabel);
        Assert.Equal(new[] { "Core radius" }, vm.Readouts.Select(r => r.Label));
        vm.Dispose();
    }

    // ---- Plan Task 6: the neutron star's moving parts join the on-demand drawing (ruling R-P3-11's condition) ----

    private const double CrabAgeS = 2.9568e10;

    private static (CloseUpSceneController C, FakeHost Host, FakeLensing Lensing, CloseUpViewModel Vm) DrawnCrab(double wall = 5)
    {
        var (c, host, lensing, vm) = Drawn("sn1054");
        vm.Main.Timeline.ObserverTimeS = CrabAgeS * vm.Main.CurrentObserver.TimeDilation;
        return (c, host, lensing, vm);
    }

    [Fact]
    public void Controller_APulsarsBeams_KeepTheViewDrawing_EvenWhilePaused()
    {
        var (c, host, _, vm) = DrawnCrab();
        Assert.False(vm.Main.Timeline.IsPlaying);
        Assert.True(vm.State.ShowBeams);
        for (int k = 0; k < 100; k++) host.Tick();   // the ray map refined long since: only the beams move now
        Assert.Equal(LensingLevel.Full, c.ShownLevel);
        Assert.True(c.IsAnimating, "the beams turn with the beam clock while the timeline is paused");
        Assert.True(host.Animating);
        c.SetVisible(false);
        Assert.False(host.Animating);                // hidden: nothing turns on screen
        c.Dispose();
    }

    [Fact]
    public void Controller_TheFieldLineAndLightCylinderSwitches_AskForAFrame()
    {
        var (c, host, _, vm) = DrawnCrab();
        int renders = host.Renders;
        vm.ShowFieldLines = !vm.ShowFieldLines;
        Assert.True(host.Renders > renders);
        renders = host.Renders;
        vm.ShowLightCylinder = !vm.ShowLightCylinder;
        Assert.True(host.Renders > renders);
        c.Dispose();
    }

    [Fact]
    public void Controller_TheSurfaceShading_IsHandedToTheLensingPass_AndTurnsWithTheBeams()
    {
        double wall = 1;
        var vm = OpenCloseUp("sn1054", out var main);
        main.Timeline.ObserverTimeS = CrabAgeS * main.CurrentObserver.TimeDilation;
        var host = new FakeHost();
        var lensing = new FakeLensing();
        var c = new CloseUpSceneController(vm, () => host, new BeamClock(() => wall), gpuFactory: () => new CloseUpGpu(lensing));
        c.Start();
        host.FirstFrame("Intel | Intel(R) Iris(R) Xe Graphics | 4.1.0 - Build 30.0.101.1960");
        host.Tick();
        var first = lensing.Last;
        Assert.True(first.SurfaceMarkings);
        Assert.Equal((float)vm.State.BeamTiltRad, first.SurfaceTiltRad);
        Assert.Equal((float)(c.BeamPhaseRad % (2 * Math.PI)), first.SurfacePhaseRad, 5);
        Assert.True(first.SurfaceColor.Length > 0);
        wall = 1.5;   // a quarter turn later (2 s a turn)
        host.Tick();
        Assert.Equal(first.SurfacePhaseRad + Math.PI / 2, lensing.Last.SurfacePhaseRad, 4);
        Assert.True(RayMapPlanner.SameRays(first, lensing.Last), "turning the surface retraces nothing");
        c.Dispose();
    }

    /// <summary>
    /// Fix round 1 (review item 1): no closed field line reaches the default camera (the director frames the star at 2.5 radii, then
    /// snaps to a ring, so the camera sits about 11 radii out): every loop stays inside 0.6 × that distance, for the Crab and for a
    /// magnetar, whose stronger field is drawn as more lines, not larger ones.
    /// </summary>
    [Fact]
    public void FieldLines_StayWellInsideTheDefaultCamera_ForThePulsarAndTheMagnetar()
    {
        var (c, _, _, vm) = DrawnCrab();
        c.Home();
        double camera = c.Camera.DistanceMpc;   // km (the scene unit)
        var s = vm.State;
        var crab = FieldLines.For(s.SurfaceRadiusKm, s.FieldG, s.LightCylinderKm);
        var magnetar = FieldLines.For(s.SurfaceRadiusKm, 1e15, Core.Compact.Magnetosphere.LightCylinderKm(2.5));
        foreach (var set in new[] { crab, magnetar })
            Assert.All(set.Closed, line => Assert.All(line, p => Assert.True(p.Length < 0.6 * camera, $"{p.Length:0} km vs camera {camera:0} km")));
        Assert.True(magnetar.Closed.Count > crab.Closed.Count, "the magnetar's field is drawn denser");
        c.Dispose();
    }

    [Fact]
    public void CloseUpViewModel_WindowTitle_NamesThePulsar()   // review item 2: the brief's title check, on the real window title
    {
        var cu = OpenCloseUp("sn1054", out var main);
        main.Timeline.ObserverTimeS = CrabAgeS * main.CurrentObserver.TimeDilation;
        Assert.StartsWith("Close-up: Neutron star (pulsar, 33", cu.WindowTitle);
        Assert.Equal("Shown 60× slower", cu.SpeedLabel);
    }

    [Fact]
    public void Controller_TheEarthLabel_StaysOnScreen_WhenTheArrowTipIsNot()   // review item 5
    {
        var (c, _, _, _) = DrawnCrab();
        c.SetViewSize(new Size(737, 705));
        for (int k = 0; k < 40; k++) c.Zoom(5);   // to the closest camera, 1.1 radii: the arrow's tip (4 radii out) is off the view
        var label = Assert.Single(c.ViewLabels, l => l.Text == CloseUpSceneController.ToEarthLabel);
        Assert.InRange(label.XDip, 0, 737 - 100);
        Assert.InRange(label.YDip, 0, 705);
        c.Dispose();
    }

    [Fact]
    public void Controller_LabelsTheEarthArrow_AndTheLightCylinderWhenItIsOnScreen()
    {
        var (c, _, _, vm) = DrawnCrab();
        c.SetViewSize(new Size(737, 705));
        Assert.Contains(c.ViewLabels, l => l.Text == CloseUpSceneController.ToEarthLabel);
        Assert.DoesNotContain(c.ViewLabels, l => l.Text == CloseUpSceneController.LightCylinderLabel);   // 1,600 km: far off the view
        c.FrameForSnapshot(1.15 * vm.State.LightCylinderKm, 25);
        var ring = Assert.Single(c.ViewLabels, l => l.Text == CloseUpSceneController.LightCylinderLabel);
        Assert.InRange(ring.XDip, 0, 737 - 100);   // room for the text
        vm.ShowLabels = false;
        Assert.Empty(c.ViewLabels);
        c.Dispose();
    }

    /// <summary>Spec C12: the surface is tone-mapped to read at about 0.9 of full brightness whatever the surroundings' exposure.</summary>
    [Theory, InlineData(0.15f), InlineData(0.2f), InlineData(1f)]
    public void SurfaceColour_IsToneMappedToRead(float exposure)
    {
        var srgb = BlackbodyColor.FromTemperature(1.8e6);
        var hdr = CloseUpSceneController.ToneMappedSurface(srgb, hdr: true, exposure);
        static double Aces(double x) => Math.Clamp(x * (2.51 * x + 0.03) / (x * (2.43 * x + 0.59) + 0.14), 0, 1);
        double shown = Math.Pow(Aces(Math.Max(hdr.X, Math.Max(hdr.Y, hdr.Z)) * exposure), 1 / 2.2);   // tonemap.frag
        Assert.InRange(shown, 0.85, 0.97);
        Assert.True(hdr.Z >= hdr.X, "blue-white stays blue-white");
        var plain = CloseUpSceneController.ToneMappedSurface(srgb, hdr: false, exposure);
        Assert.Equal(new OpenTK.Mathematics.Vector3(srgb.R, srgb.G, srgb.B), plain);   // the no-HDR path draws the colour as it is
    }

    [Fact]
    public void Controller_ABandThatAlwaysFails_IsGivenUp_AndTheViewRests()   // Task 4b review carry-over
    {
        var (c, host, lensing, vm) = Drawn();
        c.SetLevel(LensingLevel.Quarter);
        lensing.FailBands = true;
        for (int k = 0; k < 60; k++) host.Tick();
        Assert.Equal(CloseUpSceneController.MaxConsecutiveFailures, c.RefineBandsFailed);
        Assert.False(c.IsAnimating, "no retracing forever");
        Assert.False(host.Animating);
        Assert.Equal(LensingLevel.Quarter, c.ShownLevel);
        Assert.Contains(vm.Notes, n => n.Contains("could not be refined", StringComparison.Ordinal));
        c.Dispose();
    }

    [Fact]
    public void Controller_AWholeMapThatAlwaysFails_FallsBackToDirectTracing()   // Task 4b review carry-over
    {
        var (c, host, lensing, vm) = Drawn();
        lensing.FailTraces = true;
        for (int k = 0; k < 10; k++) { c.Orbit(1); host.Tick(); }
        Assert.Equal(CloseUpSceneController.MaxConsecutiveFailures, c.MapTracesFailed);
        Assert.True(lensing.DirectFrames > 0, "the view still draws, tracing colour directly");
        Assert.Contains(vm.Notes, n => n.Contains("traced afresh every frame", StringComparison.Ordinal));   // fix round 1: said, not silent
        c.Dispose();
    }

    private static (CloseUpSceneController C, FakeHost Host, FakeLensing Lensing, CloseUpViewModel Vm) Drawn(string preset = "n6946bh1")
    {
        var vm = OpenCloseUp(preset, out _);
        var host = new FakeHost();
        var lensing = new FakeLensing();
        var c = new CloseUpSceneController(vm, () => host, gpuFactory: () => new CloseUpGpu(lensing));
        c.Start();
        host.FirstFrame("Intel | Intel(R) Iris(R) Xe Graphics | 4.1.0 - Build 30.0.101.1960");
        Assert.True(c.IsReady);
        return (c, host, lensing, vm);
    }

    /// <summary>
    /// R-P3-10 at the controller: after a move traced at 1/4 (Auto's governed level), the still frames build the full map in bands, the
    /// view keeps drawing (IsAnimating, the host's Animating) until the last band, then rests, and the note follows the level shown.
    /// </summary>
    [Fact]
    public void Controller_AfterAMoveAt1_4_RefinesToFullInBands_ThenRests()
    {
        var (c, host, lensing, vm) = Drawn();
        c.SetLevel(LensingLevel.Quarter);   // Auto: the governor's moving level
        c.Orbit(1);
        host.Tick();
        Assert.Equal(RayMapWork.Trace, lensing.Steps[^1].Work);
        Assert.Equal(LensingLevel.Quarter, lensing.Steps[^1].Level);
        Assert.Equal(LensingLevel.Quarter, c.ShownLevel);
        Assert.Equal("Light bending: quarter resolution (automatic)", vm.LightBendingNote);
        Assert.True(c.IsAnimating);
        Assert.True(host.Animating, "a refinement owed keeps the host drawing");
        int bands = 0;
        for (int k = 0; k < 100 && c.ShownLevel != LensingLevel.Full; k++)
        {
            host.Tick();
            Assert.Equal(RayMapWork.RefineStrip, lensing.Steps[^1].Work);
            Assert.Equal(LensingLevel.Full, lensing.Steps[^1].Level);
            bands++;
            if (c.ShownLevel != LensingLevel.Full) Assert.True(host.Animating);
        }
        Assert.Equal(LensingLevel.Full, c.ShownLevel);
        Assert.True(bands > 1);
        Assert.Equal(bands, c.RefineBands);
        Assert.Equal(0, c.RefineBandsFailed);
        Assert.False(c.IsAnimating);
        Assert.False(host.Animating, "refined: the view rests");
        Assert.Equal("Light bending: full resolution (automatic)", vm.LightBendingNote);
        host.Tick();
        Assert.Equal(RayMapWork.None, lensing.Steps[^1].Work);   // and only samples from then on
        c.Dispose();
    }

    [Fact]
    public void Controller_AFailingBand_IsCounted_AndTheMapIsTracedAgain()
    {
        var (c, host, lensing, _) = Drawn();
        c.SetLevel(LensingLevel.Quarter);
        lensing.FailBands = true;
        host.Tick();                     // the whole map at 1/4
        int traces = c.MapTraces;
        host.Tick();                     // a band: fails
        Assert.Equal(1, c.RefineBandsFailed);
        Assert.Equal(0, c.RefineBands);
        host.Tick();                     // the map was forgotten: traced whole again
        Assert.Equal(RayMapWork.Trace, lensing.Steps[^1].Work);
        Assert.Equal(traces + 1, c.MapTraces);
        c.Dispose();
    }

    [Fact]
    public void Controller_AFixedLevel_IsTracedOnce_AndNeverRefined()
    {
        var (c, host, lensing, vm) = Drawn();
        vm.LensingDetail = LensingDetail.Half;
        for (int k = 0; k < 5; k++) host.Tick();
        Assert.Equal(1, lensing.Steps.Count(s => s.Work == RayMapWork.Trace));
        Assert.Equal(LensingLevel.Half, lensing.Steps.First(s => s.Work == RayMapWork.Trace).Level);
        Assert.DoesNotContain(lensing.Steps, s => s.Work == RayMapWork.RefineStrip);
        Assert.False(c.IsAnimating);
        Assert.Equal("Light bending: half resolution (your setting)", vm.LightBendingNote);
        c.Dispose();
    }

    [Fact]
    public void LensingFrameTag_RoundTrips()
    {
        var band = new RayMapStep(RayMapWork.RefineStrip, LensingLevel.Full, 737, 705, 96, 88, false);
        Assert.Equal((737 * 88, false, true), LensingFrameTag.Decode(LensingFrameTag.Encode(band, environmentCaptured: true)));
        var whole = new RayMapStep(RayMapWork.Trace, LensingLevel.Quarter, 185, 177, 0, 177, true);
        Assert.Equal((185 * 177, true, false), LensingFrameTag.Decode(LensingFrameTag.Encode(whole, false)));
        Assert.Equal((0, false, true), LensingFrameTag.Decode(LensingFrameTag.Encode(default, true)));
        Assert.Equal((519_585, true, false), LensingFrameTag.Decode(LensingFrameTag.EncodeDirect(519_585, false)));
    }

    /// <summary>
    /// Fix round 1's root cause: GLWpfControl 4.3.6 only renders continuously after its control has been hidden and shown again, so
    /// GlHost drives continuous frames itself. Animating, or a requested frame still owed, keeps the host's own composition-frame
    /// handler on; nothing owed turns it off. (Built on an STA thread with an unstarted control: no GL context is made.)
    /// </summary>
    [Fact]
    public void GlHost_DrivesContinuousFramesItself()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                // Fix round 1: the host starts from its control's visibility, so the control is in a shown (off-screen) window.
                var control = new OpenTK.Wpf.GLWpfControl();
                var window = new Window { Content = control, Left = -20000, Top = -20000, Width = 200, Height = 150, ShowActivated = false, ShowInTaskbar = false };
                window.Show();
                var host = new GlHost(control);
                Assert.False(host.IsTicking);
                host.Animating = true;
                Assert.True(host.IsTicking);
                host.Animating = false;
                Assert.False(host.IsTicking);
                host.RequestRender();   // two frames owed: ticks until they are drawn
                Assert.True(host.IsTicking);
                Assert.False(host.Shutdown(() => throw new InvalidOperationException("no context, nothing to release")));
                Assert.False(host.IsTicking);
                window.Close();
                var unshown = new GlHost(new OpenTK.Wpf.GLWpfControl());   // a control never shown is not visible: nothing ticks
                unshown.Animating = true;
                Assert.False(unshown.IsTicking);
            }
            catch (Exception e) { error = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(error);
    }

    /// <summary>
    /// Task 4b review carry-over (b): after Shutdown nothing can subscribe the host to CompositionTarget.Rendering again (a static event:
    /// a late Animating or RequestRender would otherwise keep the dead host, its control and its window alive).
    /// </summary>
    [Fact]
    public void GlHost_AfterShutdown_NeverTicksAgain()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var host = new GlHost(new OpenTK.Wpf.GLWpfControl());
                host.Shutdown(() => { });
                host.Animating = true;
                Assert.False(host.IsTicking);
                host.RequestRender();
                Assert.False(host.IsTicking);
            }
            catch (Exception e) { error = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(error);
    }

    /// <summary>
    /// Task 4b review carry-over (a): while its control is not visible (the window hidden or minimized) the host owes no frames and does
    /// not tick, even when animating; shown again, it ticks for the animation and owes one fresh frame. (An off-screen window on an STA
    /// thread; no GL context is made.)
    /// </summary>
    [Fact]
    public void GlHost_WhileItsControlIsHidden_OwesNoFramesAndDoesNotTick()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var control = new OpenTK.Wpf.GLWpfControl();
                var window = new Window { Content = control, Left = -20000, Top = -20000, Width = 200, Height = 150, ShowActivated = false, ShowInTaskbar = false };
                window.Show();
                var host = new GlHost(control);
                host.RequestRender();
                Assert.True(host.IsTicking);
                window.Hide();
                Assert.False(control.IsVisible);
                Assert.False(host.IsTicking, "hidden: the owed frames are dropped");
                host.Animating = true;
                Assert.False(host.IsTicking, "hidden: even an animation does not tick");
                host.RequestRender();
                Assert.False(host.IsTicking);
                window.Show();
                Assert.True(host.IsTicking, "shown again: the animation ticks");
                host.Animating = false;
                Assert.True(host.IsTicking, "and one fresh frame is owed");
                host.Shutdown(() => { });
                window.Close();
            }
            catch (Exception e) { error = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(error);
    }

    /// <summary>
    /// Plan Task 11: while the collapsar's disk shows, the lensing pass gets its annulus (the ISCO of the drawn spin to the outer edge,
    /// in GM/c²) and a brightness, and the escape sphere encloses the disk; the disk's fade changes only the brightness (no retrace). After
    /// the disk ends there is no annulus.
    /// </summary>
    [Fact]
    public void CloseUpController_PassesTheDiskToTheLensing_AndItsFadeNeverRetraces()
    {
        var cu = OpenCloseUp("sn1998bw", out var main);
        double tbh = main.CurrentOutcome.Remnant.FormationTimeS!.Value;
        main.Timeline.ObserverTimeS = tbh * main.CurrentObserver.TimeDilation + 5 * main.CurrentObserver.TimeDilation;
        var c = new CloseUpSceneController(cu, () => new FakeHost());
        var view = new Size(900, 640);
        c.SetViewSize(view);
        var s = cu.State;
        Assert.True(s.HasDisk);
        var u = c.RaysFor(view);
        Assert.Equal((float)s.DiskInnerGm, u.DiskInner);
        Assert.Equal((float)s.DiskOuterGm, u.DiskOuter);
        Assert.True(u.DiskLevel > 0);
        Assert.True(u.EscapeRadius >= 1.05f * u.DiskOuter);
        main.Timeline.ObserverTimeS = (tbh + 20) * main.CurrentObserver.TimeDilation;
        var after = c.RaysFor(view);
        Assert.Equal(0, after.DiskOuter);
        Assert.Equal(0, after.DiskLevel);
        c.Dispose();
    }

    /// <summary>
    /// Task 14's launch found the tour's outline never lit: the Border's local BorderBrush beat its style's trigger. Now the window's
    /// outline frame takes the accent brush at a Close-up stop and is transparent elsewhere (constructed, not shown: no GL context).
    /// </summary>
    [Fact]
    public void CloseUpWindow_TourOutline_LightsAtACloseUpStop()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                InstrumentsWindowTests.EnsureApplication();
                var cu = OpenCloseUp("n6946bh1", out var main);
                var w = new CloseUpWindow { DataContext = cu };
                Pump();
                var outline = (System.Windows.Controls.Border)w.FindName("TourOutline");
                var accent = (System.Windows.Media.SolidColorBrush)Application.Current.FindResource("AccentBrush");
                Assert.Equal(System.Windows.Media.Colors.Transparent, ((System.Windows.Media.SolidColorBrush)outline.BorderBrush).Color);
                var director = main.Tour.Director;
                director.Start();
                int stop = director.Stops.ToList().FindIndex(s => s.CloseUpOnly);
                director.GoTo(stop);
                Pump();
                Assert.True(main.Tour.HighlightCloseUp);
                Assert.Equal(accent.Color, ((System.Windows.Media.SolidColorBrush)outline.BorderBrush).Color);
                director.GoTo(0);   // the first stop (1 ms) is not a Close-up stop
                Pump();
                Assert.False(main.Tour.HighlightCloseUp);
                Assert.Equal(System.Windows.Media.Colors.Transparent, ((System.Windows.Media.SolidColorBrush)outline.BorderBrush).Color);
                director.Exit();
                w.Close();
                cu.Dispose();
            }
            catch (Exception e)
            {
                error = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(error);
    }

    /// <summary>
    /// Task 14's launch: at the default 900x640 window the drip label on the star's right ran under the cutaway key. Where it would, it
    /// now goes on the star's left, ending before the star; at 1366x768 it stays on the right, clear of the key. The anchors are the
    /// launch's: the star's centre at the view's centre, its edge about 50 DIP out (the label sits at 1.12 radii).
    /// </summary>
    [Theory]
    [InlineData(584, 465, true)]     // the GL view of the default 900x640 window (Task 14's capture)
    [InlineData(1050, 593, false)]   // the GL view of a 1366x768 window
    public void Controller_DripLabel_KeepsClearOfTheCutawayKey(double width, double height, bool onTheLeft)
    {
        const string text = "Neutron drip: 4.3 × 10¹¹ g/cm³";
        double cx = width / 2, cy = height / 2, edge = 50;
        var drip = CloseUpSceneController.DripLabelPlacement(text, new Point(cx + edge, cy), new Point(cx - edge, cy), width)!;
        double right = drip.XDip + 6 + 8 + CloseUpSceneController.LabelCharDip * drip.Text.Length;
        if (onTheLeft)
        {
            Assert.Equal(text + " ►", drip.Text);
            Assert.True(right < cx - edge, $"the label ends at {right:0} DIP, the star's edge is at {cx - edge:0}");
            Assert.True(drip.XDip >= 0);
        }
        else
        {
            Assert.Equal("◄ " + text, drip.Text);
            Assert.True(drip.XDip > cx);
            Assert.False(CloseUpSceneController.DripLabelUnderKey(new Point(drip.XDip, drip.YDip), drip.Text, width));
        }
        // Either way the label's box keeps out of the key's top-right box.
        Assert.True(right <= width - CloseUpSceneController.CutawayKeyWidthDip || drip.YDip - 10 >= CloseUpSceneController.CutawayKeyBottomDip);
        // With no room on the left either, nothing is drawn rather than a label under the key.
        Assert.Equal(onTheLeft, CloseUpSceneController.DripLabelPlacement(text, new Point(cx + edge, cy), null, width) is null);
    }

    // ---- Final fix wave ----

    /// <summary>Item 4: the Close-up draws only while shown and neither it nor its owner is minimized.</summary>
    [Fact]
    public void Window_IsHidden_WhenItsOwnerIsMinimized()
    {
        Assert.True(CloseUpWindow.IsEffectivelyVisible(true, WindowState.Normal, WindowState.Normal));
        Assert.True(CloseUpWindow.IsEffectivelyVisible(true, WindowState.Normal, null));
        Assert.False(CloseUpWindow.IsEffectivelyVisible(true, WindowState.Normal, WindowState.Minimized));
        Assert.False(CloseUpWindow.IsEffectivelyVisible(true, WindowState.Minimized, WindowState.Normal));
        Assert.False(CloseUpWindow.IsEffectivelyVisible(false, WindowState.Normal, WindowState.Normal));
    }

    /// <summary>Item 5: before 1 minute the Surface readout says the temperature is held; after, it does not.</summary>
    [Fact]
    public void SurfaceReadout_SaysItIsHeldBeforeOneMinute()
    {
        var p = PresetCatalog.Get("sn1054");
        var o = TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
        string Tip(double t) => new CloseUpScene(o, ObserverSetup.Default, 1).At(t).Readouts.First(r => r.Label == "Surface").ToolTip!;
        Assert.Contains(CloseUpScene.SurfaceHeldTip, Tip(20));
        Assert.DoesNotContain(CloseUpScene.SurfaceHeldTip, Tip(120));
    }

    /// <summary>Item 6: a Re-roll keeps the zoom the user chose; a new outcome reframes.</summary>
    [Fact]
    public void Reroll_KeepsTheZoom_ButANewOutcomeReframes()
    {
        var vm = OpenCloseUp("sn1054", out var main);
        var c = new CloseUpSceneController(vm, () => new FakeHost());
        c.Zoom(3);
        double zoomed = c.Camera.DistanceMpc;
        Assert.False(c.IsAutoFraming);
        main.RerollCommand.Execute(null);
        Assert.False(c.IsAutoFraming);
        Assert.Equal(zoomed, c.Camera.DistanceMpc, 12);
        main.Lab.SelectedPreset = PresetCatalog.Get("n6946bh1");
        Assert.True(c.IsAutoFraming);
        c.Dispose();
    }

    /// <summary>Item 7: field lines and the light cylinder wait for the magnetosphere, as the beams do.</summary>
    [Fact]
    public void FieldLinesAndLightCylinder_WaitForTheMagnetosphere()
    {
        var vm = OpenCloseUp("sn1054", out var main);
        var c = new CloseUpSceneController(vm, () => new FakeHost());
        vm.ShowFieldLines = true;
        vm.ShowLightCylinder = true;
        main.Timeline.ObserverTimeS = 1;
        Assert.False(c.ShowsBeams);
        Assert.False(c.DrawsFieldLines);
        Assert.False(c.DrawsLightCylinder);
        Assert.Contains("field lines", CloseUpScene.BeamsNotYetNote);
        main.Timeline.ObserverTimeS = 1e6;
        Assert.True(c.DrawsFieldLines);
        Assert.True(c.DrawsLightCylinder);
        c.Dispose();
    }
}
