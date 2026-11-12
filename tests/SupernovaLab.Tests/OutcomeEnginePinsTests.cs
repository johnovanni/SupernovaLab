using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>Preset pins in the outcome engine: ruling R44's policy and ruling R41's neutron-star and ring pins.</summary>
public class OutcomeEnginePinsTests
{
    private static readonly OutcomeEngine Engine = TestOutcomes.Engine;
    private static readonly Source Published = new("A published fit (test)", "tests");

    private static PresetPins Pins(double? e = null, double? ej = null, double? ni = null, double? r = null)
        => new(e, ej, ni, r, Published);

    // ---------------------------------------------------------------- R44: every pin is published and cited

    [Fact]
    public void EveryPinnedValue_IsPublished_WithThePresetsSource()
    {
        var o = Engine.Compute(LabInputs.Default, Pins(1.2e51, 12, 0.05, 4e13));
        Assert.True(o.UsesPublishedValues);
        foreach (var (q, value) in new[] { (o.ExplosionEnergyErg, 1.2e51), (o.EjectaMass, 12), (o.NickelMass, 0.05),
                     (o.Star!.RadiusCm, 4e13) })
        {
            Assert.NotNull(q);
            Assert.Equal(value, q!.Value);
            Assert.True(q.Published);
            Assert.Same(Published, q.Source);
            Assert.Equal(Confidence.ModelDependent, q.Confidence);
        }

        // Pins that change nothing leave the outcome unpublished; no pins at all, likewise.
        Assert.False(Engine.Compute(LabInputs.Default, Pins()).UsesPublishedValues);
        Assert.False(Engine.Compute(LabInputs.Default).UsesPublishedValues);
    }

    // ---------------------------------------------------------------- R44: the ⁵⁶Ni cap is not applied to pins

    [Fact]
    public void PinnedNickel_AboveHalfTheEjecta_IsKept_WithANote()
    {
        // R15 caps the model's ⁵⁶Ni at half the ejecta; R44 keeps a pinned value as published and says so.
        var o = Engine.Compute(LabInputs.Default with { InitialMass = 15, Stripping = Stripping.Full }, Pins(ej: 1.0, ni: 0.8));
        Assert.Equal(0.8, o.NickelMass!.Value);
        Assert.Equal(1.0, o.EjectaMass!.Value);
        Assert.Contains(o.Notes, n => n.Contains("published ⁵⁶Ni mass", StringComparison.Ordinal) &&
                                      n.Contains("more than half the published ejecta mass", StringComparison.Ordinal));

        // A set that keeps to the cap gets no such note.
        var fine = Engine.Compute(LabInputs.Default with { InitialMass = 15, Stripping = Stripping.Full }, Pins(ej: 2.0, ni: 0.1));
        Assert.DoesNotContain(fine.Notes, n => n.Contains("ejecta mass", StringComparison.Ordinal) && n.Contains("published", StringComparison.Ordinal));
    }

    [Fact]
    public void PinnedEjectaAlone_CapsTheModelsNickel_WithoutCallingItPublished()
    {
        // Review M3: R15 caps the MODEL's ⁵⁶Ni at half the ejecta, so under a pinned M_ej of 0.06 M☉ the 15 M☉ Full star's
        // model 0.059 M☉ becomes 0.03 M☉. No note calls a model value "published".
        var o = Engine.Compute(LabInputs.Default with { InitialMass = 15, Stripping = Stripping.Full }, Pins(ej: 0.06));
        Assert.Equal(0.059, Engine.Compute(LabInputs.Default with { InitialMass = 15, Stripping = Stripping.Full }).NickelMass!.Value);
        Assert.Equal(0.03, o.NickelMass!.Value, 1e-12);
        Assert.False(o.NickelMass.Published);
        Assert.DoesNotContain(o.Notes, n => n.Contains("published ⁵⁶Ni", StringComparison.Ordinal));

        // A Type Ia keeps its published yield (R17); a pinned ejecta below it gets a note naming the model's ⁵⁶Ni.
        var ia = Engine.Compute(LabInputs.Default with { Kind = StarKind.WhiteDwarf }, Pins(ej: 0.5));
        Assert.Equal(0.6, ia.NickelMass!.Value);
        Assert.Contains(ia.Notes, n => n.Contains("model's ⁵⁶Ni", StringComparison.Ordinal) &&
                                       n.Contains("published ejecta", StringComparison.Ordinal));
    }

