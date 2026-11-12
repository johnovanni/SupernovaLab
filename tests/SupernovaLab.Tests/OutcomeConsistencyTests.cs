using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>
/// Ruling R53 (final review I1–I3): the outcome's notes agree with its final remnant, ⁵⁶Ni and class after every later
/// step and after pins; pinned ejecta and remnant are checked against the star's mass; and the superluminous classes need
/// outcomes §5.3's conditions, with a disclosure when a magnetar outshines every observed supernova.
/// </summary>
public class OutcomeConsistencyTests
{
    private static readonly OutcomeEngine Engine = TestOutcomes.Engine;
    private static readonly Source Published = new("Test publication", "tests");

    private static Outcome Preset(string id)
    {
        var p = PresetCatalog.Get(id);
        return Engine.Compute(p.Inputs, p.Pins);
    }

    private static Outcome Compute(double mass, double z = 1.0, Stripping stripping = Stripping.None,
        Rotation rotation = Rotation.Slow, CoreField field = CoreField.Normal)
        => Engine.Compute(LabInputs.Default with
        {
            InitialMass = mass, Metallicity = z, Stripping = stripping, Rotation = rotation, Field = field,
        });

    // ---------------------------------------------------------------- I1: notes match the final outcome

    [Fact]
    public void Sn1998bw_Notes_NeverSayNeutronStar()
    {
        // Step 4 (Sch21) would leave a neutron star at 40 M☉, but step 7a's collapsar makes a black hole.
        var o = Preset("sn1998bw");
        Assert.Equal(RemnantKind.BlackHole, o.Remnant.Kind);
        Assert.DoesNotContain(o.Notes, n => n.Contains("neutron star", StringComparison.Ordinal));
    }

    [Fact]
    public void Sn2011fe_Notes_GiveThePinnedNickel()
    {
        // Pereira et al. 2013's 0.53 M☉ replaces the near-Chandrasekhar default 0.6 M☉, and the sentence follows it.
        var o = Preset("sn2011fe");
        Assert.Equal(0.53, o.NickelMass!.Value);
        Assert.Contains(o.Notes, n => n.Contains("0.53 M☉ of radioactive nickel", StringComparison.Ordinal));
        Assert.DoesNotContain(o.Notes, n => n.Contains("0.6 M☉", StringComparison.Ordinal));
        Assert.Equal(SupernovaType.Ia, o.Type);
    }

    [Fact]
    public void PinnedNickel_OnATypeIa_SetsTheSubtypeAndItsSentence()
    {
        // The model's own subtype rule (step 9: 91T-like at ≥ 0.8 M☉, 91bg-like at ≤ 0.1 M☉) on the published ⁵⁶Ni.
        var x = LabInputs.Default with { Kind = StarKind.WhiteDwarf };
        var bright = Engine.Compute(x, new PresetPins(null, null, 0.85, null, Published));
        Assert.Equal(SupernovaType.Ia91T, bright.Type);
        Assert.Contains(bright.Notes, n => n.Contains("0.85 M☉ of nickel", StringComparison.Ordinal));
        Assert.DoesNotContain(bright.Notes, n => n.Contains("0.6 M☉", StringComparison.Ordinal));
    }

    [Fact]
    public void RotationEnlargedCore_ReachingPulsationalPairInstability_DropsTheIronCoreNote()
    {
        // Ruling R20: 7d raises the He core into the PPISN range; the step 3 "iron core collapses" sentence no longer applies.
        var cases = from m in new[] { 60.0, 80, 100, 150 }
                    from z in new[] { 0.01, 0.1 }
                    select Compute(m, z, rotation: Rotation.Fast);
        var raised = cases.Where(o => o.Notes.Any(n => n.Contains("Its enlarged helium core", StringComparison.Ordinal))).ToList();
        Assert.NotEmpty(raised);
        foreach (var o in raised)
            Assert.DoesNotContain(o.Notes, n => n.StartsWith("The iron core of this", StringComparison.Ordinal));
    }

    public static IEnumerable<object[]> PresetIds() => PresetCatalog.LoadEmbedded().Select(p => new object[] { p.Id });

    [Theory]
    [MemberData(nameof(PresetIds))]
    public void PresetNotes_AgreeWithTheRemnantAndNickel(string id) => AssertNotesAgree(Preset(id), id);

