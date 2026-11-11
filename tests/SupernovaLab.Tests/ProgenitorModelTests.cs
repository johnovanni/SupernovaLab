using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

public class ProgenitorModelTests
{
    private static readonly ExplodabilityTable Table = ExplodabilityTable.LoadEmbedded();
    private const double RSun = PhysicalConstants.RSun;

    private static (EarlyFate Fate, PresupernovaStar Star, IReadOnlyList<string> Notes) Evolve(LabInputs inputs)
        => ProgenitorModel.Evolve(inputs, Table);

    private static (EarlyFate Fate, PresupernovaStar Star, IReadOnlyList<string> Notes) Evolve(
        double mass, double z = 1.0, Stripping stripping = Stripping.None, Rotation rotation = Rotation.Slow)
        => Evolve(LabInputs.Default with { InitialMass = mass, Metallicity = z, Stripping = stripping, Rotation = rotation });

    private static IEnumerable<Quantity> Quantities(PresupernovaStar s) => new[]
    {
        s.FinalMass, s.HeCoreMass, s.CoCoreMass, s.HydrogenEnvelopeMass, s.HeliumEnvelopeMass, s.RadiusCm,
        s.SurfaceTemperatureK, s.MassLossRateMsunPerYr, s.WindSpeedKmS,
    };

    // ---- LabInputs ----

    [Fact]
    public void Default_IsA16SolarMassStarAtSolarMetallicity()
    {
        // Ruling R8: 16, not 15, M☉ so the default star explodes in the S16 N20 data.
        var d = LabInputs.Default;
        Assert.Equal(StarKind.MassiveStar, d.Kind);
        Assert.Equal(16, d.InitialMass);
        Assert.Equal(1.0, d.Metallicity);
        Assert.Equal(Stripping.None, d.Stripping);
        Assert.Equal(Rotation.Slow, d.Rotation);
        Assert.Equal(CoreField.Normal, d.Field);
        Assert.Equal(TypeIaChannel.NearChandrasekhar, d.IaChannel);
        Assert.Equal(1.38, d.WhiteDwarfMass);
        Assert.Equal(NickelMixing.WellMixed, d.Mixing);
        Assert.Equal(NickelYield.Calibrated, d.Yield);
        Assert.False(d.DenseCsm);
        Assert.Equal(0.07, d.Opacity);
    }

    [Fact]
    public void Clamped_HoldsEveryNumberInsideItsRange()
    {
        var low = (LabInputs.Default with
        {
            InitialMass = 0.1, Metallicity = 0, IaChannel = TypeIaChannel.SubChandrasekhar, WhiteDwarfMass = 0.5,
            Opacity = 0.01,
        }).Clamped();
        Assert.Equal(0.8, low.InitialMass);
        Assert.Equal(0.001, low.Metallicity);
        Assert.Equal(0.85, low.WhiteDwarfMass);
        Assert.Equal(0.05, low.Opacity);

        var high = (LabInputs.Default with
        {
            InitialMass = 500, Metallicity = 5, IaChannel = TypeIaChannel.Iax, WhiteDwarfMass = 2, Opacity = 1,
        }).Clamped();
        Assert.Equal(300, high.InitialMass);
        Assert.Equal(2, high.Metallicity);
        Assert.Equal(1.38, high.WhiteDwarfMass);
        Assert.Equal(0.10, high.Opacity);
    }

    [Fact]
    public void Clamped_NearChandrasekhar_FixesWhiteDwarfMassAt1Point38()
        => Assert.Equal(1.38, (LabInputs.Default with { WhiteDwarfMass = 1.0 }).Clamped().WhiteDwarfMass);

    // ---- Steps 0 and 1 ----

    [Fact]
    public void Mass0Point85_HasNotDiedYet() => Assert.Equal(EarlyFate.NotDeadYet, Evolve(0.85).Fate);

    [Fact]
    public void Mass3_BecomesWhiteDwarf_WithCummingsIfmrMass()
    {
        // Cummings et al. 2018 MIST fit, 2.85 < M_i < 3.60: M_WD = 0.187 × 3 + 0.184 = 0.561 + 0.184 = 0.745 M☉.
        var (fate, star, _) = Evolve(3);
        Assert.Equal(EarlyFate.WhiteDwarf, fate);
        Assert.Equal(0.745, star.FinalMass.Value, 0.05);
        Assert.Equal(0, star.HydrogenEnvelopeMass.Value);
    }

