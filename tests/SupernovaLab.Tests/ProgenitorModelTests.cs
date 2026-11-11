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
    public void Mass15_FullStripping_FinalMassFollowsW19()
    {
        // outcomes §3.3: M_final ≈ 0.0548 M^1.40 for M < 30. 15^1.40 = e^(1.40 × 2.70805) = e^3.79127 = 44.30;
        // × 0.0548 = 2.43 M☉ (by hand). The model interpolates W19 Table 4, so allow 0.05.
        var (_, star, _) = Evolve(15, stripping: Stripping.Full);
        Assert.Equal(2.43, star.FinalMass.Value, 0.05);
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
        // outcomes §2.2: ΔM☉ = 15 − 12.5966 = 2.4034; × 0.1^0.5 (= 0.316228) = 0.76002; M_f = 15 − 0.76002 = 14.24 (by hand).
        // At Z ≤ 0.1 Z☉ the star is a blue supergiant of ~30–100 R☉.
        var (_, star, _) = Evolve(15, z: 0.1);
        Assert.Equal(14.240, star.FinalMass.Value, 1e-3);
        Assert.Equal(Confidence.Speculative, star.FinalMass.Confidence);
        Assert.InRange(star.RadiusCm.Value / RSun, 30, 100);
    }

    [Fact]
    public void AboveTheTable_AtSolarZ_UsesTheMetallicityScheme_NotTheLastRow()
    {
        // Ruling R1: no clamped 120 M☉ row. W19: M_He,i = 0.50 × 150 − 5.87 = 69.13. The scaled wind (95% loss, as at
        // 120 M☉) bares the star, which is then eroded as a W19 He star: Table 4 between 60 (29.53) and 80 (39.83):
        // 29.53 + (9.13 / 20) × 10.30 = 34.23 M☉ (by hand).
        var (fate, star, _) = Evolve(150);
        Assert.Equal(34.23, star.FinalMass.Value, 0.01);
        Assert.Equal(0, star.HydrogenEnvelopeMass.Value);
        Assert.Equal(EarlyFate.CoreCollapse, fate);
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
        Assert.Equal(star.HeCoreMass.Value > 133 ? EarlyFate.DirectCollapseAbovePisn : EarlyFate.PairInstability, fate);
        Assert.Equal(EarlyFate.DirectCollapseAbovePisn, fate);
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
}
