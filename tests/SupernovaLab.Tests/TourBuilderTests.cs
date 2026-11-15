using SupernovaLab.App.Tour;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>The guided tour's model (Project 2, Task 12; spec criterion 4; ruling R-P2-6: stop times are observer times).</summary>
public class TourBuilderTests
{
    private static Outcome ForPreset(Preset p) => TestOutcomes.Engine.Compute(p.Inputs, p.Pins);

    /// <summary>One outcome per supernova type (hand-built), and the engine's own outcome for each fate it reaches.</summary>
    public static IEnumerable<object[]> Outcomes()
    {
        foreach (var type in Enum.GetValues<SupernovaType>().Where(t => t != SupernovaType.None))
        {
            bool thermonuclear = type is SupernovaType.Ia or SupernovaType.Ia91T or SupernovaType.Ia91bg or SupernovaType.Iax;
            yield return new object[]
            {
                $"type {type}", TestOutcomes.Custom(type, 1e51, thermonuclear ? 1.4 : 10, 0.07, thermonuclear ? 1e9 : 3e13),
            };
        }
        yield return new object[] { "default", TestOutcomes.Engine.Compute(LabInputs.Default) };
        yield return new object[] { "Ia", TestOutcomes.TypeIa() };
        yield return new object[] { "collapsar", TestOutcomes.Collapsar() };
        foreach (var (name, fate) in FateExamples())
            yield return new object[] { $"fate {name}", fate };
    }

    private static IEnumerable<(string, Outcome)> FateExamples()
    {
        var wanted = new HashSet<Fate>
        {
            Fate.NotDeadYet, Fate.WhiteDwarf, Fate.FailedSupernova, Fate.PairInstabilitySupernova, Fate.PulsationalPairInstability,
        };
        // Pair instability needs a very massive star at low metal content, so sweep both.
        foreach (double z in new[] { 1.0, 0.001 })
            for (double m = LabInputs.MinInitialMass; m <= LabInputs.MaxInitialMass && wanted.Count > 0; m *= 1.03)
            {
                var o = TestOutcomes.Engine.Compute(LabInputs.Default with { InitialMass = m, Metallicity = z });
                if (wanted.Remove(o.Fate)) yield return (o.Fate.ToString(), o);
            }
        Assert.Empty(wanted); // every no-debris fate must have an example here
    }

    public static IEnumerable<object[]> Presets() => PresetCatalog.LoadEmbedded().Select(p => new object[] { p.Id });

    private static void AssertValidTour(IReadOnlyList<TourStop> tour, ObserverSetup obs, string what)
    {
        Assert.True(tour.Count >= 1, $"{what}: no stops");
        Assert.Equal(TimeAxis.MinS, tour[0].ObserverTimeS);
        for (int i = 0; i < tour.Count; i++)
        {
            var s = tour[i];
            Assert.False(string.IsNullOrWhiteSpace(s.Caption), $"{what}: stop {i} has no caption");
            Assert.DoesNotContain("NaN", s.Caption);
            Assert.DoesNotContain("∞", s.Caption);
            Assert.True(s.ObserverTimeS >= TimeAxis.MinS && s.ObserverTimeS <= TimeAxis.MaxS, $"{what}: stop {i} at {s.ObserverTimeS}");
            Assert.Equal(4, s.DwellS);
            if (i > 0)
            {
                Assert.True(s.ObserverTimeS > tour[i - 1].ObserverTimeS, $"{what}: stop {i} is not after stop {i - 1}");
                Assert.True(Math.Log10(s.ObserverTimeS / tour[i - 1].ObserverTimeS) >= TourBuilder.MergeDecades - 1e-12,
                    $"{what}: stops {i - 1} and {i} are closer than {TourBuilder.MergeDecades} decades");
            }
            Assert.Equal(s.Cutaway, s.Sways);
        }
    }

