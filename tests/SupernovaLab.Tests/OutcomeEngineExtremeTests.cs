using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>Outcomes §8.2 step 7 (rotation, core field), step 9 (Type Ia) and step 3 through the engine.</summary>
public class OutcomeEngineExtremeTests
{
    private static readonly OutcomeEngine Engine = new(ExplodabilityTable.LoadEmbedded());

    private static Outcome Compute(double mass, double z = 1.0, Stripping stripping = Stripping.None,
        Rotation rotation = Rotation.Slow, CoreField field = CoreField.Normal, bool denseCsm = false)
        => Engine.Compute(LabInputs.Default with
        {
            InitialMass = mass, Metallicity = z, Stripping = stripping, Rotation = rotation, Field = field, DenseCsm = denseCsm,
        });

    private static Outcome TypeIa(TypeIaChannel channel, double wdMass = 1.38)
        => Engine.Compute(LabInputs.Default with { Kind = StarKind.WhiteDwarf, IaChannel = channel, WhiteDwarfMass = wdMass });

    // ---------------------------------------------------------------- step 7a: collapsar

    [Fact]
    public void ExtremeRotation_LowZ_FullStripped40_IsACollapsarWithAGrb()
    {
        var o = Compute(40, z: 0.1, stripping: Stripping.Full, rotation: Rotation.Extreme);
        Assert.Equal(Fate.Supernova, o.Fate);
        Assert.Equal(SupernovaType.IcBL, o.Type);
        Assert.True(o.HasGrb);
        Assert.Equal(RemnantKind.BlackHole, o.Remnant.Kind);

        // Step 4 (Sch21) says neutron star at 40 M☉, so step 7a needs M_CO ≳ 7. By hand (ProgenitorModel, W19):
        // He_i = 0.5 × 40 − 5.87 = 14.13; Z☉ final 8.04 + (0.13 / 6) × 2.35 = 8.0909; at Z = 0.1 the loss is scaled by
        // 0.1^0.6 = 0.25119: M_f = 14.13 − 6.0391 × 0.25119 = 12.613; M_CO = 8.01 + (2.223 / 4.42) × 3.62 = 9.831.
        // ProgenitorModel flags the star chemically homogeneous (extreme rotation, Z ≤ 0.3, 20–60 M☉), so step 7d leaves
        // its cores as they are.
        Assert.True(o.Star!.ChemicallyHomogeneous);
        Assert.Equal(12.613, o.Star.FinalMass.Value, 2);
        Assert.Equal(9.831, o.Star.CoCoreMass.Value, 2);
        Assert.True(o.Star.CoCoreMass.Value >= 7);

        // Cano 2017 average GRB-SN: E_K 2.5e52, M_Ni 0.4, M_ej 6; M_BH = M_f − M_ej = 12.613 − 6 = 6.613.
        Assert.Equal(2.5e52, o.ExplosionEnergyErg!.Value);
        Assert.Equal(0.4, o.NickelMass!.Value);
        Assert.Equal(6, o.EjectaMass!.Value);
        Assert.Equal(6.613, o.Remnant.Mass!.Value, 2);
        Assert.Equal(Confidence.ModelDependent, o.ExplosionEnergyErg.Confidence); // §8.3: collapsar MODEL-DEPENDENT
        Assert.Contains(o.Notes, n => n.Contains("gamma-ray burst"));
        // Arnett: 2.0e43 × 0.4 = 8.0e42 erg/s.
        Assert.Equal(8.0e42, o.PeakLuminosityErgS!.Value, 8.0e40);
    }

    [Fact]
    public void ExtremeRotation_LowZ_CheStar_IsACollapsar()
    {
        var o = Compute(25, z: 0.1, rotation: Rotation.Extreme);
        Assert.True(o.Star!.ChemicallyHomogeneous);
        Assert.Equal(0, o.Star.HydrogenEnvelopeMass.Value);
        Assert.Equal(SupernovaType.IcBL, o.Type);
        Assert.True(o.HasGrb);
        Assert.Equal(RemnantKind.BlackHole, o.Remnant.Kind);
    }

