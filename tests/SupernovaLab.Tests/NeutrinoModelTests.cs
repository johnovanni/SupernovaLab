using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

public class NeutrinoModelTests
{
    private static readonly NeutrinoModel Model = new();
    private static readonly Outcome Star12 = TestOutcomes.Star(12);
    private static readonly Outcome Failed23 = TestOutcomes.Star(23);
    private static readonly ObserverSetup At10Kpc = ObserverSetup.Default;

    private static ObserverSetup AtKpc(double kpc) => new(kpc * 1000, 0, true);

    /// <summary>Composite Simpson's rule with n (even) intervals.</summary>
    private static double Integrate(Func<double, double> f, double a, double b, int n = 4000)
    {
        double h = (b - a) / n, s = f(a) + f(b);
        for (int i = 1; i < n; i++) s += (i % 2 == 1 ? 4 : 2) * f(a + i * h);
        return s * h / 3;
    }

    private static double Sample(Outcome o, double t, string channel, ObserverSetup? obs = null)
        => Model.Sample(o, obs ?? At10Kpc, t, channel);

    // ---- the brief's cases ----

    [Fact]
    public void NeutronizationBurst_Releases2_2e51Erg()
    {
        Assert.Equal(Fate.Supernova, Star12.Fate);
        // ν_e carries the burst on top of the same accretion and cooling as anti-ν_e, so L_nue − L_anue is the burst.
        // Signals §1.2d: L_pk s √(2π) = 3.5e53 × 2.5e-3 × 2.5066 = 2.19e51 erg; the 2.3% before t = 0 (2σ) is cut,
        // leaving 2.14e51 — within 5% of 2.2e51.
        double e = Integrate(t => Sample(Star12, t, "lum_nue") - Sample(Star12, t, "lum_anue"), 0, 0.05);
        Assert.InRange(e, 2.2e51 * 0.95, 2.2e51 * 1.05);
    }

    [Fact]
    public void MeanAntiNueEnergy_AtHalfASecond_Is11To16MeV()
        => Assert.InRange(Sample(Star12, 0.5, "meanE_anue"), 11, 16);

    [Fact]
    public void FailedSupernova_AllLuminositiesAreZero_OnceTheBlackHoleForms()
    {
        Assert.Equal(Fate.FailedSupernova, Failed23.Fate);
        double tBh = Failed23.Remnant.FormationTimeS!.Value;
        foreach (string c in new[] { "lum_nue", "lum_anue", "lum_nux" })
        {
            Assert.True(Sample(Failed23, tBh - 0.01, c) > 0, $"{c} should shine before t_BH");
            Assert.Equal(0, Sample(Failed23, tBh + 0.01, c));
        }
    }

    [Fact]
    public void SuperK_At10Kpc_For12Msun_Counts2900To9800()
        // Signals §1.4: IBD alone 2,900–7,300 (Suwa et al. 2019) up to the worked check's 9,800.
        => Assert.InRange(Sample(Star12, 20, "cumulative@superk"), 2900, 9800);

