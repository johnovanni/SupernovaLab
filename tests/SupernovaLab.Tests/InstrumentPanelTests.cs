using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>The six instrument panels, the instrument strip and their wiring (Task 15; spec §5.0, §5.1, §9).</summary>
public class InstrumentPanelTests
{
    private const double Day = 86400, Year = 3.156e7;

    private static MainViewModel NewViewModel(FakeWindowService? windows = null)
        => new(TestOutcomes.Engine, windows ?? new FakeWindowService());

    private static MainViewModel WithPreset(string id)
    {
        var vm = NewViewModel();
        vm.Lab.SelectedPreset = PresetCatalog.Get(id);
        return vm;
    }

    private static T Panel<T>(MainViewModel vm) where T : InstrumentPanelViewModel => vm.Panels.OfType<T>().Single();

    private static IEnumerable<double> AllValues(IEnumerable<PlotSeries> series) => series.SelectMany(s => s.Values);

    // ---------------------------------------------------------------- the brief's tests

    [Fact]
    public void Panels_AreTheSixMessengers_InSpecOrder()
    {
        var vm = NewViewModel();
        Assert.Equal(new[]
        {
            Messenger.GravitationalWaves, Messenger.Neutrinos, Messenger.GammaRays, Messenger.XRaysUv, Messenger.VisibleLight,
            Messenger.Radio,
        }, vm.Panels.Select(p => p.Messenger));
    }

    [Fact]
    public void EveryPanel_WithTheDefaultOutcome_HasSeries_WithoutNaN()
    {
        var vm = NewViewModel();
        foreach (var panel in vm.Panels)
        {
            Assert.True(panel.Series.Count > 0, $"{panel.Title}: no series on '{panel.SelectedSubTab}'");
            Assert.All(panel.Series, s =>
            {
                Assert.NotEmpty(s.Values);
                Assert.Equal(s.ObserverTimesS.Length, s.Values.Length);
                Assert.All(s.Values, v => Assert.True(double.IsFinite(v), $"{panel.Title}/{s.Label}: {v}"));
                Assert.All(s.ObserverTimesS, t => Assert.True(double.IsFinite(t) && t > 0, $"{panel.Title}/{s.Label}: t = {t}"));
            });
            Assert.Null(panel.ErrorText);
        }
    }

    /// <summary>NaNs (outside a model's range) are removed by splitting into segments, on every sub-tab of every panel.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("sn1987a")]
    [InlineData("sn1998bw")]
    [InlineData("betelgeuse")]
    [InlineData("sn1054")]
    [InlineData("sn2011fe")]
    [InlineData("n6946bh1")]
    public void EverySubTab_DrawsOnlyFiniteValues_AndRaisesNoModelError(string? presetId)
    {
        var vm = presetId is null ? NewViewModel() : WithPreset(presetId);
        vm.Timeline.ObserverTimeS = 30 * Day;
        foreach (var panel in vm.Panels)
        foreach (string tab in panel.SubTabs)
        {
            panel.SelectedSubTab = tab;
            Assert.Null(panel.ErrorText);
            Assert.All(AllValues(panel.Series), v => Assert.True(double.IsFinite(v), $"{panel.Title}/{tab}: {v}"));
            Assert.All(panel.Series, s => Assert.All(s.Values, v => Assert.True(!s.LogY || v > 0, $"{panel.Title}/{tab}: log of {v}")));
            // Every series of one sub-tab shares one unit, so one y axis serves them all.
            Assert.True(panel.Series.Select(s => s.Unit).Distinct().Count() <= 1, $"{panel.Title}/{tab}: mixed units");
        }
    }

    [Fact]
    public void Sn1987a_NeutrinoFacts_IncludeTheKamiokandeIICount()
    {
        var vm = WithPreset("sn1987a");
        var neutrinos = Panel<NeutrinoPanelViewModel>(vm);

        var kam = Assert.Single(neutrinos.Facts, f => f.ChannelId == "cumulative@kamiokande-ii");
        Assert.Equal(11, kam.Value);
        // A detector without a channel is never dropped silently: it becomes a text row, with the lab's own expectation
        // (≈ 18.4 events, ruling R46) beside it.
        var row = Assert.Single(neutrinos.FactNotes, n => n.Text.Contains("Kamiokande-II recorded 11"));
        Assert.Contains("lab expects about 18", row.Text);
    }

