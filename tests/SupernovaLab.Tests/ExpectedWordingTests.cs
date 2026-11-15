using SupernovaLab.App.Tour;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;

namespace SupernovaLab.Tests;

/// <summary>Task 2 (spec §7.1; compact §C10, §C11): a signal never observed from any supernova says "Expected", not "Detected".</summary>
public class ExpectedWordingTests
{
    private const double Day = 86400;

    private static MainViewModel Preset(string id, double? distancePc = null, double? observerTimeS = null)
    {
        var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        vm.Lab.SelectedPreset = PresetCatalog.Get(id);
        if (distancePc is { } d) vm.Lab.DistancePc = d;
        if (observerTimeS is { } t) vm.Timeline.ObserverTimeS = t;
        return vm;
    }

    private static T Panel<T>(MainViewModel vm) where T : InstrumentPanelViewModel => vm.Panels.OfType<T>().Single();

    [Fact]
    public void GravitationalWaveBadge_SaysExpectedNotDetected()
    {
        var gw = Panel<GravitationalWavePanelViewModel>(Preset("betelgeuse", observerTimeS: 0.3));
        Assert.Equal(DetectionStatus.Detected, gw.CurrentDetection.Status);
        Assert.Equal("Expected", gw.DetectionText);
        Assert.Equal("Expected", gw.DetectionShortText);
        Assert.Equal("Expected", gw.StripBadgeText);
    }

    [Fact]
    public void GravitationalWaveBadge_TooFaintUnchanged()
    {
        var gw = Panel<GravitationalWavePanelViewModel>(Preset("betelgeuse", distancePc: 50_000, observerTimeS: 0.3));
        Assert.Equal("Too faint at this distance", gw.DetectionText);
    }

    [Fact]
    public void GravitationalWaveOver_SaysExpected()
    {
        var gw = Panel<GravitationalWavePanelViewModel>(Preset("betelgeuse", observerTimeS: 30 * Day));
        Assert.Equal("Over: expected to be detected", gw.DetectionText);
        Assert.Equal("Over: expected", gw.StripBadgeText);
    }

    [Fact]
    public void NeutrinoBadge_ObservedSupernova_StillSaysDetected()
    {
        var nu = Panel<NeutrinoPanelViewModel>(Preset("sn1987a", observerTimeS: 5));
        Assert.Equal("Detected", nu.DetectionText);
        Assert.Equal("Detected", nu.StripBadgeText);
    }

    [Fact]
    public void NeutrinoOver_ObservedSupernova_StillSaysItWasDetected()
    {
        var nu = Panel<NeutrinoPanelViewModel>(Preset("betelgeuse", observerTimeS: 30 * Day));
        Assert.Equal("Over: it was detected", nu.DetectionText);
    }

    // The audit's triples (compact §C10), on outcomes the engine and the presets give.
    public static IEnumerable<object[]> AuditTriples()
    {
        var sn = TestOutcomes.Engine.Compute(LabInputs.Default);
        var bh = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.07, 3e13, TestOutcomes.BlackHole(8, 3));
        var ring = sn with { Inputs = sn.Inputs, HasEquatorialRing = true };
        var failed = sn with { Fate = Fate.FailedSupernova };
        var pisn = sn with { Fate = Fate.PairInstabilitySupernova };
        var ppi = sn with { Fate = Fate.PulsationalPairInstability };
        yield return new object[] { "gw", "strain", sn, true };
        yield return new object[] { "neutrino", "lum_nue", sn, true };
        yield return new object[] { "neutrino", "lum_nux", sn, true };
        yield return new object[] { "neutrino", "rate@dune", sn, true };
        yield return new object[] { "neutrino", "cumulative@dune", sn, true };
        yield return new object[] { "neutrino", "lum_anue", sn, false };
        yield return new object[] { "neutrino", "rate@superk", sn, false };
        yield return new object[] { "neutrino", "rate@superk", bh, true };
        yield return new object[] { "neutrino", "meanE_anue", bh, true };
        yield return new object[] { "pulsar", "periodS", ring, true };
        yield return new object[] { "pulsar", "periodS", sn, false };
        yield return new object[] { "lightcurve", "appV", failed, true };
        yield return new object[] { "lightcurve", "appV", sn, false };
        yield return new object[] { "gammalines", "line847", pisn, true };
        yield return new object[] { "csm", "lx", ppi, true };
        yield return new object[] { "gammalines", "line847", sn, false };
        yield return new object[] { "breakout", "lum", sn, false };
        yield return new object[] { "csm", "lx", sn, false };
    }

    [Theory, MemberData(nameof(AuditTriples))]
    public void NeverObserved_FollowsTheAuditTable(string model, string channel, Outcome o, bool expected)
        => Assert.Equal(expected, NeverObserved.Applies(model, channel, o));

    [Fact]
    public void BadgeWord_IsExpected() => Assert.Equal("Expected", NeverObserved.BadgeWord);

    /// <summary>
    /// The gravitational-wave sentence is merged into the neutrino stop (same time), whose highlight is the neutrinos, so the
    /// sentence is found by its words, not by the stop's highlight.
    /// </summary>
    [Fact]
    public void TourGravitationalWaveCaption_IsExpected()
    {
        var p = PresetCatalog.Get("betelgeuse");
        var o = TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
        var stops = TourBuilder.For(o, p.Observer, null).Where(s => s.Caption.Contains("ravitational waves ripple")).ToList();
        Assert.NotEmpty(stops);
        Assert.All(stops, s =>
        {
            Assert.Contains("Expected: Gravitational waves ripple", s.Caption);
            Assert.DoesNotContain("In the lab, gravitational waves", s.Caption);
        });
    }

    [Fact]
    public void TourNeutrinoCaption_ObservedSupernova_KeepsInTheLab()
    {
        var p = PresetCatalog.Get("sn1987a");
        var o = TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
        var stops = TourBuilder.For(o, p.Observer, null).Where(s => s.Highlight == SupernovaLab.Core.Signals.Messenger.Neutrinos).ToList();
        Assert.NotEmpty(stops);
        Assert.All(stops, s => Assert.StartsWith("In the lab,", s.Caption));
    }

    [Fact]
    public void Sn1987aStory_MentionsDisputedBarClaim()
    {
        string text = PresetCatalog.Get("sn1987a").WhatWasSeen;
        Assert.Contains("disputed", text);
        Assert.Contains("Galeotti & Pizzella 2016", text);
    }
}
