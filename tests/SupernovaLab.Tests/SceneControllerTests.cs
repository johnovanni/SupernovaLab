using System.ComponentModel;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using SupernovaLab.App.Controllers;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>
/// Task 16: the 3D view's controller. Spec §9: when OpenGL fails, the view names the requirement and the rest of the app
/// keeps working. Task 15's lesson: a controller must stop listening to the long-lived view model when it goes away.
/// </summary>
public class SceneControllerTests
{
    private sealed class FakeHost : IRenderHost
    {
        public Exception? StartFailure { get; init; }
        public Exception? ShutdownFailure { get; init; }
        public int Renders { get; private set; }
        public bool ShutDown { get; private set; }
        public bool Animating { get; set; }
        public event Action<string>? ContextReady;
        public event Action<TimeSpan, Size>? Draw;
        public void RaiseDraw(Size px) => Draw?.Invoke(TimeSpan.FromSeconds(1.0 / 60), px);
        public event Action<Exception>? FrameFailed;
        public void Start() { if (StartFailure is not null) throw StartFailure; }
        public void RequestRender() => Renders++;
        public bool Shutdown(Action release)
        {
            ShutDown = true;
            if (ShutdownFailure is not null) throw ShutdownFailure;
            return false;
        }
        public void Fail(Exception e) => FrameFailed?.Invoke(e);

        /// <summary>A first frame as GlHost runs it: ContextReady inside the frame's try, a throw becomes FrameFailed.</summary>
        public void FirstFrame(string glInfo)
        {
            try { ContextReady?.Invoke(glInfo); }
            catch (Exception e) { FrameFailed?.Invoke(e); }
        }
    }

    [Theory]
    [InlineData("4.6.0 NVIDIA 551.86", true)]
    [InlineData("4.1 Metal - 88", true)]
    [InlineData("4.10", true)]
    [InlineData("5.0", true)]
    [InlineData("4.0.0 - Build 10.18", false)]
    [InlineData("3.3.0 Mesa 23.0", false)]
    [InlineData("", false)]
    [InlineData("OpenGL ES 3.2", false)]
    public void IsSupported_NeedsOpenGl41(string version, bool supported)
        => Assert.Equal(supported, SceneController.IsSupported(version));

    [Fact]
    public void ContextBelow41_NamesTheRequirement_AndTheVersionFound()
    {
        var host = new FakeHost();
        var scene = new SceneController(NewViewModel(), () => host);
        scene.Start();
        host.FirstFrame("Mesa | llvmpipe | 3.3 (Core Profile) Mesa 23.0");
        Assert.True(scene.IsFailed);
        Assert.Contains("OpenGL 4.1", scene.FailureMessage);
        Assert.Contains("3.3", scene.FailureMessage);
        Assert.False(scene.IsReady);
    }

    [Fact]
    public void FactoryThatThrows_AtStart_LeavesTheTimelineDrivingTheOverlay()
    {
        var vm = NewViewModel();
        var scene = new SceneController(vm, () => throw new InvalidOperationException("WGL: no 4.1 core context"));
        scene.Start();
        Assert.True(scene.IsFailed);
        scene.Zoom(2);
        scene.Home();
        vm.Timeline.ObserverTimeS = 100 * PhysicalConstants.Year;
        Assert.Contains("100 years", scene.TimeText);
        scene.Dispose();
    }

    [Fact]
    public void SceneModelError_IsShownInTheView_AndDoesNotEndTheApp()
    {
        // Review M1 (spec §9): the scene's models run inside a catch; a failure shows in the view's notes.
        var vm = NewViewModel();
        var scene = new SceneController(vm, () => new FakeHost(),
            (_, _) => throw new InvalidOperationException("model broke"));
        Assert.Contains("3D view: model error", scene.NotesText);
        Assert.Contains("model broke", scene.NotesText);
        vm.Lab.SelectedPreset = PresetCatalog.Get("sn1987a");   // a new outcome rebuilds the scene: no throw
        vm.Timeline.ObserverTimeS = 10 * PhysicalConstants.Day;  // a new time restates it: no throw
        Assert.Contains("3D view: model error", scene.NotesText);
        scene.Dispose();
    }

    [Fact]
    public void NewOutcome_RestoresAutoFraming()
    {
        // Review M5: a zoom frozen on one outcome's scale would hide the next one.
        var vm = NewViewModel();
        var scene = new SceneController(vm, () => new FakeHost());
        scene.Start();
        scene.Zoom(3);
        Assert.False(scene.IsAutoFraming);
        vm.Lab.SelectedPreset = PresetCatalog.Get("n6946bh1");
        Assert.True(scene.IsAutoFraming);
        Assert.Equal(SceneController.AutoDistance, scene.Camera.DistanceMpc, 12);
    }

