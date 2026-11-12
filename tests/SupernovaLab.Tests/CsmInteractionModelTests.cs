using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;

namespace SupernovaLab.Tests;

/// <summary>Radio and X-rays from the shock running into the progenitor's wind (signals §6, §11).</summary>
public class CsmInteractionModelTests
{
    private static readonly CsmInteractionModel Model = new();
    private const double Day = 86400, Year = 3.156e7, Mpc = 1e6;

    /// <summary>A II-P: 1e51 erg, 10 M☉ of ejecta (the research's canonical II), in a red-supergiant wind, A_* = 10.</summary>
    private static Outcome IIP() => TestOutcomes.WithWind(
        TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.03, 3.5e13), 1e-6, 10);

    /// <summary>A stripped Ib: 1e51 erg, 3 M☉ (the research's canonical Ib/c), in a Wolf-Rayet wind, A_* = 1.</summary>
    private static Outcome Ib() => TestOutcomes.WithWind(TestOutcomes.Custom(SupernovaType.Ib, 1e51, 3, 0.1, 1e11), 1e-5, 1000);

    // ---------------------------------------------------------------- formulas (signals §6.1, §6.2)

    [Fact]
    public void SynchrotronPeak_ReproducesTheSn1994ICheck()
    {
        // §6.1 check: R = 1.17e16 cm and B = 0.51 G give L_p = 1.42e27 erg/s/Hz at 4.88 GHz (observed 1.40e27). By hand:
        // L_p = 1.197e24 (1.17e16 × 0.51 / 4.4e14)^(19/7) = 1.197e24 × 13.5614^2.71429 = 1.41739e27;
        // ν_p = 5 GHz (0.51/1.1) (1184.1)^(2/19) = 4.88273 GHz.
        var p = CsmInteractionModel.SynchrotronPeak(1.17e16, 0.51);
        Assert.Equal(1.41739e27, p.LuminosityErgSHz, 0.001e27);
        Assert.Equal(4.88273e9, p.FrequencyHz, 0.001e9);
        // Ruling R42: the check holds on the drawn spectrum too, which peaks at L_p exactly at ν_p.
        Assert.Equal(1.41739e27, CsmInteractionModel.SpectralLuminosity(p, 4.88273e9), 0.001e27);
    }

    [Fact]
    public void Spectrum_PeaksAtLpAtNuP()
    {
        // R42: L_ν = N L_p / ((x0 x)^(−5/2) + x0 x), x = ν/ν_p, x0 = 2.5^(2/7) = 1.299263, N = x0^(−5/2) + x0 = 0.519706 +
        // 1.299263 = 1.818969 (R42 quotes 1.81907, from x0 rounded to 1.29935). 1/(y^(−5/2) + y) peaks at y = 2.5^(2/7),
        // i.e. at x = 1, where the curve equals L_p. Scan 0.1–10 ν_p in steps of 0.0001 dex.
        var p = new CsmInteractionModel.SsaPeak(1e27, 5e9);
        double bestL = 0, bestNu = 0;
        for (int k = -10_000; k <= 10_000; k++)
        {
            double nu = 5e9 * Math.Pow(10, k / 10_000.0), l = CsmInteractionModel.SpectralLuminosity(p, nu);
            if (l > bestL) (bestL, bestNu) = (l, nu);
        }
        Assert.Equal(1e27, bestL, 1e27 * 1e-9);
        Assert.Equal(5e9, bestNu, 5e9 * 2.4e-4);
        // Asymptotes: N x0^(5/2) x^(5/2) far below, N/(x0 x) far above. At x = 1e-3: 1.818969 × 1.924167 × 3.16228e-8
        // = 1.10679e-7 (the x0 x term is negligible); at x = 1e3: 1.818969 / 1299.263 = 1.39999e-3.
        Assert.Equal(1.10679e20, CsmInteractionModel.SpectralLuminosity(p, 5e6), 0.0001e20);
        Assert.Equal(1.39999e24, CsmInteractionModel.SpectralLuminosity(p, 5e12), 0.0001e24);
    }

    [Fact]
    public void MagneticField_FollowsTheEquipartitionEnergyDensity()
    {
        // §6.1: B = sqrt(8π × 0.052 ε_B,-1 A_* t_10⁻²) G with ε_B = 0.1: A_* = 10 at 30 d gives
        // sqrt(8π × 0.052 × 10) / 3 = sqrt(13.06903) / 3 = 3.61511 / 3 = 1.20504 G.
        Assert.Equal(1.20504, CsmInteractionModel.MagneticFieldG(10, 30 * Day), 4);
    }

    [Fact]
    public void PeakFrequency_RedSupergiantWindAt30Days()
    {
        // A II-P in an RSG wind (A_* = 10; §0.4: Ṁ 1e-6 M☉/yr, v_w 10 km/s) at 30 d. R/t = 1e4 km/s (§6.1, II-P scale, at
        // the canonical 1e51 erg / 10 M☉), so R = 1e9 × 2.592e6 = 2.592e15 cm, B = 1.20504 G (above):
        // L_p = 1.197e24 (2.592e15 × 1.20504 / 4.4e14)^(19/7) = 1.197e24 × 7.09893^2.71429 = 2.44595e26 erg/s/Hz;
        // ν_p = 5 GHz (1.20504/1.1) × 204.344^(2/19) = 5 × 1.09549 × 1.75062 = 9.58899 GHz.
        // That is the range §6.1 implies: a few to ~10 GHz at weeks (SN 1994I peaked at 4.86 GHz at 36 d).
        double nu = Model.Sample(IIP(), new ObserverSetup(10 * Mpc, 0, true), 30 * Day, "nuPeakGHz");
        Assert.Equal(9.58899, nu, 0.01);
        Assert.InRange(nu, 1, 30);
    }

    [Fact]
    public void ThermalXrays_MatchTheResearchExample()
    {
        // §6.2: L_ff = 3e35 [(n−3)(n−4)²/(4(n−2))] β^½ ζ₂⁻¹ A_*² t_10⁻¹; n = 10 gives 2.3625e36 A_*² t_10⁻¹ ("2.4e36").
        // RSG wind A_* = 10 at 10 d: 2.3625e38 erg/s ("about 2.4e38 erg/s at 10 d").
        Assert.Equal(2.3625e38, CsmInteractionModel.ThermalXrayLuminosity(10, 10 * Day), 0.0001e38);
        Assert.Equal(2.3625e37, CsmInteractionModel.ThermalXrayLuminosity(10, 100 * Day), 0.0001e37);
    }

    [Fact]
    public void InverseComptonXrays_FollowCf06Eq31_OverTheBand()
    {
        // §6.2: E dL_IC/dE = 8.8e36 γ_min ε_r,-1 A_* v_s4 (L_bol/1e42) t_10⁻¹ per unit ln E, γ_min = 1, ε_r = 0.1. Flat in
        // ln E (p = 3), so 0.3–10 keV holds ln(10/0.3) = 3.50656 of it: 8.8e36 × 3.50656 = 3.08577e37 erg/s for A_* = 1,
        // v = 1e4 km/s, L_bol = 1e42 erg/s, t = 10 d.
        Assert.Equal(3.08577e37, CsmInteractionModel.InverseComptonLuminosity(1, 1e9, 1e42, 10 * Day), 0.0001e37);
    }

    // ---------------------------------------------------------------- the model

    [Fact]
    public void StrippedSupernova_IsRadioBrighterThanAIIP_AtEqualDistanceAndTime()
    {
        // §6.1 forward model at 30 d, 5 GHz, 10 Mpc (z = 0, 4πD² = 1.196525e52 cm²), R42's normalized join
        // (N = 1.818969, x0 = 1.299263):
        //  II-P (above): ν_p = 9.58899 GHz, x0 x = 0.677477, L = 1.818969 × 2.44595e26 / (0.677477^-2.5 + 0.677477)
        //      = 4.44913e26 / 3.32454 = 1.33826e26 → 1.11847 mJy.
        //  Ib: R/t = 58,000 km/s [APPROX, geometric middle of CF06's 32,000–105,000] at 1e51 erg / 3 M☉, A_* = 1:
        //      B = 0.381066 G, R = 1.50336e16 cm, L_p = 1.26901e27, ν_p = 3.60610 GHz, L(5 GHz) = 1.13650e27 → 9.49844 mJy.
        var obs = new ObserverSetup(10 * Mpc, 0, true);
        double iip = Model.Sample(IIP(), obs, 30 * Day, "radio5"), ib = Model.Sample(Ib(), obs, 30 * Day, "radio5");
        Assert.Equal(1.11847, iip, 0.005);
        Assert.Equal(9.49844, ib, 0.04);
        Assert.True(ib > iip);
    }

    [Fact]
    public void Sn1998bwLikeEjecta_GiveABrightRadioSource()
    {
        // R39: SN 1998bw's radio comes from this model. Iwamoto et al. 1998's fit (presets §7): Ic-BL, 3e52 erg, 10 M☉
        // ejecta, 0.7 M☉ ⁵⁶Ni, in a Wolf-Rayet wind (A_* = 1, §0.4), at its 38 Mpc. R/t = 58,000 × sqrt((3e52/10) /
        // (1e51/3)) = 174,000 km/s = 0.580c (below the Γβ = 1 cap). At 30 d: L_p = 2.50328e28, ν_p = 4.93581 GHz, so with
        // R42's normalized join L(5 GHz) = 2.50276e28 erg/s/Hz (5 GHz is just past the peak) → 14.4855 mJy. A typical Ic
        // (1e51 erg, 3 M☉, same wind; 9.49844 mJy at 10 Mpc above) gives 9.49844 / 3.8² = 0.657786 mJy.
        var obs = new ObserverSetup(38 * Mpc, 0, true);
        var bw = TestOutcomes.WithWind(TestOutcomes.Custom(SupernovaType.IcBL, 3e52, 10, 0.7, 1e11), 1e-5, 1000);
        var ic = TestOutcomes.WithWind(TestOutcomes.Custom(SupernovaType.Ic, 1e51, 3, 0.1, 1e11), 1e-5, 1000);
        double fBw = Model.Sample(bw, obs, 30 * Day, "radio5"), fIc = Model.Sample(ic, obs, 30 * Day, "radio5");
        Assert.Equal(14.4855, fBw, 0.06);
        Assert.Equal(0.657786, fIc, 0.003);
        Assert.True(fBw > 10 * fIc);
        // Bright against the population: L = F 4πD² = 1.44855e-25 × 1.727764e53 = 2.5028e28 erg/s/Hz, above the Ib/c mean
        // plus 1σ, 10^(25.4 + 1.7) = 1.26e27 (Bietenholz et al. 2021, §6.1).
        Assert.True(fBw * 1e-26 * 1.727764e53 > 1.26e27);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(bw, obs, 30 * Day, Detectors.Get("vla")).Status);
    }

    [Fact]
    public void RadioSpectrum_IsSelfAbsorbedBelowThePeak_AndThinAbove()
    {
        // §6.1 / R42: L_ν = N L_p / ((x0 x)^-2.5 + x0 x): ∝ ν^(5/2) far below ν_p and ν^-1 far above. II-P at 30 d,
        // ν_p = 9.58899 GHz. By hand: x0 x = 0.189694, 0.677477, 2.98090 for 1.4, 5, 22 GHz give denominators 63.9968,
        // 3.32454, 3.04608, so f5/f14 = 19.2498 and f22/f5 = 1.09141.
        var obs = new ObserverSetup(10 * Mpc, 0, true);
        var o = IIP();
        double f14 = Model.Sample(o, obs, 30 * Day, "radio1_4"), f5 = Model.Sample(o, obs, 30 * Day, "radio5"),
            f22 = Model.Sample(o, obs, 30 * Day, "radio22");
        Assert.Equal(19.2498, f5 / f14, 0.002);
        Assert.Equal(1.09141, f22 / f5, 0.0002);
    }

    [Fact]
    public void Xrays_AddThermalAndInverseCompton_AndGasAbsorbsThem()
    {
        // Hand total at 10 d (A_* = 10, v = 1e4 km/s so v_s4 = 1, t_10 = 1): L_ff = 2.3625e38 plus inverse Compton
        // 8.8e36 × 10 × 1 × (L_bol/1e42) × ln(10/0.3) = 3.08577e38 (L_bol/1e42), with L_bol the light curve's at 10 d (an
        // input from another model). Inside the observed II-P range (up to ~3e39; §6.2).
        var o = IIP();
        var clear = new ObserverSetup(10 * Mpc, 0, true);
        double lbol = new LightCurveModel().Sample(o, clear, 10 * Day, "lbol");
        double expected = 2.3625e38 + 3.08577e38 * lbol / 1e42;
        double lx = Model.Sample(o, clear, 10 * Day, "lx");
        Assert.Equal(expected, lx, expected * 1e-5);
        Assert.True(lx < 3e39, $"lx = {lx}");
        // Unabsorbed flux = L / 4πD² (z = 0). Behind A_V = 30 (N_H = 6.6e22 cm⁻²) a spectrum flat in ln E over 0.3–10 keV
        // keeps 0.303108 of its flux (Simpson by hand on e^(−6.6e22 × 2.4e-22 E^(−8/3)), averaged in ln E).
        Assert.Equal(expected / 1.196525e52, Model.Sample(o, clear, 10 * Day, "fluxX"), expected / 1.196525e52 * 1e-4);
        Assert.Equal(0.303108 * expected / 1.196525e52, Model.Sample(o, new ObserverSetup(10 * Mpc, 30, true), 10 * Day, "fluxX"),
            0.303108 * expected / 1.196525e52 * 1e-3);
    }

    [Fact]
    public void Detection_RadioAndXrays()
    {
        var o = IIP();
        var near = new ObserverSetup(10 * Mpc, 0, true);
        // 1.1 mJy at 5 GHz against the VLA's 0.03 mJy (5σ).
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, near, 30 * Day, Detectors.Get("vla")).Status);
        var far = new ObserverSetup(1e9, 0, true);
        Assert.Equal(DetectionStatus.TooFaint, Model.Detect(o, far, 30 * Day, Detectors.Get("vla")).Status);
        // ~5e38 erg/s at 10 Mpc ≈ 4e-14 erg cm⁻² s⁻¹: Chandra (1e-15) yes.
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, near, 10 * Day, Detectors.Get("chandra")).Status);
        // Blocked, not merely faint: at the distance where the hand total (previous test) gives twice Swift XRT's 1e-13
        // unabsorbed, A_V = 30 lets through 0.303108 of it, 0.61 of the limit. The margins (2× and 1.65×) follow the hand
        // value, so a light-curve change cannot flip the verdict.
        double lbol = new LightCurveModel().Sample(o, near, 10 * Day, "lbol");
        double lx = 2.3625e38 + 3.08577e38 * lbol / 1e42;
        var dusty = new ObserverSetup(Math.Sqrt(lx / (4 * Math.PI * 2e-13)) / 3.0857e18, 30, true);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, dusty with { Av = 0 }, 10 * Day, Detectors.Get("swift-xrt")).Status);
        var blocked = Model.Detect(o, dusty, 10 * Day, Detectors.Get("swift-xrt"));
        Assert.Equal(DetectionStatus.BlockedByDust, blocked.Status);
        Assert.Contains("10 ks", blocked.Reason); // review M10: the exposure the threshold assumes
        // R42: the radio reason states the ν_p the channel gives, which is where the drawn spectrum peaks.
        Assert.Contains("9.59 GHz", Model.Detect(o, near, 30 * Day, Detectors.Get("vla")).Reason);
        Assert.Equal(DetectionStatus.NotModeled, Model.Detect(o, near, 30 * Day, Detectors.Get("superk")).Status);
        Assert.Equal(DetectionStatus.NotYet, Model.Detect(o, near, 3600, Detectors.Get("vla")).Status);
    }

    [Fact]
    public void Range_IsOneDayToThirtyYears()
    {
        var o = IIP();
        var obs = new ObserverSetup(10 * Mpc, 0, true);
        Assert.Equal((Day, 30 * Year), Model.ValidRange(o));
        Assert.True(double.IsNaN(Model.Sample(o, obs, 0.5 * Day, "radio5")));
        Assert.True(double.IsFinite(Model.Sample(o, obs, 10 * Year, "radio5")));
        Assert.True(double.IsNaN(Model.Sample(o, obs, 31 * Year, "lx")));
    }

    [Fact]
    public void TypeIa_FailedSupernovae_AndDenseCsm()
    {
        var obs = new ObserverSetup(10 * Mpc, 0, true);
        // Type Ia: clean surroundings (SN 2011fe, SN 2014J: no radio or X-rays); no CSM is invented.
        var ia = TestOutcomes.TypeIa();
        Assert.False(Model.Applies(ia));
        var d = Model.Detect(ia, obs, 30 * Day, Detectors.Get("vla"));
        Assert.Equal(DetectionStatus.NotProduced, d.Status);
        Assert.Contains("2011fe", d.Reason);
        Assert.False(Model.Applies(TestOutcomes.Star(23))); // a failed supernova
        // A fallback black hole after an explosion still drives a shock into the wind.
        var fallback = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.03, 3.5e13, TestOutcomes.BlackHole(5, 3));
        Assert.True(double.IsFinite(Model.Sample(fallback, obs, 30 * Day, "radio5")));
        // Dense CSM (a IIn) applies but is not modeled: NaN and NotModeled.
        var iin = TestOutcomes.Custom(SupernovaType.IIn, 1e51, 10, 0.03, 3.5e13,
            inputs: LabInputs.Default with { DenseCsm = true });
        Assert.True(Model.Applies(iin));
        Assert.True(double.IsNaN(Model.Sample(iin, obs, 30 * Day, "radio5")));
        Assert.Equal(DetectionStatus.NotModeled, Model.Detect(iin, obs, 30 * Day, Detectors.Get("vla")).Status);
    }

    [Fact]
    public void Sn1987ARing_RisesFrom3000To9000Days_ToTheObservedPlateau()
    {
        // §6.3: Chandra 0.5–2 keV flux flat at ~8e-12 erg cm⁻² s⁻¹ since ~9,500 d, at 51.4 kpc (presets §1):
        // L = 4π (51.4 × 3.0857e21)² × 8e-12 = 4π × 2.51558e46 × 8e-12 = 2.52891e36 erg/s. Logistic from 3,000 to 9,000 d
        // (5% → 95%): midpoint 6,000 d, width 3,000 / ln 19 = 1018.87 d. Zero before 3,000 d: the shock has not reached the
        // ring yet (review M3).
        Assert.Equal(2.52891e36 / 2, CsmInteractionModel.RingXrayLuminosityErgS(6000 * Day), 0.0001e36);
        Assert.Equal(0.05 * 2.52891e36, CsmInteractionModel.RingXrayLuminosityErgS(3000 * Day), 0.0001e36);
        Assert.Equal(0, CsmInteractionModel.RingXrayLuminosityErgS(2999 * Day));
        Assert.Equal(0, CsmInteractionModel.RingXrayLuminosityErgS(100 * Day));
        Assert.Equal(0.95 * 2.52891e36, CsmInteractionModel.RingXrayLuminosityErgS(9000 * Day), 0.0001e36);
        // At 10,500 d: 1 / (1 + e^(−4500/1018.87)) = 0.98805 of the plateau, 2.49869e36.
        Assert.Equal(2.49869e36, CsmInteractionModel.RingXrayLuminosityErgS(10_500 * Day), 0.0002e36);
    }

    [Fact]
    public void RingChannel_IsTheObservedRingLuminosity_OnlyWhenThePresetGivesTheRing()
    {
        // Ruling R41: ringLx is RingXrayLuminosityErgS as observed (0.5–2 keV, already absorbed), never absorbed again. At
        // 9,500 d: 2.52891e36 / (1 + e^(−3500/1018.87)) = 2.52891e36 × 0.968788 = 2.44998e36 erg/s, whatever the dust.
        var withRing = Ib() with { HasEquatorialRing = true };
        Assert.Equal(2.44998e36, Model.Sample(withRing, new ObserverSetup(51_400, 0, true), 9500 * Day, "ringLx"), 0.0005e36);
        Assert.Equal(2.44998e36, Model.Sample(withRing, new ObserverSetup(51_400, 5, true), 9500 * Day, "ringLx"), 0.0005e36);
        Assert.Equal(0, Model.Sample(withRing, new ObserverSetup(51_400, 0, true), 1000 * Day, "ringLx"));
        Assert.True(double.IsNaN(Model.Sample(Ib(), new ObserverSetup(51_400, 0, true), 9500 * Day, "ringLx")));
        var d = Model.Detect(withRing, new ObserverSetup(51_400, 0.47, true), 9500 * Day, Detectors.Get("chandra"));
        Assert.Contains("equatorial ring", d.Reason);
    }

    /// <summary>
    /// Ruling R51a: no interaction with the wind before the shock leaves the star. The default 16 M☉ red supergiant breaks
    /// out about 1.28 d after collapse (Kistler et al. 2013's delay, ShockBreakoutModel), later than the model's 1-day floor,
    /// so at 1.1 d nothing is drawn and the verdict is NotYet; just after breakout the glow is drawn.
    /// </summary>
    [Fact]
    public void TheWindInteraction_StartsAtBreakout_ForARedSupergiant()
    {
        const double day = 86400;
        var o = TestOutcomes.Star(16);
        var csm = new CsmInteractionModel();
        double breakout = new ShockBreakoutModel().BreakoutTimeS(o);
        Assert.InRange(breakout / day, 1.2, 1.4);
        Assert.Equal(breakout, csm.StartTimeS(o));
        Assert.Equal(breakout, csm.ValidRange(o).StartS);

        var obs = ObserverSetup.Default with { DistancePc = 1e7 };
        Assert.True(double.IsNaN(csm.Sample(o, obs, 1.1 * day, "lx")));
        Assert.True(double.IsNaN(csm.Sample(o, obs, 1.1 * day, "radio5")));
        var d = csm.Detect(o, obs, 1.1 * day, Detectors.Get("swift-xrt"));
        Assert.Equal(DetectionStatus.NotYet, d.Status);
        Assert.Contains("broken out", d.Reason);
        Assert.True(csm.Sample(o, obs, breakout * 1.01, "lx") > 0);

        // A compact star breaks out within seconds: the 1-day floor stands (§11).
        Assert.Equal(CsmInteractionModel.StartS, csm.StartTimeS(TestOutcomes.Collapsar()));
    }
}
