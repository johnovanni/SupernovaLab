using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

public class OutcomeEngineTests
{
    private static readonly OutcomeEngine Engine = new(ExplodabilityTable.LoadEmbedded());

    private static Outcome Compute(double mass, double z = 1.0, Stripping stripping = Stripping.None,
        NickelMixing mixing = NickelMixing.WellMixed, NickelYield yield = NickelYield.Calibrated, bool denseCsm = false)
        => Engine.Compute(LabInputs.Default with
        {
            InitialMass = mass, Metallicity = z, Stripping = stripping, Mixing = mixing, Yield = yield, DenseCsm = denseCsm,
        });

    private static Quantity Q(double value, string unit = "M☉")
        => new(value, unit, Confidence.ModelDependent, new Source("test", "test"));

    private static IEnumerable<(string Name, Quantity? Q)> Quantities(Outcome o)
    {
        yield return ("E", o.ExplosionEnergyErg);
        yield return ("Mej", o.EjectaMass);
        yield return ("MNi", o.NickelMass);
        yield return ("Lpeak", o.PeakLuminosityErgS);
        yield return ("Emag", o.MagnetarEnergyErg);
        yield return ("tmag", o.MagnetarTimescaleS);
        var r = o.Remnant;
        yield return ("rem.M", r.Mass);
        yield return ("rem.R", r.RadiusKm);
        yield return ("rem.P", r.SpinPeriodS);
        yield return ("rem.B", r.MagneticFieldG);
        yield return ("rem.kick", r.KickKmS);
        yield return ("rem.tBH", r.FormationTimeS);
        if (o.Star is { } s)
        {
            yield return ("Mf", s.FinalMass);
            yield return ("MHe", s.HeCoreMass);
            yield return ("MCO", s.CoCoreMass);
            yield return ("Menv", s.HydrogenEnvelopeMass);
            yield return ("MHeEnv", s.HeliumEnvelopeMass);
            yield return ("R", s.RadiusCm);
            yield return ("Teff", s.SurfaceTemperatureK);
            yield return ("Mdot", s.MassLossRateMsunPerYr);
            yield return ("vwind", s.WindSpeedKmS);
        }
    }

    // ---- the brief's cases ----

    [Fact]
    public void Mass23_CollapsesStraightToABlackHole()
    {
        var o = Compute(23);
        Assert.Equal(Fate.FailedSupernova, o.Fate);
        Assert.Equal(RemnantKind.BlackHole, o.Remnant.Kind);
        Assert.Equal(SupernovaType.None, o.Type);
        Assert.Null(o.ExplosionEnergyErg);
        // S16 default BH mass = He core (outcomes §1.5); the 23 M☉ row's He core is 7.42747, M_f 16.4599.
        Assert.Equal(7.42747, o.Remnant.Mass!.Value, 4);
        Assert.Equal(16.4599 - 0.3, o.Remnant.Mass.High!.Value, 4);
        // t_BH = 0.241 ξ^-1.5 with ξ_2.5 = 0.441365: 0.441365^1.5 = 0.293221, 0.241 / 0.293221 = 0.8219 s.
        Assert.Equal(0.8219, o.Remnant.FormationTimeS!.Value, 3);
        Assert.Equal(Confidence.ModelDependent, o.Remnant.FormationTimeS.Confidence);
        Assert.Equal(0, o.Remnant.KickKmS!.Value); // direct collapse, no ejecta (outcomes §6)
        Assert.Contains(o.Notes, n => n.Contains("black hole") && n.Contains("too compact"));
    }

