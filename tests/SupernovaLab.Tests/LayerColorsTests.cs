using SupernovaLab.Core.Structure;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>Task 7: the element colours (Global Constraints, spec §3.5) and the tone-map inverse that keeps them recognisable.</summary>
public class LayerColorsTests
{
    [Theory]
    [InlineData(ElementLayer.Hydrogen, "#D85A30")]
    [InlineData(ElementLayer.Helium, "#378ADD")]
    [InlineData(ElementLayer.CarbonOxygenCore, "#639922")]
    [InlineData(ElementLayer.NickelIron, "#EF9F27")]
    [InlineData(ElementLayer.IntermediateMass, "#7F77DD")]
    [InlineData(ElementLayer.UnburnedCarbonOxygen, "#888780")]
    public void ElementColours_AreTheGlobalConstraintsHex(ElementLayer layer, string hex)
    {
        Assert.Equal(hex, LayerColors.HexOf(layer));
        var (r, g, b) = LayerColors.For(layer);
        Assert.Equal(Convert.ToInt32(hex.Substring(1, 2), 16) / 255f, r, 5);
        Assert.Equal(Convert.ToInt32(hex.Substring(3, 2), 16) / 255f, g, 5);
        Assert.Equal(Convert.ToInt32(hex.Substring(5, 2), 16) / 255f, b, 5);
    }

    [Fact]
    public void EveryLayer_HasAName_AndADistinctColour()
    {
        var layers = Enum.GetValues<ElementLayer>();
        Assert.Equal(layers.Length, layers.Select(LayerColors.HexOf).Distinct().Count());
        Assert.All(layers, l => Assert.False(string.IsNullOrWhiteSpace(LayerColors.NameOf(l))));
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.3)]
    [InlineData(0.8)]
    [InlineData(0.97)]
    public void InverseTonemap_UndoesTheAcesFit(double mapped)
        => Assert.Equal(mapped, HdrPipeline.Tonemap(HdrPipeline.InverseTonemap((float)mapped)), 4);

    [Fact]
    public void FaceColour_ToneMapsAndGammasBackToTheHex()
    {
        // The HDR value of a cut face, after exposure, tone map and gamma 2.2, is the element's hex (to within a 1/255 step).
        const float exposure = 0.2f;
        foreach (var layer in Enum.GetValues<ElementLayer>())
        {
            var (hr, hg, hb) = LayerColors.For(layer);
            var (r, g, b) = LayerColors.FaceHdr(layer, exposure);
            Assert.Equal(hr, MathF.Pow(HdrPipeline.Tonemap(r * exposure), 1 / 2.2f), 2);
            Assert.Equal(hg, MathF.Pow(HdrPipeline.Tonemap(g * exposure), 1 / 2.2f), 2);
            Assert.Equal(hb, MathF.Pow(HdrPipeline.Tonemap(b * exposure), 1 / 2.2f), 2);
        }
    }
}
