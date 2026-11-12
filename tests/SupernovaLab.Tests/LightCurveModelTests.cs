using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

public class LightCurveModelTests
{
    private static readonly LightCurveModel Model = new();
    private const double Day = PhysicalConstants.Day, RSun = PhysicalConstants.RSun;

    private static readonly ObserverSetup TenKpc = ObserverSetup.Default;

    /// <summary>The 16 M☉ star (ruling R8: 15 M☉ fails in S16): a red supergiant that explodes as II-P.</summary>
    private static readonly Outcome RedSupergiant16 = TestOutcomes.Star(16);

    /// <summary>Signals §4.3's round-number II-P: E = 1e51 erg, M_ej = 10 M☉, M_Ni = 0.05 M☉, R = 500 R☉.</summary>
    private static readonly Outcome RoundIIP = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.05, 500 * RSun);

    /// <summary>Task 5's magnetar SLSN-I: 20 M☉, Full stripping, fast rotation, strong core field.</summary>
    // Ruling R53: 30 M☉, above outcomes §5.3's 20 M☉ (20 M☉ before R53).
    private static readonly Outcome MagnetarSlsn = TestOutcomes.Engine.Compute(LabInputs.Default with
    {
        InitialMass = 30, Stripping = Stripping.Full, Rotation = Rotation.Fast, Field = CoreField.Strong,
    });

    /// <summary>
    /// SN 1987A-like (presets §1): a 19 M☉ star (initial mass ~18–20) at Z = 0.1, which the engine ends as a blue
    /// supergiant, with presets.md's values pinned: ⁵⁶Ni 0.07 M☉, R = 3e12 cm (~40–50 R☉), and the middles of E_kin
    /// 1.1–1.5 foe and M_ej 14–15 M☉.
    /// </summary>
    private static readonly Outcome Sn1987ALike = TestOutcomes.Engine.Compute(
        LabInputs.Default with { InitialMass = 19, Metallicity = 0.1 },
        new PresetPins(1.3e51, 14.5, 0.07, 3e12, new Source("SN 1987A (test)", "presets §1")));

    private static double Lbol(Outcome o, double t) => Model.Sample(o, TenKpc, t, "lbol");

    /// <summary>The bolometric maximum (time s, L erg/s), sampled every 0.05 d over [fromDays, toDays].</summary>
    private static (double TimeS, double L) BolometricPeak(Outcome o, double fromDays, double toDays)
    {
        (double, double) best = (0, 0);
        for (double d = fromDays; d <= toDays; d += 0.05)
            if (Lbol(o, d * Day) is var l && l > best.Item2) best = (d * Day, l);
        return best;
    }

    // ---------------------------------------------------------------- §4.1 heating

    [Fact]
    public void NickelCobaltDecay_PerSolarMass_MatchesNadyozhin()
    {
        // Signals §4.1 quotes Nadyozhin 1994 as written by Stritzinger & Leibundgut 2005 with the same coefficients:
        // L = 6.45e43 e^(−t/8.8 d) + 1.45e43 e^(−t/111.3 d) per M☉. By hand:
        // 10 d: 6.45e43 × 0.320984 + 1.45e43 × 0.914076 = 2.0703e43 + 1.3254e43 = 3.3958e43;
        // 100 d: 6.45e43 × 1.1607e-5 + 1.45e43 × 0.407198 = 7.49e38 + 5.9044e42 = 5.9050e42.
        Assert.Equal(3.40e43, LightCurveModel.NickelCobaltDecayErgS(1, 10 * Day), 0.02 * 3.40e43);
        Assert.Equal(5.91e42, LightCurveModel.NickelCobaltDecayErgS(1, 100 * Day), 0.02 * 5.91e42);
        Assert.Equal(3.3958e43, LightCurveModel.NickelCobaltDecayErgS(1, 10 * Day), 0.0001 * 3.3958e43);
    }

    [Fact]
    public void DepositedHeating_FollowsSignals41()
    {
        // Full trapping at 10 d, with Arnett et al. 2017's consistent set (§4.1): M☉ [3.9805e10 × 0.320984 + (6.4552e9 +
        // 2.1458e8) × 0.644005] = 1.989e33 × (1.27768e10 + 4.29537e9) = 1.989e33 × 1.707217e10 = 3.3957e43 erg/s, within
        // 0.01% of Nadyozhin's two-exponential form (3.3958e43).
        Assert.Equal(3.3957e43, LightCurveModel.DepositedNickelHeatingErgS(1, 10 * Day, 1e12), 0.0005 * 3.3957e43);
        // At 300 d with T0 = 35 d: D = 1 − exp(−(35/300)²) = 0.0135189; Co(t) = 0.073311; Ni term ≈ 0.
        // M☉ [6.4552e9 × 0.073311 × 0.0135189 + 2.1458e8 × 0.073311] = 1.989e33 × 2.21286e7 = 4.4014e40 erg/s:
        // positrons carry most of the late tail.
        Assert.Equal(4.4014e40, LightCurveModel.DepositedNickelHeatingErgS(1, 300 * Day, 35 * Day), 0.001 * 4.4014e40);
    }

    [Fact]
    public void GammaRayTrappingTime_MatchesSignals41WorkedExamples()
    {
        // T0 = √(0.05 × 0.03 × M² / E): Ia 1.4 M☉, 1.3e51 erg → 34.62 d ("about 35 d"); Ib/c 3 M☉, 1e51 → 84.58 d
        // ("about 85 d"); II-P 10 M☉, 1e51 → 281.9 d ("about 280 d"). By hand.
        Assert.Equal(34.62, LightCurveModel.GammaRayTrappingTimeS(1.4, 1.3e51) / Day, 0.01);
        Assert.Equal(84.58, LightCurveModel.GammaRayTrappingTimeS(3, 1e51) / Day, 0.01);
        Assert.Equal(281.9, LightCurveModel.GammaRayTrappingTimeS(10, 1e51) / Day, 0.1);
    }

    // ---------------------------------------------------------------- §4.2 Arnett

    [Fact]
    public void ArnettDiffusionTime_MatchesSignals42Example()
    {
        // κ = 0.1, M_ej = 3 M☉, E = 1e51: v = √(10e51 / (3 × 5.967e33)) = 7.4741e8 cm/s;
        // τ_m = √(2 × 0.1 × 5.967e33 / (13.8 × 2.99792458e10 × 7.4741e8)) = 1.96455e6 s = 22.74 d ("about 23 d").
        Assert.Equal(22.74, LightCurveModel.ArnettDiffusionTimeS(0.1, 3, 1e51) / Day, 0.01);
    }

    [Fact]
    public void ArnettKernel_WithConstantHeating_GivesOneMinusExpOfMinusXSquared()
    {
        // ∫₀ˣ 2z e^(z² − x²) dz = 1 − e^(−x²): at t = τ_m, L = P (1 − 1/e) = 0.632121 P; at 3 τ_m, P (1 − e^−9) =
        // 0.999877 P; long after, P.
        const double p = 1e42, tau = 20 * Day;
        Assert.Equal(0.632121 * p, LightCurveModel.ArnettLuminosityErgS(_ => p, tau, tau), 1e-5 * p);
        Assert.Equal(0.999877 * p, LightCurveModel.ArnettLuminosityErgS(_ => p, tau, 3 * tau), 1e-5 * p);
        Assert.Equal(p, LightCurveModel.ArnettLuminosityErgS(_ => p, tau, 1000 * tau), 1e-5 * p);
        Assert.Equal(0, LightCurveModel.ArnettLuminosityErgS(_ => p, tau, 0));
    }

    [Fact]
    public void TypeIa_WithPoint6SolarMassesOfNickel_PeaksAt1Point2e43_After15To21Days()
    {
        // Signals §4.2 / Arnett's rule: L_peak ≈ 2.0e43 × 0.6 = 1.2e43 erg/s (outcomes §7.2's Stritzinger et al. 2006
        // factor) and an observed rise of about 17–19 d (Hayden et al. 2010; Firth et al. 2015).
        var ia = TestOutcomes.TypeIa();
        Assert.Equal(0.6, ia.NickelMass!.Value);
        var (t, l) = BolometricPeak(ia, 5, 40);
        Assert.Equal(1.2e43, l, 0.15 * 1.2e43);
        Assert.InRange(t / Day, 15, 21);
        // Arnett's rule: at the peak the light equals the heating, here 0.6 × L_dep(t) with T0 = √(0.05 × 0.03 ×
        // (1.38 M☉)² / 1.35e51) = 33.5 d: within a few per cent.
        double heating = LightCurveModel.DepositedNickelHeatingErgS(0.6, t, LightCurveModel.GammaRayTrappingTimeS(1.38, 1.35e51));
        Assert.Equal(1, l / heating, 0.03);
        // The V peak comes a little after the bolometric one, as the colour reddens towards the Lira relation.
        Assert.InRange(Model.PeakTimeS(ia) / Day, t / Day, t / Day + 4);
        // Ruling R21: the outcome's Phillips peak M_B = 4.74 − 2.5 log10(1.2e43 / 3.828e33) = −19.00 (BC_B ≈ 0). An Ia
        // at peak has B−V ≈ 0, so the light curve's peak M_V agrees within a few tenths.
        Assert.Equal(-19.00, ia.PeakAbsoluteMagB!.Value, 0.01);
        Assert.Equal(-19.00, Model.Sample(ia, TenKpc, Model.PeakTimeS(ia), "absV"), 0.3);
    }

    [Fact]
    public void Opacity_SliderSetsStrippedEnvelopeRiseTimes_ButNotTypeIa()
    {
        // τ_m ∝ κ^(1/2) (§4.2): the 0.05–0.10 slider moves an Ib's peak; a Type Ia keeps its calibrated 0.19.
        var ib = LabInputs.Default with { InitialMass = 15, Stripping = Stripping.Full };
        double Rise(LabInputs x) => BolometricPeak(TestOutcomes.Engine.Compute(x), 3, 60).TimeS;
        Assert.True(Rise(ib with { Opacity = 0.05 }) < Rise(ib with { Opacity = 0.10 }) - 2 * Day);
        var ia = LabInputs.Default with { Kind = StarKind.WhiteDwarf };
        Assert.Equal(Rise(ia with { Opacity = 0.05 }), Rise(ia with { Opacity = 0.10 }));
    }

    // ---------------------------------------------------------------- §4.3 plateau

    [Fact]
    public void Plateau_RoundNumberCase_MatchesGoldberg2019ByHand()
    {
        // E51 = M10 = R500 = 1: L50 = 10^42.16 = 1.44544e42 erg/s; t_p = 10^(2.184 + 0.134 log 0.05) = 102.25 d.
        Assert.Equal(1.44544e42, Lbol(RoundIIP, 50 * Day), 0.005 * 1.44544e42);
        Assert.Equal(102.25, Model.PlateauEndS(RoundIIP)!.Value / Day, 0.05);
        // After the drop the light is the deposited ⁵⁶Ni power, 0.05 × L_dep(t) with T0 = 281.9 d (§4.3 step 4, ruling
        // R33d): from t_p + 3w the plateau's step has faded the diffusion-lagged light out. At 150 d the step is
        // 1 / (1 + e^(47.75/3)) = 1.2e-7 of L50, so the tail is L_dep to well within 0.1%.
        double Tail(double t) => LightCurveModel.DepositedNickelHeatingErgS(0.05, t, LightCurveModel.GammaRayTrappingTimeS(10, 1e51));
        Assert.Equal(1, Lbol(RoundIIP, 150 * Day) / Tail(150 * Day), 0.001);
        Assert.Equal(1, Lbol(RoundIIP, 300 * Day) / Tail(300 * Day), 0.001);
    }

    [Fact]
    public void Plateau_RedSupergiant16_Lasts80To140Days_WithL50BetweenKasenWoosleyAndGoldberg()
    {
        // Ruling R8: 16 M☉ for the brief's 15 M☉. The engine gives E = 1.255e51 erg, M_ej = 11.598 M☉, M_Ni = 0.0552 M☉,
        // R = 6.166e13 cm (R500 = 1.7726). By hand, signals §4.3:
        // Kasen & Woosley 2009: 1.26e42 × 1.255^(5/6) × 1.1598^(−1/2) × 1.7726^(2/3) = 2.0707e42 erg/s;
        // Goldberg et al. 2019: 10^(42.16 − 0.40 log 1.1598 + 0.74 log 1.255 + 0.76 log 1.7726) = 2.4899e42 erg/s;
        // t_p = 10^(2.184 + 0.134 log 0.0552 + 0.411 log 1.1598 − 0.282 log 1.255) = 103.29 d.
        var o = RedSupergiant16;
        Assert.Equal(1.255e51, o.ExplosionEnergyErg!.Value, 0.001e51);
        Assert.Equal(11.598, o.EjectaMass!.Value, 0.001);
        Assert.Equal(0.0552, o.NickelMass!.Value, 0.0001);
        Assert.Equal(6.166e13, o.Star!.RadiusCm.Value, 0.001e13);

        Assert.InRange(Lbol(o, 50 * Day), 2.0707e42, 2.4899e42 * 1.005);
        double? end = Model.PlateauEndS(o);
        Assert.NotNull(end);
        Assert.InRange(end!.Value / Day, 80, 140);
        Assert.Equal(103.29, end.Value / Day, 0.05);
        // M_V on the plateau, by hand (§4.5): a 6,000 K blackbody has B−V = 0.591373; Lyman Type II BC_B = −0.138 −
        // 0.013 × 0.591373 − 0.649 × 0.591373² = −0.372658, so BC_V = BC_B + (B−V) = 0.218716; M_bol =
        // −2.5 log10(2.49045e42 / 3.0128e35) = −2.5 × 6.917308 = −17.293270; M_V = −17.293270 − 0.218716 = −17.511986.
        Assert.Equal(-17.512, Model.Sample(o, TenKpc, 50 * Day, "absV"), 0.001);
        // Flat through the plateau, then a drop of more than a magnitude within three weeks.
        Assert.Equal(1, Lbol(o, 40 * Day) / Lbol(o, 90 * Day), 0.02);
        Assert.True(Lbol(o, (103.29 + 20) * Day) < Lbol(o, 90 * Day) / 2.5);
    }

    [Fact]
    public void Sn1987ALike_IsASlowNickelDome_PeakingNearV2Point9FromTheLmc()
    {
        // Presets §1: peak V ≈ 2.9 at ~85 d, at 51.4 kpc. Presets.md gives no A_V for SN 1987A, so the test uses A_V = 0
        // (an unreddened view; see the task report). μ = 5 log10(51,400 / 10) = 18.555.
        var o = Sn1987ALike;
        Assert.Equal(SupernovaType.IIP, o.Type);
        Assert.Equal(3e12, o.Star!.RadiusCm.Value);
        var lmc = new ObserverSetup(51_400, 0, true);
        double tPeak = Model.PeakTimeS(o);
        Assert.Equal(2.9, Model.Sample(o, lmc, tPeak, "appV"), 0.5);
        // The compact blue supergiant's plateau is faint (L50 ∝ R^0.76), so the light is the diffused ⁵⁶Ni: a slow
        // dome, not a plateau, peaking after two to three months, with M_bol near −15.5 (presets §1).
        Assert.InRange(tPeak / Day, 60, 100);
        Assert.Null(Model.PlateauEndS(o));
        Assert.True(Lbol(o, 70 * Day) > 1.5 * Lbol(o, 15 * Day));
        Assert.Equal(-15.5, BolometricCorrection.AbsoluteBolometricMagnitude(Lbol(o, tPeak)), 0.3);
    }

    [Fact]
    public void StrippedStar_CoolingPeak_Dip_ThenNickelPeak()
    {
        // The SN 2008D-like handoff (risk f): 15 M☉ Full-stripped (Ib). The helium-envelope cooling glow is bright at 1 d
        // (≈7.5e41 erg/s), fades to a dip by 3 d (≈2.8e41) before the ⁵⁶Ni light rises to its peak near 16 d (≈1.35e42):
        // L(3 d) < L(1 d) < L(16 d).
        var ib = TestOutcomes.Engine.Compute(LabInputs.Default with { InitialMass = 15, Stripping = Stripping.Full });
        Assert.Equal(SupernovaType.Ib, ib.Type);
        double l1 = Lbol(ib, Day), l3 = Lbol(ib, 3 * Day), l16 = Lbol(ib, 16 * Day);
        Assert.True(l3 < l1 && l1 < l16, $"L(1 d) {l1:E3}, L(3 d) {l3:E3}, L(16 d) {l16:E3}");
    }

    // ---------------------------------------------------------------- §4.4 magnetar

    [Fact]
    public void MagnetarSlsnI_PeaksBrighterThanMinus20()
    {
        Assert.Equal(SupernovaType.SlsnI, MagnetarSlsn.Type);
        double tPeak = Model.PeakTimeS(MagnetarSlsn);
        Assert.True(Model.Sample(MagnetarSlsn, TenKpc, tPeak, "absV") < -20);
        // Spin-down powers it: without the magnetar, 0.059 M☉ of ⁵⁶Ni gives about 1e42 erg/s, two orders less.
        Assert.True(Lbol(MagnetarSlsn, tPeak) > 1e44);
    }

    [Fact]
    public void MagnetarStar16_FastAndStrongField_DrawsTheEnginePeak_ButIsNotSuperluminous()
    {
        // Rulings R32/R33a: the 16 M☉ fast-spinning, strong-field star. Its engine peak (1.3370e44 erg/s by hand, see
        // OutcomeEngineExtremeTests) is the light curve's magnetar part; the drawn peak adds only the diffused ⁵⁶Ni and
        // the faint plateau. Ruling R53: a 16 M☉ star is not above outcomes §5.3's 20 M☉, so it stays a magnetar-boosted
        // II-P (SLSN-II before R53).
        var o = TestOutcomes.Engine.Compute(LabInputs.Default with
        {
            InitialMass = 16, Rotation = Rotation.Fast, Field = CoreField.Strong,
        });
        Assert.Equal(SupernovaType.IIP, o.Type);
        double drawn = DrawnPeak(o);
        Assert.InRange(drawn / o.PeakLuminosityErgS!.Value, 1, 1.05);
        Assert.True(drawn >= 7e43);
    }

    [Fact]
    public void SuperluminousLabel_AgreesWithTheDrawnPeak()
    {
        // Ruling R32: for every magnetar-powered outcome, the label is SLSN-I/II exactly when the drawn bolometric peak
        // is ≥ 7e43 erg/s (R22's cut) and outcomes §5.3's conditions hold (ruling R53: initial mass above 20 M☉, at least
        // 2.2 M☉ of ejecta), and the engine's stated peak is the drawn magnetar part: the drawn peak is at
        // least it and at most 5% more (⁵⁶Ni, plateau and cooling add the rest). Outcomes without a magnetar are never
        // labeled superluminous. Not covered by R22: a magnetorotational hypernova (7e51 erg) in a red supergiant, e.g.
        // 30 M☉ at Z☉ with fast rotation and a strong field, keeps its II-P label although its first days of shock
        // cooling top 7e43 (1.06e44 at 1.5 d, below the cut after ~3 d); see the task report.
        var cases = new List<Outcome> { TestOutcomes.TypeIa() };
        foreach (var field in new[] { CoreField.Normal, CoreField.Strong })
        foreach (var rotation in new[] { Rotation.Fast, Rotation.Extreme })
        foreach (var stripping in new[] { Stripping.None, Stripping.Partial, Stripping.Full })
        foreach (double z in new[] { 0.1, 1.0 })
        foreach (double mass in new[] { 9.5, 12, 16, 20, 30, 40 })
            cases.Add(TestOutcomes.Engine.Compute(LabInputs.Default with
            {
                InitialMass = mass, Metallicity = z, Rotation = rotation, Stripping = stripping, Field = field,
            }));
        int magnetars = 0;
        foreach (var o in cases.Where(Model.Applies).Where(o => !double.IsNaN(Model.ValidRange(o).EndS)))
        {
            double drawn = DrawnPeak(o);
            if (double.IsNaN(drawn)) continue;
            bool superluminous = o.Type is SupernovaType.SlsnI or SupernovaType.SlsnII;
            string what = $"{o.Inputs.InitialMass} M☉, Z {o.Inputs.Metallicity}, {o.Inputs.Rotation}, {o.Inputs.Stripping}, " +
                          $"{o.Inputs.Field}: {o.Type}, drawn peak {drawn:E3}";
            if (o.MagnetarEnergyErg is null)
            {
                Assert.False(superluminous, what);
                continue;
            }
            magnetars++;
            bool conditions = o.Inputs.InitialMass > 20 && o.EjectaMass!.Value >= 2.2;
            Assert.True(superluminous == (drawn >= 7e43 && conditions), what);
            Assert.InRange(drawn / o.PeakLuminosityErgS!.Value, 1 - 1e-6, 1.05);
        }
        Assert.True(magnetars >= 20, $"only {magnetars} magnetar outcomes");
    }

    /// <summary>The drawn bolometric peak, erg/s: the largest lbol on a 0.5%-step grid from 1 to 500 d; NaN if none.</summary>
    private static double DrawnPeak(Outcome o)
    {
        double best = double.NaN;
        for (double t = Day; t <= 500 * Day; t *= 1.005)
            if (Lbol(o, t) is var l && !double.IsNaN(l) && !(l <= best)) best = l;
        return best;
    }

    // ---------------------------------------------------------------- channels and markers

    [Fact]
    public void ApparentMagnitude_AddsDistanceModulusAndDust()
    {
        // m_V = M_V + 5 log10(D / 10 pc) + A_V (§4.7): at 10 kpc with A_V = 2, m − M = 15 + 2 = 17.
        var obs = new ObserverSetup(10_000, 2, true);
        double t = 50 * Day;
        double absV = Model.Sample(RedSupergiant16, obs, t, "absV"), appV = Model.Sample(RedSupergiant16, obs, t, "appV");
        Assert.Equal(17, appV - absV, 1e-9);
        // detect@x is the margin: the detector's limit minus m_V (naked eye 6, ZTF 20.5, Rubin 24).
        Assert.Equal(6 - appV, Model.Sample(RedSupergiant16, obs, t, "detect@eye"), 1e-9);
        Assert.Equal(20.5 - appV, Model.Sample(RedSupergiant16, obs, t, "detect@ztf"), 1e-9);
        Assert.Equal(24 - appV, Model.Sample(RedSupergiant16, obs, t, "detect@rubin"), 1e-9);
        Assert.Throws<ArgumentException>(() => Model.Sample(RedSupergiant16, obs, t, "detect@superk"));
    }

    [Fact]
    public void ValidRange_StartsWithShockCooling_OrOneHourForATypeIa_AndEndsAtThreeYears()
    {
        var cooling = new ShockBreakoutModel().Cooling(RedSupergiant16)!;
        Assert.Equal((cooling.StartS, 3 * PhysicalConstants.Year), Model.ValidRange(RedSupergiant16));
        Assert.Equal((3600, 3 * PhysicalConstants.Year), Model.ValidRange(TestOutcomes.TypeIa()));
        Assert.True(double.IsNaN(Lbol(RedSupergiant16, cooling.StartS - 1)));
        Assert.Equal(cooling.LuminosityErgS(cooling.StartS), Lbol(RedSupergiant16, cooling.StartS));
    }

    [Fact]
    public void LightCurve_IsContinuous_OnAFineGrid()
    {
        // No jumps: on a grid 0.5% apart in time, the magnitude changes by less than 0.15 mag anywhere. The steepest real
        // change is the plateau's drop: with a 3-day Fermi-Dirac width, about 0.15 mag/d at its middle, under 0.1 mag per
        // 0.5% step near 100 d.
        foreach (var o in new[] { RedSupergiant16, Sn1987ALike, MagnetarSlsn, TestOutcomes.TypeIa(),
                     TestOutcomes.Engine.Compute(LabInputs.Default with { InitialMass = 15, Stripping = Stripping.Full }) })
        {
            var (start, end) = Model.ValidRange(o);
            double prev = double.NaN;
            for (double t = start; t <= end; t *= 1.005)
            {
                double v = Model.Sample(o, TenKpc, t, "absV");
                if (!double.IsNaN(prev)) Assert.True(Math.Abs(v - prev) < 0.15, $"{o.Type}: jump of {v - prev} mag at {t / Day} d");
                prev = v;
            }
        }
    }

    [Fact]
    public void PlateauEnd_IsNullWithoutAPlateau()
    {
        Assert.Null(Model.PlateauEndS(TestOutcomes.TypeIa()));
        Assert.Null(Model.PlateauEndS(MagnetarSlsn));
        Assert.Null(Model.PlateauEndS(TestOutcomes.Star(23))); // failed
        Assert.True(double.IsNaN(Model.PeakTimeS(TestOutcomes.Star(23))));
    }

    // ---------------------------------------------------------------- detection

    [Fact]
    public void Detect_Sn1987ALike_IsNakedEyeFromTheLmc()
    {
        var lmc = new ObserverSetup(51_400, 0, true);
        var d = Model.Detect(Sn1987ALike, lmc, Model.PeakTimeS(Sn1987ALike), Detectors.Get("eye"));
        Assert.Equal(DetectionStatus.Detected, d.Status);
        Assert.Contains("naked eye", d.Reason);
    }

    [Fact]
    public void Detect_TowardTheGalacticCentre_IsBlockedByDust()
    {
        // A_V = 30 at 10 kpc: m_V ≈ −17.5 + 15 + 30 = 27.5, past even Rubin's 24; without the dust it would be −2.5.
        var obs = new ObserverSetup(10_000, 30, true);
        double t = 50 * Day;
        Assert.Equal(DetectionStatus.BlockedByDust, Model.Detect(RedSupergiant16, obs, t, Detectors.Get("eye")).Status);
        Assert.Equal(DetectionStatus.BlockedByDust, Model.Detect(RedSupergiant16, obs, t, Detectors.Get("rubin")).Status);
    }

    [Fact]
    public void Detect_TypeIaAt1Gpc_IsTooFaintForZtf_ButSeenByRubin()
    {
        // M_V ≈ −19 at peak; at 1 Gpc (z ≈ 0.25, μ ≈ 40.5) m_V ≈ 21.5: past ZTF's 20.5, inside Rubin's 24.
        var ia = TestOutcomes.TypeIa();
        var far = new ObserverSetup(1e9, 0, true);
        double t = Model.PeakTimeS(ia);
        Assert.Equal(DetectionStatus.TooFaint, Model.Detect(ia, far, t, Detectors.Get("ztf")).Status);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(ia, far, t, Detectors.Get("rubin")).Status);
        Assert.Equal(DetectionStatus.TooFaint, Model.Detect(ia, far, t, Detectors.Get("eye")).Status);
    }

    [Fact]
    public void Detect_BeforeTheLight_AfterTheModel_AndOtherInstruments()
    {
        var o = RedSupergiant16;
        Assert.Equal(DetectionStatus.NotYet, Model.Detect(o, TenKpc, 1000, Detectors.Get("eye")).Status);
        Assert.Equal(DetectionStatus.NotModeled, Model.Detect(o, TenKpc, 4 * PhysicalConstants.Year, Detectors.Get("eye")).Status);
        Assert.Equal(DetectionStatus.NotModeled, Model.Detect(o, TenKpc, 50 * Day, Detectors.Get("superk")).Status);
        // The spectrograph's classification limit, V 18, is a visible-light threshold too.
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, TenKpc, 50 * Day, Detectors.Get("spectrograph")).Status);
    }

    [Fact]
    public void FailedSupernova_IsNotProduced_AndMentionsTheVanishingStar()
    {
        var failed = TestOutcomes.Star(23);
        Assert.Equal(Fate.FailedSupernova, failed.Fate);
        Assert.False(Model.Applies(failed));
        var d = Model.Detect(failed, TenKpc, 50 * Day, Detectors.Get("eye"));
        Assert.Equal(DetectionStatus.NotProduced, d.Status);
        Assert.Contains("vanish", d.Reason);
        Assert.Contains("N6946-BH1", d.Reason);
        Assert.True(double.IsNaN(Lbol(failed, 50 * Day)));
    }

    [Fact]
    public void PulsationalPairInstabilityTransient_IsProducedButNotModeled()
    {
        var o = TestOutcomes.Engine.Compute(LabInputs.Default with { InitialMass = 100, Metallicity = 0.01 });
        Assert.Equal(Fate.PulsationalPairInstability, o.Fate);
        Assert.Equal(SupernovaType.IIn, o.Type);
        Assert.True(Model.Applies(o));
        Assert.Equal(DetectionStatus.NotModeled, Model.Detect(o, TenKpc, 50 * Day, Detectors.Get("eye")).Status);
        Assert.True(double.IsNaN(Lbol(o, 50 * Day)));
        Assert.True(double.IsNaN(Model.PeakTimeS(o)));
    }

    [Fact]
    public void InteractingSupernova_CarriesTheMissingCollisionCaveat()
    {
        var iin = TestOutcomes.Engine.Compute(LabInputs.Default with { InitialMass = 16, DenseCsm = true });
        Assert.Equal(SupernovaType.IIn, iin.Type);
        Assert.Contains("dense gas", Model.Detect(iin, TenKpc, 50 * Day, Detectors.Get("eye")).Reason);
    }
}