    [Fact]
    public void MovingTheTime_DoesNotResample()
    {
        var vm = WithPreset("sn1987a");
        var before = vm.Panels.Select(p => (p.Series, p.MainSeries)).ToList();

        vm.Timeline.ObserverTimeS = 3 * Day;
        vm.Timeline.ObserverTimeS = 300 * Day;

        for (int i = 0; i < vm.Panels.Count; i++)
        {
            Assert.Same(before[i].Series, vm.Panels[i].Series);
            Assert.Same(before[i].MainSeries, vm.Panels[i].MainSeries);
        }
    }

    [Fact]
    public void ChangingTheDistance_Resamples()
    {
        var vm = NewViewModel();
        var before = vm.Panels.Select(p => p.Series).ToList();

        vm.Lab.DistancePc = 50_000;

        for (int i = 0; i < vm.Panels.Count; i++) Assert.NotSame(before[i], vm.Panels[i].Series);
    }

    [Theory]
    [InlineData(Messenger.GravitationalWaves, "strain", false)]
    [InlineData(Messenger.Neutrinos, "rate@superk", false)]
    [InlineData(Messenger.GammaRays, "line847", false)]
    [InlineData(Messenger.XRaysUv, "lum", false)]
    [InlineData(Messenger.VisibleLight, "appV", true)]
    [InlineData(Messenger.Radio, "radio5", false)]
    public void EachTile_ShowsItsMainSeries_AndItsPanelsBadge(Messenger messenger, string channel, bool inverted)
    {
        var vm = NewViewModel();
        vm.Timeline.ObserverTimeS = 20 * Day;
        var tile = Assert.Single(vm.Tiles, t => t.Messenger == messenger);
        var panel = Assert.Single(vm.Panels, p => p.Messenger == messenger);

        Assert.NotEmpty(tile.MainSeries);
        Assert.All(tile.MainSeries, s => Assert.Equal(channel, s.ChannelId));
        Assert.Equal(inverted, tile.InvertY);
        Assert.Same(panel.CurrentDetection, tile.Badge);

        vm.Timeline.ObserverTimeS = 2 * Year;
        Assert.Same(panel.CurrentDetection, tile.Badge);
        Assert.Equal(TimeAxis.ToFraction(2 * Year), tile.CursorFraction, 12);
    }

    [Fact]
    public void GammaTile_ShowsThePromptBurst_WhenTheOutcomeHasAGrb()
    {
        var vm = WithPreset("sn1998bw");
        Assert.True(vm.CurrentOutcome.HasGrb);
        var tile = Assert.Single(vm.Tiles, t => t.Messenger == Messenger.GammaRays);
        Assert.NotEmpty(tile.MainSeries);
        Assert.All(tile.MainSeries, s => Assert.Equal("prompt", s.ChannelId));
    }

    [Fact]
    public void ClickingATile_OpensTheInstrumentsWindowOnItsPanel()
    {
        var windows = new FakeWindowService();
        var vm = NewViewModel(windows);
        var tile = Assert.Single(vm.Tiles, t => t.Messenger == Messenger.Radio);

        tile.OpenCommand.Execute(null);

        Assert.Equal(new Messenger?[] { Messenger.Radio }, windows.InstrumentsCalls);
    }

    [Fact]
    public void Panels_AreTheSameInstances_AfterTheWindowIsShownTwice()
    {
        var windows = new FakeWindowService();
        var vm = NewViewModel(windows);
        var panels = vm.Panels;
        var first = panels.ToList();

        vm.ShowInstrumentsCommand.Execute(null);
        vm.ShowInstrumentsCommand.Execute(Messenger.Neutrinos);

        Assert.Equal(2, windows.InstrumentsCalls.Count);
        Assert.Same(panels, vm.Panels);
        Assert.Equal(first, vm.Panels);
        Assert.All(vm.Tiles, t => Assert.Contains(t.Panel, first));
    }

