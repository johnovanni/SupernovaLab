using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>The close-up's neutron-star state and early cooling (spec §4; compact §C3, §C4, §C9; ruling R-P3-1).</summary>
public class NeutronStarModelTests
{
    private const double Year = PhysicalConstants.Year;

    private static Outcome Preset(string id)
    {
        var p = PresetCatalog.Get(id);
        return TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
    }

    private static readonly Outcome Crab = Preset("sn1054"), CasA = Preset("casa");
    private static readonly ObserverSetup CrabObs = PresetCatalog.Get("sn1054").Observer;

    /// <summary>A 1.4 M☉, 12 km neutron star (the research's reference star, §C3 table).</summary>
    private static readonly Outcome Default = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.03, 3.5e13);

    private static Outcome WithRemnant(Remnant r) => TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.03, 3.5e13, r);

    [Fact]
    public void Crab_PeriodAtAge_And_LightCylinder()   // criterion 3
    {
        // The preset's Crab fact is P = 33.3924 ms in 1991 (937 yr after 1054); the pinned birth period and dipole
        // spin-down give it back within the 5% Project 1 allows.
        var ns = NeutronStarModel.At(Crab, CrabObs, 1, 2.9568e10)!;
        Assert.InRange(ns.PeriodS, 0.0333924 * 0.95, 0.0333924 * 1.05);
        Assert.InRange(Magnetosphere.LightCylinderKm(0.0334), 1590, 1600);
        Assert.Equal(CompactResearch.C4.CrabLightCylinderKm, Magnetosphere.LightCylinderKm(0.0334), 0.5);
        Assert.False(ns.IsMagnetar);
        Assert.True(ns.HasCrust);
        Assert.Equal(2.9568e10, ns.AgeS);
    }

    [Fact]
    public void CasA_TemperatureComparisonDisclosed()   // #54, ruling R-P3-1
    {
        var t = NeutronStarModel.SurfaceTemperature(CasA, 340 * Year);
        Assert.InRange(t.Value, 1.75e6, 1.9e6);
        string text = NeutronStarModel.CasAComparison(CasA, 340 * Year)!;
        // The observed far-away value, the lab's value, "warm", and the inconsistency's number.
        Assert.Contains("1.4–1.5", text);
        Assert.Contains("1.8", text);
        Assert.Contains("warm", text);
        Assert.Contains("#54", text);
        Assert.DoesNotContain("cool", text);
        // The Heinke & Ho local value is quoted as the local surface value.
        Assert.Contains("2.04–2.12", text);
        Assert.Contains("surface", text);
        Assert.Contains("local, not redshifted", text);
    }

    [Fact]
    public void CasAComparison_OnlyAroundTheObservationsAge()
    {
        // Heinke & Ho 2010 and Shternin et al. 2023 observe a star about 330-350 yr old (§C9.3): elsewhere the comparison is meaningless.
        Assert.Null(NeutronStarModel.CasAComparison(CasA, 10 * Year));
        Assert.Null(NeutronStarModel.CasAComparison(CasA, 1000 * Year));
        Assert.NotNull(NeutronStarModel.CasAComparison(CasA, 300 * Year));
        Assert.NotNull(NeutronStarModel.CasAComparison(CasA, 340 * Year));
        Assert.NotNull(NeutronStarModel.CasAComparison(CasA, 380 * Year));
        Assert.Null(NeutronStarModel.CasAComparison(CasA, 299 * Year));
    }

    [Fact]
    public void CasAComparison_OnlyForTheCasAPreset()
    {
        Assert.Null(NeutronStarModel.CasAComparison(Default, 340 * Year));
        Assert.Null(NeutronStarModel.CasAComparison(Crab, 340 * Year));
    }

    [Fact]
    public void Temperature_BandBefore100yr_JoinsAt30yr()
    {
        var early = NeutronStarModel.SurfaceTemperature(Default, 29.9 * Year);
        var model = NeutronStarModel.SurfaceTemperature(Default, 30.1 * Year);
        Assert.NotNull(early.Low);
        Assert.NotNull(early.High);
        Assert.NotNull(model.Low);
        Assert.NotNull(model.High);
        Assert.InRange(early.Value / model.Value, 0.97, 1.03);
        // At 30 yr the early band is exactly the model's band (§C3).
        Assert.InRange(early.High!.Value / model.High!.Value, 0.97, 1.03);
        Assert.InRange(early.Low!.Value / model.Low!.Value, 0.999, 1.001);
        Assert.Null(NeutronStarModel.SurfaceTemperature(Default, 200 * Year).Low);
        Assert.Null(NeutronStarModel.SurfaceTemperature(Default, 200 * Year).High);
    }

    [Theory]
    [InlineData(60, CompactResearch.C3.At60sMK, CompactResearch.C3.At60sMK)]
    [InlineData(1 * 3.156e7, CompactResearch.C3.At1yrMK, CompactResearch.C3.At1yrMK)]
    [InlineData(3 * 3.156e7, CompactResearch.C3.At3yrMK, CompactResearch.C3.PlateauMK)]
    [InlineData(10 * 3.156e7, CompactResearch.C3.At10yrMK, CompactResearch.C3.PlateauMK)]
    public void EarlyBand_MatchesTheResearchCheckValues(double t, double valueMK, double highMK)
    {
        // §C3 App check values for 1.4 M☉, 12 km: the band runs from the model's 100-yr value to max(value, plateau).
        var q = NeutronStarModel.SurfaceTemperature(Default, t);
        Assert.Equal(valueMK, q.Value / 1e6, 0.01);
        Assert.Equal(CompactResearch.C3.At100yrMK, q.Low!.Value / 1e6, 0.01);
        Assert.Equal(highMK, q.High!.Value / 1e6, 0.01);
        Assert.Equal(Confidence.ModelDependent, q.Confidence);
        Assert.Contains("Gnedin", q.Source.Citation);
    }

    [Fact]
    public void EarlyBand_At30yr_IsTheModelsOwnValue()
    {
        var q = NeutronStarModel.SurfaceTemperature(Default, 30 * Year);
        Assert.Equal(CompactResearch.C3.At30yrMK, q.Value / 1e6, 0.005);
        Assert.Equal(CompactResearch.C3.At30yrMK, q.High!.Value / 1e6, 0.005);
    }

    [Fact]
    public void Temperature_FiniteFromOneMinute()
        => Assert.True(double.IsFinite(NeutronStarModel.SurfaceTemperature(Default, NeutronStarModel.EarlyStartS).Value));

    [Fact]
    public void Temperature_BeforeOneMinute_IsHeldAtTheOneMinuteValue()
        => Assert.Equal(NeutronStarModel.SurfaceTemperature(Default, 60).Value, NeutronStarModel.SurfaceTemperature(Default, 1).Value);

    [Fact]
    public void Temperature_NaNQuantityWhenThereIsNoNeutronStar()
    {
        var bh = TestOutcomes.Custom(SupernovaType.IIP, null, null, null, 3.5e13, TestOutcomes.BlackHole(10, 1), Fate.FailedSupernova);
        Assert.True(double.IsNaN(NeutronStarModel.SurfaceTemperature(bh, 100).Value));
        Assert.Null(NeutronStarModel.At(bh, ObserverSetup.Default, 1, 100));
    }

    [Fact]
    public void HasCrust_FromTheCrustFormationTime()
    {
        Assert.False(NeutronStarModel.At(Default, ObserverSetup.Default, 1, 3600)!.HasCrust);
        Assert.True(NeutronStarModel.At(Default, ObserverSetup.Default, 1, 86400)!.HasCrust);   // §C5: 1 day
    }

    [Fact]
    public void Magnetar_UsesPulsarModelsSpin()
    {
        var m = WithRemnant(TestOutcomes.Pulsar(0.01, 1e15, RemnantKind.Magnetar));
        var ns = NeutronStarModel.At(m, ObserverSetup.Default, 1, 100 * Year)!;
        var spin = PulsarModel.Spin.Of(m);
        Assert.True(ns.IsMagnetar);
        Assert.Equal(spin.PeriodS(100 * Year), ns.PeriodS);
        Assert.Equal(spin.PeriodDerivative(100 * Year), ns.PeriodDerivative);
        Assert.Equal(1e15, ns.FieldG);
    }

    [Theory]
    [InlineData(2.2, 1e-3, 1e16, RemnantKind.NeutronStar)]   // Review Focus 4
    [InlineData(2.2, 1e-3, 1e16, RemnantKind.Magnetar)]
    [InlineData(1.2, 1, 1e8, RemnantKind.NeutronStar)]
    public void Extremes_Finite(double m, double p0, double b, RemnantKind kind)
    {
        var r = new Remnant(kind, TestOutcomes.Q(m, "M☉"), TestOutcomes.Q(12, "km"), TestOutcomes.Q(p0, "s"),
            TestOutcomes.Q(b, "G"), TestOutcomes.Q(400, "km/s"), null);
        foreach (double t in new[] { 0.0, 1, 60, 86400, 30 * Year, 1000 * Year })
        {
            var ns = NeutronStarModel.At(WithRemnant(r), ObserverSetup.Default, 1, t)!;
            Assert.True(double.IsFinite(ns.PeriodS) && ns.PeriodS > 0);
            Assert.True(double.IsFinite(ns.PeriodDerivative));
            Assert.True(double.IsFinite(ns.SurfaceTemperatureK.Value));
            Assert.True(double.IsFinite(ns.OffsetCm.X) && double.IsFinite(ns.OffsetCm.Y) && double.IsFinite(ns.OffsetCm.Z));
            Assert.Equal(m, ns.MassMsun);
            Assert.Equal(12, ns.RadiusKm);
        }
    }

    [Fact]
    public void Offset_IsTheKickDriftsOffset()
    {
        var ns = NeutronStarModel.At(Default, ObserverSetup.Default, 7, 500 * Year)!;
        Assert.Equal(KickDrift.OffsetCm(Default, ObserverSetup.Default, 7, 500 * Year), ns.OffsetCm);
    }
}