    [Fact]
    public void Mass12_ExplodesAsIIPWithANeutronStar()
    {
        var o = Compute(12);
        Assert.Equal(Fate.Supernova, o.Fate);
        Assert.Equal(SupernovaType.IIP, o.Type);
        Assert.Equal(RemnantKind.NeutronStar, o.Remnant.Kind);
        Assert.InRange(o.Remnant.Mass!.Value, 1.2, 1.6);
        // Lattimer & Yahil inverse on the 12 M☉ row's 1.52916 baryonic: (−1 + √1.458748) / 0.15 = 1.3852.
        Assert.Equal(1.3852, o.Remnant.Mass.Value, 3);
        Assert.InRange(o.ExplosionEnergyErg!.Value, 0.1e51, 1.0e51);
        Assert.Equal(6.54743e50, o.ExplosionEnergyErg.Value, 1e45);
        // M_ej = M_f − M_rem,bary = 10.9023 − 1.52916 = 9.37314.
        Assert.Equal(9.37314, o.EjectaMass!.Value, 3);
    }

    [Fact]
    public void Mass26_Explodes()
    {
        var o = Compute(26);
        Assert.Equal(Fate.Supernova, o.Fate);
        Assert.Equal(RemnantKind.NeutronStar, o.Remnant.Kind);
    }

    [Theory]
    [InlineData(Stripping.Partial, SupernovaType.IIb)]
    [InlineData(Stripping.Full, SupernovaType.Ib)]
    [InlineData(Stripping.HydrogenAndHelium, SupernovaType.Ic)]
    public void Mass15_StrippingSetsTheType(Stripping stripping, SupernovaType expected)
    {
        var o = Compute(15, stripping: stripping);
        Assert.Equal(Fate.Supernova, o.Fate);
        Assert.Equal(expected, o.Type);
    }

    [Fact]
    public void Mass15_FullStrippingWithWeakMixing_FollowsTheDessartThreshold()
    {
        // outcomes §8.2 step 6: with weak ⁵⁶Ni mixing, Ic is allowed up to ~1.6 M☉ of He (Dessart et al. 2012).
        // By hand: W19 He core 0.0385 × 15^1.603 = 2.9562; final 0.939 × 2.9562^0.872 = 2.4163;
        // CO 2.4163 × 1.61 / 2.45 = 1.5878; He shell 0.8284 M☉ ≤ 1.6, so Ic.
        var o = Compute(15, stripping: Stripping.Full, mixing: NickelMixing.Weak);
        double he = o.Star!.HeliumEnvelopeMass.Value;
        Assert.Equal(0.8284, he, 2);
        Assert.Equal(he > SpectralTyper.HeliumVisibleWeakMixing ? SupernovaType.Ib : SupernovaType.Ic, o.Type);
        Assert.Equal(SupernovaType.Ic, o.Type);
    }

    [Fact]
    public void DenseCsm_GivesIInWithHydrogen_AndIbnWhenStripped()
    {
        Assert.Equal(SupernovaType.IIn, Compute(16, denseCsm: true).Type);
        Assert.Equal(SupernovaType.Ibn, Compute(15, stripping: Stripping.Full, denseCsm: true).Type);
        Assert.Equal(SupernovaType.Icn, Compute(15, stripping: Stripping.HydrogenAndHelium, denseCsm: true).Type);
    }

    [Fact]
    public void Mass9_1_IsAnElectronCaptureSupernova()
    {
        // outcomes §8.2 step 1: E = 0.1 B, M_Ni = 0.003, NS 1.25 M☉ gravitational, M_ej = M_f − 1.36.
        var o = Compute(9.1);
        Assert.Equal(Fate.Supernova, o.Fate);
        Assert.Equal(SupernovaType.IIP, o.Type);
        Assert.Equal(0.003, o.NickelMass!.Value, 6);
        Assert.Equal(1e50, o.ExplosionEnergyErg!.Value, 1e44);
        Assert.Equal(1.25, o.Remnant.Mass!.Value, 6);
        Assert.Equal(8.74622 - 1.36, o.EjectaMass!.Value, 3); // 9 M☉ row's M_f
        Assert.Equal(0.003, Compute(9.1, yield: NickelYield.Theory).NickelMass!.Value, 6);
    }

