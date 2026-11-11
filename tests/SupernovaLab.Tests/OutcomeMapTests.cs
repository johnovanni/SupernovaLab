using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;

namespace SupernovaLab.Tests;

public class OutcomeMapTests
{
    private static readonly OutcomeEngine Engine = new(ExplodabilityTable.LoadEmbedded());

    [Fact]
    public void Strip_IsLogSpacedFrom08To300()
    {
        var cells = OutcomeMap.Strip(Engine, LabInputs.Default);
        Assert.Equal(400, cells.Count);
        Assert.Equal(0.8, cells[0].InitialMass, 9);
        Assert.Equal(300, cells[^1].InitialMass, 9);
        // Equal ratios: (300 / 0.8)^(1/399) = 375^(1/399) = 1.014965.
        for (int i = 1; i < cells.Count; i++)
            Assert.Equal(1.014965, cells[i].InitialMass / cells[i - 1].InitialMass, 6);
    }

    [Fact]
    public void Strip_Default_ShowsTheBlackHoleIslandAndWhiteDwarfs()
    {
        var cells = OutcomeMap.Strip(Engine, LabInputs.Default);
        var island = cells.Where(c => c.InitialMass > 22 && c.InitialMass < 25).ToList();
        Assert.Contains(island, c => c.Remnant == RemnantKind.BlackHole);
        double firstBh = island.First(c => c.Remnant == RemnantKind.BlackHole).InitialMass;
        Assert.Contains(cells, c => c.InitialMass < firstBh && c.InitialMass > 9 && c.Fate == Fate.Supernova);
        Assert.Contains(cells, c => c.InitialMass > 25 && c.InitialMass < 27 && c.Fate == Fate.Supernova);
        Assert.Contains(cells, c => c.InitialMass < 8 && c.Fate == Fate.WhiteDwarf && c.Remnant == RemnantKind.WhiteDwarf);
    }

    [Fact]
    public void Strip_KeepsTheTemplatesOtherInputs()
    {
        var template = LabInputs.Default with { Metallicity = 0.001, Stripping = Stripping.Full };
        var cells = OutcomeMap.Strip(Engine, template, samples: 50);
        Assert.Equal(50, cells.Count);
        foreach (var c in cells)
        {
            var o = Engine.Compute(template with { InitialMass = c.InitialMass });
            Assert.Equal(o.Fate, c.Fate);
            Assert.Equal(o.Type, c.Type);
            Assert.Equal(o.Remnant.Kind, c.Remnant);
        }
    }

    [Fact]
    public void Strip_OfAWhiteDwarfTemplate_MapsTheMassiveStarPath()
    {
        var cells = OutcomeMap.Strip(Engine, LabInputs.Default with { Kind = StarKind.WhiteDwarf }, samples: 20);
        Assert.Contains(cells, c => c.Fate == Fate.NotDeadYet);
        Assert.DoesNotContain(cells, c => c.Type == SupernovaType.Ia);
    }

    [Fact]
    public void Strip_NeedsTwoSamples()
        => Assert.Throws<ArgumentOutOfRangeException>(() => OutcomeMap.Strip(Engine, LabInputs.Default, samples: 1));
}
