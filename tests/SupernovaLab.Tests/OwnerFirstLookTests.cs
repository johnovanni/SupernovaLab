using System.Windows;
using System.Windows.Threading;
using SupernovaLab.App.Controllers;
using SupernovaLab.App.Services;
using SupernovaLab.App.Tour;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>
/// Project 3, Task 15: the owner's four requests after first using the app (2026-11-16). Rulings R-P3-41 (a change to the outcome
/// pauses and goes back to t = 0), R-P3-42 (t = 0, the timeline's left end, shows the initial condition everywhere), the Close-up's
/// sound (it could not be heard in practice), and R-P3-43 (the Close-up opens at the lower right).
/// </summary>
public class OwnerFirstLookTests
{
    private const double Day = PhysicalConstants.Day, Year = PhysicalConstants.Year;
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

    private static MainViewModel Vm(string? preset = null)
    {
        var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        if (preset is not null) vm.Lab.SelectedPreset = PresetCatalog.Get(preset);
        return vm;
    }

    private static MainViewModel PlayingAt(double observerTimeS, string? preset = null)
    {
        var vm = Vm(preset);
        vm.Timeline.ObserverTimeS = observerTimeS;
        vm.Timeline.IsPlaying = true;
        return vm;
    }

    // =========================================================== 1. R-P3-41: a change to the outcome pauses and goes back to t = 0

    public static IEnumerable<object[]> OutcomeEdits() => new[]
    {
        new object[] { "initial mass", (Action<LabViewModel>)(l => l.InitialMass = 20) },
        new object[] { "metallicity", (Action<LabViewModel>)(l => l.Metallicity = 0.3) },
        new object[] { "stripping", (Action<LabViewModel>)(l => l.Stripping = Stripping.Full) },
        new object[] { "kind", (Action<LabViewModel>)(l => l.Kind = StarKind.WhiteDwarf) },
        new object[] { "advanced: mixing", (Action<LabViewModel>)(l => l.Mixing = NickelMixing.Weak) },
        new object[] { "advanced: dense gas", (Action<LabViewModel>)(l => l.DenseCsm = true) },
        new object[] { "advanced: opacity", (Action<LabViewModel>)(l => l.Opacity = 0.2) },
        new object[] { "a preset", (Action<LabViewModel>)(l => l.SelectedPreset = PresetCatalog.Get("sn1054")) },
    };

    [Theory]
    [MemberData(nameof(OutcomeEdits))]
    public void AnOutcomeChange_PausesAndGoesBackToTheStart(string what, Action<LabViewModel> edit)
    {
        var vm = PlayingAt(3 * Day);
        var before = vm.CurrentOutcome;
        edit(vm.Lab);
        Assert.NotSame(before, vm.CurrentOutcome);
        Assert.False(vm.Timeline.IsPlaying, what);
        Assert.Equal(TimeAxis.MinS, vm.Timeline.ObserverTimeS);
        Assert.True(vm.Timeline.IsAtStart, what);
    }

    [Fact]
    public void DroppingAPresetsPins_IsAnOutcomeChange()
    {
        var vm = PlayingAt(3 * Day, "sn1987a");
        vm.Timeline.ObserverTimeS = 3 * Day;
        vm.Timeline.IsPlaying = true;
        vm.Lab.SelectedPreset = null;
        Assert.False(vm.Timeline.IsPlaying);
        Assert.True(vm.Timeline.IsAtStart);
    }

    public static IEnumerable<object[]> ObserverEdits() => new[]
    {
        new object[] { "distance", (Action<LabViewModel>)(l => l.DistancePc = 50_000) },
        new object[] { "dust", (Action<LabViewModel>)(l => l.Av = 2) },
        new object[] { "beam toward Earth", (Action<LabViewModel>)(l => l.BeamTowardEarth = !l.BeamTowardEarth) },
    };