    // ---------------------------------------------------------------- hand-offs: neutrino dots, re-roll

    /// <summary>
    /// Betelgeuse at 168 pc: Super-K expects millions of events (spec §8's preset check: 10⁶–10⁸), so the dots are thinned
    /// to at most about 5,000, each standing for N events, and the label says so.
    /// </summary>
    [Fact]
    public void NeutrinoDots_AreThinned_ToAtMostAbout5000_AndLabelled()
    {
        var vm = WithPreset("betelgeuse");
        var neutrinos = Panel<NeutrinoPanelViewModel>(vm);
        neutrinos.SelectedSubTab = NeutrinoPanelViewModel.CumulativeTab;

        var dots = Assert.Single(neutrinos.Series, s => s.IsDots);
        int perDot = neutrinos.EventsPerDot;
        Assert.True(perDot > 100, $"expected heavy thinning for ~10⁷ events, got 1 dot = {perDot}");
        // Poisson: the count of dots has mean ≤ 5,000 and σ ≈ 71, so 5,400 is more than 5σ above it.
        Assert.InRange(dots.Values.Length, 4000, 5400);
        Assert.Contains($"1 dot = {perDot:N0} events", dots.Label);
        // The dots climb the expected cumulative curve: the last dot's running total is within 3% of the curve's end.
        var curve = neutrinos.Series.Where(s => !s.IsDots).SelectMany(s => s.Values).Max();
        Assert.InRange(dots.Values[^1] / curve, 0.97, 1.03);
    }

    [Fact]
    public void NeutrinoDots_UseTheRunNumberAsSeed_AndReroll_Resamples()
    {
        var vm = NewViewModel();
        var neutrinos = Panel<NeutrinoPanelViewModel>(vm);
        neutrinos.SelectedSubTab = NeutrinoPanelViewModel.CumulativeTab;
        var first = neutrinos.Series.Single(s => s.IsDots);
        int outcomeChanged = 0;
        vm.OutcomeChanged += (_, _) => outcomeChanged++;

        vm.RerollCommand.Execute(null);

        // Re-roll raises PropertyChanged(RunNumber), not OutcomeChanged; the panels resample on it anyway.
        Assert.Equal(0, outcomeChanged);
        var second = neutrinos.Series.Single(s => s.IsDots);
        Assert.NotSame(first, second);
        Assert.NotEqual(first.ObserverTimesS, second.ObserverTimesS);

        // The same seed gives the same events.
        var again = NewViewModel();
        var p2 = Panel<NeutrinoPanelViewModel>(again);
        p2.SelectedSubTab = NeutrinoPanelViewModel.CumulativeTab;
        Assert.Equal(first.ObserverTimesS, p2.Series.Single(s => s.IsDots).ObserverTimesS);
    }

    // ---------------------------------------------------------------- hand-offs: GW spectrogram

    [Fact]
    public void GwSpectrogram_IsLogScaled_AndNotesTheRingdownAboveItsRange_ForABlackHole()
    {
        var vm = NewViewModel();
        vm.Lab.InitialMass = 25; // S16: a failed supernova (direct black hole) at solar metal content
        Assert.Equal(RemnantKind.BlackHole, vm.CurrentOutcome.Remnant.Kind);
        var gw = Panel<GravitationalWavePanelViewModel>(vm);

        gw.SelectedSubTab = GravitationalWavePanelViewModel.SpectrogramTab;

        Assert.Equal(PlotKind.Spectrogram, gw.Kind);
        var s = Assert.IsType<SpectrogramView>(gw.Spectrogram);
        // log10 of power normalized to 1: the maximum is 0 and nothing lies below the floor.
        double max = s.Log10Power.Cast<double>().Max(), min = s.Log10Power.Cast<double>().Min();
        Assert.Equal(0, max, 9);
        Assert.True(min >= -SpectrogramView.FloorDecades - 1e-9);
        Assert.Equal(2048, s.MaxFrequencyHz);
        Assert.Contains("3 kHz", gw.PanelNote);
    }

