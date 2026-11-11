using SupernovaLab.Core.Outcomes;

namespace SupernovaLab.Tests;

public class ExplodabilityTableTests
{
    private static readonly ExplodabilityTable Table = ExplodabilityTable.LoadEmbedded();

    [Fact]
    public void Mass23_FailsAsDirectBlackHole()
    {
        // outcomes §1.2 / §1.4: 22–25 M☉ gives "very few or no successful explosions", BH by direct collapse.
        var row = Table.Lookup(23.0);
        Assert.False(row.Explodes);
        Assert.Equal(CollapseResult.DirectBlackHole, row.Result);
    }

    [Fact]
    public void Mass12_Explodes() => Assert.True(Table.Lookup(12.0).Explodes);

    [Fact]
    public void Mass26_Explodes() => Assert.True(Table.Lookup(26.0).Explodes);

    [Fact]
    public void ExplodingRows_HaveEnergyBetweenPoint1And2Point5Bethe()
    {
        foreach (var row in Table.Rows.Where(r => r.Explodes))
            Assert.InRange(row.ExplosionEnergyErg, 0.1e51, 2.5e51);
    }

    // The published S16 100 M☉ WR model (N20 engine) leaves a 2.033 M☉ baryonic NS (1.75 M☉ gravitational), just above
    // the 2.0 bound. It is published data, so it is pinned to its published value instead of being altered.
    private const double PublishedWr100NsBaryonicMass = 2.03268;

    [Fact]
    public void NeutronStarRows_HaveBaryonicMassBetween1Point2And2()
    {
        // Ruling R2: the bound applies to NeutronStar rows only; fallback-BH rows explode with heavier remnants.
        foreach (var row in Table.Rows.Where(r => r.Result == CollapseResult.NeutronStar && r.InitialMass != 100.0))
            Assert.InRange(row.RemnantBaryonicMass, 1.2, 2.0);
    }

    [Fact]
    public void Mass100_NeutronStarMatchesPublishedValue()
    {
        // figure_N20_Z9.6.txt (S16 archive), 100 M☉ row: M_b,remnant = 2.03268 M☉.
        var row = Table.Lookup(100.0);
        Assert.Equal(CollapseResult.NeutronStar, row.Result);
        Assert.Equal(PublishedWr100NsBaryonicMass, row.RemnantBaryonicMass, 5);
    }

    [Fact]
    public void Mass10_MatchesS16Table7()
    {
        // outcomes §1.4, S16 Table 7 (Z9.6 engine), 10.0 M☉: E = 0.60 B, P-HOTB M_Ni = 0.031, M_rem,bary = 1.45.
        var row = Table.Lookup(10.0);
        Assert.Equal(CollapseResult.NeutronStar, row.Result);
        Assert.Equal(0.60e51, row.ExplosionEnergyErg, 0.01e51);
        Assert.Equal(0.031, row.NickelMass, 0.001);
        Assert.Equal(1.45, row.RemnantBaryonicMass, 0.01);
    }

    [Fact]
    public void Mass15_StructureMatchesS16Table2()
    {
        // outcomes §1.3 (S16 Table 2), 15 M☉: M_f = 12.603, M_He = 4.352, M_CO = 3.144, log R = 13.77 cm.
        // Envelope = 12.603 - 4.352 = 8.25 (by hand). The archive's values agree to ~0.01 M☉.
        var row = Table.Lookup(15.0);
        Assert.Equal(12.603, row.PreSnMass, 0.01);
        Assert.Equal(4.352, row.HeCoreMass, 0.015);
        Assert.Equal(3.144, row.CoCoreMass, 0.015);
        Assert.Equal(8.25, row.HydrogenEnvelopeMass, 0.02);
        Assert.Equal(5.888e13, row.RadiusCm, 0.001e13); // 10^13.77 = 5.888e13 (by hand)
    }

    [Fact]
    public void Lookup_BelowRange_ClampsToFirstRow() => Assert.Same(Table.Rows[0], Table.Lookup(5));

    [Fact]
    public void Lookup_AboveRange_ClampsToLastRow() => Assert.Same(Table.Rows[^1], Table.Lookup(500));

    [Fact]
    public void Lookup_ReturnsNearestRow() => Assert.Equal(26.0, Table.Lookup(26.04).InitialMass);

    [Fact]
    public void Rows_SpanNineTo120SolarMasses()
    {
        Assert.Equal(9.0, Table.Rows[0].InitialMass);
        Assert.Equal(120.0, Table.Rows[^1].InitialMass);
    }

    [Fact]
    public void SourceDescription_IsNotEmpty() => Assert.False(string.IsNullOrWhiteSpace(Table.SourceDescription));
}
