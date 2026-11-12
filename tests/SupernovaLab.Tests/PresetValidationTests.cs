using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;

namespace SupernovaLab.Tests;

/// <summary>
/// The real-supernova presets (spec §2.3) and the validations of spec §8 against what was observed. Targets are the values
/// checked in docs/research/presets-verification.md; tolerances are spec §8's.
/// </summary>
public class PresetValidationTests
{
    private const double Day = 86400, Year = 3.156e7;

    private static readonly string[] Ids =
    {
        "sn1987a", "sn1054", "casa", "sn2011fe", "sn2014j", "sn1993j", "sn1998bw",
        "sn2008d", "sn2023ixf", "sn2015bn", "tycho", "kepler", "n6946bh1", "betelgeuse",
    };

    private static readonly Dictionary<string, Outcome> Outcomes =
        PresetCatalog.LoadEmbedded().ToDictionary(p => p.Id, p => TestOutcomes.Engine.Compute(p.Inputs, p.Pins));

    private static Outcome Of(string id) => Outcomes[id];

    private static ObserverSetup ObserverOf(string id) => PresetCatalog.Get(id).Observer;

    // ---------------------------------------------------------------- the catalog

    [Fact]
    public void EveryPresetLoads_WithUniqueIds_InTheListedOrder()
    {
        var presets = PresetCatalog.LoadEmbedded();
        Assert.Equal(Ids, presets.Select(p => p.Id).ToArray());
        Assert.Equal(presets.Count, presets.Select(p => p.Id).Distinct().Count());
        foreach (var p in presets)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Name), p.Id);
            Assert.False(string.IsNullOrWhiteSpace(p.Summary), p.Id);
            Assert.False(string.IsNullOrWhiteSpace(p.WhatWasSeen), p.Id);
            Assert.NotNull(p.ObserverSource);
            Assert.True(p.Observer.DistancePc > 0, p.Id);
            Assert.InRange(p.Observer.Av, 0, 30);
            Assert.Equal(p.Inputs, p.Inputs.Clamped()); // every input inside its slider range
            if (p.Pins is { } pins) Assert.False(string.IsNullOrWhiteSpace(pins.Source.Citation), p.Id);
            foreach (var f in p.Facts)
            {
                Assert.False(string.IsNullOrWhiteSpace(f.Source.Citation), $"{p.Id}/{f.ChannelId}");
                Assert.True(double.IsFinite(f.Value), $"{p.Id}/{f.ChannelId}");
            }
        }
        Assert.Same(presets[0], PresetCatalog.Get("sn1987a"));
        Assert.Throws<KeyNotFoundException>(() => PresetCatalog.Get("sn9999x"));
    }

    public static TheoryData<string, SupernovaType> ExpectedTypes() => new()
    {
        { "sn1987a", SupernovaType.IIP }, { "casa", SupernovaType.IIb }, { "sn2011fe", SupernovaType.Ia },
        { "sn1993j", SupernovaType.IIb }, { "sn1998bw", SupernovaType.IcBL }, { "sn2008d", SupernovaType.Ib },
        { "sn2015bn", SupernovaType.SlsnI }, { "betelgeuse", SupernovaType.IIP },
    };

    [Theory]
    [MemberData(nameof(ExpectedTypes))]
    public void EachPreset_GivesItsObservedType(string id, SupernovaType type)
    {
        Assert.Equal(Fate.Supernova, Of(id).Fate);
        Assert.Equal(type, Of(id).Type);
    }

    [Fact]
    public void SpecialCases_1987AIsNotedPeculiar_1998bwHasItsBurst_N6946BH1FailsToExplode()
    {
        // SN 1987A: the lab's II-P for what astronomers call II-pec, from a blue supergiant.
        Assert.Contains("peculiar", PresetCatalog.Get("sn1987a").Summary);
        Assert.Contains(Of("sn1987a").Notes, n => n.Contains("blue supergiant", StringComparison.Ordinal));
        Assert.True(Of("sn1998bw").HasGrb);
        // N6946-BH1: a failed supernova with no explosion pins to ignore (R44).
        var bh1 = Of("n6946bh1");
        Assert.Equal(Fate.FailedSupernova, bh1.Fate);
        Assert.Equal(RemnantKind.BlackHole, bh1.Remnant.Kind);
        Assert.Null(PresetCatalog.Get("n6946bh1").Pins);
    }

    // ---------------------------------------------------------------- Review Focus 5: editing an input clears the pins

    [Fact]
    public void PinnedPresets_UsePublishedValues_AndEditingAnInputReturnsToTheModel()
    {
        foreach (var p in PresetCatalog.LoadEmbedded())
        {
            Assert.Equal(p.Pins is not null, Of(p.Id).UsesPublishedValues);
            // Changing an input clears the pins (spec §2.3): the lab computes again without them.
            var edited = TestOutcomes.Engine.Compute(p.Inputs with { InitialMass = p.Inputs.InitialMass + 1 }, null);
            Assert.False(edited.UsesPublishedValues, p.Id);
        }
    }

    [Fact]
    public void PublishedValues_ArePinned_WithTheirCitations()
    {
        // Ruling R35: Cas A and SN 1987A ⁴⁴Ti; R36: GRB 980425; R41: the Crab pulsar and the SN 1987A ring.
        Assert.Equal(1.25e-4, Of("casa").Ti44Mass!.Value);
        Assert.Contains("Grefenstette", Of("casa").Ti44Mass!.Source.Citation);
        Assert.Equal(1.5e-4, Of("sn1987a").Ti44Mass!.Value);
        Assert.Contains("Boggs", Of("sn1987a").Ti44Mass!.Source.Citation);
        Assert.Equal(0.078, Of("sn1987a").NickelMass!.Value);
        Assert.True(Of("sn1987a").Remnant.Mass!.Published); // R46: Blinnikov's mass cut
        Assert.Contains("Blinnikov", Of("sn1987a").NickelMass!.Source.Citation);
        Assert.True(Of("sn1987a").HasEquatorialRing);
        Assert.Equal(0.10, Of("sn1987a").Inputs.Opacity);

        var bw = Of("sn1998bw");
        Assert.Equal(8.5e47, bw.GrbIsotropicEnergyErg!.Value);
        Assert.Equal(55, bw.GrbPeakEnergyKeV!.Value);
        Assert.Equal(23.3, bw.GrbT90S!.Value);

        var crab = Of("sn1054");
        Assert.Equal(56, crab.PulsarRadioLuminosityMjyKpc2!.Value);
        Assert.True(crab.Remnant.SpinPeriodS!.Published);
    }

    // ---------------------------------------------------------------- spec §8 validations

    [Fact]
    public void Sn1987A_KamiokandeII_ExpectedCount_Is6To20()
    {
        // Spec §8: Kamiokande-II expected count 6–20 (11 seen; Hirata et al. 1987). The real detector: 2,140 t of water,
        // its published efficiency (Fiorillo et al. 2023, Eq. 8), at 51.4 kpc, over 20 s. Ruling R46: the neutron star is
        // Blinnikov et al. 2000's mass cut, 1.444 M☉ gravitational (binding energy 3.0e53 erg at 12 km, the top of the
        // 2–3e53 the burst implies); the engine's own 1.634 M☉ for the 19 M☉ star would give about 24.
        var o = Of("sn1987a");
        var lmc = ObserverOf("sn1987a");
        Assert.Equal(51_400, lmc.DistancePc);
        Assert.Equal(1.444, o.Remnant.Mass!.Value);
        double n = NeutrinoModel.KamiokandeIIEvents(o, lmc);
        Assert.InRange(n, 6, 20);
        // The efficiency removes events: it is below 1 at every energy.
        Assert.True(n < NeutrinoModel.WaterCherenkovIbdEvents(o, lmc, NeutrinoModel.KamiokandeIIKilotons, _ => 1, 20));
    }

    [Fact]
    public void Sn1987A_PeakV_IsWithinHalfAMagnitudeOf2Point975()
    {
        // Spec §8: peak V of about +2.9 within 0.5 mag. Verified: V = 2.975 at day 84.5 (Hamuy & Suntzeff 1990). The
        // preset's observer is at 51.4 kpc with A_V = 0.47.
        var lc = new LightCurveModel();
        var o = Of("sn1987a");
        double tPeak = lc.PeakTimeS(o);
        Assert.Equal(2.975, lc.Sample(o, ObserverOf("sn1987a"), tPeak, "appV"), 0.5);
        Assert.InRange(tPeak / Day, 84.5 - 20, 84.5 + 20);
    }

    [Fact]
    public void Sn2014J_847keV_OverDays50To100_IsWithinAFactorOf2OfIntegral()
    {
        // Spec §8: INTEGRAL's (2.34 ± 0.74)e-4 photons cm⁻² s⁻¹ averaged over days 50–100 (Churazov et al. 2014).
        double f = new GammaLineModel().AverageFlux(Of("sn2014j"), ObserverOf("sn2014j"), "line847", 50 * Day, 100 * Day);
        Assert.InRange(f, 2.34e-4 / 2, 2.34e-4 * 2);
    }

    [Fact]
    public void Sn2011fe_PeakV_IsWithin0Point3MagOf9Point98()
    {
        // Spec §8: peak apparent B or V within 0.3 mag. Verified: V_max = 9.98 ± 0.02 (Pereira et al. 2013).
        var lc = new LightCurveModel();
        var o = Of("sn2011fe");
        Assert.Equal(9.98, lc.Sample(o, ObserverOf("sn2011fe"), lc.PeakTimeS(o), "appV"), 0.3);
    }

    [Fact]
    public void Crab_PeriodIs33Ms_AndItsSpinDownAgeIsWithin50PercentOfItsTrueAge()
    {
        // Spec §8: P = 33 ms and a characteristic age within 50% of the true age. The preset pins the birth period
        // P0 = √(P² − 2PṖt) = 16.845 ms and B = 3.2e19 √(PṖ) = 3.794e12 G from ATNF's P = 33.3924 ms, Ṗ = 4.20972e-13 at
        // MJD 48442.5, 937 yr after 1054 July 4, so the model's period there is 33.39 ms.
        var o = Of("sn1054");
        var pulsar = new PulsarModel();
        var obs = ObserverOf("sn1054");
        double epoch = 936.97 * 365.25 * Day; // MJD 48442.5 in Julian years after 1054 July 4
        Assert.Equal(0.0333924, pulsar.Sample(o, obs, epoch, "periodS"), 0.0333924 * 0.002);
        Assert.Equal(4.20972e-13, pulsar.Sample(o, obs, epoch, "pdot"), 4.20972e-13 * 0.002);

        // At the Crab's age today, about 970 yr: P ≈ 33.8 ms, and τ_c = P / 2Ṗ ≈ 1,290 yr, 1.33 × the true age.
        double age = 970 * Year;
        double p = pulsar.Sample(o, obs, age, "periodS");
        Assert.Equal(0.033, p, 0.033 * 0.05);
        double tauC = PulsarModel.CharacteristicAgeS(p, pulsar.Sample(o, obs, age, "pdot"));
        Assert.InRange(tauC / age, 0.5, 1.5);
        // Its measured radio luminosity gives back ATNF's 14 mJy at 2.0 kpc.
        Assert.Equal(14, pulsar.Sample(o, obs, age, "fluxMjy"), 1e-9);
    }

    [Fact]
    public void Sn2008D_XrayBreakoutLuminosity_IsWithinAFactorOf3Of6e43()
    {
        // Spec §8 / Soderberg et al. 2008: L_X,p ≈ 6.1e43 erg/s in 0.3–10 keV. The model's 0.3–10 keV luminosity is the
        // swift-xrt flux without dust × 4πD² at the preset's 27 Mpc (z = 0), 1 s after breakout (inside the flash).
        var o = Of("sn2008d");
        var breakout = new ShockBreakoutModel();
        var obs = ObserverOf("sn2008d") with { Av = 0 };
        double t = breakout.BreakoutTimeS(o) + 1;
        double lx = breakout.Sample(o, obs, t, "flux@swift-xrt") * 4 * Math.PI * Math.Pow(obs.LuminosityDistanceCm, 2);
        Assert.InRange(lx, 6.1e43 / 3, 6.1e43 * 3);
    }

    [Fact]
    public void Betelgeuse_FloodsSuperK_AndOutshinesVenus()
    {
        // Spec §8: a Super-K count of 1e6–1e8 and a peak apparent V brighter than −8 (presets §13 [D]: ~1e7 events).
        var o = Of("betelgeuse");
        var obs = ObserverOf("betelgeuse");
        Assert.Equal(168, obs.DistancePc);
        double events = new NeutrinoModel().Sample(o, obs, 20, "cumulative@superk");
        Assert.InRange(events, 1e6, 1e8);
        var lc = new LightCurveModel();
        Assert.True(lc.Sample(o, obs, lc.PeakTimeS(o), "appV") < -8);
    }

    [Fact]
    public void Sn1987A_RingChannel_IsLitByTheRing()
    {
        // Ruling R41: the preset's ring gives the X-ray ring channel; Chandra's plateau is 2.53e36 erg/s (signals §6.3).
        double l = new CsmInteractionModel().Sample(Of("sn1987a"), ObserverOf("sn1987a"), 9500 * Day, "ringLx");
        Assert.InRange(l, 2.4e36, 2.53e36);
    }
}