    [Fact]
    public void GridNotes_AgreeWithTheRemnantAndNickel()
    {
        // Collapsars, magnetorotational explosions, magnetars and the Type Ia channels: every branch that replaces an
        // earlier step's remnant or ⁵⁶Ni.
        foreach (double m in new[] { 12.0, 20, 25, 30, 40, 60 })
        foreach (double z in new[] { 0.1, 1.0 })
        foreach (var s in new[] { Stripping.None, Stripping.Partial, Stripping.Full, Stripping.HydrogenAndHelium })
        foreach (var r in new[] { Rotation.Fast, Rotation.Extreme })
        foreach (var f in new[] { CoreField.Normal, CoreField.Strong })
        {
            var o = Compute(m, z, s, r, f);
            AssertNotesAgree(o, $"{m} M☉ Z {z} {s} {r} {f}");
        }
        foreach (var ch in new[] { TypeIaChannel.NearChandrasekhar, TypeIaChannel.SubChandrasekhar })
        foreach (double wd in new[] { 0.85, 1.0, 1.15 })
        {
            var x = LabInputs.Default with { Kind = StarKind.WhiteDwarf, IaChannel = ch, WhiteDwarfMass = wd };
            AssertNotesAgree(Engine.Compute(x), $"Ia {ch} {wd}");
            AssertNotesAgree(Engine.Compute(x, new PresetPins(null, null, 0.45, null, Published)), $"Ia {ch} {wd} pinned");
        }
    }

    private static void AssertNotesAgree(Outcome o, string where)
    {
        string notes = string.Join(" | ", o.Notes);
        if (o.Remnant.Kind == RemnantKind.BlackHole)
        {
            Assert.False(notes.Contains("leaving a neutron star", StringComparison.Ordinal), $"{where}: {notes}");
            Assert.False(notes.Contains("leaving a light neutron star", StringComparison.Ordinal), $"{where}: {notes}");
            Assert.False(notes.Contains("newborn neutron star restart", StringComparison.Ordinal), $"{where}: {notes}");
            // A black hole's mass, when a note gives it, is the outcome's.
            foreach (var n in o.Notes.Where(n => n.Contains("black hole of ", StringComparison.Ordinal)))
                Assert.True(n.Contains($"black hole of {UnitFormat.Mass(o.Remnant.Mass!.Value)}", StringComparison.Ordinal),
                    $"{where}: {n}");
        }
        if (o.Remnant.Kind is RemnantKind.NeutronStar or RemnantKind.Magnetar)
            Assert.False(notes.Contains("black hole of", StringComparison.Ordinal), $"{where}: {notes}");
        // A Type Ia's ⁵⁶Ni sentence gives the outcome's ⁵⁶Ni.
        foreach (var n in o.Notes.Where(n => n.Contains(" of nickel", StringComparison.Ordinal) ||
                                             n.Contains(" of radioactive nickel", StringComparison.Ordinal)))
            Assert.True(n.Contains(UnitFormat.Mass(o.NickelMass!.Value), StringComparison.Ordinal), $"{where}: {n}");
    }

    // ---------------------------------------------------------------- I2: remnant pin and mass budget

    [Fact]
    public void Sn1998bw_PinsIwamotosBlackHole_AndNotesTheMassBudget()
    {
        // Iwamoto et al. 1998, model CO138: a 13.8 M☉ CO star, M_ej = 13.8 − 2.9 = 10.9 M☉, so the remnant is 2.9 M☉. The
        // lab's star ends at 11.2 M☉, so 10.9 + 2.9 = 13.8 M☉ is more than it holds: the note says so.
        var o = Preset("sn1998bw");
        Assert.Equal(RemnantKind.BlackHole, o.Remnant.Kind);
        Assert.Equal(2.9, o.Remnant.Mass!.Value);
        Assert.True(o.Remnant.Mass.Published);
        Assert.True(o.EjectaMass!.Value + o.Remnant.Mass.Value > o.Star!.FinalMass.Value);
        Assert.Contains(o.Notes, n => n.Contains("more than the lab's star holds", StringComparison.Ordinal));
        // The collapsar sentence gives the published black hole.
        Assert.Contains(o.Notes, n => n.Contains("black hole of 2.9 M☉", StringComparison.Ordinal));
        // Momentum-scaled kick (outcomes §6): v_NS × 1.4 / M_BH = 347.78 × 1.4 / 2.9 = 167.89 km/s.
        Assert.Equal(167.89, o.Remnant.KickKmS!.Value, 0.01);
    }