    [Theory]
    [InlineData(40, 1.0, Stripping.Full)]  // Z above 0.3 Z☉
    [InlineData(50, 0.1, Stripping.None)]  // a CHE star, but its He core is in the PPISN range
    [InlineData(16, 0.1, Stripping.Partial)] // hydrogen left
    public void ExtremeRotation_WithoutTheCollapsarConditions_HasNoGrb(double mass, double z, Stripping stripping)
    {
        // 50 M☉ unstripped at Z = 0.1 is a CHE star with a 39.7 M☉ He core, in the PPISN range, so step 3 decides it
        // before step 7.
        var o = Compute(mass, z, stripping, Rotation.Extreme);
        Assert.False(o.HasGrb);
        Assert.Contains(o.Notes, n => n.Contains("rotation", StringComparison.OrdinalIgnoreCase));
    }

    // ---------------------------------------------------------------- step 7b: magnetar

    [Fact]
    public void StrongField_FastRotation_FullStripped20_IsAMagnetarPoweredSlsnI()
    {
        var o = Compute(20, stripping: Stripping.Full, rotation: Rotation.Fast, field: CoreField.Strong);
        Assert.Equal(Fate.Supernova, o.Fate);
        Assert.Equal(SupernovaType.SlsnI, o.Type);
        Assert.Equal(RemnantKind.Magnetar, o.Remnant.Kind);
        Assert.InRange(o.Remnant.SpinPeriodS!.Value, 1e-3, 5e-3);
        Assert.Equal(2.4e-3, o.Remnant.SpinPeriodS.Value, 9);            // N17 median for fast rotation (step 7b)
        // Ruling R18: step 7b's default for fast or extreme rotation is the N17 median 0.8e14 G.
        Assert.InRange(o.Remnant.MagneticFieldG!.Value, 5e13, 1e15);
        Assert.Equal(0.8e14, o.Remnant.MagneticFieldG.Value, 1e9);
        Assert.Equal(Confidence.Speculative, o.Remnant.MagneticFieldG.Confidence); // §8.3: progenitor → P, B SPECULATIVE

        // KB10: E_p = 2e52 / 2.4² = 3.4722e51 erg; t_p = 1.3 yr × 3.156e7 s/yr × (2.4 / 10)² / 0.8² = 3.6925e6 s.
        Assert.True(o.MagnetarEnergyErg!.Value >= 1e51);
        Assert.Equal(3.4722e51, o.MagnetarEnergyErg.Value, 0.0001e51);
        Assert.Equal(3.6925e6, o.MagnetarTimescaleS!.Value, 0.0001e6);
        // E_K ≈ E_SN + E_p = 0.88e51 (Sch21) + 3.4722e51 = 4.3522e51, inside N17's 1.9–9.8 B.
        Assert.Equal(4.3522e51, o.ExplosionEnergyErg!.Value, 0.0001e51);
        Assert.True(o.PeakLuminosityErgS!.Value > 1e43);
        Assert.Contains(o.Notes, n => n.Contains("magnetar"));
        // Ruling R22: the superluminous class replaces the base class (Ib here), so its sentence is gone.
        Assert.DoesNotContain(o.Notes, n => n.Contains("Type Ib"));
    }

    [Fact]
    public void StrongField_ExtremeRotation_AtSolarZ_IsAOneMillisecondMagnetar()
    {
        var o = Compute(20, stripping: Stripping.Full, rotation: Rotation.Extreme, field: CoreField.Strong);
        Assert.False(o.HasGrb); // Z☉ is above the collapsar limit
        Assert.Equal(RemnantKind.Magnetar, o.Remnant.Kind);
        Assert.Equal(1.2e-3, o.Remnant.SpinPeriodS!.Value, 9);           // step 7b: extreme = 1–2 ms (N17 1σ low end 1.2)
        // KB10: E_p = 2e52 / 1.2² = 1.3889e52 erg.
        Assert.Equal(1.3889e52, o.MagnetarEnergyErg!.Value, 0.0001e52);
        Assert.Equal(SupernovaType.SlsnI, o.Type);
        // E_K = 0.88e51 + 13.889e51 = 14.77e51, capped at N17's 9.8 B.
        Assert.Equal(9.8e51, o.ExplosionEnergyErg!.Value);

        // Ruling R32: the peak of the spin-down power diffused through Arnett's kernel, with the outcome's E_K (9.8e51,
        // as the light curve uses) and κ = 0.2. By hand (an independent Python integration, 4,000 Simpson intervals):
        // M_ej = 3.56677 M☉, v = √(10 E / 3M) = 2.14584e9 cm/s, τ_m = √(2 κ M / (13.8 c v)) = 1.78787e6 s;
        // t_p = 4.1028e7 s × 0.12² / 0.8² = 9.2313e5 s; the diffused L_p peaks at 1.8640e45 erg/s near 19.7 d.
        // (Task 5's L_p(t_d) with KB10's t_d and the uncapped energy gave 7.09e44.)
        Assert.Equal(1.8640e45, o.PeakLuminosityErgS!.Value, 0.002 * 1.8640e45);
    }

