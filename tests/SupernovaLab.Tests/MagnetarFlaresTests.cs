using SupernovaLab.App.Services;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;
using static SupernovaLab.Tests.CompactResearch;

namespace SupernovaLab.Tests;

/// <summary>Plan Task 12 (spec §5, §7.3; compact §C6): the magnetar, its flares and their spike on the gamma-ray panel.</summary>
public class MagnetarFlaresTests
{
    private const double Year = 3.156e7;

    private static Outcome Magnetar(double periodS = 2.5, double fieldG = 1e15)
        => TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.07, 3e13, TestOutcomes.Pulsar(periodS, fieldG, RemnantKind.Magnetar));

    private static Outcome Default => TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.07, 3e13);

    private static CloseUpScene SceneFor(Outcome o, int run = 1) => new(o, ObserverSetup.Default, runNumber: run);

    /// <summary>The first run (from 1) whose flares number exactly one.</summary>
    private static int RunWithOneFlare(Outcome o)
    {
        for (int run = 1; run < 500; run++)
            if (MagnetarFlares.For(o, run).Count == 1) return run;
        throw new InvalidOperationException("No run with one flare");
    }

    // ---- The flares (brief tests) ----

    [Fact]
    public void Flares_OnlyForMagnetars_SeededByRun()
    {
        var mag = Magnetar();
        Assert.Empty(MagnetarFlares.For(Default, 1));
        Assert.Empty(MagnetarFlares.For(TestOutcomes.Star(23), 1));
        Assert.Equal(MagnetarFlares.For(mag, 5), MagnetarFlares.For(mag, 5));
        Assert.NotEqual(MagnetarFlares.For(mag, 5), MagnetarFlares.For(mag, 6));
        // The run number alone: the outcome's other numbers do not change the draw.
        Assert.Equal(MagnetarFlares.For(mag, 5), MagnetarFlares.For(Magnetar(0.091, 5e14), 5));
    }

    [Fact]
    public void Flare_MeanCountMatchesRate()
    {
        var mag = Magnetar();
        double mean = Enumerable.Range(1, 2000).Average(run => MagnetarFlares.For(mag, run).Count);
        double expected = MagnetarFlares.RatePerYear * 1000;
        Assert.InRange(mean, expected * 0.95, expected * 1.05);
    }

    [Fact]
    public void Rate_IsCompactC6s_AndInsideGehrelsRange()
    {
        Assert.Equal(C6.RateExact, MagnetarFlares.RatePerYear, 4);                 // 3 / (47.7 × 30)
        Assert.InRange(MagnetarFlares.RatePerYear, C6.RatePerYear * 0.99, C6.RatePerYear * 1.01);
        Assert.InRange(MagnetarFlares.RatePerYear, C6.RateGehrelsLow, C6.RateGehrelsHigh);
        Assert.InRange(C6.RateBurns, C6.RateGehrelsLow, C6.RateGehrelsHigh);       // the cross-check agrees within errors
        Assert.Equal(C6.RateGehrelsLow, MagnetarFlares.RateLowPerYear);
        Assert.Equal(C6.RateGehrelsHigh, MagnetarFlares.RateHighPerYear);
    }

    [Fact]
    public void Flares_HaveTheResearchsSpikeAndEnergyRange()
    {
        var all = Enumerable.Range(1, 400).SelectMany(r => MagnetarFlares.For(Magnetar(), r)).ToList();
        Assert.NotEmpty(all);
        Assert.All(all, f =>
        {
            Assert.Equal(C6.SpikeS, f.SpikeS);
            Assert.InRange(f.EnergyErg, C6.EnergyLowErg, C6.EnergyHighErg);
            Assert.Equal(f.EnergyErg / f.SpikeS, f.PeakLuminosityErgS, 3);
            Assert.InRange(f.SourceTimeS, Magnetosphere.MagnetosphereFormsS(Magnetar()), TimeAxis.MaxS);
        });
        Assert.Contains(all, f => f.EnergyErg < 1e45);   // log-uniform: the faint end and the bright end both occur
        Assert.Contains(all, f => f.EnergyErg > 5e45);
    }

    [Fact]
    public void Flare_SingleRiseNoFlicker()
    {
        var f = new GiantFlare(1e8, 2e46, 2e46 / C6.SpikeS, C6.SpikeS);
        double decadesPerSample = 1e-5;
        var values = new List<double>();
        for (double u = -0.2; u <= 0.3; u += decadesPerSample) values.Add(MagnetarFlares.DrawnIntensity(f, f.SourceTimeS * Math.Pow(10, u)));

        Assert.All(values, v => Assert.InRange(v, 0, 1));
        Assert.Equal(1, values.Max(), 6);                         // the brightest flare reaches the cap, never more
        int maxima = 0;
        for (int i = 1; i < values.Count - 1; i++)
            if (values[i] > values[i - 1] && values[i] >= values[i + 1] && values[i] > 0) maxima++;
        Assert.Equal(1, maxima);
        // One rise then one fade: non-decreasing up to the peak, non-increasing after.
        int peak = values.IndexOf(values.Max());
        for (int i = 1; i <= peak; i++) Assert.True(values[i] >= values[i - 1] - 1e-12);
        for (int i = peak + 1; i < values.Count; i++) Assert.True(values[i] <= values[i - 1] + 1e-12);
        // The rise lasts at least 0.15 s of default playback, and rise plus fade about half a second.
        Assert.True(MagnetarFlares.RisePlaybackS >= 0.15);
        double total = (MagnetarFlares.RiseDecades + MagnetarFlares.FadeDecades) / MagnetarFlares.ReferenceDecadesPerSecond;
        Assert.InRange(total, 0.4, 0.6);
        Assert.Equal(TimelineViewModel.DefaultDecadesPerSecond, MagnetarFlares.ReferenceDecadesPerSecond);
    }

    [Fact]
    public void Flare_BrightnessIsCappedAtOne_AndZeroOutsideTheFlash()
    {
        foreach (double e in new[] { 1.5e44, 1e45, 2e46, 1e48 })
        {
            var f = new GiantFlare(1e6, e, e / C6.SpikeS, C6.SpikeS);
            for (double u = -0.3; u <= 0.4; u += 0.001) Assert.InRange(MagnetarFlares.DrawnIntensity(f, 1e6 * Math.Pow(10, u)), 0, 1);
            Assert.Equal(0, MagnetarFlares.DrawnIntensity(f, 1e6 * 0.5));
            Assert.Equal(0, MagnetarFlares.DrawnIntensity(f, 1e6 * 2));
            Assert.Equal(0, MagnetarFlares.DrawnIntensity(f, 0));
            Assert.Equal(0, MagnetarFlares.DrawnIntensity(f, double.NaN));
        }
    }

    // ---- The gamma-ray panel (spec §7.3) ----

    private static readonly ObserverSetup DefaultObserver = ObserverSetup.Default;

    private static GammaPanelViewModel PanelFor(Outcome o, ObserverSetup obs, int run)
    {
        var panel = new GammaPanelViewModel();
        panel.Update(o, obs, run, Array.Empty<SupernovaLab.Core.Presets.ObservedFact>(), true);
        panel.SelectedSubTab = GammaPanelViewModel.GrbTab;
        panel.SelectedInstrument = panel.Instruments.First(d => d.Id == "gbm");
        return panel;
    }

    [Fact]
    public void FlareModel_AppliesOnlyForAMagnetar_NotNeverObserved_AndIsSpeculative()
    {
        var m = new MagnetarFlareModel();
        Assert.True(m.Applies(Magnetar()));
        Assert.False(m.Applies(Default));
        Assert.False(m.Applies(TestOutcomes.Collapsar()));
        Assert.Equal(Messenger.GammaRays, m.Messenger);
        Assert.Equal(Confidence.Speculative, m.Confidence);
        // Giant flares have been observed (1979, 1998, 2004): the "Expected" wording is not used for them.
        foreach (string? channel in new string?[] { null, "spike", "detect@gbm" })
            Assert.False(NeverObserved.Applies(m.Id, channel, Magnetar()));
        Assert.Equal(DetectionStatus.NotProduced, m.Detect(Default, DefaultObserver, 1e8, Detectors.Get("gbm")).Status);
    }

    [Fact]
    public void GammaPanel_ShowsFlareSpikeAtFlareTime()
    {
        var o = Magnetar();
        int run = RunWithOneFlare(o);
        var flare = MagnetarFlares.For(o, run).Single();
        var panel = PanelFor(o, DefaultObserver, run);

        var dots = panel.Series.Where(s => s.IsDots).ToList();
        Assert.Single(dots);
        Assert.Equal(flare.SourceTimeS * DefaultObserver.TimeDilation, dots[0].ObserverTimesS[0], 6);
        double expectedFlux = flare.PeakLuminosityErgS / (4 * Math.PI * Math.Pow(DefaultObserver.LuminosityDistanceCm, 2));
        Assert.Equal(expectedFlux, dots[0].Values[0], 12);
        Assert.Equal("erg cm⁻² s⁻¹", dots[0].Unit);                       // the GRB tab's own units
        Assert.True(panel.LogY);

        // The detection logic runs through the panel's instruments: at the spike, GBM and BAT judge it.
        panel.OnTimeChanged(flare.SourceTimeS * DefaultObserver.TimeDilation + 0.1);
        Assert.Equal(DetectionStatus.Detected, panel.CurrentDetection.Status);
        Assert.Equal("Detected", panel.DetectionText);               // not "Expected": the flare has been observed
        Assert.Contains("Fermi GBM", panel.CurrentDetection.Reason);
        panel.SelectedInstrument = panel.Instruments.First(d => d.Id == "bat");
        Assert.Equal(DetectionStatus.Detected, panel.CurrentDetection.Status);
        Assert.Contains(MagnetarFlareModel.AssumedPeakEnergyKeV.ToString("0"), panel.CurrentDetection.Reason);

        // Long afterwards the spike is over, and the badge says so.
        panel.OnTimeChanged(Math.Min(TimeAxis.MaxS, flare.SourceTimeS * DefaultObserver.TimeDilation * 3));
        Assert.StartsWith("Over", panel.DetectionText);
        // Before it: not yet.
        panel.OnTimeChanged(flare.SourceTimeS * DefaultObserver.TimeDilation / 10);
        Assert.Equal(DetectionStatus.NotYet, panel.CurrentDetection.Status);
    }

    [Fact]
    public void GammaPanel_FarAway_TheSpikeIsTooFaintForTheInstruments()
    {
        var o = Magnetar();
        int run = RunWithOneFlare(o);
        var flare = MagnetarFlares.For(o, run).Single();
        var far = new ObserverSetup(1e9, 0, true);                    // 1 Gpc
        var model = new MagnetarFlareModel(run);
        var gbm = model.Detect(o, far, flare.SourceTimeS + 0.1, Detectors.Get("gbm"));
        Assert.Equal(DetectionStatus.TooFaint, gbm.Status);
        Assert.Equal(DetectionStatus.TooFaint, model.Detect(o, far, flare.SourceTimeS + 0.1, Detectors.Get("bat")).Status);
        // The spike's flux falls as 1/D²: 1 Gpc against 10 kpc is 10¹⁰ times fainter, redshift apart.
        double near = model.Sample(o, DefaultObserver, flare.SourceTimeS + 0.1, "spike");
        double farFlux = model.Sample(o, far, flare.SourceTimeS + 0.1, "spike");
        Assert.True(near / farFlux > 1e9);
    }

    [Fact]
    public void GammaPanel_FlareSampling_IsZeroBetweenFlaresAndAtTheSpikeOnly()
    {
        var o = Magnetar();
        int run = RunWithOneFlare(o);
        var f = MagnetarFlares.For(o, run).Single();
        var m = new MagnetarFlareModel(run);
        Assert.Equal(0, m.Sample(o, DefaultObserver, f.SourceTimeS - 1, "spike"));
        Assert.True(m.Sample(o, DefaultObserver, f.SourceTimeS, "spike") > 0);
        Assert.True(m.Sample(o, DefaultObserver, f.SourceTimeS + f.SpikeS * 0.99, "spike") > 0);
        Assert.Equal(0, m.Sample(o, DefaultObserver, f.SourceTimeS + f.SpikeS * 1.01, "spike"));
        Assert.True(double.IsNaN(m.Sample(Default, DefaultObserver, f.SourceTimeS, "spike")));
        Assert.Throws<ArgumentException>(() => m.Sample(o, DefaultObserver, 1e8, "nope"));
    }

    [Fact]
    public void Flare_ObserverTimeStretchedByRedshift()
    {
        var o = Magnetar();
        var obs = new ObserverSetup(1e9, 0, true);
        Assert.True(obs.TimeDilation > 1.1);
        int run = RunWithOneFlare(o);
        var flare = MagnetarFlares.For(o, run).Single();
        var markers = EventMarkers.For(o, obs, run);
        var marker = Assert.Single(markers, m => m.GlossaryKey == nameof(Glossary.GiantFlare));
        Assert.Equal("Giant flare", marker.Label);
        Assert.Equal(flare.SourceTimeS * obs.TimeDilation, marker.ObserverTimeS, 6);
        Assert.Contains(MagnetarFlares.Comparison2004, marker.Basis);
        Assert.Contains("Speculative", marker.Basis);
        Assert.Contains("compact §C6", marker.Basis);
        // The panel's spike sits at the same observer time.
        var panel = PanelFor(o, obs, run);
        Assert.Equal(marker.ObserverTimeS, panel.Series.Single(s => s.IsDots).ObserverTimesS[0], 6);
        // Not a magnetar, or no run given: no marker.
        Assert.DoesNotContain(EventMarkers.For(Default, obs, run), m => m.GlossaryKey == nameof(Glossary.GiantFlare));
        Assert.DoesNotContain(EventMarkers.For(o, obs), m => m.GlossaryKey == nameof(Glossary.GiantFlare));
    }

    // ---- The magnetar's spin, field and disclosure ----

    [Fact]
    public void Magnetar_PeriodReachesSeconds_ShownAtTheRealRateOrSlowedBySlowdownLabel()
    {
        // R-P3-3: only the slow-rotation magnetar (5 × 10¹⁴ G, 91 ms) reaches seconds (1.39 s at 1,000 yr, §C6), still under the 2 s floor.
        var slow = Magnetar(C6.SlowBirthPeriodS, C6.SlowFieldG);
        var st = SceneFor(slow).At(1000 * Year);
        double period = st.NeutronStar!.PeriodS;
        Assert.InRange(period, 1, 20);
        Assert.Equal(C6.SlowPeriodAt1000yrS, period, 1);
        Assert.Equal(Magnetosphere.SlowdownLabel(period, 1), st.SpeedLabel);
        Assert.Equal("Shown 1.4× slower", st.SpeedLabel);               // 1.39 s < 2 s: slowed 1.4×, not "at real speed"
        // At 2.5 s and over, real speed.
        Assert.Equal("Shown at real speed", SceneFor(Magnetar(2.5, 1e15)).At(100 * Year).SpeedLabel);
    }

    [Fact]
    public void FastMagnetars_StayNearTwoTenthsOfASecond()
    {
        double fast = SceneFor(Magnetar(C6.FastBirthPeriodS, C6.FastFieldG)).At(1000 * Year).NeutronStar!.PeriodS;
        Assert.Equal(C6.FastPeriodAt1000yrS, fast, 2);
    }

    [Theory]
    [InlineData(C6.FastBirthPeriodS, C6.FastFieldG)]
    [InlineData(C6.SlowBirthPeriodS, C6.SlowFieldG)]
    public void FastMagnetars_DiscloseInconsistency57_OnScreen_NotByPreset(double birthS, double fieldG)
    {
        var o = Magnetar(birthS, fieldG);
        Assert.False(o.Discloses(CloseUpScene.MagnetarSpinInconsistency));    // no preset's pin involved
        var st = SceneFor(o).At(1000 * Year);
        Assert.True(st.NeutronStar!.PeriodS < Magnetosphere.MinDrawnPeriodS);
        string note = Assert.Single(st.Notes, n => n.Contains("#57"));
        Assert.Contains("2–12 s", note);
        Assert.Contains("Olausen", note);
        var spin = st.Readouts.Single(r => r.Label == "Spin");
        Assert.Contains("#57", spin.ToolTip);
        Assert.Contains("2–12 s", spin.ToolTip);
    }

    [Fact]
    public void Magnetar_AtObservedPeriods_AndOtherStars_DoNotDisclose57()
    {
        var slowStar = SceneFor(Magnetar(2.5, 1e15)).At(100 * Year);
        Assert.DoesNotContain(slowStar.Notes, n => n.Contains("#57"));
        Assert.DoesNotContain("#57", slowStar.Readouts.Single(r => r.Label == "Spin").ToolTip);
        // A fast pulsar that is not a magnetar says nothing about magnetar periods.
        var pulsar = SceneFor(TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.07, 3e13, TestOutcomes.Pulsar(0.03, 3e12))).At(100 * Year);
        Assert.DoesNotContain(pulsar.Notes, n => n.Contains("#57"));
        Assert.DoesNotContain(pulsar.Notes, n => n.Contains("Giant flare"));
    }

    [Fact]
    public void Magnetar_FieldComparisonFromModelValue()
    {
        Assert.Equal("about a trillion times a fridge magnet's field (about 100 G)", Comparisons.ForMagneticField(1e14));
        Assert.Contains("5 trillion", Comparisons.ForMagneticField(5e14));
        Assert.Contains("10 trillion", Comparisons.ForMagneticField(1e15));
        Assert.Contains("10 billion", Comparisons.ForMagneticField(1e12));     // an ordinary pulsar
        Assert.Contains("times a fridge", Comparisons.ForMagneticField(50));
        Assert.Equal("no field to compare", Comparisons.ForMagneticField(double.NaN));

        // The close-up's Field readout uses the outcome's own value, and says the decay is not modelled (§C6).
        var st = SceneFor(Magnetar(2.5, 5e14)).At(100 * Year);
        var field = st.Readouts.Single(r => r.Label == "Field");
        Assert.Equal(5e14, field.Value.Value);
        Assert.Contains(Comparisons.ForMagneticField(5e14), field.ToolTip);
        Assert.Contains(CloseUpScene.FieldDecayNote, field.ToolTip);
        Assert.Contains(CloseUpScene.FieldDecayNote, st.Notes);
        Assert.Contains("10,000 years", CloseUpScene.FieldDecayNote);
        Assert.Contains("Beniamini", CloseUpScene.FieldDecayNote);
        Assert.Equal(C6.FieldDecayYr, 1e4);
        // Another star's field has no magnetar comparison.
        var ordinary = SceneFor(Default).At(100 * Year).Readouts.Single(r => r.Label == "Field");
        Assert.DoesNotContain("fridge", ordinary.ToolTip);
    }

    // ---- The close-up and the main view draw the flare ----

    [Fact]
    public void CloseUp_ADrawnFlare_IsOnlyForAMagnetar_AtTheFlaresTime_AndNoted()
    {
        var o = Magnetar();
        int run = RunWithOneFlare(o);
        var f = MagnetarFlares.For(o, run).Single();
        var scene = SceneFor(o, run);

        var during = scene.At(f.SourceTimeS);
        Assert.InRange(during.FlareIntensity, 0.5, 1);
        Assert.Equal(f, during.Flare);
        Assert.Equal(MagnetarFlares.DrawnIntensity(f, f.SourceTimeS), during.FlareIntensity);
        Assert.Contains(during.Notes, n => n.StartsWith("Giant flare (speculative)") && n.Contains(MagnetarFlares.Comparison2004));
        Assert.Equal(0, scene.At(f.SourceTimeS * 2).FlareIntensity);
        Assert.Equal(0, scene.At(f.SourceTimeS / 2).FlareIntensity);
        Assert.Null(scene.At(f.SourceTimeS * 2).Flare);
        Assert.DoesNotContain(scene.At(f.SourceTimeS * 2).Notes, n => n.StartsWith("Giant flare (speculative): a spike"));
        // The standing note says what this run draws.
        Assert.Contains(scene.At(f.SourceTimeS * 2).Notes, n => n.StartsWith("Giant flares (speculative)") && n.Contains("1 in the timeline"));
        // Another run has other flares; a non-magnetar has none.
        Assert.Equal(0, SceneFor(o, run + 1).At(f.SourceTimeS).FlareIntensity);
        Assert.Equal(0, SceneFor(Default, run).At(f.SourceTimeS).FlareIntensity);
    }

    [Fact]
    public void MainView_TheMarkerGlowsWithTheFlare_ForAMagnetarOnly()
    {
        var o = Magnetar();
        int run = RunWithOneFlare(o);
        var f = MagnetarFlares.For(o, run).Single();
        var scene = new ExplosionScene(o, runNumber: run);
        var during = scene.At(f.SourceTimeS);
        Assert.InRange(during.FlareIntensity, 0.5f, 1f);
        Assert.Equal((float)MagnetarFlares.DrawnIntensity(f, f.SourceTimeS), during.FlareIntensity);
        Assert.Equal(0f, scene.At(f.SourceTimeS * 3).FlareIntensity);
        scene.RunNumber = run + 1;                                    // Re-roll redraws the flares
        Assert.Equal(0f, scene.At(f.SourceTimeS).FlareIntensity);
        Assert.Equal(0f, new ExplosionScene(Default, runNumber: run).At(f.SourceTimeS).FlareIntensity);
    }

    [Fact]
    public void ARunningFlare_KeepsTheOnDemandViewDrawing()
    {
        var o = Magnetar();
        int run = RunWithOneFlare(o);
        var f = MagnetarFlares.For(o, run).Single();
        var scene = SceneFor(o, run);
        Assert.True(SupernovaLab.App.Controllers.CloseUpSceneController.FlareKeepsDrawing(scene.At(f.SourceTimeS)));
        Assert.False(SupernovaLab.App.Controllers.CloseUpSceneController.FlareKeepsDrawing(scene.At(f.SourceTimeS * 3)));
    }

    [Fact]
    public void Reroll_MovesTheFlareMarkers_AndOnlyAMagnetarPanelResamples()
    {
        var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        var gamma = vm.Panels.OfType<GammaPanelViewModel>().Single();
        Assert.False(gamma.UsesRunNumber);                                 // a default star: Project 1's panel is untouched
        vm.Lab.SelectedPreset = SupernovaLab.Core.Presets.PresetCatalog.Get("sn2015bn");
        Assert.Equal(RemnantKind.Magnetar, vm.CurrentOutcome.Remnant.Kind);
        Assert.True(gamma.UsesRunNumber);
        gamma.SelectedSubTab = GammaPanelViewModel.GrbTab;
        var seen = new HashSet<string>();
        bool any = false;
        for (int i = 0; i < 12; i++)
        {
            var flares = MagnetarFlares.For(vm.CurrentOutcome, vm.RunNumber)
                .Where(f => f.SourceTimeS * vm.CurrentObserver.TimeDilation <= TimeAxis.MaxS).ToList();   // later ones fall off the axis
            var markers = vm.Timeline.Markers.Where(m => m.GlossaryKey == nameof(Glossary.GiantFlare)).ToList();
            Assert.Equal(flares.Count, markers.Count);
            for (int k = 0; k < flares.Count; k++)
                Assert.Equal(flares[k].SourceTimeS * vm.CurrentObserver.TimeDilation, markers[k].ObserverTimeS, 6);
            Assert.Equal(markers.Count, gamma.Series.Where(s => s.IsDots && s.ChannelId == "spike").Sum(s => s.Values.Length));   // a spike per marker
            any |= flares.Count > 0;
            seen.Add(string.Join(",", flares.Select(f => f.SourceTimeS)));
            vm.RerollCommand.Execute(null);
        }
        Assert.True(any);
        Assert.True(seen.Count > 6, "Re-roll draws different flares");
    }

    [Fact]
    public void LinesTab_PointsToTheGiantFlares_OnlyWhenThisRunHasOne()
    {
        var o = Magnetar();
        int run = RunWithOneFlare(o);
        var panel = new GammaPanelViewModel();
        panel.Update(o, DefaultObserver, run, Array.Empty<SupernovaLab.Core.Presets.ObservedFact>(), true);
        panel.SelectedSubTab = GammaPanelViewModel.LinesTab;
        Assert.Contains(GammaPanelViewModel.FlaresPointer, panel.PanelNote);
        int none = Enumerable.Range(1, 200).First(r => MagnetarFlares.For(o, r).Count == 0);
        panel.Update(o, DefaultObserver, none, Array.Empty<SupernovaLab.Core.Presets.ObservedFact>(), true);
        Assert.DoesNotContain(GammaPanelViewModel.FlaresPointer, panel.PanelNote);
    }

    [Fact]
    public void Glossary_ExplainsTheGiantFlare()
    {
        Assert.Contains("1979", Glossary.GiantFlare);
        Assert.Contains("speculative", Glossary.GiantFlare);
    }
}
