using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Tests;

public class GravitationalWaveModelTests
{
    private static readonly GravitationalWaveModel Model = new();
    private static readonly Outcome Star12 = TestOutcomes.Star(12);
    private static readonly Outcome Failed23 = TestOutcomes.Star(23);
    private static readonly ObserverSetup At10Kpc = ObserverSetup.Default;

    private static ObserverSetup AtKpc(double kpc) => new(kpc * 1000, 0, true);

    private static Outcome Rotating(double initialMass, Rotation rotation)
        => TestOutcomes.Engine.Compute(LabInputs.Default with { InitialMass = initialMass, Rotation = rotation });

    private static double Strain(Outcome o, double t, ObserverSetup? obs = null) => Model.Sample(o, obs ?? At10Kpc, t, "strain");

    /// <summary>Strain sampled every <paramref name="step"/> seconds from a to b.</summary>
    private static IEnumerable<(double T, double H)> Series(Outcome o, double a, double b, double step, ObserverSetup? obs = null)
    {
        for (double t = a; t <= b; t += step) yield return (t, Strain(o, t, obs));
    }

    // ---- the brief's cases ----

    [Fact]
    public void PeakStrain_12MsunSlowRotation_At10Kpc_Is1e23To1e21()
    {
        // Signals §2.1: h of about 1e-23 to 2e-21 at 10 kpc; 3D models sit at the low end.
        Assert.Equal(Fate.Supernova, Star12.Fate);
        Assert.Equal(Rotation.Slow, Star12.Inputs.Rotation);
        double peak = Series(Star12, 0, 1.2, 1.0 / 16384).Max(p => Math.Abs(p.H));
        Assert.InRange(peak, 1e-23, 1e-21);
    }

    [Fact]
    public void Strain_ScalesAsOneOverDistance()
    {
        // Signals §0.3: h ∝ 1/D. The realization is the same at every distance, so at 20 kpc h is exactly half.
        var rotating = Rotating(12, Rotation.Fast);
        // The slow rotator is quiet for the first 10 ms (signals §2.1); the fast rotator's bounce spike fills them.
        foreach (var (o, times) in new[] { (Star12, new[] { 0.02, 0.031, 0.25, 0.5, 0.777 }), (rotating, new[] { 0.0003, 0.004 }) })
        foreach (double t in times)
        {
            double h10 = Strain(o, t), h20 = Strain(o, t, AtKpc(20));
            Assert.NotEqual(0, h10);
            Assert.Equal(h10 / 2, h20, Math.Abs(h10) * 1e-12);
        }
    }

    [Fact]
    public void ALigo_DetectsANonRotatingCollapseAt5Kpc_ButNotAt50Kpc()
    {
        // Signals §2.3/§11: non-rotating, aLIGO/Virgo about 5.5 kpc (3-detector network), "about 5-10 kpc".
        var aligo = Detectors.Get("aligo");
        Assert.Equal(DetectionStatus.Detected, Model.Detect(Star12, AtKpc(5), 1.5, aligo).Status);
        Assert.Equal(DetectionStatus.TooFaint, Model.Detect(Star12, AtKpc(50), 1.5, aligo).Status);
    }

    [Fact]
    public void ALigo_DetectsExtremeRotationAt50Kpc()
    {
        // Signals §2.3/§11: rotating bounce about 50 kpc (LMC) and beyond; extreme models further.
        var extreme = Rotating(12, Rotation.Extreme);
        Assert.True(Model.Applies(extreme));
        var aligo = Detectors.Get("aligo");
        Assert.Equal(DetectionStatus.Detected, Model.Detect(extreme, AtKpc(50), 1.5, aligo).Status);
    }

    [Fact]
    public void Spectrogram_InTheFirst50Ms_HasLessPowerAbove1500HzThanAt100To1000Hz()
    {
        foreach (var o in new[] { Star12, Rotating(12, Rotation.Fast), Rotating(12, Rotation.Extreme) })
        {
            // 200 time bins over 2 s (10 ms each), 256 frequency bins over 2,048 Hz (8 Hz each).
            double[,] s = Model.Spectrogram(o, At10Kpc, 200, 256);
            double high = 0, mid = 0;
            for (int i = 0; i < 5; i++)                      // 0-50 ms
            {
                for (int k = 188; k < 256; k++) high += s[i, k]; // 1,504-2,048 Hz
                for (int k = 13; k < 125; k++) mid += s[i, k];   // 104-1,000 Hz
            }
            Assert.True(mid > 0, "the first 50 ms should carry power at 100-1,000 Hz");
            Assert.True(high < mid, $"high {high} should be below mid {mid}");
        }
    }