    // ---------------------------------------------------------------- hand-offs: X-ray instruments

    /// <summary>
    /// Ruling R29: once the shock has broken out, Swift's NotYet means its first exposure is still collecting photons.
    /// The badge says so, and never "before breakout"; before breakout it is a plain "Not yet".
    /// </summary>
    [Fact]
    public void XrayNotYet_AfterBreakout_SaysTheExposureIsFilling()
    {
        // The default 16 M☉ red supergiant (z = 0 below 50 Mpc), seen by Einstein Probe, which only the breakout model judges.
        // EP needs 2.5e-8 erg cm⁻² (2.5e-11 × 1,000 s). Whether and when its first exposure fills depends on the distance,
        // so the test looks over 0.1–10 Mpc and moments from 0.1 ms to 100 s after breakout for a filling exposure.
        var vm = NewViewModel();
        var xray = Panel<XrayUvPanelViewModel>(vm);
        xray.SelectedInstrument = Detectors.Get("einstein-probe");
        double tb = new ShockBreakoutModel().BreakoutTimeS(vm.CurrentOutcome) * vm.CurrentObserver.TimeDilation;

        vm.Timeline.ObserverTimeS = tb * 0.5;
        Assert.Equal(DetectionStatus.NotYet, xray.CurrentDetection.Status);
        Assert.Equal("Not yet", xray.DetectionText);

        var seen = new List<string>();
        foreach (double pc in new[] { 1e5, 3e5, 1e6, 3e6, 1e7 })
        {
            vm.Lab.DistancePc = pc;
            foreach (double dt in new[] { 1e-4, 1e-3, 1e-2, 0.1, 1, 10, 100 })
            {
                vm.Timeline.ObserverTimeS = tb + dt;
                seen.Add($"{pc} pc +{dt} s: {xray.CurrentDetection.Status}");
                if (xray.CurrentDetection.Status != DetectionStatus.NotYet) continue;
                Assert.Equal(XrayUvPanelViewModel.ExposureFillingText, xray.DetectionText);
                Assert.DoesNotContain("breakout", xray.DetectionText);
                Assert.Equal("Exposure filling", xray.DetectionShortText);
                return;
            }
        }
        Assert.Fail("No filling first exposure found: " + string.Join("; ", seen));
    }

    /// <summary>
    /// The badge answers "does this instrument see this messenger now?": every model of the panel that judges it is
    /// asked. After the neutrino burst it says the burst is over and whether it was caught.
    /// </summary>
    [Fact]
    public void AfterTheBurst_TheBadgeSaysOver_AndWhetherItWasCaught()
    {
        var vm = WithPreset("betelgeuse");
        vm.Timeline.ObserverTimeS = 30 * Day;
        // Super-K expects about 2 × 10⁷ events from Betelgeuse at 168 pc (spec §8: 10⁶–10⁸).
        Assert.Equal("Over: it was detected", Panel<NeutrinoPanelViewModel>(vm).DetectionText);
        Assert.Equal("Over: detected", Panel<NeutrinoPanelViewModel>(vm).DetectionShortText);

        var far = WithPreset("sn1998bw"); // 38 Mpc: fewer than 0.01 events
        far.Timeline.ObserverTimeS = 30 * Day;
        Assert.Equal("Over: too faint", Panel<NeutrinoPanelViewModel>(far).DetectionShortText);
    }

    /// <summary>The circumstellar model is a Radio model, but its X-ray channels go on the X-rays &amp; UV panel.</summary>
    [Fact]
    public void CsmXrayChannels_AreOnTheXrayPanel_AndTheRingFactIsMarked()
    {
        var vm = WithPreset("sn1987a");
        var xray = Panel<XrayUvPanelViewModel>(vm);
        xray.SelectedSubTab = XrayUvPanelViewModel.CsmTab;

        Assert.Contains(xray.Series, s => s.ChannelId == "lx");
        Assert.Contains(xray.Series, s => s.ChannelId == "ringLx");
        var marker = Assert.Single(xray.FactMarkers, m => m.Fact.ChannelId == "ringLx");
        Assert.Equal(820800000, marker.ObserverTimeS!.Value, 0); // z = 0 at 51.4 kpc
        Assert.DoesNotContain(Panel<RadioPanelViewModel>(vm).Series, s => s.ChannelId is "lx" or "fluxX" or "ringLx");
    }

