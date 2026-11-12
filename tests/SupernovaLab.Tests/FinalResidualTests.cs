using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Text;

namespace SupernovaLab.Tests;

/// <summary>The final re-review's residuals (I5 tooltip, N1, N2, N4, M10), fixed by the controller.</summary>
public class FinalResidualTests
{
    private static MainViewModel WithPreset(string id)
    {
        var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        vm.Lab.SelectedPreset = PresetCatalog.Get(id);
        return vm;
    }

    [Fact]
    public void PhillipsTooltip_NoLongerSaysItShapesTheCurve()
    {
        Assert.DoesNotContain("which shapes the light curve", Glossary.PhillipsRelation);
        Assert.Contains("does not shape the drawn light curve", Glossary.PhillipsRelation);
    }

    [Fact]
    public void MagnetarBoostedIIP_IsNotBlamedOnTheStarsSize()
    {
        // N1: 16 M☉, Z☉, Fast rotation, Strong field is magnetar-boosted (II-P after R53 I3, M_V ≈ −21.8 in the
        // re-review's probe). The "bright because the star is large" note must not appear.
        var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        vm.Lab.Rotation = Rotation.Fast;
        vm.Lab.Field = CoreField.Strong;
        Assert.NotNull(vm.CurrentOutcome.MagnetarEnergyErg);
        Assert.DoesNotContain(vm.Outcome.Notes, n => n.StartsWith("Bright for a Type II-P", StringComparison.Ordinal));
    }

    [Fact]
    public void Ia91TSentence_DoesNotClaimTheDrawnCurveFadesSlowly()
    {
        // N2: the drawn curve does not follow the Phillips relation, so the subtype sentence must not claim a slow fade.
        var (type, note) = TypeIaModel.Subtype(0.8, published: false);
        Assert.Equal(SupernovaType.Ia91T, type);
        Assert.Contains("does not reproduce", note);
        var (type2, note2) = TypeIaModel.Subtype(0.1, published: false);
        Assert.Equal(SupernovaType.Ia91bg, type2);
        Assert.Contains("does not reproduce", note2);
    }

    [Fact]
    public void TypeIaOpacityAndLira_OnlyInTypeIaSources()
    {
        // M10: SN 1998bw (Ic-BL) must not list the Type Ia opacity or the Lira relation; SN 2011fe (Ia) must.
        Assert.DoesNotContain("Type Ia: opacity 0.19", WithPreset("sn1998bw").Outcome.CurveSourceText);
        Assert.DoesNotContain("Lira relation", WithPreset("sn1998bw").Outcome.CurveSourceText);
        string ia = WithPreset("sn2011fe").Outcome.CurveSourceText;
        Assert.Contains("Type Ia: opacity 0.19", ia);
        Assert.Contains("Lira relation", ia);
    }

    [Fact]
    public void InteractionCaveat_GivesTheHydrogenFreeColourToo()
    {
        // N4: Ibn/Icn are drawn at 10,000 K, so the caveat must not say only "6,000 K under a hydrogen envelope".
        var source = new LightCurveModel().Sources.Single(s => s.Citation.StartsWith("Circumstellar interaction", StringComparison.Ordinal));
        Assert.Contains("10,000 K without", source.Citation);
    }
}
