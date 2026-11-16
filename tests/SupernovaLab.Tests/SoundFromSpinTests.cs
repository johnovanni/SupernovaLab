using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>
/// Task 19 (the owner, 2026-11-16: "the closeup window is much closer and since the sound is coming from that window, it makes sense to
/// play it right after the pulsar starts spinning"). The sound no longer follows what Earth receives: it clicks once per turn, at the
/// star's own spin period, from when its beams form (about 30 s after bounce), whatever the debris or Earth's direction.
/// </summary>
public class SoundFromSpinTests
{
    private const double Year = PhysicalConstants.Year;

    private static (CloseUpViewModel Cu, FakePulseAudio Audio, MainViewModel Main) CloseUp(string preset, bool beamToward = true)
    {
        var main = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        main.Lab.SelectedPreset = PresetCatalog.Get(preset);
        main.Lab.BeamTowardEarth = beamToward;
        var fake = new FakePulseAudio();
        return (new CloseUpViewModel(main, () => fake), fake, main);
    }

    private static void At(MainViewModel main, double sourceTimeS) => main.Timeline.ObserverTimeS = sourceTimeS * main.CurrentObserver.TimeDilation;

    [Fact]
    public void ThePeriod_IsTheStarsOwnSpin_FromWhenTheBeamsForm()
    {
        var o = TestOutcomes.Engine.Compute(PresetCatalog.Get("sn1054").Inputs, PresetCatalog.Get("sn1054").Pins);
        double forms = Magnetosphere.MagnetosphereFormsS(o);
        Assert.Equal(30, forms);
        Assert.True(double.IsNaN(PulseSchedule.CloseUpPeriodS(o, forms * 0.99)));   // before the beams: silence
        Assert.Equal(PulsarModel.Spin.Of(o).PeriodS(forms), PulseSchedule.CloseUpPeriodS(o, forms));
        Assert.Equal(PulsarModel.Spin.Of(o).PeriodS(5 * Year), PulseSchedule.CloseUpPeriodS(o, 5 * Year));   // inside the debris: it plays
        var bh = TestOutcomes.Engine.Compute(PresetCatalog.Get("sn1998bw").Inputs, PresetCatalog.Get("sn1998bw").Pins);
        Assert.True(double.IsNaN(PulseSchedule.CloseUpPeriodS(bh, 5 * Year)));       // no pulsar: silence
    }

    [Fact]
    public void TheSound_PlaysInsideTheDebris_AndWithTheBeamAwayFromEarth()
    {
        foreach (bool toward in new[] { true, false })
        {
            var (cu, audio, main) = CloseUp("sn1054", toward);
            cu.IsSoundOn = true;
            At(main, 5 * Year);                       // Earth receives nothing yet (hidden by debris)
            Assert.Equal(PulseSchedule.CloseUpPeriodS(main.CurrentOutcome, cu.SourceTimeS), audio.LastPeriod!.Value, 12);
            Assert.Contains("one per turn", cu.SpeakerState);
            Assert.False(cu.PulsesShown);             // the radio trace still says what Earth receives
            cu.Dispose();
        }
    }

    [Fact]
    public void BeforeTheBeamsForm_ItIsSilent_AndSaysWhen()
    {
        var (cu, audio, main) = CloseUp("sn1054");
        cu.IsSoundOn = true;
        At(main, 5);
        Assert.Null(audio.LastPeriod);
        Assert.Contains("silent until the beams form, about 30 s after bounce (approximate)", cu.SpeakerState);
        Assert.Contains("10 to 100 s", cu.SpeakerStateToolTip);
        At(main, 31);
        Assert.NotNull(audio.LastPeriod);
        Assert.Null(cu.SpeakerStateToolTip);
        cu.Dispose();
    }

    [Fact]
    public void TheRule_OnScreen_SaysWhenTheClicksPlay()
    {
        Assert.Contains("once the pulsar's beams form", SupernovaLab.Core.Text.Glossary.CloseUpSoundRule);
        Assert.DoesNotContain("Earth", SupernovaLab.Core.Text.Glossary.CloseUpSoundRule);
    }

    [Fact]
    public void TheTrace_SpeaksOfEarth_NotOfTheSound()
        => Assert.StartsWith("To Earth: 30 pulses a second", PulseSchedule.TraceText(
            TestOutcomes.Engine.Compute(PresetCatalog.Get("sn1054").Inputs, PresetCatalog.Get("sn1054").Pins),
            SupernovaLab.Core.Observer.ObserverSetup.Default, 2.9568e10));
}