    [Fact]
    public void Mass3_EndsAsAWhiteDwarf()
    {
        var o = Compute(3);
        Assert.Equal(Fate.WhiteDwarf, o.Fate);
        Assert.Equal(RemnantKind.WhiteDwarf, o.Remnant.Kind);
        Assert.Equal(SupernovaType.None, o.Type);
        Assert.Equal(0.745, o.Remnant.Mass!.Value, 3); // 0.187 × 3 + 0.184
        Assert.NotNull(o.Remnant.RadiusKm);
        Assert.Null(o.ExplosionEnergyErg);
    }

    [Fact]
    public void Mass16_TheoryNickelExceedsCalibrated()
    {
        // Ruling R8: 16, not 15, M☉ (15.0 fails in S16 N20). Theory = the 16 M☉ row's 0.110441 M☉; the calibrated
        // default halves the S16 M > 12 value (outcomes §8.2 step 5): 0.0552205.
        var theory = Compute(16, yield: NickelYield.Theory).NickelMass!;
        var calibrated = Compute(16, yield: NickelYield.Calibrated).NickelMass!;
        Assert.True(theory.Value > calibrated.Value);
        Assert.Equal(0.110441, theory.Value, 6);
        Assert.Equal(0.0552205, calibrated.Value, 6);
        Assert.Equal(Confidence.ModelDependent, theory.Confidence);
        Assert.Equal(Confidence.Speculative, calibrated.Confidence); // outcomes §8.2 step 5 calls the ×0.5 an APPROXIMATION
    }

    [Fact]
    public void RemnantModel_NeutronStarAbove2_2SolarMassesBecomesABlackHole()
    {
        Assert.Equal(RemnantKind.BlackHole, RemnantModel.NeutronStar(Q(2.3)).Kind);
        Assert.Equal(RemnantKind.NeutronStar, RemnantModel.NeutronStar(Q(2.1)).Kind);
        // 2.7 baryonic: (−1 + √1.81) / 0.15 = 2.3024 gravitational, above 2.2.
        var bh = RemnantModel.FromBaryonicMass(Q(2.7));
        Assert.Equal(RemnantKind.BlackHole, bh.Kind);
        Assert.NotNull(bh.FormationTimeS);
        Assert.Null(bh.SpinPeriodS);
    }

    [Fact]
    public void RemnantModel_LattimerYahil_MatchesTheWorkedExample()
    {
        // outcomes §1.5: M_bary = 1.56 gives M_grav = 1.41.
        Assert.Equal(1.41, RemnantModel.GravitationalMass(1.56), 2);
        Assert.Equal(1.56, RemnantModel.BaryonicMass(RemnantModel.GravitationalMass(1.56)), 9);
        var ns = RemnantModel.FromBaryonicMass(Q(1.56));
        Assert.Equal(RemnantKind.NeutronStar, ns.Kind);
        Assert.Equal(12, ns.RadiusKm!.Value);                          // outcomes §6, §8.2 step 8
        Assert.Equal(Math.Pow(10, -1.04), ns.SpinPeriodS!.Value, 6);   // Igoshev 2022 median ≈ 91 ms
        Assert.Equal(12.44, Math.Log10(ns.MagneticFieldG!.Value), 6);   // Igoshev 2022 ⟨log B⟩ = 12.44
        // Verbunt 2017 mixture mean: √(8/π) (0.42 × 77 + 0.58 × 320) = 1.595769 × 217.94 = 347.78 km/s.
        Assert.Equal(347.78, ns.KickKmS!.Value, 1);
        Assert.Null(ns.FormationTimeS);
    }