    private static MainViewModel NewViewModel() => new(TestOutcomes.Engine, new FakeWindowService());

    [Fact]
    public void HostThatCannotStart_NamesTheOpenGlRequirement_AndTheLabKeepsWorking()
    {
        var vm = NewViewModel();
        var scene = new SceneController(vm, () => new FakeHost { StartFailure = new InvalidOperationException("no pixel format") });
        scene.Start();

        Assert.True(scene.IsFailed);
        Assert.Contains("OpenGL 4.1", scene.FailureMessage);
        Assert.Contains("no pixel format", scene.FailureMessage);

        // The lab, the timeline and the overlay still work.
        vm.Lab.SelectedPreset = PresetCatalog.Get("sn1987a");
        vm.Timeline.ObserverTimeS = PhysicalConstants.Day;
        Assert.Contains("day", scene.TimeText);
        Assert.False(string.IsNullOrEmpty(scene.RadiusText));
    }

    [Fact]
    public void HostFactoryThatThrows_IsAFailureToo()
    {
        var scene = new SceneController(NewViewModel(), () => throw new DllNotFoundException("opengl32"));
        scene.Start();
        Assert.True(scene.IsFailed);
        Assert.Contains("OpenGL 4.1", scene.FailureMessage);
    }

    [Fact]
    public void FrameThatFails_StopsTheViewWithTheRequirement()
    {
        var host = new FakeHost();
        var scene = new SceneController(NewViewModel(), () => host);
        scene.Start();
        Assert.False(scene.IsFailed);
        host.Fail(new InvalidOperationException("OpenGL error InvalidOperation."));
        Assert.True(scene.IsFailed);
        Assert.Contains("OpenGL 4.1", scene.FailureMessage);
    }

    [Fact]
    public void TimeAndOutcomeChanges_RedrawAndUpdateTheOverlay()
    {
        var vm = NewViewModel();
        var host = new FakeHost();
        var scene = new SceneController(vm, () => host);
        scene.Start();
        int before = host.Renders;
        vm.Timeline.ObserverTimeS = 100 * PhysicalConstants.Year;
        Assert.True(host.Renders > before);
        Assert.Contains("100 years", scene.TimeText);
        Assert.Contains("pc", scene.RadiusText);

        before = host.Renders;
        vm.Lab.SelectedPreset = PresetCatalog.Get("sn1998bw");
        Assert.True(host.Renders > before);
    }

    [Fact]
    public void Dispose_StopsListeningToTheViewModel()
    {
        var vm = NewViewModel();
        var host = new FakeHost();
        var scene = new SceneController(vm, () => host);
        scene.Start();
        Assert.Equal(1, OutcomeListeners(vm, scene));
        Assert.Equal(1, TimelineListeners(vm, scene));

        scene.Dispose();

        Assert.Equal(0, OutcomeListeners(vm, scene));
        Assert.Equal(0, TimelineListeners(vm, scene));
        Assert.True(host.ShutDown);
        int renders = host.Renders;
        vm.Timeline.ObserverTimeS = PhysicalConstants.Day;
        Assert.Equal(renders, host.Renders);
    }

    [Fact]
    public void GlInfo_IsKeptSoTheGpuCanBeLogged()
    {
        var host = new FakeHost();
        var scene = new SceneController(NewViewModel(), () => host);
        Assert.Equal("", scene.GlInfo);
        scene.Start();
        host.FirstFrame("Test Vendor | Test GPU | 3.3.0 Test");   // below 4.1: fails, but the info is recorded first
        Assert.Equal("Test Vendor | Test GPU | 3.3.0 Test", scene.GlInfo);
    }

    [Fact]
    public void Dispose_SurvivesAGlErrorWhileReleasing()
    {
        // Task 16 re-review N2: Dispose runs in the window's Closing; an exception there would stop the window closing.
        var vm = NewViewModel();
        var host = new FakeHost { ShutdownFailure = new InvalidOperationException("GL_INVALID_OPERATION") };
        var scene = new SceneController(vm, () => host);
        scene.Start();

        scene.Dispose();

        Assert.True(host.ShutDown);
        Assert.False(scene.GlReleased);
        Assert.Equal(0, OutcomeListeners(vm, scene));
        scene.Dispose();   // idempotent
    }

