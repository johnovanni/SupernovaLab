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
}