    [Fact]
    public void Mass9Point1_AtSolarZ_IsElectronCapture_WithThe9SolarMassRowStructure()
    {
        // outcomes §8.2 step 1: ECSN window 9.0–9.25 M☉; envelope and radius from the 9 M☉ row (S16 archive: M_f 8.74622,
        // M_He 1.5707, envelope 7.17552).
        var (fate, star, _) = Evolve(9.1);
        Assert.Equal(EarlyFate.ElectronCapture, fate);
        Assert.Equal(8.74622, star.FinalMass.Value, 1e-4);
        Assert.Equal(7.17552, star.HydrogenEnvelopeMass.Value, 1e-4);
    }

    [Fact]
    public void Mass9Point3_IsCoreCollapse() => Assert.Equal(EarlyFate.CoreCollapse, Evolve(9.3).Fate);

    [Fact]
    public void WhiteDwarfKind_IsTheChosenWhiteDwarf()
    {
        var (fate, star, _) = Evolve(LabInputs.Default with
        {
            Kind = StarKind.WhiteDwarf, IaChannel = TypeIaChannel.SubChandrasekhar, WhiteDwarfMass = 1.0,
        });
        Assert.Equal(EarlyFate.WhiteDwarf, fate);
        Assert.Equal(1.0, star.FinalMass.Value);
    }

    // ---- Step 2: presupernova structure ----

    [Fact]
    public void Mass15_NoStripping_IsCoreCollapse_RedSupergiantWithThickEnvelope()
    {
        var (fate, star, _) = Evolve(15);
        Assert.Equal(EarlyFate.CoreCollapse, fate);
        Assert.True(star.HydrogenEnvelopeMass.Value >= 2, $"H envelope {star.HydrogenEnvelopeMass.Value}");
        Assert.True(star.RadiusCm.Value > 300 * RSun, $"radius {star.RadiusCm.Value / RSun} R☉");
        Assert.Equal(Confidence.ModelDependent, star.FinalMass.Confidence);
    }

    [Fact]
    public void Mass15_FullStripping_HasNoHydrogen_AndIsCompact()
    {
        var (_, star, _) = Evolve(15, stripping: Stripping.Full);
        Assert.Equal(0, star.HydrogenEnvelopeMass.Value);
        Assert.True(star.RadiusCm.Value < 50 * RSun, $"radius {star.RadiusCm.Value / RSun} R☉");
    }

    [Fact]
    public void Mass15_FullStripping_ErodesTheS16HeCoreWithW19()
    {
        // Ruling R14: S16 cores at 15.0 M☉ (archive row): M_He = 4.36318, M_CO = 3.1564. W19 Table 4 between 4 → 3.15
        // and 6 → 4.45: 3.15 + 0.36318 × 1.30 / 2 = 3.38607 M☉ (by hand). The shell floor 3.1564 + 0.16 = 3.3164 does
        // not bind, so M_f = 3.38607 and the He shell is 0.22967 M☉.
        var (_, star, _) = Evolve(15, stripping: Stripping.Full);
        Assert.Equal(3.38607, star.FinalMass.Value, 4);
        Assert.Equal(3.1564, star.CoCoreMass.Value, 4);
        Assert.Equal(0.22967, star.HeliumEnvelopeMass.Value, 4);
        Assert.Equal(Confidence.ModelDependent, star.FinalMass.Confidence);
    }

    [Fact]
    public void Mass20_FullStripping_KeepsAtLeast0Point16OfHelium()
    {
        // Ruling R14: S16 20.0 M☉ row M_He = 6.2175, M_CO = 4.978. W19 erosion: 4.45 + 0.2175 × 1.19 / 2 = 4.57941, below
        // M_CO + 0.16 = 5.138 (by hand), so the floor binds: M_f = 5.138, Speculative.
        var (_, star, _) = Evolve(20, stripping: Stripping.Full);
        Assert.Equal(5.138, star.FinalMass.Value, 4);
        Assert.Equal(0.16, star.HeliumEnvelopeMass.Value, 9);
        Assert.Equal(Confidence.Speculative, star.FinalMass.Confidence);
    }