    [Theory]
    [MemberData(nameof(ObserverEdits))]
    public void AnObserverChange_KeepsTheTimeAndPlayback(string what, Action<LabViewModel> edit)
    {
        var vm = PlayingAt(3 * Day);
        edit(vm.Lab);
        Assert.True(vm.Timeline.IsPlaying, what);
        Assert.Equal(3 * Day, vm.Timeline.ObserverTimeS);
    }

    [Fact]
    public void ReRoll_KeepsTheTimeAndPlayback()
    {
        var vm = PlayingAt(3 * Day);
        vm.RerollCommand.Execute(null);
        Assert.True(vm.Timeline.IsPlaying);
        Assert.Equal(3 * Day, vm.Timeline.ObserverTimeS);
    }

    [Fact]
    public void TheReset_IsNotUserInputOnTheTimeline()
    {
        var vm = PlayingAt(3 * Day);
        int input = 0;
        vm.Timeline.UserInteracted += (_, _) => input++;
        vm.Lab.InitialMass = 20;
        Assert.Equal(0, input);
    }

    [Fact]
    public void ARunningTour_EndsWithItsNewExplosionNote_OnAnOutcomeChange()
    {
        var vm = Vm("sn1987a");
        var scene = new SceneController(vm, () => new FakeHost());
        var d = vm.Tour.Director;
        d.Scene = scene;
        d.Start();
        for (int i = 0; i < 2000 && d.StopIndex < 2; i++) d.Tick(Frame);
        Assert.NotEqual(TourState.Off, d.State);
        vm.Lab.InitialMass = 20;
        Assert.Equal(TourState.Off, d.State);
        Assert.Equal(TourDirector.NewExplosionNote, d.Note);
        Assert.True(vm.Timeline.IsAtStart);
        Assert.False(vm.Timeline.IsPlaying);
    }

    [Fact]
    public void AnObserverChange_DuringATour_StillEndsIt_AsBefore()
    {
        var vm = Vm("sn1987a");
        var d = vm.Tour.Director;
        d.Start();
        for (int i = 0; i < 2000 && d.StopIndex < 2; i++) d.Tick(Frame);
        double t = vm.Timeline.ObserverTimeS;
        vm.Lab.Av = 1;
        Assert.Equal(TourState.Off, d.State);
        Assert.Equal(t, vm.Timeline.ObserverTimeS);   // the observer does not reset the time
    }

    // =========================================================== 2. R-P3-42: t = 0 (the left end) shows the initial condition

    [Fact]
    public void TheTimeline_SaysTZero_AtItsLeftEnd()
    {
        var vm = Vm();
        Assert.True(vm.Timeline.IsAtStart);
        Assert.Equal("t = 0 · before core bounce", vm.Timeline.ObserverTimeText);
        vm.Timeline.ObserverTimeS = 2e-3;
        Assert.False(vm.Timeline.IsAtStart);
        Assert.Equal("2 ms after core bounce", vm.Timeline.ObserverTimeText);
        vm.Timeline.ScrubFraction = 0;
        Assert.True(vm.Timeline.IsAtStart);
    }

    [Fact]
    public void TheTimeline_SaysBeforeIgnition_ForAThermonuclearOutcome()
    {
        var vm = Vm("sn2011fe");
        Assert.Equal("t = 0 · before ignition", vm.Timeline.ObserverTimeText);
    }

    public static IEnumerable<object[]> AllPresets() => PresetCatalog.LoadEmbedded().Select(p => new object[] { p.Id });