    [Fact]
    public void SuperK_AtM31_IsTooFaint_ButDetectedAt10And200Kpc()
    {
        // Ruling R25: the distant-supernova trigger needs 2 events with E_e+ ≥ 17 MeV within 20 s (signals §1.4). At
        // M31 (778 kpc) even the all-IBD count is ~1.3, so the trigger count is below 2. Super-K detects with 100%
        // efficiency to about 200 kpc (§1.4): ~20 IBD events there, of which roughly half to 70% pass 17 MeV.
        var superk = Detectors.Get("superk");
        Assert.True(Sample(Star12, 20, "cumulative@superk", AtKpc(778)) < 2);
        Assert.Equal(DetectionStatus.TooFaint, Model.Detect(Star12, AtKpc(778), 20, superk).Status);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(Star12, At10Kpc, 20, superk).Status);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(Star12, AtKpc(200), 20, superk).Status);
    }

    [Fact]
    public void SuperKTriggerFraction_Above17MeV_MatchesTheHandValue()
    {
        // Fraction of the IBD event spectrum (Fermi-Dirac E² × σ ∝ (E − 1.293)²) with E_e+ = E − 1.293 ≥ 17 MeV.
        // Computed independently by Simpson integration (200,000 steps, 0–400 MeV): ⟨E⟩ = 12 MeV → 0.5196,
        // 15 MeV → 0.7005, 20 MeV → 0.8637.
        Assert.InRange(NeutrinoModel.SuperKTriggerFraction(12), 0.5196 * 0.99, 0.5196 * 1.01);
        Assert.InRange(NeutrinoModel.SuperKTriggerFraction(15), 0.7005 * 0.99, 0.7005 * 1.01);
        Assert.InRange(NeutrinoModel.SuperKTriggerFraction(20), 0.8637 * 0.99, 0.8637 * 1.01);
        foreach (double e in new[] { 1.0, 5, 30, 1e4 })
            Assert.InRange(NeutrinoModel.SuperKTriggerFraction(e), 0, 1);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(2.0)]
    [InlineData(4.7)]
    public void FailedSupernova_AntiNueEnergy_StaysInsideTheModelCompilation(double tBh)
    {
        // Signals §1.3 (Suwa et al. 2025, 28 models): E(anti-ν_e) = 0.27–1.53e53 erg for t_BH = 0.34–6.2 s.
        // Ruling R23: L_a = L_a0 min(1, 2 s / t_BH), L_a0 = 7.41e52 erg/s, gives 3.7e52 (0.5 s) and 1.48e53 (2, 4.7 s).
        var o = TestOutcomes.Custom(SupernovaType.None, null, null, null, 1e13, TestOutcomes.BlackHole(10, tBh),
            Fate.FailedSupernova);
        double e = Integrate(t => Sample(o, t, "lum_anue"), 0, tBh);
        Assert.InRange(e, 0.27e53, 1.53e53);
    }

    [Fact]
    public void Dune_SeesTheBurstThroughMswConversion()
    {
        // Ruling R25: F_νe,Earth = 0.022 F_νe + 0.978 F_νx. By hand for 12 M☉ (scale ≈ 1.17): unoscillated, the ν_e rate
        // at the burst peak (5 ms, Σ L⟨E⟩ ≈ 3.5e53 × 12.5 + 1.17 × 7.3e52 × 11.5 ≈ 5.4e54) is ~20 times that at 0.5 s
        // (≈ 1.17 × 2.0e52 × 11.5 ≈ 2.7e53). With conversion: 0.022 × 5.4e54 ≈ 1.2e53 at 5 ms (ν_x ≈ 0 so early)
        // against 0.022 × 2.7e53 + 0.978 × 1.17 × 3.75e51 × 14.5 ≈ 6.8e52 at 0.5 s, a ratio of ~1.8.
        double ratio = Sample(Star12, 0.005, "rate@dune") / Sample(Star12, 0.5, "rate@dune");
        Assert.InRange(ratio, 1.0, 3.0);
    }

    [Fact]
    public void SuperK_At2Mpc_IsTooFaint()
        => Assert.Equal(DetectionStatus.TooFaint, Model.Detect(Star12, AtKpc(2000), 20, Detectors.Get("superk")).Status);

    // ---- formulas against the research file's numbers ----

    [Theory]
    [InlineData(12, 1.5e-41)]
    [InlineData(14, 2.1e-41)]
    [InlineData(15, 2.4e-41)]
    [InlineData(16, 2.8e-41)]
    public void MeanIbdCrossSection_MatchesTheFermiDiracTable(double meanEnergyMeV, double expectedCm2)
        // Signals §1.4: spectrum-averaged <σ> for a Fermi-Dirac spectrum, quoted to two figures.
        => Assert.InRange(NeutrinoModel.MeanIbdCrossSectionCm2(meanEnergyMeV), expectedCm2 * 0.97, expectedCm2 * 1.03);

    [Fact]
    public void BindingEnergy_Of1_4Msun12Km_Is2_8e53Erg()
        // Signals §1.1: Lattimer & Prakash 2001 for M = 1.4 M☉, R = 12 km gives about 2.8e53 erg.
        => Assert.InRange(NeutrinoModel.BindingEnergyErg(1.4, 12), 2.8e53 * 0.98, 2.8e53 * 1.02);

    [Fact]
    public void TotalNeutrinoEnergy_IsTheBindingEnergyPlusTheBurst()
    {
        // A 1.4 M☉, 12 km neutron star: E_b = 2.83e53 erg (β = G M/(R c²) = 0.17232; 0.6β/(1 − 0.5β) = 0.11314;
        // M c² = 2.5026e54 erg), plus the 2.14e51 erg burst = 2.85e53 erg. Six species: ν_e, anti-ν_e and four ν_x.
        var o = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.05, 3e13, TestOutcomes.NeutronStar(1.4, 12));
        double total = Integrate(t => Sample(o, t, "lum_nue") + Sample(o, t, "lum_anue") + 4 * Sample(o, t, "lum_nux"),
                           0, 0.05)
                       + Integrate(t => Sample(o, t, "lum_nue") + Sample(o, t, "lum_anue") + 4 * Sample(o, t, "lum_nux"),
                           0.05, 100, 20000);
        Assert.InRange(total, 2.85e53 * 0.98, 2.85e53 * 1.02);
    }

    [Fact]
    public void FallbackBlackHole_CutsTheCoolingCurveAtTbh()
    {
        var o = TestOutcomes.Custom(SupernovaType.IIP, 5e50, 10, 0, 3e13, TestOutcomes.BlackHole(5, 3.0));
        Assert.True(Model.Applies(o));
        Assert.True(Sample(o, 2.9, "lum_anue") > 1e51);
        Assert.Equal(0, Sample(o, 3.1, "lum_anue"));
        Assert.Equal(0, Sample(o, 3.1, "rate@superk"));
        // The cumulative count stops growing after t_BH.
        Assert.Equal(Sample(o, 3.1, "cumulative@superk"), Sample(o, 50, "cumulative@superk"), 6);
    }

    [Fact]
    public void Counts_FallAsTheInverseSquareOfDistance()
    {
        double n10 = Sample(Star12, 20, "cumulative@superk");
        double n20 = Sample(Star12, 20, "cumulative@superk", AtKpc(20));
        Assert.Equal(0.25, n20 / n10, 9);
    }

    [Theory]
    // SNEWS 2.0 (Al Kharusi et al. 2021) at 10 kpc, signals §1.4: JUNO 3,800–7,200; DUNE 2,700–5,800; IceCube
    // 3.2e5–8.2e5 photon hits; Hyper-K from SNEWS 2.0's 28,000 up to the Design Report's 90,000.
    [InlineData("hyperk", 28000, 90000)]
    [InlineData("juno", 3800, 7200)]
    [InlineData("dune", 2700, 5800)]
    [InlineData("icecube", 3.2e5, 8.2e5)]
    public void OtherDetectors_At10Kpc_MatchThePublishedRanges(string detector, double low, double high)
        => Assert.InRange(Sample(Star12, 20, "cumulative@" + detector), low, high);

    [Fact]
    public void IceCube_DetectsTheLmc_ButNotM31()
    {
        // Signals §11: about 6σ at 50 kpc, so detected there; at M31 the significance is ~240 times lower.
        var icecube = Detectors.Get("icecube");
        Assert.Equal(DetectionStatus.Detected, Model.Detect(Star12, AtKpc(50), 20, icecube).Status);
        Assert.Equal(DetectionStatus.TooFaint, Model.Detect(Star12, AtKpc(770), 20, icecube).Status);
    }

    [Fact]
    public void AnExpectedCountBelowOne_IsNeverDetected()
    {
        foreach (var d in Detectors.All.Where(d => d.Messenger == Messenger.Neutrinos && d.Id != "icecube"))
        foreach (double kpc in new[] { 100.0, 300, 770, 1000, 2000, 5000, 1e5 })
        {
            var obs = AtKpc(kpc);
            double n = Sample(Star12, 20, "cumulative@" + d.Id, obs);
            var status = Model.Detect(Star12, obs, 20, d).Status;
            if (n < 2) Assert.Equal(DetectionStatus.TooFaint, status);
            // Super-K's trigger counts only events above 17 MeV (ruling R25), so n ≥ 2 need not be Detected there.
            else if (d.Id != "superk") Assert.Equal(DetectionStatus.Detected, status);
        }
    }

    [Fact]
    public void Detect_IsNotYetBeforeBounce_AndNotModeledAfterTheBurst()
    {
        var superk = Detectors.Get("superk");
        Assert.Equal(DetectionStatus.NotYet, Model.Detect(Star12, At10Kpc, -1, superk).Status);
        Assert.Equal(DetectionStatus.NotModeled, Model.Detect(Star12, At10Kpc, 1e4, superk).Status);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(Star12, At10Kpc, 1, superk).Status);
    }

    [Fact]
    public void ThermonuclearAndWhiteDwarfOutcomes_ProduceNoNeutrinoBurst()
    {
        foreach (var o in new[] { TestOutcomes.TypeIa(), TestOutcomes.Star(3) })
        {
            Assert.False(Model.Applies(o));
            Assert.Equal(DetectionStatus.NotProduced, Model.Detect(o, At10Kpc, 1, Detectors.Get("superk")).Status);
            Assert.True(double.IsNaN(Sample(o, 1, "lum_anue")));
        }
    }

    [Fact]
    public void ABareRateChannel_IsSuperK_AndAnUnknownChannelThrows()
    {
        Assert.Equal(Sample(Star12, 1, "rate@superk"), Sample(Star12, 1, "rate"));
        Assert.Throws<ArgumentException>(() => Sample(Star12, 1, "rate@aligo"));
        Assert.Throws<ArgumentException>(() => Sample(Star12, 1, "nonsense"));
    }

    [Fact]
    public void ChannelId_SplitsTheDetectorSuffix()
    {
        Assert.Equal(new ChannelId("rate", "superk"), ChannelId.Parse("rate@superk"));
        Assert.Equal(new ChannelId("lum_nue", null), ChannelId.Parse("lum_nue"));
        Assert.Equal("snr@et", ChannelId.Compose("snr", "et"));
        Assert.Throws<ArgumentException>(() => ChannelId.Parse("rate@"));
        Assert.Throws<ArgumentException>(() => ChannelId.Parse("@superk"));
    }

    [Fact]
    public void Detectors_ListEveryInstrumentOnce_WithSources()
    {
        string[] ids =
        {
            "superk", "hyperk", "icecube", "juno", "dune", "aligo", "et", "ce", "gbm", "bat", "spi", "cosi", "nustar",
            "swift-xrt", "einstein-probe", "ultrasat", "chandra", "xmm", "eye", "ztf", "rubin", "spectrograph",
            "vla", "meerkat", "fast", "ska", "parkes", "meerkat-psr",
        };
        Assert.Equal(ids, Detectors.All.Select(d => d.Id));
        foreach (var d in Detectors.All)
        {
            Assert.Same(d, Detectors.Get(d.Id));
            Assert.True(double.IsFinite(d.Threshold));
            Assert.False(string.IsNullOrWhiteSpace(d.ThresholdUnit));
            Assert.False(string.IsNullOrWhiteSpace(d.Source.Citation));
            Assert.StartsWith("signals §", d.Source.ResearchRef);
            // Ruling R24: an estimated threshold is Speculative in data, not only in its citation text.
            if (d.Source.Citation.Contains("APPROX")) Assert.Equal(Confidence.Speculative, d.ThresholdConfidence);
        }
        Assert.Contains("17 MeV", Detectors.Get("superk").Source.Citation);
        Assert.Equal(Confidence.Speculative, Detectors.Get("ultrasat").ThresholdConfidence);
        Assert.DoesNotContain("Lorimer", Detectors.Get("fast").Source.Citation);
        Assert.Throws<KeyNotFoundException>(() => Detectors.Get("hubble"));
    }

    // ---- Kamiokande-II (spec §8, the SN 1987A check) ----

    [Fact]
    public void KamiokandeIIEfficiency_MatchesTheFiorilloFitByHand()
    {
        // Fiorillo et al. 2023 Eq. 8, η = 0.932 / √(1 + (34 / (12 − 7x + x²))²) for x > 4. By hand: x = 7.5: 12 − 52.5 +
        // 56.25 = 15.75, 34/15.75 = 2.15873, √(1 + 4.66012) = 2.37910, η = 0.391745. x = 10: 34/42 = 0.809524,
        // √1.655329 = 1.286596, η = 0.724392. x = 20: 34/272 = 0.125, √1.015625 = 1.007782, η = 0.924803.
        Assert.Equal(0.391745, NeutrinoModel.KamiokandeIIEfficiency(7.5), 5);
        Assert.Equal(0.724392, NeutrinoModel.KamiokandeIIEfficiency(10), 5);
        Assert.Equal(0.924803, NeutrinoModel.KamiokandeIIEfficiency(20), 5);
        Assert.Equal(0, NeutrinoModel.KamiokandeIIEfficiency(4));
        Assert.Equal(0, NeutrinoModel.KamiokandeIIEfficiency(2));
        Assert.Equal(2.14, NeutrinoModel.KamiokandeIIKilotons);
    }

    [Fact]
    public void WaterCherenkovEvents_WithFullEfficiency_MatchSuperKsCumulativeCount_Scaled()
    {
        // With η = 1 the efficiency-weighted count is the plain IBD count: Super-K's 32 kt cumulative over 20 s scaled to
        // the detector's mass. An efficiency below 1 can only lower it.
        var lmc = AtKpc(51.4);
        double superK = Sample(Star12, 20, "cumulative@superk", lmc);
        double full = NeutrinoModel.WaterCherenkovIbdEvents(Star12, lmc, 2.14, _ => 1, 20);
        Assert.Equal(superK * 2.14 / 32, full, superK * 2.14 / 32 * 1e-3);
        double kam = NeutrinoModel.KamiokandeIIEvents(Star12, lmc);
        Assert.InRange(kam, 0.5 * full, full);
        Assert.Equal(0, NeutrinoModel.WaterCherenkovIbdEvents(TestOutcomes.TypeIa(), lmc, 2.14, _ => 1, 20));
    }
}
