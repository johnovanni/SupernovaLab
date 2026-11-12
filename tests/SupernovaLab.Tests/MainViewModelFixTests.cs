using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Text;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>Task 14 review fixes: I1 (badge tooltips), I3 (texts), R49 (observer edits) and M2–M12.</summary>
public class MainViewModelFixTests
{
    private static MainViewModel NewViewModel() => new(TestOutcomes.Engine, new FakeWindowService());

    private static MainViewModel WithPreset(string id)
    {
        var vm = NewViewModel();
        vm.Lab.SelectedPreset = PresetCatalog.Get(id);
        return vm;
    }

    // ---------------------------------------------------------------- I1: every badge's tooltip names the source

    [Theory]
    [InlineData(null)]
    [InlineData("sn1987a")]
    [InlineData("sn2011fe")]
    [InlineData("n6946bh1")]
    public void EveryRow_BadgeToolTip_GivesTheConfidenceAndTheSourceCitation(string? presetId)
    {
        var vm = presetId is null ? NewViewModel() : WithPreset(presetId);
        var rows = vm.Outcome.KeyNumbers.Concat(vm.Outcome.DetailRows).Concat(vm.Outcome.CurveRows).ToList();
        Assert.NotEmpty(vm.Outcome.KeyNumbers);

        Assert.All(rows, r =>
        {
            Assert.Contains(r.Citation, r.BadgeToolTip);
            Assert.Contains(r.ResearchRef, r.BadgeToolTip);
            Assert.StartsWith(r.Confidence switch
            {
                Confidence.Established => "Established",
                Confidence.ModelDependent => "Model-dependent",
                _ => "Speculative",
            }, r.BadgeToolTip);
            Assert.Contains(r.Citation, r.PublishedToolTip);
        });
    }