    [Theory]
    [MemberData(nameof(PresetIds))]
    public void EveryPreset_KeepsItsMassBudget_OrSaysItCannot(string id)
    {
        var o = Preset(id);
        if (o.EjectaMass is null || o.Star is null || o.Remnant.Mass is null) return;
        bool within = o.EjectaMass.Value + o.Remnant.Mass.Value <= o.Star.FinalMass.Value + 0.01;
        Assert.True(within || o.Notes.Any(n => n.Contains("more than the lab's star holds", StringComparison.Ordinal)),
            $"{id}: M_ej {o.EjectaMass.Value} + remnant {o.Remnant.Mass.Value} > M_f {o.Star.FinalMass.Value}, with no note");
    }

    [Fact]
    public void Sn2015bn_IsOverItsMassBudget_AndSaysSo()
    {
        // Nicholl et al. 2016's 8.4 M☉ of ejecta plus the 1.42 M☉ magnetar (Sch21) is 9.82 M☉, more than the 30 M☉
        // Full-stripped star's 9.28 M☉ at death.
        var o = Preset("sn2015bn");
        Assert.Equal(SupernovaType.SlsnI, o.Type);
        Assert.Contains(o.Notes, n => n.Contains("more than the lab's star holds", StringComparison.Ordinal));
    }

