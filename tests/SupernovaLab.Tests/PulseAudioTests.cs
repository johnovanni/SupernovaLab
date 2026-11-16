using System.Windows;
using System.Windows.Threading;
using SupernovaLab.App.Services;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>Plan Task 8 (spec §4.4, ruling P3-10): the click train, and when the Close-up asks for sound.</summary>
public class PulseAudioTests
{
    private const double Year = PhysicalConstants.Year;
    private const double CrabAgeS = 2.9568e10;
    private const int Rate = 44100;

    // ---- the click train (no audio device) ----

    private static float[] Render(ClickTrain t, double seconds)
    {
        var buf = new float[(int)(seconds * Rate)];
        t.Fill(buf, 0, buf.Length);
        return buf;
    }

    /// <summary>Sample indices where a click starts: the first nonzero sample after a gap of silence.</summary>
    private static List<int> Onsets(float[] x, int gap = 30)
    {
        var onsets = new List<int>();
        int lastNonZero = -1_000_000;
        for (int i = 0; i < x.Length; i++)
        {
            if (x[i] == 0) continue;
            if (i - lastNonZero > gap) onsets.Add(i);
            lastNonZero = i;
        }
        return onsets;
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.0334)]
    [InlineData(3.7)]
    [InlineData(0.01)]
    public void ClickTrain_ClicksAtThePeriod(double periodS)
    {
        var t = new ClickTrain(Rate) { Volume = 1 };
        t.SetPeriod(periodS);
        var onsets = Onsets(Render(t, 20));
        Assert.InRange(onsets.Count, (int)(20 / periodS) - 1, (int)(20 / periodS) + 1);
        for (int i = 1; i < onsets.Count; i++)
            Assert.Equal(periodS * Rate, onsets[i] - onsets[i - 1], 1.5);   // spaced by the period, to a sample
    }

    [Fact]
    public void ClickTrain_AClickIsClickSLong_UnderARaisedCosineEnvelope()   // Task 16: 6 ms (it was 2 ms)
    {
        var t = new ClickTrain(Rate) { Volume = 1 };
        t.SetPeriod(0.5);
        var x = Render(t, 0.4);
        var onset = Onsets(x)[0];
        int length = (int)Math.Round(ClickTrain.ClickS * Rate);
        Assert.True(x.Skip(onset + length + 2).Take((int)(0.4 * Rate) - onset - length - 3).All(v => v == 0), "silent between clicks");
        float peak = x.Skip(onset).Take(length).Max(Math.Abs);
        Assert.InRange(peak, 0.3f, 1f);
        Assert.Equal(0, x[onset + length - 1], 0.05);   // the envelope returns to zero smoothly
    }

    [Fact]
    public void ClickTrain_IsSilentWithoutAPeriod_AndStartsAClickWhenOneComes()
    {
        var t = new ClickTrain(Rate) { Volume = 1 };
        Assert.All(Render(t, 0.1), v => Assert.Equal(0, v));
        t.SetPeriod(0.2);
        Assert.NotEmpty(Onsets(Render(t, 0.3)));
        t.SetPeriod(null);
        Assert.All(Render(t, 0.1).Skip(200), v => Assert.Equal(0, v));   // a click in flight may finish; then silence
    }

    [Fact]
    public void ClickTrain_AFollowingPeriodChange_ChangesTheSpacing()
    {
        var t = new ClickTrain(Rate) { Volume = 1 };
        t.SetPeriod(0.5);
        Render(t, 2);
        t.SetPeriod(0.1);
        var on = Onsets(Render(t, 3));
        Assert.InRange(on.Count, 28, 31);
    }

    [Fact]
    public void ClickTrain_UnderAMillisecond_IsAToneAtOneOverP()
    {
        var t = new ClickTrain(Rate) { Volume = 1 };
        t.SetPeriod(0.0005);                   // 2 kHz
        var x = Render(t, 1);
        int crossings = 0;
        for (int i = 1; i < x.Length; i++) if ((x[i - 1] < 0) != (x[i] < 0)) crossings++;
        Assert.InRange(crossings, 3990, 4010);  // 2 crossings per cycle
        Assert.True(x.Max() > 0.2f);
    }

    [Fact]
    public void ClickTrain_VolumeScalesTheOutput_AndStaysInRange()
    {
        var loud = new ClickTrain(Rate) { Volume = 1 };
        var quiet = new ClickTrain(Rate) { Volume = 0.25f };
        loud.SetPeriod(0.1);
        quiet.SetPeriod(0.1);
        float a = Render(loud, 1).Max(Math.Abs), b = Render(quiet, 1).Max(Math.Abs);
        Assert.Equal(0.25, b / a, 3);
        Assert.InRange(a, 0.3f, 1f);
        var off = new ClickTrain(Rate) { Volume = 0 };
        off.SetPeriod(0.1);
        Assert.All(Render(off, 0.5), v => Assert.Equal(0, v));
    }

    [Fact]
    public void ClickTrain_ADreadfulPeriod_IsSilence()
    {
        foreach (double bad in new[] { double.NaN, 0, -1, double.PositiveInfinity })
        {
            var t = new ClickTrain(Rate) { Volume = 1 };
            t.SetPeriod(bad);
            Assert.All(Render(t, 0.2), v => Assert.Equal(0, v));
        }
    }

    [Fact]
    public void NAudioPulseAudio_DoesNothingWhileDisabled_AndDisposesCleanly()
    {
        var audio = new NAudioPulseAudio();
        Assert.False(audio.Enabled);
        audio.SetPulsePeriod(0.0334);            // disabled: no audio device is opened
        audio.Volume = 0.3f;
        Assert.Equal(0.3f, audio.Volume);
        Assert.Null(audio.Problem);
        audio.Dispose();
        audio.Dispose();
        audio.SetPulsePeriod(1);                 // after dispose: ignored
    }

    // ---- the Close-up asks for sound exactly when the trace says pulses arrive ----

    private static (CloseUpViewModel Cu, FakePulseAudio Audio, MainViewModel Main) CloseUpFor(string preset, bool beamToward = true)
    {
        var main = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        main.Lab.SelectedPreset = PresetCatalog.Get(preset);
        main.Lab.BeamTowardEarth = beamToward;
        var fake = new FakePulseAudio();
        return (new CloseUpViewModel(main, () => fake), fake, main);
    }

    private static void At(MainViewModel main, double sourceTimeS) => main.Timeline.ObserverTimeS = sourceTimeS * main.CurrentObserver.TimeDilation;

    [Fact]
    public void Sound_OffByDefault_AndSilentWhenHidden()
    {
        var (cu, audio, main) = CloseUpFor("sn1054");
        Assert.False(cu.IsSoundOn);
        At(main, CrabAgeS);
        main.Timeline.IsPlaying = true;
        Assert.Null(audio.LastPeriod);                       // off: silent even though pulses arrive and the timeline plays
        Assert.False(audio.Enabled);

        cu.IsSoundOn = true;
        Assert.True(audio.Enabled);
        Assert.Equal(PulseSchedule.CloseUpPeriodS(main.CurrentOutcome, cu.SourceTimeS), audio.LastPeriod);
        Assert.Equal(PulseSchedule.TraceText(main.CurrentOutcome, main.CurrentObserver, cu.SourceTimeS), cu.TraceText);

        At(main, 5 * Year);                                  // scrub back into the debris: Task 19, the Close-up's sound still plays...
        Assert.NotNull(audio.LastPeriod);
        Assert.Equal(PulseSchedule.HiddenText, cu.TraceText); // ...while the trace says Earth receives nothing yet
        cu.Dispose();
    }

    [Fact]
    public void Sound_FollowsThePeriodAsItSpinsDown()
    {
        var (cu, audio, main) = CloseUpFor("sn1054");
        cu.IsSoundOn = true;
        main.Timeline.IsPlaying = true;
        At(main, 20 * Year);
        double early = audio.LastPeriod!.Value;
        At(main, 900 * Year);
        Assert.True(audio.LastPeriod > early);               // P(t) grows
        cu.Dispose();
    }

    [Fact]
    public void Sound_PlaysEvenWhenTheBeamMissesEarth_TheTraceSaysSo()   // Task 19: it was silent
    {
        var (cu, audio, main) = CloseUpFor("sn1054", beamToward: false);
        cu.IsSoundOn = true;
        main.Timeline.IsPlaying = true;
        At(main, CrabAgeS);
        Assert.NotNull(audio.LastPeriod);
        Assert.Equal(PulseSchedule.MissesText, cu.TraceText);
        Assert.False(cu.PulsesShown);
        cu.Dispose();
    }

    /// <summary>
    /// Review Focus 3, revised by Task 15 (the owner could not hear the sound): it now plays while Earth receives the pulses whether the
    /// timeline is paused or playing, as the pulse trace keeps running; it still stops when switched off and when the window closes.
    /// </summary>
    [Fact]
    public void Sound_PlaysPausedOrPlaying_AndStopsWhenSwitchedOff_AndOnWindowClose()
    {
        var (cu, audio, main) = CloseUpFor("sn1054");
        cu.IsSoundOn = true;
        At(main, CrabAgeS);
        Assert.NotNull(audio.LastPeriod);                                 // paused: the pulses still arrive
        main.Timeline.IsPlaying = true;
        Assert.NotNull(audio.LastPeriod);
        main.Timeline.IsPlaying = false;
        Assert.NotNull(audio.LastPeriod);
        cu.IsSoundOn = false;
        Assert.Null(audio.LastPeriod);
        cu.IsSoundOn = true;
        Assert.NotNull(audio.LastPeriod);

        cu.Dispose();                                                     // the window closing
        Assert.Null(audio.LastPeriod);
        Assert.True(audio.Disposed);
        main.Timeline.IsPlaying = false;
        main.Timeline.IsPlaying = true;
        Assert.Null(audio.LastPeriod);                                    // a closed window makes no sound
    }

    [Fact]
    public void Sound_StopsWhenTheOutcomeChangesToNoPulsar()
    {
        var (cu, audio, main) = CloseUpFor("sn1054");
        cu.IsSoundOn = true;
        At(main, CrabAgeS);
        main.Timeline.IsPlaying = true;
        Assert.NotNull(audio.LastPeriod);
        main.Lab.SelectedPreset = PresetCatalog.LoadEmbedded().First(p =>
        {
            var o = TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
            return PulseSchedule.Visibility(o, main.CurrentObserver, 1e10) == PulseVisibility.NotAPulsar;
        });
        Assert.Null(audio.LastPeriod);
        Assert.False(cu.ShowPulseTrace);
        cu.Dispose();
    }

    [Fact]
    public void Speaker_ShowsItsStateAndVolume_OnScreen()
    {
        var (cu, audio, main) = CloseUpFor("sn1054");
        At(main, CrabAgeS);
        Assert.Contains("off", cu.SpeakerState, StringComparison.OrdinalIgnoreCase);
        cu.IsSoundOn = true;
        cu.Volume = 0.4f;
        Assert.Equal(0.4f, audio.Volume);
        Assert.Contains("40%", cu.SpeakerState);
        Assert.Contains("on", cu.SpeakerState, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("clicking 30 a second", cu.SpeakerState);   // Task 15: what it plays, paused or playing
        main.Timeline.IsPlaying = true;
        Assert.Contains("clicking 30 a second", cu.SpeakerState);
        At(main, 5);   // Task 19: before the beams form (it was 5 years, inside the debris)
        Assert.Contains("beams form", cu.SpeakerState, StringComparison.OrdinalIgnoreCase);
        cu.Dispose();
    }

    [Fact]
    public void Speaker_SaysSoWhenThereIsNoAudioDevice()
    {
        var (cu, audio, main) = CloseUpFor("sn1054");
        audio.Problem = "no audio device";
        cu.IsSoundOn = true;
        Assert.Contains("no audio device", cu.SpeakerState);
        cu.Dispose();
    }

    [Fact]
    public void NoAudioFactory_MeansNoSound_ButTheTraceStillWorks()
    {
        var main = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        main.Lab.SelectedPreset = PresetCatalog.Get("sn1054");
        var cu = new CloseUpViewModel(main);
        At(main, CrabAgeS);
        cu.IsSoundOn = true;
        Assert.StartsWith("To Earth:", cu.TraceText);
        cu.Dispose();
    }

    [Fact]
    public void TheTrace_ShowsOnlyForAPulsar()
    {
        var (cu, _, main) = CloseUpFor("sn1054");
        At(main, CrabAgeS);
        Assert.True(cu.ShowPulseTrace);
        Assert.True(cu.PulsesShown);
        At(main, 5 * Year);
        Assert.True(cu.ShowPulseTrace);
        Assert.False(cu.PulsesShown);
        cu.Dispose();
    }

    [Fact]
    public void WindowService_StopsTheSoundWhenTheCloseUpCloses()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                InstrumentsWindowTests.EnsureApplication();
                var fake = new FakePulseAudio();
                var service = new WindowService(closeUpFactory: _ => OffScreen(new Window()), audioFactory: () => fake);
                var vm = new MainViewModel(TestOutcomes.Engine, service);
                var owner = OffScreen(new Window { DataContext = vm });
                owner.Show();
                service.Attach(owner);
                vm.ShowCloseUpCommand.Execute(null);
                var cu = Assert.IsType<CloseUpViewModel>(service.CloseUp!.DataContext);
                cu.IsSoundOn = true;
                Assert.False(fake.Disposed);
                service.CloseUp.Close();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                Assert.True(fake.Disposed);
                Assert.Null(fake.LastPeriod);
                owner.Close();
            }
            catch (Exception e) { error = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(error);
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
}

