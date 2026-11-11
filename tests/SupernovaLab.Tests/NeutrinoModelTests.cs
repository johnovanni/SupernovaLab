using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;

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
    public void SuperK_AtM31_FollowsTheTwoEventRule()
    {
        var m31 = AtKpc(770);
        double n = Sample(Star12, 20, "cumulative@superk", m31);
        var expected = n >= 2 ? DetectionStatus.Detected : DetectionStatus.TooFaint;
        Assert.Equal(expected, Model.Detect(Star12, m31, 20, Detectors.Get("superk")).Status);
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
            else Assert.Equal(DetectionStatus.Detected, status);
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
            "vla", "meerkat", "fast", "ska",
        };
        Assert.Equal(ids, Detectors.All.Select(d => d.Id));
        foreach (var d in Detectors.All)
        {
            Assert.Same(d, Detectors.Get(d.Id));
            Assert.True(double.IsFinite(d.Threshold));
            Assert.False(string.IsNullOrWhiteSpace(d.ThresholdUnit));
            Assert.False(string.IsNullOrWhiteSpace(d.Source.Citation));
            Assert.StartsWith("signals §", d.Source.ResearchRef);
        }
        Assert.Throws<KeyNotFoundException>(() => Detectors.Get("hubble"));
    }
}
