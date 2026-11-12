using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;

namespace SupernovaLab.Tests;

/// <summary>Pulsar spin-down, radio pulses and beaming (signals §8.1–8.3, §11; ruling R40).</summary>
public class PulsarModelTests
{
    private static readonly PulsarModel Model = new();
    private const double Year = 3.156e7;

    private static Outcome WithRemnant(Remnant r) => TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.03, 3.5e13, r);

    /// <summary>The Crab pulsar's period and field (presets §2: P ≈ 33 ms, B ≈ 3.8e12 G).</summary>
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
        // A typical neutron star (P0 = 0.1 s, B = 3e12 G): in a 1 s window at 20 yr, about 10 pulses, each one period after
        // the last.
        var o = WithRemnant(TestOutcomes.Pulsar(0.1, 3e12));
        double start = 20 * Year, p = Model.Sample(o, ObserverSetup.Default, start, "periodS");
        var pulses = Model.PulseTimes(o, start, 1.0);
        Assert.InRange(pulses.Count, 9, 11);
        Assert.All(pulses, t => Assert.InRange(t, start, start + 1.0));
        // Times near 6.3e8 s carry about 1e-7 s of double-precision rounding, so the spacing is checked to 1 µs.
        for (int i = 1; i < pulses.Count; i++) Assert.Equal(p, pulses[i] - pulses[i - 1], 1e-6);
        Assert.Empty(Model.PulseTimes(o, 5 * Year, 1.0)); // before the ejecta clear
    }

    [Fact]
    public void Magnetar_SpinsDownOnTheEngineTimescale()
    {
        // A magnetar's spin-down uses Kasen & Bildsten's t_p (the engine's and the light curve's): P0 = 2 ms,
        // B = 1e14 G gives t_p = 1.3 yr × 0.2² = 0.052 yr = 1.64112e6 s; at 100 yr P = 0.002 √(1 + 100/0.052) = 0.0877286 s.
        var o = WithRemnant(TestOutcomes.Pulsar(0.002, 1e14, RemnantKind.Magnetar));
        Assert.Equal(0.0877286, Model.Sample(o, ObserverSetup.Default, 100 * Year, "periodS"), 6);
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
}
