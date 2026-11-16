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
    public void ClickTrain_AClickIsTwoMillisecondsLong_UnderARaisedCosineEnvelope()
    {
        var t = new ClickTrain(Rate) { Volume = 1 };
        t.SetPeriod(0.5);
        var x = Render(t, 0.4);
        var onset = Onsets(x)[0];
        int length = (int)Math.Round(0.002 * Rate);
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
        Assert.Equal(PulseSchedule.ReceivedPeriodS(main.CurrentOutcome, main.CurrentObserver, cu.SourceTimeS), audio.LastPeriod);
        Assert.Equal(PulseSchedule.TraceText(main.CurrentOutcome, main.CurrentObserver, cu.SourceTimeS), cu.TraceText);

        At(main, 5 * Year);                                  // scrub back into the debris
        Assert.Null(audio.LastPeriod);
        Assert.Equal(PulseSchedule.HiddenText, cu.TraceText);
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
    public void Sound_SilentWhenTheBeamMissesEarth_WithTheReasonOnScreen()
    {
        var (cu, audio, main) = CloseUpFor("sn1054", beamToward: false);
        cu.IsSoundOn = true;
        main.Timeline.IsPlaying = true;
        At(main, CrabAgeS);
        Assert.Null(audio.LastPeriod);
        Assert.Equal(PulseSchedule.MissesText, cu.TraceText);
        Assert.False(cu.PulsesShown);
        cu.Dispose();
    }

    [Fact]
    public void Sound_StopsOnPause_AndOnWindowClose()                     // Review Focus 3
    {
        var (cu, audio, main) = CloseUpFor("sn1054");
        cu.IsSoundOn = true;
        At(main, CrabAgeS);
        Assert.Null(audio.LastPeriod);                                    // sound only while the timeline plays
        main.Timeline.IsPlaying = true;
        Assert.NotNull(audio.LastPeriod);
        main.Timeline.IsPlaying = false;
        Assert.Null(audio.LastPeriod);
        main.Timeline.IsPlaying = true;
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
        Assert.Contains("paused", cu.SpeakerState, StringComparison.OrdinalIgnoreCase);   // the reason it is silent
        main.Timeline.IsPlaying = true;
        Assert.DoesNotContain("paused", cu.SpeakerState, StringComparison.OrdinalIgnoreCase);
        At(main, 5 * Year);
        Assert.Contains("debris", cu.SpeakerState, StringComparison.OrdinalIgnoreCase);
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
        Assert.StartsWith("Sound:", cu.TraceText);
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
