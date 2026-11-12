using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

/// <summary>Pulsar spin-down, radio pulses and beaming (signals §8.1–8.3, §11; ruling R40).</summary>
public class PulsarModelTests
{
    private static readonly PulsarModel Model = new();
    private const double Year = 3.156e7;

    private static Outcome WithRemnant(Remnant r) => TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.03, 3.5e13, r);

    /// <summary>
    /// A Crab-like pulsar: P = 33 ms (the brief's value; presets §2 gives 33.5 ms, signals §8.2 33.4 ms) and
    /// B = 3.8e12 G (presets §2, signals §8.2).
    /// </summary>
    private static Outcome CrabLike() => WithRemnant(TestOutcomes.Pulsar(0.033, 3.8e12));

    [Fact]
    public void CrabLikePulsar_SpinDownPowerAndCharacteristicAge()
    {
        // §8.1: Ṗ = B² / (3.2e19)² / P = 1.444e25 / 1.024e39 / 0.033 = 4.27320e-13; Ė = 4π² I Ṗ / P³ = 39.4784 × 1e45 ×
        // 4.27320e-13 / 3.5937e-5 = 4.69430e38 erg/s, within 10% of the Crab's 4.5e38 (§8.2). τ_c = P / (2Ṗ) =
        // 3.86127e10 s = 1223.47 yr, within 50% of the true age 970 yr (presets §2: "~1,260 yr vs. true age ~970 yr").
        double pdot = PulsarModel.PeriodDerivative(0.033, 3.8e12);
        Assert.Equal(4.27320e-13, pdot, 0.00001e-13);
        double edot = PulsarModel.SpinDownLuminosity(0.033, pdot);
        Assert.Equal(4.69430e38, edot, 0.0001e38);
        Assert.InRange(edot, 0.9 * 4.5e38, 1.1 * 4.5e38);
        double age = PulsarModel.CharacteristicAgeS(0.033, pdot) / Year;
        Assert.Equal(1223.47, age, 0.01);
        Assert.InRange(age, 0.5 * 970, 1.5 * 970);
    }

    [Fact]
    public void CrabLikePulsar_SpinsDownAsADipole()
    {
        // n = 3 (§8.1): τ0 = 1223.47 yr; at 10 yr, x = 10 / 1223.47 = 0.00817348, P = 0.033 √(1 + x) = 0.0331346 s and
        // Ė = 4.69430e38 / (1 + x)² = 4.61850e38 erg/s.
        var o = CrabLike();
        Assert.Equal(0.0331346, Model.Sample(o, ObserverSetup.Default, 10 * Year, "periodS"), 7);
        Assert.Equal(4.61850e38, Model.Sample(o, ObserverSetup.Default, 10 * Year, "edot"), 0.0002e38);
        Assert.InRange(Model.Sample(o, ObserverSetup.Default, 10 * Year, "edot"), 0.9 * 4.5e38, 1.1 * 4.5e38);
    }

    [Fact]
    public void BeamingFraction_FollowsTaurisManchesterAndItsExtension()
    {
        // §8.3: Crab P = 0.0334 s gives about 0.51 (extension, −0.255 log P + 0.135 = 0.511445); P = 1 s gives 0.09 × 1 +
        // 0.03 = 0.12; the extension is capped at 0.9.
        Assert.Equal(0.511445, PulsarModel.BeamingFraction(0.0334), 1e-6);
        Assert.Equal(0.12, PulsarModel.BeamingFraction(1), 9);
        Assert.Equal(0.9, PulsarModel.BeamingFraction(1e-4), 9);
    }

    [Fact]
    public void RadioFlux_IsTheMedianLuminosityOverDistanceSquared_AndDetectorsUseRulingR40()
    {
        // §8.3: log-normal L_1400 with ⟨log10 L⟩ = −1.1 (mJy kpc²), median 0.0794328 mJy kpc². At 2 kpc: 0.0198582 mJy.
        var o = CrabLike();
        var obs = new ObserverSetup(2000, 0, true);
        double t = 20 * Year;
        Assert.Equal(0.0198582, Model.Sample(o, obs, t, "fluxMjy"), 1e-6);
        // R40: FAST 0.01 mJy detects it; Parkes and MeerKAT pulsar searches (0.1 mJy) do not.
        Assert.Equal(1.98582, Model.Sample(o, obs, t, "detect@fast"), 1e-4);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, obs, t, Detectors.Get("fast")).Status);
        Assert.Equal(DetectionStatus.TooFaint, Model.Detect(o, obs, t, Detectors.Get("parkes")).Status);
        Assert.Equal(DetectionStatus.TooFaint, Model.Detect(o, obs, t, Detectors.Get("meerkat-psr")).Status);
        // At 500 pc: 0.317731 mJy, enough for Parkes.
        Assert.Equal(DetectionStatus.Detected, Model.Detect(o, new ObserverSetup(500, 0, true), t, Detectors.Get("parkes")).Status);
        // The continuum VLA entry is not a pulsar search (R40).
        Assert.Equal(DetectionStatus.NotModeled, Model.Detect(o, obs, t, Detectors.Get("vla")).Status);
    }

    [Fact]
    public void Pulses_AreHiddenUntilTheEjectaClear_AndOnlySeenInsideTheBeam()
    {
        var o = CrabLike();
        // §8.3 [APPROX]: "pulses visible" after about 10–30 yr; the model starts at 10 yr.
        Assert.Equal(10 * Year, Model.EmergenceTimeS(o));
        Assert.Equal((10 * Year, 1000 * Year), Model.ValidRange(o));
        Assert.True(double.IsNaN(Model.Sample(o, ObserverSetup.Default, 5 * Year, "periodS")));
        Assert.Equal(DetectionStatus.NotYet, Model.Detect(o, ObserverSetup.Default, 5 * Year, Detectors.Get("fast")).Status);
        // The brief: NotPointedAtUs when the beam misses Earth; no flux. No dice are rolled in the model.
        var away = new ObserverSetup(2000, 0, false);
        var d = Model.Detect(o, away, 20 * Year, Detectors.Get("fast"));
        Assert.Equal(DetectionStatus.NotPointedAtUs, d.Status);
        Assert.Contains("51%", d.Reason);
        Assert.True(double.IsNaN(Model.Sample(o, away, 20 * Year, "fluxMjy")));
    }

    [Fact]
    public void PulseTimes_AreOnePeriodApart()
    {
        // A typical neutron star (P0 = 0.1 s, B = 3e12 G): Ṗ0 = 9e24 / 1.024e39 / 0.1 = 8.78906e-14, τ0 = 0.1 / (2Ṗ0) =
        // 5.68889e11 s; at 20 yr, x = 6.312e8 / 5.68889e11 = 1.10953e-3, P = 0.1 √(1 + x) = 0.10005546 s. The phase
        // φ = (2τ0/P0)(√(1 + x) − 1) is 6310250130.32 at 20 yr and 6310250140.31 one second later: exactly 10 pulses.
        var o = WithRemnant(TestOutcomes.Pulsar(0.1, 3e12));
        double start = 20 * Year;
        var pulses = Model.PulseTimes(o, start, 1.0);
        Assert.Equal(10, pulses.Count);
        Assert.All(pulses, t => Assert.InRange(t, start, start + 1.0));
        // Times near 6.3e8 s carry about 1e-7 s of double-precision rounding, so the spacing is checked to 1 µs.
        for (int i = 1; i < pulses.Count; i++) Assert.Equal(0.10005546, pulses[i] - pulses[i - 1], 1e-6);
        Assert.Empty(Model.PulseTimes(o, 5 * Year, 1.0)); // wholly before the ejecta clear
        // Review M6: a window straddling the start (or end) is clipped to the valid part. Phases 3155562411.29 →
        // 3155562416.29 over [10 yr, 10 yr + 0.5 s]: 5 pulses; 311340259683.96 → 311340259688.83 over [1000 yr − 0.5 s,
        // 1000 yr]: 5 pulses.
        var clipped = Model.PulseTimes(o, 10 * Year - 0.5, 1.0);
        Assert.Equal(5, clipped.Count);
        Assert.All(clipped, t => Assert.InRange(t, 10 * Year, 10 * Year + 0.5));
        Assert.Equal(5, Model.PulseTimes(o, 1000 * Year - 0.5, 1.0).Count);
    }

    [Fact]
    public void Magnetar_SpinsDownOnTheEngineTimescale()
    {
        // A magnetar's spin-down uses Kasen & Bildsten's t_p (the engine's and the light curve's): P0 = 2 ms,
        // B = 1e14 G gives t_p = 1.3 yr × 0.2² = 0.052 yr = 1.64112e6 s; at 100 yr P = 0.002 √(1 + 100/0.052) = 0.0877286 s.
        var o = WithRemnant(TestOutcomes.Pulsar(0.002, 1e14, RemnantKind.Magnetar));
        Assert.Equal(0.0877286, Model.Sample(o, ObserverSetup.Default, 100 * Year, "periodS"), 6);
        // Review M7 [APPROX]: most known magnetars are radio-quiet, and the luminosity function is fitted to ordinary pulsars.
        Assert.Contains("radio-quiet", Model.Detect(o, ObserverSetup.Default, 100 * Year, Detectors.Get("fast")).Reason);
        Assert.DoesNotContain("radio-quiet", Model.Detect(CrabLike(), ObserverSetup.Default, 100 * Year, Detectors.Get("fast")).Reason);
    }

    [Fact]
    public void BlackHolesAndTypeIa_HaveNoPulsar()
    {
        var bh = TestOutcomes.Custom(SupernovaType.Ic, 1e51, 3, 0.1, 1e11, TestOutcomes.BlackHole(5, 3));
        Assert.False(Model.Applies(bh));
        Assert.Equal(DetectionStatus.NotProduced, Model.Detect(bh, ObserverSetup.Default, 100 * Year, Detectors.Get("fast")).Status);
        Assert.False(Model.Applies(TestOutcomes.TypeIa()));
        Assert.False(Model.Applies(TestOutcomes.Star(23)));
        Assert.True(double.IsNaN(Model.EmergenceTimeS(bh)));
    }

    // ---------------------------------------------------------------- ruling R41: a measured radio luminosity

    [Fact]
    public void PinnedRadioLuminosity_ReplacesTheMedian()
    {
        // The Crab: L_1400 = 56 mJy kpc² (ATNF S1400 = 14 mJy at 2.0 kpc). At 2 kpc: 56 / 2² = 14 mJy; FAST's 0.01 mJy limit
        // is passed 1,400 times over.
        var source = new Source("Crab (test)", "tests");
        var o = CrabLike() with { PulsarRadioLuminosityMjyKpc2 = new(56, "mJy kpc²", Confidence.ModelDependent, source) };
        var obs = new ObserverSetup(2000, 0, true);
        Assert.Equal(14, Model.Sample(o, obs, 100 * Year, "fluxMjy"), 1e-9);
        Assert.Equal(1400, Model.Sample(o, obs, 100 * Year, "detect@fast"), 1e-6);
        var d = Model.Detect(o, obs, 100 * Year, Detectors.Get("parkes"));
        Assert.Equal(DetectionStatus.Detected, d.Status);
        Assert.Contains("measured radio luminosity", d.Reason);
        // Without the pin, the median 10^-1.1 = 0.0794328 mJy kpc² gives 0.0198582 mJy at 2 kpc.
        Assert.Equal(0.0198582, Model.Sample(CrabLike(), obs, 100 * Year, "fluxMjy"), 1e-7);
    }
}