    [Fact]
    public void StrippedStars_UpTo30_EndBelowTheirS16HeCoreAndAboveTheirCoCore()
    {
        // Ruling R14: M_CO < M_f < M_He,i, where M_He,i is the S16 He core (interpolated between archive rows).
        var rows = Table.Rows;
        foreach (Stripping s in new[] { Stripping.Partial, Stripping.Full, Stripping.HydrogenAndHelium })
        foreach (double z in new[] { 0.001, 0.1, 0.5, 1, 2 })
        for (double m = 9.3; m <= 30.0001; m += 0.1)
        {
            int i = 1;
            while (rows[i].InitialMass < m) i++;
            double f = (m - rows[i - 1].InitialMass) / (rows[i].InitialMass - rows[i - 1].InitialMass);
            double heInitial = rows[i - 1].HeCoreMass + (rows[i].HeCoreMass - rows[i - 1].HeCoreMass) * f;
            var star = Evolve(m, z, s).Star;
            double heStar = star.FinalMass.Value - star.HydrogenEnvelopeMass.Value; // the He star under any H envelope
            string where = $"M={m:0.0}, Z={z}, {s}";
            Assert.True(heStar < heInitial, $"{where}: He star {heStar} not below M_He,i {heInitial}");
            Assert.True(star.FinalMass.Value > star.CoCoreMass.Value, where);
        }
    }

    [Fact]
    public void Mass15_PartialStripping_KeepsAThinExtendedEnvelope()
    {
        // outcomes §8.2 step 2: 0.1 M☉ of hydrogen; with X_H ≈ 0.6 (outcomes §1.3, 9–20 M☉) the H-rich envelope is
        // 0.1 / 0.6 = 0.1667 M☉ (by hand), above 0.1 M☉, so the extended (eIIb) radius 10^13 cm applies.
        var (_, star, _) = Evolve(15, stripping: Stripping.Partial);
        Assert.Equal(0.1667, star.HydrogenEnvelopeMass.Value, 0.001);
        Assert.Equal(1e13, star.RadiusCm.Value, 1e9);
    }

    [Fact]
    public void Mass15_HydrogenAndHeliumStripping_Keeps0Point05OfHelium()
    {
        var (_, star, _) = Evolve(15, stripping: Stripping.HydrogenAndHelium);
        Assert.Equal(0, star.HydrogenEnvelopeMass.Value);
        Assert.Equal(0.05, star.HeliumEnvelopeMass.Value, 1e-9);
        Assert.Equal(star.CoCoreMass.Value + 0.05, star.FinalMass.Value, 1e-9);
    }

    [Fact]
    public void SolarZ_InterpolatesBetweenTableRows()
    {
        // S16 archive rows: 15.0 M☉ M_f = 12.5966, 15.1 M☉ M_f = 12.9264. Midpoint (by hand) = 12.7615.
        var (_, star, _) = Evolve(15.05);
        Assert.Equal(12.7615, star.FinalMass.Value, 1e-3);
    }

    [Fact]
    public void LowZ_ScalesWindLoss_AndIsSpeculative()
    {
        // outcomes §2.2, anchored form M_f = M (M_f☉/M)^((Z/Z☉)^0.5): M_f☉/M = 12.5966/15 = 0.839773, ln = −0.174624;
        // × 0.1^0.5 (= 0.316228) = −0.055221; e^-0.055221 = 0.946276; × 15 = 14.194 M☉ (by hand).
        // At Z ≤ 0.1 Z☉ the star is a blue supergiant of ~30–100 R☉.
        var (_, star, _) = Evolve(15, z: 0.1);
        Assert.Equal(14.194, star.FinalMass.Value, 1e-3);
        Assert.Equal(Confidence.Speculative, star.FinalMass.Confidence);
        Assert.InRange(star.RadiusCm.Value / RSun, 30, 100);
    }

