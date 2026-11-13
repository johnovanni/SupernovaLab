using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;

namespace SupernovaLab.Tests;

/// <summary>Supernova remnant growth, size on the sky and radio brightness (signals §7, §11).</summary>
public class RemnantEvolutionModelTests
{
    private static readonly RemnantEvolutionModel Model = new();
    private const double Year = 3.156e7;

    private static Outcome Explosion(double energyErg, double ejectaMsun)
        => TestOutcomes.Custom(SupernovaType.IIb, energyErg, ejectaMsun, 0.06, 1e12);

    [Fact]
    public void SedovRadius_MatchesTheTrueloveMcKeeValue()
    {
        // §7.2: R = 1.152 (E t² / ρ0)^(1/5), "about 0.336 pc E51^0.2 n0^-0.2 (t/yr)^0.4" for ρ0 = n0 m_p. E = 1e51 erg,
        // n0 = 1 cm⁻³, t = 1,000 yr: 0.336 × 1000^0.4 = 0.336 × 15.8489 = 5.32524 pc (the research's rounded coefficient).
        // Exactly: E t²/ρ0 = 1e51 × (3.156e10)² / 1.6726e-24 = 5.95500e95, ^0.2 = 1.42890e19, × 1.152 = 1.64609e19 cm
        // = 5.33427 pc.
        Assert.Equal(5.3343, RemnantEvolutionModel.SedovRadiusPc(1e51, 1, 1000 * Year), 0.001);
    }

    [Fact]
    public void ModelRadius_At1000Years_IsNearTheSedovValue()
    {
        // §7.1–7.2, M_ej = 1 M☉, E = 1e51, n0 = 1: R_ch = 3.4 pc, t_ch = 473 yr, t* = 2.11416. Tang & Chevalier's n = 0 form
        // R* = [(2.01 t*)^-2.5 + (2.026 t*²)^-0.5]^-0.4 = [0.0268635 + 0.332308]^-0.4 = 1.50619 → R = 5.12104 pc, within 10%
        // of the Sedov 5.32524 pc (it is still approaching Sedov–Taylor).
        double r = Model.Sample(Explosion(1e51, 1), ObserverSetup.Default, 1000 * Year, "radiusPc");
        Assert.Equal(5.12104, r, 0.01);
        Assert.InRange(r, 0.9 * 5.32524, 1.1 * 5.32524);
    }

    [Fact]
    public void CharacteristicScales_FollowTrueloveMcKee()
    {
        // §7.1: R_ch = 3.4 pc M^(1/3) n0^(-1/3), t_ch = 473 yr E51^(-1/2) M^(5/6) n0^(-1/3). M = 1.4: R_ch = 3.4 × 1.11869
        // = 3.80354 pc, t_ch = 473 × 1.32367 = 626.087 yr.
        Assert.Equal(3.80354, RemnantEvolutionModel.CharacteristicRadiusPc(1.4, 1), 4);
        Assert.Equal(626.087 * Year, RemnantEvolutionModel.CharacteristicTimeS(1e51, 1.4, 1), 0.01 * Year);
    }

    [Fact]
    public void CasALike_HasAboutCasASizeOnTheSky()
    {
        // Cas A (presets §3): E ≈ 2e51 erg, M_ej ≈ 3 M☉, about 340 yr, 3.4 kpc, 5′ across (§7.3). By hand: t_ch = 473 ×
        // 2^-0.5 × 3^(5/6) = 835.501 yr, t* = 0.406941, R* = 0.614442, R_ch = 4.90365 pc → R = 3.01301 pc; θ = 2R/D =
        // 2 × 3.01301 / 3400 rad = 365.575″ (6.1′), 22% above the catalogue 5′ (n0 = 1 is assumed; Cas A sits in a wind).
        var o = Explosion(2e51, 3);
        var obs = new ObserverSetup(3400, 5, true);
        Assert.Equal(3.01301, Model.Sample(o, obs, 340 * Year, "radiusPc"), 0.005);
        double theta = Model.Sample(o, obs, 340 * Year, "angularSizeArcsec");
        Assert.Equal(365.575, theta, 0.5);
        Assert.InRange(theta, 0.7 * 300, 1.3 * 300);
    }

    [Fact]
    public void Radio_IsCalibratedToCasA()
    {
        // §11: radio calibrated to Cas A, 2,300 Jy at 1 GHz at 3.4 kpc (§7.3); S ∝ D⁻², so 575 Jy at 6.8 kpc.
        var o = Explosion(2e51, 3);
        Assert.Equal(2300, Model.Sample(o, new ObserverSetup(3400, 0, true), 340 * Year, "radio1GHzJy"), 1e-6);
        Assert.Equal(575, Model.Sample(o, new ObserverSetup(6800, 0, true), 100 * Year, "radio1GHzJy"), 1e-6);
        // 2,300 Jy × (3.4 kpc / 10 Mpc)² = 2.6588e-4 Jy = 0.266 mJy: the VLA (0.03 mJy) sees it; at 100 Mpc, 2.66 µJy, not.
        Assert.Equal(DetectionStatus.Detected,
            Model.Detect(o, new ObserverSetup(10e6, 0, true), 340 * Year, Detectors.Get("vla")).Status);
        Assert.Equal(DetectionStatus.TooFaint,
            Model.Detect(o, new ObserverSetup(100e6, 0, true), 340 * Year, Detectors.Get("vla")).Status);
    }

    [Fact]
    public void ResolvableTime_IsWhenTheRemnantReachesHalfAnArcsecond()
    {
        // θ = 0.5″ at 1 Mpc needs R = 0.25″ × 1 Mpc = 1.21203 pc. E = 1e51, M = 1.4: R* = 1.21203 / 3.80354 = 0.31866,
        // reached at t* = 0.175910 (bisection on the §7.2 form by hand), t = 0.175910 × 626.087 yr = 110.136 yr.
        var o = Explosion(1e51, 1.4);
        var obs = new ObserverSetup(1e6, 0, true);
        double t = Model.ResolvableTimeS(o, obs);
        Assert.Equal(110.136 * Year, t, 0.2 * Year);
        Assert.Equal(0.5, Model.Sample(o, obs, t, "angularSizeArcsec"), 1e-4);
        // Never within 1,000 yr at 1 Gpc.
        Assert.Equal(double.PositiveInfinity, Model.ResolvableTimeS(o, new ObserverSetup(1e9, 0, true)));
        Assert.True(double.IsNaN(Model.ResolvableTimeS(TestOutcomes.Star(23), obs)));
    }

    [Fact]
    public void AppliesToEveryExplosion_IncludingTypeIa_ButNotFailedSupernovae()
    {
        Assert.True(Model.Applies(TestOutcomes.TypeIa())); // Tycho, Kepler and SN 1006 are Type Ia remnants (§7.3)
        Assert.True(Model.Applies(TestOutcomes.Star(16)));
        Assert.False(Model.Applies(TestOutcomes.Star(23))); // failed: no ejecta
        Assert.Equal((10 * Year, 1000 * Year), Model.ValidRange(TestOutcomes.Star(16)));
        Assert.True(double.IsNaN(Model.Sample(TestOutcomes.Star(16), ObserverSetup.Default, 5 * Year, "radiusPc")));
        Assert.Equal(DetectionStatus.NotProduced,
            Model.Detect(TestOutcomes.Star(23), ObserverSetup.Default, 100 * Year, Detectors.Get("vla")).Status);
    }
}