/// <summary>Task 8 fix round 1: a failing or unplugged device, and a clean stop.</summary>
public class PulseAudioFixTests
{
    private sealed class FakePlayer : NAudio.Wave.IWavePlayer
    {
        public NAudio.Wave.PlaybackState PlaybackState { get; private set; } = NAudio.Wave.PlaybackState.Stopped;
        public float Volume { get; set; } = 1;
        public NAudio.Wave.WaveFormat OutputWaveFormat => NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
        public event EventHandler<NAudio.Wave.StoppedEventArgs>? PlaybackStopped;
        public int Stops, Disposes;
        public void Init(NAudio.Wave.IWaveProvider waveProvider) { }
        public void Play() => PlaybackState = NAudio.Wave.PlaybackState.Playing;
        public void Pause() => PlaybackState = NAudio.Wave.PlaybackState.Paused;
        public void Stop() { Stops++; PlaybackState = NAudio.Wave.PlaybackState.Stopped; }
        public void Dispose() => Disposes++;
        public void Fail(Exception e) => PlaybackStopped?.Invoke(this, new NAudio.Wave.StoppedEventArgs(e));
    }

    [Fact]
    public void AFailedOpen_IsLatched_NotRetriedEveryTick_UntilToggled()
    {
        int made = 0;
        var audio = new NAudioPulseAudio(() => { made++; throw new InvalidOperationException("no device"); });
        audio.Enabled = true;
        for (int i = 0; i < 100; i++) audio.SetPulsePeriod(0.03 + i * 1e-4);
        Assert.Equal(1, made);
        Assert.NotNull(audio.Problem);
        audio.Enabled = false;
        audio.Enabled = true;
        audio.SetPulsePeriod(0.03);
        Assert.Equal(2, made);                    // a toggle retries once
        audio.Dispose();
    }

