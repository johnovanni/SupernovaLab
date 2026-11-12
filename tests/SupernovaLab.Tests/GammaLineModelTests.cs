using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
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
    public void KleinNishinaOpacity_At847keV_MatchesTheHandValue()
    {
        // Ruling R37: κ_KN = σ_KN Y_e / m_u, Y_e = 0.5, m_e c² = 510.99895 keV, r_e = 2.8179403262e-13 cm (CODATA 2018).
        // x = 846.77 / 510.99895 = 1.657088, ln(1 + 2x) = 1.461906;
        // σ_KN = 2π r_e² {(1+x)/x² [2(1+x)/(1+2x) − ln(1+2x)/x] + ln(1+2x)/(2x) − (1+3x)/(1+2x)²}
        //      = 4.98935e-25 cm² × 0.458549 = 2.28786e-25 cm²; κ_KN = 2.28786e-25 × 0.5 / 1.6605e-24 = 0.0688906 cm²/g
        // (independent Python). The ruling's check: ≈ 0.068. Low energies approach Thomson, 0.5 × 6.6524587e-25 / m_u
        // = 0.200315 cm²/g; 1157 keV gives 0.0591444.
        Assert.Equal(0.0688906, GammaLineModel.KleinNishinaOpacity(846.77), 0.0001 * 0.0688906);
        Assert.Equal(0.200315, GammaLineModel.KleinNishinaOpacity(1e-3), 0.0001 * 0.200315);
        Assert.Equal(0.0591444, GammaLineModel.KleinNishinaOpacity(1157.0), 0.0001 * 0.0591444);
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
        // = 33.4872 d. Ruling R37: the 847 keV line escapes with T0_line = T0 √(κ_KN(847 keV) / 0.03) = 33.4872 ×
        // √(0.0688906 / 0.03) = 50.7456 d. By hand (independent Python, Simpson with 20,000 intervals): N0 = 1.28339e55
        // ⁵⁶Ni atoms, the 847 keV flux averaged over days 50–100 at 3.5 Mpc is 3.02061e-4 (1.29 × the measurement;
        // f_esc at day 75 is 0.632675 against Churazov's fitted 0.60).
        var o = PinnedIa(0.6);
        var obs = At(3.5e6);
        double f = Model.AverageFlux(o, obs, "line847", 50 * Day, 100 * Day);
        Assert.InRange(f, 2.34e-4 / 2, 2.34e-4 * 2);
        Assert.Equal(3.02061e-4, f, 0.002 * 3.02061e-4);

        // The 1238 keV line (I = 0.66, κ_KN 0.0571318): 2.17175e-4 by hand, against the measured (2.78 ± 0.74)e-4.
        double f1238 = Model.AverageFlux(o, obs, "line1238", 50 * Day, 100 * Day);
        Assert.InRange(f1238, 2.78e-4 / 2, 2.78e-4 * 2);
        Assert.Equal(2.17175e-4, f1238, 0.002 * 2.17175e-4);
    }

    [Fact]
    public void Line847_AtDay75_MatchesTheHandValue()
    {
        // By hand: N_Co(75 d) = N0 × 111.3/102.5 × (e^(−75/111.3) − e^(−75/8.8)); rate N_Co/τ_Co; line escape (R37)
        // f_esc = exp(−(50.7456/75)²) = 0.632675; 4π (3.5 Mpc)²: F_847 = 3.18734e-4.
        double f = Model.Sample(PinnedIa(0.6), At(3.5e6), 75 * Day, "line847");
        Assert.Equal(3.18734e-4, f, 0.001 * 3.18734e-4);
        Assert.Equal(0.632675, GammaLineModel.LineEscapeFraction(33.4872 * Day, 846.77, 75 * Day), 5);
        // Ratio of the two ⁵⁶Ni lines at day 20: intensities 0.860/0.988 times the escape ratio
        // exp(−(T0/t)² (κ_KN(812) − κ_KN(158)) / 0.03) = exp(2.80353 × 2.04089): 265.825. The 158 keV line is far more
        // easily scattered.
        double f158 = Model.Sample(PinnedIa(0.6), At(3.5e6), 20 * Day, "line158");
        double f812 = Model.Sample(PinnedIa(0.6), At(3.5e6), 20 * Day, "line812");
        Assert.Equal(265.825, f812 / f158, 0.002 * 265.825);
    }

    // ---------------------------------------------------------------- detection

    [Fact]
    public void TypeIa_IsSeenBySpiNearby_AndByCosiFarther()
    {
        // Signals §11: SPI (3e-5 in 10⁶ s) sees an Ia with 0.6 M☉ of Ni to about 5–10 Mpc; COSI (narrow-line 3e-6) sees
        // ⁵⁶Co from an Ia at about 20 Mpc (Tomsick et al. 2023). Ruling R52 raises both limits for the broad Ia lines
        // (SPI ×3.69, COSI ×1.90 at 847 keV), so the model's horizons shrink to about 5.9 Mpc (SPI) and 15 Mpc (COSI).
        // SPI's 11.6-day pointing ends near the 847 keV peak (day 75); COSI's 100-day window ends at day 135.
        var o = PinnedIa(0.6);
        double spiT = 75 * Day, cosiT = 135 * Day;
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, At(4e6), spiT, Detectors.Get("spi")).Status);
        Assert.Equal(DetectionStatus.TooFaint, Model.Detect(o, At(10e6), spiT, Detectors.Get("spi")).Status);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, At(12e6), cosiT, Detectors.Get("cosi")).Status);
        Assert.Equal(DetectionStatus.TooFaint, Model.Detect(o, At(25e6), cosiT, Detectors.Get("cosi")).Status);
    }

    // ---------------------------------------------------------------- ruling R52: broad lines

    [Fact]
    public void LineWidthFactor_ScalesTheNarrowLineLimit()
    {
        // Ruling R52: limit × √max(1, ΔE_line / ΔE_instr). A Type Ia line: SN 2014J's 4,100 km/s rms × 2.355 = 9,655.5
        // km/s FWHM, so at 846.77 keV ΔE = 846.77 × 9,655.5 / 299,792.458 = 27.2722 keV.
        // SPI (≈2 keV): √(27.2722 / 2) = 3.69271. COSI, interpolated between 6.0 keV at 511 and 9.0 keV at 1,157 keV:
        // 6.0 + 3.0 × 335.77/646 = 7.55930 keV, √(27.2722 / 7.55930) = 1.89941. NuSTAR keeps the narrow-line figure (1).
        // A line narrower than the instrument's resolution keeps 1.
        Assert.Equal(3.69271, GammaLineModel.LineWidthFactor("spi", 846.77, 9655.5), 4);
        Assert.Equal(1.89941, GammaLineModel.LineWidthFactor("cosi", 846.77, 9655.5), 4);
        Assert.Equal(1.0, GammaLineModel.LineWidthFactor("nustar", 67.87, 9655.5));
        Assert.Equal(1.0, GammaLineModel.LineWidthFactor("spi", 846.77, 500));
        // Type Ia: 9,655.5 km/s (R52). Core collapse: v_ej = √(10E / 3M) (signals §0.4); the default 16 M☉ star,
        // E 1.25537e51 erg and M_ej 11.59828 M☉: 4,259.03 km/s.
        Assert.Equal(9655.5, GammaLineModel.LineFwhmKmS(TestOutcomes.TypeIa()), 6);
        // Review M2: Type Iax, the R45c velocity 5,000 km/s taken as the FWHM (as for core collapse); SPI factor at 847 keV
        // √(846.77 × 5,000 / 299,792.458 / 2) = 2.65731.
        var iax = TestOutcomes.Engine.Compute(LabInputs.Default with { Kind = StarKind.WhiteDwarf, IaChannel = TypeIaChannel.Iax });
        Assert.Equal(SupernovaType.Iax, iax.Type);
        Assert.Equal(5000, GammaLineModel.LineFwhmKmS(iax));
        Assert.Equal(2.65731, GammaLineModel.LineWidthFactor("spi", 846.77, 5000), 4);
        Assert.Equal(4259.03, GammaLineModel.LineFwhmKmS(TestOutcomes.Star(16)), 0.01);
    }

    private static (Outcome Outcome, ObserverSetup Observer) FromPreset(string id)
    {
        var p = PresetCatalog.Get(id);
        return (TestOutcomes.Engine.Compute(p.Inputs, p.Pins), p.Observer);
    }

    [Fact]
    public void Sn2014J_StaysDetectedBySpi_WithItsBroadLines()
    {
        // Churazov et al. 2014 detected the 847 keV line (about 3σ). Preset: 0.6 M☉ Ni at 3.53 Mpc, the engine's Ia
        // ejecta (1.38 M☉, 1.35e51 erg), so T0_line(847) = 50.7456 d (R37). By hand (independent Python, Simpson with
        // 20,000 intervals): the 10⁶ s SPI pointing ending at day 75 (days 63.43–75) averages 3.03576e-4 against
        // 3e-5 × 3.69271 = 1.10781e-4, a ratio of 2.74032 (before R52, against 3e-5: 10.1).
        var (o, obs) = FromPreset("sn2014j");
        var d = Model.Detect(o, obs, 75 * Day, Detectors.Get("spi"));
        Assert.Equal(DetectionStatus.Detected, d.Status);
        Assert.Contains("broad", d.Reason);
        Assert.Equal(2.74032, Model.Sample(o, obs, 75 * Day, "detect@spi"), 0.002 * 2.74032);
    }

    [Fact]
    public void Sn2011fe_IsNotDetectedBySpi_AtAnyTime()
    {
        // INTEGRAL/SPI saw none of SN 2011fe's lines (Isern et al. 2013, A&A 552, A97; days 45–88: 830–875 keV
        // < 1.39e-4 photons cm⁻² s⁻¹, 2σ, ATel 3822), which its authors put down to the lines' 20–40 keV width. Preset:
        // 0.53 M☉ Ni at 6.43 Mpc. Before R52 the lab "detected" it: its best SPI ratio over days 5–400 was 2.81 against the
        // narrow-line 3e-5 (the app's snapshot showed one moment of it, 6.8e-5 against 3e-5). Now the best is 0.76.
        // The margin is thin and rests on two [APPROX]/pinned inputs: SPI's resolution (2 keV here; at the ATel's ~3 keV
        // the ratio would be ≈0.93) and Pereira et al.'s 0.53 M☉ of Ni (≈0.70 M☉ would reach 1).
        var (o, obs) = FromPreset("sn2011fe");
        var spi = Detectors.Get("spi");
        for (double day = 5; day <= 400; day += 1)
            Assert.NotEqual(DetectionStatus.Detected, Model.Detect(o, obs, day * Day, spi).Status);
        Assert.Contains("broad", Model.Detect(o, obs, 75 * Day, spi).Reason);
        // The lab's line stays below the published upper limit over the same days.
        Assert.True(Model.AverageFlux(o, obs, "line847", 45 * Day, 88 * Day) < 1.39e-4);
        var fact = PresetCatalog.Get("sn2011fe").Facts.Single(f => f.ChannelId == "line847");
        Assert.Equal(1.39e-4, fact.Value);
        Assert.Contains("upper limit", fact.Description);
    }

    [Fact]
    public void Sn2011fe_LineFact_IsAPreliminaryUpperLimitOverDays45To88()
    {
        // Review I1/I2: ATel 3822 gives "preliminary" 95% (2σ) upper limits for days 45–88.
        var fact = PresetCatalog.Get("sn2011fe").Facts.Single(f => f.ChannelId == "line847");
        Assert.True(fact.IsUpperLimit);
        Assert.Equal(45 * Day, fact.SourceTimeS);
        Assert.Equal(88 * Day, fact.SourceTimeEndS);
        Assert.Contains("preliminary", fact.Description);
        Assert.Contains("preliminary", fact.Source.Citation);
        Assert.Contains("ATel 3822", fact.Source.Citation);
        // Every other fact is a measurement at one time (or none).
        foreach (var p in PresetCatalog.LoadEmbedded())
        foreach (var f in p.Facts.Where(f => !ReferenceEquals(f, fact)))
        {
            Assert.False(f.IsUpperLimit, $"{p.Id}/{f.ChannelId}");
            Assert.Null(f.SourceTimeEndS);
        }
    }

    [Fact]
    public void DrawnLimit_IsTheBroadLineLimitOfTheDefaultWindow()
    {
        // Review M1: the limit a panel draws is the one the badge uses once the window is full. Type Ia, 847 keV:
        // SPI 3e-5 (10⁶ s window = reference) × 3.69271 = 1.10781e-4; COSI 3e-6 × √(2 yr / 100 d) = 3e-6 × 2.70288,
        // × 1.89941 = 1.54016e-5; NuSTAR's ⁴⁴Ti line stays narrow: 2e-6.
        var ia = PinnedIa(0.6);
        Assert.Equal(1.10781e-4, GammaLineModel.DrawnLimit(ia, "spi", "line847"), 0.0005 * 1.10781e-4);
        Assert.Equal(1.54016e-5, GammaLineModel.DrawnLimit(ia, "cosi", "line847"), 0.0005 * 1.54016e-5);
        Assert.Equal(2e-6, GammaLineModel.DrawnLimit(TestOutcomes.Star(16), "nustar", "ti44_68"), 1e-15);
        Assert.True(double.IsNaN(GammaLineModel.DrawnLimit(ia, "nustar", "line847"))); // NuSTAR does not see it
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
        // F_1157 = 1.74538e-5 (the 1.3e-4 M☉ hand value above) × 1.25/1.3 = 1.67825e-5, with f_esc ≈ 1 at 340 yr (T0_line
        // ≈ 2 yr); F_68 = 0.930 × that ≈ 1.56e-5 and F_78 = 0.968 × that ≈ 1.62e-5. NuSTAR (ruling R38): 2e-6 for its 2.6 Ms
        // window. COSI's 2-yr survey: 3.0e-6.
        var o = TestOutcomes.Engine.Compute(LabInputs.Default,
            new PresetPins(null, null, null, null, TestSource, Ti44Mass: 1.25e-4));
        Assert.Equal(1.25e-4, o.Ti44Mass!.Value);
        Assert.True(o.Ti44Mass.Published);
        var obs = At(3.4e3);
        double t = 340 * Year;
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, obs, t, Detectors.Get("nustar")).Status);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, obs, t, Detectors.Get("cosi")).Status);
        Assert.Equal(1.67825e-5, Model.Sample(o, obs, t, "ti44_1157"), 0.001 * 1.67825e-5);
    }

    [Fact]
    public void Sn1987ALike_Ti44_IsSeenByNuStar()
    {
        // Ruling R38: NuSTAR saw SN 1987A's ⁴⁴Ti (Boggs et al. 2015, ~5σ in ~2.6 Ms) about 27 yr after the explosion.
        // 1.5e-4 M☉ at 51.4 kpc, 27 yr: N0 = 4.08289e51, e^(−27/84.9747) = 0.727736, rate 1.10794e42 s⁻¹,
        // 4π (51.4 kpc)² = 3.16016e47 → F_78 = 3.39349e-6 before escape. Pinned E 1.1e51, M_ej 14: T0 = 376.356 d;
        // κ_KN(78 keV) = 0.156431, T0_line = 2.35275 yr, f_esc(27 yr) = 0.992436 → F_78 = 3.36782e-6 (independent
        // Python), above 2e-6. F_68 (I = 0.930) is similar.
        var o = TestOutcomes.Engine.Compute(LabInputs.Default,
            new PresetPins(1.1e51, 14, 0.07, 3e12, TestSource, Ti44Mass: 1.5e-4));
        var obs = At(51.4e3);
        double t = 27 * Year;
        Assert.Equal(3.36782e-6, Model.Sample(o, obs, t, "ti44_78"), 0.002 * 3.36782e-6);
        var d = Model.Detect(o, obs, t, Detectors.Get("nustar"));
        Assert.Equal(DetectionStatus.Detected, d.Status);
        Assert.Contains("30.1 days", d.Reason); // the 2.6 Ms window
        Assert.Equal(2e-6, Detectors.Get("nustar").Threshold);
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