    /// <summary>Signals §8.4: before about 100 yr the neutron star's surface temperature is drawn as a band.</summary>
    [Fact]
    public void NeutronStarCooling_IsABand_Before100Years()
    {
        var vm = NewViewModel();
        Assert.Equal(RemnantKind.NeutronStar, vm.CurrentOutcome.Remnant.Kind);
        var xray = Panel<XrayUvPanelViewModel>(vm);
        xray.SelectedSubTab = XrayUvPanelViewModel.CoolingTab;

        var band = Assert.Single(xray.Bands);
        Assert.Equal("K", band.Unit);
        Assert.All(band.ObserverTimesS, t => Assert.InRange(t, 30 * Year * 0.99, 100 * Year * 1.01));
        for (int i = 0; i < band.Low.Length; i++) Assert.True(band.Low[i] <= band.High[i]);
        Assert.True(band.Low.Zip(band.High).Any(p => p.Second > p.First * 1.01), "the band has no width");
        Assert.Contains(xray.Series, s => s.ChannelId == "tempK");
    }

    // ---------------------------------------------------------------- spectrum and pulses (views at the current time)

    [Fact]
    public void SchematicSpectrum_FollowsTheTime_WithItsSourcesAndConfidence()
    {
        var vm = NewViewModel();
        var visible = Panel<VisiblePanelViewModel>(vm);
        visible.SelectedSubTab = VisiblePanelViewModel.SpectrumTab;
        vm.Timeline.ObserverTimeS = 20 * Day;

        Assert.Equal(PlotKind.Spectrum, visible.Kind);
        var s = Assert.IsType<SpectrumView>(visible.Spectrum);
        Assert.Equal(SpectrumModel.PointCount, s.WavelengthsAngstrom.Length);
        Assert.Contains("Schematic spectrum", s.Caption);
        Assert.NotEmpty(s.Lines);
        Assert.Equal(Confidence.Speculative, visible.Confidence);
        Assert.Contains("Filippenko", visible.ConfidenceToolTip);

        vm.Timeline.ObserverTimeS = 60 * Day;
        Assert.NotSame(s, visible.Spectrum);
    }

    [Fact]
    public void SpectrumOfAnInteractingType_LabelsTheVelocityAsTheUnderlyingEjectas()
    {
        var vm = NewViewModel();
        vm.Lab.DenseCsm = true;
        Assert.Equal(SupernovaType.IIn, vm.CurrentOutcome.Type);
        var visible = Panel<VisiblePanelViewModel>(vm);
        visible.SelectedSubTab = VisiblePanelViewModel.SpectrumTab;
        vm.Timeline.ObserverTimeS = 30 * Day;

        Assert.Contains("underlying ejecta", visible.Spectrum!.Caption);
    }

    /// <summary>
    /// The Crab (sn1054) at its present age, 2.9568e10 s: the pulses come every 33.4 ms (Lyne et al. 1993; the preset's
    /// fact), so 1 s holds 1 / 0.0334 ≈ 30 pulses.
    /// </summary>
    [Fact]
    public void PulsarTab_ShowsOneSecondOfPulses_AtTheCurrentTime()
    {
        var vm = WithPreset("sn1054");
        var radio = Panel<RadioPanelViewModel>(vm);
        radio.SelectedSubTab = RadioPanelViewModel.PulsarTab;
        vm.Timeline.ObserverTimeS = 2.9568e10;

        Assert.Equal(PlotKind.PulseTrain, radio.Kind);
        var p = Assert.IsType<PulseTrainView>(radio.PulseTrain);
        Assert.InRange(p.PulseOffsetsS.Count, 29, 31);
        double spacing = p.PulseOffsetsS[1] - p.PulseOffsetsS[0];
        Assert.InRange(spacing, 0.0334 * 0.97, 0.0334 * 1.03);
        Assert.Equal(1.0, p.WindowS);
    }

