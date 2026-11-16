using System.IO;
using System.Windows;
using System.Windows.Threading;
using SupernovaLab.App.Controllers;
using SupernovaLab.App.Services;
using SupernovaLab.App.ViewModels;
using SupernovaLab.App.Windows;
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

    private static CloseUpViewModel OpenCloseUp(string preset, out MainViewModel main)
    {
        (main, _) = NewMain();
        main.Lab.SelectedPreset = PresetCatalog.Get(preset);
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
        Assert.Equal(new[] { "Mass", "Horizon diameter", "Shadow diameter" }, cu.Readouts.Select(r => r.Label));
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
}
