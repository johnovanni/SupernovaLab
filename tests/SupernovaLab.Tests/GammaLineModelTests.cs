using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>Gamma-ray lines (signals §5, §11; ruling R35).</summary>
public class GammaLineModelTests
{
    private static readonly GammaLineModel Model = new();
    private const double Day = 86400, Year = 3.156e7, Pc = 3.0857e18;
    private static readonly Source TestSource = new("test pin", "tests");

    private static Outcome PinnedIa(double nickel)
        => TestOutcomes.Engine.Compute(LabInputs.Default with { Kind = StarKind.WhiteDwarf },
            new PresetPins(null, null, nickel, null, TestSource));

    private static ObserverSetup At(double distancePc, double av = 0) => new(distancePc, av, true);

    // ---------------------------------------------------------------- formulas

    [Fact]
    public void Ti44LineFlux_CasALike_IsAbout1Point7e5()
    {
        // Brief / signals §11: 1.3e-4 M☉ of ⁴⁴Ti at 3.4 kpc after 340 yr gives about 1.7e-5 photons cm⁻² s⁻¹ (1157 keV,
        // intensity ≈ 1). By hand: τ = 58.9 yr / ln 2 = 84.9747 yr = 2.68180e9 s; N0 = 1.3e-4 × 1.989e33 / (44 × 1.6605e-24)
        // = 3.53905e51; N = N0 e^(−340/84.9747) = 3.53905e51 × 0.0182806 = 6.46962e49; N/τ = 2.41242e40 s⁻¹;
        // 4πD² = 4π (3.4e3 × 3.0857e18)² = 1.38220e45 cm²; F = 1.74538e-5.
        double f = GammaLineModel.Ti44LineFlux(1.3e-4, 340 * Year, 3.4e3 * Pc, 1.0);
        Assert.InRange(f, 0.7 * 1.7e-5, 1.3 * 1.7e-5);
        Assert.Equal(1.74538e-5, f, 0.001 * 1.74538e-5);
    }

    [Fact]
    public void Ti44LineFlux_ReproducesTheResearchCasAExample()
    {
        // Signals §5.3 [DERIVED]: 1.25e-4 M☉, age about 333 yr, τ = 85 yr gives F_68 of about 1.7e-5. By hand with
        // τ = 84.9747 yr and I_68 = 0.930: N0 = 3.40293e51, e^(−333/84.9747) = 0.0198664, F = 1.69479e-5.
        double f = GammaLineModel.Ti44LineFlux(1.25e-4, 333 * Year, 3.4e3 * Pc, 0.930);
        Assert.Equal(1.69479e-5, f, 0.001 * 1.69479e-5);
        Assert.InRange(f, 0.9 * 1.7e-5, 1.1 * 1.7e-5);
    }

    [Fact]
    public void EscapeFraction_IsExpOfMinusT0OverTSquared()
    {
        // §5.2: f_esc = exp(−(T0/t)²). At t = T0: e^-1 = 0.367879; at t = 2 T0: e^-0.25 = 0.778801.
        Assert.Equal(0.367879, GammaLineModel.EscapeFraction(30 * Day, 30 * Day), 6);
        Assert.Equal(0.778801, GammaLineModel.EscapeFraction(30 * Day, 60 * Day), 6);
        Assert.Equal(0, GammaLineModel.EscapeFraction(30 * Day, 0));
    }

    // ---------------------------------------------------------------- SN 2014J

    [Fact]
    public void Sn2014JLike_847keV_OverDays50To100_IsWithinAFactorOf2OfIntegral()
    {
        // Spec §8 / signals §5.3: Churazov et al. 2014, INTEGRAL days 50–100: F_847 = (2.34 ± 0.74)e-4 photons cm⁻² s⁻¹.
        // Ni pinned at 0.6 M☉; the engine's Ia ejecta 1.38 M☉ and E 1.35e51 erg give T0 = √(0.05 × 0.03 × M² / E)
        // = 33.4872 d. By hand (independent Python, Simpson with 20,000 intervals): N0 = 1.28339e55 ⁵⁶Ni atoms,
        // the 847 keV flux averaged over days 50–100 at 3.5 Mpc is 4.02764e-4 (1.72 × the measurement; the model's
        // escape fraction at day 75 is 0.82 against Churazov's fitted 0.60).
        var o = PinnedIa(0.6);
        var obs = At(3.5e6);
        double f = Model.AverageFlux(o, obs, "line847", 50 * Day, 100 * Day);
        Assert.InRange(f, 2.34e-4 / 2, 2.34e-4 * 2);
        Assert.Equal(4.02764e-4, f, 0.002 * 4.02764e-4);

        // The 1238 keV line (I = 0.66): 2.65824e-4 by hand, against the measured (2.78 ± 0.74)e-4.
        double f1238 = Model.AverageFlux(o, obs, "line1238", 50 * Day, 100 * Day);
        Assert.InRange(f1238, 2.78e-4 / 2, 2.78e-4 * 2);
        Assert.Equal(2.65824e-4, f1238, 0.002 * 2.65824e-4);
    }