    [Fact]
    public void StrongField_FastRotation_HydrogenRich16_IsAboveTheSuperluminousCut_SoSlsnII()
    {
        // Ruling R22 with R32's peak: SLSN-II if the diffused spin-down power peaks at ≥ 7e43 erg/s. 16 M☉ at Z☉:
        // M_ej 11.59828 M☉, M_H,env 8.38164 M☉, E_K = 1.25537e51 + 3.47222e51 = 4.72759e51 erg. Ruling R33a's opacity:
        // X̄ = 0.6 × 8.38164 / 11.59828 = 0.433597, κ = 0.2 × 1.433597 = 0.286719; v = √(10 E / 3M) = 8.2650e8 cm/s,
        // τ_m = 6.21993e6 s; t_p = 3.69252e6 s. By hand (independent Python integration of Arnett's kernel), the peak is
        // 1.3370e44 erg/s near 70.6 d, above the cut. (Task 5's L_p(t_d) gave 6.18e43 and kept it II-P; with κ = 0.2
        // the peak was 1.584e44.)
        var o = Compute(16, rotation: Rotation.Fast, field: CoreField.Strong);
        Assert.Equal(SupernovaType.SlsnII, o.Type);
        Assert.Equal(RemnantKind.Magnetar, o.Remnant.Kind);
        Assert.Equal(4.72759e51, o.ExplosionEnergyErg!.Value, 0.00001e51);
        Assert.Equal(1.3370e44, o.PeakLuminosityErgS!.Value, 0.002 * 1.3370e44);
        Assert.Contains(o.Notes, n => n.Contains("superluminous supernova with hydrogen lines"));
        Assert.DoesNotContain(o.Notes, n => n.Contains("Type II-P"));
    }

    [Fact]
    public void HydrogenRichMagnetarClass_BelowTheCut_StaysLuminousII()
    {
        // Ruling R33b: R22's sub-threshold branch, kept for preset pins (no unpinned input reaches it under R32/R33a).
        var (below, note) = OutcomeEngine.HydrogenRichMagnetarClass("It pours energy in.", 6.99e43);
        Assert.Null(below);
        Assert.Contains("not superluminous", note);
        Assert.Contains("unusually luminous", note);
        Assert.StartsWith("It pours energy in.", note);
        var (at, atNote) = OutcomeEngine.HydrogenRichMagnetarClass("It pours energy in.", 7e43);
        Assert.Equal(SupernovaType.SlsnII, at);
        Assert.Contains("superluminous supernova with hydrogen lines", atNote);
    }

    [Fact]
    public void MagnetarEjectaOpacity_WeightsHydrogen_AndKeepsKb10ForStrippedEjecta()
    {
        // Ruling R33a: κ = 0.2 (1 + X̄), X̄ = X_H M_H,env / M_ej. Stripped: exactly 0.2. 16 M☉ (X_H 0.6): 0.6 × 8.38164 /
        // 11.59828 = 0.433597, κ = 0.286719. 25 M☉ (X_H 0.5) with 8 of 10 M☉ hydrogen envelope: 0.2 × 1.4 = 0.28.
        Assert.Equal(0.2, MagnetarModel.EjectaOpacity(20, 0, 3.56677));
        Assert.Equal(0.286719, MagnetarModel.EjectaOpacity(16, 8.38164, 11.59828), 1e-6);
        Assert.Equal(0.28, MagnetarModel.EjectaOpacity(25, 8, 10), 1e-12);
    }

