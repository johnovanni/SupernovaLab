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
        public event Action<TimeSpan, Size>? Draw { add { } remove { } }
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
}
