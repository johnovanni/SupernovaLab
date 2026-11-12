using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>The app shell's view models (Task 14; spec §2–§5.0, §9; Review Focus 4).</summary>
public class MainViewModelTests
{
    private const double Day = 86400;

    private static MainViewModel NewViewModel(FakeWindowService? windows = null)
        => new(TestOutcomes.Engine, windows ?? new FakeWindowService());

    // ---------------------------------------------------------------- inputs and the outcome (Review Focus 4)

    [Fact]
    public void ChangingInitialMass_WhilePlaying_KeepsTheTime_AndRaisesOutcomeChangedOnce()
    {
        var vm = NewViewModel();
        vm.Timeline.ObserverTimeS = 3 * Day;
        vm.Timeline.IsPlaying = true;
        var before = vm.CurrentOutcome;
        int raised = 0;
        vm.OutcomeChanged += (_, _) => raised++;

        vm.Lab.InitialMass = 20;

        Assert.Equal(1, raised);
        Assert.Equal(3 * Day, vm.Timeline.ObserverTimeS);
        Assert.True(vm.Timeline.IsPlaying);
        Assert.NotSame(before, vm.CurrentOutcome);
        Assert.Equal(20, vm.CurrentOutcome.Inputs.InitialMass);
    }

    [Fact]
    public void ChangingTheDistance_RecomputesTheObserver_AndRaisesOutcomeChangedOnce()
    {
        var vm = NewViewModel();
        int raised = 0;
        vm.OutcomeChanged += (_, _) => raised++;

        vm.Lab.DistancePc = 50_000;

        Assert.Equal(1, raised);
        Assert.Equal(50_000, vm.CurrentObserver.DistancePc);
    }

    /// <summary>Spec §2.3 and rulings R44 / R47e: a preset pins its published values; editing any input drops them.</summary>
    [Fact]
    public void SelectingSn1987a_AppliesItsInputsAndPins_ThenChangingMetallicityDropsThePins_WithAStatusMessage()
    {
        var vm = NewViewModel();
        var preset = PresetCatalog.Get("sn1987a");

        vm.Lab.SelectedPreset = preset;

        Assert.Equal(preset.Inputs, vm.Lab.Inputs);
        Assert.Equal(preset.Observer, vm.CurrentObserver);
        Assert.True(vm.CurrentOutcome.UsesPublishedValues);
        Assert.DoesNotContain("no longer apply", vm.StatusMessage ?? "");

        vm.Lab.Metallicity = 0.5;

        Assert.False(vm.CurrentOutcome.UsesPublishedValues);
        Assert.Equal(0.5, vm.CurrentOutcome.Inputs.Metallicity);
        Assert.Null(vm.Lab.SelectedPreset);
        Assert.Contains("SN 1987A", vm.StatusMessage);
        Assert.Contains("no longer apply", vm.StatusMessage);
    }

    [Fact]
    public void SelectingAPreset_RaisesOutcomeChangedOnce()
    {
        var vm = NewViewModel();
        int raised = 0;
        vm.OutcomeChanged += (_, _) => raised++;

        vm.Lab.SelectedPreset = PresetCatalog.Get("sn2011fe");

        Assert.Equal(1, raised);
        Assert.Equal(StarKind.WhiteDwarf, vm.CurrentOutcome.Inputs.Kind);
    }

    /// <summary>Ruling R31: Type Ia/Iax use a fixed effective opacity, so the κ slider is disabled with a reason.</summary>
    [Fact]
    public void OpacitySlider_IsDisabledForWhiteDwarfs_WithAReason()
    {
        var vm = NewViewModel();
        Assert.True(vm.Lab.IsOpacityEnabled);

        vm.Lab.Kind = StarKind.WhiteDwarf;

        Assert.False(vm.Lab.IsOpacityEnabled);
        Assert.Contains("0.19", vm.Lab.OpacityToolTip);
    }

    [Fact]
    public void WhiteDwarfMass_IsFixedForNearChandrasekhar()
    {
        var vm = NewViewModel();
        vm.Lab.Kind = StarKind.WhiteDwarf;
        Assert.False(vm.Lab.IsWhiteDwarfMassEnabled);

        vm.Lab.IaChannel = TypeIaChannel.SubChandrasekhar;

        Assert.True(vm.Lab.IsWhiteDwarfMassEnabled);
    }

    [Fact]
    public void BeyondFiftyMpc_TheObserverSectionSaysTheKCorrectionIsNotModeled()
    {
        var vm = NewViewModel();
        Assert.True(string.IsNullOrEmpty(vm.Lab.KCorrectionNote));

        vm.Lab.DistancePc = 100e6;

        Assert.Equal("Beyond 50 Mpc the shift of light between bands (K-correction) is not modeled.", vm.Lab.KCorrectionNote);
    }

    // ---------------------------------------------------------------- the timeline