    [Fact]
    public void PulsarTab_ShowsNoPulses_WhenTheBeamMissesEarth()
    {
        var vm = WithPreset("sn1054");
        vm.Lab.BeamTowardEarth = false;
        var radio = Panel<RadioPanelViewModel>(vm);
        radio.SelectedSubTab = RadioPanelViewModel.PulsarTab;
        radio.SelectedInstrument = Detectors.Get("fast");
        vm.Timeline.ObserverTimeS = 2.9568e10;

        Assert.Empty(radio.PulseTrain?.PulseOffsetsS ?? Array.Empty<double>());
        Assert.Equal(DetectionStatus.NotPointedAtUs, radio.CurrentDetection.Status);
        Assert.Contains("beams", radio.PlotNote);
    }

    // ---------------------------------------------------------------- facts, notes, comparisons

    [Fact]
    public void EveryFact_IsListed_AndTimedFactsOnADrawnChannelAreMarked()
    {
        var vm = WithPreset("sn1998bw");
        foreach (var panel in vm.Panels)
        {
            var expected = PresetCatalog.Get("sn1998bw").Facts.Where(f => f.Messenger == panel.Messenger).ToList();
            Assert.Equal(expected, panel.Facts);
            Assert.True(panel.FactNotes.Count >= expected.Count, $"{panel.Title}: a fact was dropped");
        }

        // Radio 5 GHz on day 12 is timed and drawn: a marker. The peak V has no time: a labelled level line.
        var radio = Panel<RadioPanelViewModel>(vm);
        Assert.Single(radio.FactMarkers, m => m.Fact.ChannelId == "radio5");
        var visible = Panel<VisiblePanelViewModel>(vm);
        Assert.Contains(visible.ReferenceLines, l => !l.IsTime && l.Value == 13.62 && l.Label.Contains("Observed"));
    }

    [Fact]
    public void AfterTheObserverIsMoved_FactsAreNotMarked_AndANoteSaysWhy()
    {
        var vm = WithPreset("sn1998bw");
        vm.Lab.DistancePc = 1e6;
        var radio = Panel<RadioPanelViewModel>(vm);

        Assert.Empty(radio.FactMarkers);
        Assert.Contains(radio.FactNotes, n => n.Text.Contains("observer has been moved"));
        Assert.Contains(radio.FactNotes, n => n.Text.Contains("45 mJy"));
    }

    /// <summary>
    /// SN 1987A's peak, stated from the drawn curve (R34) with its plain-language comparison: V ≈ 2.9–3.0 lies between
    /// 1.5 and 6, which Comparisons calls "visible to the naked eye".
    /// </summary>
    [Fact]
    public void VisiblePanel_StatesThePeakAndTheCurrentMagnitude_WithComparisons()
    {
        var vm = WithPreset("sn1987a");
        var visible = Panel<VisiblePanelViewModel>(vm);
        vm.Timeline.ObserverTimeS = 200 * Day;

        Assert.Contains("Peak V", visible.SummaryText);
        Assert.Contains("visible to the naked eye", visible.SummaryText);
        Assert.Contains("Now V", visible.SummaryText);
        Assert.Contains(visible.ReferenceLines, l => !l.IsTime && l.Value == 6);
    }

    [Fact]
    public void PanelConfidence_IsTheLeastCertainOfItsModels()
    {
        var vm = NewViewModel();
        Assert.Equal(Confidence.Speculative, Panel<GravitationalWavePanelViewModel>(vm).Confidence);
        Assert.Equal(Confidence.ModelDependent, Panel<NeutrinoPanelViewModel>(vm).Confidence);
        Assert.All(vm.Panels, p => Assert.False(string.IsNullOrWhiteSpace(p.ConfidenceToolTip)));
    }