    // ------------------------------------------------------------------ Task 3: rings and the director camera

    private static readonly Size View = new(800, 600);

    /// <summary>A controller whose wall clock only moves when a frame is drawn (60 fps), so the ease is deterministic.</summary>
    private sealed class Rig
    {
        public readonly MainViewModel Vm = NewViewModel();
        public readonly FakeHost Host = new();
        public readonly SceneController Scene;
        private double _now;

        public Rig()
        {
            Scene = new SceneController(Vm, () => Host, wallClock: () => _now);
            Scene.Start();
            Scene.SetViewSize(View);
        }

        public void Frame(int count = 1)
        {
            for (int i = 0; i < count; i++) { _now += 1.0 / 60; Host.RaiseDraw(View); }
        }

        public void Seek(double observerS) => Vm.Timeline.ObserverTimeS = observerS;

        /// <summary>Plays forward by timeline ticks (about 0.0043 decades each) until the director starts a step.</summary>
        public bool PlayUntilStep(double fromS, double toS)
        {
            Seek(fromS);
            double h = Scene.Director.HalfHeightCm;
            Vm.Timeline.IsPlaying = true;
            var dt = TimeSpan.FromSeconds(Math.Log10(1.01) / Vm.Timeline.DecadesPerSecond);
            while (Vm.Timeline.ObserverTimeS < toS)
            {
                Vm.Timeline.Tick(dt);
                Frame();
                if (Scene.Director.HalfHeightCm != h) return true;
            }
            return false;
        }
    }

    [Fact]
    public void Playing_StepsTheDirector_AndTheLabelsFollow()
    {
        var rig = new Rig();
        var scene = rig.Scene;
        rig.Seek(PhysicalConstants.Day);
        rig.Frame();
        double h0 = scene.Director.HalfHeightCm;
        Assert.True(h0 > 0);
        Assert.NotEmpty(scene.RingLabels);

        // At rest the outermost ring sits on the 90% line: 10% of the half-height below the top of a 600 px view.
        var outer = scene.RingLabels[0];
        Assert.Equal(DistanceRings.Rungs(h0)[0].Label, outer.Text);
        Assert.Equal(400, outer.XDip, 6);
        Assert.Equal(300 * (1 - CameraDirector.RingLine), outer.YDip, 6);
        Assert.Equal(1.0, outer.Opacity, 6);

        // While the debris grow inside the outer ring the camera holds still.
        rig.Seek(1.2 * PhysicalConstants.Day);
        rig.Frame();
        Assert.Equal(h0, scene.Director.HalfHeightCm);

        // Later the debris reach the outer ring: the camera steps back, easing, and the labels follow.
        Assert.True(rig.PlayUntilStep(1.2 * PhysicalConstants.Day, 1000 * PhysicalConstants.Day));
        Assert.True(scene.Director.IsEasing);
        Assert.True(rig.Host.Animating);
        rig.Frame(30);
        Assert.False(scene.Director.IsEasing);
        Assert.True(scene.Director.HalfHeightCm > 3 * h0);
        Assert.Equal(DistanceRings.Rungs(scene.Director.HalfHeightCm)[0].Label, scene.RingLabels[0].Text);
        Assert.Equal(300 * (1 - CameraDirector.RingLine), scene.RingLabels[0].YDip, 6);
        Assert.Equal(scene.Director.HalfHeightCm, scene.UnitCm);
    }

    [Fact]
    public void NewOutcome_DuringAnEase_ReframesAtOnce()
    {
        var rig = new Rig();
        rig.Seek(PhysicalConstants.Day);
        rig.Frame();
        Assert.True(rig.PlayUntilStep(PhysicalConstants.Day, 1000 * PhysicalConstants.Day));
        Assert.True(rig.Scene.Director.IsEasing);

        rig.Vm.Lab.SelectedPreset = PresetCatalog.Get("sn1987a");   // Review Focus 4: a jump, not a slow ease

        var expected = new CameraDirector();
        expected.Jump(rig.Scene.State.FrameRadiusCm);
        Assert.False(rig.Scene.Director.IsEasing);
        Assert.Equal(expected.HalfHeightCm, rig.Scene.Director.HalfHeightCm);
        Assert.Equal(expected.OuterRingCm, rig.Scene.Director.OuterRingCm);
    }