    [Fact]
    public void PinnedTypeIaNickel_AboveTheEjecta_IsKept_WithANote()
    {
        // Ruling R17: a thermonuclear outcome only needs ⁵⁶Ni < M_ej; R44 keeps a pinned value that breaks it.
        var ia = LabInputs.Default with { Kind = StarKind.WhiteDwarf };
        var o = Engine.Compute(ia, Pins(ni: 1.5));
        Assert.Equal(1.5, o.NickelMass!.Value);
        Assert.Contains(o.Notes, n => n.Contains("published ⁵⁶Ni mass", StringComparison.Ordinal) &&
                                       n.Contains("more than the model's ejecta mass", StringComparison.Ordinal));
        Assert.DoesNotContain(Engine.Compute(ia, Pins(ni: 0.6)).Notes, n => n.Contains("published ⁵⁶Ni", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- R44: explosion pins need an explosion

    [Fact]
    public void ExplosionPins_OnAFailedSupernova_AreIgnored_WithANote()
    {
        var x = LabInputs.Default with { InitialMass = 25 };
        Assert.Equal(Fate.FailedSupernova, Engine.Compute(x).Fate);

        var o = Engine.Compute(x, new PresetPins(1e51, 10, 0.07, null, Published, Ti44Mass: 1e-4, GrbEisoErg: 1e48,
            NsSpinPeriodS: 0.02, PulsarRadioLumMjyKpc2: 50, EquatorialRing: true));
        Assert.Equal(Fate.FailedSupernova, o.Fate);
        Assert.Null(o.ExplosionEnergyErg);
        Assert.Null(o.EjectaMass);
        Assert.Null(o.NickelMass);
        Assert.Null(o.Ti44Mass);
        Assert.Null(o.GrbIsotropicEnergyErg);
        Assert.Null(o.PulsarRadioLuminosityMjyKpc2);
        Assert.False(o.HasEquatorialRing);
        Assert.False(o.UsesPublishedValues);
        Assert.Contains(o.Notes, n => n.Contains("does not explode", StringComparison.Ordinal));

        // The radius describes the star, so it still applies.
        var withRadius = Engine.Compute(x, Pins(e: 1e51, r: 5e13));
        Assert.Equal(5e13, withRadius.Star!.RadiusCm.Value);
        Assert.True(withRadius.UsesPublishedValues);
    }

    [Fact]
    public void PinsThatDoNotFitTheOutcome_AreIgnored_WithANote()
    {
        // ⁴⁴Ti on a Type Ia (R35: core collapse only); GRB values without a burst; neutron-star values on a black hole.
        var ia = Engine.Compute(LabInputs.Default with { Kind = StarKind.WhiteDwarf }, new PresetPins(null, null, null, null,
            Published, Ti44Mass: 1e-4));
        Assert.Null(ia.Ti44Mass);
        Assert.Contains(ia.Notes, n => n.Contains("⁴⁴Ti", StringComparison.Ordinal) && n.Contains("not used", StringComparison.Ordinal));

        var noGrb = Engine.Compute(LabInputs.Default, new PresetPins(null, null, null, null, Published, GrbEisoErg: 1e48));
        Assert.False(noGrb.HasGrb);
        Assert.Null(noGrb.GrbIsotropicEnergyErg);
        Assert.Contains(noGrb.Notes, n => n.Contains("gamma-ray burst values are not used", StringComparison.Ordinal));

        var collapsar = Engine.Compute(TestOutcomes.Collapsar().Inputs, new PresetPins(null, null, null, null, Published,
            NsSpinPeriodS: 0.02, NsFieldG: 1e12));
        Assert.Equal(RemnantKind.BlackHole, collapsar.Remnant.Kind);
        Assert.Contains(collapsar.Notes, n => n.Contains("neutron-star values are not used", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- R44: R22 judged again after pins

    // Ruling R53 moved the hydrogen-rich re-judgement test to OutcomeConsistencyTests (a 25 M☉ partly stripped star): the
    // 16 M☉ star used here is no longer superluminous, being below outcomes §5.3's 20 M☉.

    // ---------------------------------------------------------------- R41: neutron-star pins

    [Fact]
    public void NeutronStarPins_SetTheSpinFieldAndRadioLuminosity()
    {
        var o = Engine.Compute(LabInputs.Default with { InitialMass = 9.1 }, new PresetPins(null, null, null, null, Published,
            NsSpinPeriodS: 0.0168, NsFieldG: 3.8e12, PulsarRadioLumMjyKpc2: 56));
        Assert.Equal(RemnantKind.NeutronStar, o.Remnant.Kind);
        Assert.Equal(0.0168, o.Remnant.SpinPeriodS!.Value);
        Assert.Equal(3.8e12, o.Remnant.MagneticFieldG!.Value);
        Assert.Same(Published, o.Remnant.SpinPeriodS.Source);
        Assert.True(o.Remnant.MagneticFieldG.Published);
        Assert.Equal(56, o.PulsarRadioLuminosityMjyKpc2!.Value);
        Assert.True(o.UsesPublishedValues);
        Assert.Null(Engine.Compute(LabInputs.Default with { InitialMass = 9.1 }).PulsarRadioLuminosityMjyKpc2);
    }

    [Fact]
    public void NeutronStarMassPin_SetsTheRemnantMass_AndTheNeutrinoBindingEnergy()
    {
        // Ruling R46. The 19 M☉ star at Z = 0.1 leaves a 1.634 M☉ neutron star in the model; pinned, 1.444 M☉.
        var x = LabInputs.Default with { InitialMass = 19, Metallicity = 0.1 };
        var model = Engine.Compute(x);
        Assert.Equal(1.634, model.Remnant.Mass!.Value, 0.001);
        Assert.False(model.UsesPublishedValues);

        var o = Engine.Compute(x, new PresetPins(null, null, null, null, Published, NsGravitationalMass: 1.444));
        Assert.Equal(RemnantKind.NeutronStar, o.Remnant.Kind);
        Assert.Equal(1.444, o.Remnant.Mass!.Value);
        Assert.True(o.Remnant.Mass.Published);
        Assert.Same(Published, o.Remnant.Mass.Source);
        Assert.True(o.UsesPublishedValues);

        // A lighter neutron star releases less binding energy, so fewer neutrinos are counted.
        var lmc = new ObserverSetup(51_400, 0, true);
        Assert.True(NeutrinoModel.KamiokandeIIEvents(o, lmc) <
                    NeutrinoModel.KamiokandeIIEvents(model, lmc));

        // On a failed supernova (no neutron star) it is ignored with a note (R44).
        var failed = Engine.Compute(LabInputs.Default with { InitialMass = 25 },
            new PresetPins(null, null, null, null, Published, NsGravitationalMass: 1.444));
        Assert.Equal(RemnantKind.BlackHole, failed.Remnant.Kind);
        Assert.False(failed.UsesPublishedValues);
        Assert.Contains(failed.Notes, n => n.Contains("does not explode", StringComparison.Ordinal));
    }

    [Fact]
    public void MagnetarPins_SetTheSpinDownEnergyAndTime()
    {
        // KB10: E_p = 2e52 / P_ms² = 2e52 / 2.1² = 4.53515e51 erg; t_p = 1.3 yr × P10² / B14² = 1.3 × 3.156e7 × 0.21² / 0.9²
        // = 4.1028e7 × 0.0441 / 0.81 = 2.23375e6 s (25.9 d).
        var x = LabInputs.Default with { InitialMass = 30, Stripping = Stripping.Full, Rotation = Rotation.Fast, Field = CoreField.Strong };
        var o = Engine.Compute(x, new PresetPins(null, null, null, null, Published, NsSpinPeriodS: 2.1e-3, NsFieldG: 0.9e14));
        Assert.Equal(RemnantKind.Magnetar, o.Remnant.Kind);
        Assert.Equal(2.1e-3, o.Remnant.SpinPeriodS!.Value);
        Assert.Equal(4.53515e51, o.MagnetarEnergyErg!.Value, 0.00001e51);
        Assert.Equal(2.23375e6, o.MagnetarTimescaleS!.Value, 0.00002e6);
        Assert.Equal(SupernovaType.SlsnI, o.Type);
        Assert.True(o.UsesPublishedValues);
    }

    [Fact]
    public void RingPin_TurnsOnTheRing_ForAnExplosion()
    {
        var o = Engine.Compute(LabInputs.Default, new PresetPins(null, null, null, null, Published, EquatorialRing: true));
        Assert.True(o.HasEquatorialRing);
        Assert.True(o.UsesPublishedValues);
        Assert.False(Engine.Compute(LabInputs.Default).HasEquatorialRing);
    }

    [Fact]
    public void Ti44Pin_OnACoreCollapseSupernova_IsUsed()
    {
        var o = Engine.Compute(LabInputs.Default, new PresetPins(null, null, null, null, Published, Ti44Mass: 1.25e-4));
        Assert.Equal(1.25e-4, o.Ti44Mass!.Value);
        Assert.Same(Published, o.Ti44Mass.Source);
        Assert.True(o.UsesPublishedValues);
    }
}
