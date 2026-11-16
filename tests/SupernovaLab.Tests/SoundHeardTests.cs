using SupernovaLab.App.Services;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Presets;

namespace SupernovaLab.Tests;

/// <summary>
/// Task 16 (the owner's second look: "When I click the speaker icon or move the sound slidebar, it remains silent"). The sound clicks only
/// while a pulsar's pulses reach Earth (from about 10 years), so switching it on at t = 0, or moving the slider, gave nothing to hear and
/// no way to tell a working speaker from a silent one. Now both play a short test tone, and the click is fuller (6 ms, three cycles).
/// </summary>
public class SoundHeardTests
{
    private const int Rate = 44100;

    private static float[] Render(ClickTrain t, double seconds)
    {
        var buf = new float[(int)(seconds * Rate)];
        t.Fill(buf, 0, buf.Length);
        return buf;
    }

    [Fact]
    public void TheTestTone_SoundsWithoutAPulsar_ForItsLength_ThenStops()
    {
        var t = new ClickTrain(Rate) { Volume = 1 };
        Assert.All(Render(t, 0.1), v => Assert.Equal(0, v));   // no period, no tone: silence
        t.StartTestTone();
        Assert.True(t.TestToneActive);
        int length = (int)Math.Round(ClickTrain.TestToneS * Rate);
        var x = Render(t, ClickTrain.TestToneS + 0.1);
        Assert.InRange(x.Take(length).Max(Math.Abs), 0.6f, 1f);          // clearly loud, never clipping
        Assert.All(x.Skip(length + 1), v => Assert.Equal(0, v));        // then silence again
        Assert.False(t.TestToneActive);
        int crossings = 0;
        for (int i = 1; i < length; i++) if (Math.Sign(x[i - 1]) * Math.Sign(x[i]) < 0) crossings++;
        Assert.InRange(crossings, 2 * ClickTrain.TestToneHz * ClickTrain.TestToneS - 3, 2 * ClickTrain.TestToneHz * ClickTrain.TestToneS + 3);
        Assert.InRange(ClickTrain.TestToneHz, 300, 500);   // low: high pitches are usually the hardest to hear
    }

    [Fact]
    public void TheTestTone_FollowsTheVolume_AndIsNotRestartedWhileItPlays()
    {
        var quiet = new ClickTrain(Rate) { Volume = 0.25f };
        var loud = new ClickTrain(Rate) { Volume = 1 };
        quiet.StartTestTone();
        loud.StartTestTone();
        Assert.Equal(0.25, Render(quiet, 0.2).Max(Math.Abs) / Render(loud, 0.2).Max(Math.Abs), 3);

        var t = new ClickTrain(Rate) { Volume = 1 };
        t.StartTestTone();
        Render(t, ClickTrain.TestToneS / 2);
        t.StartTestTone();   // a slider drag asks again and again: the tone runs on, not from its fade-in each time
        var rest = Render(t, ClickTrain.TestToneS);
        int half = (int)(ClickTrain.TestToneS / 2 * Rate);
        Assert.True(rest.Take(half - 50).Max(Math.Abs) > 0.3f);              // it ran on...
        Assert.All(rest.Skip(half + 2), v => Assert.Equal(0, v));           // ...and ended when the first one would have
    }

    [Fact]
    public void TheTestTone_MixesWithTheClicks_WithoutClipping()
    {
        var t = new ClickTrain(Rate) { Volume = 1 };
        t.SetPeriod(0.0334);
        t.StartTestTone();
        Assert.All(Render(t, 0.5), v => Assert.InRange(v, -1f, 1f));
    }

    [Fact]
    public void TheClick_IsSixMilliseconds_ThreeCyclesOf500Hz_WithNoNetPush()
    {
        Assert.Equal(0.006, ClickTrain.ClickS, 9);
        var t = new ClickTrain(Rate) { Volume = 1 };
        t.SetPeriod(0.5);
        var x = Render(t, 0.1);
        int length = (int)Math.Round(ClickTrain.ClickS * Rate);
        int crossings = 0;
        for (int i = 2; i < length - 1; i++) if (Math.Sign(x[i - 1]) * Math.Sign(x[i]) < 0) crossings++;
        Assert.Equal(5, crossings);                                     // three cycles
        Assert.InRange(x.Take(length).Max(Math.Abs), 0.9f, 1f);
        Assert.Equal(0, x.Take(length).Sum(v => (double)v), 2);         // no thump
        Assert.All(x.Skip(length + 1), v => Assert.Equal(0, v));
    }

    [Fact]
    public void TheAudio_OpensTheDeviceForATestTone_EvenWithNoPulses()
    {
        var player = new PullingPlayer();
        var audio = new NAudioPulseAudio(() => player);
        audio.PlayTestTone();                       // switched off: nothing opens
        Assert.Equal(0, audio.DevicesOpened);
        audio.Enabled = true;
        audio.SetPulsePeriod(null);                 // no pulses reach Earth
        audio.PlayTestTone();
        Assert.Equal(1, audio.DevicesOpened);
        Assert.Equal(1, audio.TestTonesPlayed);
        Assert.True(player.Pull((int)(NAudioPulseAudio.LeadInS * Rate) + Rate / 10).Max(Math.Abs) > 0.3f);   // the device gets sound
        Assert.Contains("test tones 1", audio.DiagnosticText);
        audio.Dispose();
    }