    [Fact]
    public void EveryQuantityHasASource()
    {
        double[] masses = { 0.85, 3, 9.1, 9.3, 12, 15, 16, 23, 26, 27.4, 29, 32, 40, 60, 80, 100, 150, 200, 300 };
        double[] zs = { 0.001, 0.1, 1, 2 };
        var inputs = new List<LabInputs>();
        foreach (double m in masses)
        foreach (double z in zs)
        foreach (Stripping s in Enum.GetValues<Stripping>())
        foreach (bool csm in new[] { false, true })
            inputs.Add(LabInputs.Default with { InitialMass = m, Metallicity = z, Stripping = s, DenseCsm = csm });
        inputs.Add(LabInputs.Default with { Kind = StarKind.WhiteDwarf });

        foreach (var x in inputs)
        {
            var o = Engine.Compute(x);
            Assert.NotEmpty(o.Notes);
            foreach (var (name, q) in Quantities(o))
            {
                if (q is null) continue;
                string where = $"{name} at M={x.InitialMass}, Z={x.Metallicity}, {x.Stripping}, CSM={x.DenseCsm}";
                Assert.True(q.Source is not null, where);
                Assert.False(string.IsNullOrWhiteSpace(q.Source.Citation), where);
                Assert.False(string.IsNullOrWhiteSpace(q.Source.ResearchRef), where);
                Assert.True(double.IsFinite(q.Value) && q.Value >= 0, $"{where}: {q.Value}");
            }
        }
    }

    // ---- further cases ----

    [Fact]
    public void Mass27_4_IsAFallbackBlackHoleWithNoNickel()
    {
        var o = Compute(27.4, yield: NickelYield.Theory);
        Assert.Equal(Fate.Supernova, o.Fate);
        Assert.Equal(RemnantKind.BlackHole, o.Remnant.Kind);
        Assert.Equal(4.96234, o.Remnant.Mass!.Value, 5);
        Assert.Equal(0, o.NickelMass!.Value); // the row's 2.4e-13 M☉ is a zero yield
        Assert.Equal(5.18e50, o.ExplosionEnergyErg!.Value, 1e45);
        Assert.Equal(3.0, o.Remnant.FormationTimeS!.Value);
        Assert.Equal(Confidence.Speculative, o.Remnant.FormationTimeS.Confidence);
        // Momentum-scaled kick: 347.78 × 1.4 / 4.96234 = 98.12 km/s.
        Assert.Equal(98.12, o.Remnant.KickKmS!.Value, 1);
    }

    [Fact]
    public void Mass16_PlateauLuminosityFollowsS16Eq15()
    {
        // L50 = 1.85e42 E51^(5/6) M10^(-1/2) R500^(2/3) with the 16 M☉ row: E51 = 1.25537, M_env = 8.38164,
        // R = 6.166e13 cm. 1.25537^(5/6) = 1.208690; 0.838164^(-1/2) = 1.092283; (6.166e13 / 3.4785e13)^(2/3) = 1.464683;
        // product 1.933720 × 1.85e42 = 3.5774e42 erg/s.
        var o = Compute(16);
        Assert.Equal(3.5774e42, o.PeakLuminosityErgS!.Value, 0.0005e42);
    }

    [Fact]
    public void StrippedStar_PeakFollowsArnettsRule()
    {
        // Ib: calibrated ⁵⁶Ni = Anderson 2019 Ib median 0.163; L = 2.0e43 × 0.163 = 3.26e42 erg/s.
        var o = Compute(15, stripping: Stripping.Full);
        Assert.Equal(0.163, o.NickelMass!.Value, 6);
        Assert.Equal(3.26e42, o.PeakLuminosityErgS!.Value, 1e39);
        Assert.Equal(0.88e51, o.ExplosionEnergyErg!.Value, 1e45); // Sch21 Ib/c mean
        Assert.Equal(1.42, o.Remnant.Mass!.Value, 6);              // Sch21 stripped NS average
    }