    [Fact]
    public void Tick_OneSecondAtOneThirdDecadePerSecond_MultipliesTheTimeByTenToTheOneThird()
    {
        var t = new TimelineViewModel { ObserverTimeS = 10, IsPlaying = true };
        Assert.Equal(1.0 / 3, t.DecadesPerSecond, 12);

        t.Tick(TimeSpan.FromSeconds(1));

        // 10 × 10^(1/3) = 21.5443469...
        Assert.Equal(21.544346900318832, t.ObserverTimeS, 9);
    }

    [Fact]
    public void Tick_StopsAtTheEndOfTheAxis()
    {
        var t = new TimelineViewModel { ObserverTimeS = TimeAxis.MaxS / 2, IsPlaying = true, DecadesPerSecond = 3 };

        t.Tick(TimeSpan.FromSeconds(1));

        Assert.Equal(TimeAxis.MaxS, t.ObserverTimeS);
        Assert.False(t.IsPlaying);
    }

    [Fact]
    public void Tick_WhilePaused_DoesNothing()
    {
        var t = new TimelineViewModel { ObserverTimeS = 10 };
        t.Tick(TimeSpan.FromSeconds(1));
        Assert.Equal(10, t.ObserverTimeS);
    }

    [Fact]
    public void Explode_StartsTheTimelineAtTheBeginning_AndReroll_IncrementsTheRunNumber()
    {
        var vm = NewViewModel();
        vm.Timeline.ObserverTimeS = 1e6;
        int run = vm.RunNumber;

        vm.ExplodeCommand.Execute(null);

        Assert.Equal(TimeAxis.MinS, vm.Timeline.ObserverTimeS);
        Assert.True(vm.Timeline.IsPlaying);

        vm.RerollCommand.Execute(null);
        Assert.Equal(run + 1, vm.RunNumber);
    }

    /// <summary>Spec §4: the markers come from the models' marker methods, in observer time, omitted when they don't apply.</summary>
    [Fact]
    public void Markers_ForTheDefaultStar_IncludeNeutrinosGwBreakoutPeakAndPlateauEnd_InObserverTime()
    {
        var vm = NewViewModel();
        var keys = vm.Timeline.Markers.Select(m => m.GlossaryKey).ToList();

        Assert.Contains("NeutrinoBurst", keys);
        Assert.Contains("GravitationalWaves", keys);
        Assert.Contains("ShockBreakout", keys);
        Assert.Contains("PeakBrightness", keys);
        Assert.Contains("PlateauEnd", keys);
        Assert.DoesNotContain("GammaRayBurst", keys);   // no GRB for a 16 M☉ red supergiant
        Assert.DoesNotContain("BlackHoleForms", keys);  // it leaves a neutron star
        Assert.Equal(0.01, vm.Timeline.Markers.Single(m => m.GlossaryKey == "NeutrinoBurst").ObserverTimeS);
        double peak = new LightCurveModel().PeakTimeS(vm.CurrentOutcome);
        Assert.Equal(peak, vm.Timeline.Markers.Single(m => m.GlossaryKey == "PeakBrightness").ObserverTimeS);
        Assert.All(vm.Timeline.Markers, m => Assert.InRange(m.ObserverTimeS, TimeAxis.MinS, TimeAxis.MaxS));
        Assert.All(vm.Timeline.Markers, m => Assert.False(string.IsNullOrWhiteSpace(m.Basis), m.Label));
    }

    [Fact]
    public void Markers_AreStretchedByOnePlusZ()
    {
        var vm = NewViewModel();
        vm.Lab.DistancePc = 500e6;
        double z1 = vm.CurrentObserver.TimeDilation;
        Assert.True(z1 > 1.05);

        double peak = new LightCurveModel().PeakTimeS(vm.CurrentOutcome);
        Assert.Equal(peak * z1, vm.Timeline.Markers.Single(m => m.GlossaryKey == "PeakBrightness").ObserverTimeS, 6);
    }

    [Fact]
    public void Markers_ForACollapsar_IncludeTheBlackHoleAndTheBurst()
    {
        var vm = NewViewModel();
        vm.Lab.InitialMass = 40;
        vm.Lab.Metallicity = 0.1;
        vm.Lab.Stripping = Stripping.Full;
        vm.Lab.Rotation = Rotation.Extreme;
        Assert.True(vm.CurrentOutcome.HasGrb);

        var keys = vm.Timeline.Markers.Select(m => m.GlossaryKey).ToList();
        Assert.Contains("BlackHoleForms", keys);
        Assert.Contains("GammaRayBurst", keys);
    }

    // ---------------------------------------------------------------- the window service

