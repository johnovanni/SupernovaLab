using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>Project 3, Task 14: the small fixes carried from Tasks 2, 6 and 8 (task-14-carried.md).</summary>
public class Project3CarriedFixTests
{
    private const double Year = PhysicalConstants.Year;

    private static MainViewModel Preset(string id, double observerTimeS)
    {
        var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        vm.Lab.SelectedPreset = PresetCatalog.Get(id);
        vm.Timeline.ObserverTimeS = observerTimeS;
        return vm;
    }

    // ---- Task 2: each "Expected" badge's tooltip gives §C10's reason ----

    [Fact]
    public void ExpectedBadge_ToolTipGivesTheNeverObservedReason()
    {
        var vm = Preset("betelgeuse", 0.3);
        var gw = vm.Panels.OfType<GravitationalWavePanelViewModel>().Single();
        Assert.Equal("Expected", gw.DetectionText);
        string reason = NeverObserved.Reason("gw", null, vm.CurrentOutcome)!;
        Assert.Contains("No gravitational waves have been detected from any supernova", reason);
        Assert.Contains(reason, gw.DetectionToolTip);
        Assert.Contains("compact.md §C10", gw.DetectionToolTip);
        Assert.Contains(reason, gw.StripToolTip);
    }

    [Fact]
    public void ExpectedBadge_OverWords_AlsoCarryTheReason()
    {
        var vm = Preset("betelgeuse", 30 * 86400);
        var gw = vm.Panels.OfType<GravitationalWavePanelViewModel>().Single();
        Assert.Equal("Over: expected to be detected", gw.DetectionText);
        Assert.Contains(NeverObserved.Reason("gw", null, vm.CurrentOutcome)!, gw.DetectionToolTip);
    }

    [Fact]
    public void DetectedBadge_HasNoExpectedParagraph()
    {
        var nu = Preset("sn1987a", 5).Panels.OfType<NeutrinoPanelViewModel>().Single();
        Assert.Equal("Detected", nu.DetectionText);
        Assert.DoesNotContain("§C10", nu.DetectionToolTip);
    }

    [Fact]
    public void NeverObservedReason_ExistsExactlyWhenTheRuleApplies()
    {
        foreach (var row in ExpectedWordingTests.AuditTriples())
        {
            var (model, channel, o, applies) = ((string)row[0], (string)row[1], (Core.Outcomes.Outcome)row[2], (bool)row[3]);
            string? reason = NeverObserved.Reason(model, channel, o);
            Assert.Equal(applies, reason is not null);
            if (reason is not null) Assert.EndsWith(".", reason);
        }
        var bh = TestOutcomes.Custom(Core.Outcomes.SupernovaType.IIP, 1e51, 10, 0.07, 3e13, TestOutcomes.BlackHole(8, 3));
        Assert.Contains("left a neutron star", NeverObserved.Reason("neutrino", "lum_anue", bh));
        Assert.Contains("electron neutrinos", NeverObserved.Reason("neutrino", "rate@dune", TestOutcomes.Engine.Compute(Core.Progenitors.LabInputs.Default)));
    }

    // ---- Task 6: Cas A's "no pulses seen" note only once beams exist ----

    [Fact]
    public void CasA_NoPulsesSeenNote_OnlyOnceTheBeamsExist()
    {
        var p = PresetCatalog.Get("casa");
        var scene = new CloseUpScene(TestOutcomes.Engine.Compute(p.Inputs, p.Pins), p.Observer, 1);
        var proto = scene.At(10);   // a proto-neutron star: the magnetosphere forms at 30 s
        Assert.Equal(CloseUpEra.NeutronStar, proto.Era);
        Assert.False(proto.ShowBeams);
        Assert.DoesNotContain(CloseUpScene.NoPulsesSeenNote, proto.Notes);
        Assert.StartsWith("Proto-neutron star", proto.Title);
        var star = scene.At(951 * Year);
        Assert.True(star.ShowBeams);
        Assert.Equal(CloseUpScene.NoPulsesSeenNote, star.Notes[0]);
    }

    // ---- Task 8: the speed note never contradicts "Shown at real speed" under redshift ----

    [Fact]
    public void SpeedNote_UnderRedshift_SaysTheReceivedSpeed()
    {
        // A star turning once in 1.9 s at z = 0.1: its pulses arrive once in 2.09 s, so the label is "Shown at real speed", and the note
        // explains the received clock rather than claiming a 2 s floor slowed it.
        Assert.Equal("Shown at real speed", Magnetosphere.SlowdownLabel(1.9, 1.1));
        string note = CloseUpScene.SpeedNote(1.9, 1.1);
        Assert.StartsWith("Shown at about real speed as received on Earth", note);
        Assert.Contains(UnitFormat.Time(1.9), note);
        Assert.Contains(UnitFormat.Time(1.9 * 1.1), note);
        Assert.Contains("never turns faster than once in 2 s", note);

        // The Crab (slowed): the label and the floor, as before.
        Assert.Equal(Magnetosphere.SlowdownLabel(0.0334, 1) + ": the drawn spin never turns faster than once in 2 s, so no beam crosses Earth " +
                     "more than once a second", CloseUpScene.SpeedNote(0.0334, 1));
        // A slow star (no floor): just the label.
        Assert.Equal("Shown at real speed", CloseUpScene.SpeedNote(5, 1));
        Assert.Equal(Magnetosphere.SlowdownLabel(1.9, 1), CloseUpScene.SpeedNote(1.9, 1).Split(':')[0]);
    }

    [Fact]
    public void SpeedNote_IsTheNeutronStarsNote()
    {
        var p = PresetCatalog.Get("sn2015bn");   // z = 0.114
        var o = TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
        var st = new CloseUpScene(o, p.Observer, 1).At(100 * Year);
        Assert.Contains(CloseUpScene.SpeedNote(st.NeutronStar!.PeriodS, p.Observer.TimeDilation), st.Notes);
        Assert.Equal(Magnetosphere.SlowdownLabel(st.NeutronStar.PeriodS, p.Observer.TimeDilation), st.SpeedLabel);
    }
}