    [Fact]
    public void TimelineClick_IsAJump_NotAnEase()
    {
        var rig = new Rig();
        rig.Seek(PhysicalConstants.Day);
        rig.Frame();

        rig.Seek(100 * PhysicalConstants.Day);   // two decades in one click
        var expected = new CameraDirector();
        expected.Jump(rig.Scene.State.FrameRadiusCm);
        Assert.False(rig.Scene.Director.IsEasing);
        Assert.Equal(expected.HalfHeightCm, rig.Scene.Director.HalfHeightCm);
        rig.Frame();
        Assert.Equal(expected.HalfHeightCm, rig.Scene.Director.HalfHeightCm);
        Assert.False(rig.Scene.Director.IsEasing);

        // A marker jump is a jump too, even from a nearby time.
        var marker = rig.Vm.Timeline.Markers.First(m => m.ObserverTimeS > 1000 * PhysicalConstants.Day);
        rig.Vm.Timeline.JumpToMarkerCommand.Execute(marker);
        Assert.False(rig.Scene.Director.IsEasing);
    }

    [Fact]
    public void FastPlayback_WithALongFrame_IsStillAnEase()
    {
        // Review: at 3 decades/s a 30 ms frame steps 0.09 decades; playback must ease, never snap.
        var rig = new Rig();
        rig.Vm.Timeline.DecadesPerSecond = TimelineViewModel.MaxDecadesPerSecond;
        rig.Seek(PhysicalConstants.Day);
        rig.Frame();
        rig.Vm.Timeline.IsPlaying = true;
        double h = rig.Scene.Director.HalfHeightCm;
        bool stepped = false;
        for (int i = 0; i < 400 && !stepped; i++)
        {
            rig.Vm.Timeline.Tick(TimeSpan.FromMilliseconds(30));
            rig.Frame();
            stepped = rig.Scene.Director.HalfHeightCm != h || rig.Scene.Director.IsEasing;
        }
        Assert.True(stepped);
        // Eased, not snapped: one frame in, the view is partway to its new half-height.
        Assert.True(rig.Scene.Director.IsEasing);
        Assert.True(rig.Scene.Director.HalfHeightCm < rig.Scene.Director.OuterRingCm / CameraDirector.RingLine);
        Assert.True(rig.Scene.Director.HalfHeightCm > h);
    }

    [Fact]
    public void Zoom_MidEase_StopsTheAnimationLoop()
    {
        var rig = new Rig();
        rig.Seek(PhysicalConstants.Day);
        rig.Frame();
        Assert.True(rig.PlayUntilStep(PhysicalConstants.Day, 1000 * PhysicalConstants.Day));
        Assert.True(rig.Scene.Director.IsEasing);
        Assert.True(rig.Host.Animating);
        Assert.NotEqual(SceneMarker.Pulsar, rig.Scene.State.Marker);

        rig.Scene.Zoom(1);
        rig.Frame();
        Assert.False(rig.Host.Animating);

        rig.Scene.Home();
        Assert.False(rig.Scene.Director.IsEasing);
        Assert.False(rig.Host.Animating);
    }

    [Fact]
    public void Zoom_FreezesTheScale_HomeHandsBackToTheDirector()
    {
        var rig = new Rig();
        var scene = rig.Scene;
        rig.Seek(PhysicalConstants.Day);
        rig.Frame();
        double h0 = scene.Director.HalfHeightCm;

        scene.Zoom(2);
        Assert.False(scene.IsAutoFraming);
        Assert.Equal(h0, scene.UnitCm);

        // The debris outgrow the ring, but the scale stays frozen.
        rig.Seek(30 * PhysicalConstants.Day);
        rig.Frame(30);
        Assert.Equal(h0, scene.UnitCm);
        Assert.Equal(h0, scene.Director.HalfHeightCm);

        scene.Home();
        var expected = new CameraDirector();
        expected.Jump(scene.State.FrameRadiusCm);
        Assert.True(scene.IsAutoFraming);
        Assert.Equal(expected.HalfHeightCm, scene.UnitCm);
        Assert.Equal(expected.HalfHeightCm, scene.Director.HalfHeightCm);
        Assert.Equal(SceneController.AutoDistance, scene.Camera.DistanceMpc, 12);
        Assert.Equal(1 / Math.Tan(22.5 * Math.PI / 180), SceneController.AutoDistance, 12);
    }

