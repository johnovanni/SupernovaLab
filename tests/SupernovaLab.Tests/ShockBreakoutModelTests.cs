using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

public class ShockBreakoutModelTests
{
    private static readonly ShockBreakoutModel Model = new();
    private const double RSun = PhysicalConstants.RSun, Mpc = PhysicalConstants.Mpc, Day = PhysicalConstants.Day;

    /// <summary>The 16 M☉ star (ruling R8: 15 M☉ fails in S16) with no stripping: a red supergiant that explodes as II-P.</summary>
    private static readonly Outcome RedSupergiant16 = TestOutcomes.Star(16);

    /// <summary>15 M☉, Full stripping: a compact Wolf-Rayet-like He star exploding as Ib.</summary>
    private static readonly Outcome FullStripped15 =
        TestOutcomes.Engine.Compute(LabInputs.Default with { InitialMass = 15, Stripping = Stripping.Full });

    /// <summary>The brief's SN 2008D-like case: Ib, R = 1e11 cm, E = 6e51 erg, M_ej = 7 M☉ (signals §3.2; presets §8).</summary>
    private static readonly Outcome Sn2008DLike = TestOutcomes.Custom(SupernovaType.Ib, 6e51, 7, 0.07, 1e11);

    /// <summary>Signals §3.1's red-supergiant worked example: n = 3/2, R = 500 R☉, M_ej = 10 M☉, E = 1e51, M_NS = 1.4.</summary>
    private static readonly Outcome RsgExample = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.05, 500 * RSun);

    private static ObserverSetup At(double distancePc, double av = 0) => new(distancePc, av, true);

    // ---------------------------------------------------------------- breakout delay (signals §3.1)

    [Fact]
    public void BreakoutDelay_RedSupergiant16Msun_IsBetween0_3And2Days()
    {
        Assert.Equal(Fate.Supernova, RedSupergiant16.Fate);
        Assert.Equal(Stripping.None, RedSupergiant16.Inputs.Stripping);
        Assert.InRange(Model.BreakoutTimeS(RedSupergiant16), 0.3 * Day, 2 * Day);
    }

    [Fact]
    public void BreakoutDelay_FullStripped15Msun_IsBetween10SecondsAnd1Hour()
    {
        Assert.Equal(Fate.Supernova, FullStripped15.Fate);
        Assert.InRange(Model.BreakoutTimeS(FullStripped15), 10, 3600);
    }

    [Fact]
    public void BreakoutDelay_MatchesSignals31WorkedExamples()
    {
        // Kistler et al. 2013 Eq. 5, by hand (E = 1e51, M_NS = 1.4):
        // RSG n = 3/2, 500 R☉, 10 M☉: x = 0.14; 7226 × 1 × 1 × 10 × (1 − 0.738·0.14^0.8 + 0.467·0.14^1.2) = 64,386 s (§3.1: 6.4e4).
        Assert.Equal(64_386, Model.BreakoutTimeS(RsgExample), 64_386 * 0.001);
        // BSG n = 3, 50 R☉, 14 M☉ (a SN 1987A-like compact H star): x = 0.1; 6851 × √1.4 × 1 × (1 − 0.407·0.1^0.81 +
        // 0.285·0.1^1.12) = 7,770 s (§3.1: 7.8e3).
        var bsg = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 14, 0.07, 50 * RSun);
        Assert.Equal(7_770.5, Model.BreakoutTimeS(bsg), 7_770.5 * 0.001);
        // Wolf-Rayet n = 3, 3 R☉, 3 M☉: x = 0.467; 6851 × √0.3 × 0.06 × (1 − 0.407·x^0.81 + 0.285·x^1.12) = 203.0 s (§3.1: ~200).
        var wr = TestOutcomes.Custom(SupernovaType.Ib, 1e51, 3, 0.07, 3 * RSun);
        Assert.Equal(203.05, Model.BreakoutTimeS(wr), 203.05 * 0.001);
    }

    [Fact]
    public void BreakoutTime_IsNaN_WhenThereIsNoBreakout()
    {
        Assert.True(double.IsNaN(Model.BreakoutTimeS(TestOutcomes.TypeIa())));
        var failed = TestOutcomes.Star(23);
        Assert.Equal(Fate.FailedSupernova, failed.Fate);
        Assert.True(double.IsNaN(Model.BreakoutTimeS(failed)));
    }

    // ---------------------------------------------------------------- the flash (signals §3.2)

    [Fact]
    public void Sn2008DLike_PeakXRayLuminosity_IsWithinAFactorOf3Of6e43()
    {
        // Signals §3.2: Swift XRT measured a peak L_X of 6.1e43 erg/s in 0.3-10 keV. The model's 0.3-10 keV luminosity
        // is the swift-xrt band flux × 4πD² (10 Mpc, no dust, z = 0).
        var obs = At(10e6);
        double tPeak = Model.BreakoutTimeS(Sn2008DLike) + 1; // inside the 3.3-s flash
        double lx = Model.Sample(Sn2008DLike, obs, tPeak, "flux@swift-xrt") * 4 * Math.PI * Math.Pow(obs.LuminosityDistanceCm, 2);
        Assert.InRange(lx, 6e43 / 3, 6e43 * 3);
        // Hand computation (independent script): bolometric 1.0084e44 erg/s with v_bo capped at 0.3c (below); the
        // blackbody-plus-Comptonized-plateau spectrum (kT_eq = 167.1 eV, hν_peak = 38.53 keV) puts 61.15% in 0.3-10 keV:
        // 6.166e43 erg/s, 1.03 × 6e43.
        Assert.Equal(6.166e43, lx, 6.166e43 * 0.01);
    }

    [Fact]
    public void Sn2008DLike_BolometricFlash_MatchesWaxmanKatzByHand()
    {
        // Waxman & Katz 2017 (signals §3.2), n = 3, He envelope κ = 0.2 (κ_0.34 = 0.5882):
        // v_* = √(6e51 / 7 M☉) = 6.5646e8 cm/s (v_*,8.5 = 2.0759); v_bo/v_* = 13 · 0.7^0.16 · 2.0759^0.16 · 0.1^-0.32 ·
        // 0.5882^0.16 = 26.488, so v_bo = 1.7388e10 cm/s = 0.58c. Ruling R28 caps it at 0.3c = 8.9938e9 cm/s:
        // E_BO = 2.2e47 · (0.01)² · 8.9938 / 0.5882 = 3.3637e44 erg; t_bo = 90 s / 0.5882 / 73.62 / 8.9938² = 0.026 s
        // < R/c = 3.3356 s, so L = 3.3637e44 / 3.3356 = 1.0084e44 erg/s.
        double t = Model.BreakoutTimeS(Sn2008DLike) + 1;
        Assert.Equal(1.0084e44, Model.Sample(Sn2008DLike, At(10e6), t, "lum"), 1.0084e44 * 0.002);
        // hν_peak = 10^(1.4 + √8.9938 + (0.25 − 0.05 √8.9938) log10 73.62) = 38.53 keV; temp = hν_peak / (3.9207 k_B)
        // = 1.1403e8 K (no longer ~1e9 K).
        Assert.Equal(1.1403e8, Model.Sample(Sn2008DLike, At(10e6), t, "temp"), 1.1403e8 * 0.002);
    }

    [Fact]
    public void RedSupergiantFlash_IsAboutRoverCLong_AndBrighterThan1e43()
    {
        // Signals §3.2: the RSG flash lasts about R/c (20 min for 500 R☉); allow 1e43-1e45 erg/s [APPROX range]. W&K 2017
        // gives E_BO = 1.857e48 erg for this example (n = 3/2, M10 = 1, v_*,8.5 = 0.7091, R13 = 3.4785), so
        // L = 1.857e48 / 1,160.3 s = 1.60e45 erg/s: the formula sits just above the research's rough range (reported).
        double tb = Model.BreakoutTimeS(RsgExample);
        var obs = At(10e6);
        Assert.Equal(1.6009e45, Model.Sample(RsgExample, obs, tb + 600, "lum"), 1.6009e45 * 0.002);
        Assert.Equal(1.6009e45, Model.Sample(RsgExample, obs, tb + 1150, "lum"), 1.6009e45 * 0.002);
        Assert.True(Model.Sample(RsgExample, obs, tb + 1300, "lum") < 1.6e45);
    }

    // ---------------------------------------------------------------- shock cooling (signals §3.3)

    [Fact]
    public void Cooling_RedSupergiantAt2Days_MatchesSapirWaxmanByHand()
    {
        // Sapir & Waxman 2017, n = 3/2, f_ρ = 1, κ_0.34 = 1, M = 10, R13 = 3.4785, M_env = 5 (TestOutcomes: ej/2):
        // v_s* = 1.05 √(E/M) = 2.3543e8 (v_s*,8.5 = 0.74451); at t_d = 2: L_RW = 2.0e42 (0.74451·4/10)^-0.086 · 0.74451² ·
        // 3.4785 = 4.2796e42; t_tr = 19.5 d (5 / 0.74451)^0.5 = 50.534 d; suppression 0.94 exp(−(1.67·2/50.534)^0.8) = 0.83889;
        // L = 3.5902e42 erg/s. T_ph = 1.61 (0.74451²·4/10)^0.027 · 3.4785^0.25 / √2 = 1.4928 eV; T_col = 1.2 T_ph = 20,788 K.
        double t = Model.BreakoutTimeS(RsgExample) + 2 * Day;
        var obs = At(10e6);
        Assert.Equal(3.5902e42, Model.Sample(RsgExample, obs, t, "lum"), 3.5902e42 * 0.002);
        Assert.Equal(20_788, Model.Sample(RsgExample, obs, t, "temp"), 20_788 * 0.002);
    }

    [Fact]
    public void Cooling_HeliumEnvelope_MatchesRabinakWaxmanByHand()
    {
        // Rabinak & Waxman 2011 He envelope (signals §3.3), E51 = 6, M = 7, R12 = 0.1, f_ρ = 1, at t = 1e4 s (t5 = 0.1):
        // L = 3.3e42 · 6^0.84 · 0.1^0.85 / 7^0.67 · 0.1^-0.03 = 6.1087e41 erg/s; T_ph = 1.33 · 0.1^0.2 · 0.1^-0.38 = 2.0130 eV,
        // T_col = 1.2 T_ph = 28,032 K. Valid from 9.6 s to 5.28e4 s after breakout (T_ph > 1.07 eV).
        double t = Model.BreakoutTimeS(Sn2008DLike) + 1e4;
        var obs = At(10e6);
        Assert.Equal(6.1087e41, Model.Sample(Sn2008DLike, obs, t, "lum"), 6.1087e41 * 0.002);
        Assert.Equal(28_032, Model.Sample(Sn2008DLike, obs, t, "temp"), 28_032 * 0.002);
        var (start, end) = Model.ValidRange(Sn2008DLike);
        Assert.Equal(Model.BreakoutTimeS(Sn2008DLike), start);
        Assert.Equal(start + 52_757, end, 52_757 * 0.002);
    }

    [Fact]
    public void Luminosity_IsContinuous_AndFallsAfterTheFlash()
    {
        foreach (var o in new[] { RedSupergiant16, FullStripped15, Sn2008DLike, RsgExample })
        {
            var (start, end) = Model.ValidRange(o);
            var obs = At(10e6);
            double previous = double.NaN;
            for (double f = 0; f <= 1; f += 1.0 / 2000)
            {
                double t = start + (end - start) * Math.Pow(f, 3);
                double l = Model.Sample(o, obs, t, "lum");
                Assert.True(double.IsFinite(l) && l > 0, $"{o.Type} lum at {t} s = {l}");
                if (!double.IsNaN(previous)) Assert.True(l <= previous * 1.0001, $"{o.Type} lum rises at {t} s");
                previous = l;
            }
        }
    }

    // ---------------------------------------------------------------- detection

    [Fact]
    public void XrayFluenceThresholds_AreTheRegistryFluxTimesExposure()
    {
        // Ruling R29: Swift XRT 1e-13 erg cm⁻² s⁻¹ × 10 ks = 1e-9 erg cm⁻²; Einstein Probe WXT 2.5e-11 × 1,000 s = 2.5e-8.
        var xrt = Detectors.Get("swift-xrt");
        var ep = Detectors.Get("einstein-probe");
        Assert.Equal(1e-9, ShockBreakoutModel.FluenceThreshold(xrt), 1e-9 * 1e-9);
        Assert.Equal(10_000, ShockBreakoutModel.ExposureS(xrt));
        Assert.Equal(2.5e-8, ShockBreakoutModel.FluenceThreshold(ep), 2.5e-8 * 1e-9);
        Assert.Equal(1_000, ShockBreakoutModel.ExposureS(ep));
    }

    [Fact]
    public void Sn2008DLike_SwiftXrt_DetectedAt27Mpc_TooFaintAt1Gpc()
    {
        var xrt = Detectors.Get("swift-xrt");
        double t = Model.BreakoutTimeS(Sn2008DLike) + 10;
        // The flash's 0.3-10 keV fluence: 6.166e43 erg/s × 3.3356 s = 2.06e44 erg (plus a little from the bridge). At
        // 27 Mpc (4πD² = 8.722e52 cm²) that is 2.4e-9 erg cm⁻² ≥ 1e-9: Detected. The horizon is about √(2.06e44 /
        // (4π · 1e-9)) = 1.28e26 cm ≈ 41 Mpc for the flash alone.
        var near = Model.Detect(Sn2008DLike, At(27e6), t, xrt);
        Assert.Equal(DetectionStatus.Detected, near.Status);
        Assert.Contains("pointed", near.Reason);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(Sn2008DLike, At(38e6), t, xrt).Status);
        Assert.Equal(DetectionStatus.TooFaint, Model.Detect(Sn2008DLike, At(60e6), t, xrt).Status); // horizon ≈ 47 Mpc
        var far = Model.Detect(Sn2008DLike, At(1e9), t, xrt);
        Assert.Equal(DetectionStatus.TooFaint, far.Status);
        // The flash is stretched by 1 + z (about 1.25 at 1 Gpc): R/c = 3.3356 s × (1 + z).
        Assert.Contains(UnitFormat.Time(3.3356 * At(1e9).TimeDilation), far.Reason);
    }

    [Fact]
    public void XrayVerdict_NeverTurnsTooFaintIntoDetected()
    {
        // Ruling R29 (review I2): with one fluence criterion the verdict runs NotYet → Detected → TooFaint (faded), or is
        // TooFaint throughout; it never flips from TooFaint to Detected at the end of the flash.
        int Rank(DetectionStatus s) => s switch
        {
            DetectionStatus.NotYet => 0,
            DetectionStatus.Detected => 1,
            DetectionStatus.TooFaint => 2,
            _ => throw new InvalidOperationException($"unexpected {s}"),
        };
        var cases = new[]
        {
            (RedSupergiant16, 300e6), (Sn2008DLike, 300e6), (Sn2008DLike, 100e6), (Sn2008DLike, 27e6),
            (RedSupergiant16, 10e6), (FullStripped15, 30e6),
        };
        foreach (var (o, d) in cases)
        foreach (string id in new[] { "swift-xrt", "einstein-probe" })
        {
            var det = Detectors.Get(id);
            var (start, end) = Model.ValidRange(o);
            double flashS = o.Star!.RadiusCm.Value / PhysicalConstants.C; // R/c sets these flashes' length
            var times = new List<double> { start + flashS * 0.99, start + flashS * 1.01 };
            for (int i = 0; i <= 400; i++) times.Add(start + (end - start) * Math.Pow(10, -6 + 6.0 * i / 400));
            times.Sort();
            int previous = 0;
            foreach (double t in times)
            {
                int rank = Rank(Model.Detect(o, At(d), t, det).Status);
                Assert.True(rank >= previous, $"{o.Type} at {d / 1e6} Mpc, {id}: verdict went back at τ = {t - start} s");
                previous = rank;
            }
        }
    }

    [Fact]
    public void HydrogenFreeAndRelativisticBreakouts_CarryTheirCaveats()
    {
        var xrt = Detectors.Get("swift-xrt");
        // SN 2008D-like: hydrogen-free (R30) and v_bo = 0.58c (R28).
        string reason = Model.Detect(Sn2008DLike, At(27e6), Model.BreakoutTimeS(Sn2008DLike) + 10, xrt).Reason;
        Assert.Contains("dense stellar wind", reason);
        Assert.Contains("Relativistic breakout", reason);
        Assert.Contains("outside the Waxman & Katz formulas' range", reason);
        // A red supergiant has neither caveat.
        string rsg = Model.Detect(RedSupergiant16, At(10e6), Model.BreakoutTimeS(RedSupergiant16) + 100, xrt).Reason;
        Assert.DoesNotContain("dense stellar wind", rsg);
        Assert.DoesNotContain("Relativistic", rsg);
        Assert.Contains(Model.Sources, s => s.Citation.Contains("outside the Waxman & Katz formulas' range"));
        Assert.Contains(Model.Sources, s => s.Citation.Contains("dense stellar wind"));
    }

    [Fact]
    public void MagnetarPoweredStrippedStar_TemperatureIsCapped()
    {
        // Review I1: a Full-stripped 16 M☉ star with Extreme rotation and a strong field (SLSN-I) breaks out at about
        // 0.88c uncapped, which gave ~1e10 K. With v_bo ≤ 0.3c, hν_peak ≤ 10^(4.40 + 0.10 log10 ρ_-9): ≲ 50 keV, ≲ 2e8 K.
        var slsn = TestOutcomes.Engine.Compute(LabInputs.Default with
        {
            InitialMass = 16, Stripping = Stripping.Full, Rotation = Rotation.Extreme, Field = CoreField.Strong,
        });
        Assert.True(Model.Applies(slsn));
        double t = Model.BreakoutTimeS(slsn) + 0.1;
        Assert.InRange(Model.Sample(slsn, At(10e6), t, "temp"), 1e6, 2e8);
        Assert.Contains("Relativistic breakout", Model.Detect(slsn, At(10e6), t, Detectors.Get("swift-xrt")).Reason);
    }

    [Fact]
    public void KistlerDelay_ClampsTheRemnantToEjectaRatioAtOne()
    {
        // An ultra-stripped explosion, M_ej = 0.1 M☉ and a 1.26 M☉ neutron star (x = 12.6), is evaluated at x = 1:
        // 6851 × √(1e51/1e51) × √(0.1/10) × (3/50) × (1 − 0.407 + 0.285) = 6851 × 0.1 × 0.06 × 0.878 = 36.091 s.
        Assert.Equal(36.091, ShockBreakoutModel.KistlerDelayS(3, 1e51, 0.1, 3, 1.26), 36.091 * 1e-4);
    }

    [Fact]
    public void CoolingPhase_GivesOnlyTheCoolingFormula()
    {
        // Ruling R28: Task 9's accessor. RSG example (n = 3/2): cooling valid from 1.2 · 10^0.4 · 1 · 3.4785^1.3 h =
        // 54,865 s to 7.4 · 3.4785^0.55 d = 1,269,144 s after breakout; at 2 d L = 3.5902e42 erg/s, T_col = 20,788 K.
        var cooling = Model.Cooling(RsgExample)!;
        double tb = Model.BreakoutTimeS(RsgExample);
        Assert.Equal(tb + 54_865, cooling.StartS, 54_865 * 0.001);
        Assert.Equal(tb + 1_269_144, cooling.EndS, 1_269_144 * 0.001);
        Assert.Equal(3.5902e42, cooling.LuminosityErgS(tb + 2 * Day), 3.5902e42 * 0.002);
        Assert.Equal(20_788, cooling.ColourTemperatureK(tb + 2 * Day), 20_788 * 0.002);
        Assert.True(double.IsNaN(cooling.LuminosityErgS(tb + 600)), "the flash is not part of the cooling phase");
        Assert.Null(Model.Cooling(TestOutcomes.TypeIa()));
    }

    [Fact]
    public void CoolingPhase_FadesPastItsEnd_WithSapirWaxmanLateSuppression()
    {
        // RSG example (n = 3/2, M_env = 5 M☉ in TestOutcomes.Custom): v_s*,8.5 = 1.05 √(1e51 / 1.989e34) / 10^8.5 =
        // 0.744512; t_tr = 19.5 √(5 / 0.744512) = 50.534 d. The formula ends at τ_end = 1,269,144 s = 14.6892 d after
        // breakout; at 2 τ_end the suppression ratio is exp((1.67 τ_end/t_tr)^0.8 − (1.67 · 2τ_end/t_tr)^0.8) =
        // exp(0.56093 − 0.97664) = 0.659877, by hand. T_col continues as τ^(−1/2): at 4 τ_end it is half its end value.
        var cooling = Model.Cooling(RsgExample)!;
        double tb = Model.BreakoutTimeS(RsgExample), tauEnd = cooling.EndS - tb;
        double lEnd = cooling.LuminosityErgS(cooling.EndS);
        Assert.Equal(lEnd, cooling.FadingLuminosityErgS(cooling.EndS));
        Assert.Equal(lEnd, cooling.FadingLuminosityErgS(cooling.EndS + 1), lEnd * 1e-5);
        Assert.Equal(0.659877, cooling.FadingLuminosityErgS(tb + 2 * tauEnd) / lEnd, 0.002);
        Assert.Equal(0.5, cooling.FadingColourTemperatureK(tb + 4 * tauEnd) / cooling.ColourTemperatureK(cooling.EndS), 1e-9);
        Assert.True(double.IsNaN(cooling.FadingLuminosityErgS(cooling.StartS - 1)));
        Assert.True(double.IsNaN(cooling.LuminosityErgS(tb + 2 * tauEnd)), "the formula itself stays bounded by its validity");
    }

    [Theory]
    [InlineData(1.0e11)]
    [InlineData(1.1e11)]
    [InlineData(1.2e11)]
    [InlineData(1.3e11)]
    [InlineData(1.4e11)]
    [InlineData(1.5e11)]
    [InlineData(1.7e11)]
    [InlineData(2.0e11)]
    public void CoolingPhase_AtItsStart_UsesTheCoolingFormula_NotTheFlash(double radiusCm)
    {
        // Task 8 review N1. A very energetic, compact, hydrogen-free star: E = 1e53 erg, M_ej = 1 M☉, R = 1–2e11 cm. The
        // cooling formula's own start, 1.2 · 1^0.4 · 10^-1 · (R/1e13)^1.3 h, is 1.1–2.7 s, shorter than the flash
        // (R/c = 3.3–6.7 s), so the cooling phase starts when the flash ends. There the luminosity must be the Rabinak &
        // Waxman helium formula, L ∝ t^−0.03, which changes by about 3e-11 per 1e-9 relative step — not the flash's
        // E_BO / duration, orders of magnitude brighter.
        var o = TestOutcomes.Custom(SupernovaType.Ic, 1e53, 1, 0.1, radiusCm);
        var cooling = Model.Cooling(o)!;
        double start = cooling.StartS, after = start * (1 + 1e-9);
        double l0 = cooling.LuminosityErgS(start), l1 = cooling.LuminosityErgS(after);
        Assert.Equal(1, l0 / l1, 1e-6);
        Assert.Equal(1, cooling.ColourTemperatureK(start) / cooling.ColourTemperatureK(after), 1e-6);
    }

    [Fact]
    public void UltrasatChannel_IsAnAbMagnitude()
    {
        var mag = Assert.Single(Model.Channels, c => c.Id == "mag@ultrasat");
        Assert.Equal("AB mag", mag.Unit);
        Assert.True(mag.MayBeNegative);
        Assert.DoesNotContain(Model.Channels, c => c.Id == "flux@ultrasat");
        Assert.Throws<ArgumentException>(() => Model.Sample(RsgExample, At(10e6), Model.BreakoutTimeS(RsgExample), "flux@ultrasat"));
    }

    [Fact]
    public void Ultrasat_WithAv30_IsBlockedByDust()
    {
        var ultrasat = Detectors.Get("ultrasat");
        double t = Model.BreakoutTimeS(RedSupergiant16) + 1 * Day;
        // At 10 kpc with no dust the cooling emission is far brighter than 22.5 AB mag ...
        Assert.Equal(DetectionStatus.Detected, Model.Detect(RedSupergiant16, At(10_000), t, ultrasat).Status);
        // ... but A_UV = 2.6 × 30 = 78 mag of dust hides it.
        Assert.Equal(DetectionStatus.BlockedByDust, Model.Detect(RedSupergiant16, At(10_000, 30), t, ultrasat).Status);
    }

    [Fact]
    public void SoftXrays_AreAbsorbedByTheGasColumn()
    {
        // N_H = 2.2e21 A_V: at A_V = 30 the 0.3-10 keV flux of the 2008D-like flash falls; it is never raised.
        double t = Model.BreakoutTimeS(Sn2008DLike) + 1;
        double clear = Model.Sample(Sn2008DLike, At(10e6), t, "flux@swift-xrt");
        double dusty = Model.Sample(Sn2008DLike, At(10e6, 30), t, "flux@swift-xrt");
        Assert.True(dusty < clear * 0.5, $"{dusty} vs {clear}");
        Assert.True(dusty > 0);
    }

    [Fact]
    public void TypeIa_IsNotProduced()
    {
        var ia = TestOutcomes.TypeIa();
        Assert.False(Model.Applies(ia));
        foreach (string id in new[] { "swift-xrt", "einstein-probe", "ultrasat" })
            Assert.Equal(DetectionStatus.NotProduced, Model.Detect(ia, ObserverSetup.Default, 100, Detectors.Get(id)).Status);
    }

    [Fact]
    public void Detect_BeforeBreakout_IsNotYet_AndOtherInstrumentsAreNotModeled()
    {
        double tb = Model.BreakoutTimeS(RedSupergiant16);
        Assert.Equal(DetectionStatus.NotYet,
            Model.Detect(RedSupergiant16, ObserverSetup.Default, tb / 2, Detectors.Get("swift-xrt")).Status);
        Assert.Equal(DetectionStatus.NotModeled,
            Model.Detect(RedSupergiant16, ObserverSetup.Default, tb + 10, Detectors.Get("chandra")).Status);
        var superk = Model.Detect(RedSupergiant16, ObserverSetup.Default, tb + 10, Detectors.Get("superk"));
        Assert.Equal(DetectionStatus.NotModeled, superk.Status);
        Assert.Contains("not modeled in this view", superk.Reason);
    }

    [Fact]
    public void DenseCsm_IsNotModeled()
    {
        var iin = TestOutcomes.Engine.Compute(LabInputs.Default with { DenseCsm = true });
        Assert.Equal(SupernovaType.IIn, iin.Type);
        Assert.True(Model.Applies(iin));
        Assert.True(double.IsNaN(Model.BreakoutTimeS(iin)));
        Assert.Equal(DetectionStatus.NotModeled, Model.Detect(iin, ObserverSetup.Default, 1e5, Detectors.Get("ultrasat")).Status);
    }
}