    [Fact]
    public void TypeIa_IsNotProduced()
    {
        var ia = TestOutcomes.TypeIa();
        Assert.False(Model.Applies(ia));
        foreach (string id in GravitationalWaveModel.DetectorIds)
            Assert.Equal(DetectionStatus.NotProduced, Model.Detect(ia, At10Kpc, 0.5, Detectors.Get(id)).Status);
        Assert.True(double.IsNaN(Strain(ia, 0.5)));
        double[,] s = Model.Spectrogram(ia, At10Kpc, 20, 16);
        foreach (double v in s) Assert.Equal(0, v);
    }

    // ---- formulas against the research's worked examples ----

    [Fact]
    public void Hrss_ForTheWorkedExample_Is2_9e23()
    {
        // Signals §2.4 step 1: E = 2e45 erg, f0 = 800 Hz, D = 10 kpc gives h_rss ≈ 2.9e-23 Hz^-1/2.
        // Hand: G/(π² c³) = 6.674e-8 / (9.8696044 × 2.6944002e31) = 2.509722e-40; × 2e45 = 5.019444e5, √ = 708.481;
        // / (3.0857e22 × 800 = 2.46856e25) = 2.86999e-23.
        double h = GravitationalWaveModel.HrssForEnergy(2e45, 10 * PhysicalConstants.Kpc, 800);
        Assert.Equal(2.86999e-23, h, 2.86999e-23 * 0.002);
    }

    [Fact]
    public void ALigoNoise_FollowsTheResearchLevels_AndThirdGenerationIsLower()
    {
        // Signals §2.4 step 2 [APPROX]: about 4e-24 Hz^-1/2 near 200-300 Hz, about 1e-23 at 1 kHz.
        Assert.Equal(4e-24, GravitationalWaveModel.AmplitudeSpectralDensity("aligo", 250), 4e-24 * 0.01);
        Assert.Equal(1e-23, GravitationalWaveModel.AmplitudeSpectralDensity("aligo", 1000), 1e-23 * 0.01);
        // ET-D "about 10 times lower".
        Assert.Equal(0.1, GravitationalWaveModel.AmplitudeSpectralDensity("et", 800) /
                          GravitationalWaveModel.AmplitudeSpectralDensity("aligo", 800), 1e-12);
        Assert.True(GravitationalWaveModel.AmplitudeSpectralDensity("ce", 800) <
                    GravitationalWaveModel.AmplitudeSpectralDensity("et", 800));
    }

    [Fact]
    public void NarrowBandSnr_ForTheWorkedExample_IsAbout4()
    {
        // Signals §2.4 step 2: ρ ≈ h_rss √(2 / S_n(f0)); the worked example with aLIGO at 800 Hz gives ρ "about 4".
        // Hand, with this model's aLIGO curve: ASD(800) = 4e-24 (800/300)^0.761056 √(1 + (50/800)^4)
        // = 4e-24 × 2.10958 × 1.0000076 = 8.43838e-24; ρ = 2.86999e-23 × 1.414214 / 8.43838e-24 = 4.8099.
        double h = GravitationalWaveModel.HrssForEnergy(2e45, 10 * PhysicalConstants.Kpc, 800);
        double rho = GravitationalWaveModel.NarrowBandSnr(h, "aligo", 800);
        Assert.Equal(4.8099, rho, 4.8099 * 0.005);
    }

    [Fact]
    public void BounceAmplitude_MatchesTheWorkedExample()
    {
        // Signals §2.2: hD ~ (G M)² / (c⁴ R) × T/|W|; M = 0.6 M☉, R = 15 km, T/|W| = 0.05 gives about 260 cm.
        // Hand: G M = 6.674e-8 × 0.6 × 1.989e33 = 7.96475e25; (G M)² = 6.34372e51; c⁴ R = 8.07761e41 × 1.5e6
        // = 1.211642e48; ratio 5,235.6 cm; × 0.05 = 261.8 cm; × 0.09 (Extreme) = 471.2 cm.
        Assert.Equal(261.8, GravitationalWaveModel.BounceStrainDistanceCm(0.6, 15, 0.05), 0.3);

        var fast = Model.BounceAmplitudeCm(Rotating(12, Rotation.Fast));
        var extreme = Model.BounceAmplitudeCm(Rotating(12, Rotation.Extreme));
        Assert.NotNull(fast);
        Assert.NotNull(extreme);
        Assert.Equal(261.8, fast!.Value, 0.3);
        Assert.Equal(471.2, extreme!.Value, 0.5);
        Assert.Equal(Confidence.Speculative, fast.Confidence);
        Assert.Null(Model.BounceAmplitudeCm(Star12));
    }