    [Fact]
    public void InstrumentChoices_AreTheSpecTable_WithEachThresholdsConfidence()
    {
        var vm = NewViewModel();
        Assert.Equal(new[] { "aligo", "et", "ce" }, Panel<GravitationalWavePanelViewModel>(vm).Instruments.Select(d => d.Id));
        Assert.Equal(new[] { "superk", "hyperk", "icecube", "juno", "dune" },
            Panel<NeutrinoPanelViewModel>(vm).Instruments.Select(d => d.Id));
        Assert.Equal(new[] { "gbm", "bat", "spi", "cosi", "nustar" }, Panel<GammaPanelViewModel>(vm).Instruments.Select(d => d.Id));
        Assert.Equal(new[] { "swift-xrt", "einstein-probe", "ultrasat", "chandra", "xmm" },
            Panel<XrayUvPanelViewModel>(vm).Instruments.Select(d => d.Id));
        Assert.Equal(new[] { "eye", "ztf", "rubin", "spectrograph" }, Panel<VisiblePanelViewModel>(vm).Instruments.Select(d => d.Id));
        // Ruling R40 adds Parkes and MeerKAT's pulsar mode for the pulsar view.
        Assert.Equal(new[] { "vla", "meerkat", "fast", "ska", "parkes", "meerkat-psr" },
            Panel<RadioPanelViewModel>(vm).Instruments.Select(d => d.Id));
        Assert.All(vm.Panels.SelectMany(p => p.InstrumentChoices), c => Assert.Contains(c.Detector.ThresholdConfidence switch
        {
            Confidence.Established => "Established",
            Confidence.ModelDependent => "Model-dependent",
            _ => "Speculative",
        }, c.ToolTip));
    }

    /// <summary>Spec §5.1: a glossary tooltip on every term, the sub-tab names included.</summary>
    [Fact]
    public void EverySubTab_HasAGlossaryToolTip()
    {
        var vm = NewViewModel();
        foreach (string tab in vm.Panels.SelectMany(p => p.SubTabs))
            Assert.True(SupernovaLab.App.Resources.SubTabToolTipConverter.All.ContainsKey(tab), $"no tooltip for '{tab}'");
    }

    [Fact]
    public void OutsideAModelsRange_ThePanelSaysWhy()
    {
        var vm = NewViewModel();
        vm.Timeline.ObserverTimeS = 100 * Year;
        var gw = Panel<GravitationalWavePanelViewModel>(vm);
        Assert.False(string.IsNullOrEmpty(gw.PlotNote));
        Assert.Contains("over", gw.PlotNote);
    }

    // ---------------------------------------------------------------- spec §9: model failure

    [Fact]
    public void AThrowingModel_ShowsModelError_InThatPanelOnly()
    {
        var bad = new FakePanel(new FakeModel(throws: true));
        var good = new FakePanel(new FakeModel(throws: false));
        var o = TestOutcomes.Star(16);

        bad.Update(o, ObserverSetup.Default, 1, Array.Empty<ObservedFact>(), true);
        good.Update(o, ObserverSetup.Default, 1, Array.Empty<ObservedFact>(), true);
        bad.OnTimeChanged(10);
        good.OnTimeChanged(10);

        Assert.Contains("Model error", bad.ErrorText);
        Assert.Contains("fake", bad.ErrorText);
        Assert.Empty(bad.Series);
        Assert.Equal("Model error", bad.DetectionText);
        Assert.Null(good.ErrorText);
        Assert.NotEmpty(good.Series);
    }

    [Fact]
    public void AnInfiniteValue_IsAModelError()
    {
        var panel = new FakePanel(new FakeModel(throws: false, infinite: true));
        panel.Update(TestOutcomes.Star(16), ObserverSetup.Default, 1, Array.Empty<ObservedFact>(), true);

        Assert.Contains("Model error", panel.ErrorText);
        Assert.Empty(panel.Series);
    }

    // ---------------------------------------------------------------- Task 14 carry-over (N2)