    [Theory]
    [MemberData(nameof(Outcomes))]
    public void EveryOutcomeType_GetsATour_StartingAt1ms_InTimeOrder(string what, Outcome o)
    {
        var tour = TourBuilder.For(o, ObserverSetup.Default, null);
        AssertValidTour(tour, ObserverSetup.Default, what);
        if (TourCaptions.IsThermonuclear(o) && o.Fate == Fate.Supernova) Assert.Equal("Ignition", tour[0].Caption);
        if (o.Fate == Fate.Supernova && !TourCaptions.IsThermonuclear(o)) Assert.Equal("Core collapse", tour[0].Caption);
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void EveryPreset_GetsATour_StartingAt1ms_InTimeOrder(string id)
    {
        var p = PresetCatalog.Get(id);
        var tour = TourBuilder.For(ForPreset(p), p.Observer, p);
        AssertValidTour(tour, p.Observer, id);
    }

    [Fact]
    public void ADefaultStar_StopsAtBounce_Neutrinos_Breakout_AndPeakLight_WithTheRightFlags()
    {
        var o = TestOutcomes.Engine.Compute(LabInputs.Default);
        var obs = ObserverSetup.Default;
        var tour = TourBuilder.For(o, obs, null);
        double breakout = EjectaKinematics.BreakoutTimeS(o) * obs.TimeDilation;

        Assert.Equal("Core collapse", tour[0].Caption);
        Assert.True(tour[0].Cutaway); // 1 ms is between bounce and breakout
        Assert.True(tour[0].Sways);

        var neutrino = Assert.Single(tour, s => s.Highlight == Messenger.Neutrinos);
        Assert.Equal(EventMarkers.NeutrinoAndGwTimeS, neutrino.ObserverTimeS);
        Assert.True(neutrino.Cutaway);

        var bo = Assert.Single(tour, s => s.Highlight == Messenger.XRaysUv);
        Assert.Equal(breakout, bo.ObserverTimeS);
        Assert.False(bo.Cutaway); // at breakout the star is gone: not "between"
        Assert.False(bo.Sways);

        // Ruling R-P2-33 (I7): the default star's debris have mixing fingers, so the peak-light stop is cut away with the orbit and
        // says what the slice shows; every other post-breakout stop is not.
        var peak = Assert.Single(tour, s => s.Highlight == Messenger.VisibleLight);
        Assert.True(peak.ObserverTimeS > breakout);
        Assert.True(peak.Cutaway);
        Assert.True(peak.Sways);
        Assert.Contains("In the lab, a slice through the debris shows the mixing fingers", peak.Caption);
        Assert.All(tour, s => Assert.Equal(s.ObserverTimeS < breakout || ReferenceEquals(s, peak), s.Cutaway));
    }

    [Fact]
    public void CaptionNumbers_EqualTheModelValues()
    {
        var p = PresetCatalog.Get("sn1987a"); // a preset whose breakout lies inside the timeline
        var o = ForPreset(p);
        var tour = TourBuilder.For(o, p.Observer, p);
        string expected = UnitFormat.Time(EjectaKinematics.BreakoutTimeS(o) * (1 + p.Observer.Redshift));
        var bo = Assert.Single(tour, s => s.Caption.Contains("breaks out"));
        Assert.Contains(expected, bo.Caption);

        var d = TestOutcomes.Engine.Compute(LabInputs.Default);
        var tourD = TourBuilder.For(d, ObserverSetup.Default, null);
        Assert.Contains(UnitFormat.Time(EjectaKinematics.BreakoutTimeS(d)), Assert.Single(tourD, s => s.Highlight == Messenger.XRaysUv).Caption);
    }

    [Fact]
    public void RedshiftedObserver_TourTimesAreObserverTimes()
    {
        var o = TestOutcomes.Engine.Compute(LabInputs.Default);
        var near = ObserverSetup.Default;
        // z = 1: comoving distance of the lab's cosmology at z = 1, found from the model itself.
        var far = new ObserverSetup(DistanceForRedshift(1.0), 0, true);
        Assert.InRange(far.Redshift, 0.999, 1.001);

        var tNear = TourBuilder.For(o, near, null);
        var tFar = TourBuilder.For(o, far, null);
        // The first stop is the timeline's start, not dilated; the neutrino burst, breakout and peak light are the near
        // observer's stops later by exactly 1 + z (the radio peak and the resolvable remnant also depend on the grid and the distance).
        Assert.Equal(TimeAxis.MinS, tFar[0].ObserverTimeS);
        foreach (var m in new[] { Messenger.Neutrinos, Messenger.XRaysUv, Messenger.VisibleLight })
        {
            var n = tNear.First(s => s.Highlight == m);
            var f = tFar.First(s => s.Highlight == m);
            Assert.Equal(n.ObserverTimeS * far.TimeDilation, f.ObserverTimeS, 6);
        }
        Assert.Equal(0.02, tFar.First(s => s.Highlight == Messenger.Neutrinos).ObserverTimeS, 9);
        double breakoutFar = EjectaKinematics.BreakoutTimeS(o) * far.TimeDilation;
        Assert.Contains(UnitFormat.Time(breakoutFar), Assert.Single(tFar, s => s.Highlight == Messenger.XRaysUv).Caption);
    }

    [Fact]
    public void PresetLines_AreMergedByTime_AndUseTheObserversDilation()
    {
        var p = PresetCatalog.Get("sn1987a");
        var o = ForPreset(p);
        var with = TourBuilder.For(o, p.Observer, p);
        var without = TourBuilder.For(o, p.Observer, null);
        Assert.True(with.Count > without.Count);
        var line = p.TourLines.First(l => l.Text.Contains("hidden neutron star"));
        Assert.Contains(with, s => s.Caption.Contains(line.Text) && Math.Abs(s.ObserverTimeS / (line.SourceTimeS * p.Observer.TimeDilation) - 1) < 1e-9);

        // A line outside the timeline is left out; with z = 1 a late line moves later by exactly 2.
        var far = new ObserverSetup(DistanceForRedshift(1.0), 0, true);
        var test = new Preset("t", "t", "s", "w", p.Inputs, far, p.Pins, p.Facts)
        {
            TourLines = new[] { new TourLine(1e6, "A day-12 line.", new Source("c", "r"), Messenger.Radio) },
        };
        var tour = TourBuilder.For(o, far, test);
        var stop = Assert.Single(tour, s => s.Caption.Contains("A day-12 line."));
        Assert.Equal(1e6 * far.TimeDilation, stop.ObserverTimeS, 6);
    }

    [Fact]
    public void TwoStopsCloserThan005Decades_MergeIntoOne_KeepingTheEarlierTime()
    {
        var o = TestOutcomes.Engine.Compute(LabInputs.Default);
        var obs = ObserverSetup.Default;
        var marker = TourBuilder.For(o, obs, null).First(s => s.Highlight == Messenger.VisibleLight);
        var test = new Preset("t", "t", "s", "w", LabInputs.Default, obs, null, Array.Empty<ObservedFact>())
        {
            TourLines = new[]
            {
                new TourLine(marker.ObserverTimeS * 1.05, "Nearby line.", new Source("c", "r"), Messenger.Radio),
                new TourLine(marker.ObserverTimeS * 1.5, "Far line.", new Source("c", "r"), null),
            },
        };
        var tour = TourBuilder.For(o, obs, test);
        var merged = Assert.Single(tour, s => s.Caption.Contains("Nearby line."));
        Assert.Equal(marker.ObserverTimeS, merged.ObserverTimeS);
        Assert.Equal(Messenger.VisibleLight, merged.Highlight); // the first non-null highlight, in time order
        Assert.Contains(marker.Caption, merged.Caption);
        Assert.NotEqual(merged.ObserverTimeS, Assert.Single(tour, s => s.Caption.Contains("Far line.")).ObserverTimeS);
    }

    [Fact]
    public void NoDebrisOutcomes_NameTheStarsOwnFate()
    {
        var notYet = TestOutcomes.Star(LabInputs.MinInitialMass);
        if (notYet.Fate == Fate.NotDeadYet)
            Assert.Contains("main sequence", TourBuilder.For(notYet, ObserverSetup.Default, null)[0].Caption);
        foreach (var (name, o) in FateExamples().Where(f => f.Item2.Fate is Fate.WhiteDwarf or Fate.PairInstabilitySupernova or Fate.PulsationalPairInstability))
        {
            var first = TourBuilder.For(o, ObserverSetup.Default, null)[0];
            if (o.Fate == Fate.WhiteDwarf) Assert.False(first.Cutaway, name); // no star left to cut away
            Assert.NotEqual("Core collapse", first.Caption);
        }
    }

    // ---------------------------------------------------------------- fix round 1 (sources, wording, ruling R-P2-31)

    [Fact]
    public void AMergedStop_SaysWhichSentenceIsObserved_AndWhichIsTheLabs_InOrder()
    {
        var p = PresetCatalog.Get("sn1987a");
        var o = ForPreset(p);
        var tour = TourBuilder.For(o, p.Observer, p);
        var peak = Assert.Single(tour, s => s.Caption.Contains("V = 2.975"));
        int observed = peak.Caption.IndexOf("Observed:", StringComparison.Ordinal);
        int lab = peak.Caption.IndexOf("In the lab,", StringComparison.Ordinal);
        Assert.True(observed >= 0 && lab > observed, peak.Caption);

        // The model's peak magnitude is pinned to the light-curve model at the stop's own time.
        double v = new LightCurveModel().Sample(o, p.Observer, peak.ObserverTimeS / p.Observer.TimeDilation, "appV");
        // The observed line, the lab's peak and (ruling R-P2-33, I7) the lab's mixing slice, in caption order.
        Assert.Equal(3, peak.Notes.Count);
        Assert.Equal(TourSourceKind.ObservedChecked, peak.Notes[0].Kind);
        Assert.Equal(TourSourceKind.LabModel, peak.Notes[1].Kind);
        Assert.Equal(TourSourceKind.LabModel, peak.Notes[2].Kind);
        var model = TourBuilder.For(o, p.Observer, null).First(s => s.Highlight == Messenger.VisibleLight);
        double vModel = new LightCurveModel().Sample(o, p.Observer, model.ObserverTimeS / p.Observer.TimeDilation, "appV");
        Assert.Contains($"at about V = {vModel.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} as seen from here", peak.Caption);
        Assert.True(double.IsFinite(v));
    }

    [Fact]
    public void EveryStop_ListsTheSourceAndConfidenceOfItsSentences()
    {
        var p = PresetCatalog.Get("sn1987a");
        var tour = TourBuilder.For(ForPreset(p), p.Observer, p);
        Assert.Empty(tour[0].Notes); // "Core collapse" has no number
        Assert.All(tour.Skip(1), s =>
        {
            Assert.NotEmpty(s.Notes);
            Assert.Equal(s.Notes.Count, s.Sources.Count);
            Assert.All(s.Notes, n => Assert.Contains(n.Source.Citation, s.SourceText));
            Assert.Equal(s.Notes.Count, s.Caption.Split(new[] { "Observed:", "In the lab,", "Expected:" }, StringSplitOptions.None).Length - 1);
        });
        var photos = Assert.Single(tour, s => s.Caption.Contains("first photographs"));
        Assert.Contains("(recalled)", photos.Caption);
        Assert.Equal(TourSourceKind.ObservedRecalled, Assert.Single(photos.Notes).Kind);
        Assert.Contains("Observed (recalled, not re-checked)", photos.SourceText);
        var neutrinos = Assert.Single(tour, s => s.Caption.Contains("24 neutrinos"));
        Assert.DoesNotContain("(recalled)", neutrinos.Caption);
        Assert.Contains("checked; see source", neutrinos.SourceText);
        // Model sentences say they are the lab's.
        Assert.StartsWith("In the lab,", Assert.Single(tour, s => s.Caption.Contains("breaks out")).Caption);
    }

    [Fact]
    public void ModelCaptions_UseTheModelsOwnConstants()
    {
        var o = TestOutcomes.Engine.Compute(LabInputs.Default);
        var tour = TourBuilder.For(o, ObserverSetup.Default, null);
        Assert.Contains($"line at {GammaLineModel.Co56MainLineKeV:0} keV", Assert.Single(tour, s => s.Caption.Contains("gamma-ray line")).Caption);
        Assert.Contains("at least 0.5 arcseconds", Assert.Single(tour, s => s.Caption.Contains("arcseconds")).Caption);
        Assert.Equal(0.5, RemnantEvolutionModel.ResolvableArcsec);
    }

    [Fact]
    public void SN2023ixfDelays_AreMeasuredFromTheLabsBreakout_NotFromBounce()
    {
        var p = PresetCatalog.Get("sn2023ixf");
        var o = ForPreset(p);
        double breakout = EjectaKinematics.BreakoutTimeS(o) * p.Observer.TimeDilation;
        var tour = TourBuilder.For(o, p.Observer, p);
        foreach (var line in p.TourLines.Where(l => l.FromBreakout))
        {
            double expected = (EjectaKinematics.BreakoutTimeS(o) + line.SourceTimeS) * p.Observer.TimeDilation;
            Assert.Contains(tour, s => s.Caption.Contains(line.Text.TrimEnd('.').Split(" (")[0]) && Math.Abs(s.ObserverTimeS / expected - 1) < 1e-9);
            Assert.True(expected > breakout);
        }
        Assert.Equal(2, p.TourLines.Count(l => l.FromBreakout));
    }

    /// <summary>The distance whose redshift is z, by bisection on the observer model.</summary>
    private static double DistanceForRedshift(double z)
    {
        double lo = 1e8, hi = 1e11;
        for (int i = 0; i < 80; i++)
        {
            double mid = Math.Sqrt(lo * hi);
            if (new ObserverSetup(mid, 0, true).Redshift < z) lo = mid; else hi = mid;
        }
        return Math.Sqrt(lo * hi);
    }

    [Theory]
    [InlineData("The shock reaches the surface.", "In the lab, the shock reaches the surface.")]
    [InlineData("GRB jets are drawn.", "In the lab, GRB jets are drawn.")]   // an acronym keeps its capitals
    [InlineData("A neutron star forms.", "In the lab, a neutron star forms.")]
    [InlineData("X", "In the lab, x")]
    public void InTheLab_LowercasesOnlyAWordsFirstLetter(string sentence, string expected)
        => Assert.Equal(expected, TourCaptions.InTheLab(sentence));

    [Fact]
    public void CheckedSources_AreNotSaidToBeAllPrimaryPapers()
    {
        var note = new TourSourceNote(TourSourceKind.ObservedChecked, new Source("c", "presets §1"));
        Assert.Equal("Observed (checked; see source)", note.KindText);
    }
}