    [Fact]
    public void RotatingBounce_IsANegativeSpikeOfThePredictedSize()
    {
        // Signals §2.4 step 4: h = −A e^(−t/τ) sin(2π f t), f = 750 Hz (rapid), τ = 4 ms, A = 261.8 cm / 3.0857e22 cm
        // = 8.4843e-21 at 10 kpc. The first extremum: tan(2π f t) = 2π f τ = 18.850, so 2π f t = 1.51783 rad,
        // t = 0.32210 ms; sin = 0.998596, e^(−0.080525) = 0.922632; h = −0.921337 × 8.4843e-21 = −7.8169e-21.
        var fast = Rotating(12, Rotation.Fast);
        var series = Series(fast, 0, 0.002, 1e-6).ToList();
        var min = series.MinBy(p => p.H);
        Assert.Equal(-7.8169e-21, min.H, 7.8169e-21 * 0.005);
        Assert.InRange(min.T, 0.00030, 0.00035);
        // The spike comes first: before it the strain is never positive.
        Assert.All(series.Where(p => p.T < min.T), p => Assert.True(p.H <= 0));
    }

    [Fact]
    public void RmsStrain_InTheFlatPartOfTheTrack_MatchesTheEnergyNormalization()
    {
        // Signals §2.4 step 1: E_GW = (π² c³ / G) D² ∫ f² ⟨h²⟩ dt, default E_GW = 1e45 erg. For an exploding star the
        // shape is: main track f = 400 + 1,556 s Hz (s = t − 0.15 s), w² rising linearly over 50 ms, 1 to s = 0.65,
        // falling linearly to 0 at s = 0.85 (t = 1.0 s); prompt packet 0.5× at 100 Hz (Hann² over 10-50 ms); SASI 0.3× at
        // 150 Hz (Hann² over 0.15-0.40 s). Hand integrals of w² f² dt (Hz² s):
        //   ramp-up 20 [a² s²/2 + 2ab s³/3 + b² s⁴/4] at 0.05 = 20 × 255.6497 = 5,112.99
        //   flat (f(0.65)³ − f(0.05)³) / (3 × 1,556) = (1411.4³ − 477.8³) / 4,668 = 578,942.0
        //   ramp-down 5 [2,967,350.76 v²/2 − 5,360,731.2 v³/3 + 2,421,136 v⁴/4] at 0.2 = 230,100.9
        //   prompt 0.25 × (3/8 × 0.04) × 100² = 37.5; SASI 0.09 × (3/8 × 0.25) × 150² = 189.8
        //   total I = 814,383.2
        // A² = (G/(π² c³)) E / (D² I) = 2.509722e-40 × 1e45 / (9.521544e44 × 814,383.2) = 3.23660e-46; A = 1.79905e-23.
        double rms = Model.RmsStrain(Star12, At10Kpc, 0.5);
        Assert.Equal(1.79905e-23, rms, 1.79905e-23 * 0.005);

        // The realization honours it on average: ∫ h² dt over 0-1 s should be A² × (0.025 + 0.6 + 0.1 + 0.25 × 0.015 +
        // 0.09 × 0.09375) = A² × 0.7371875 = 2.38598e-46 (signals §2.4 step 3: "∫ h² dt equals h_rss²"). A fixed
        // realization with ~200 independent cells scatters by ~10%; 25% is allowed.
        const double dt = 1.0 / 16384;
        double sum = Series(Star12, 0, 1.0, dt).Sum(p => p.H * p.H) * dt;
        Assert.InRange(sum, 2.38598e-46 * 0.75, 2.38598e-46 * 1.25);
    }

    [Fact]
    public void Spectrogram_FollowsTheRisingTrack_AndPeaksAtOne()
    {
        // Signals §2.1: 1,100 Hz at 600 ms. Bin 59 covers 0.59-0.60 s, where f = 400 + 1,556 (t − 0.15) averages 1,100 Hz.
        double[,] s = Model.Spectrogram(Star12, At10Kpc, 200, 256);
        Assert.Equal(1, s.Cast<double>().Max(), 1e-12);
        int best = Enumerable.Range(0, 256).MaxBy(k => s[59, k]);
        Assert.InRange((best + 0.5) * 8, 1070, 1130);
        Assert.All(s.Cast<double>(), v => Assert.True(v >= 0 && double.IsFinite(v)));
    }

