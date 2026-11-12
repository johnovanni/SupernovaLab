using System.IO;
using System.Text;
using SupernovaLab.Core.Presets;

namespace SupernovaLab.Tests;

/// <summary>Ruling R47f: the preset file is parsed strictly, and its errors name the preset.</summary>
public class PresetCatalogTests
{
    private const string Observer =
        "\"observer\": { \"distancePc\": 1000, \"av\": 0, \"source\": { \"citation\": \"c\", \"researchRef\": \"r\" } }";

    private static Stream Json(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    private static string OnePreset(string extra) =>
        "{ \"presets\": [ { \"id\": \"test1\", \"name\": \"n\", \"summary\": \"s\", \"whatWasSeen\": \"w\", " +
        Observer + extra + " } ] }";

    [Fact]
    public void AWellFormedPreset_Parses()
    {
        var presets = PresetCatalog.Parse(Json(OnePreset(", \"pins\": { \"nickelMass\": 0.07, \"source\": { \"citation\": \"c\", \"researchRef\": \"r\" } }")));
        Assert.Equal("test1", Assert.Single(presets).Id);
        Assert.Equal(0.07, presets[0].Pins!.NickelMass);
    }

    [Fact]
    public void AMisspeltKey_IsRejected_NamingThePreset()
    {
        // "nickelMas" would otherwise drop a published pin without a word.
        var e = Assert.Throws<InvalidDataException>(() => PresetCatalog.Parse(Json(OnePreset(
            ", \"pins\": { \"nickelMas\": 0.07, \"source\": { \"citation\": \"c\", \"researchRef\": \"r\" } }"))));
        Assert.Contains("test1", e.Message);
        Assert.Contains("nickelMas", e.Message);
    }

    [Fact]
    public void BadJson_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => PresetCatalog.Parse(Json("{ \"presets\": [ { \"id\": ")));
        Assert.Throws<InvalidDataException>(() => PresetCatalog.Parse(Json(OnePreset(", \"inputs\": { \"stripping\": \"Sideways\" }"))));
    }
}
