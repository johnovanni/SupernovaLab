using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;
using C5 = SupernovaLab.Tests.CompactResearch.C5;

namespace SupernovaLab.Tests;

/// <summary>The neutron star's layers and the neutron drip line (spec §4.2; compact §C5, App block).</summary>
public class NeutronStarLayersTests
{
    private static readonly Outcome Default = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.03, 3.5e13);

    private static double CrustKm(double m, double r) => NeutronStarLayers.CrustThicknessKm(m, r);

    [Fact]
    public void Layers_OrderedContiguous_FromCentreToSurface()
    {
        var L = NeutronStarLayers.For(1.4, 12);
        Assert.Equal(new[] { "Inner core", "Outer core", "Nuclear pasta", "Inner crust", "Outer crust", "Atmosphere" }, L.Select(l => l.Name));
        Assert.Equal(0, L[0].InnerRadiusKm);
        Assert.Equal(12, L[^1].OuterRadiusKm, 6);
        for (int i = 1; i < L.Count; i++) Assert.Equal(L[i - 1].OuterRadiusKm, L[i].InnerRadiusKm, 9);
    }

    [Fact]
    public void Drip_DensityAndDepthFromResearch()
    {
        Assert.InRange(NeutronStarLayers.DripDensityGcm3, C5.DripDensityLow, C5.DripDensityHigh);
        Assert.Equal(C5.DripDensityGcm3, NeutronStarLayers.DripDensityGcm3);
        Assert.InRange(NeutronStarLayers.DripDepthKm(1.4, 12), C5.DripDepth14Low, C5.DripDepth14High);
    }

    [Fact]
    public void DripDepthAndCrust_MatchTheC5Table_AtEveryMass()
    {
        foreach (var (m, drip, crust, frac) in C5.Table)
        {
            Assert.InRange(NeutronStarLayers.DripDepthKm(m, 12), drip - C5.LengthToleranceKm, drip + C5.LengthToleranceKm);
            Assert.InRange(CrustKm(m, 12), crust - C5.LengthToleranceKm, crust + C5.LengthToleranceKm);
            Assert.Equal(frac, CrustKm(m, 12) / 12, 3);
            Assert.InRange(NeutronStarLayers.DripDepthKm(m, 12), C5.DripDepthLowKm - 0.005, C5.DripDepthHighKm + 0.005);
        }
    }

    [Fact]
    public void Crust_About8PercentOfRadius_At14Msun_AndPerMassRanges()
    {
        Assert.InRange(CrustKm(1.4, 12) / 12, 0.05, 0.11);                         // the plan's range holds at 1.4 only
        Assert.Equal(C5.Crust14Fraction, CrustKm(1.4, 12) / 12, 3);
        Assert.Equal(C5.Crust12Fraction, CrustKm(1.2, 12) / 12, 3);
        Assert.Equal(C5.Crust22Fraction, CrustKm(2.2, 12) / 12, 3);
    }

    [Fact]
    public void DripLine_LiesInsideTheCrust_BetweenOuterAndInnerCrust()
    {
        var L = NeutronStarLayers.For(1.4, 12);
        var outer = L.Single(l => l.Name == "Outer crust");
        var inner = L.Single(l => l.Name == "Inner crust");
        Assert.Equal(12 - NeutronStarLayers.DripDepthKm(1.4, 12), outer.InnerRadiusKm, 9);
        Assert.Equal(outer.InnerRadiusKm, inner.OuterRadiusKm, 9);
    }

    [Fact]
    public void Pasta_IsThirtyPercentOfTheInnerCrust_AndInnerCoreHalfTheCoreRadius()
    {
        var L = NeutronStarLayers.For(1.4, 12);
        var pasta = L.Single(l => l.Name == "Nuclear pasta");
        double innerCrustTotal = L.Single(l => l.Name == "Inner crust").OuterRadiusKm - pasta.InnerRadiusKm;
        Assert.Equal(C5.PastaFractionOfInnerCrust, (pasta.OuterRadiusKm - pasta.InnerRadiusKm) / innerCrustTotal, 6);
        Assert.Equal(C5.InnerCoreFractionOfCoreRadius * pasta.InnerRadiusKm, L[0].OuterRadiusKm, 9);
    }

    [Fact]
    public void InnerCore_LabelledUnknown_PastaModelDependent()
    {
        var L = NeutronStarLayers.For(1.4, 12);
        Assert.Equal(NeutronStarLayers.UnknownCoreLabel, L[0].Label);
        Assert.Equal("Inner core: unknown", NeutronStarLayers.UnknownCoreLabel);
        Assert.Equal(Confidence.Speculative, L[0].Confidence);
        Assert.Equal(Confidence.ModelDependent, L.Single(l => l.Name == "Nuclear pasta").Confidence);
        Assert.Equal(Confidence.Established, L.Single(l => l.Name == "Outer crust").Confidence);
        Assert.All(L, l => Assert.False(string.IsNullOrWhiteSpace(l.Source.Citation)));
    }

    [Fact]
    public void DripConfidenceText_IsTheSpecsWords()
        => Assert.Equal("Established theory, observed indirectly", NeutronStarLayers.DripConfidenceText);

    [Fact]
    public void NoCrust_BeforeCrystallization_ThenCrust()
    {
        double tc = NeutronStarLayers.CrustFormationTimeS(Default);
        Assert.Equal(C5.CrustFormationTimeS, tc);
        var obs = ObserverSetup.Default;
        Assert.False(NeutronStarModel.At(Default, obs, 1, tc * 0.9)!.HasCrust);
        Assert.True(NeutronStarModel.At(Default, obs, 1, tc * 1.1)!.HasCrust);
    }

    [Theory, InlineData(1.2, 12), InlineData(1.4, 12), InlineData(2.2, 12), InlineData(1.4, 10), InlineData(2.0, 13)]
    public void Layers_ValidAcrossMassRange(double m, double r)
    {
        var L = NeutronStarLayers.For(m, r);
        Assert.Equal(6, L.Count);
        Assert.All(L, l => { Assert.True(double.IsFinite(l.InnerRadiusKm) && double.IsFinite(l.OuterRadiusKm)); Assert.True(l.OuterRadiusKm >= l.InnerRadiusKm); });
        Assert.Equal(0, L[0].InnerRadiusKm);
        Assert.Equal(r, L[^1].OuterRadiusKm, 6);
        for (int i = 1; i < L.Count; i++) Assert.Equal(L[i - 1].OuterRadiusKm, L[i].InnerRadiusKm, 9);
        Assert.True(CrustKm(m, r) > 0 && CrustKm(m, r) < 0.2 * r);
    }

    [Theory, InlineData(1.2), InlineData(1.4), InlineData(2.2)]
    public void Magnification_CrustFillsAtLeast40Percent_OfTheStrip(double m)
    {
        double crust = CrustKm(m, 12);
        int n = NeutronStarLayers.Magnification(crust, 12);
        Assert.True(n >= 2);
        Assert.True(crust * n / 12 >= 0.4);                  // the strip spans R/N of depth, drawn R long
        Assert.True(crust * (n - 1) / 12 < 0.4);             // and no more magnified than needed
        Assert.Equal(5, NeutronStarLayers.Magnification(CrustKm(1.4, 12), 12));
    }
}