    [Fact]
    public void ReselectingTheSamePreset_AfterAnObserverEdit_RestoresItsPublishedObserver()
    {
        var vm = WithPreset("sn1987a");
        var preset = PresetCatalog.Get("sn1987a");
        vm.Lab.DistancePc = 780_000;
        vm.Lab.Av = 3;
        vm.Lab.BeamTowardEarth = false;
        Assert.Contains("Observer changed", vm.StatusMessage);

        vm.Lab.SelectedPreset = preset;

        Assert.Equal(preset.Observer, vm.CurrentObserver);
        Assert.True(vm.Lab.IsObserverPublished);
        Assert.DoesNotContain("Observer changed", vm.StatusMessage);
        Assert.Contains("SN 1987A", vm.StatusMessage);
        // The panels were resampled with the published observer: SN 1987A's measurements are marked again.
        Assert.Contains(Panel<VisiblePanelViewModel>(vm).FactMarkers, m => m.Fact.ChannelId == "appV");
    }

    [Fact]
    public void ReselectingTheSamePreset_WithItsObserverUnchanged_DoesNothing()
    {
        var vm = WithPreset("sn1987a");
        int raised = 0;
        vm.OutcomeChanged += (_, _) => raised++;

        vm.Lab.SelectedPreset = PresetCatalog.Get("sn1987a");

        Assert.Equal(0, raised);
    }

    [Fact]
    public void ALaterEdit_ReplacesAStaleStatusMessage()
    {
        var vm = WithPreset("sn1987a");
        vm.Lab.Metallicity = 0.5; // drops the preset
        Assert.Contains("no longer apply", vm.StatusMessage);

        vm.Lab.DistancePc = 20_000; // a plain observer edit, with no preset left

        Assert.DoesNotContain("no longer apply", vm.StatusMessage ?? "");

        var moved = WithPreset("sn1987a");
        moved.Lab.DistancePc = 780_000;
        string first = moved.StatusMessage!;
        moved.StatusMessage = "something stale";
        moved.Lab.DistancePc = 900_000; // a second observer edit restates where things stand
        Assert.Equal(first, moved.StatusMessage);
    }

    // ---------------------------------------------------------------- fakes

    private sealed class FakeModel(bool throws, bool infinite = false) : ISignalModel
    {
        public string Id => "fake";
        public Messenger Messenger => Messenger.Radio;
        public IReadOnlyList<SignalChannel> Channels { get; } = new[] { new SignalChannel("x", "Fake", "mJy", true) };
        public Confidence Confidence => Confidence.Speculative;
        public IReadOnlyList<Source> Sources { get; } = new[] { new Source("Fake", "tests") };
        public bool Applies(Outcome outcome) => true;
        public (double StartS, double EndS) ValidRange(Outcome outcome) => (1, 1e6);

        public double Sample(Outcome outcome, ObserverSetup observer, double sourceTimeS, string channelId)
        {
            if (throws) throw new InvalidOperationException("boom");
            if (!(sourceTimeS >= 1 && sourceTimeS <= 1e6)) return double.NaN;
            return infinite && sourceTimeS > 100 ? double.PositiveInfinity : 1 / sourceTimeS;
        }

        public Detection Detect(Outcome outcome, ObserverSetup observer, double sourceTimeS, Detector detector)
            => throws ? throw new InvalidOperationException("boom") : new Detection(DetectionStatus.Detected, "Fake.");
    }

    private sealed class FakePanel(ISignalModel model)
        : InstrumentPanelViewModel(new[] { "Only" }, new[] { Detectors.Get("vla") })
    {
        public override Messenger Messenger => Messenger.Radio;
        public override string Title => "Fake";

        protected override PanelContent BuildTab(string subTab, Detector instrument)
            => new() { Series = SampleChannel(model, "x"), YLabel = "Fake (mJy)", LogY = true };

        protected override IReadOnlyList<PlotSeries> BuildMainSeries() => SampleChannel(model, "x");
        protected override IReadOnlyList<ISignalModel> TabModels(string subTab) => new[] { model };
        protected override IReadOnlyList<ISignalModel> DetectorModels(Detector instrument) => new[] { model };
    }
}