    // ---- Task 16b (the owner: a Bluetooth speaker takes a fraction of a second to connect, and swallows what comes first) ----

    [Fact]
    public void HoldSilence_DelaysEverything_WithoutLosingIt()
    {
        var t = new ClickTrain(Rate) { Volume = 1 };
        t.SetPeriod(0.5);
        t.StartTestTone();
        t.HoldSilence(0.25);
        int hold = (int)Math.Round(0.25 * Rate);
        var x = Render(t, 0.25 + ClickTrain.TestToneS + 0.05);
        Assert.All(x.Take(hold), v => Assert.Equal(0, v));                       // a quarter second of silence first...
        Assert.True(x.Skip(hold).Take(Rate / 20).Max(Math.Abs) > 0.3f);          // ...then the tone and the first click, whole
        Assert.Equal(1, t.ClicksWritten);
    }

    [Fact]
    public void OpeningTheDevice_StartsWithAQuarterSecondOfSilence()
    {
        Assert.Equal(0.25, NAudioPulseAudio.LeadInS);
        var player = new PullingPlayer();
        var audio = new NAudioPulseAudio(() => player);
        audio.Enabled = true;
        audio.PlayTestTone();
        int hold = (int)Math.Round(NAudioPulseAudio.LeadInS * Rate);
        var first = player.Pull(hold);
        Assert.All(first, v => Assert.Equal(0, v));
        Assert.True(player.Pull(Rate / 10).Max(Math.Abs) > 0.3f);
        // Once open, the device stays open (playing silence) while the sound is on, so a later tone or click is not held back again.
        audio.SetPulsePeriod(0.5);
        Assert.Equal(1, audio.DevicesOpened);
        audio.Dispose();
    }

    [Fact]
    public void TheSoundPanel_SaysTheClicksPlayOnlyWhenDetectedFromEarth()
        => Assert.Contains("detected from Earth", SupernovaLab.Core.Text.Glossary.CloseUpSoundRule);

    private static (CloseUpViewModel Cu, FakePulseAudio Audio, MainViewModel Main) CloseUp(string preset)
    {
        var main = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        main.Lab.SelectedPreset = PresetCatalog.Get(preset);
        var fake = new FakePulseAudio();
        return (new CloseUpViewModel(main, () => fake), fake, main);
    }

    [Fact]
    public void SwitchingTheSoundOn_PlaysATestTone_AtTimeZero_WhenNoPulsesArrive()
    {
        var (cu, audio, main) = CloseUp("sn1054");
        Assert.True(main.Timeline.IsAtStart);
        cu.IsSoundOn = true;
        Assert.Null(audio.LastPeriod);          // no pulses at t = 0...
        Assert.Equal(1, audio.TestTones);       // ...but the speaker is heard
        Assert.Contains("test tone", SupernovaLab.Core.Text.Glossary.CloseUpSound);   // the switch's tooltip says so
        cu.IsSoundOn = false;
        Assert.Equal(1, audio.TestTones);
        cu.Dispose();
    }

    [Fact]
    public void MovingTheVolumeSlider_PlaysATestTone_OnlyWhileTheSoundIsOn()
    {
        var (cu, audio, _) = CloseUp("sn1998bw");   // a black hole: never any clicks
        cu.Volume = 0.5f;
        Assert.Equal(0, audio.TestTones);
        cu.IsSoundOn = true;
        cu.Volume = 0.9f;
        Assert.Equal(2, audio.TestTones);           // switched on, then the slider
        cu.Dispose();
    }

    private sealed class PullingPlayer : NAudio.Wave.IWavePlayer
    {
        private NAudio.Wave.ISampleProvider? _source;
        public NAudio.Wave.PlaybackState PlaybackState { get; private set; }
        public float Volume { get; set; } = 1;
        public NAudio.Wave.WaveFormat OutputWaveFormat => _source!.WaveFormat;
        public event EventHandler<NAudio.Wave.StoppedEventArgs>? PlaybackStopped { add { } remove { } }
        public void Init(NAudio.Wave.IWaveProvider waveProvider) => _source = NAudio.Wave.WaveExtensionMethods.ToSampleProvider(waveProvider);
        public void Play() => PlaybackState = NAudio.Wave.PlaybackState.Playing;
        public void Pause() => PlaybackState = NAudio.Wave.PlaybackState.Paused;
        public void Stop() => PlaybackState = NAudio.Wave.PlaybackState.Stopped;
        public void Dispose() { }
        public float[] Pull(int n)
        {
            var buf = new float[n];
            _source!.Read(buf, 0, n);
            return buf;
        }
    }
}