    [Fact]
    public void UltraStrippedExplosion_HasE1e50AndAnEjectaFloor()
    {
        // Rulings R9 / R13. 13.5 M☉, H&He-stripped: W19 He 0.0385 × 13.5^1.603 = 2.49682; He star 0.939 × 2.49682^0.872
        // = 2.08545; CO 2.08545 × 1.61 / 2.45 = 1.37044; M_f = 1.37044 + 0.05 = 1.42044, below 1.5712 + 0.1.
        // Remnant capped at 1.32044 baryonic → (−1 + √(1 + 0.3 × 1.32044)) / 0.15 = 1.2105 gravitational (≥ 1.2).
        var o = Compute(13.5, stripping: Stripping.HydrogenAndHelium);
        Assert.Equal(Fate.Supernova, o.Fate);
        Assert.Equal(0.1, o.EjectaMass!.Value, 9);
        Assert.Equal(Confidence.Speculative, o.EjectaMass.Confidence);
        Assert.Equal(1.2105, o.Remnant.Mass!.Value, 3);
        // R13: ejecta ≤ 0.2 M☉ is an ultra-stripped explosion with E = 1e50 erg, not Sch21's 0.88 B.
        Assert.Equal(1e50, o.ExplosionEnergyErg!.Value);
        Assert.Equal(Confidence.Speculative, o.ExplosionEnergyErg.Confidence);
        Assert.Contains(o.Notes, n => n.Contains("ultra-stripped"));
    }

    [Fact]
    public void LightestHeliumStrippedStarThatExplodes_LeavesANeutronStarOfAtLeast1_2()
    {
        // Ruling R13: collapse needs M_f ≥ M_bary(1.2) + 0.1 = 1.2 + 0.075 × 1.44 + 0.1 = 1.408 M☉. For H&He stripping
        // at Z☉, M_f = CO + 0.05 with CO = 1.61 / 2.45 × 0.939 He^0.872, He = 0.0385 M^1.603. Solving by hand:
        // CO = 1.358 → He star final 2.06652 → He 2.47095 → M = (64.1805)^(1/1.603) = 13.413 M☉.
        // Masses from 9.3 M☉ (above the electron-capture window) in 0.01 M☉ steps.
        Outcome? previous = null;
        for (int i = 0; i <= 1070; i++)
        {
            double m = 9.3 + 0.01 * i;
            var o = Compute(m, stripping: Stripping.HydrogenAndHelium);
            if (o.Fate == Fate.Supernova)
            {
                Assert.InRange(m, 13.41, 13.43);
                Assert.Equal(RemnantKind.NeutronStar, o.Remnant.Kind);
                Assert.True(o.Remnant.Mass!.Value >= 1.2, $"NS {o.Remnant.Mass.Value} at {m}");
                Assert.Equal(Fate.WhiteDwarf, previous!.Fate);
                return;
            }
            Assert.Equal(Fate.WhiteDwarf, o.Fate);
            previous = o;
        }
        Assert.Fail("No H&He-stripped star exploded below 20 M☉.");
    }

    [Fact]
    public void StrippedStarTooLightToCollapse_EndsAsAWhiteDwarf()
    {
        // 13.3 M☉ H&He-stripped: He 0.0385 × 13.3^1.603 = 2.4378; He star 0.939 × 2.4378^0.872 = 2.0424;
        // CO 2.0424 × 1.61 / 2.45 = 1.3422; M_f = 1.3922 < 1.408 (R13). The white dwarf is the CO core.
        var o = Compute(13.3, stripping: Stripping.HydrogenAndHelium);
        Assert.Equal(Fate.WhiteDwarf, o.Fate);
        Assert.Equal(SupernovaType.None, o.Type);
        Assert.Equal(RemnantKind.WhiteDwarf, o.Remnant.Kind);
        Assert.Equal(1.3422, o.Remnant.Mass!.Value, 3);
        Assert.Equal(Confidence.Speculative, o.Remnant.Mass.Confidence);
        Assert.NotNull(o.Remnant.RadiusKm);
        Assert.Null(o.ExplosionEnergyErg);
        Assert.Contains(o.Notes, n => n.Contains("never reached collapse") && n.Contains("white dwarf"));
        Assert.DoesNotContain(o.Notes, n => n.Contains("collapses")); // ProgenitorModel's collapse note is removed
    }

    [Theory]
    [InlineData(32, Fate.FailedSupernova)] // Sch21: BH for 31.5–34
    [InlineData(40, Fate.Supernova)]       // NS for 34–70
    [InlineData(80, Fate.FailedSupernova)] // BH above 70
    public void StrippedStars_FollowSchneider2021(double mass, Fate expected)
        => Assert.Equal(expected, Compute(mass, stripping: Stripping.Full).Fate);