    [Fact]
    public void AboveTheTable_AtSolarZ_ContinuesThe120SolarMassModel_NotTheLastRow()
    {
        // Ruling R1: no clamped 120 M☉ row. The 120 M☉ model keeps 5.99728/120 of its mass, so 150 M☉ keeps
        // 150 × 5.99728 / 120 = 7.4966 M☉ (by hand), all of it bare He core.
        var (fate, star, _) = Evolve(150);
        Assert.Equal(7.4966, star.FinalMass.Value, 1e-3);
        Assert.Equal(star.FinalMass.Value, star.HeCoreMass.Value, 1e-9);
        Assert.Equal(0, star.HydrogenEnvelopeMass.Value);
        Assert.Equal(EarlyFate.CoreCollapse, fate);
    }

    [Fact]
    public void ThinEnvelope_AtNearSolarZ_IsCompact_NotARedSupergiant()
    {
        // 40 M☉ at Z = 0.9: M_f = 40 × (15.337/40)^(0.9^0.85) = 16.650 M☉, He core 14.688 (S16), envelope 1.96 M☉ (by
        // hand), under the 2 M☉ H03 needs for an extended red supergiant. S16's 35 and 40 M☉ models are 12–21 R☉.
        var (_, star, _) = Evolve(40, z: 0.9);
        Assert.Equal(1.96, star.HydrogenEnvelopeMass.Value, 0.01);
        Assert.True(star.RadiusCm.Value < 100 * RSun, $"radius {star.RadiusCm.Value / RSun} R☉");
        Assert.NotEqual(3500, star.SurfaceTemperatureK.Value);
        Assert.Equal(1000, star.WindSpeedKmS.Value);
    }

    [Fact]
    public void AboveSolarZ_WolfRayetFinalMass_IsFloored()
    {
        // S16 100 M☉: M_f☉ = 6.03642. The anchored law alone gives ~0.63 M☉ at 2 Z☉; the floor is
        // M_f☉ × 2^−0.6 = 6.03642 × 0.659754 = 3.9825 M☉ (by hand).
        Assert.Equal(3.9825, Evolve(100, z: 2).Star.FinalMass.Value, 1e-3);
    }

    [Fact]
    public void HydrogenMassFraction_FollowsOutcomes1Point3()
    {
        Assert.Equal(0.6, ProgenitorModel.HydrogenMassFraction(15));
        Assert.Equal(0.5, ProgenitorModel.HydrogenMassFraction(25));
    }

    [Fact]
    public void WhiteDwarfMass_IsContinuousAt7Point2()
    {
        // MIST at 7.2: 0.107 × 7.2 + 0.471 = 1.2414 M☉ (by hand). Above, the PARSEC slope is offset to meet it.
        double below = Evolve(7.2).Star.FinalMass.Value, above = Evolve(7.2001).Star.FinalMass.Value;
        Assert.Equal(1.2414, below, 1e-4);
        Assert.Equal(below, above, 1e-3);
        Assert.Equal(Confidence.Speculative, Evolve(7.5).Star.FinalMass.Confidence);
    }

    [Fact]
    public void ExtrapolatedOrFlooredStrippedValues_AreSpeculative()
    {
        // 20 M☉ Full: the R14 He-shell floor binds. 300 M☉ Full: M_He,i = 144.13, above Table 4's 120 M☉.
        Assert.Equal(Confidence.Speculative, Evolve(20, stripping: Stripping.Full).Star.CoCoreMass.Confidence);
        Assert.Equal(Confidence.Speculative, Evolve(300, stripping: Stripping.Full).Star.FinalMass.Confidence);
        Assert.Equal(Confidence.ModelDependent, Evolve(40, stripping: Stripping.Full).Star.FinalMass.Confidence);
    }

    // ---- Step 3: pair instability ----

    [Fact]
    public void Mass200_AtZ0Point001_IsPairInstability() => Assert.Equal(EarlyFate.PairInstability, Evolve(200, z: 0.001).Fate);

    [Fact]
    public void Mass100_AtZ0Point001_IsPulsationalPairInstability()
    {
        // W19: M_He = 0.50 × 100 − 5.87 = 44.13 M☉, inside the ~35–64 PPISN range (outcomes §8.2 step 3).
        var (fate, star, _) = Evolve(100, z: 0.001);
        Assert.Equal(44.13, star.HeCoreMass.Value, 0.01);
        Assert.Equal(EarlyFate.PulsationalPairInstability, fate);
    }

