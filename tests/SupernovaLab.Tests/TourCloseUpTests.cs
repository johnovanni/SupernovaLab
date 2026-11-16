using SupernovaLab.App.Tour;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>The guided tour's Close-up stops (Project 3, Task 13; spec §2.4).</summary>
public class TourCloseUpTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromSeconds(1.0 / 60);

    private static Outcome ForPreset(Preset p) => TestOutcomes.Engine.Compute(p.Inputs, p.Pins);

    private static Outcome Magnetar() => TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.07, 3e13,
        TestOutcomes.Pulsar(2.5, 1e15, RemnantKind.Magnetar));

    /// <summary>Every hand-built type, every fate example, and every preset: (name, outcome, observer, preset).</summary>
    public static IEnumerable<object?[]> AllPresetsAndTypes()
    {
        foreach (var row in TourBuilderTests.Outcomes())
            yield return new object?[] { row[0], row[1], ObserverSetup.Default, null };
        foreach (var p in PresetCatalog.LoadEmbedded())
            yield return new object?[] { $"preset {p.Id}", ForPreset(p), p.Observer, p };
    }

    private static bool HasCompactRemnant(Outcome o) => o.Remnant.Kind is RemnantKind.NeutronStar or RemnantKind.Magnetar or RemnantKind.BlackHole;

    [Theory, MemberData(nameof(AllPresetsAndTypes))]
    public void EveryTour_HasCloseUpStopsWhereARemnantExists(string what, Outcome o, ObserverSetup obs, Preset? p)
    {
        var stops = TourBuilder.For(o, obs, p, runNumber: 1);
        // A core-collapse outcome (a track of the Close-up's first second) has them; nothing else does.
        Assert.Equal(CoreCollapseHistory.TrackFor(o) is not null, stops.Any(s => s.CloseUp));
        if (!HasCompactRemnant(o)) Assert.DoesNotContain(stops, s => s.CloseUp);
        for (int i = 1; i < stops.Count; i++) Assert.True(stops[i].ObserverTimeS > stops[i - 1].ObserverTimeS, what);
        Assert.All(stops, s => Assert.True(!s.CloseUpOnly || s.CloseUp));
    }

    [Fact]
    public void NeutronStarTour_HasEveryStopInTimeOrder_WithFromSimulationsFirstSecond()
    {
        var p = PresetCatalog.Get("sn1987a");
        var o = ForPreset(p);
        var cu = TourBuilder.For(o, p.Observer, p, 1).Where(s => s.CloseUp).ToList();
        string all = string.Join("\n", cu.Select(s => s.Caption));
        double z1 = p.Observer.TimeDilation;

        Assert.Contains("has rebounded", all);
        Assert.Contains("stalls", all);
        Assert.Contains("revives the shock", all);
        Assert.Contains("solid crust", all);
        Assert.DoesNotContain("into a black hole", all);
        Assert.Contains(CoreCollapseHistory.FromSimulationsLabel, cu[0].Caption);

        var stop = cu.First(s => s.Caption.Contains("stalls"));
        Assert.Contains(CoreCollapseHistory.FromSimulationsLabel, stop.Caption);
        Assert.Equal(CoreCollapseHistory.StalledFromS * z1, stop.ObserverTimeS, 12);
        var core = CoreCollapseHistory.At(o, CoreCollapseHistory.StalledFromS);
        Assert.Contains(UnitFormat.Distance(core.ShockRadiusKm * 1e5), stop.Caption);
        Assert.Contains(UnitFormat.Number(core.InfallSpeedKmS), stop.Caption);
        Assert.Contains(UnitFormat.Distance(CoreCollapseHistory.DrawnProtoNsRadiusKm(core) * 1e5), stop.Caption);

        var revival = cu.First(s => s.Caption.Contains("revives the shock"));
        Assert.Contains(CoreCollapseHistory.FromSimulationsLabel, revival.Caption);
        Assert.Equal(CoreCollapseHistory.RevivalTimeS(o) * z1, revival.ObserverTimeS, 12);
        Assert.Contains(UnitFormat.Time(revival.ObserverTimeS), revival.Caption);
        var crust = cu.First(s => s.Caption.Contains("solid crust"));
        Assert.Equal(NeutronStarLayers.CrustFormationTimeS(o) * z1, crust.ObserverTimeS, 6);
        Assert.Contains(UnitFormat.Time(crust.ObserverTimeS), crust.Caption);
    }

    [Fact]
    public void BlackHoleTour_HasFormationAndSettledStops_WithModelNumbers()
    {
        var p = PresetCatalog.Get("n6946bh1");
        var o = ForPreset(p);
        Assert.Equal(RemnantKind.BlackHole, o.Remnant.Kind);
        double z1 = p.Observer.TimeDilation;
        var cu = TourBuilder.For(o, p.Observer, p, 1).Where(s => s.CloseUp).ToList();

        var bh = cu.First(s => s.Caption.Contains("collapses into a black hole"));
        Assert.Equal(CoreCollapseHistory.BlackHoleFormsS(o) * z1, bh.ObserverTimeS, 9);
        Assert.Contains(UnitFormat.Distance(CoreCollapseHistory.HorizonAtFormationKm(o) * 1e5), bh.Caption);
        Assert.Contains(CoreCollapseHistory.FromSimulationsLabel, bh.Caption);

        var settled = cu.First(s => s.Caption.Contains(BlackHoleModel.SettledCaption));
        Assert.Equal(BlackHoleModel.SettleEndS(o) * z1, settled.ObserverTimeS, 6);
        Assert.Contains(UnitFormat.Number(BlackHoleModel.SettledSpin(o)), settled.Caption);
        Assert.DoesNotContain(cu, s => s.Caption.Contains("solid crust") || s.Caption.Contains("pulsar"));
    }

    [Fact]
    public void CloseUpStops_FollowTimeDilation()
    {
        var o = ForPreset(PresetCatalog.Get("sn1987a"));
        var far = ObserverSetup.Default with { DistancePc = 2e9 };
        Assert.True(far.TimeDilation > 1.2);
        var near = TourBuilder.For(o, ObserverSetup.Default, null, 1).First(s => s.Caption.Contains("stalls"));
        var dil = TourBuilder.For(o, far, null, 1).First(s => s.Caption.Contains("stalls"));
        Assert.Equal(near.ObserverTimeS * far.TimeDilation / ObserverSetup.Default.TimeDilation, dil.ObserverTimeS, 12);
    }

    [Fact]
    public void CloseUpCaptions_AreLabModel_NeverObservedOrGravitationalWave()
    {
        foreach (var o in new[] { ForPreset(PresetCatalog.Get("sn1987a")), ForPreset(PresetCatalog.Get("n6946bh1")), Magnetar() })
            foreach (var s in TourBuilder.For(o, ObserverSetup.Default, null, 1).Where(s => s.CloseUpOnly))
            {
                Assert.StartsWith("In the lab", s.Caption);
                Assert.DoesNotContain("ravitational", s.Caption);
                Assert.DoesNotContain("Observed:", s.Caption);
                Assert.DoesNotContain("NaN", s.Caption);
            }
    }

    [Fact]
    public void GiantFlareStop_UsesTheRunNumber_AndComparesWith2004()
    {
        var o = Magnetar();
        int withFlare = Enumerable.Range(1, 400).First(r => MagnetarFlares.For(o, r).Count > 0);
        int without = Enumerable.Range(1, 400).First(r => MagnetarFlares.For(o, r).Count == 0);
        var flare = MagnetarFlares.For(o, withFlare)[0];

        var stops = TourBuilder.For(o, ObserverSetup.Default, null, withFlare);
        var stop = Assert.Single(stops, s => s.Caption.Contains("giant flare"));
        Assert.True(stop.CloseUp);
        Assert.Equal(flare.SourceTimeS * ObserverSetup.Default.TimeDilation, stop.ObserverTimeS, 6);
        Assert.Contains(UnitFormat.Energy(flare.EnergyErg), stop.Caption);
        Assert.Contains(MagnetarFlares.Comparison2004, stop.Caption);
        Assert.Contains(MagnetarFlares.Comparison2004Numbers, stop.Caption);

        Assert.DoesNotContain(TourBuilder.For(o, ObserverSetup.Default, null, without), s => s.Caption.Contains("giant flare"));
        Assert.DoesNotContain(TourBuilder.For(o, ObserverSetup.Default, null), s => s.Caption.Contains("giant flare"));   // no run: none
        Assert.DoesNotContain(TourBuilder.For(ForPreset(PresetCatalog.Get("sn1987a")), ObserverSetup.Default, null, withFlare),
            s => s.Caption.Contains("giant flare"));   // a non-magnetar never flares
    }

    [Fact]
    public void Director_UsesTheCurrentRunNumber()
    {
        var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        vm.Lab.InitialMass = 30; vm.Lab.Stripping = Stripping.Full; vm.Lab.Rotation = Rotation.Fast; vm.Lab.Field = CoreField.Strong;
        Assert.Equal(RemnantKind.Magnetar, vm.CurrentOutcome.Remnant.Kind);
        while (MagnetarFlares.For(vm.CurrentOutcome, vm.RunNumber).Count == 0) vm.RerollCommand.Execute(null);
        vm.Tour.Director.Start();
        Assert.Contains(vm.Tour.Director.Stops, s => s.Caption.Contains("giant flare"));
        vm.Tour.Director.Exit();
        while (MagnetarFlares.For(vm.CurrentOutcome, vm.RunNumber).Count > 0) vm.RerollCommand.Execute(null);
        vm.Tour.Director.Start();
        Assert.DoesNotContain(vm.Tour.Director.Stops, s => s.Caption.Contains("giant flare"));
    }

    // ---- the director ----

    private static (MainViewModel Vm, FakeWindowService Windows, TourDirector Director) Rig(string presetId)
    {
        var windows = new FakeWindowService();
        var vm = new MainViewModel(TestOutcomes.Engine, windows);
        vm.Lab.SelectedPreset = PresetCatalog.Get(presetId);
        return (vm, windows, vm.Tour.Director);
    }

    [Fact]
    public void FirstCloseUpStop_OpensWindow_OnceOnly()
    {
        var (_, windows, d) = Rig("sn1987a");
        Assert.Equal(0, windows.CloseUpCalls);
        d.Start();
        Assert.False(d.Current!.CloseUp);   // the first stop is Project 2's "Core collapse"
        Assert.Equal(0, windows.CloseUpCalls);
        d.Next();
        Assert.True(d.Current!.CloseUp);   // the bounce is the first Close-up stop
        Assert.Equal(1, windows.CloseUpCalls);
        for (int i = 0; i < 60 * 900 && d.State != TourState.Off; i++) d.Tick(Frame);
        Assert.Equal(TourState.Off, d.State);
        Assert.Equal(1, windows.CloseUpCalls);
    }

    [Fact]
    public void TourWithoutACompactRemnant_NeverOpensTheCloseUp()
    {
        var windows = new FakeWindowService();
        var vm = new MainViewModel(TestOutcomes.Engine, windows);
        vm.Lab.Kind = StarKind.WhiteDwarf;
        Assert.False(HasCompactRemnant(vm.CurrentOutcome));
        vm.Tour.Director.Start();
        for (int i = 0; i < 60 * 900 && vm.Tour.Director.State != TourState.Off; i++) vm.Tour.Director.Tick(Frame);
        Assert.Equal(0, windows.CloseUpCalls);
    }

    [Fact]
    public void ClosingWindow_SkipsRemainingCloseUpStops_TourContinues()
    {
        var (vm, windows, d) = Rig("sn1987a");
        d.Start();
        d.Next();   // the first Close-up stop opens the window
        windows.IsCloseUpOpen = false;   // the user closes the Close-up window
        d.Tick(Frame);
        Assert.False(vm.Tour.HighlightCloseUp);

        var visited = new List<int>();
        for (int i = 0; i < 60 * 900 && d.State != TourState.Off; i++)
        {
            d.Tick(Frame);
            if (d.State != TourState.Off && (visited.Count == 0 || visited[^1] != d.StopIndex)) visited.Add(d.StopIndex);
        }
        Assert.Equal(TourState.Off, d.State);
        Assert.Contains(d.Stops, s => s.CloseUpOnly);                                    // there were some to skip ...
        Assert.All(visited.Skip(1), i => Assert.False(d.Stops[i].CloseUpOnly, $"stop {i} was shown"));   // ... and none was shown
        Assert.Contains(visited, i => !d.Stops[i].CloseUp);                              // the tour went on to the other stops
        Assert.Equal(1, windows.CloseUpCalls);                                           // and the window was not reopened
    }

    [Fact]
    public void Next_SkipsCloseUpOnlyStops_AfterTheWindowIsClosed()
    {
        var (_, windows, d) = Rig("sn1987a");
        d.Start();
        d.Next();   // the bounce opens the window
        windows.IsCloseUpOpen = false;
        d.Next();
        int expected = Enumerable.Range(2, d.Stops.Count - 2).First(i => !d.Stops[i].CloseUpOnly);
        Assert.Equal(expected, d.StopIndex);
    }

    [Fact]
    public void CloseUpStop_HighlightsWindowOutline()
    {
        var (vm, windows, d) = Rig("sn1987a");
        Assert.False(vm.Tour.HighlightCloseUp);
        d.Start();
        Assert.False(vm.Tour.HighlightCloseUp);   // the first stop is not a Close-up one
        d.Next();
        Assert.True(vm.Tour.HighlightCloseUp);   // the bounce

        int plain = Enumerable.Range(0, d.Stops.Count).First(i => !d.Stops[i].CloseUp);
        d.GoTo(plain);
        Assert.False(vm.Tour.HighlightCloseUp);
        int closeUp = Enumerable.Range(plain + 1, d.Stops.Count - plain - 1).First(i => d.Stops[i].CloseUp);
        d.GoTo(closeUp);
        Assert.True(vm.Tour.HighlightCloseUp);
        windows.IsCloseUpOpen = false;
        d.Tick(Frame);
        Assert.False(vm.Tour.HighlightCloseUp);   // no outline for a window that is gone
        d.Exit();
        Assert.False(vm.Tour.HighlightCloseUp);
    }

    [Fact]
    public void HighlightCloseUp_RaisesPropertyChanged()
    {
        var (vm, _, d) = Rig("sn1987a");
        var raised = new List<string?>();
        vm.Tour.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        d.Start();
        Assert.Contains(nameof(TourViewModel.HighlightCloseUp), raised);
    }
}
