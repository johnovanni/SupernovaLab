using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;
using static SupernovaLab.Tests.CompactResearch;

namespace SupernovaLab.Tests;

/// <summary>
/// Plan Task 9: the core's first second (spec §3; compact §C1, §C2): the tracks inside the research's ranges, the failed track ending
/// exactly when the neutrino signal stops (criterion 4), the tracks of the other black-hole outcomes (spec §3.1 item 6), the
/// proto-neutron star's brightness on the main view's rule, and what the Close-up shows in the PreCollapse and FirstSecond eras.
/// </summary>
public class CoreCollapseHistoryTests
{
    private static readonly NeutrinoModel Neutrinos = new();

    private static Outcome Preset(string id)
    {
        var p = PresetCatalog.Get(id);
        return TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
    }

    private static Outcome Lab(LabInputs x) => TestOutcomes.Engine.Compute(x);

    private static Outcome Default => Lab(LabInputs.Default);

    /// <summary>NGC 6946-BH1, the observed failed supernova (t_BH 1.21 s in the lab).</summary>
    private static Outcome FailedSn => Preset("n6946bh1");

    /// <summary>A 100 M☉ star at Z = 0.01 Z☉: pulsational pair instability, then a black hole.</summary>
    private static Outcome Ppisn => Lab(LabInputs.Default with { InitialMass = 100, Metallicity = 0.01 });

    /// <summary>A 25 M☉ star, strong field and extreme rotation at solar metal content (no collapsar): a magnetorotational explosion.</summary>
    private static Outcome Magnetorotational => Lab(LabInputs.Default with
    {
        InitialMass = 25, Rotation = Rotation.Extreme, Field = CoreField.Strong,
    });

