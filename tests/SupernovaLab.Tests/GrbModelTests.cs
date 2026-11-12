using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;

namespace SupernovaLab.Tests;

/// <summary>Gamma-ray bursts from collapsars: prompt emission, beaming and afterglow (signals §9, §11).</summary>
public class GrbModelTests
{
    private static readonly GrbModel Model = new();
    private const double Day = 86400;
    private static readonly Outcome Collapsar = TestOutcomes.Collapsar();

    /// <summary>38 Mpc, SN 1998bw / GRB 980425's distance (presets §7); below 50 Mpc, so z = 0.</summary>
    private static ObserverSetup At38Mpc(bool beamed = true, double av = 0) => new(38e6, av, beamed);

    // ---------------------------------------------------------------- energetics (formula tests)

    [Fact]
    public void Energetics_FollowFrailBeamingAndTheAmatiRelation()
    {
        // §9.2: E_γ ≈ 5e50 erg (Frail et al. 2001); θ_j = 5° [APPROX, the research's example] gives f_b = 1 − cos 5°
        // = 0.00380530, so E_iso = 5e50 / 0.00380530 = 1.31396e53 erg (inside §9.1's 1e48–1e54).
        Assert.Equal(0.00380530, GrbModel.BeamingFraction(GrbModel.JetHalfAngleRad), 8);
        Assert.Equal(1.31396e53, GrbModel.IsotropicEnergyErg, 0.0001e53);
        // §9.1 Amati: E_p,i = 95 keV (E_iso / 1e52)^0.49 = 95 × 13.1396^0.49 = 335.605 keV.
        Assert.Equal(335.605, GrbModel.AmatiPeakEnergyKeV(1.31396e53), 0.01);
    }

    [Fact]
    public void T90_IsALongBurst()
    {
        // §9.1: long GRBs have T90 > 2 s. [APPROX] 23.3 s, GRB 980425's measured T90 (§9.4).
        double t90 = Model.T90S(Collapsar);
        Assert.True(t90 > 2);
        Assert.Equal(23.3, t90, 9);
        Assert.True(double.IsNaN(Model.T90S(TestOutcomes.Star(16))));
    }

    [Fact]
    public void Onset_IsBlackHoleFormationPlusTheLightCrossingTimeOfTheStar()
    {
        // t_BH = 1 s (the 40 M☉ collapsar), R = 3.16228e11 cm: 1 + 3.16228e11 / 2.99792458e10 = 11.5482 s.
        Assert.Equal(11.5482, Model.OnsetS(Collapsar), 3);
    }

    [Fact]
    public void Prompt_CarriesTheIsotropicFluence_AndItsT90MatchesT90S()
    {
        // §9.1: total fluence S = E_iso / (4π D²) = 1.31396e53 / (4π (38 Mpc)²) = 1.31396e53 / 1.72777e53 = 0.760494.
        // Integrated here from the channel (trapezoid on a fine grid), and the 5%–95% fluence interval gives T90.
        var obs = At38Mpc();
        double t0 = Model.OnsetS(Collapsar), dt = 0.001, total = 0;
        var cumulative = new List<(double T, double S)>();
        for (double t = t0; t <= t0 + 3 * 23.3; t += dt)
        {
            double a = Model.Sample(Collapsar, obs, t, "prompt"), b = Model.Sample(Collapsar, obs, t + dt, "prompt");
            if (double.IsNaN(b)) break;
            total += 0.5 * (a + b) * dt;
            cumulative.Add((t + dt, total));
        }
        Assert.Equal(0.760494, total, 0.005 * 0.760494);
        double t5 = cumulative.First(c => c.S >= 0.05 * total).T, t95 = cumulative.First(c => c.S >= 0.95 * total).T;
        Assert.Equal(23.3, t95 - t5, 0.05);
    }