    [Fact]
    public void Mass300_AtZ0Point001_FollowsTheHeCoreRule()
    {
        // W19: M_He = 0.50 × 300 − 5.87 = 144.13 M☉ (by hand). Step 3: He core > 133 gives direct collapse; 64–133 PISN.
        var (fate, star, _) = Evolve(300, z: 0.001);
        Assert.Equal(144.13, star.HeCoreMass.Value, 0.01);
        Assert.Equal(EarlyFate.DirectCollapseAbovePisn, fate);
    }

    [Fact]
    public void Mass300_AtSolarZ_IsNotPairInstability()
    {
        // Ruling R12: a PISN needs Z ≤ 1/3 (H-rich) or ≤ 0.1 (stripped). Unstripped and Full-stripped both fail at Z☉.
        Assert.NotEqual(EarlyFate.PairInstability, Evolve(300).Fate);
        Assert.NotEqual(EarlyFate.PairInstability, Evolve(300, stripping: Stripping.Full).Fate);
    }

    [Fact]
    public void Mass200_AtHalfSolarZ_IsNotPairInstability() => Assert.NotEqual(EarlyFate.PairInstability, Evolve(200, z: 0.5).Fate);

    [Fact]
    public void StrippedPisnCeiling_Is0Point1SolarZ()
    {
        // 300 M☉ Full: M_He,i = 144.13; Table 4 extended to 144.13: 60.78 + 24.13 × 20.95/40 = 73.418. Eroded He star
        // = 144.13 − 70.712 × Z^0.6: Z = 0.08 → 128.59, Z = 0.2 → 117.21 (by hand). Both cores are in the PISN range;
        // only the first is under the stripped ceiling of 0.1 Z☉.
        var (lowFate, low, _) = Evolve(300, z: 0.08, stripping: Stripping.Full);
        Assert.Equal(128.59, low.HeCoreMass.Value, 0.01);
        Assert.Equal(EarlyFate.PairInstability, lowFate);

        var (highFate, high, notes) = Evolve(300, z: 0.2, stripping: Stripping.Full);
        Assert.Equal(117.21, high.HeCoreMass.Value, 0.01);
        Assert.Equal(EarlyFate.CoreCollapse, highFate);
        Assert.Contains(notes, n => n.Contains("black hole"));
    }

    // ---- CHE ----

    [Fact]
    public void ExtremeRotation_LowZ_InCheMassRange_IsChemicallyHomogeneousWithoutHydrogen()
    {
        var (_, star, notes) = Evolve(30, z: 0.1, rotation: Rotation.Extreme);
        Assert.True(star.ChemicallyHomogeneous);
        Assert.Equal(0, star.HydrogenEnvelopeMass.Value);
        Assert.NotEmpty(notes);
    }

    [Theory]
    [InlineData(30, 1.0, Rotation.Extreme)]  // Z too high
    [InlineData(15, 0.1, Rotation.Extreme)]  // below 20 M☉
    [InlineData(30, 0.1, Rotation.Fast)]     // not extreme
    public void OutsideTheCheConditions_IsNotChemicallyHomogeneous(double mass, double z, Rotation rotation)
        => Assert.False(Evolve(mass, z, rotation: rotation).Star.ChemicallyHomogeneous);

    // ---- Every quantity has a source ----

    public static IEnumerable<object[]> Sweep()
    {
        foreach (double m in new[] { 0.8, 0.85, 3, 7.5, 8.5, 9.1, 12, 15, 23, 40, 80, 150, 200, 300 })
        foreach (double z in new[] { 0.001, 0.1, 1, 2 })
        foreach (Stripping s in Enum.GetValues<Stripping>())
        foreach (Rotation r in new[] { Rotation.Slow, Rotation.Extreme })
            yield return new object[] { m, z, s, r };
    }