    [Fact]
    public void RedshiftedObserver_UsesSourceTimeForTheRadius()
    {
        // Review Focus 3: observer time ÷ (1 + z) is the source time the radius is taken at.
        var near = new Rig();
        near.Seek(PhysicalConstants.Day);

        var far = new Rig();
        far.Vm.Lab.DistancePc = 5e9;
        double dilation = far.Vm.CurrentObserver.TimeDilation;
        Assert.True(dilation > 1.5);
        far.Seek(dilation * PhysicalConstants.Day);

        Assert.Equal(near.Scene.State.FrameRadiusCm, far.Scene.State.FrameRadiusCm, 6);
        far.Frame();
        near.Frame();
        Assert.Equal(near.Scene.Director.HalfHeightCm, far.Scene.Director.HalfHeightCm);
    }

    [Fact]
    public void ZeroSizeView_DoesNotThrow()
    {
        var rig = new Rig();
        rig.Seek(PhysicalConstants.Day);
        rig.Scene.SetViewSize(new Size(0, 0));
        rig.Host.RaiseDraw(new Size(0, 0));
        rig.Scene.Zoom(1);
        rig.Scene.Home();
        Assert.Empty(rig.Scene.RingLabels);
        rig.Scene.SetViewSize(View);
        Assert.NotEmpty(rig.Scene.RingLabels);
    }

    [Fact]
    public void ShowRings_False_HidesTheLabels()
    {
        var rig = new Rig();
        rig.Seek(PhysicalConstants.Day);
        rig.Frame();
        Assert.NotEmpty(rig.Scene.RingLabels);
        rig.Scene.ShowRings = false;
        Assert.Empty(rig.Scene.RingLabels);
        rig.Scene.ShowRings = true;
        Assert.NotEmpty(rig.Scene.RingLabels);
    }