    [Theory]
    [MemberData(nameof(AllPresets))]
    public void TheMainView_AtTheLeftEnd_ShowsTheStarBeforeTheExplosion(string presetId)
    {
        var vm = Vm(presetId);
        var scene = new SceneController(vm, () => new FakeHost());
        var s = scene.State;
        Assert.False(s.ShowEjecta, presetId);
        Assert.False(s.ShowParticles, presetId);
        Assert.False(s.ShowJets, presetId);
        Assert.Equal(0f, s.FlashIntensity);
        Assert.True(double.IsNaN(s.ShockInStarRadiusCm), presetId);
        Assert.DoesNotContain(s.Notes, n => n.StartsWith("The shock is crossing", StringComparison.Ordinal));
        bool ia = vm.CurrentOutcome.Inputs.Kind == StarKind.WhiteDwarf;
        if (ia)
        {
            Assert.Equal(SceneMarker.WhiteDwarf, s.Marker);
            Assert.Contains(ExplosionScene.BeforeIgnitionNote, s.Notes);
        }
        else
        {
            Assert.True(s.ShowStar, presetId);
            Assert.Equal(SceneMarker.None, s.Marker);
            Assert.Contains(ExplosionScene.BeforeExplosionNote, s.Notes);
        }

        // One playback step later the explosion has begun, as before.
        vm.Timeline.IsPlaying = true;
        vm.Timeline.Tick(Frame);
        Assert.False(vm.Timeline.IsAtStart);
        Assert.DoesNotContain(ExplosionScene.BeforeExplosionNote, scene.State.Notes);
        Assert.DoesNotContain(ExplosionScene.BeforeIgnitionNote, scene.State.Notes);
        scene.Dispose();
    }

    [Fact]
    public void TheMainView_AtTheLeftEnd_IsTheStarWithoutTheShock_EvenInTheCutaway()
    {
        var vm = Vm();
        var scene = new SceneController(vm, () => new FakeHost()) { IsCutaway = true };
        Assert.True(scene.State.ShowStar);
        Assert.NotEmpty(scene.State.StarLayers);              // the layers show in the cutaway
        Assert.True(double.IsNaN(scene.State.ShockInStarRadiusCm));   // and no shock yet
        Assert.False(scene.ShowShockKey);
        vm.Timeline.ObserverTimeS = 1.5e-3;
        Assert.True(double.IsFinite(scene.State.ShockInStarRadiusCm));
        scene.Dispose();
    }

    [Theory]
    [MemberData(nameof(AllPresets))]
    public void ThePanels_AtTheLeftEnd_SayNotYet_OrNotProduced(string presetId)
    {
        var vm = Vm(presetId);
        foreach (var p in vm.Panels)
        {
            Assert.True(p.DetectionText is "Not yet" or "Not produced by this supernova" or "Not observed by this instrument"
                    or "Not pointed at us" or "Not modeled here",
                $"{presetId} {p.Title}: {p.DetectionText}");
            Assert.True(p.StripBadgeText is "Not yet" or "Not produced" or "Not modeled" or "Not pointed at us",
                $"{presetId} {p.Title} tile: {p.StripBadgeText}");
            Assert.Null(p.CurrentValueText);
        }
        // The neutrinos are detected one step later, for SN 1987A as in 1987.
        if (presetId == "sn1987a")
        {
            vm.Timeline.ObserverTimeS = 0.5;
            Assert.Equal("Detected", vm.Panels.Single(p => p.Messenger == Messenger.Neutrinos).DetectionText);
        }
    }

    [Fact]
    public void TheNeutrinoPanel_AtTheLeftEnd_SaysWhy()
    {
        var vm = Vm();   // the default star: 1 ms after bounce its neutrinos were "Detected"
        var nu = vm.Panels.Single(p => p.Messenger == Messenger.Neutrinos);
        Assert.Equal("Not yet", nu.DetectionText);
        Assert.Contains(InstrumentPanelViewModel.BeforeExplosionReason, nu.DetectionToolTip);
        Assert.Equal(InstrumentPanelViewModel.BeforeExplosionReason, nu.PlotNote);
        vm.Timeline.ObserverTimeS = 2e-3;
        Assert.Equal("Detected", nu.DetectionText);
    }

    [Theory]
    [MemberData(nameof(AllPresets))]
    public void TheCloseUp_AtTheLeftEnd_ShowsThePreCollapseCore(string presetId)
    {
        var vm = Vm(presetId);
        var cu = new CloseUpViewModel(vm);
        bool coreCollapse = CoreCollapseHistory.TrackFor(vm.CurrentOutcome) is not null;
        Assert.Equal(coreCollapse ? CloseUpEra.PreCollapse : CloseUpEra.None, cu.State.Era);
        cu.Dispose();
    }