    [Fact]
    public void Line847_AtDay75_MatchesTheHandValue()
    {
        // By hand: N_Co(75 d) = N0 × 111.3/102.5 × (e^(−75/111.3) − e^(−75/8.8)); rate N_Co/τ_Co; f_esc = 0.819256;
        // 4π (3.5 Mpc)²: F_847 = 4.12731e-4.
        double f = Model.Sample(PinnedIa(0.6), At(3.5e6), 75 * Day, "line847");
        Assert.Equal(4.12731e-4, f, 0.001 * 4.12731e-4);
        // Line ratios follow the intensities: 812/158 = 0.860/0.988, 1238/847 = 0.66.
        double f158 = Model.Sample(PinnedIa(0.6), At(3.5e6), 20 * Day, "line158");
        double f812 = Model.Sample(PinnedIa(0.6), At(3.5e6), 20 * Day, "line812");
        Assert.Equal(0.860 / 0.988, f812 / f158, 9);
    }

    // ---------------------------------------------------------------- detection

    [Fact]
    public void TypeIa_IsSeenBySpiNearby_AndByCosiToAbout20Mpc()
    {
        // Signals §11: SPI (3e-5 in 10⁶ s) sees an Ia with 0.6 M☉ of Ni to about 5–10 Mpc; COSI sees ⁵⁶Co from an Ia
        // at about 20 Mpc (Tomsick et al. 2023). SPI's 11.6-day pointing ends near the 847 keV peak (day 75); COSI's
        // 100-day survey window ends at day 135, so it spans days 35–135 around the peak. (The model's horizons are
        // about 13 Mpc for SPI and 23 Mpc for COSI.)
        var o = PinnedIa(0.6);
        double spiT = 75 * Day, cosiT = 135 * Day;
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, At(5e6), spiT, Detectors.Get("spi")).Status);
        Assert.Equal(DetectionStatus.TooFaint, Model.Detect(o, At(20e6), spiT, Detectors.Get("spi")).Status);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, At(20e6), cosiT, Detectors.Get("cosi")).Status);
        Assert.Equal(DetectionStatus.TooFaint, Model.Detect(o, At(40e6), cosiT, Detectors.Get("cosi")).Status);
    }

    [Fact]
    public void Detection_ReasonGivesTheWindow_AndDustDoesNotMatter()
    {
        var o = PinnedIa(0.6);
        var clear = Model.Detect(o, At(5e6), 75 * Day, Detectors.Get("spi"));
        var dusty = Model.Detect(o, At(5e6, av: 30), 75 * Day, Detectors.Get("spi"));
        Assert.Equal(clear.Status, dusty.Status);
        Assert.Contains("847 keV", clear.Reason);
        Assert.Contains("11.6 days", clear.Reason); // the 10⁶ s pointing
        Assert.Contains("dust", dusty.Reason);
    }

    [Fact]
    public void DetectChannel_IsTheBestLineOverTheScaledLimit()
    {
        var o = PinnedIa(0.6);
        double near = Model.Sample(o, At(5e6), 75 * Day, "detect@spi");
        double far = Model.Sample(o, At(20e6), 75 * Day, "detect@spi");
        Assert.True(near >= 1, $"SPI ratio at 5 Mpc {near}");
        Assert.True(far < 1, $"SPI ratio at 20 Mpc {far}");
        // Inverse square: 4× the distance, 1/16 of the ratio.
        Assert.Equal(near / 16, far, 1e-9 * near);
    }

    [Fact]
    public void CasALike_Ti44_IsSeenByNuStarAndCosi()
    {
        // A core-collapse outcome with Cas A's NuSTAR ⁴⁴Ti mass pinned (1.25e-4 M☉) seen from 3.4 kpc 340 yr later:
        // F_68 ≈ 1.56e-5, F_78 ≈ 1.63e-5, F_1157 ≈ 1.68e-5. NuSTAR's limit 1.7e-5 for 1 Ms scaled to the 2.4 Ms window:
        // 1.7e-5 / √2.4 = 1.0974e-5. COSI's 2-yr survey: 3.0e-6.
        var o = TestOutcomes.Engine.Compute(LabInputs.Default,
            new PresetPins(null, null, null, null, TestSource, Ti44Mass: 1.25e-4));
        Assert.Equal(1.25e-4, o.Ti44Mass!.Value);
        Assert.True(o.Ti44Mass.Published);
        var obs = At(3.4e3);
        double t = 340 * Year;
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, obs, t, Detectors.Get("nustar")).Status);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, obs, t, Detectors.Get("cosi")).Status);
        Assert.Equal(1.68e-5, Model.Sample(o, obs, t, "ti44_1157"), 0.01e-5);
    }

    [Fact]
    public void BeforeTheLinesEmerge_IsNotYet()
    {
        var o = PinnedIa(0.6);
        var (start, _) = Model.ValidRange(o);
        Assert.Equal(DetectionStatus.NotYet, Model.Detect(o, At(5e6), start / 2, Detectors.Get("spi")).Status);
    }

    // ---------------------------------------------------------------- ⁴⁴Ti mass (ruling R35)

    [Fact]
    public void Ti44Mass_IsTwoThousandthsOfTheNickel_ForCoreCollapse()
    {
        // Ruling R35: 2e-3 × M_Ni; the default 16 M☉ star has 0.0552205 M☉ of Ni → 1.10441e-4 M☉, Speculative.
        var o = TestOutcomes.Star(16);
        Assert.Equal(0.0552205, o.NickelMass!.Value, 7);
        Assert.Equal(1.10441e-4, o.Ti44Mass!.Value, 1e-9);
        Assert.Equal(Confidence.Speculative, o.Ti44Mass.Confidence);
        Assert.Null(TestOutcomes.TypeIa().Ti44Mass);
        Assert.Null(TestOutcomes.Star(23).Ti44Mass); // failed supernova
    }

    [Fact]
    public void TypeIa_Ti44_IsNotModeled_ButItsNickelLinesAre()
    {
        var o = TestOutcomes.TypeIa();
        var obs = At(5e6);
        Assert.True(Model.Applies(o));
        Assert.True(double.IsNaN(Model.Sample(o, obs, 2 * Year, "ti44_1157")));
        Assert.True(Model.Sample(o, obs, 75 * Day, "line847") > 0);
        var d = Model.Detect(o, obs, 2 * Year, Detectors.Get("nustar"));
        Assert.Equal(DetectionStatus.NotModeled, d.Status);
        Assert.Contains("thermonuclear", d.Reason);
    }

    // ---------------------------------------------------------------- not produced

    [Fact]
    public void FailedSupernova_AndNoNickel_GiveNotProduced()
    {
        var failed = TestOutcomes.Star(23);
        Assert.Equal(Fate.FailedSupernova, failed.Fate);
        Assert.False(Model.Applies(failed));
        Assert.Equal(DetectionStatus.NotProduced, Model.Detect(failed, At(1e4), 75 * Day, Detectors.Get("spi")).Status);
        Assert.True(double.IsNaN(Model.Sample(failed, At(1e4), 75 * Day, "line847")));

        var noNickel = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0, 5e13);
        Assert.False(Model.Applies(noNickel));
        Assert.Equal(DetectionStatus.NotProduced, Model.Detect(noNickel, At(1e4), 75 * Day, Detectors.Get("cosi")).Status);
    }

    [Fact]
    public void OtherDetectors_AreNotModeled()
    {
        var d = Model.Detect(PinnedIa(0.6), At(5e6), 75 * Day, Detectors.Get("gbm"));
        Assert.Equal(DetectionStatus.NotModeled, d.Status);
    }
}
