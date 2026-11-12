using System.IO;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Text;

namespace SupernovaLab.Tests;

/// <summary>Final review fixes in the view models: I5's drawn Δm15(B), M3's map note, M10's filtered sources, M11's note.</summary>
public class FinalReviewAppTests
{
    private static MainViewModel NewViewModel() => new(TestOutcomes.Engine, new FakeWindowService());

    private static MainViewModel WithPreset(string id)
    {
        var vm = NewViewModel();
        vm.Lab.SelectedPreset = PresetCatalog.Get(id);
        return vm;
    }

    [Fact]
    public void TypeIa_DeclineRate_IsReadOffTheDrawnCurve_NotTheEngine()
    {
        // Ruling R53 (I5): Δm15(B) is a drawn-curve row with the drawn curve's source, and the peak B row says what it is.
        var vm = WithPreset("sn2011fe");
        var row = vm.Outcome.CurveRows.Single(r => r.Label.StartsWith("Decline rate Δm15(B)", StringComparison.Ordinal));
        Assert.Equal(new LightCurveModel().DeclineRateDm15B(vm.CurrentOutcome), row.Value, 12);
        Assert.Contains("light curve drawn", row.Citation);
        Assert.DoesNotContain(vm.Outcome.DetailRows, r => r.Label.Contains("Δm15"));
        var b = vm.Outcome.DetailRows.Single(r => r.Label.StartsWith("Peak absolute B magnitude", StringComparison.Ordinal));
        Assert.Contains("estimated from the engine's peak luminosity", b.Label);
        Assert.Contains("does not shape the light curve", b.Label);
        Assert.DoesNotContain("(shapes the light curve)", b.Label);
        // A core-collapse supernova has no Δm15 row.
        Assert.DoesNotContain(NewViewModel().Outcome.CurveRows, r => r.Label.Contains("Δm15"));
    }

    [Fact]
    public void CurveSource_ListsOnlyTheSourcesThatApplyToTheOutcome()
    {
        // Review M10. The default II-P: plateau yes; magnetar and interaction no.
        string iip = NewViewModel().Outcome.CurveSourceText;
        Assert.Contains("Goldberg, Bildsten & Paxton 2019", iip);
        Assert.DoesNotContain("Kasen & Bildsten 2010", iip);
        Assert.DoesNotContain("Circumstellar interaction", iip);
        // SN 1998bw (Ic-BL): no plateau, no magnetar, no interaction.
        string icbl = WithPreset("sn1998bw").Outcome.CurveSourceText;
        Assert.DoesNotContain("Goldberg, Bildsten & Paxton 2019", icbl);
        Assert.DoesNotContain("Kasen & Bildsten 2010", icbl);
        Assert.Contains("Arnett 1982", icbl);
        // SN 2015bn: the magnetar's source.
        Assert.Contains("Kasen & Bildsten 2010", WithPreset("sn2015bn").Outcome.CurveSourceText);
    }

    [Fact]
    public void DefaultIIP_IsNotedAsBrightBecauseTheStarIsLarge()
    {
        // Review M11: the default 16 M☉ red supergiant (886 R☉) peaks at M_V −17.87 in the final review's probe, brighter
        // than Richardson et al. 2014's II-P mean −16.80 − 0.97 = −17.77.
        var vm = NewViewModel();
        Assert.Equal(SupernovaType.IIP, vm.CurrentOutcome.Type);
        Assert.Contains(vm.Outcome.Notes, n => n.StartsWith("Bright for a Type II-P", StringComparison.Ordinal) &&
                                                n.Contains("size", StringComparison.Ordinal));
        // SN 1987A (a compact blue supergiant, II-P in the lab) is faint: no such note.
        Assert.DoesNotContain(WithPreset("sn1987a").Outcome.Notes, n => n.StartsWith("Bright for a Type II-P", StringComparison.Ordinal));
    }

    [Fact]
    public void TimelineMarkers_FromAThrowingModel_AreEmpty_NotACrash()
    {
        // Review M1: EventMarkers.For runs six models outside any panel; MainViewModel calls it through this guard.
        Assert.Empty(EventMarkers.Guarded(() => throw new InvalidOperationException("model broke")));
        var vm = NewViewModel();
        var markers = EventMarkers.Guarded(() => EventMarkers.For(vm.CurrentOutcome, vm.CurrentObserver));
        Assert.NotEmpty(markers);
    }

    [Fact]
    public void ErrorLog_AppendsTheException_AndReturnsItsPath()
    {
        // Review M1: the normal-mode last-resort handler's log.
        string dir = Path.Combine(Path.GetTempPath(), "SupernovaLab.tests", Guid.NewGuid().ToString("N"));
        try
        {
            string path = SupernovaLab.App.Services.ErrorLog.Write(new InvalidOperationException("boom"), dir);
            Assert.Equal(Path.Combine(dir, "error.log"), path);
            Assert.Contains("boom", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void OutcomeMap_NotesTheCoreStepAt30Msun()
    {
        // Review M3: the S16 → W19 switch at 30 M☉ moves the CO core abruptly, which can draw an island on the strip. The
        // map's legend and every segment's tooltip say so.
        var vm = NewViewModel();
        Assert.Contains("30 M☉", vm.Outcome.MapNote);
        Assert.Contains("not a real", vm.Outcome.MapNote);
        Assert.All(vm.Outcome.MapSegments, s => Assert.Contains(vm.Outcome.MapNote, s.ToolTip));
    }
}