    [Fact]
    public void PeakFlux_InTheGbmAndBatBands_MatchesAnIndependentBandIntegration()
    {
        // FRED pulse: linear rise over τ_d / 3, exponential decay τ_d [APPROX]; T90 = 2.97771 τ_d, so τ_d = 7.82480 s and
        // L_peak = E_iso / (7/6 τ_d) = 1.43933e52 erg/s; at 38 Mpc the peak energy flux is 0.0833060 erg cm⁻² s⁻¹.
        // Band function α = −1, β = −2.3, E_p = 335.605 keV, normalized over 1–10⁴ keV. By hand (independent Python,
        // 200,000-interval log Simpson): GBM 50–300 keV peak photon flux 1.29544e5 photons cm⁻² s⁻¹; BAT 15–150 keV
        // peak energy flux 1.82721e-2 erg cm⁻² s⁻¹.
        var obs = At38Mpc();
        double peak = Model.PeakTimeS(Collapsar);
        Assert.Equal(11.5482 + 2.60827, peak, 3);
        Assert.Equal(0.0833060, Model.Sample(Collapsar, obs, peak, "prompt"), 0.001 * 0.0833060);
        Assert.Equal(1.29544e5 / 0.7, Model.Sample(Collapsar, obs, peak, "detect@gbm"), 0.005 * 1.29544e5 / 0.7);
        Assert.Equal(1.82721e-2 / 1e-8, Model.Sample(Collapsar, obs, peak, "detect@bat"), 0.005 * 1.82721e-2 / 1e-8);
    }

    // ---------------------------------------------------------------- pinned energetics (ruling R36)

    private static Outcome Grb980425()
        => TestOutcomes.Engine.Compute(Collapsar.Inputs, new PresetPins(null, null, null, null,
            new SupernovaLab.Core.Units.Source("GRB 980425 (test)", "signals §9.4"), GrbEisoErg: 8.5e47, GrbEpKeV: 55,
            GrbT90S: 23.3));

    [Fact]
    public void Grb980425Pinned_At38Mpc_IsDetectedByGbm_WithAboutTheObservedPeakFlux()
    {
        // Ruling R36 pins (signals §9.4; E_p,i ≈ 55 keV from Amati 2006's table [APPROX]). By hand: the FRED pulse with
        // T90 = 23.3 s has τ_d = 23.3 / 2.97771 = 7.82480 s and holds (1 + 1/6) τ_d = 9.12893 s × L_peak, so
        // L_peak = 8.5e47 / 9.12893 = 9.31106e46 erg/s (the observed peak L is 5.5e46; one pulse is peakier than the real
        // light curve). At 38 Mpc: 9.31106e46 / 1.72777e53 = 5.38908e-7 erg cm⁻² s⁻¹ (1–10⁴ keV), 1.80 × the observed
        // 3.0e-7 (24–1,820 keV); in that band the Band spectrum (E_p = 55 keV) holds 0.682795 of it: 3.680e-7, 1.23 ×.
        // GBM, 50–300 keV (independent Python, 200,000-interval Band integral): 1.12275 photons cm⁻² s⁻¹, 1.60393 × 0.7.
        var o = Grb980425();
        Assert.True(o.HasGrb);
        Assert.Equal(8.5e47, o.GrbIsotropicEnergyErg!.Value);
        Assert.True(o.GrbIsotropicEnergyErg.Published);
        Assert.Equal(55, o.GrbPeakEnergyKeV!.Value);
        Assert.Equal(23.3, Model.T90S(o));

        var obs = At38Mpc();
        double peak = Model.PeakTimeS(o);
        double flux = Model.Sample(o, obs, peak, "prompt");
        Assert.InRange(flux, 3.0e-7 / 2, 3.0e-7 * 2);
        Assert.Equal(5.38908e-7, flux, 0.001 * 5.38908e-7);
        Assert.Equal(1.60393, Model.Sample(o, obs, peak, "detect@gbm"), 0.005 * 1.60393);
        var d = Model.Detect(o, obs, peak + 5, Detectors.Get("gbm"));
        Assert.Equal(DetectionStatus.Detected, d.Status);
        Assert.Contains("1.12 photons", d.Reason);
        Assert.Contains("published", d.Reason);
    }