    [Fact]
    public void Commands_CallTheWindowServiceOnce_WithTheRightArgument()
    {
        var windows = new FakeWindowService();
        var vm = NewViewModel(windows);

        vm.ShowOutcomeDetailsCommand.Execute(null);
        vm.ShowPresetInfoCommand.Execute(null);
        vm.ShowInstrumentsCommand.Execute(Messenger.Neutrinos);

        Assert.Equal(1, windows.OutcomeDetailsCalls);
        Assert.Equal(1, windows.PresetInfoCalls);
        Assert.Equal(new Messenger?[] { Messenger.Neutrinos }, windows.InstrumentsCalls);
    }

    // ---------------------------------------------------------------- the outcome summary and details

    /// <summary>
    /// The default 16 M☉ outcome has 19 non-null quantities, counted by hand: the explosion's energy, ejecta, ⁵⁶Ni, peak
    /// luminosity and ⁴⁴Ti (5; no magnetar, Phillips, GRB or pulsar pins); the neutron star's mass, radius, spin period,
    /// field and kick (5; no black-hole formation time); and the star's nine (final mass, He core, CO core, H envelope, He
    /// layer, radius, surface temperature, mass-loss rate, wind speed).
    /// </summary>
    [Fact]
    public void DetailRows_ListEveryNonNullQuantity_AndChangeWithTheOutcome()
    {
        var vm = NewViewModel();
        var rows = vm.Outcome.DetailRows;

        Assert.Equal(19, rows.Count);
        Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.Citation), r.Label));
        Assert.Contains(rows, r => r.Label.Contains("⁵⁶Ni"));

        vm.Lab.Kind = StarKind.WhiteDwarf;

        Assert.NotSame(rows, vm.Outcome.DetailRows);
        Assert.Contains(vm.Outcome.DetailRows, r => r.Label.Contains("Δm15"));
        Assert.DoesNotContain(vm.Outcome.DetailRows, r => r.Label.Contains("⁴⁴Ti"));
    }

    [Fact]
    public void KeyNumbers_AreThreeOrFour_EachWithASource()
    {
        var vm = NewViewModel();
        Assert.InRange(vm.Outcome.KeyNumbers.Count, 3, 4);
        Assert.All(vm.Outcome.KeyNumbers, r => Assert.False(string.IsNullOrWhiteSpace(r.Citation), r.Label));
    }

    /// <summary>Ruling R34: the stated peak comes from the drawn light curve, never from Outcome.PeakLuminosityErgS.</summary>
    [Fact]
    public void PeakBrightness_ComesFromTheDrawnLightCurve()
    {
        var vm = NewViewModel();
        var o = vm.CurrentOutcome;
        var lc = new LightCurveModel();
        double drawnPeakAbsV = lc.Sample(o, vm.CurrentObserver, lc.PeakTimeS(o), "absV");

        var row = vm.Outcome.KeyNumbers.Single(r => r.GlossaryKey == "PeakBrightness");

        Assert.Equal(drawnPeakAbsV, row.Value, 9);
        Assert.Contains("light curve", row.Citation);
        var engineRow = vm.Outcome.DetailRows.Single(r => ReferenceEquals(r.Quantity, o.PeakLuminosityErgS));
        Assert.DoesNotContain("Peak brightness", engineRow.Label);
    }

    /// <summary>Spec §9: an input that has no effect gets a note in the outcome card.</summary>
    [Fact]
    public void AnInputWithNoEffect_IsNotedInTheSummary()
    {
        var vm = NewViewModel();
        Assert.Empty(vm.Outcome.InputNotes);

        vm.Lab.InitialMass = 5;          // ends as a white dwarf: no neutron star for the field to act on
        vm.Lab.Field = CoreField.Strong;

        Assert.Contains(vm.Outcome.InputNotes, n => n.Contains("no effect"));
        Assert.All(vm.Outcome.InputNotes, n => Assert.Contains(n, vm.Outcome.Notes));
    }

    [Fact]
    public void ClickingTheOutcomeMap_SetsTheMass()
    {
        var vm = NewViewModel();
        Assert.NotEmpty(vm.Outcome.MapSegments);

        vm.Outcome.SetMassFromMapFraction(1.0);

        Assert.Equal(LabInputs.MaxInitialMass, vm.Lab.InitialMass, 9);
        Assert.Equal(1.0, vm.Outcome.MapMarkerFraction, 9);
    }

    [Fact]
    public void TheTimeReadout_CountsFromBounceOrIgnition()
    {
        var vm = NewViewModel();
        vm.Timeline.ObserverTimeS = 3 * Day;
        Assert.Equal("3 days after core bounce", vm.Timeline.ObserverTimeText);

        vm.Lab.Kind = StarKind.WhiteDwarf;

        Assert.Equal("3 days after ignition", vm.Timeline.ObserverTimeText);
    }

    [Fact]
    public void LightLeftLine_GivesTheLightTravelTime()
    {
        var vm = NewViewModel();
        vm.Lab.DistancePc = 50_000; // the LMC: 163,000 light-years
        Assert.Equal("Light from this moment left the star 163,000 years before it reached Earth.", vm.Timeline.LightLeftText);
    }
}
