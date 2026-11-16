using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>
/// Task 22 (the owner: "I still don't see the neutron star cutaway view", then "Let's default to Cutaway on in the closeup view. Yes, add
/// info"): the Close-up's cutaway is on from the start, and while it cannot show, the view says why and when it will.
/// </summary>
public class CutawayDefaultTests
{
    private static (CloseUpViewModel Cu, MainViewModel Main) CloseUp(string preset)
    {
        var main = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        main.Lab.SelectedPreset = PresetCatalog.Get(preset);
        return (new CloseUpViewModel(main, () => new FakePulseAudio()), main);
    }

    private static void At(MainViewModel main, double sourceTimeS) => main.Timeline.ObserverTimeS = sourceTimeS * main.CurrentObserver.TimeDilation;

    [Fact]
    public void TheCutaway_IsOnByDefault()
    {
        var (cu, main) = CloseUp("sn1054");
        Assert.True(cu.IsCutaway);
        At(main, 7 * PhysicalConstants.Day);
        Assert.NotNull(cu.ActiveCutaway);
        Assert.True(cu.ShowStrip);                 // the crust and the drip line, with no switch to find
        Assert.NotNull(cu.DripLabel);
        cu.Dispose();
    }

    [Fact]
    public void InTheFirstSecond_ItSaysWhenTheNeutronStarForms()
    {
        var (cu, main) = CloseUp("sn1054");
        At(main, 0.25);
        Assert.Null(cu.ActiveCutaway);
        Assert.StartsWith("Cutaway: shown once the neutron star forms", cu.CutawayStateLabel);
        cu.Dispose();
    }

    [Fact]
    public void BeforeTheCrust_ItSaysSo()
    {
        var (cu, main) = CloseUp("sn1054");
        At(main, 3600);
        Assert.Equal("Proto-neutron star: hot and liquid, no crust yet", cu.CutawayStateLabel);
        cu.Dispose();
    }

    [Fact]
    public void ForABlackHole_ItSaysThereIsNone()
    {
        var (cu, main) = CloseUp("sn1998bw");
        At(main, 1e7);
        Assert.StartsWith("Cutaway: none for a black hole", cu.CutawayStateLabel);
        cu.Dispose();
    }

    [Fact]
    public void SwitchedOff_ItSaysNothing()
    {
        var (cu, main) = CloseUp("sn1054");
        At(main, 0.25);
        cu.IsCutaway = false;
        Assert.Null(cu.CutawayStateLabel);
        cu.Dispose();
    }
}
