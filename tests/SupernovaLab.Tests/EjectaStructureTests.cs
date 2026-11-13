using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Structure;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>
/// Task 5: the ejecta's layers, velocity profile and mixing reach (structure §S1–§S3, §S10; rulings P1–P3, R-P2-12,
/// R-P2-13). Expected values are hand-computed from the research's formulas (shown in the comments) with the outcome's
/// inputs as printed by the engine, never with the code under test.
/// </summary>
public class EjectaStructureTests
{
    private const double KmS = 1e5;

    private static Outcome Preset(string id)
    {
        var p = PresetCatalog.Get(id);
        return TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
    }

    private static Outcome Inputs(LabInputs x) => TestOutcomes.Engine.Compute(x);

    private static Outcome Default16() => TestOutcomes.Star(16);
    private static Outcome StrippedIb() => Inputs(LabInputs.Default with { Stripping = Stripping.Full });
    private static Outcome StrippedIc() => Inputs(LabInputs.Default with { Stripping = Stripping.HydrogenAndHelium });
    private static Outcome PairInstability() => Inputs(LabInputs.Default with { InitialMass = 200, Metallicity = 0.1 });

    private static double Mass(EjectaStructure s, ElementLayer layer)
        => s.Layers.Where(l => l.Layer == layer).Sum(l => l.MassMsun);