    [Fact]
    public void HeavySingleStarAwayFromSolarZ_WithABigHeliumCoreFails()
    {
        // outcomes §8.2 step 4, M > 30: final He core ≳ 7.5 M☉ → direct BH.
        var o = Compute(40, z: 0.5);
        Assert.True(o.Star!.HeCoreMass.Value > 7.5);
        Assert.Equal(Fate.FailedSupernova, o.Fate);
    }

    [Fact]
    public void Mass200_LowZ_IsAPairInstabilitySupernova()
    {
        // He core 0.50 × 200 − 5.87 = 94.13. HW02 ⁵⁶Ni, log-interpolated between 90 (1.31) and 100 (5.82):
        // 10^(0.117271 + 0.413 × 0.647652) = 10^0.384751 = 2.39883 × 1.011000 = 2.4252. E: log-linear from 4 B at 65 to 93.5 B at 133:
        // 4e51 × 23.375^(29.13 / 68) = 4e51 × 10^0.586349 = 4e51 × 3.80189 × 1.014727 = 1.5432e52 erg.
        var o = Compute(200, z: 0.001);
        Assert.Equal(Fate.PairInstabilitySupernova, o.Fate);
        Assert.Equal(SupernovaType.Pisn, o.Type);
        Assert.Equal(RemnantKind.None, o.Remnant.Kind);
        Assert.Equal(2.4252, o.NickelMass!.Value, 3);
        Assert.Equal(1.5432e52, o.ExplosionEnergyErg!.Value, 0.0005e52);
        Assert.Equal(o.Star!.FinalMass.Value, o.EjectaMass!.Value);
    }

    [Fact]
    public void Mass100_LowZ_PulsatesThenFormsABlackHole()
    {
        // He core 0.50 × 100 − 5.87 = 44.13; M_BH = min(44.13, 45) = 44.13 (outcomes §2.3).
        var o = Compute(100, z: 0.001);
        Assert.Equal(Fate.PulsationalPairInstability, o.Fate);
        Assert.Equal(RemnantKind.BlackHole, o.Remnant.Kind);
        Assert.Equal(44.13, o.Remnant.Mass!.Value, 6);
        Assert.Equal(o.Star!.FinalMass.Value - 44.13, o.EjectaMass!.Value, 6);
        Assert.Equal(SupernovaType.IIn, o.Type); // H retained: IIn-like interacting transient
    }

    [Fact]
    public void Mass300_LowZ_CollapsesDirectlyAbovePairInstability()
    {
        var o = Compute(300, z: 0.001);
        Assert.Equal(Fate.FailedSupernova, o.Fate);
        Assert.Equal(RemnantKind.BlackHole, o.Remnant.Kind);
        Assert.Equal(o.Star!.FinalMass.Value, o.Remnant.Mass!.Value); // outcomes §8.2 step 3: M_BH = M_f
    }

    [Fact]
    public void PisnCoreAboveTheMetallicityCeiling_CollapsesToABlackHole()
    {
        // Ruling R12: Full-stripped 300 M☉ at Z = 0.2 has a 117.21 M☉ He core (PISN range) above the 0.1 Z☉ ceiling.
        var o = Compute(300, z: 0.2, stripping: Stripping.Full);
        Assert.Equal(Fate.FailedSupernova, o.Fate);
        Assert.Equal(RemnantKind.BlackHole, o.Remnant.Kind);
        Assert.Equal(117.21, o.Remnant.Mass!.Value, 2);
    }

    [Fact]
    public void Mass0_85_HasNotDied()
    {
        var o = Compute(0.85);
        Assert.Equal(Fate.NotDeadYet, o.Fate);
        Assert.Equal(RemnantKind.None, o.Remnant.Kind);
        Assert.Equal(SupernovaType.None, o.Type);
    }