    // =========================================================== 3. The Close-up's sound

    private static (CloseUpViewModel Cu, FakePulseAudio Audio, MainViewModel Main) CloseUp(string? preset)
    {
        var main = Vm(preset);
        var fake = new FakePulseAudio();
        return (new CloseUpViewModel(main, () => fake), fake, main);
    }

    private static void AtSource(MainViewModel main, double sourceTimeS) => main.Timeline.ObserverTimeS = sourceTimeS * main.CurrentObserver.TimeDilation;

    /// <summary>
    /// The diagnosis, kept as a test: with the sound on, Explode then playing the whole timeline gives the Crab's clicks only while the
    /// pulses reach Earth, from 10 yr to the axis's end, about 6 of the run's 40 s; then playback stops at 1,000 yr.
    /// </summary>
    [Fact]
    public void Diagnosis_AWholePlayback_GivesOnlyAFewSecondsOfPulses()
    {
        var (cu, audio, main) = CloseUp("sn1054");
        cu.IsSoundOn = true;
        main.ExplodeCommand.Execute(null);
        double sounding = 0, total = 0;
        while (main.Timeline.IsPlaying && total < 100)
        {
            main.Timeline.Tick(Frame);
            total += Frame.TotalSeconds;
            if (audio.LastPeriod is not null) sounding += Frame.TotalSeconds;
        }
        Assert.InRange(total, 39, 42);
        Assert.InRange(sounding, 5, 7);
        cu.Dispose();
    }

    [Fact]
    public void TheSound_PlaysWhilePaused_WhenPulsesReachEarth_AsTheTraceKeepsRunning()
    {
        var (cu, audio, main) = CloseUp("sn1054");
        AtSource(main, 960 * Year);
        Assert.False(main.Timeline.IsPlaying);
        cu.IsSoundOn = true;
        Assert.NotNull(audio.LastPeriod);
        Assert.Equal(PulseSchedule.ReceivedPeriodS(main.CurrentOutcome, main.CurrentObserver, cu.SourceTimeS), audio.LastPeriod!.Value, 12);
        Assert.Contains("clicking", cu.SpeakerState);
        Assert.DoesNotContain("paused", cu.SpeakerState);
        cu.Dispose();
    }

    [Fact]
    public void TheSound_PlaysDuringTheGuidedTour_WhenPulsesReachEarth()
    {
        var (cu, audio, main) = CloseUp("sn1054");
        cu.IsSoundOn = true;
        main.Tour.Director.Start();
        main.Timeline.DirectTo(500 * Year, playbackStep: true);   // as the director moves the time, with the timeline itself paused
        Assert.NotNull(audio.LastPeriod);
        cu.Dispose();
    }

    [Theory]
    [InlineData(null, "silent until")]        // the default star at t = 0: the core, no pulsar yet
    [InlineData("sn1998bw", "no pulsar")]     // a black hole
    [InlineData("sn2011fe", "no pulsar")]     // a Type Ia: nothing left
    public void TheSoundsState_IsShownForEveryOutcome_NotOnlyInThePulseTraceRow(string? preset, string reason)
    {
        var (cu, audio, _) = CloseUp(preset);
        Assert.False(cu.ShowSoundStatus);
        cu.IsSoundOn = true;
        Assert.True(cu.ShowSoundStatus);
        Assert.False(cu.ShowPulseTrace);                        // the trace row is hidden here...
        Assert.StartsWith("Sound on, volume 80%", cu.SpeakerState);   // ...but the state shows at the top of the window
        Assert.Contains(reason, cu.SpeakerState);
        Assert.Null(audio.LastPeriod);
        cu.IsSoundOn = false;
        Assert.False(cu.ShowSoundStatus);
        cu.Dispose();
    }