    /// <summary>A red supergiant with §S1.4's worked-example inputs: E = 10⁵¹ erg, M_ej = 10 M☉, R = 1,000 R☉.</summary>
    private static Outcome WorkedExampleRsg()
        => TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.05, 1000 * PhysicalConstants.RSun);

    // ---------------------------------------------------------------- layers (§S2)

    public static TheoryData<string> SumCases => new()
    {
        "default16", "ib", "ic", "sn1987a", "sn1998bw", "ia", "pisn",
    };

    private static Outcome Case(string id) => id switch
    {
        "default16" => Default16(),
        "ib" => StrippedIb(),
        "ic" => StrippedIc(),
        "ia" => TestOutcomes.TypeIa(),
        "pisn" => PairInstability(),
        _ => Preset(id),
    };

    [Theory]
    [MemberData(nameof(SumCases))]
    public void LayerMasses_SumToEjectaMass_WithinOnePercent(string id)
    {
        var o = Case(id);
        var s = EjectaStructureModel.For(o)!;
        double ej = o.EjectaMass!.Value;
        Assert.Equal(ej, s.EjectaMassMsun, 12);
        Assert.InRange(s.Layers.Sum(l => l.MassMsun), 0.99 * ej, 1.01 * ej);
        Assert.All(s.Layers, l => Assert.True(l.MassMsun > 0, $"{id}: {l.Layer} {l.MassMsun}"));
    }

    [Fact]
    public void LayerMasses_FollowRulingP2_ForTheTestedStars()
    {
        // SN 1987A (pins E 1.1e51, M_ej 14.7, Ni 0.078; star H 12.00534, He 1.23094, M_CO 4.57716; NS 1.444 M☉ grav):
        // rem = 1.444 + 0.075 × 1.444² = 1.600385; CO = 4.57716 − 1.600385 − 0.078 = 2.898775;
        // s = (14.7 − 0.078) / (12.00534 + 1.23094 + 2.898775) = 0.906226 (inside 0.9–1.1: no note).
        // Layers: Ni 0.078, CO 2.626944, He 1.115509, H 10.879547.
        var a = EjectaStructureModel.For(Preset("sn1987a"))!;
        Assert.Equal(0.906226, a.LayerScale, 5);
        Assert.Equal(0.078, Mass(a, ElementLayer.NickelIron), 9);
        Assert.Equal(2.626944, Mass(a, ElementLayer.CarbonOxygenCore), 5);
        Assert.Equal(1.115509, Mass(a, ElementLayer.Helium), 5);
        Assert.Equal(10.879547, Mass(a, ElementLayer.Hydrogen), 5);
        Assert.DoesNotContain(a.Notes, n => n.StartsWith("Layer masses scaled"));

        // SN 1998bw (pins E 3e52, M_ej 10.9, Ni 0.7; star He 2.526147, M_CO 8.671314, H 0; black hole 2.9 M☉):
        // rem = 2.9 (a black hole's mass as given); CO = 8.671314 − 2.9 − 0.7 = 5.071314;
        // s = (10.9 − 0.7) / (2.526147 + 5.071314) = 1.342554. Layers: Ni 0.7, CO 6.808512, He 3.391488.
        var b = EjectaStructureModel.For(Preset("sn1998bw"))!;
        Assert.Equal(1.342554, b.LayerScale, 5);
        Assert.Equal(6.808512, Mass(b, ElementLayer.CarbonOxygenCore), 5);
        Assert.Equal(3.391488, Mass(b, ElementLayer.Helium), 5);

        // Default 16 M☉ (unpinned): s = 1 to rounding; CO = 3.53801 − (1.393981 + 0.075 × 1.393981²) − 0.0552205 = 1.943070.
        var c = EjectaStructureModel.For(Default16())!;
        Assert.Equal(1.0, c.LayerScale, 9);
        Assert.Equal(1.943070, Mass(c, ElementLayer.CarbonOxygenCore), 5);
        Assert.Equal(8.38164, Mass(c, ElementLayer.Hydrogen), 5);

        // Order, centre outward (ruling P3): Ni, CO/Si, He, H.
        Assert.Equal(new[] { ElementLayer.NickelIron, ElementLayer.CarbonOxygenCore, ElementLayer.Helium, ElementLayer.Hydrogen },
            c.Layers.Select(l => l.Layer));
    }

    [Fact]
    public void LayerMasses_MagnetarRemnant_UsesTheBaryonicMass()
    {
        // SN 2015bn (magnetar, 1.42 M☉ grav): rem = 1.42 + 0.075 × 1.42² = 1.57123; CO = 9.11601 − 1.57123 − 0.059.
        var o = Preset("sn2015bn");
        Assert.Equal(RemnantKind.Magnetar, o.Remnant.Kind);
        var s = EjectaStructureModel.For(o)!;
        double co = o.Star!.CoCoreMass.Value - 1.57123 - o.NickelMass!.Value;
        double scale = (o.EjectaMass!.Value - o.NickelMass.Value) / (co + o.Star.HeliumEnvelopeMass.Value);
        Assert.Equal(scale * co, Mass(s, ElementLayer.CarbonOxygenCore), 4);
    }

    [Fact]
    public void Nickel_EqualsTheOutcomesNickel()
    {
        foreach (var o in new[] { Default16(), StrippedIb(), StrippedIc(), Preset("sn1987a"), Preset("sn1998bw"), PairInstability() })
            Assert.Equal(o.NickelMass!.Value, Mass(EjectaStructureModel.For(o)!, ElementLayer.NickelIron), 12);

        // Type Ia: the innermost layer is ⁵⁶Ni plus Mazzali et al. 2007's 0.20 M☉ of stable iron group (§S2.2).
        var ia = TestOutcomes.TypeIa();
        Assert.Equal(ia.NickelMass!.Value + 0.20, Mass(EjectaStructureModel.For(ia)!, ElementLayer.NickelIron), 12);
    }

    [Fact]
    public void StrippedStars_HaveNoHydrogen_IcHasNoHelium()
    {
        foreach (var o in new[] { StrippedIb(), StrippedIc(), Preset("sn1998bw") })
            Assert.Equal(0, Mass(EjectaStructureModel.For(o)!, ElementLayer.Hydrogen));

        // The lab's H&He-stripped Ic keeps 0.05 M☉ of helium by design (outcomes §8.2 step 2): below the 0.14 M☉ that
        // shows helium lines (Hachinger et al. 2012), so it has no visible helium. That small shell is drawn as it is.
        var ic = EjectaStructureModel.For(StrippedIc())!;
        Assert.InRange(Mass(ic, ElementLayer.Helium), 0, SpectralTyper.HeliumVisibleWellMixed);
        Assert.Equal(0.05, Mass(ic, ElementLayer.Helium), 9);

        // A Wolf–Rayet Ic (60 M☉ at Z☉) has lost its helium shell completely.
        var wr = Inputs(LabInputs.Default with { InitialMass = 60 });
        Assert.Equal(SupernovaType.Ic, wr.Type);
        var s = EjectaStructureModel.For(wr)!;
        Assert.Equal(0, Mass(s, ElementLayer.Helium));
        Assert.Equal(0, Mass(s, ElementLayer.Hydrogen));
    }

    [Fact]
    public void TypeIa_HasNickelIntermediateAndUnburned_InThatOrder()
    {
        // §S2.2 example, M_Ni = 0.6, M_ej = 1.38: stable Fe 0.20 + ⁵⁶Ni 0.60 = 0.80; IME = 1.05 − 0.80 = 0.25;
        // CO = 1.38 − 1.05 = 0.33.
        var s = EjectaStructureModel.For(TestOutcomes.TypeIa())!;
        Assert.Equal(new[] { ElementLayer.NickelIron, ElementLayer.IntermediateMass, ElementLayer.UnburnedCarbonOxygen },
            s.Layers.Select(l => l.Layer));
        Assert.Equal(0.80, s.Layers[0].MassMsun, 9);
        Assert.Equal(0.25, s.Layers[1].MassMsun, 9);
        Assert.Equal(0.33, s.Layers[2].MassMsun, 9);
        Assert.Equal(1.0, s.LayerScale);
        Assert.Equal(EjectaClass.TypeIa, s.Class);

        // Sub-Chandrasekhar (M = 1.0, Ni = 0.383333): Fe+Ni = 0.145 + 0.383333 = 0.528333; CO = 0.24; IME = 0.231667.
        var sub = EjectaStructureModel.For(Inputs(LabInputs.Default with
        {
            Kind = StarKind.WhiteDwarf, IaChannel = TypeIaChannel.SubChandrasekhar, WhiteDwarfMass = 1.0,
        }))!;
        Assert.Equal(0.528333, sub.Layers[0].MassMsun, 5);
        Assert.Equal(0.231667, sub.Layers[1].MassMsun, 5);
        Assert.Equal(0.24, sub.Layers[2].MassMsun, 5);
        Assert.Contains(sub.Notes, n => n.Contains("schematic"));
    }

    [Fact]
    public void ScaledLayers_AddANote_OutsidePoint9To1Point1()
    {
        // SN 1998bw's s = 1.342554 (see above) → "×1.34".
        var s = EjectaStructureModel.For(Preset("sn1998bw"))!;
        Assert.Contains("Layer masses scaled by ×1.34 to match the published ejecta mass", s.Notes);
        Assert.DoesNotContain(EjectaStructureModel.For(Default16())!.Notes, n => n.StartsWith("Layer masses scaled"));
    }

    // ---------------------------------------------------------------- profile (§S1)

    [Fact]
    public void Class_FollowsTheBreakoutModelsTest()
    {
        // 16 M☉: 886 R☉ ≥ √(500 × 50) = 158.1 R☉ → red supergiant; SN 1987A 48.5 R☉ → blue; Cas A (IIb, 144 R☉) → blue.
        Assert.Equal(EjectaClass.RedSupergiant, EjectaStructureModel.ClassOf(Default16()));
        Assert.Equal(EjectaClass.BlueSupergiant, EjectaStructureModel.ClassOf(Preset("sn1987a")));
        Assert.Equal(EjectaClass.BlueSupergiant, EjectaStructureModel.ClassOf(Preset("casa")));
        Assert.Equal(EjectaClass.StrippedEnvelope, EjectaStructureModel.ClassOf(StrippedIb()));
        Assert.Equal(EjectaClass.TypeIa, EjectaStructureModel.ClassOf(TestOutcomes.TypeIa()));
        // Pair instability by its envelope: 36.9 M☉ of hydrogen at 50 R☉ → blue.
        Assert.Equal(EjectaClass.BlueSupergiant, EjectaStructureModel.ClassOf(PairInstability()));

        var rsg = EjectaStructureModel.For(Default16())!;
        Assert.Equal((1.0, 12.0), (rsg.Delta, rsg.N));
        var bsg = EjectaStructureModel.For(Preset("sn1987a"))!;
        Assert.Equal((1.0, 10.18), (bsg.Delta, bsg.N));
        var ia = EjectaStructureModel.For(TestOutcomes.TypeIa())!;
        Assert.Equal((1.0, 10.0), (ia.Delta, ia.N));
    }

    [Fact]
    public void TransitionVelocity_MatchesStructureS1WorkedExample()
    {
        // §S1.4: E = 1e51 erg, M = 10 M☉, δ = 1, n = 12. E/M = 1e51 / 1.989e34 = 5.0277e16 cm²/s²; coefficient
        // 2(5−1)(12−5)/((3−1)(12−3)) = 56/18 = 3.1111; v_t = √(3.1111 × 5.0277e16) = 3.95494e8 cm/s (3,955 km/s).
        // m_t = (12−3)/(12−1) = 0.8182. v_max = v_t (2 / (11 × 0.001))^{1/9} = 1.7827 v_t = 7,050 km/s.
        var s = EjectaStructureModel.For(WorkedExampleRsg())!;
        Assert.Equal(EjectaClass.RedSupergiant, s.Class);
        Assert.Equal(3955 * KmS, s.TransitionCmS, 3955 * KmS * 1e-3);
        Assert.Equal(7050 * KmS, s.MaxCmS, 7050 * KmS * 1e-3);
        Assert.Equal(0.8182, EjectaStructureModel.EnclosedMassFraction(s, s.TransitionCmS), 4);

        // §S1.4's table: M(<1,000) = 0.0523; M(<2,000) = 0.2092; M(<5,000) = 0.9780; M(<6,000) = 0.9957;
        // v(0.1) = 1,383; v(0.5) = 3,092; v(0.9) = 4,227; v(0.99) = 5,459 km/s.
        Assert.Equal(0.0523, EjectaStructureModel.EnclosedMassFraction(s, 1000 * KmS), 4);
        Assert.Equal(0.2092, EjectaStructureModel.EnclosedMassFraction(s, 2000 * KmS), 4);
        Assert.Equal(0.9780, EjectaStructureModel.EnclosedMassFraction(s, 5000 * KmS), 4);
        Assert.Equal(0.9957, EjectaStructureModel.EnclosedMassFraction(s, 6000 * KmS), 4);
        Assert.Equal(1383, EjectaStructureModel.VelocityAtMassFraction(s, 0.1) / KmS, 1383 * 1e-3);
        Assert.Equal(3092, EjectaStructureModel.VelocityAtMassFraction(s, 0.5) / KmS, 3092 * 1e-3);
        Assert.Equal(4227, EjectaStructureModel.VelocityAtMassFraction(s, 0.9) / KmS, 4227 * 1e-3);
        Assert.Equal(5459, EjectaStructureModel.VelocityAtMassFraction(s, 0.99) / KmS, 5459 * 1e-3);
    }

    [Fact]
    public void TransitionVelocity_OtherS1Cases()
    {
        // §S1.4 table: SN 1987A pins (1.1e51 erg, 14.7 M☉, n = 10.18): v_t 3,295, v_max 6,974 km/s.
        var a = EjectaStructureModel.For(Preset("sn1987a"))!;
        Assert.Equal(3295 * KmS, a.TransitionCmS, 3295 * KmS * 1e-3);
        Assert.Equal(6974 * KmS, a.MaxCmS, 6974 * KmS * 1e-3);
        // §S1.2's Kasen check: δ = 1, n = 10, 1e51 erg, 1.4 M☉: v_t = √(40/14 × 1e51 / 2.7846e33) = 1.012942e9 cm/s
        // (10,129 km/s with M☉ = 1.989e33; Kasen's eq. 1 gives 10,140, the research's 0.1% agreement).
        var ia = EjectaStructureModel.For(TestOutcomes.Custom(SupernovaType.Ia, 1e51, 1.4, 0.6, 1e9))!;
        Assert.Equal(1.012942e9, ia.TransitionCmS, 1.012942e9 * 1e-5);
        Assert.Equal(10140 * KmS, ia.TransitionCmS, 10140 * KmS * 2e-3);
    }

    [Fact]
    public void Profile_EnclosesAllMassAtVmax_AndHalfAtTheMedian()
    {
        foreach (var o in new[] { WorkedExampleRsg(), Preset("sn1987a"), StrippedIc(), TestOutcomes.TypeIa(), PairInstability() })
        {
            var s = EjectaStructureModel.For(o)!;
            Assert.Equal(0.999, EjectaStructureModel.EnclosedMassFraction(s, s.MaxCmS), 6);
            double median = EjectaStructureModel.VelocityAtMassFraction(s, 0.5);
            Assert.Equal(0.5, EjectaStructureModel.EnclosedMassFraction(s, median), 9);
            Assert.Equal(0, EjectaStructureModel.EnclosedMassFraction(s, 0));
            foreach (double q in new[] { 1e-6, 0.01, 0.3, 0.5, 0.75, 0.9, 0.99, 0.999, 0.99999 })
            {
                double v = EjectaStructureModel.VelocityAtMassFraction(s, q);
                Assert.Equal(q, EjectaStructureModel.EnclosedMassFraction(s, v), 12);
                Assert.Equal(1, EjectaStructureModel.VelocityAtMassFraction(s, EjectaStructureModel.EnclosedMassFraction(s, v)) / v, 9);
            }
        }
    }

    // ---------------------------------------------------------------- mixing reach (§S3)

    [Fact]
    public void MixingReach_PerClass_FollowsTheS3Table()
    {
        // Red supergiant: tip 1.0 v_t, hydrogen 0.2 v_t, K 8, f 0.2, F_Ni 0.5.
        var rsg = EjectaStructureModel.For(Default16())!;
        Assert.Equal(1.0 * rsg.TransitionCmS, rsg.Mixing.NickelMaxCmS, 6);
        Assert.Equal(0.2 * rsg.TransitionCmS, rsg.Mixing.HydrogenMinCmS, 6);
        Assert.Equal((8, 0.2, 0.5), (rsg.Mixing.FingerCount, rsg.Mixing.CoveredFraction, rsg.Mixing.NickelFingerFraction));
        Assert.Equal(Confidence.Speculative, rsg.Mixing.Confidence);

        // Blue supergiant (Cas A, a compact IIb): 1.4 v_t, 0.2 v_t, K 4, f 0.1.
        var bsg = EjectaStructureModel.For(Preset("casa"))!;
        Assert.Equal(1.4 * bsg.TransitionCmS, bsg.Mixing.NickelMaxCmS, 6);
        Assert.Equal(0.2 * bsg.TransitionCmS, bsg.Mixing.HydrogenMinCmS, 6);
        Assert.Equal((4, 0.1, 0.5), (bsg.Mixing.FingerCount, bsg.Mixing.CoveredFraction, bsg.Mixing.NickelFingerFraction));

        // Stripped: 0.4 v_t, no hydrogen to mix, K 10, f 0.2.
        var ib = EjectaStructureModel.For(StrippedIb())!;
        Assert.Equal(0.4 * ib.TransitionCmS, ib.Mixing.NickelMaxCmS, 6);
        Assert.True(double.IsNaN(ib.Mixing.HydrogenMinCmS));
        Assert.Equal((10, 0.2, 0.5), (ib.Mixing.FingerCount, ib.Mixing.CoveredFraction, ib.Mixing.NickelFingerFraction));

        // SN 1987A (HasEquatorialRing): the observed 4,700 and 700 km/s, K 4, f 0.1, F_Ni 0.5.
        var sn = EjectaStructureModel.For(Preset("sn1987a"))!;
        Assert.Equal(4700 * KmS, sn.Mixing.NickelMaxCmS);
        Assert.Equal(700 * KmS, sn.Mixing.HydrogenMinCmS);
        Assert.Equal((4, 0.1, 0.5), (sn.Mixing.FingerCount, sn.Mixing.CoveredFraction, sn.Mixing.NickelFingerFraction));

        // Type Ia: stratified, no fingers.
        var ia = EjectaStructureModel.For(TestOutcomes.TypeIa())!;
        Assert.Equal((0, 0.0, 0.0), (ia.Mixing.FingerCount, ia.Mixing.CoveredFraction, ia.Mixing.NickelFingerFraction));
        Assert.False(ia.Mixing.HasFingers);
    }

    [Fact]
    public void Frame99_IsTheSpeedEnclosing99PercentOfTheParticleMass()
    {
        // Without mixing (Type Ia) it is the profile's speed at 0.999 × 0.99 of the mass (ruling P1: particles sit at
        // v(0.999 q)). With mixing, hydrogen pulled inward lowers it a little, never raises it.
        var ia = EjectaStructureModel.For(TestOutcomes.TypeIa())!;
        Assert.Equal(EjectaStructureModel.VelocityAtMassFraction(ia, 0.999 * 0.99), ia.Frame99CmS, 6);
        foreach (var o in new[] { Default16(), Preset("sn1987a") })
        {
            var s = EjectaStructureModel.For(o)!;
            double unmixed = EjectaStructureModel.VelocityAtMassFraction(s, 0.999 * 0.99);
            Assert.InRange(s.Frame99CmS, 0.95 * unmixed, unmixed);
        }
    }

    // ---------------------------------------------------------------- no ejecta (Review Focus 1), §S10

    [Fact]
    public void NoEjecta_GivesNull()
    {
        var notDead = Inputs(LabInputs.Default with { InitialMass = 0.8 });
        Assert.Equal(Fate.NotDeadYet, notDead.Fate);
        var whiteDwarf = Inputs(LabInputs.Default with { InitialMass = 3 });
        Assert.Equal(Fate.WhiteDwarf, whiteDwarf.Fate);
        var pulses = Inputs(LabInputs.Default with { InitialMass = 100, Metallicity = 0.1 });
        Assert.Equal(Fate.PulsationalPairInstability, pulses.Fate);
        Assert.Null(EjectaStructureModel.For(notDead));
        Assert.Null(EjectaStructureModel.For(whiteDwarf));
        Assert.Null(EjectaStructureModel.For(pulses));
    }

    [Fact]
    public void FailedSupernova_GivesTheS10EnvelopeShell()
    {
        // §S10 says the failed supernova does eject a weak envelope (Lovegrove & Woosley 2013; Fernández et al. 2018),
        // so For returns the shell instead of null (the brief's exception).

        // NGC 6946-BH1: a red supergiant (1,405 R☉, H 7.57 M☉) → min(4.5, 7.57) = 4.5 M☉ of hydrogen at 75 km/s.
        // E_s = ½ × 4.5 × 1.989e33 × (7.5e6)² = 2.51733e47 erg.
        var bh1 = Preset("n6946bh1");
        Assert.Equal(Fate.FailedSupernova, bh1.Fate);
        var s = EjectaStructureModel.For(bh1)!;
        Assert.True(s.IsEnvelopeShell);
        Assert.Equal(new[] { new LayerMass(ElementLayer.Hydrogen, 4.5) }, s.Layers);
        Assert.Equal(4.5, s.EjectaMassMsun);
        Assert.Equal(75 * KmS, s.MaxCmS);
        Assert.Equal(75 * KmS, s.Frame99CmS);
        Assert.Equal(2.51733e47, s.EnergyErg, 2.51733e47 * 1e-5);
        Assert.False(s.Mixing.HasFingers);
        Assert.Contains("weak envelope ejection, Lovegrove & Woosley 2013; Fernández et al. 2018", s.Notes);
        // Every particle of the shell moves at v_s.
        Assert.Equal(0, EjectaStructureModel.EnclosedMassFraction(s, 74 * KmS));
        Assert.Equal(1, EjectaStructureModel.EnclosedMassFraction(s, 75 * KmS));
        Assert.Equal(75 * KmS, EjectaStructureModel.VelocityAtMassFraction(s, 0.3));
        // Launch: Kistler n = 3/2, ejectaMass = max(4.5, 15.8408 − 8.26828) = 7.57252, x = 8.26828 / 7.57252 → 1 (clamped),
        // R = 9.772e13 / 6.957e10 = 1404.63 R☉: t = 7226 √(1e51 / 2.51733e47) √(0.757252) (1404.63/50)
        // × (1 − 0.738 + 0.467) = 8.11647e6 s.
        Assert.Equal(8.11647e6, s.EnvelopeShellLaunchS!.Value, 8.11647e6 * 1e-4);

        // A compact hydrogen-poor star (40 M☉ at Z☉: 11.7 R☉, H 0.649 M☉) → 0.1 M☉ at 700 km/s.
        var compact = EjectaStructureModel.For(Inputs(LabInputs.Default with { InitialMass = 40 }))!;
        Assert.Equal(new[] { new LayerMass(ElementLayer.Hydrogen, 0.1) }, compact.Layers);
        Assert.Equal(700 * KmS, compact.MaxCmS);

        // A Wolf–Rayet star (200 M☉ at Z☉: no hydrogen or helium shell left) → 1e-3 M☉ at 2,300 km/s of its outer
        // layer, which is carbon/oxygen.
        var wr = EjectaStructureModel.For(Inputs(LabInputs.Default with { InitialMass = 200 }))!;
        Assert.Equal(new[] { new LayerMass(ElementLayer.CarbonOxygenCore, 1e-3) }, wr.Layers);
        Assert.Equal(2300 * KmS, wr.MaxCmS);
    }

    [Fact]
    public void FailedSupernova_ShellLaunch_MatchesTheS10Check()
    {
        // §S10's check: the S16 22 M☉ red supergiant (M_f 16.2, M_BH = M_He = 7.0, 1,168 R☉), E_s = 2.5173e47 erg:
        // ejectaMass = 16.2 − 7.0 = 9.2, x = 0.76087; t = 7226 √(1e51/2.51733e47) √(0.92) (1168/50)
        // × (1 − 0.738 x^0.8 + 0.467 x^1.2) = 7.58565e6 s (research: 7.6e6 s).
        var o = TestOutcomes.Custom(SupernovaType.None, null, null, null, 1168 * PhysicalConstants.RSun,
            TestOutcomes.BlackHole(7.0, 1), Fate.FailedSupernova);
        o = o with
        {
            Star = o.Star! with
            {
                FinalMass = TestOutcomes.Q(16.2, "M☉"), HeCoreMass = TestOutcomes.Q(7.0, "M☉"),
                CoCoreMass = TestOutcomes.Q(5.7, "M☉"), HydrogenEnvelopeMass = TestOutcomes.Q(9.2, "M☉"),
                HeliumEnvelopeMass = TestOutcomes.Q(1.3, "M☉"),
            },
        };
        var s = EjectaStructureModel.For(o)!;
        Assert.Equal(EjectaClass.RedSupergiant, s.Class);
        Assert.Equal(7.58565e6, s.EnvelopeShellLaunchS!.Value, 7.58565e6 * 1e-4);
    }

    // ---------------------------------------------------------------- sweep (Review Focus 2)

    [Fact]
    public void OutcomeMapStrip_AllFinite()
    {
        var outcomes = new List<Outcome>();
        foreach (double z in new[] { 1.0, 0.1 })
        {
            var template = LabInputs.Default with { Metallicity = z };
            foreach (var cell in OutcomeMap.Strip(TestOutcomes.Engine, template))
                outcomes.Add(TestOutcomes.Engine.Compute(template with { InitialMass = cell.InitialMass }));
        }
        outcomes.Add(TestOutcomes.Custom(SupernovaType.Ic, 1e52, 0.1, 0.01, 1e11));

        int drawn = 0;
        foreach (var o in outcomes)
        {
            var s = EjectaStructureModel.For(o);
            string what = $"{o.Inputs.InitialMass:0.##} M☉ Z {o.Inputs.Metallicity} {o.Fate} {o.Type}";
            if (s is null)
            {
                Assert.True(o.Fate is Fate.NotDeadYet or Fate.WhiteDwarf or Fate.PulsationalPairInstability, what);
                continue;
            }
            drawn++;
            Assert.NotEmpty(s.Layers);
            Assert.All(s.Layers, l => Assert.True(l.MassMsun > 0 && double.IsFinite(l.MassMsun), what));
            Assert.Equal(s.EjectaMassMsun, s.Layers.Sum(l => l.MassMsun), 9);
            foreach (double v in new[] { s.TransitionCmS, s.MaxCmS, s.Frame99CmS, s.EnergyErg })
                Assert.True(double.IsFinite(v) && v > 0, $"{what}: {v}");
            Assert.True(double.IsFinite(s.LayerScale) && s.LayerScale >= 0, what);   // 0 only if Ni ≥ M_ej (§S2)
            Assert.True(s.Frame99CmS <= s.MaxCmS, what);
            Assert.True(s.MaxCmS < PhysicalConstants.C, what);
            if (s.Mixing.HasFingers) Assert.True(double.IsFinite(s.Mixing.NickelMaxCmS), what);

            var p = ParticleSampler.Sample(s, 2000);
            Assert.All(p.Velocity, x => Assert.True(float.IsFinite(x) && Math.Abs(x) <= 1.0001f, what));
        }
        Assert.True(drawn > 200);
    }
}