    [Fact]
    public void GrbPins_AreIgnoredWithoutAGrb()
    {
        var o = TestOutcomes.Engine.Compute(TestOutcomes.Star(16).Inputs, new PresetPins(null, null, null, null,
            new SupernovaLab.Core.Units.Source("test", "tests"), GrbEisoErg: 8.5e47));
        Assert.Null(o.GrbIsotropicEnergyErg);
        Assert.False(o.UsesPublishedValues);
    }

    // ---------------------------------------------------------------- detection

    [Fact]
    public void Prompt_IsDetectedByGbmAt38Mpc_WhenBeamed_AndNotPointedAtUsWhenNot()
    {
        double t = Model.OnsetS(Collapsar) + 10;
        var gbm = Detectors.Get("gbm");
        var seen = Model.Detect(Collapsar, At38Mpc(), t, gbm);
        Assert.Equal(DetectionStatus.Detected, seen.Status);
        Assert.Contains("photons", seen.Reason);
        var away = Model.Detect(Collapsar, At38Mpc(beamed: false), t, gbm);
        Assert.Equal(DetectionStatus.NotPointedAtUs, away.Status);
        Assert.Contains("0.4%", away.Reason);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(Collapsar, At38Mpc(), t, Detectors.Get("bat")).Status);
        Assert.Equal(DetectionStatus.NotPointedAtUs,
            Model.Detect(Collapsar, At38Mpc(beamed: false), t, Detectors.Get("bat")).Status);
    }

    [Fact]
    public void BeforeTheBurst_IsNotYet_AndAfterwardsTheTriggerStands()
    {
        var gbm = Detectors.Get("gbm");
        Assert.Equal(DetectionStatus.NotYet, Model.Detect(Collapsar, At38Mpc(), 1, gbm).Status);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(Collapsar, At38Mpc(), 30 * Day, gbm).Status);
    }

    [Fact]
    public void NonGrbOutcomes_GiveNotProduced()
    {
        foreach (var o in new[] { TestOutcomes.Star(16), TestOutcomes.TypeIa(), TestOutcomes.Star(23) })
        {
            Assert.False(Model.Applies(o));
            Assert.Equal(DetectionStatus.NotProduced, Model.Detect(o, At38Mpc(), 20, Detectors.Get("gbm")).Status);
            Assert.True(double.IsNaN(Model.Sample(o, At38Mpc(), 20, "prompt")));
        }
    }

    [Fact]
    public void OtherDetectors_AreNotModeled()
    {
        var d = Model.Detect(Collapsar, At38Mpc(), Model.OnsetS(Collapsar) + 10, Detectors.Get("spi"));
        Assert.Equal(DetectionStatus.NotModeled, d.Status);
    }

    // ---------------------------------------------------------------- afterglow

    [Fact]
    public void WindAfterglow_BreakFrequencies_AtOneDay()
    {
        // §9.3 wind medium (Chevalier & Li 2000) at z = 0 (x = 0.5), t = 1 d, E52 = 13.1396, ε_e = 0.1, ε_B = 0.01,
        // A_* = (2.51189e-6 / 1e-5) / (1000 / 1000) = 0.251189 (the collapsar's Wolf-Rayet wind). By hand:
        // ν_m = 5e12 × 0.5^½ × 0.1^½ × 13.1396^½ = 4.05271e12 Hz; ν_c = 2e12 × 0.5^-1.5 × 0.1^-1.5 × 13.1396^½ ×
        // 0.251189^-2 = 1.02770e16 Hz; ν_A = 1e11 × 0.5^-0.4 × 0.1^0.2 × 13.1396^-0.4 × 0.251189^1.2 = 5.66213e9 Hz;
        // F_ν,max = 7.0e32 x^1.5 (ε_B/0.1)^½ E52^½ A_* t_d^-½ / (4π D²) [DERIVED: the research's 3.5e32 local luminosity
        // is per z = 1 observer day, i.e. per 2 rest-frame days] = 4.12438e-22 erg cm⁻² s⁻¹ Hz⁻¹ at 38 Mpc.
        var b = GrbModel.WindAfterglow(1.31396e53, 0.251189, 1.0, 0, 38e6 * 3.0857e18, double.PositiveInfinity);
        Assert.Equal(4.05271e12, b.NuMHz, 0.001 * 4.05271e12);
        Assert.Equal(1.02770e16, b.NuCHz, 0.001 * 1.02770e16);
        Assert.Equal(5.66213e9, b.NuAHz, 0.001 * 5.66213e9);
        Assert.Equal(4.12438e-22, b.FMaxCgs, 0.001 * 4.12438e-22);
    }

    [Fact]
    public void JetBreak_FromTheFrailFormula()
    {
        // §9.2 inverted for t_j with η_γ = 0.2 and n = 0.1 cm⁻³: t_j = (θ_j / 0.057)^(8/3) ((1+z)/2) (E_iso / 1e53)^(1/3)
        // days = (0.0872665 / 0.057)^(8/3) × 0.5 × 1.31396^(1/3) = 1.70513 d.
        Assert.Equal(1.70513, GrbModel.JetBreakDays(1.31396e53, 0), 0.0001);
    }

    [Fact]
    public void Afterglow_OneDayAfterTheBurst_At38Mpc()
    {
        // By hand from the breaks above (slow cooling, p = 2.5): radio 5 GHz is below ν_A, so F = F_max (ν_A/ν_m)^(1/3)
        // (ν/ν_A)² = 3595.43 mJy; V (5.45077e14 Hz, between ν_m and ν_c): F_max (ν/ν_m)^-0.75 → AB 8.8529 mag;
        // X-rays 0.3–10 keV (above ν_c, unabsorbed with A_V = 0) integrate to 1.69940e-8 erg cm⁻² s⁻¹.
        var obs = At38Mpc();
        double t = Model.OnsetS(Collapsar) + Day;
        Assert.Equal(3595.43, Model.Sample(Collapsar, obs, t, "afterglowRadio"), 0.002 * 3595.43);
        Assert.Equal(8.8529, Model.Sample(Collapsar, obs, t, "afterglowOpt"), 0.002);
        Assert.Equal(1.69940e-8, Model.Sample(Collapsar, obs, t, "afterglowX"), 0.005 * 1.69940e-8);

        // Dust dims the optical by A_V and gas absorbs soft X-rays; radio passes.
        var dusty = At38Mpc(av: 2);
        Assert.Equal(8.8529 + 2, Model.Sample(Collapsar, dusty, t, "afterglowOpt"), 0.002);
        Assert.True(Model.Sample(Collapsar, dusty, t, "afterglowX") < 1.69940e-8);
        Assert.Equal(3595.43, Model.Sample(Collapsar, dusty, t, "afterglowRadio"), 0.002 * 3595.43);
    }

    [Fact]
    public void Afterglow_AfterTheJetBreak_FallsAsTMinusP()
    {
        // §9.3: after the jet break, F ∝ t^-p above ν_m (Sari, Piran & Halpern 1999); p = 2.5. X-rays between 5 and 10 d.
        var obs = At38Mpc();
        double t0 = Model.OnsetS(Collapsar);
        double f5 = Model.Sample(Collapsar, obs, t0 + 5 * Day, "afterglowX");
        double f10 = Model.Sample(Collapsar, obs, t0 + 10 * Day, "afterglowX");
        Assert.Equal(-2.5, Math.Log(f10 / f5) / Math.Log(2), 3);
    }

    [Fact]
    public void Afterglow_IsNotDrawnWhenTheJetPointsAway()
    {
        // §9.2: an off-axis "orphan" afterglow is qualitative only, not modeled.
        double t = Model.OnsetS(Collapsar) + Day;
        foreach (string c in new[] { "prompt", "afterglowX", "afterglowOpt", "afterglowRadio", "detect@gbm" })
            Assert.True(double.IsNaN(Model.Sample(Collapsar, At38Mpc(beamed: false), t, c)), c);
    }
}