    [Fact]
    public void StrongField_FastRotation_HydrogenRich_AboveTheSuperluminousCut_IsSlsnII()
    {
        // 12 M☉ at Z☉ has a lighter envelope (M_ej 9.37 M☉), so its peak passes the 7e43 cut (Task 5's L_p(t_d) gave
        // 7.43e43; ruling R32's diffused peak is higher still).
        var o = Compute(12, rotation: Rotation.Fast, field: CoreField.Strong);
        Assert.Equal(SupernovaType.SlsnII, o.Type);
        Assert.True(o.PeakLuminosityErgS!.Value >= 7e43);
        Assert.Equal(Confidence.Speculative, o.PeakLuminosityErgS.Confidence);
        Assert.DoesNotContain(o.Notes, n => n.Contains("Type II-P"));
    }

    [Fact]
    public void StrongField_SlowRotation_IsAnOrdinaryIIPWithAMagnetarRemnant()
    {
        // Ruling R8: 16 M☉ rather than the brief's 15 M☉, which fails to explode in the S16 N20 data.
        var o = Compute(16, field: CoreField.Strong);
        Assert.Equal(SupernovaType.IIP, o.Type);
        Assert.Equal(RemnantKind.Magnetar, o.Remnant.Kind);
        Assert.Equal(5e14, o.Remnant.MagneticFieldG!.Value, 1e9);        // step 7b: 5e14 G otherwise (KB10; Beniamini 2019)
        Assert.Equal(Math.Pow(10, -1.04), o.Remnant.SpinPeriodS!.Value, 9); // the normal-NS distribution (§6)
        Assert.Null(o.MagnetarEnergyErg);                                  // no light-curve effect
        Assert.Contains(o.Notes, n => n.Contains("too small to change the light curve"));
        var plain = Compute(16);
        Assert.Equal(plain.ExplosionEnergyErg!.Value, o.ExplosionEnergyErg!.Value);
        Assert.Equal(plain.PeakLuminosityErgS!.Value, o.PeakLuminosityErgS!.Value);
    }

    [Fact]
    public void StrongField_OnABlackHole_HasNoEffect()
    {
        var o = Compute(23, field: CoreField.Strong);
        Assert.Equal(Fate.FailedSupernova, o.Fate);
        Assert.Equal(RemnantKind.BlackHole, o.Remnant.Kind);
        Assert.Contains(o.Notes, n => n.Contains("magnetic field") && n.Contains("no effect"));
    }

    // ---------------------------------------------------------------- step 7c: magnetorotational explosion

    [Fact]
    public void FastRotation_StrongField_BlackHoleFormingStrippedCore_IsAMagnetorotationalIcBL()
    {
        // Sch21: stripped 31.5–34 M☉ stars form black holes (step 4).
        var o = Compute(32, stripping: Stripping.Full, rotation: Rotation.Fast, field: CoreField.Strong);
        Assert.Equal(Fate.Supernova, o.Fate);
        Assert.Equal(SupernovaType.IcBL, o.Type);
        Assert.False(o.HasGrb);
        Assert.Equal(RemnantKind.BlackHole, o.Remnant.Kind);
        // T19 Ic-BL means: E 7 B, M_Ni 0.31, M_ej 4.
        Assert.Equal(7e51, o.ExplosionEnergyErg!.Value);
        Assert.Equal(0.31, o.NickelMass!.Value);
        Assert.Equal(4, o.EjectaMass!.Value);
        Assert.Equal(Confidence.Speculative, o.ExplosionEnergyErg.Confidence); // §8.3: MR explosions SPECULATIVE
        Assert.Equal(o.Star!.FinalMass.Value - 4, o.Remnant.Mass!.Value, 9);
    }

    [Fact]
    public void FastRotation_StrongField_HydrogenRichBlackHoleCore_KeepsItsHydrogenClass()
    {
        var o = Compute(23, rotation: Rotation.Fast, field: CoreField.Strong);
        Assert.Equal(Fate.Supernova, o.Fate);
        Assert.Equal(SupernovaType.IIP, o.Type);
        Assert.Equal(RemnantKind.BlackHole, o.Remnant.Kind);
        // M_ej = 4 + the H-rich envelope; the 23 M☉ row: M_f 16.4599, He core 7.42747, so 4 + 9.03243 = 13.03243.
        Assert.Equal(13.03243, o.EjectaMass!.Value, 4);
        Assert.Equal(16.4599 - 13.03243, o.Remnant.Mass!.Value, 4);
    }

