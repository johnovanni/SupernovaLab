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
        public int Renders { get; private set; }
        public bool ShutDown { get; private set; }
        public bool Animating { get; set; }
        public event Action<string>? ContextReady;
        public event Action<TimeSpan, Size>? Draw;
        public event Action<Exception>? FrameFailed;
        public void Start() { if (StartFailure is not null) throw StartFailure; }
        public void RequestRender() => Renders++;
        public void Shutdown(Action release) => ShutDown = true;
        public void Fail(Exception e) => FrameFailed?.Invoke(e);
        public bool HasListeners => ContextReady is not null || Draw is not null;
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
