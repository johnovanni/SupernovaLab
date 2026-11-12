using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Text;

namespace SupernovaLab.Tests;

/// <summary>Global constraint: every astronomy term in the UI has a plain-language tooltip from <see cref="Glossary"/>.</summary>
public class GlossaryTests
{
    private static string AppSourceDirectory([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "src", "SupernovaLab.App"));

    /// <summary>Every <c>Glossary.Key</c> referenced from the App's XAML ({x:Static} or a binding's key).</summary>
    private static IReadOnlyList<(string File, string Key)> XamlKeys()
    {
        var keys = new List<(string, string)>();
        foreach (string file in Directory.EnumerateFiles(AppSourceDirectory(), "*.xaml", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"\bGlossary\.(\w+)"))
                keys.Add((Path.GetFileName(file), m.Groups[1].Value));
        }
        return keys;
    }

    [Fact]
    public void EveryKeyReferencedFromXaml_Exists()
    {
        var keys = XamlKeys();
        Assert.True(keys.Count >= 30, $"Expected the XAML to use the glossary widely; found {keys.Count} references.");
        var missing = keys.Where(k => !Glossary.All.ContainsKey(k.Key)).Select(k => $"{k.File}: {k.Key}").ToList();
        Assert.True(missing.Count == 0, "Unknown glossary keys: " + string.Join(", ", missing));
    }

    [Fact]
    public void EveryText_IsAPlainSentence()
    {
        Assert.All(Glossary.All, kv =>
        {
            Assert.True(kv.Value.Length > 20, kv.Key);
            Assert.EndsWith(".", kv.Value.TrimEnd(')'));
        });
    }

    [Fact]
    public void SceneView_ExplainsTheOverlay()
    {
        // Task 17 gap: the overlay's text ignores the mouse, so the view's own tooltip explains the radius, the bar and
        // the schematic parts. (A tooltip cannot be captured by --snapshot's RenderTargetBitmap, so it is tested here.)
        string text = Glossary.SceneView;
        Assert.Contains("radius", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("blast wave", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scale", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("schematic", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownKey_Throws() => Assert.Throws<KeyNotFoundException>(() => Glossary.Get("NoSuchTerm"));

    /// <summary>Keys that view models hand to the UI (markers, outcome types and fates) exist too.</summary>
    [Fact]
    public void KeysFromViewModels_Exist()
    {
        foreach (var preset in PresetCatalog.LoadEmbedded())
        {
            var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
            vm.Lab.SelectedPreset = preset;
            Assert.All(vm.Timeline.Markers, m => Assert.True(Glossary.All.ContainsKey(m.GlossaryKey), $"{preset.Id}: {m.GlossaryKey}"));
            Assert.All(vm.Outcome.DetailRows.Concat(vm.Outcome.KeyNumbers).Concat(vm.Outcome.CurveRows),
                r => Assert.True(Glossary.All.ContainsKey(r.GlossaryKey), $"{preset.Id}: {r.GlossaryKey}"));
        }
        foreach (SupernovaType type in Enum.GetValues<SupernovaType>())
            if (OutcomeTexts.TypeGlossaryKey(type) is { } key) Assert.True(Glossary.All.ContainsKey(key), key);
        foreach (Fate fate in Enum.GetValues<Fate>())
            Assert.True(Glossary.All.ContainsKey(OutcomeTexts.FateGlossaryKey(fate)), fate.ToString());
        foreach (RemnantKind kind in Enum.GetValues<RemnantKind>())
            if (OutcomeTexts.RemnantGlossaryKey(kind) is { } key) Assert.True(Glossary.All.ContainsKey(key), key);
    }

    /// <summary>Spec §4: the neutrino delay from neutrino mass is ignored, and the glossary says so.</summary>
    [Fact]
    public void NeutrinoBurst_StatesThatTheMassDelayIsIgnored()
        => Assert.Contains("ignore", Glossary.Get("NeutrinoBurst"));
}