    // ---------------------------------------------------------------- step 7d: rotation enlarges the cores

    [Theory]
    [InlineData(20)]
    [InlineData(25)]
    [InlineData(29)]
    public void FastRotation_OnAStrippedStar_LeavesItsCoresAndClass(double mass)
    {
        // Ruling R20: step 7d applies to unstripped stars only; a stripped star keeps its He shell and class.
        var slow = Compute(mass, z: 0.1, stripping: Stripping.Full);
        var fast = Compute(mass, z: 0.1, stripping: Stripping.Full, rotation: Rotation.Fast);
        Assert.Equal(slow.Type, fast.Type);
        Assert.Equal(slow.Star!.HeliumEnvelopeMass.Value, fast.Star!.HeliumEnvelopeMass.Value, 12);
        Assert.Equal(slow.Star.CoCoreMass.Value, fast.Star.CoCoreMass.Value, 12);
        Assert.True(fast.Star.HeliumEnvelopeMass.Value >= 0.16);
        Assert.Contains(fast.Notes, n => n.Contains("rotation") && n.Contains("stripped star"));
    }

    [Theory]
    [InlineData(75, 0.001)]
    [InlineData(80, 0.001)]
    [InlineData(75, 0.1)]
    [InlineData(80, 0.1)]
    public void FastRotation_RaisingTheHeCorePast35_LeadsToPulsationalPairInstability(double mass, double z)
    {
        // Ruling R20: step 3 re-runs on the raised He core. 75 M☉: W19 He = 0.5 × 75 − 5.87 = 31.63 < 35, raised by
        // 17.5% to 37.165; 80 M☉: 34.13 → 40.103. Both are in the PPISN range (35–64).
        var slow = Compute(mass, z);
        Assert.Equal(Fate.FailedSupernova, slow.Fate);
        var o = Compute(mass, z, rotation: Rotation.Fast);
        Assert.Equal(Fate.PulsationalPairInstability, o.Fate);
        Assert.Equal((0.5 * mass - 5.87) * 1.175, o.Star!.HeCoreMass.Value, 6);
        Assert.Contains(o.Notes, n => n.Contains("pulsational pair-instability"));
    }

    [Fact]
    public void FastRotation_NormalField_RaisesTheCores()
    {
        var slow = Compute(16).Star!;
        var o = Compute(16, rotation: Rotation.Fast);
        Assert.Equal(slow.HeCoreMass.Value * 1.175, o.Star!.HeCoreMass.Value, 6);
        Assert.Equal(slow.CoCoreMass.Value * 1.175, o.Star.CoCoreMass.Value, 6);
        Assert.Equal(slow.FinalMass.Value, o.Star.FinalMass.Value, 9);
        Assert.Equal(o.Star.FinalMass.Value - o.Star.HeCoreMass.Value, o.Star.HydrogenEnvelopeMass.Value, 9);
        Assert.Equal(Confidence.Speculative, o.Star.CoCoreMass.Confidence);
        Assert.Contains(o.Notes, n => n.Contains("rotation", StringComparison.OrdinalIgnoreCase) && n.Contains("core"));
    }

    // ---------------------------------------------------------------- inputs that do not apply

