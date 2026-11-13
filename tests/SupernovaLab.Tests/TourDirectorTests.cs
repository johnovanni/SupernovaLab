using System.Windows;
using SupernovaLab.App.Controllers;
using SupernovaLab.App.Tour;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>The guided tour's director and view model (Project 2, Task 13; spec criterion 4; Review Focus 4).</summary>
public class TourDirectorTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromSeconds(1.0 / 60);

    private sealed class FakeHost : IRenderHost
    {
        public bool Animating { get; set; }
        public event Action<string>? ContextReady { add { } remove { } }
        public event Action<TimeSpan, Size>? Draw { add { } remove { } }
        public event Action<Exception>? FrameFailed { add { } remove { } }
        public void Start() { }
        public void RequestRender() { }
        public bool Shutdown(Action release) => false;
    }

    private static MainViewModel NewVm(string? presetId = null)
    {
        var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        if (presetId is not null) vm.Lab.SelectedPreset = PresetCatalog.Get(presetId);
        return vm;
    }

    private static (MainViewModel Vm, SceneController Scene, TourDirector Director) Rig(string? presetId = null, bool withScene = true)
    {
        var vm = NewVm(presetId);
        var scene = withScene ? new SceneController(vm, () => new FakeHost()) : null;
        if (scene is not null) vm.Tour.Director.Scene = scene;
        return (vm, scene!, vm.Tour.Director);
    }

    public static IEnumerable<object[]> PresetIds() => PresetCatalog.LoadEmbedded().Select(p => new object[] { p.Id });

    /// <summary>Runs 1/60 s ticks until the tour is off; returns the stop indices in the order they were reached.</summary>
    private static List<int> RunToEnd(TourDirector d, MainViewModel vm)
    {
        var visited = new List<int>();
        int last = -1;
        double prevT = vm.Timeline.ObserverTimeS;
        for (int i = 0; i < 60 * 900 && d.State != TourState.Off; i++)
        {
            d.Tick(Frame);
            if (d.State == TourState.Off) break;
            if (d.StopIndex != last) { last = d.StopIndex; visited.Add(last); }
            double t = vm.Timeline.ObserverTimeS;
            Assert.True(t >= prevT, "the tour only plays forward");
            Assert.InRange(t, TimeAxis.MinS, TimeAxis.MaxS);
            prevT = t;
        }
        return visited;
    }

    [Theory]
    [MemberData(nameof(PresetIds))]
    public void Tour_RunsStartToEnd_WithNoError(string id)
    {
        var (vm, _, d) = Rig(id, withScene: false);   // the 3D view is covered by the tests below; this one is the director's own logic
        d.Start();
        Assert.Equal(TourState.Dwelling, d.State);
        int count = d.Stops.Count;
        Assert.True(count >= 1);

        var visited = RunToEnd(d, vm);

        Assert.Equal(TourState.Off, d.State);
        Assert.Equal(Enumerable.Range(0, count), visited.Prepend(0).Distinct());   // every stop, in order
        Assert.Null(d.Note);
        Assert.False(vm.Tour.IsActive);
        Assert.All(vm.Panels, p => Assert.False(p.IsHighlighted));
    }

    [Fact]
    public void Tour_RunsStartToEnd_ForTheDefaultLab_WithoutAScene()
    {
        var (vm, _, d) = Rig(withScene: false);
        d.Start();
        var visited = RunToEnd(d, vm);
        Assert.Equal(TourState.Off, d.State);
        Assert.Equal(Enumerable.Range(0, d.Stops.Count), visited.Prepend(0).Distinct());
    }

    [Fact]
    public void Start_JumpsToTheFirstStop_AndStopsThePlayback()
    {
        var (vm, _, d) = Rig("sn1987a");
        vm.Timeline.ObserverTimeS = 1e6;
        vm.Timeline.IsPlaying = true;
        d.Start();

        Assert.Equal(0, d.StopIndex);
        Assert.Equal(TimeAxis.MinS, vm.Timeline.ObserverTimeS);
        Assert.False(vm.Timeline.IsPlaying);   // the director drives the time
        Assert.Equal(d.Stops[0].Caption, vm.Tour.Caption);
        Assert.True(vm.Tour.IsActive);
        Assert.False(vm.Tour.ShowResume);
    }

    [Fact]
    public void Tour_SlowsNearAStop_AndDwells()
    {
        var (vm, _, d) = Rig("sn1987a", withScene: false);
        var playbackFlags = new List<bool>();
        vm.Timeline.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TimelineViewModel.ObserverTimeS)) playbackFlags.Add(vm.Timeline.IsPlaybackStep);
        };
        d.Start();
        double stop0 = d.Stops[0].ObserverTimeS;

        // Dwell: the time holds for DwellS (4 s) with the caption shown.
        int dwellTicks = 0;
        while (d.State == TourState.Dwelling && dwellTicks < 1000)
        {
            Assert.Equal(stop0, vm.Timeline.ObserverTimeS);
            d.Tick(Frame);
            dwellTicks++;
        }
        Assert.InRange(dwellTicks, (int)(d.Stops[0].DwellS * 60) - 1, (int)(d.Stops[0].DwellS * 60) + 2);
        Assert.Equal(TourState.Playing, d.State);

        // Travel: full default speed far from the next stop, the minimum speed within 0.2 decades of it.
        double target = d.Stops[1].ObserverTimeS;
        var far = new List<double>();
        var near = new List<double>();
        playbackFlags.Clear();
        int safety = 0;
        while (d.State == TourState.Playing && safety++ < 100000)
        {
            double before = vm.Timeline.ObserverTimeS, remaining = Math.Log10(target / before);
            d.Tick(Frame);
            if (d.State != TourState.Playing) break;
            double step = Math.Log10(vm.Timeline.ObserverTimeS / before);
            (remaining > TourDirector.SlowDecades ? far : near).Add(step);
        }
        Assert.NotEmpty(far);
        Assert.NotEmpty(near);
        Assert.All(far, s => Assert.Equal(TimelineViewModel.DefaultDecadesPerSecond / 60, s, 6));
        Assert.All(near, s => Assert.Equal(TimelineViewModel.MinDecadesPerSecond / 60, s, 6));
        Assert.True(playbackFlags.Count > 0);
        Assert.All(playbackFlags.Take(playbackFlags.Count - 1), f => Assert.True(f));   // eased by the camera, not jumped
        Assert.Equal(1, d.StopIndex);
        Assert.Equal(target, vm.Timeline.ObserverTimeS);   // lands exactly on the stop
        Assert.Equal(TourState.Dwelling, d.State);
    }

    [Fact]
    public void AtAStop_TheCutaway_Orbit_AndHighlight_AreApplied()
    {
        var (vm, scene, d) = Rig("sn1987a");
        scene.IsCutaway = false;
        d.Start();
        var first = d.Stops[0];
        Assert.True(first.Cutaway);
        Assert.True(first.OrbitDegPerS > 0);
        Assert.True(scene.IsCutaway);

        double yaw = scene.Camera.YawDeg;
        for (int i = 0; i < 60; i++) d.Tick(Frame);
        double turned = (scene.Camera.YawDeg - yaw + 360) % 360;
        Assert.Equal(first.OrbitDegPerS, turned, 0);   // one second of orbit

        // The neutrino stop highlights the neutrino panel and its tile, and no other.
        int n = d.Stops.ToList().FindIndex(s => s.Highlight == Messenger.Neutrinos);
        Assert.True(n > 0);
        d.GoTo(n);
        Assert.Equal(Messenger.Neutrinos, d.Current!.Highlight);
        Assert.Equal(new[] { Messenger.Neutrinos }, vm.Panels.Where(p => p.IsHighlighted).Select(p => p.Messenger));
        Assert.Equal(new[] { Messenger.Neutrinos }, vm.Tiles.Where(t => t.IsHighlighted).Select(t => t.Messenger));
        Assert.Equal(d.Current.Cutaway, scene.IsCutaway);
    }

    public static IEnumerable<object[]> UserInputs() => new[]
    {
        new object[] { "scrub" }, new object[] { "marker" }, new object[] { "play" },
        new object[] { "rotate" }, new object[] { "zoom" }, new object[] { "home" },
    };

    [Theory]
    [MemberData(nameof(UserInputs))]
    public void UserInput_PausesTheTour_ResumeContinues(string input)
    {
        var (vm, scene, d) = Rig("sn1987a");
        d.Start();
        for (int i = 0; i < 600 && d.State != TourState.Playing; i++) d.Tick(Frame);   // into the first leg
        for (int i = 0; i < 30; i++) d.Tick(Frame);
        Assert.Equal(TourState.Playing, d.State);

        switch (input)
        {
            case "scrub": vm.Timeline.ObserverTimeS = 5; break;
            case "marker": vm.Timeline.JumpToMarkerCommand.Execute(vm.Timeline.Markers[0]); break;
            case "play": vm.Timeline.PlayPauseCommand.Execute(null); break;
            case "rotate": scene.Rotate(20, 0); break;
            case "zoom": scene.Zoom(1); break;
            case "home": scene.Home(); break;
        }

        Assert.Equal(TourState.Paused, d.State);
        Assert.True(vm.Tour.ShowResume);
        Assert.True(vm.Tour.IsActive);
        vm.Timeline.IsPlaying = false;
        double held = vm.Timeline.ObserverTimeS, yaw = scene.Camera.YawDeg;
        for (int i = 0; i < 120; i++) d.Tick(Frame);
        Assert.Equal(held, vm.Timeline.ObserverTimeS);   // paused: nothing moves
        Assert.Equal(yaw, scene.Camera.YawDeg);

        int from = d.StopIndex;
        vm.Tour.ResumeCommand.Execute(null);
        Assert.NotEqual(TourState.Paused, d.State);
        Assert.False(vm.Tour.ShowResume);
        double resumedAt = vm.Timeline.ObserverTimeS;
        for (int i = 0; i < 60; i++) d.Tick(Frame);
        Assert.True(d.StopIndex >= from);
        Assert.True(vm.Timeline.ObserverTimeS > resumedAt || d.StopIndex > from || d.State == TourState.Dwelling);   // it carries on
    }

    [Fact]
    public void ResumeAfterAScrubPastAStop_ContinuesFromTheStopBehindTheTime()
    {
        var (vm, _, d) = Rig("sn1987a", withScene: false);
        d.Start();
        double t = d.Stops[3].ObserverTimeS * 1.01;
        vm.Timeline.ObserverTimeS = t;
        Assert.Equal(TourState.Paused, d.State);
        d.Resume();
        Assert.Equal(3, d.StopIndex);
        Assert.Equal(TourState.Playing, d.State);
        Assert.Equal(t, vm.Timeline.ObserverTimeS);   // the time is where the user put it
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NewOutcome_EndsTheTour_WithANote(bool pausedFirst)
    {
        var (vm, scene, d) = Rig("sn1987a");
        d.Start();
        d.GoTo(d.Stops.ToList().FindIndex(s => s.Highlight is not null));
        if (pausedFirst) d.Pause();

        vm.Lab.SelectedPreset = PresetCatalog.Get("sn1993j");

        Assert.Equal(TourState.Off, d.State);
        Assert.Equal("Tour ended: new explosion", d.Note);
        Assert.Contains("Tour ended: new explosion", vm.StatusMessage);
        Assert.Equal("Tour ended: new explosion", vm.Tour.Note);
        Assert.False(vm.Tour.IsActive);
        Assert.All(vm.Panels, p => Assert.False(p.IsHighlighted));
        Assert.False(scene.IsCutaway);

        // Starting again clears the note.
        d.Start();
        Assert.Null(d.Note);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Exit_RestoresCutaway_AndClearsHighlight(bool cutawayBefore)
    {
        var (vm, scene, d) = Rig("sn1987a");
        scene.IsCutaway = cutawayBefore;
        d.Start();
        d.GoTo(d.Stops.ToList().FindIndex(s => !s.Cutaway && s.Highlight is not null));
        Assert.False(scene.IsCutaway);
        Assert.Contains(vm.Panels, p => p.IsHighlighted);

        d.Exit();

        Assert.Equal(TourState.Off, d.State);
        Assert.Equal(cutawayBefore, scene.IsCutaway);
        Assert.All(vm.Panels, p => Assert.False(p.IsHighlighted));
        Assert.All(vm.Tiles, t => Assert.False(t.IsHighlighted));
        Assert.Null(d.Note);
    }

    [Fact]
    public void NextAndPrevious_AreJumps_AndStayWithinTheTour()
    {
        var (vm, _, d) = Rig("sn1987a", withScene: false);
        var flags = new List<bool>();
        d.Start();
        vm.Timeline.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TimelineViewModel.ObserverTimeS)) flags.Add(vm.Timeline.IsPlaybackStep);
        };

        d.Next();
        Assert.Equal(1, d.StopIndex);
        Assert.Equal(d.Stops[1].ObserverTimeS, vm.Timeline.ObserverTimeS);
        Assert.Equal(TourState.Dwelling, d.State);   // a skip does not pause the tour
        d.Previous();
        Assert.Equal(0, d.StopIndex);
        d.Previous();
        Assert.Equal(0, d.StopIndex);
        Assert.Equal(new[] { false, false }, flags.Take(2));   // jumps, not playback steps
        for (int i = 0; i < d.Stops.Count; i++) d.Next();
        Assert.Equal(TourState.Off, d.State);   // Next past the last stop ends the tour
    }

    [Fact]
    public void TourViewModel_CommandsDriveTheDirector_AndCaptionCarriesItsSources()
    {
        var (vm, _, d) = Rig("sn1987a", withScene: false);
        var tour = vm.Tour;
        Assert.False(tour.IsActive);
        Assert.Equal("", tour.Caption);
        Assert.True(tour.TourCommand.CanExecute(null));

        tour.TourCommand.Execute(null);
        Assert.True(tour.IsActive);
        Assert.False(tour.TourCommand.CanExecute(null));

        // A stop with sources: the tooltip is the stop's ready-made source text (criterion 4: source and confidence on hover).
        int withSources = d.Stops.ToList().FindIndex(s => s.Notes.Any(n => n.Kind == TourSourceKind.LabModel));
        d.GoTo(withSources);
        Assert.Equal(d.Current!.Caption, tour.Caption);
        Assert.Equal(d.Current.SourceText, tour.SourceText);
        Assert.True(tour.HasSources);
        Assert.Contains("In the lab (model)", tour.SourceText);

        // An observed stop's text names its kind and its source.
        int observed = d.Stops.ToList().FindIndex(s => s.Notes.Any(n => n.Kind != TourSourceKind.LabModel));
        d.GoTo(observed);
        Assert.Contains("Observed (", tour.SourceText);
        Assert.Contains("[presets", tour.SourceText);

        tour.PauseCommand.Execute(null);
        Assert.True(tour.ShowResume);
        tour.ResumeCommand.Execute(null);
        Assert.False(tour.ShowResume);
        tour.PreviousCommand.Execute(null);
        Assert.Equal(observed - 1, d.StopIndex);
        tour.ExitCommand.Execute(null);
        Assert.False(tour.IsActive);
        Assert.Equal("", tour.Caption);
        Assert.False(tour.HasSources);
    }

    [Fact]
    public void TheScenesUserInput_DoesNotPauseAnInactiveTour()
    {
        var (vm, scene, d) = Rig("sn1987a");
        scene.Rotate(10, 0);
        scene.Zoom(1);
        vm.Timeline.ObserverTimeS = 100;
        Assert.Equal(TourState.Off, d.State);
        Assert.False(vm.Tour.ShowResume);
    }

    [Fact]
    public void DetachingTheScene_StopsItsInputPausingTheTour()
    {
        var (_, scene, d) = Rig("sn1987a");
        d.Start();
        d.Scene = null;
        scene.Rotate(10, 0);
        Assert.NotEqual(TourState.Paused, d.State);
    }

    [Fact]
    public void Notes_PutQualityAndTheCurrentEventsNoteFirst()
    {
        var notes = new[]
        {
            "The shock is crossing the star", "Gas around the star: extent from x", ExplosionScene.FlashNote,
            SceneController.CutawayNote, "Colours schematic",
        };
        var ordered = SceneController.OrderNotes(notes, "Quality lowered to Low");
        Assert.Equal("Quality lowered to Low", ordered[0]);
        Assert.Equal(new[] { ExplosionScene.FlashNote, SceneController.CutawayNote }, ordered.Skip(1).Take(2));
        Assert.Equal(new[] { "The shock is crossing the star", "Gas around the star: extent from x", "Colours schematic" }, ordered.Skip(3));

        Assert.Equal(5, SceneController.OrderNotes(notes, null).Count);   // no note is dropped
        Assert.Equal(ExplosionScene.FlashNote, SceneController.OrderNotes(notes, null)[0]);
    }

    [Fact]
    public void TimelineUserInput_RaisesAnEvent_ButNotPlaybackOrDirectedWrites()
    {
        var timeline = new TimelineViewModel();
        int raised = 0;
        timeline.UserInteracted += (_, _) => raised++;

        timeline.ObserverTimeS = 5;                     // scrub
        Assert.Equal(1, raised);
        timeline.PlayPauseCommand.Execute(null);        // play
        Assert.Equal(2, raised);
        timeline.Tick(TimeSpan.FromSeconds(0.1));       // playback
        Assert.Equal(2, raised);
        timeline.DirectTo(50, playbackStep: true);      // the director
        timeline.DirectTo(500, playbackStep: false);
        Assert.Equal(2, raised);
        Assert.False(timeline.IsPlaybackStep);
    }
}