    [Fact]
    public void TheSoundsState_NamesWhenThePulsesWillArrive()
    {
        var (cu, _, main) = CloseUp("sn1054");
        cu.IsSoundOn = true;
        AtSource(main, 3 * Year);
        Assert.Contains("hidden by debris until about 10 years (a speculative estimate)", cu.SpeakerState);
        Assert.Contains("Confidence: Speculative", cu.SpeakerStateToolTip);   // the shown number keeps its confidence and source
        Assert.Contains("Pulses shown from 10 yr", cu.SpeakerStateToolTip);
        AtSource(main, 0.5);
        Assert.Contains("silent until the pulsar's pulses reach Earth (about 10 years, a speculative estimate)", cu.SpeakerState);
        Assert.NotNull(cu.SpeakerStateToolTip);
        AtSource(main, 960 * Year);
        Assert.Null(cu.SpeakerStateToolTip);   // "clicking …" names no model time
        cu.Dispose();
    }

    [Fact]
    public void TheSound_IsLouderByDefault()
    {
        var (cu, audio, _) = CloseUp("sn1054");
        Assert.Equal(0.8f, cu.Volume);
        cu.IsSoundOn = true;
        Assert.Equal(0.8f, audio.Volume);
        cu.Dispose();
    }

    [Fact]
    public void TheClick_IsLowerPitched_500Hz_WholeCyclesUnderItsEnvelope()   // Task 16 made it 6 ms, three cycles (SoundHeardTests)
    {
        Assert.Equal(500, ClickTrain.CarrierHz);
        var t = new ClickTrain(44100) { Volume = 1 };
        t.SetPeriod(0.5);
        var x = new float[(int)(ClickTrain.ClickS * 44100) + 4];
        t.Fill(x, 0, x.Length);
        int length = (int)Math.Round(ClickTrain.ClickS * 44100);
        int crossings = 0;
        for (int i = 2; i < length - 1; i++) if (Math.Sign(x[i - 1]) * Math.Sign(x[i]) < 0) crossings++;
        Assert.Equal((int)Math.Round(2 * ClickTrain.CarrierHz * ClickTrain.ClickS) - 1, crossings);   // whole cycles
        Assert.InRange(x.Take(length).Max(Math.Abs), 0.9f, 1f);        // louder than before (0.68), never clipping
        Assert.Equal(0, x.Take(length).Sum(v => (double)v), 2);        // no net push: no thump
    }

    [Fact]
    public void TheAudio_CountsWhatItQueues_ForTheLaunchLog()
    {
        var player = new CountingPlayer();
        var audio = new NAudioPulseAudio(() => player);
        audio.Enabled = true;
        audio.SetPulsePeriod(0.0334);
        Assert.Equal(1, audio.DevicesOpened);
        player.Pull(44100);   // one second, as the device would pull it
        Assert.InRange(audio.ClicksQueued, 29, 31);
        Assert.Equal(44100, audio.SamplesQueued);
        Assert.Contains("audio device opened", audio.DiagnosticText);
        Assert.Contains("clicks queued", audio.DiagnosticText);
        audio.Dispose();
    }

    private sealed class CountingPlayer : NAudio.Wave.IWavePlayer
    {
        private NAudio.Wave.IWaveProvider? _source;
        public NAudio.Wave.PlaybackState PlaybackState { get; private set; }
        public float Volume { get; set; } = 1;
        public NAudio.Wave.WaveFormat OutputWaveFormat => _source!.WaveFormat;
        public event EventHandler<NAudio.Wave.StoppedEventArgs>? PlaybackStopped { add { } remove { } }
        public void Init(NAudio.Wave.IWaveProvider waveProvider) => _source = waveProvider;
        public void Play() => PlaybackState = NAudio.Wave.PlaybackState.Playing;
        public void Pause() => PlaybackState = NAudio.Wave.PlaybackState.Paused;
        public void Stop() => PlaybackState = NAudio.Wave.PlaybackState.Stopped;
        public void Dispose() { }
        public void Pull(int samples)
        {
            var bytes = new byte[samples * 4];
            _source!.Read(bytes, 0, bytes.Length);
        }
    }