    /// <summary>The badge template itself: its tooltip is the row's text (the old MultiBinding produced nothing).</summary>
    [Fact]
    public void ConfidenceAndPublishedBadgeTemplates_ShowTheSourceAsTheirToolTip()
    {
        var row = WithPreset("sn1987a").Outcome.KeyNumbers.First(r => r.Published);
        object? confidenceTip = null, publishedTip = null;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var theme = (ResourceDictionary)Application.LoadComponent(
                    new Uri("/SupernovaLab.App;component/Resources/Theme.xaml", UriKind.Relative));
                var badge = (FrameworkElement)((DataTemplate)theme["ConfidenceBadge"]).LoadContent();
                badge.DataContext = row;
                var published = (FrameworkElement)((DataTemplate)theme["PublishedBadge"]).LoadContent();
                published.DataContext = row;
                // Bindings activate at DataBind priority: let the dispatcher run them.
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { },
                    System.Windows.Threading.DispatcherPriority.ContextIdle);
                confidenceTip = badge.ToolTip;
                publishedTip = published.ToolTip;
            }
            catch (Exception e)
            {
                error = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(error);
        Assert.Equal(row.BadgeToolTip, confidenceTip);
        Assert.Equal(row.PublishedToolTip, publishedTip);
        Assert.Contains("Blinnikov", (string)confidenceTip!);
    }

    // ---------------------------------------------------------------- R49: observer edits keep the explosion pins

    [Fact]
    public void DistanceEdit_KeepsTheExplosionPins_LeavesThePublishedObserver_AndSaysSo()
    {
        var vm = WithPreset("sn1987a");
        Assert.True(vm.Lab.IsObserverPublished);
        int raised = 0;
        vm.OutcomeChanged += (_, _) => raised++;

        vm.Lab.DistancePc = 780_000; // SN 1987A seen from Andromeda

        Assert.Equal(1, raised);
        Assert.True(vm.CurrentOutcome.UsesPublishedValues);
        Assert.Same(PresetCatalog.Get("sn1987a"), vm.Lab.SelectedPreset);
        Assert.NotNull(vm.Lab.Pins);
        Assert.Equal(780_000, vm.CurrentObserver.DistancePc);
        Assert.False(vm.Lab.IsObserverPublished);
        Assert.Null(vm.Lab.ObserverSourceText);
        Assert.NotNull(vm.Lab.ObserverChangedNote);
        Assert.Contains("SN 1987A", vm.StatusMessage);
        Assert.Contains("still apply", vm.StatusMessage);
    }

    [Theory]
    [InlineData("av")]
    [InlineData("beam")]
    public void DustAndBeamEdits_AlsoKeepThePins(string input)
    {
        var vm = WithPreset("sn1998bw");
        if (input == "av") vm.Lab.Av = 3;
        else vm.Lab.BeamTowardEarth = !vm.Lab.BeamTowardEarth;

        Assert.True(vm.CurrentOutcome.UsesPublishedValues);
        Assert.NotNull(vm.Lab.SelectedPreset);
    }

    [Fact]
    public void AfterAnObserverEdit_AStarEditClearsEverything()
    {
        var vm = WithPreset("sn1987a");
        vm.Lab.DistancePc = 780_000;

        vm.Lab.Metallicity = 0.5;

        Assert.False(vm.CurrentOutcome.UsesPublishedValues);
        Assert.Null(vm.Lab.SelectedPreset);
        Assert.Null(vm.Lab.Pins);
        Assert.Null(vm.Lab.ObserverChangedNote);
        Assert.Contains("no longer apply", vm.StatusMessage);
    }

    // ---------------------------------------------------------------- I3: texts that state numbers

    [Fact]
    public void Glossary_NeutronStarSizeAndJetAngle_AreStatedCorrectly()
    {
        Assert.Contains("24 km across (radius about 12 km)", Glossary.NeutronStar);
        Assert.Contains("5° half-opening angle (10° across)", Glossary.Beam);
    }

    [Fact]
    public void PresetFacts_SayWhenEachWasMeasured_InTheRightWay()
    {
        var rows = WithPreset("sn1987a").Lab.PresetFactRows;

        Assert.Contains(rows, r => r.Value == "11 events in the first 12.4 s");               // a count accumulates
        Assert.Contains(rows, r => r.Value == "2.975 mag at 84.5 days after the explosion");  // a single time
        var smm = rows.Single(r => r.Label.StartsWith("The Solar Maximum Mission", StringComparison.Ordinal));
        Assert.DoesNotContain("days", smm.Value);                                              // an average: no single time
        Assert.Contains("days 159–250", smm.Label);
    }

    // ---------------------------------------------------------------- minors

    /// <summary>M4: the K-correction note follows the redshift, so they appear together at exactly 50 Mpc.</summary>
    [Fact]
    public void KCorrectionNote_AppearsExactlyWhenThereIsARedshift()
    {
        var vm = NewViewModel();
        vm.Lab.DistancePc = 50e6;
        Assert.True(vm.CurrentObserver.Redshift > 0);
        Assert.NotNull(vm.Lab.KCorrectionNote);
        Assert.NotNull(vm.Lab.RedshiftText);

        vm.Lab.DistancePc = 49.9e6;
        Assert.Null(vm.Lab.KCorrectionNote);
        Assert.Null(vm.Lab.RedshiftText);
    }

    /// <summary>M5: the drawn-curve rows cite the light-curve model's literature, not only the ruling.</summary>
    [Fact]
    public void DrawnCurveRows_CiteTheLightCurveSources()
    {
        var row = NewViewModel().Outcome.CurveRows.First();
        Assert.Contains("Arnett 1982", row.Citation);
        Assert.Contains("Goldberg, Bildsten & Paxton 2019", row.Citation);
        Assert.Contains("signals §4.2", row.ResearchRef);
        Assert.True(row.SharedSource);
        Assert.Equal("Source: given under this heading.", row.SourceLine);
        Assert.Contains("Arnett 1982", NewViewModel().Outcome.CurveSourceText);
    }

    /// <summary>M7: superluminous classes read "an SLSN-I (superluminous supernova)".</summary>
    [Fact]
    public void Summary_OfASuperluminousSupernova_ReadsCorrectly()
    {
        var o = TestOutcomes.Engine.Compute(LabInputs.Default with
        {
            InitialMass = 30, Stripping = Stripping.Full, Rotation = Rotation.Fast, Field = CoreField.Strong,
        });
        Assert.Equal(SupernovaType.SlsnI, o.Type);

        string summary = OutcomeTexts.Summary(o);

        Assert.Contains("an SLSN-I (superluminous supernova)", summary);
        Assert.DoesNotContain("SLSN-I supernova", summary);
    }

    /// <summary>M7: without a supernova type the fate is the headline, and the line below names only the remnant.</summary>
    [Fact]
    public void Headline_WithoutASupernova_DoesNotRepeatTheFate()
    {
        var vm = NewViewModel();
        vm.Lab.InitialMass = 5;

        Assert.Equal("No supernova", vm.Outcome.TypeText);
        Assert.Equal("", vm.Outcome.FateText);
        Assert.Equal("", vm.Outcome.FateSeparator);
        Assert.Equal("White dwarf", vm.Outcome.RemnantText);

        vm.Lab.InitialMass = 16;
        Assert.Equal("Supernova", vm.Outcome.FateText);
        Assert.Equal(" · ", vm.Outcome.FateSeparator);
    }

    /// <summary>M8: the map legend's fate names have glossary tooltips.</summary>
    [Fact]
    public void MapLegend_ItemsHaveGlossaryToolTips()
        => Assert.All(NewViewModel().Outcome.MapLegend,
            item => Assert.Equal(Glossary.Get(OutcomeTexts.FateGlossaryKey(item.Fate)), item.ToolTip));

    /// <summary>M9: the run number changes only through Re-roll.</summary>
    [Fact]
    public void RunNumber_HasNoPublicSetter()
        => Assert.False(typeof(MainViewModel).GetProperty(nameof(MainViewModel.RunNumber))!.SetMethod?.IsPublic ?? false);

    /// <summary>M11: every Quantity of Outcome, Remnant and PresupernovaStar that is set is listed in the details.</summary>
    [Fact]
    public void Details_ListEveryQuantityMember_ForManyOutcomes()
    {
        var outcomes = PresetCatalog.LoadEmbedded().Select(p => TestOutcomes.Engine.Compute(p.Inputs, p.Pins))
            .Append(TestOutcomes.Star(16)).Append(TestOutcomes.Star(5)).Append(TestOutcomes.Star(0.85))
            .Append(TestOutcomes.TypeIa()).Append(TestOutcomes.Collapsar())
            .Append(TestOutcomes.Engine.Compute(LabInputs.Default with
            {
                InitialMass = 30, Stripping = Stripping.Full, Rotation = Rotation.Fast, Field = CoreField.Strong,
            }));
        foreach (var o in outcomes)
        {
            var expected = QuantityMembers(o).Concat(QuantityMembers(o.Remnant));
            if (o.Star is { } star) expected = expected.Concat(QuantityMembers(star));
            var listed = OutcomeTexts.Quantities(o).Select(q => q.Quantity).ToList();

            var missing = expected.Where(q => !listed.Any(l => ReferenceEquals(l, q))).ToList();
            Assert.True(missing.Count == 0, $"{o.Type}/{o.Fate}: {missing.Count} quantities not in the details");
        }
    }

    private static IEnumerable<Quantity> QuantityMembers(object record)
        => record.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(Quantity))
            .Select(p => p.GetValue(record)).OfType<Quantity>();

    /// <summary>M12: the disabled near-Chandrasekhar slider shows the 1.38 M☉ in use, not a stale mass.</summary>
    [Fact]
    public void WhiteDwarfMassSlider_ShowsTheFixedMassForNearChandrasekhar()
    {
        var vm = NewViewModel();
        vm.Lab.Kind = StarKind.WhiteDwarf;
        vm.Lab.IaChannel = TypeIaChannel.SubChandrasekhar;
        vm.Lab.WhiteDwarfMassSlider = 1.0;
        Assert.Equal(1.0, vm.Lab.WhiteDwarfMassSlider);

        vm.Lab.IaChannel = TypeIaChannel.NearChandrasekhar;

        Assert.Equal(LabInputs.MaxWhiteDwarfMass, vm.Lab.WhiteDwarfMassSlider);
        Assert.Equal("1.38", vm.Lab.WhiteDwarfMassNumber);
    }

    /// <summary>M1: values split into number and unit, so the view draws the unit (e.g. M☉) at normal weight.</summary>
    [Fact]
    public void KeyNumberValues_SplitIntoNumberAndUnit()
    {
        var rows = NewViewModel().Outcome.KeyNumbers;
        var energy = rows.First(r => r.GlossaryKey == nameof(Glossary.ExplosionEnergy));
        Assert.Equal(energy.ValueText, energy.ValueNumber + energy.ValueUnit);
        Assert.Equal(" erg", energy.ValueUnit);
        var peak = rows.First(r => r.GlossaryKey == nameof(Glossary.PeakBrightness));
        Assert.Equal("", peak.ValueUnit);
        var seen = NewViewModel().Outcome.CurveRows.First(r => r.GlossaryKey == nameof(Glossary.ApparentMagnitude));
        Assert.Equal("", seen.ValueUnit);   // "m_V …: visible in daylight" is prose, not a unit
        var mass = rows.First(r => r.GlossaryKey == nameof(Glossary.RemnantMass));
        Assert.Equal(" M☉", mass.ValueUnit);
    }

    /// <summary>
    /// M2: crowded markers (a collapsar: neutrinos, gravitational waves, black hole and burst within a few decades) get
    /// labels that never overlap in a row; the most important ones are kept.
    /// </summary>
    [Theory]
    [InlineData("sn1998bw", 1000)]
    [InlineData("sn1998bw", 600)]
    [InlineData("sn1987a", 1300)]
    public void MarkerLabels_DoNotOverlap_AndKeepTheImportantOnes(string presetId, double width)
    {
        var t = WithPreset(presetId).Timeline;

        t.LayoutMarkerLabels(width);

        var labels = t.MarkerLabels;
        Assert.NotEmpty(labels);
        Assert.All(labels, l =>
        {
            Assert.InRange(l.Row, 0, TimelineViewModel.MarkerRows - 1);
            Assert.InRange(l.Left, 0, width - l.Width + 1e-9);
        });
        foreach (var row in labels.GroupBy(l => l.Row))
        {
            var sorted = row.OrderBy(l => l.Left).ToList();
            for (int i = 1; i < sorted.Count; i++)
                Assert.True(sorted[i].Left >= sorted[i - 1].Left + sorted[i - 1].Width,
                    $"{sorted[i - 1].Marker.Label} overlaps {sorted[i].Marker.Label}");
        }
        Assert.Contains(labels, l => l.Marker.GlossaryKey == "PeakBrightness");
        Assert.Contains(labels, l => l.Marker.GlossaryKey == "NeutrinoBurst");
        if (presetId == "sn1998bw") Assert.Contains(labels, l => l.Marker.GlossaryKey == "GammaRayBurst");
    }

    [Fact]
    public void MarkerLabels_FollowNewMarkers()
    {
        var vm = NewViewModel();
        vm.Timeline.LayoutMarkerLabels(1000);
        var before = vm.Timeline.MarkerLabels;

        vm.Lab.SelectedPreset = PresetCatalog.Get("sn1998bw");

        Assert.NotSame(before, vm.Timeline.MarkerLabels);
        Assert.Contains(vm.Timeline.MarkerLabels, l => l.Marker.GlossaryKey == "GammaRayBurst");
    }
}