    [Fact]
    public void ADeviceLostMidPlay_ShowsAProblem_AndIsNotReopenedEveryTick()
    {
        var players = new List<FakePlayer>();
        var audio = new NAudioPulseAudio(() => { var p = new FakePlayer(); players.Add(p); return p; });
        audio.Enabled = true;
        audio.SetPulsePeriod(0.03);
        Assert.Single(players);
        Assert.Null(audio.Problem);
        players[0].Fail(new InvalidOperationException("unplugged"));
        Assert.NotNull(audio.Problem);
        for (int i = 0; i < 50; i++) audio.SetPulsePeriod(0.03 + i * 1e-4);
        Assert.Single(players);
        audio.Dispose();
    }

    [Fact]
    public void Disabling_StopsAndReleasesTheDevice_SoNothingStaleSoundsOnReEnable()
    {
        var players = new List<FakePlayer>();
        var audio = new NAudioPulseAudio(() => { var p = new FakePlayer(); players.Add(p); return p; });
        audio.Enabled = true;
        audio.SetPulsePeriod(0.5);
        audio.Enabled = false;
        Assert.Equal(1, players[0].Stops);
        Assert.Equal(1, players[0].Disposes);
        audio.Enabled = true;
        audio.SetPulsePeriod(0.5);
        Assert.Equal(2, players.Count);           // a fresh device with empty buffers
        audio.Dispose();
    }

    [Fact]
    public void ClickTrain_ANewPeriodDuringAClick_DoesNotCutIt()
    {
        var t = new ClickTrain(44100) { Volume = 1 };
        t.SetPeriod(0.5);
        var a = new float[40];
        t.Fill(a, 0, 40);                          // 40 samples into the 88-sample click
        t.SetPeriod(null);
        t.SetPeriod(0.5);                          // resume mid-click: it carries on, not restarts
        var b = new float[48];
        t.Fill(b, 0, 48);
        var whole = new ClickTrain(44100) { Volume = 1 };
        whole.SetPeriod(0.5);
        var ref88 = new float[88];
        whole.Fill(ref88, 0, 88);
        Assert.Equal(ref88.Skip(40), b);
    }
}