    [Fact]
    public void RemnantMassPin_WithoutABlackHole_IsNotUsed()
    {
        var o = Engine.Compute(LabInputs.Default, new PresetPins(null, null, null, null, Published, RemnantMass: 3));
        Assert.Equal(RemnantKind.NeutronStar, o.Remnant.Kind);
        Assert.NotEqual(3, o.Remnant.Mass!.Value);
        Assert.Contains(o.Notes, n => n.Contains("remnant mass is not used", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- I3: superluminous conditions and disclosure

    [Fact]
    public void UltraStrippedExtremeMagnetar_IsNotSuperluminous_ButSaysItOutshinesEveryObservedSupernova()
    {
        // 10 M☉, Z 0.01, H&He-stripped, extreme rotation, strong field: 0.1 M☉ of ejecta (ruling R9's floor) and a 1.2 ms
        // magnetar. Outcomes §5.3: SLSN-I magnetar fits have M_ej ≥ 2.2 M☉ (N17's 1σ low end) from stars of more than
        // 20 M☉, so it keeps its base class (Ic) as a magnetar-boosted supernova. Its full-trapping peak (about 1e46 erg/s
        // in the final review's probe) is above ASASSN-15lh's 2.2e45 erg/s, and a note says so.
        var o = Compute(10, 0.01, Stripping.HydrogenAndHelium, Rotation.Extreme, CoreField.Strong);
        Assert.Equal(RemnantKind.Magnetar, o.Remnant.Kind);
        Assert.Equal(SupernovaType.Ic, o.Type);
        Assert.Contains(o.Notes, n => n.Contains("magnetar-boosted", StringComparison.Ordinal));
        Assert.True(o.PeakLuminosityErgS!.Value > 2e45);
        Assert.Contains(o.Notes, n => n.Contains("brighter than any supernova ever observed", StringComparison.Ordinal));
    }

    [Fact]
    public void SuperluminousClass_NeedsAStarAbove20Msun()
    {
        // 20 M☉ Full-stripped, fast rotation, strong field: E_p 3.47e51 erg and M_ej 3.57 M☉ qualify, but the star is not
        // more than 20 M☉ (§5.3), so it stays Ib, magnetar-boosted. 30 M☉ (M_ej 7.7 M☉) is SLSN-I.
        var twenty = Compute(20, stripping: Stripping.Full, rotation: Rotation.Fast, field: CoreField.Strong);
        Assert.Equal(SupernovaType.Ib, twenty.Type);
        Assert.Contains(twenty.Notes, n => n.Contains("magnetar-boosted", StringComparison.Ordinal));
        Assert.Contains(twenty.Notes, n => n.Contains("Type Ib", StringComparison.Ordinal));
        var thirty = Compute(30, stripping: Stripping.Full, rotation: Rotation.Fast, field: CoreField.Strong);
        Assert.Equal(SupernovaType.SlsnI, thirty.Type);
        Assert.DoesNotContain(thirty.Notes, n => n.Contains("magnetar-boosted", StringComparison.Ordinal));
    }

    [Fact]
    public void SuperluminousClass_NeedsAtLeast2Point2MsunOfEjecta_JudgedOnThePinnedEjecta()
    {
        var x = LabInputs.Default with
        {
            InitialMass = 30, Stripping = Stripping.Full, Rotation = Rotation.Fast, Field = CoreField.Strong,
        };
        Assert.Equal(SupernovaType.SlsnI, Engine.Compute(x).Type);
        var light = Engine.Compute(x, new PresetPins(null, 2.0, null, null, Published));
        Assert.NotEqual(SupernovaType.SlsnI, light.Type);
        Assert.Contains(light.Notes, n => n.Contains("magnetar-boosted", StringComparison.Ordinal));
        Assert.Equal(SupernovaType.SlsnI, Engine.Compute(x, new PresetPins(null, 2.2, null, null, Published)).Type);
    }

    [Fact]
    public void Sn2015bn_StaysSlsnI()
    {
        // 30 M☉ (> 20) with Nicholl et al. 2016's 8.4 M☉ of ejecta (≥ 2.2): both §5.3 conditions hold.
        Assert.Equal(SupernovaType.SlsnI, Preset("sn2015bn").Type);
    }

    [Fact]
    public void EveryMagnetarAbove2e45_SaysSo_AndNoSuperluminousClassBreaksTheConditions()
    {
        int bright = 0;
        foreach (double m in new[] { 9.1, 10, 12, 16, 20, 22, 25, 30, 40 })
        foreach (double z in new[] { 0.01, 0.3, 1.0 })
        foreach (var s in new[] { Stripping.None, Stripping.Partial, Stripping.Full, Stripping.HydrogenAndHelium })
        foreach (var r in new[] { Rotation.Fast, Rotation.Extreme })
        {
            var o = Compute(m, z, s, r, CoreField.Strong);
            string where = $"{m} M☉ Z {z} {s} {r}: {o.Type}";
            if (o.Type is SupernovaType.SlsnI or SupernovaType.SlsnII)
            {
                Assert.True(o.Inputs.InitialMass > 20, where);
                Assert.True(o.EjectaMass!.Value >= 2.2, where);
            }
            bool over = o.MagnetarEnergyErg is not null && o.PeakLuminosityErgS is { Value: > 2e45 };
            if (over) bright++;
            Assert.Equal(over, o.Notes.Any(n => n.Contains("brighter than any supernova ever observed", StringComparison.Ordinal)));
        }
        Assert.True(bright > 0);
    }

    [Fact]
    public void HydrogenRichMagnetar_IsJudgedAgain_OnThePinnedEjecta()
    {
        // Ruling R44 with R22/R53: 25 M☉, partly stripped, fast rotation, strong field: SLSN-II in the model (M_ej 5.8 M☉).
        // Pinned with a heavy, slow ejecta the magnetar's peak drops under R22's 7e43 erg/s: it keeps its base class.
        var x = LabInputs.Default with { InitialMass = 25, Stripping = Stripping.Partial, Rotation = Rotation.Fast, Field = CoreField.Strong };
        var model = Engine.Compute(x);
        Assert.Equal(SupernovaType.SlsnII, model.Type);
        var heavy = Engine.Compute(x, new PresetPins(1e51, 60, null, null, Published));
        Assert.True(heavy.PeakLuminosityErgS!.Value < 7e43, $"peak {heavy.PeakLuminosityErgS.Value:e3}");
        Assert.NotEqual(SupernovaType.SlsnII, heavy.Type);
        Assert.Contains(heavy.Notes, n => n.Contains("not superluminous", StringComparison.Ordinal));
        Assert.DoesNotContain(heavy.Notes, n => n.Contains("superluminous supernova with hydrogen lines", StringComparison.Ordinal));
        Assert.Equal(SupernovaType.SlsnII, Engine.Compute(x, new PresetPins(null, model.EjectaMass!.Value, null, null, Published)).Type);
    }
}