    [Fact]
    public void TheWindowService_RemembersTheSoundSwitchAndVolume_ForTheSession()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                InstrumentsWindowTests.EnsureApplication();
                var fakes = new List<FakePulseAudio>();
                var service = new WindowService(closeUpFactory: _ => OffScreen(new Window()), audioFactory: () =>
                {
                    var f = new FakePulseAudio();
                    fakes.Add(f);
                    return f;
                });
                var vm = new MainViewModel(TestOutcomes.Engine, service);
                var owner = OffScreen(new Window { DataContext = vm });
                owner.Show();
                service.Attach(owner);
                vm.ShowCloseUpCommand.Execute(null);
                var cu = Assert.IsType<CloseUpViewModel>(service.CloseUp!.DataContext);
                cu.IsSoundOn = true;
                cu.Volume = 0.3f;
                service.CloseUp.Close();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                Assert.True(fakes[0].Disposed);
                vm.ShowCloseUpCommand.Execute(null);
                var again = Assert.IsType<CloseUpViewModel>(service.CloseUp!.DataContext);
                Assert.True(again.IsSoundOn);
                Assert.Equal(0.3f, again.Volume);
                owner.Close();
            }
            catch (Exception e) { error = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(error);
    }

    // =========================================================== 4. R-P3-43: the Close-up opens at the lower right

    private static readonly Rect WorkArea = new(0, 0, 1920, 1040);

    [Fact]
    public void TheCloseUp_OpensAtTheLowerRightOfTheWorkArea_With16DipMargin()
    {
        var at = CloseUpPlacement.LowerRight(WorkArea, new Rect(100, 50, 1366, 768), new Size(900, 640));
        Assert.Equal(new Point(1920 - 900 - 16, 1040 - 640 - 16), at);
    }

    [Fact]
    public void TheWorkArea_NeedNotStartAtZero()
    {
        var area = new Rect(0, 40, 1920, 1040);   // a taskbar along the top
        var at = CloseUpPlacement.LowerRight(area, new Rect(0, 40, 800, 600), new Size(900, 640));
        Assert.Equal(new Point(1004, 40 + 1040 - 640 - 16), at);
    }

    [Fact]
    public void AWindowLargerThanTheWorkArea_KeepsItsTitleBarOnScreen()
    {
        var area = new Rect(0, 0, 800, 600);
        var at = CloseUpPlacement.LowerRight(area, new Rect(0, 0, 800, 600), new Size(900, 640));
        Assert.Equal(new Point(0, 0), at);
    }

    [Fact]
    public void AMainWindowOnAnotherScreen_PutsTheCloseUpAtItsOwnLowerRight()
    {
        // WPF knows only the primary screen's work area (no P/Invoke): a main window whose centre is off it is on another screen, so the
        // Close-up goes to the lower right of the main window itself.
        var owner = new Rect(2000, 100, 1600, 900);
        var at = CloseUpPlacement.LowerRight(WorkArea, owner, new Size(900, 640));
        Assert.Equal(new Point(2000 + 1600 - 900 - 16, 100 + 900 - 640 - 16), at);
    }

    [Fact]
    public void OnAnotherScreen_TheCloseUpIsHeldInsideTheVirtualScreen()
    {
        // Two 1920 x 1080 monitors side by side; the main window hangs off the right one's right and bottom edges.
        var virtualScreen = new Rect(0, 0, 3840, 1080);
        var owner = new Rect(3000, 600, 1600, 900);
        var at = CloseUpPlacement.LowerRight(WorkArea, owner, new Size(900, 640), virtualScreen: virtualScreen);
        Assert.Equal(new Point(3840 - 900, 1080 - 640), at);
        Assert.False(CloseUpPlacement.OnPrimaryScreen(WorkArea, owner));
        // Unheld (an empty virtual screen, as in a --snapshot run, or none given): the main window's own corner.
        Assert.Equal(new Point(4600 - 916, 1500 - 656), CloseUpPlacement.LowerRight(WorkArea, owner, new Size(900, 640), virtualScreen: Rect.Empty));
        // On the primary screen the work area already lies inside the virtual screen: nothing changes.
        Assert.Equal(new Point(1004, 384), CloseUpPlacement.LowerRight(WorkArea, new Rect(100, 50, 1366, 768), new Size(900, 640), virtualScreen: virtualScreen));
    }

    [Fact]
    public void AnUnknownOwner_UsesTheWorkArea()
    {
        var at = CloseUpPlacement.LowerRight(WorkArea, Rect.Empty, new Size(900, 640));
        Assert.Equal(new Point(1004, 384), at);
    }

    [Fact]
    public void TheWindowService_PlacesTheCloseUp_UnlessTheUserMovedItThisSession()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                InstrumentsWindowTests.EnsureApplication();
                // A window that opens where its XAML says (CenterOwner), as the real Close-up does. The work area is given far off the
                // screen, so nothing flashes on the user's screen while the test runs.
                var area = new Rect(-20000, -20000, 1920, 1040);   // within Windows' coordinate limit at 125% scaling
                var service = new WindowService(closeUpFactory: _ => new Window
                {
                    Width = 300, Height = 200, ShowActivated = false, ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                }, audioFactory: () => new FakePulseAudio(), workArea: () => area);
                var vm = new MainViewModel(TestOutcomes.Engine, service);
                var owner = new Window { DataContext = vm, Width = 400, Height = 300, ShowActivated = false, ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.Manual, Left = area.Left, Top = area.Top };
                owner.Show();
                service.Attach(owner);
                vm.ShowCloseUpCommand.Execute(null);
                var w = service.CloseUp!;
                var expected = new Point(area.Right - 300 - 16, area.Bottom - 200 - 16);
                Assert.Equal(WindowStartupLocation.Manual, w.WindowStartupLocation);
                Near(expected.X, w.Left);
                Near(expected.Y, w.Top);
                // The user moves it; closed and opened again, it comes back where they left it.
                w.Left = expected.X - 100;
                w.Top = expected.Y - 50;
                w.Close();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                vm.ShowCloseUpCommand.Execute(null);
                Near(expected.X - 100, service.CloseUp!.Left);
                Near(expected.Y - 50, service.CloseUp.Top);
                service.CloseUp.Close();
                owner.Close();
            }
            catch (Exception e) { error = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(error);
    }

    [Fact]
    public void TheSoundCheck_IsASnapshotOption_ThatOpensTheCloseUp()
    {
        var r = SnapshotRequest.Parse(new[] { "--snapshot", "out", "--sound-check", "3" })!;
        Assert.Equal(3, r.SoundCheckSeconds);
        Assert.True(r.OpenCloseUp);
        Assert.Null(SnapshotRequest.Parse(new[] { "--snapshot", "out" })!.SoundCheckSeconds);
        Assert.Throws<ArgumentException>(() => SnapshotRequest.Parse(new[] { "--snapshot", "out", "--sound-check", "0" }));
        Assert.Throws<ArgumentException>(() => SnapshotRequest.Parse(new[] { "--snapshot", "out", "--sound-check", "31" }));
    }

    [Fact]
    public void APlaybackClock_CanBeStopped_SoTheSoundCheckHandsOverToTheBench()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                InstrumentsWindowTests.EnsureApplication();
                var tl = new TimelineViewModel();
                var clock = new PlaybackClock(tl);
                clock.Start();
                tl.IsPlaying = true;
                Assert.True(clock.IsTicking);
                clock.Stop();
                Assert.False(clock.IsTicking);
                tl.IsPlaying = false;
                tl.IsPlaying = true;
                Assert.False(clock.IsTicking);   // no longer following the timeline
            }
            catch (Exception e) { error = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(error);
    }

    /// <summary>Within a device pixel (Windows rounds a moved window's position to whole pixels, 0.8 DIP at 125%).</summary>
    private static void Near(double expected, double actual) => Assert.InRange(actual, expected - 1, expected + 1);

    private static Window OffScreen(Window w)
    {
        w.WindowStartupLocation = WindowStartupLocation.Manual;
        w.Left = -30000;
        w.Top = -30000;
        w.ShowActivated = false;
        w.ShowInTaskbar = false;
        return w;
    }
}