    [Theory]
    [MemberData(nameof(Sweep))]
    public void EveryQuantity_HasACitation_AndAFiniteNonNegativeValue(double mass, double z, Stripping stripping, Rotation rotation)
    {
        var (_, star, notes) = Evolve(mass, z, stripping, rotation);
        foreach (var q in Quantities(star))
        {
            Assert.False(string.IsNullOrWhiteSpace(q.Source.Citation));
            Assert.False(string.IsNullOrWhiteSpace(q.Source.ResearchRef));
            Assert.True(double.IsFinite(q.Value) && q.Value >= 0, $"{q.Unit} = {q.Value}");
        }
        Assert.True(star.HeCoreMass.Value <= star.FinalMass.Value + 1e-9);
        Assert.True(star.CoCoreMass.Value <= star.HeCoreMass.Value + 1e-9);
        Assert.NotEmpty(notes);
    }

    [Theory]
    [InlineData(TypeIaChannel.NearChandrasekhar)]
    [InlineData(TypeIaChannel.SubChandrasekhar)]
    [InlineData(TypeIaChannel.Iax)]
    public void WhiteDwarfKind_EveryQuantityHasACitation(TypeIaChannel channel)
    {
        var (_, star, _) = Evolve(LabInputs.Default with { Kind = StarKind.WhiteDwarf, IaChannel = channel, WhiteDwarfMass = 0.9 });
        Assert.All(Quantities(star), q => Assert.False(string.IsNullOrWhiteSpace(q.Source.Citation)));
    }

    // ---- Continuity and monotonicity of the final mass in Z (review fix round 1) ----

    private static readonly double[] SweepMasses = { 0.8, 0.85, 3, 7.5, 8.5, 9.1, 12, 15, 23, 40, 80, 150, 200, 300 };

    public static IEnumerable<object[]> MassStrippingRotation()
    {
        foreach (double m in SweepMasses)
        foreach (Stripping s in Enum.GetValues<Stripping>())
        foreach (Rotation r in new[] { Rotation.Slow, Rotation.Extreme })
            yield return new object[] { m, s, r };
    }

    /// <summary>Z = 0.001, then 0.05, 0.10, … 2.00.</summary>
    private static double[] ZGrid() => new[] { 0.001 }.Concat(Enumerable.Range(1, 40).Select(k => 0.05 * k)).ToArray();

    [Theory]
    [MemberData(nameof(MassStrippingRotation))]
    public void FinalMass_NeverRisesWithZ_AndNeverJumpsMoreThan25Percent(double mass, Stripping stripping, Rotation rotation)
    {
        double previous = double.PositiveInfinity;
        foreach (double z in ZGrid())
        {
            double final = Evolve(mass, z, stripping, rotation).Star.FinalMass.Value;
            Assert.True(final <= previous + 1e-9, $"M_f rose from {previous} to {final} at Z = {z}");
            if (!double.IsPositiveInfinity(previous))
                Assert.True((previous - final) / previous <= 0.25, $"M_f fell from {previous} to {final} at Z = {z}");
            previous = final;
        }
    }

    [Theory]
    [MemberData(nameof(MassStrippingRotation))]
    public void FinalMass_IsContinuousAcrossSolarZ(double mass, Stripping stripping, Rotation rotation)
    {
        double solar = Evolve(mass, 1.0, stripping, rotation).Star.FinalMass.Value;
        foreach (double z in new[] { 0.999, 1.001 })
        {
            double near = Evolve(mass, z, stripping, rotation).Star.FinalMass.Value;
            Assert.True(Math.Abs(near - solar) <= 0.02 * solar, $"M_f {near} at Z = {z} vs {solar} at Z☉");
        }
    }

    [Theory]
    [InlineData(Stripping.None)]
    [InlineData(Stripping.Partial)]
    [InlineData(Stripping.Full)]
    [InlineData(Stripping.HydrogenAndHelium)]
    public void FinalMass_IsContinuousAcross120SolarMasses(Stripping stripping)
    {
        double atTable = Evolve(120, stripping: stripping).Star.FinalMass.Value;
        double above = Evolve(120.01, stripping: stripping).Star.FinalMass.Value;
        Assert.True(Math.Abs(above - atTable) <= 0.05 * atTable, $"M_f {atTable} at 120 vs {above} at 120.01");
    }
}