    [Theory]
    [InlineData(3.0)]   // white dwarf
    [InlineData(0.85)]  // not dead
    public void RotationAndField_OnAStarWithoutCollapse_AreNotedAsHavingNoEffect(double mass)
    {
        var o = Compute(mass, rotation: Rotation.Fast, field: CoreField.Strong);
        Assert.Contains(o.Notes, n => n.Contains("rotation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(o.Notes, n => n.Contains("magnetic field") && n.Contains("no effect"));
    }

    // ---------------------------------------------------------------- step 9: Type Ia

    [Fact]
    public void NearChandrasekhar_IsANormalIaWithNoRemnant()
    {
        var o = TypeIa(TypeIaChannel.NearChandrasekhar);
        Assert.Equal(Fate.Supernova, o.Fate);
        Assert.Equal(SupernovaType.Ia, o.Type);
        Assert.Equal(0.6, o.NickelMass!.Value);
        Assert.Equal(1.38, o.EjectaMass!.Value);
        Assert.Equal(RemnantKind.None, o.Remnant.Kind);
        // Step 9: L_peak = 2.0e43 × M_Ni = 1.2e43 erg/s.
        Assert.Equal(2.0e43 * o.NickelMass.Value, o.PeakLuminosityErgS!.Value, 0.01 * 2.0e43 * o.NickelMass.Value);
        Assert.Equal(1.2e43, o.PeakLuminosityErgS.Value, 0.012e43);
        Assert.False(o.HasGrb);
    }

    [Fact]
    public void SubChandrasekhar_097_MakesAbout03OfNickel()
    {
        var o = TypeIa(TypeIaChannel.SubChandrasekhar, 0.97);
        Assert.Equal(0.3, o.NickelMass!.Value, 3);                 // Sim et al. 2010: 0.97 M☉ → 0.3 M☉
        Assert.Equal(0.97, o.EjectaMass!.Value, 9);                // M_ej = M_WD
        Assert.Equal(SupernovaType.Ia, o.Type);
        Assert.Equal(RemnantKind.None, o.Remnant.Kind);
    }

    [Fact]
    public void SubChandrasekhar_LightWhiteDwarf_Is91bgLike()
    {
        // [APPROX] log-linear through (0.9, 0.05) and (0.97, 0.3): at 0.85,
        // log10 M_Ni = log10 0.05 − 0.05 × log10(6) / 0.07 = −1.30103 − 0.55581 = −1.85684 → 0.013904 M☉.
        var o = TypeIa(TypeIaChannel.SubChandrasekhar, 0.85);
        Assert.Equal(0.013904, o.NickelMass!.Value, 5);
        Assert.Equal(SupernovaType.Ia91bg, o.Type);
        Assert.Equal(Confidence.Speculative, o.NickelMass.Confidence);
    }

    [Fact]
    public void SubChandrasekhar_InsideSim2010_Interpolates()
    {
        // Linear between (0.97, 0.3) and (1.15, 0.8): at 1.0, 0.3 + 0.03 × 0.5 / 0.18 = 0.38333.
        var o = TypeIa(TypeIaChannel.SubChandrasekhar, 1.0);
        Assert.Equal(0.38333, o.NickelMass!.Value, 4);
        Assert.Equal(Confidence.ModelDependent, o.NickelMass.Confidence);
    }

    [Fact]
    public void SubChandrasekhar_115_MakesSim2010s08AndIs91TLike()
    {
        // Ruling R17: the published yield, not capped at half the ejecta (0.575): Sim et al. 2010, 1.15 M☉ → 0.8 M☉.
        // Step 9: 91T-like when M_Ni ≥ 0.8.
        var o = TypeIa(TypeIaChannel.SubChandrasekhar, 1.15);
        Assert.Equal(0.8, o.NickelMass!.Value, 9);
        Assert.Equal(SupernovaType.Ia91T, o.Type);
        // Arnett: 2.0e43 × 0.8 = 1.6e43 erg/s.
        Assert.Equal(1.6e43, o.PeakLuminosityErgS!.Value, 0.016e43);
    }

    [Fact]
    public void SubChandrasekhar_AboveSim2010_HoldsItsHighestYield()
    {
        // [APPROX] above Sim's 1.15 M☉ model the yield stays at 0.8 M☉ (a linear extension would reach M_ej at ~1.35).
        var o = TypeIa(TypeIaChannel.SubChandrasekhar, 1.38);
        Assert.Equal(0.8, o.NickelMass!.Value, 9);
        Assert.True(o.NickelMass.Value < o.EjectaMass!.Value);
        Assert.Equal(Confidence.Speculative, o.NickelMass.Confidence);
        Assert.Equal(SupernovaType.Ia91T, o.Type);
    }

    [Fact]
    public void Iax_LeavesABoundWhiteDwarf()
    {
        var o = TypeIa(TypeIaChannel.Iax);
        Assert.Equal(SupernovaType.Iax, o.Type);
        Assert.Equal(RemnantKind.WhiteDwarf, o.Remnant.Kind);
        // Kromer et al. 2013 N5def: ejecta 0.372, ⁵⁶Ni 0.158, E_K 1.34e50 erg; bound remnant = 1.38 − 0.372 = 1.008.
        Assert.Equal(0.372, o.EjectaMass!.Value);
        Assert.Equal(0.158, o.NickelMass!.Value);
        Assert.Equal(1.34e50, o.ExplosionEnergyErg!.Value);
        Assert.Equal(Confidence.Speculative, o.ExplosionEnergyErg.Confidence); // Iax E_K is not in the research files
        Assert.Equal(1.008, o.Remnant.Mass!.Value, 9);
    }

    [Fact]
    public void NearChandrasekhar_PhillipsOutputs()
    {
        // Ruling R21, step 9. L_peak = 1.2e43 erg/s: M_B ≈ M_bol = 4.74 − 2.5 log10(1.2e43 / 3.828e33)
        // = 4.74 − 2.5 × 9.496211 = −19.0005. Inverted Phillips 1999 (B: a 0.786, b 0.633, M_B,0 −19.3): ΔM = 0.2995,
        // u = (−0.786 + √(0.617796 + 4 × 0.633 × 0.2995)) / 1.266 = (−0.786 + 1.173090) / 1.266 = 0.305758,
        // Δm15 = 1.1 + 0.305758 = 1.4058.
        var o = TypeIa(TypeIaChannel.NearChandrasekhar);
        Assert.Equal(-19.0005, o.PeakAbsoluteMagB!.Value, 0.0005);
        Assert.Equal(1.4058, o.DeclineRateDm15B!.Value, 0.0005);
        Assert.Equal(Confidence.Speculative, o.PeakAbsoluteMagB.Confidence);
    }

    [Fact]
    public void Bright91TLike_PhillipsOutputs()
    {
        // M_Ni 0.8: L = 1.6e43; M_B = 4.74 − 2.5 log10(4.179728e9) = 4.74 − 24.05285 = −19.3129. ΔM = −0.0129,
        // u = (−0.786 + √(0.617796 − 0.032663)) / 1.266 = (−0.786 + 0.764940) / 1.266 = −0.016635, Δm15 = 1.0834.
        var o = TypeIa(TypeIaChannel.SubChandrasekhar, 1.15);
        Assert.Equal(-19.3129, o.PeakAbsoluteMagB!.Value, 3);
        Assert.Equal(1.0834, o.DeclineRateDm15B!.Value, 3);
    }

    [Fact]
    public void Faint91bgLike_DeclineRateIsClampedWithANote()
    {
        // M_Ni 0.013904: L = 2.7808e41; M_B = 4.74 − 2.5 log10(7.26437e7) = 4.74 − 19.65302 = −14.913, far fainter than
        // the relation's faint end (Δm15 = 1.7 ⇔ M_B = −19.3 + 0.786 × 0.6 + 0.633 × 0.36 = −18.6005), so Δm15 = 1.7.
        var o = TypeIa(TypeIaChannel.SubChandrasekhar, 0.85);
        Assert.Equal(-14.913, o.PeakAbsoluteMagB!.Value, 2);
        Assert.Equal(1.7, o.DeclineRateDm15B!.Value);
        Assert.Contains("[APPROX]", o.DeclineRateDm15B.Source.Citation);
        Assert.Contains(o.Notes, n => n.Contains("Phillips relation"));
    }

    [Fact]
    public void CoreCollapse_HasNoPhillipsOutputs()
    {
        var o = Compute(16);
        Assert.Null(o.PeakAbsoluteMagB);
        Assert.Null(o.DeclineRateDm15B);
    }

    [Fact]
    public void TypeIa_NotesThatRotationAndFieldDoNotApply()
    {
        var o = Engine.Compute(LabInputs.Default with { Kind = StarKind.WhiteDwarf, Rotation = Rotation.Fast, Field = CoreField.Strong });
        Assert.Equal(SupernovaType.Ia, o.Type);
        Assert.Contains(o.Notes, n => n.Contains("rotation", StringComparison.OrdinalIgnoreCase) && n.Contains("magnetic field"));
    }

    // ---------------------------------------------------------------- step 3 through the engine

    [Fact]
    public void Mass200_AtLowZ_IsAPairInstabilitySupernova()
    {
        var o = Compute(200, z: 0.001);
        Assert.Equal(Fate.PairInstabilitySupernova, o.Fate);
        Assert.Equal(SupernovaType.Pisn, o.Type);
        Assert.Equal(RemnantKind.None, o.Remnant.Kind);
    }
}