    [Fact]
    public void TheWholeModel_IsSpeculative_AndSaysTheEnergyIsADefault()
    {
        // Ruling R27: the strain rests on [APPROX] track, ASDs and envelope choices. Signals §2.1/§12.2: E_GW spans about
        // 3 orders of magnitude across 3D models, and 1e45 erg is the recipe's default.
        Assert.Equal(Confidence.Speculative, Model.Confidence);
        Assert.Contains(Model.Sources, s => s.Citation.Contains("1e45 erg") && s.Citation.Contains("3 orders of magnitude"));
        var e = Model.StochasticEnergyErg(Star12);
        Assert.NotNull(e);
        Assert.Equal(Confidence.Speculative, e!.Confidence);
        Assert.Equal(1e45, e.Value, 1e45 * 1e-3);
        Assert.Null(Model.StochasticEnergyErg(TestOutcomes.TypeIa()));
    }

    /// <summary>A black hole after a successful explosion (fallback or collapsar), formed at t_BH.</summary>
    private static Outcome ExplosionThenBlackHole(double tBh)
        => TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.05, 5e13, TestOutcomes.BlackHole(5, tBh));

    [Fact]
    public void FallbackBlackHole_RadiatesLikeTheExplodingStar_ThenRingsDownAtTbh()
    {
        // Ruling R27: Fate.Supernova + black hole uses the exploding star's shape (tapered, ends at 1.0 s), cut at
        // min(t_BH, 1.0 s), with the ring-down at t_BH. With t_BH = 3.0 s (RemnantModel's fallback value) the track ends
        // before t_BH, so the strain matches the neutron-star case and E_GW is the default 1e45 erg.
        var fallback = ExplosionThenBlackHole(3.0);
        Assert.Equal(Fate.Supernova, fallback.Fate);
        foreach (double t in new[] { 0.02, 0.3, 0.6, 0.95 })
            Assert.Equal(Strain(Star12, t), Strain(fallback, t));
        Assert.Equal(1e45, Model.StochasticEnergyErg(fallback)!.Value, 1e45 * 1e-3);
        Assert.Equal(0, Strain(fallback, 1.5));
        Assert.Contains(Series(fallback, 3.0, 3.002, 1e-5), p => p.H != 0);
        // The modelled range ends with the 2 ms ring-down, so later times are outside it (NaN, the contract).
        Assert.Equal(3.002, Model.ValidRange(fallback).EndS, 1e-12);
        Assert.True(double.IsNaN(Strain(fallback, 3.01)));
    }

    [Fact]
    public void EarlyCollapsarBlackHole_CutsTheExplodingShapeAtTbh()
    {
        // t_BH = 0.5 s: the exploding shape up to 0.5 s. Hand integrals of w² f² dt (Hz² s): ramp-up 5,112.99; flat from
        // s = 0.05 to 0.35, (944.6³ − 477.8³) / 4,668 = (842,837,448.5 − 109,078,319) / 4,668 = 157,189.2; prompt 37.5;
        // SASI (ends at 0.40 s) 189.84; total 162,529.5. E = 1e45 × 162,529.5 / 814,383.2 = 1.99569e44 erg.
        var collapsar = ExplosionThenBlackHole(0.5);
        Assert.Equal(1.99569e44, Model.StochasticEnergyErg(collapsar)!.Value, 1.99569e44 * 0.005);
        Assert.Equal(Strain(Star12, 0.45), Strain(collapsar, 0.45));
        Assert.Equal(0, Strain(collapsar, 0.51));
    }

    [Fact]
    public void LateFailedSupernova_StaysInsideThe2DEnergyRange()
    {
        // A failed supernova accretes at full strength until t_BH; the latest t_BH the engine produces is 4.7 s. Hand:
        // ramp-up 5,112.99; flat from s = 0.05 to the 2 kHz cap at s = 1.028278, (2000³ − 477.8³) / 4,668 = 1,690,428.8;
        // capped 2000² × (4.7 − 1.178278) = 14,086,888; prompt 37.5; SASI 189.84; total 15,782,657.1.
        // E = 1e45 × 15,782,657.1 / 814,383.2 = 1.93797e46 erg, inside signals §2.1's 2D range (3.2e42 to 2.0e46).
        var failed = TestOutcomes.Custom(SupernovaType.None, null, null, null, 5e13, TestOutcomes.BlackHole(10, 4.7),
            Fate.FailedSupernova);
        double e = Model.StochasticEnergyErg(failed)!.Value;
        Assert.Equal(1.93797e46, e, 1.93797e46 * 0.005);
        Assert.True(e <= 2.0e46);
    }

    [Fact]
    public void IsDeterministic_AcrossInstances()
    {
        var other = new GravitationalWaveModel();
        foreach (double t in new[] { 0.03, 0.2, 0.6, 0.9 })
            Assert.Equal(Strain(Star12, t), other.Sample(Star12, At10Kpc, t, "strain"));
        Assert.Equal(Model.Spectrogram(Star12, At10Kpc, 40, 32), other.Spectrogram(Star12, At10Kpc, 40, 32));
    }

    [Fact]
    public void BlackHole_CutsTheStrainOffAtTbh()
    {
        Assert.Equal(Fate.FailedSupernova, Failed23.Fate);
        double tBh = Failed23.Remnant.FormationTimeS!.Value;
        Assert.True(tBh > 0.3, $"t_BH {tBh}");
        Assert.Contains(Series(Failed23, tBh - 0.02, tBh - 0.001, 1e-4), p => p.H != 0);
        // The ring-down (signals §2.4 step 3) fills the 2 ms after t_BH.
        Assert.Contains(Series(Failed23, tBh, tBh + 0.002, 1e-5), p => p.H != 0);
        foreach (double t in new[] { tBh + 0.01, tBh + 0.1 })
            if (t <= Model.ValidRange(Failed23).EndS) Assert.Equal(0, Strain(Failed23, t));
        // The SNR stops growing once the black hole has formed.
        double after = Model.Sample(Failed23, At10Kpc, tBh + 0.01, "snr@aligo");
        double end = Model.Sample(Failed23, At10Kpc, Model.ValidRange(Failed23).EndS, "snr@aligo");
        Assert.Equal(after, end, after * 1e-9);
    }

    [Fact]
    public void Snr_IsCumulative_AndScalesAsOneOverDistance()
    {
        double previous = 0;
        foreach (double t in new[] { 0.0, 0.03, 0.1, 0.3, 0.6, 1.0, 1.5 })
        {
            double snr = Model.Sample(Star12, At10Kpc, t, "snr@aligo");
            Assert.True(snr >= previous, $"SNR fell at {t} s");
            previous = snr;
        }
        Assert.Equal(previous / 2, Model.Sample(Star12, AtKpc(20), 1.5, "snr@aligo"), previous * 1e-9);
    }

    [Fact]
    public void ThirdGenerationDetectors_ReachTheirResearchRanges()
    {
        // Signals §2.3/§11: ET about 25-140 kpc for SNR 8; CE (supernova-optimized) about 100 kpc.
        var et = Detectors.Get("et");
        var ce = Detectors.Get("ce");
        Assert.Equal(DetectionStatus.Detected, Model.Detect(Star12, AtKpc(25), 1.5, et).Status);
        Assert.Equal(DetectionStatus.TooFaint, Model.Detect(Star12, AtKpc(150), 1.5, et).Status);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(Star12, AtKpc(70), 1.5, ce).Status);
        Assert.Equal(DetectionStatus.TooFaint, Model.Detect(Star12, AtKpc(200), 1.5, ce).Status);
    }

    [Fact]
    public void Detect_FollowsTheStateMachine()
    {
        var aligo = Detectors.Get("aligo");
        Assert.Equal(DetectionStatus.NotYet, Model.Detect(Star12, AtKpc(1), -1, aligo).Status);
        // At 1 kpc the main track pushes the SNR past 8 well before it ends; nothing has arrived by 5 ms.
        Assert.Equal(DetectionStatus.NotYet, Model.Detect(Star12, AtKpc(1), 0.005, aligo).Status);
        Assert.Equal(DetectionStatus.Detected, Model.Detect(Star12, AtKpc(1), 0.5, aligo).Status);
        // After the modelled range (2 s) the badge reports the verdict as history, like the neutrino burst's.
        var over = Model.Detect(Star12, AtKpc(1), 3, aligo);
        Assert.Equal(DetectionStatus.NotModeled, over.Status);
        Assert.Contains("detected", over.Reason);
        Assert.Equal(DetectionStatus.NotModeled, Model.Detect(Star12, AtKpc(1), 0.5, Detectors.Get("superk")).Status);
    }
}
