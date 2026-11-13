using System.IO;
using System.Text;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;

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

    // ---------------------------------------------------------------- tour lines (Project 2, Task 12; plan ruling P10)

    [Fact]
    public void PresetLines_LoadFromJson_WithSources()
    {
        var presets = PresetCatalog.Parse(Json(OnePreset(
            ", \"tourLines\": [ { \"sourceTimeS\": 12.4, \"text\": \"t1\", \"highlight\": \"Neutrinos\", " +
            "\"source\": { \"citation\": \"Hirata 1987 [R]\", \"researchRef\": \"presets §1\" } }, " +
            "{ \"sourceTimeS\": 100, \"text\": \"t2\", \"highlight\": null, \"source\": { \"citation\": \"c\", \"researchRef\": \"r\" } } ]")));
        var lines = presets[0].TourLines;
        Assert.Equal(2, lines.Count);
        Assert.Equal(12.4, lines[0].SourceTimeS);
        Assert.Equal(Messenger.Neutrinos, lines[0].Highlight);
        Assert.Equal("Hirata 1987 [R]", lines[0].Source.Citation);
        Assert.Null(lines[1].Highlight);
        Assert.Empty(PresetCatalog.Parse(Json(OnePreset(""))) [0].TourLines);
    }

    [Fact]
    public void ATourLine_WithoutASource_OrATime_IsRejected()
    {
        Assert.Throws<InvalidDataException>(() => PresetCatalog.Parse(Json(OnePreset(", \"tourLines\": [ { \"sourceTimeS\": 1, \"text\": \"t\" } ]"))));
        Assert.Throws<InvalidDataException>(() => PresetCatalog.Parse(Json(OnePreset(
            ", \"tourLines\": [ { \"text\": \"t\", \"source\": { \"citation\": \"c\", \"researchRef\": \"r\" } } ]"))));
    }

    [Theory]
    [InlineData("sn1987a")]
    [InlineData("sn1054")]
    [InlineData("casa")]
    [InlineData("sn2011fe")]
    [InlineData("sn1998bw")]
    [InlineData("sn2023ixf")]
    [InlineData("tycho")]
    public void TheEmbeddedHistoryLines_HaveSources_AndKeepTheirRecalledTag(string id)
    {
        var lines = PresetCatalog.Get(id).TourLines;
        Assert.NotEmpty(lines);
        Assert.Equal(lines.OrderBy(l => l.SourceTimeS).Select(l => l.SourceTimeS), lines.Select(l => l.SourceTimeS));
        foreach (var l in lines)
        {
            Assert.True(l.SourceTimeS > 0 && double.IsFinite(l.SourceTimeS));
            Assert.False(string.IsNullOrWhiteSpace(l.Text));
            Assert.False(string.IsNullOrWhiteSpace(l.Source.Citation));
            Assert.StartsWith("presets", l.Source.ResearchRef);
            // Every line says either that a paper was checked ([V], [SOURCE], verified, ATNF/Hamuy...) or that it was recalled [R].
            Assert.True(l.Source.Citation.Contains("[R") || l.Source.Citation.Contains("[V") || l.Source.Citation.Contains("[SOURCE")
                        || l.Source.ResearchRef.Contains("presets-verification"), $"{id}: '{l.Text}' has no confidence tag");
        }
    }

    [Fact]
    public void PresetsWithoutHistoryLines_HaveNone()
    {
        Assert.Empty(PresetCatalog.Get("betelgeuse").TourLines);
    }
}