    [Fact]
    public void WhiteDwarfKind_DoesNotThrowAndSaysTypeIaIsNotModeledYet()
    {
        var o = Engine.Compute(LabInputs.Default with { Kind = StarKind.WhiteDwarf });
        Assert.Equal(RemnantKind.WhiteDwarf, o.Remnant.Kind);
        Assert.Contains(o.Notes, n => n.Contains("not modeled yet"));
    }

    [Fact]
    public void RotationAndField_AreTreatedAsSlowAndNormal_WithANote()
    {
        var o = Engine.Compute(LabInputs.Default with
        {
            InitialMass = 25, Metallicity = 0.1, Rotation = Rotation.Extreme, Field = CoreField.Strong,
        });
        Assert.False(o.Star!.ChemicallyHomogeneous);
        Assert.False(o.HasGrb);
        Assert.NotEqual(RemnantKind.Magnetar, o.Remnant.Kind);
        Assert.Contains(o.Notes, n => n.Contains("not modeled yet"));
        Assert.Equal(Rotation.Extreme, o.Inputs.Rotation); // the outcome keeps the inputs as given
    }

    [Fact]
    public void Pins_ReplaceModelValuesAndAreMarkedPublished()
    {
        var src = new Source("Pinned paper", "presets §1");
        var o = Engine.Compute(LabInputs.Default, new PresetPins(1.1e51, 14, 0.07, 3e12, src));
        Assert.True(o.UsesPublishedValues);
        foreach (var (q, v) in new[] { (o.ExplosionEnergyErg!, 1.1e51), (o.EjectaMass!, 14.0), (o.NickelMass!, 0.07), (o.Star!.RadiusCm, 3e12) })
        {
            Assert.Equal(v, q.Value);
            Assert.True(q.Published);
            Assert.Equal(src, q.Source);
        }

        var partial = Engine.Compute(LabInputs.Default, new PresetPins(null, null, 0.07, null, src));
        Assert.True(partial.UsesPublishedValues);
        Assert.False(partial.ExplosionEnergyErg!.Published);
        Assert.False(Engine.Compute(LabInputs.Default).UsesPublishedValues);
    }

    [Theory]
    [InlineData(2.0, NickelMixing.Weak, SupernovaType.Ib)]
    [InlineData(1.0, NickelMixing.Weak, SupernovaType.Ic)]
    [InlineData(0.2, NickelMixing.WellMixed, SupernovaType.Ib)]
    [InlineData(0.1, NickelMixing.WellMixed, SupernovaType.Ic)]
    public void SpectralTyper_HeliumThresholdDependsOnMixing(double heShell, NickelMixing mixing, SupernovaType expected)
    {
        var star = Engine.Compute(LabInputs.Default with { InitialMass = 15, Stripping = Stripping.Full }).Star!;
        star = star with { HeliumEnvelopeMass = Q(heShell) };
        Assert.Equal(expected, SpectralTyper.Classify(star, 15, Stripping.Full, mixing).Type);
    }

    [Theory]
    [InlineData(3.0, SupernovaType.IIP)]
    [InlineData(1.0, SupernovaType.IIL)]
    [InlineData(0.4, SupernovaType.IIb)]    // hydrogen 0.6 × 0.4 = 0.24 M☉
    [InlineData(0.002, SupernovaType.IIb)]  // hydrogen 0.6 × 0.002 = 0.0012 M☉ ≥ 0.001
    [InlineData(0.0015, SupernovaType.Ib)]  // hydrogen 0.6 × 0.0015 = 0.0009 M☉ < 0.001; He shell 0.83 ≥ 0.14
    public void SpectralTyper_HydrogenThresholds(double envelope, SupernovaType expected)
    {
        // Ruling R10: II-P / II-L use the envelope mass; IIb needs ≥ 0.001 M☉ of hydrogen = X_H (0.6 at 15 M☉) × envelope.
        var star = Engine.Compute(LabInputs.Default with { InitialMass = 15, Stripping = Stripping.Full }).Star!;
        star = star with { HydrogenEnvelopeMass = Q(envelope) };
        Assert.Equal(expected, SpectralTyper.Classify(star, 15, Stripping.None, NickelMixing.WellMixed).Type);
    }
}