    /// <summary>A hand-built explosion that leaves a black hole by fallback at the lab's 3.0 s.</summary>
    private static Outcome Fallback => TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.07, 3e13,
        TestOutcomes.BlackHole(3, 3.0), Fate.Supernova);

    /// <summary>A failed supernova whose black hole forms early (O'Connor &amp; Ott's shortest, 0.23 s): the monotone join.</summary>
    private static Outcome EarlyFailed => TestOutcomes.Custom(SupernovaType.None, null, null, null, 1e13,
        TestOutcomes.BlackHole(10, 0.23), Fate.FailedSupernova);

    private static double NeutrinoTotal(Outcome o, double t)
        => Neutrinos.Sample(o, ObserverSetup.Default, t, "lum_nue") + Neutrinos.Sample(o, ObserverSetup.Default, t, "lum_anue")
           + 4 * Neutrinos.Sample(o, ObserverSetup.Default, t, "lum_nux");

    public static TheoryData<string> CollapseOutcomes => new()
    {
        "default", "sn1987a", "sn1054", "casa", "sn2015bn", "n6946bh1", "sn1998bw", "ppisn", "mr", "fallback", "early-failed", "m15",
    };

    private static Outcome Named(string name) => name switch
    {
        "default" => Default,
        "ppisn" => Ppisn,
        "mr" => Magnetorotational,
        "fallback" => Fallback,
        "early-failed" => EarlyFailed,
        "m15" => TestOutcomes.Star(15),   // fails to explode in the lab, t_BH 3.5 s
        _ => Preset(name),
    };

    private static double Tbh(Outcome o) => o.Remnant.FormationTimeS?.Value ?? double.NaN;

    // ---- Brief tests (plan Task 9, Step 1) ----

    [Theory, MemberData(nameof(CollapseOutcomes))]
    public void Tracks_InsideResearchRanges(string name)
    {
        var o = Named(name);
        var track = CoreCollapseHistory.TrackFor(o)!.Value;
        double tbh = Tbh(o);
        int rows = 0;
        foreach (var (t, lo, hi) in C1.ShockTable(track, tbh))
        {
            Assert.InRange(CoreCollapseHistory.At(o, t).ShockRadiusKm, lo, hi);
            rows++;
        }
        Assert.True(rows >= 5, $"{name}: {rows} shock rows checked");
        foreach (var (t, lo, hi) in C1.ProtoNsTable(track, tbh))
            Assert.InRange(CoreCollapseHistory.At(o, t).ProtoNsRadiusKm, lo, hi);
        foreach (var (t, lo, hi) in C1.GainTable(track, tbh))
            Assert.InRange(CoreCollapseHistory.At(o, t).GainRadiusKm, lo, hi);
    }

    [Fact]
    public void Failed_TrackEndsExactlyAtFormationTime()   // criterion 4
    {
        double tbh = FailedSn.Remnant.FormationTimeS!.Value;
        // compact §C1 App: the collapse is drawn over the last 1 ms. The plan's tbh × 0.999 is 1.2 ms before t_BH = 1.21 s, still
        // stalled, so the test looks half a millisecond before (the App block wins over the plan's example numbers).
        Assert.Equal(CorePhase.Stalled, CoreCollapseHistory.At(FailedSn, tbh - 1.5 * C1.CollapseS).Phase);
        Assert.Equal(CorePhase.Collapsing, CoreCollapseHistory.At(FailedSn, tbh - 0.5 * C1.CollapseS).Phase);
        Assert.Equal(CorePhase.BlackHole, CoreCollapseHistory.At(FailedSn, tbh).Phase);
        Assert.Equal(CorePhase.BlackHole, CoreCollapseHistory.At(FailedSn, tbh * 1.001).Phase);
        Assert.Equal(0, NeutrinoTotal(FailedSn, tbh * 1.001));
        Assert.True(NeutrinoTotal(FailedSn, tbh * 0.999) > 0);
        Assert.Equal(tbh, CoreCollapseHistory.StageEndS(FailedSn));
    }

    [Fact]
    public void Collapsar_UsesFailedTrack() => Assert.Equal(CoreTrack.Failed, CoreCollapseHistory.TrackFor(Preset("sn1998bw")));

    [Fact]
    public void PulsationalPairInstability_UsesFailedTrack()
    {
        Assert.Equal(Fate.PulsationalPairInstability, Ppisn.Fate);
        Assert.Equal(CoreTrack.Failed, CoreCollapseHistory.TrackFor(Ppisn));
        Assert.Equal(CorePhase.BlackHole, CoreCollapseHistory.At(Ppisn, Tbh(Ppisn) * 1.001).Phase);
    }

    [Fact]
    public void Magnetorotational_UsesExplodingTrack_WithNote()   // Strong B + Extreme rotation, no collapsar (solar metal content)
    {
        var o = Magnetorotational;
        Assert.Equal(RemnantKind.BlackHole, o.Remnant.Kind);
        Assert.False(o.HasGrb);
        Assert.Equal(CoreTrack.Magnetorotational, CoreCollapseHistory.TrackFor(o));
        foreach (double t in new[] { 0.01, 0.1, 0.5, 0.9 })
            Assert.Equal(CoreCollapseHistory.At(Default, t).ShockRadiusKm, CoreCollapseHistory.At(o, t).ShockRadiusKm, 9);
        Assert.Equal(CorePhase.Revived, CoreCollapseHistory.At(o, 0.5).Phase);
        Assert.Equal("A magnetar-driven explosion revives the shock differently (Speculative)", CoreCollapseHistory.MagnetorotationalNote);
        var scene = new CloseUpScene(o, ObserverSetup.Default, 1);
        Assert.Contains(CoreCollapseHistory.MagnetorotationalNote, scene.At(0.5).Notes);
        Assert.DoesNotContain(CoreCollapseHistory.MagnetorotationalNote, new CloseUpScene(Default, ObserverSetup.Default, 1).At(0.5).Notes);
    }

    [Fact]
    public void Exploding_ShockStallsThenRevives_ProtoNsShrinksTo12km()
    {
        Assert.Equal(CorePhase.Stalled, CoreCollapseHistory.At(Default, 0.05).Phase);
        Assert.Equal(CorePhase.Revived, CoreCollapseHistory.At(Default, 0.8).Phase);
        Assert.InRange(CoreCollapseHistory.At(Default, 60).ProtoNsRadiusKm, C2.At60Low, C2.At60High);
        Assert.Equal(CoreTrack.Exploding, CoreCollapseHistory.TrackFor(Default));
    }

    [Fact]
    public void ProtoNsBrightness_FollowsNeutrinoTotal()
    {
        var o = Default;
        double peak = 0;
        for (int i = 0; i <= 4000; i++) peak = Math.Max(peak, NeutrinoTotal(o, Math.Pow(10, -3 + 4.0 * i / 4000)));
        var scene = new CloseUpScene(o, ObserverSetup.Default, 1);
        foreach (double t in new[] { 0.002, 0.005, 0.05, 0.3, 0.9 })
        {
            float expected = ExplosionScene.LogBrightness(NeutrinoTotal(o, t), peak);
            Assert.Equal(expected, scene.At(t).ProtoNsBrightness, 2);
        }
        Assert.True(scene.At(0.9).ProtoNsBrightness < scene.At(0.005).ProtoNsBrightness);
        Assert.Equal(NeutrinoTotal(o, 0.3), CoreCollapseHistory.NeutrinoLuminosityErgS(o, 0.3), 6);
    }

    [Fact]
    public void NoCollapse_NoTrack()
    {
        Assert.Null(CoreCollapseHistory.TrackFor(Preset("sn2011fe")));
        Assert.Null(CoreCollapseHistory.TrackFor(Lab(LabInputs.Default with { InitialMass = 5 })));      // a white dwarf
        Assert.Null(CoreCollapseHistory.TrackFor(Lab(LabInputs.Default with { InitialMass = 0.5 })));    // not dead yet
        Assert.Null(CoreCollapseHistory.TrackFor(Lab(LabInputs.Default with { InitialMass = 150, Metallicity = 0.01 })));   // PISN
        Assert.True(double.IsNaN(CoreCollapseHistory.StageEndS(Preset("sn2011fe"))));
    }

    // ---- Further checks: compact §C1 App rules ----

    [Fact]
    public void Exploding_StageEndsWhenTheTypicalShockPasses3000km()
    {
        Assert.Equal(C1.ExplodingStageEndS, CoreCollapseHistory.StageEndS(Default));
        Assert.Equal(C1.StageEndShockKm, CoreCollapseHistory.At(Default, C1.ExplodingStageEndS).ShockRadiusKm, 6);
        Assert.Equal(CorePhase.Cooling, CoreCollapseHistory.At(Default, 2).Phase);
        Assert.True(double.IsNaN(CoreCollapseHistory.At(Default, 2).ShockRadiusKm));   // left the frame: Project 2's cutaway follows it
    }

    [Fact]
    public void Revival_IronAt0p3s_ElectronCaptureAt0p07s()
    {
        var crab = Preset("sn1054");   // 9.1 M☉: an electron-capture (O-Ne-Mg) core
        Assert.True(CoreCollapseHistory.IsElectronCapture(crab));
        Assert.False(CoreCollapseHistory.IsElectronCapture(Default));
        Assert.Equal(C1.RevivalOneMgS, CoreCollapseHistory.RevivalTimeS(crab));
        Assert.Equal(C1.RevivalIronS, CoreCollapseHistory.RevivalTimeS(Default));
        Assert.Equal(CorePhase.Bounce, CoreCollapseHistory.At(Default, 0.02).Phase);
        Assert.Equal(CorePhase.Stalled, CoreCollapseHistory.At(Default, C1.StalledFromS).Phase);
        Assert.Equal(CorePhase.Stalled, CoreCollapseHistory.At(Default, 0.29).Phase);
        Assert.Equal(CorePhase.Revived, CoreCollapseHistory.At(Default, 0.3).Phase);
        Assert.Equal(CorePhase.Stalled, CoreCollapseHistory.At(crab, 0.06).Phase);
        Assert.Equal(CorePhase.Revived, CoreCollapseHistory.At(crab, 0.07).Phase);
        Assert.Equal(CorePhase.PreCollapse, CoreCollapseHistory.At(Default, 0).Phase);
    }

    [Fact]
    public void PreCollapseCore_IronOrOxygenNeonMagnesium()
    {
        Assert.Equal(C1.IronCoreKm, CoreCollapseHistory.PreCollapseCoreRadiusKm(Default));
        Assert.Equal(C1.OneMgCoreKm, CoreCollapseHistory.PreCollapseCoreRadiusKm(Preset("sn1054")));
        Assert.Equal(C1.IronCoreKm, CoreCollapseHistory.PreCollapseCoreRadiusKm(FailedSn));
        Assert.True(double.IsNaN(CoreCollapseHistory.PreCollapseCoreRadiusKm(Preset("sn2011fe"))));
    }

    [Fact]
    public void DrawnProtoNs_BoundedByTheShock_GainShellFrom30ms()
    {
        var early = CoreCollapseHistory.At(Default, 0.001);
        Assert.Equal(C1.DrawnProtoNsMaxOfShock * early.ShockRadiusKm, CoreCollapseHistory.DrawnProtoNsRadiusKm(early), 9);
        var later = CoreCollapseHistory.At(Default, 0.1);
        Assert.Equal(later.ProtoNsRadiusKm, CoreCollapseHistory.DrawnProtoNsRadiusKm(later), 9);
        Assert.True(double.IsNaN(CoreCollapseHistory.At(Default, 0.02).GainRadiusKm));
        Assert.False(double.IsNaN(CoreCollapseHistory.At(Default, 0.03).GainRadiusKm));
        // The failed track draws the gain shell only while R_gain > 1.1 R_PNS (gone by 0.95 t_BH).
        double tbh = Tbh(FailedSn);
        foreach (double f in new[] { 0.5, 0.8, 0.9, 0.95, 0.99 })
        {
            var s = CoreCollapseHistory.At(FailedSn, f * tbh);
            Assert.True(double.IsNaN(s.GainRadiusKm) || s.GainRadiusKm > C1.FailedGainMinOfProtoNs * s.ProtoNsRadiusKm, $"{f}");
        }
        Assert.True(double.IsNaN(CoreCollapseHistory.At(FailedSn, 0.96 * tbh).GainRadiusKm));
    }

    [Fact]
    public void FailedTrack_EarlyBlackHole_JoinsMonotonically()
    {
        var o = EarlyFailed;
        var times = C1.ShockTable(CoreTrack.Failed, 0.23).Select(r => r.T).ToArray();
        Assert.Equal(times.OrderBy(t => t), times);
        Assert.Equal(times.Distinct().Count(), times.Length);
        Assert.DoesNotContain(0.1, times);   // t_BH < 0.25 s: the failed rows begin at 0.4 t_BH instead
        Assert.Equal(CorePhase.BlackHole, CoreCollapseHistory.At(o, 0.23).Phase);
    }

    [Fact]
    public void Collapse_ShockTo12km_ProtoNsToTheHorizon_InTheLastMillisecond()
    {
        double tbh = Tbh(FailedSn);
        var start = CoreCollapseHistory.At(FailedSn, tbh - C1.CollapseS);
        var end = CoreCollapseHistory.At(FailedSn, tbh - 1e-9);
        Assert.Equal(CorePhase.Collapsing, start.Phase);
        Assert.Equal(C1.ShockAtCollapseKm, end.ShockRadiusKm, 3);
        double horizonKm = 2 * BlackHoleOptics.GravitationalRadiusCm(RemnantModel.MaxNeutronStarMass) / 1e5;   // 2.2 M☉ gravitational
        Assert.Equal(6.50, horizonKm, 2);
        Assert.Equal(horizonKm, end.ProtoNsRadiusKm, 3);
        Assert.Equal(horizonKm, CoreCollapseHistory.HorizonAtFormationKm(FailedSn), 9);
        Assert.True(start.ProtoNsRadiusKm > end.ProtoNsRadiusKm);
        var bh = CoreCollapseHistory.At(FailedSn, tbh);
        Assert.True(double.IsNaN(bh.ShockRadiusKm) && double.IsNaN(bh.ProtoNsRadiusKm));
    }

    [Fact]
    public void Contraction_FollowsC2_AndJoinsAt1s()
    {
        foreach (var (t, km) in C2.Radius12)
            Assert.Equal(km, CoreCollapseHistory.At(Default, t).ProtoNsRadiusKm, C2.ToleranceKm);
        double before = CoreCollapseHistory.At(Default, 0.999).ProtoNsRadiusKm, after = CoreCollapseHistory.At(Default, 1.001).ProtoNsRadiusKm;
        Assert.InRange(before - after, -0.1, 0.1);
        Assert.Equal(12, CoreCollapseHistory.At(Default, 1e3).ProtoNsRadiusKm);   // the remnant's radius from 60 s
    }

    [Fact]
    public void FallbackBlackHole_ExplodesThenContractsThenCollapsesAtFormationTime()
    {
        var o = Fallback;
        Assert.Equal(CoreTrack.Exploding, CoreCollapseHistory.TrackFor(o));
        Assert.Equal(3.0, CoreCollapseHistory.StageEndS(o));
        Assert.Equal(CorePhase.Revived, CoreCollapseHistory.At(o, 0.5).Phase);
        Assert.Equal(CorePhase.Cooling, CoreCollapseHistory.At(o, 2).Phase);
        Assert.Equal(C2.FallbackAt3sKm, CoreCollapseHistory.At(o, 3.0 - C1.CollapseS).ProtoNsRadiusKm, 1);
        Assert.Equal(CorePhase.Collapsing, CoreCollapseHistory.At(o, 3.0 - 0.5 * C1.CollapseS).Phase);
        Assert.Equal(CorePhase.BlackHole, CoreCollapseHistory.At(o, 3.0).Phase);
        Assert.Equal(0, NeutrinoTotal(o, 3.0 * 1.001));
    }

    [Fact]
    public void Distortion_SchematicShape_ApproximateAmplitude()
    {
        var s = CoreCollapseHistory.At(Default, 0.15);
        Assert.Equal(C1.DistortionFraction, s.DistortionFraction);
        Assert.False(s.DistortionSchematicOnly);
        Assert.Equal(0, CoreCollapseHistory.At(Default, 0.01).DistortionFraction);   // the bounce shock is round (no gain layer yet)
        double ramp = CoreCollapseHistory.At(Default, 0.05).DistortionFraction;
        Assert.InRange(ramp, 0.001, C1.DistortionFraction - 0.001);
    }

    [Fact]
    public void Infall_IsFreeFallSpeedAtTheShock()
    {
        Assert.InRange(CoreCollapseHistory.InfallSpeedKmS(1.3, 150), C1.InfallAt150KmLow - 100, C1.InfallAt150KmLow + 100);
        Assert.InRange(CoreCollapseHistory.InfallSpeedKmS(1.5, 150), C1.InfallAt150KmHigh - 100, C1.InfallAt150KmHigh + 100);
        var s = CoreCollapseHistory.At(Default, 0.1);
        double m = RemnantModel.BaryonicMass(Default.Remnant.Mass!.Value);
        Assert.Equal(CoreCollapseHistory.InfallSpeedKmS(m, s.ShockRadiusKm), s.InfallSpeedKmS, 6);
        // A black hole's proto-neutron star is taken at the heaviest neutron star (the hole's own mass comes later, by accretion).
        Assert.Equal(RemnantModel.MinBlackHoleMass, CoreCollapseHistory.ProtoNsMassMsun(FailedSn));
    }

    // ---- The Close-up's eras (plan ruling P3-12) ----

    private static CloseUpScene SceneFor(Outcome o) => new(o, ObserverSetup.Default, 1);

    [Fact]
    public void Eras_PreCollapse_FirstSecond_ThenTheRemnant()
    {
        var scene = SceneFor(Default);
        Assert.Equal(CloseUpEra.PreCollapse, scene.At(0).Era);
        Assert.Equal(CloseUpEra.PreCollapse, scene.At(1e-3).Era);   // the scrubber's left end (spec §3)
        Assert.Equal(CloseUpEra.FirstSecond, scene.At(1.001e-3).Era);
        Assert.Equal(CloseUpEra.FirstSecond, scene.At(0.999).Era);
        Assert.Equal(CloseUpEra.NeutronStar, scene.At(1.0).Era);
        var failed = SceneFor(FailedSn);
        double tbh = Tbh(FailedSn);
        Assert.Equal(CloseUpEra.FirstSecond, failed.At(tbh * 0.9999).Era);
        Assert.Equal(CloseUpEra.BlackHoleForming, failed.At(tbh).Era);   // plan Task 10: forming, then settling, then settled
        Assert.Equal(CloseUpEra.BlackHole, failed.At(tbh * 3).Era);
        Assert.Equal(CloseUpEra.None, SceneFor(Preset("sn2011fe")).At(1e-3).Era);
    }

    [Fact]
    public void PreCollapse_ShowsTheCoreAndItsSize()
    {
        var st = SceneFor(Default).At(1e-3);
        Assert.Contains("iron core", st.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(C1.IronCoreKm, st.CoreSphereRadiusKm);
        var size = st.Readouts.Single(r => r.Label == "Core radius");
        Assert.Equal(C1.IronCoreKm, size.Value.Value);
        Assert.Equal(Confidence.ModelDependent, size.Value.Confidence);
        Assert.False(st.IsLensed);
        Assert.True(st.FrameRadiusKm > C1.IronCoreKm);
        var crab = SceneFor(Preset("sn1054")).At(1e-3);
        Assert.Contains("oxygen-neon-magnesium", crab.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(C1.OneMgCoreKm, crab.CoreSphereRadiusKm);
    }

    [Fact]
    public void FirstSecond_LabelsNotesAndReadouts()
    {
        var st = SceneFor(Default).At(0.1);
        Assert.Equal(CloseUpEra.FirstSecond, st.Era);
        Assert.Equal("From simulations", CoreCollapseHistory.FromSimulationsLabel);
        Assert.StartsWith(CoreCollapseHistory.FromSimulationsLabel, st.StageLabel);
        Assert.Contains(CloseUpScene.Sn1987aNeutrinosNote, st.Notes);
        Assert.Contains("SN 1987A", CloseUpScene.Sn1987aNeutrinosNote);
        Assert.Contains(CloseUpScene.CoreUnbentNote, st.Notes);
        Assert.False(st.IsLensed);
        Assert.NotNull(st.Core);
        var byLabel = st.Readouts.ToDictionary(r => r.Label);
        foreach (string radius in new[] { "Shock radius", "Gain radius", "Proto-neutron star" })
        {
            Assert.Equal(Confidence.ModelDependent, byLabel[radius].Value.Confidence);
            Assert.Equal("km", byLabel[radius].Value.Unit);
            Assert.False(string.IsNullOrEmpty(byLabel[radius].Value.Source.Citation));
        }
        Assert.Equal(st.Core!.ShockRadiusKm, byLabel["Shock radius"].Value.Value);
        Assert.Equal(Confidence.ModelDependent, byLabel["Shock distortion"].Value.Confidence);   // the size; the shape is schematic (fix round 1)
        Assert.Equal(Confidence.ModelDependent, byLabel["Neutrinos"].Value.Confidence);
        Assert.Equal(Confidence.ModelDependent, byLabel["Infall speed"].Value.Confidence);
        Assert.All(st.Readouts, r => Assert.False(string.IsNullOrEmpty(r.Value.Source.Citation)));
        Assert.All(st.Readouts, r => Assert.True(double.IsFinite(r.Value.Value), r.Label));
        Assert.True(st.FrameRadiusKm >= st.Core.ShockRadiusKm);
        Assert.InRange(st.FrameRadiusKm, 1, 10_000);
    }

    [Fact]
    public void FirstSecond_FailedTrackNote_AndNoGainRowOnceItIsGone()
    {
        double tbh = Tbh(FailedSn);
        var st = SceneFor(FailedSn).At(0.5 * tbh);
        Assert.Contains(st.Notes, n => n.Contains("never revives"));
        var late = SceneFor(FailedSn).At(0.97 * tbh);
        Assert.DoesNotContain(late.Readouts, r => r.Label == "Gain radius");
        var collapsing = SceneFor(FailedSn).At(tbh - 0.5 * C1.CollapseS);
        Assert.Contains("black hole", collapsing.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NeutronStarEra_ContractsTo12kmByOneMinute()
    {
        var scene = SceneFor(Default);
        Assert.Equal(16.0, scene.At(2).SurfaceRadiusKm, C2.ToleranceKm);
        Assert.Equal(12, scene.At(100).SurfaceRadiusKm);
        Assert.Equal(CloseUpScene.NeutronStarFrameRadii * scene.At(2).SurfaceRadiusKm, scene.At(2).FrameRadiusKm, 9);
        var radius = scene.At(2).Readouts.Single(r => r.Label == "Radius");
        Assert.Equal(scene.At(2).SurfaceRadiusKm, radius.Value.Value);
        Assert.Equal(Confidence.ModelDependent, radius.Value.Confidence);
        Assert.Equal("compact §C2", radius.Value.Source.ResearchRef);
    }

    [Fact]
    public void FirstSecond_AnimationIsFlashSafe()   // spec §1 flash safety, criterion 6
    {
        Assert.True(CoreAnimation.SloshPeriodS >= 1.0 / 3);
        var scene = SceneFor(Default);
        foreach (double t in new[] { 0.002, 0.01, 0.05, 0.1, 0.3, 0.6, 0.99 })
        {
            var st = scene.At(t);
            double cycle = CoreAnimation.InfallCycleS(st.Core!);
            Assert.InRange(cycle, CoreAnimation.MinInfallCycleS, 10 * CoreAnimation.MinInfallCycleS);
            Assert.True(cycle >= 1.0 / 3);
            Assert.Matches(@"^Infall shown [0-9,]+× slower$", CoreAnimation.InfallLabel(st.Core!));
        }
    }

    // ---- Fix round 1: rulings R-P3-21 (inside the star) and R-P3-22 (the infall's 0.3c limit) ----

    [Fact]
    public void InsideTheStar_UntilBreakout_TheSurroundingsAreAHaze_WithItsNote()   // ruling R-P3-21
    {
        var o = Preset("sn1987a");
        double breakout = EjectaKinematics.BreakoutTimeS(o);
        Assert.True(breakout > 1, $"breakout {breakout} s");
        var scene = SceneFor(o);
        Assert.Equal(breakout, scene.InsideStarUntilS);
        string note = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "Inside the star until the shock breaks out ({0}): opaque hot gas surrounds the core; drawn as a dim haze (schematic)",
            UnitFormat.Time(breakout));
        Assert.Equal(note, scene.InsideStarNote);
        foreach (double t in new[] { 1e-3, 0.05, 0.9, 2, 0.99 * breakout })   // the core's eras and the neutron star before breakout
        {
            var st = scene.At(t);
            Assert.True(st.InsideStar, $"{t}");
            Assert.Contains(note, st.Notes);
        }
        var after = scene.At(1.01 * breakout);
        Assert.False(after.InsideStar);
        Assert.DoesNotContain(note, after.Notes);
        Assert.Equal(CloseUpEra.NeutronStar, after.Era);
    }

    [Fact]
    public void InsideTheStar_AFailedSupernova_UntilTheBlackHoleForms()   // R-P3-21 for a star with no breakout (my call)
    {
        Assert.True(double.IsNaN(EjectaKinematics.BreakoutTimeS(FailedSn)));
        var scene = SceneFor(FailedSn);
        double tbh = Tbh(FailedSn);
        Assert.Equal(tbh, scene.InsideStarUntilS);
        Assert.True(scene.At(0.5 * tbh).InsideStar);
        Assert.Contains(scene.At(0.5 * tbh).Notes, n => n.StartsWith("Inside the star: it never explodes"));
        Assert.False(scene.At(tbh * 1.001).InsideStar);
        Assert.False(SceneFor(Preset("sn2011fe")).At(1e-3).InsideStar);
        Assert.True(double.IsNaN(SceneFor(Preset("sn2011fe")).InsideStarUntilS));
    }

    [Fact]
    public void InfallAbove0p3c_IsFlaggedAsNewtonian()   // ruling R-P3-22
    {
        double tbh = Tbh(FailedSn);
        var fast = SceneFor(FailedSn).At(tbh - 0.5 * C1.CollapseS);
        Assert.True(fast.Core!.InfallSpeedKmS * 1e5 > 0.3 * PhysicalConstants.C);
        Assert.Contains(CloseUpScene.RelativisticInfallNote, fast.Notes);
        Assert.Contains("0.3c", fast.Readouts.Single(r => r.Label == "Infall speed").ToolTip);
        var slow = SceneFor(Default).At(0.1);
        Assert.True(slow.Core!.InfallSpeedKmS * 1e5 < 0.3 * PhysicalConstants.C);
        Assert.DoesNotContain(CloseUpScene.RelativisticInfallNote, slow.Notes);
        Assert.DoesNotContain("0.3c", slow.Readouts.Single(r => r.Label == "Infall speed").ToolTip);
        Assert.Equal(0.3, CloseUpScene.RelativisticInfallBeta);
    }

    [Fact]
    public void ExplosionScene_LogBrightness_IsTheMainViewsRule()
    {
        Assert.Equal(1f, ExplosionScene.LogBrightness(5, 5), 6);
        Assert.Equal(ExplosionScene.MinBrightnessLevel, ExplosionScene.LogBrightness(5e-4, 5), 6);   // four decades down
        Assert.Equal(ExplosionScene.MinBrightnessLevel + (1 - ExplosionScene.MinBrightnessLevel) * 0.5f, ExplosionScene.LogBrightness(5e-2, 5), 5);
        Assert.Equal(ExplosionScene.MinBrightnessLevel, ExplosionScene.LogBrightness(0, 5));
    }
}