    private static int OutcomeListeners(MainViewModel vm, object target)
    {
        var field = typeof(MainViewModel).GetField(nameof(MainViewModel.OutcomeChanged), BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (field.GetValue(vm) as EventHandler)?.GetInvocationList().Count(d => d.Target == target) ?? 0;
    }

    private static int TimelineListeners(MainViewModel vm, object target)
    {
        var field = typeof(ObservableObject).GetField("PropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (field.GetValue(vm.Timeline) as PropertyChangedEventHandler)?.GetInvocationList().Count(d => d.Target == target) ?? 0;
    }

    // ---------------------------------------------------------------- Task 7: the cutaway

    private static SceneController CutawayController(string? presetId, double observerS, out MainViewModel vm)
    {
        vm = NewViewModel();
        if (presetId is not null) vm.Lab.SelectedPreset = PresetCatalog.Get(presetId);
        vm.Timeline.ObserverTimeS = observerS;
        return new SceneController(vm, () => new FakeHost());
    }

    [Fact]
    public void Legend_ListsOnlyTheLayersPresent()
    {
        // A Type Ic (a 20 M☉ star stripped of its hydrogen and helium): no hydrogen layer. The lab's Ic keeps 0.05 M☉ of helium
        // (under the 0.14 M☉ that shows as helium lines; ~1.4% of the ejecta, about 2,900 particles), so helium is listed too.
        var ic = CutawayController(null, 1, out var icVm);
        icVm.Lab.InitialMass = 20;
        icVm.Lab.Stripping = SupernovaLab.Core.Progenitors.Stripping.HydrogenAndHelium;
        icVm.Timeline.ObserverTimeS = 20 * PhysicalConstants.Day;
        Assert.Equal(SupernovaLab.Core.Outcomes.SupernovaType.Ic, icVm.CurrentOutcome.Type);
        Assert.Empty(ic.Legend);   // the cutaway is off
        ic.IsCutaway = true;
        Assert.Equal(new[] { "Nickel/iron", "Oxygen/silicon", "Helium" }, ic.Legend.Select(l => l.Name));
        Assert.Equal(new[] { "#EF9F27", "#639922", "#378ADD" }, ic.Legend.Select(l => l.ColorHex));

        // A Type Ia (sn2011fe): nickel/iron, intermediate-mass elements, unburned carbon/oxygen.
        var ia = CutawayController("sn2011fe", 20 * PhysicalConstants.Day, out _);
        ia.IsCutaway = true;
        Assert.Equal(new[] { "Nickel/iron", "Intermediate-mass elements", "Unburned carbon/oxygen" }, ia.Legend.Select(l => l.Name));
        Assert.Equal(new[] { "#EF9F27", "#7F77DD", "#888780" }, ia.Legend.Select(l => l.ColorHex));

        // A hydrogen-rich explosion (SN 1987A): all four core-collapse layers, hydrogen outermost.
        var ii = CutawayController("sn1987a", 20 * PhysicalConstants.Day, out _);
        ii.IsCutaway = true;
        Assert.Equal(new[] { "Nickel/iron", "Oxygen/silicon", "Helium", "Hydrogen" }, ii.Legend.Select(l => l.Name));
        ic.Dispose(); ia.Dispose(); ii.Dispose();
    }

    [Fact]
    public void ShellKey_IsShownWhileTheShocksAreDrawn()
    {
        // Task 8: the key explains the swept-up shell and the reverse shock whenever they are drawn, cutaway or not.
        var c = CutawayController(null, 1, out var vm);
        Assert.False(c.ShowShellKey);
        Assert.False(c.ShowKey);
        vm.Timeline.ObserverTimeS = 30 * PhysicalConstants.Year;
        Assert.True(c.ShowShellKey);
        Assert.True(c.ShowKey);
        Assert.Empty(c.Legend);
        Assert.Equal("#CCE0FF", c.ForwardShellHex);   // (0.80, 0.88, 1.00)
        Assert.Contains("Shocks: radii from", c.NotesText);
        c.Dispose();
    }

    [Fact]
    public void Readout_HasASecondLineForTheForwardShock_WhileShocksAreDrawn()
    {
        // Ruling R-P2-25: one line (the debris radius) before the shocks start; two lines once they are drawn.
        var c = CutawayController(null, PhysicalConstants.Day / 2, out var vm);
        Assert.Null(c.State.Shocks);
        Assert.Single(c.RadiusText.Split(Environment.NewLine));
        vm.Timeline.ObserverTimeS = 30 * PhysicalConstants.Year;
        var lines = c.RadiusText.Split(Environment.NewLine);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Blast-wave radius: ", lines[0]);
        Assert.Equal($"Forward shock (steep-ejecta solution): {UnitFormat.Distance(c.State.Shocks!.ForwardCm)}", lines[1]);
        vm.Timeline.ObserverTimeS = PhysicalConstants.Year;
        lines = c.RadiusText.Split(Environment.NewLine);
        Assert.StartsWith("Ejecta radius: ", lines[0]);
        Assert.StartsWith("Forward shock (radio model's shock): ", lines[1]);
        c.Dispose();
    }

    [Fact]
    public void Legend_BeforeBreakout_ListsTheStarsLayers_AndTheNoteStatesTheCutaway()
    {
        var c = CutawayController("sn1987a", 100, out _);   // 100 s: well before this star's breakout
        Assert.True(c.State.ShowStar);
        c.IsCutaway = true;
        Assert.Equal(new[] { "Oxygen/silicon", "Helium", "Hydrogen" }, c.Legend.Select(l => l.Name));
        Assert.Contains("Cutaway: colours show elements (schematic)", c.NotesText);
        Assert.Contains("The shock is crossing the star: it reaches the surface", c.NotesText);
        c.IsCutaway = false;
        Assert.Empty(c.Legend);
        Assert.DoesNotContain("Cutaway", c.NotesText);
        c.Dispose();
    }

    [Fact]
    public void Cutaway_TurnsTheCameraWithTheWedge_SoTheNotchKeepsFacingTheViewer()
    {
        var c = CutawayController("sn1987a", 20 * PhysicalConstants.Day, out _);
        Assert.Equal(30, c.Camera.YawDeg, 6);   // off: the original default view
        Assert.Equal(20, c.Camera.PitchDeg, 6);
        c.IsCutaway = true;
        double phi = c.WedgeAngleRad;
        Assert.True(phi > 0);
        // The camera direction seen from the wedge frame is the original 30/20 degrees.
        var b = c.Camera.Backward;
        var w = SupernovaLab.Core.Structure.WedgeOrientation.ToWedge(phi, b.X, b.Y, b.Z);
        double yaw = 30 * Math.PI / 180, pitch = 20 * Math.PI / 180;
        Assert.Equal(Math.Cos(pitch) * Math.Cos(yaw), w.X, 9);
        Assert.Equal(Math.Cos(pitch) * Math.Sin(yaw), w.Y, 9);
        Assert.Equal(Math.Sin(pitch), w.Z, 9);
        c.Rotate(40, 0);   // the user turns it: Home restores the wedge-facing default
        c.Home();
        var h = c.Camera.Backward;
        Assert.Equal(b.X, h.X, 9);
        Assert.Equal(b.Z, h.Z, 9);
        c.IsCutaway = false;
        Assert.Equal(30, c.Camera.YawDeg, 6);
        c.Dispose();
    }
}
