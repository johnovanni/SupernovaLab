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
        // §6.1 forward model at 30 d, 5 GHz, 10 Mpc (z = 0, 4πD² = 1.196525e52 cm²), smooth join s = 1:
        //  II-P (above): ν_p = 9.58899 GHz, x = 0.521431, L = 2.44595e26 / (x^-2.5 + x) = 4.35623e25 → 0.364078 mJy.
        //  Ib: R/t = 58,000 km/s [APPROX, geometric middle of CF06's 32,000–105,000] at 1e51 erg / 3 M☉, A_* = 1:
        //      B = 0.381066 G, R = 1.50336e16 cm, L_p = 1.26901e27, ν_p = 3.60610 GHz, L(5 GHz) = 6.94101e26 → 5.80103 mJy.
        var obs = new ObserverSetup(10 * Mpc, 0, true);
        double iip = Model.Sample(IIP(), obs, 30 * Day, "radio5"), ib = Model.Sample(Ib(), obs, 30 * Day, "radio5");
        Assert.Equal(0.364078, iip, 0.002);
        Assert.Equal(5.80103, ib, 0.03);
        Assert.True(ib > iip);
    }

    [Fact]
    public void Sn1998bwLikeEjecta_GiveABrightRadioSource()
    {
        // R39: SN 1998bw's radio comes from this model. Iwamoto et al. 1998's fit (presets §7): Ic-BL, 3e52 erg, 10 M☉
        // ejecta, 0.7 M☉ ⁵⁶Ni, in a Wolf-Rayet wind (A_* = 1, §0.4), at its 38 Mpc. R/t = 58,000 × sqrt((3e52/10) /
        // (1e51/3)) = 174,000 km/s = 0.580c (below the Γβ = 1 cap). At 30 d: L_p = 2.50328e28, ν_p = 4.93581 GHz, so
        // L(5 GHz) = 1.26351e28 erg/s/Hz → 7.31295 mJy. A typical Ic (1e51 erg, 3 M☉, same wind) gives 0.401734 mJy.
        var obs = new ObserverSetup(38 * Mpc, 0, true);
        var bw = TestOutcomes.WithWind(TestOutcomes.Custom(SupernovaType.IcBL, 3e52, 10, 0.7, 1e11), 1e-5, 1000);
        var ic = TestOutcomes.WithWind(TestOutcomes.Custom(SupernovaType.Ic, 1e51, 3, 0.1, 1e11), 1e-5, 1000);
        double fBw = Model.Sample(bw, obs, 30 * Day, "radio5"), fIc = Model.Sample(ic, obs, 30 * Day, "radio5");
        Assert.Equal(7.31295, fBw, 0.04);
        Assert.Equal(0.401734, fIc, 0.003);
        Assert.True(fBw > 10 * fIc);
        // Bright against the population: L = F 4πD² = 7.31295e-26 × 1.727764e53 = 1.2635e28 erg/s/Hz, above the Ib/c mean
        // plus 1σ, 10^(25.4 + 1.7) = 1.26e27 (Bietenholz et al. 2021, §6.1).
        Assert.True(fBw * 1e-26 * 1.727764e53 > 1.26e27);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(bw, obs, 30 * Day, Detectors.Get("vla")).Status);
    }

    [Fact]
    public void RadioSpectrum_IsSelfAbsorbedBelowThePeak_AndThinAbove()
    {
        // §6.1: L_ν = L_p / ((ν/ν_p)^-2.5 + ν/ν_p) (s = 1): ∝ ν^(5/2) far below ν_p and ν^-1 far above. II-P at 30 d,
        // ν_p = 9.58899 GHz. By hand: x = 0.146001, 0.521431, 2.29430 for 1.4, 5, 22 GHz give denominators 122.921,
        // 5.61482, 2.41972, so f5/f14 = 21.8923 and f22/f5 = 2.32044.
        var obs = new ObserverSetup(10 * Mpc, 0, true);
        var o = IIP();
        double f14 = Model.Sample(o, obs, 30 * Day, "radio1_4"), f5 = Model.Sample(o, obs, 30 * Day, "radio5"),
            f22 = Model.Sample(o, obs, 30 * Day, "radio22");
        Assert.Equal(21.8923, f5 / f14, 0.002);
        Assert.Equal(2.32044, f22 / f5, 0.0002);
    }

    [Fact]
    public void Xrays_AddThermalAndInverseCompton_AndGasAbsorbsThem()
    {
        var o = IIP();
        var clear = new ObserverSetup(10 * Mpc, 0, true);
        double lx = Model.Sample(o, clear, 10 * Day, "lx");
        // At least the thermal part (2.3625e38 erg/s), and within the observed II-P range (up to ~3e39; §6.2).
        Assert.True(lx >= 2.3625e38 && lx < 3e39, $"lx = {lx}");
        // Unabsorbed flux = L / 4πD² (z = 0).
        Assert.Equal(lx / 1.196525e52, Model.Sample(o, clear, 10 * Day, "fluxX"), lx / 1.196525e52 * 1e-4);
        double dusty = Model.Sample(o, new ObserverSetup(10 * Mpc, 5, true), 10 * Day, "fluxX");
        Assert.True(dusty < 0.9 * lx / 1.196525e52);
    }

    [Fact]
    public void Detection_RadioAndXrays()
    {
        var o = IIP();
        var near = new ObserverSetup(10 * Mpc, 0, true);
        // 0.36 mJy at 5 GHz against the VLA's 0.03 mJy (5σ).
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, near, 30 * Day, Detectors.Get("vla")).Status);
        var far = new ObserverSetup(1e9, 0, true);
        Assert.Equal(DetectionStatus.TooFaint, Model.Detect(o, far, 30 * Day, Detectors.Get("vla")).Status);
        // ~5e38 erg/s at 10 Mpc ≈ 4e-14 erg cm⁻² s⁻¹: Chandra (1e-15) yes.
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, near, 10 * Day, Detectors.Get("chandra")).Status);
        // At 5 Mpc the unabsorbed flux (~1.6e-13) would reach Swift XRT's 1e-13, but behind A_V = 30 (N_H = 6.6e22 cm⁻²)
        // only the hard part above ~3 keV gets through: blocked, not merely faint.
        var dusty = new ObserverSetup(5e6, 30, true);
        Assert.Equal(DetectionStatus.BlockedByDust, Model.Detect(o, dusty, 10 * Day, Detectors.Get("swift-xrt")).Status);
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
        // (5% → 95%): midpoint 6,000 d, width 3,000 / ln 19 = 1018.87 d.
        Assert.Equal(2.52891e36 / 2, CsmInteractionModel.RingXrayLuminosityErgS(6000 * Day), 0.0001e36);
        Assert.Equal(0.05 * 2.52891e36, CsmInteractionModel.RingXrayLuminosityErgS(3000 * Day), 0.0001e36);
        Assert.Equal(0.95 * 2.52891e36, CsmInteractionModel.RingXrayLuminosityErgS(9000 * Day), 0.0001e36);
        // At 10,500 d: 1 / (1 + e^(−4500/1018.87)) = 0.98805 of the plateau, 2.49869e36.
        Assert.Equal(2.49869e36, CsmInteractionModel.RingXrayLuminosityErgS(10_500 * Day), 0.0002e36);
    }
}
