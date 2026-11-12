using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;

namespace SupernovaLab.Tests;

/// <summary>Thermal X-rays from the cooling neutron star (signals §8.4, §11).</summary>
public class NeutronStarCoolingModelTests
{
    private static readonly NeutronStarCoolingModel Model = new();
    private const double Year = 3.156e7;

    /// <summary>A 1.4 M☉, 12 km neutron star (the engine's radius; outcomes §6).</summary>
    private static Outcome WithNeutronStar() => TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.03, 3.5e13);

    [Fact]
    public void Envelope_ReproducesTheResearchWorkedExample()
    {
        // §8.4 at 300 yr: T_i = 1e9 K × 300^(−1/6) = 3.86497e8 K (research "T_b about 3.9e8"); with g14 = 2.4,
        // T_s6 = [2.4 (3.86497e8 / 1.288e8)^(1/0.455)]^(1/4) = [2.4 × 11.1907]^(1/4) = 2.27650 (research "about 2.3e6 K").
        Assert.Equal(3.86497e8, NeutronStarCoolingModel.InteriorTemperatureK(300 * Year), 0.00001e8);
        Assert.Equal(2.27650e6, NeutronStarCoolingModel.SurfaceTemperatureK(3.86497e8, 2.4), 0.0001e6);
    }

    [Fact]
    public void SurfaceTemperatureAt300Years_IsTwoMillionKelvinOrSo()
    {
        // §8.4: r_g = 2GM/c² = 4.13559e5 cm for 1.4 M☉, so 1 − r_g/R = 0.655367 at 12 km and T_s^∞ = 2.27650e6 × 0.809548
        // = 1.84293e6 K (research "about 1.8e6 K"); inside 1.5–3 MK (Cas A's neutron star, ~340 yr: 2.0–2.1e6 K).
        // L^∞ = 4πR²σT_s⁴ (1 − r_g/R) = 2.75584e34 × 0.655367 = 1.80609e34 erg/s (the research's "about 3e34" is the
        // surface value, 2.75584e34).
        var o = WithNeutronStar();
        double t = Model.Sample(o, ObserverSetup.Default, 300 * Year, "tempK");
        Assert.Equal(1.84293e6, t, 0.0005e6);
        Assert.InRange(t, 1.5e6, 3e6);
        Assert.Equal(1.80609e34, Model.Sample(o, ObserverSetup.Default, 300 * Year, "lum"), 0.0005e34);
    }

    [Fact]
    public void BeforeAHundredYears_TheTemperatureIsABand()
    {
        // §8.4: "before about 100 yr ... show a band, not a line". [APPROX] The band runs from the 100-yr value (a crust
        // holding its temperature until it relaxes) to the relaxed-interior value at this age. At 50 yr, T_i = 5.21001e8 K and
        // g14 = 2.4: T_s6 = [2.4 × 4.04504^(1/0.455)]^(1/4) = [2.4 × 21.5723]^(1/4) = 2.68242, × 0.809548 = 2.17155e6 K;
        // at 100 yr, T_i = 4.64159e8 K: T_s^∞ = 2.51744e6 × 0.809548 = 2.03799e6 K.
        var o = WithNeutronStar();
        var early = Model.SurfaceTemperatureAt(o, 50 * Year);
        Assert.Equal(2.17155e6, early.Value, 0.001e6);
        Assert.Equal(2.03799e6, early.Low!.Value, 0.001e6);
        Assert.Equal(2.17155e6, early.High!.Value, 0.001e6);
        var late = Model.SurfaceTemperatureAt(o, 300 * Year);
        Assert.Null(late.Low);
        Assert.Null(late.High);
        Assert.Equal((30 * Year, 1000 * Year), Model.ValidRange(o));
        Assert.True(double.IsNaN(Model.Sample(o, ObserverSetup.Default, 20 * Year, "tempK")));
    }

    [Fact]
    public void Chandra_SeesItAcrossTheGalaxy_IfTheGasAllows_ButNotBeyondTheLocalGroup()
    {
        // §11: "L about 3e34 erg/s at 300 yr: Chandra-detectable through the Galaxy if N_H allows; not beyond the Local
        // Group". By hand (blackbody at T_s^∞ = 1.84293e6 K, 0.3–10 keV, e^(−N_H σ(E)), Simpson): 10 kpc, A_V = 0:
        // 1.27e-12 erg cm⁻² s⁻¹; Cas A's 3.4 kpc, A_V = 5: 4.0e-13; 8 kpc behind A_V = 40: 4.6e-16 of an unabsorbed
        // 2.0e-12 (blocked); 3 Mpc: 1.4e-17. Chandra's limit is 1e-15.
        var o = WithNeutronStar();
        var chandra = Detectors.Get("chandra");
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, new ObserverSetup(10e3, 0, true), 300 * Year, chandra).Status);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, new ObserverSetup(3.4e3, 5, true), 340 * Year, chandra).Status);
        Assert.Equal(DetectionStatus.BlockedByDust,
            Model.Detect(o, new ObserverSetup(8e3, 40, true), 300 * Year, chandra).Status);
        Assert.Equal(DetectionStatus.TooFaint, Model.Detect(o, new ObserverSetup(3e6, 0, true), 300 * Year, chandra).Status);
        Assert.Equal(DetectionStatus.NotModeled,
            Model.Detect(o, new ObserverSetup(10e3, 0, true), 300 * Year, Detectors.Get("vla")).Status);
    }

    [Fact]
    public void BlackHolesAndTypeIa_HaveNoCoolingNeutronStar()
    {
        var bh = TestOutcomes.Custom(SupernovaType.Ic, 1e51, 3, 0.1, 1e11, TestOutcomes.BlackHole(5, 3));
        Assert.False(Model.Applies(bh));
        Assert.Equal(DetectionStatus.NotProduced,
            Model.Detect(bh, ObserverSetup.Default, 300 * Year, Detectors.Get("chandra")).Status);
        Assert.False(Model.Applies(TestOutcomes.TypeIa()));
        Assert.False(Model.Applies(TestOutcomes.Star(23)));
    }
}
