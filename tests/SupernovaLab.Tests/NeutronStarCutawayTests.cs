using SupernovaLab.App.Services;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>The Close-up's cutaway: what it shows for a neutron star, before and after the crust forms (spec §4.2; compact §C5; plan Task 7).</summary>
public class NeutronStarCutawayTests
{
    private static readonly SupernovaLab.Core.Presets.Preset Crab = PresetCatalog.Get("sn1054");

    private static CloseUpScene SceneFor(string id)
    {
        var p = PresetCatalog.Get(id);
        return new CloseUpScene(TestOutcomes.Engine.Compute(p.Inputs, p.Pins), p.Observer, 1);
    }

    private static CutawayInfo After => SceneFor("sn1054").At(1e6).Cutaway!;
    private static CutawayInfo Before => SceneFor("sn1054").At(3600).Cutaway!;

    [Fact]
    public void BeforeTheCrustForms_AHotUniformProtoNeutronStar_WithItsLabel()
    {
        var info = Before;
        Assert.False(info.HasCrust);
        Assert.Empty(info.Layers);
        Assert.Empty(info.Strip);
        Assert.Null(info.DripLabel);
        Assert.Null(info.MagnifiedLabel);
        Assert.Equal("Proto-neutron star: hot and liquid, no crust yet", info.StateLabel);
        Assert.Equal(NeutronStarLayers.ProtoNeutronStarLabel, info.StateLabel);
        Assert.DoesNotContain(info.Legend, e => e.Name.Contains("drip", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AfterTheCrustForms_TheLayersAndTheDripLine()
    {
        var info = After;
        Assert.True(info.HasCrust);
        Assert.Equal(6, info.Layers.Count);
        Assert.Null(info.StateLabel);
        var drip = info.Legend.Single(e => e.Name == "Neutron drip line");
        Assert.Contains("4.3 × 10¹¹ g/cm³", drip.Label);
        Assert.Equal("Established theory, observed indirectly", drip.ConfidenceText);
        Assert.Contains("glitches", drip.ToolTip);
        Assert.Contains("accretion outbursts", drip.ToolTip);
        Assert.Contains(info.Legend, e => e.Label == "Inner core: unknown");
        Assert.Equal(info.Layers.Count + 1, info.Legend.Count);
        double dripRadius = info.Layers.Single(l => l.Name == "Outer crust").InnerRadiusKm / info.RadiusKm;
        Assert.Equal(dripRadius, info.DripRadiusFraction, 9);
    }

    [Fact]
    public void TheStateChangesAtTheCrustFormationTime()
    {
        var scene = SceneFor("sn1054");
        double tc = NeutronStarLayers.CrustFormationTimeS(TestOutcomes.Engine.Compute(Crab.Inputs, Crab.Pins));
        Assert.False(scene.At(tc * 0.9).Cutaway!.HasCrust);
        Assert.True(scene.At(tc * 1.1).Cutaway!.HasCrust);
        Assert.Same(scene.At(tc * 2).Cutaway, scene.At(tc * 3).Cutaway);   // made once
    }

    [Fact]
    public void MagnifiedStrip_IsContiguous_CrustFillsAtLeast40Percent_AndLabelled()
    {
        var info = After;
        Assert.Equal($"Magnified ×{info.Magnification}", info.MagnifiedLabel);
        Assert.Equal(NeutronStarLayers.Magnification(NeutronStarLayers.CrustThicknessKm(SceneFor("sn1054").At(1e6).MassMsun, info.RadiusKm), info.RadiusKm), info.Magnification);
        Assert.Equal(0, info.Strip[0].StartFraction, 9);
        Assert.Equal(1, info.Strip[^1].EndFraction, 9);
        for (int i = 1; i < info.Strip.Count; i++) Assert.Equal(info.Strip[i - 1].EndFraction, info.Strip[i].StartFraction, 9);
        double crust = info.Strip.Where(s => s.Name is "Nuclear pasta" or "Inner crust" or "Outer crust").Sum(s => s.EndFraction - s.StartFraction);
        Assert.True(crust >= 0.4, $"the crust fills {crust:P1} of the strip");
        double depthShown = info.RadiusKm / info.Magnification;
        Assert.Equal(1 - NeutronStarLayers.DripDepthKm(SceneFor("sn1054").At(1e6).MassMsun, info.RadiusKm) / depthShown, info.DripStripFraction, 9);
        Assert.Equal(info.Strip[^1].StartFraction, info.DripStripFraction, 9);
    }

    [Fact]
    public void EveryNumberShown_IsAQuantityWithConfidenceAndSource()
    {
        foreach (var info in new[] { Before, After })
            foreach (var r in info.Readouts)
            {
                Assert.False(string.IsNullOrWhiteSpace(r.Value.Source.Citation), r.Label);
                Assert.False(string.IsNullOrWhiteSpace(r.Value.Source.ResearchRef), r.Label);
                Assert.True(double.IsFinite(r.Value.Value), r.Label);
            }
        var density = After.Readouts.Single(r => r.Label == "Neutron drip density");
        Assert.Equal(Confidence.Established, density.Value.Confidence);
        Assert.Equal("4.3 × 10¹¹ g/cm³", Readout.Format(density.Value));
        var depth = After.Readouts.Single(r => r.Label == "Drip depth");
        Assert.InRange(depth.Value.Value, CompactResearch.C5.DripDepthLowKm, CompactResearch.C5.DripDepthHighKm);   // §C5: 0.19-0.53 km over 2.2-1.2 Msun
        Assert.Contains(After.Readouts, r => r.Label == "Crust thickness");
    }

    [Fact]
    public void Colours_AreSchematic_WithALegendEntryForEachLayer()
    {
        var info = After;
        Assert.Equal(info.Legend.Count, info.Legend.Select(e => e.ColorHex).Distinct().Count());
        Assert.Contains(info.Notes, n => n.Contains("schematic"));
        Assert.Contains(info.Notes, n => n.Contains("to scale"));
        Assert.Contains(info.Notes, n => n.Contains("without the bending of light"));
    }

    [Fact]
    public void Other_Objects_HaveNoCutaway()
    {
        var bh = SceneFor("sn1987a");   // a neutron star or a black hole; either way, only a neutron star has a cutaway
        var st = bh.At(1e6);
        Assert.Equal(st.NeutronStar is not null, st.Cutaway is not null);
    }

    [Fact]
    public void ViewModel_ShowsTheKeyStripAndNumbers_OnlyWhileTheCutawayIsOn()
    {
        var main = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        main.Lab.SelectedPreset = Crab;
        main.Timeline.ObserverTimeS = 1e9;   // long after the crust forms
        using var vm = new CloseUpViewModel(main);
        Assert.NotNull(vm.State.NeutronStar);
        Assert.False(vm.ShowCutawayKey);
        Assert.Empty(vm.CutawayKey);
        Assert.Empty(vm.CutawayReadouts);
        vm.IsCutaway = true;
        Assert.Equal(vm.State.NeutronStar!.HasCrust, vm.ShowStrip);
        Assert.True(vm.ShowCutawayKey);
        Assert.Equal(7, vm.CutawayKey.Count);
        Assert.StartsWith("Magnified ×", vm.MagnifiedLabel);
        Assert.Contains(vm.CutawayStrip, p => p.WidthDip > 0);
        Assert.True(vm.CutawayStrip.All(p => p.XDip + p.WidthDip <= CloseUpViewModel.StripWidthDip + 1e-6));
        Assert.NotEmpty(vm.CutawayReadouts);
        Assert.Contains(vm.Notes, n => n.StartsWith("Cutaway:"));
        vm.IsCutaway = false;
        Assert.Empty(vm.CutawayKey);
        Assert.DoesNotContain(vm.Notes, n => n.StartsWith("Cutaway:"));
    }
}
