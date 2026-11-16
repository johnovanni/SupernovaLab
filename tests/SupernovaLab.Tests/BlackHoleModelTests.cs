using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;
using static SupernovaLab.Tests.CompactResearch;

namespace SupernovaLab.Tests;

/// <summary>
/// Plan Task 10: the black hole's state, settling and spin (spec §6.1–§6.5; compact §C8, §C9; rulings P3-11, P3-13), and the Close-up's
/// black-hole eras built on it.
/// </summary>
public class BlackHoleModelTests
{
    private static readonly ObserverSetup obs = ObserverSetup.Default;

    /// <summary>One kiloparsec in cm.</summary>
    private const double Kpc = 1e3 * PhysicalConstants.Pc;

    private static Outcome Preset(string id)
    {
        var p = PresetCatalog.Get(id);
        return TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
    }

    /// <summary>NGC 6946-BH1: a failed supernova (direct collapse, Rotation Slow).</summary>
    private static Outcome FailedSn => Preset("n6946bh1");

    /// <summary>SN 1998bw: a collapsar (a long GRB).</summary>
    private static Outcome Grb => Preset("sn1998bw");

    private static double tbh => Grb.Remnant.FormationTimeS!.Value;

    /// <summary>A failed supernova of this black-hole mass (M☉) and rotation, its hole forming at 1 s.</summary>
    private static Outcome Failed(double mass, Rotation rotation = Rotation.Slow)
        => TestOutcomes.Custom(SupernovaType.None, null, null, null, 1e13, TestOutcomes.BlackHole(mass, 1), Fate.FailedSupernova,
            inputs: LabInputs.Default with { Rotation = rotation });

    /// <summary>A black hole by fallback (an exploding core whose proto-neutron star collapses at 3 s).</summary>
    private static Outcome Fallback()
    {
        var hole = new Remnant(RemnantKind.BlackHole, TestOutcomes.Q(3, "M☉"), null, null, null, TestOutcomes.Q(160, "km/s"),
            TestOutcomes.Q(3, "s"));
        return TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.07, 3e13, hole, Fate.Supernova,
            inputs: LabInputs.Default with { Rotation = Rotation.Fast });
    }

    private static CloseUpScene SceneFor(string id, double benchSpin = 0) => new(Preset(id), obs, 1, benchSpin);

    // ---- Brief tests (plan Task 10, Step 1) ----

    [Fact]
    public void Spin_FromRotationInput()
    {
        Assert.Equal(Rotation.Slow, FailedSn.Inputs.Rotation);
        Assert.Equal(C8.Slow, BlackHoleModel.SettledSpin(FailedSn));
        Assert.Equal(C8.Fast, BlackHoleModel.SettledSpin(Failed(10, Rotation.Fast)));
        Assert.Equal(C8.Extreme, BlackHoleModel.SettledSpin(Failed(10, Rotation.Extreme)));
        Assert.Equal(C8.Extreme, BlackHoleModel.SettledSpin(Preset("sn1998bw")));
        Assert.Equal(C8.Grb, BlackHoleModel.SettledSpin(Grb with { Inputs = Grb.Inputs with { Rotation = Rotation.Slow } }));   // any GRB
        Assert.Equal(Confidence.Speculative, BlackHoleModel.At(FailedSn, obs, 1, 1e3)!.SpinQuantity.Confidence);
        var q = BlackHoleModel.At(FailedSn, obs, 1, 1e3)!.SpinQuantity;
        Assert.Equal((C8.SlowLow, C8.SlowHigh), (q.Low!.Value, q.High!.Value));
        var e = BlackHoleModel.At(Grb, obs, 1, 1e3)!.SpinQuantity;
        Assert.Equal((C8.ExtremeLow, C8.ExtremeHigh), (e.Low!.Value, e.High!.Value));
    }

    [Fact]
    public void DirectCollapse_FadesOverOneThirdDecade()
    {
        double t0 = FailedSn.Remnant.FormationTimeS!.Value;
        Assert.Equal(0, BlackHoleModel.At(FailedSn, obs, 1, t0 * 1.0001)!.SettledFraction, 2);
        Assert.Equal(1, BlackHoleModel.At(FailedSn, obs, 1, t0 * Math.Pow(10, C8.DirectFadeDecades) * 1.001)!.SettledFraction, 6);
        Assert.Equal(0.5, BlackHoleModel.At(FailedSn, obs, 1, t0 * Math.Pow(10, C8.DirectFadeDecades / 2))!.SettledFraction, 6);   // log time
        Assert.Equal(t0, BlackHoleModel.SettleStartS(FailedSn), 12);
        Assert.Equal(t0 * Math.Pow(10, C8.DirectFadeDecades), BlackHoleModel.SettleEndS(FailedSn), 9);
    }

    [Fact]
    public void Collapsar_SpinClimbsDuringSpinUp()
    {
        var a = BlackHoleModel.At(Grb, obs, 1, tbh + 1)!.Spin;
        var b = BlackHoleModel.At(Grb, obs, 1, tbh + 10)!.Spin;
        Assert.True(b > a);
        Assert.Equal(BlackHoleModel.SettledSpin(Grb), BlackHoleModel.At(Grb, obs, 1, BlackHoleModel.SettleEndS(Grb) * 1.01)!.Spin, 6);
        Assert.Equal(tbh + C8.SpinUpS, BlackHoleModel.SettleEndS(Grb), 9);
        Assert.Equal(C8.CollapsarStart, BlackHoleModel.At(Grb, obs, 1, tbh)!.Spin, 9);
        foreach (var (dt, spin) in C8.SpinUpChecks)
            Assert.Equal(spin, BlackHoleModel.At(Grb, obs, 1, tbh + dt)!.Spin, 2);
    }

    [Fact]
    public void FromEarthText_10Msun_10kpc()
        => Assert.Matches(@"about 0\.1 nano-arcseconds.*about 200,000 times", BlackHoleModel.FromEarthText(10, 10 * Kpc));

    [Fact]
    public void SettledEra_CaptionAndReadouts()
    {
        var st = SceneFor("n6946bh1").At(1e6);
        Assert.Equal(CloseUpEra.BlackHole, st.Era);
        Assert.Contains(BlackHoleModel.SettledCaption, st.Notes);
        Assert.Equal(new[] { "Mass", "Horizon", "Shadow", "Spin", "Kick", "From Earth" }, st.Readouts.Select(r => r.Label));
        Assert.Contains("OGLE-2011-BLG-0462", st.Readouts[0].ToolTip);
    }

    [Theory, InlineData(300), InlineData(2.6)]
    public void ExtremeMasses_Finite(double m)   // Review Focus 4
    {
        foreach (double bench in new[] { 0, 0.998 })
        {
            var scene = new CloseUpScene(Failed(m), obs, 1, bench);
            foreach (double t in new[] { 1, 1.0005, 1.5, 3, 1e3, 3e9 })
            {
                var s = scene.At(t);
                Assert.True(s.IsLensed);
                Assert.True(double.IsFinite(s.FrameRadiusKm) && s.FrameRadiusKm > 0);
                Assert.True(double.IsFinite(s.MassMsun) && double.IsFinite(s.Spin));
                Assert.All(s.Readouts, r => Assert.True(double.IsFinite(r.Value.Value), $"{m} M☉, spin {bench}, t {t}: {r.Label}"));
                Assert.All(s.Readouts, r => Assert.False(string.IsNullOrEmpty(r.Value.Source.Citation)));
                var rays = BlackHoleOptics.ShadowEdgeDirections(s.Spin, new Vec3(18, 0, 0.5), 96);
                Assert.NotEmpty(rays);
                Assert.All(rays, d => Assert.True(double.IsFinite(d.X) && double.IsFinite(d.Y) && double.IsFinite(d.Z)));
            }
            Assert.Equal(bench > 0 ? bench : C8.Slow, scene.At(1e3).Spin, 9);
        }
        Assert.Contains("nano-arcseconds", BlackHoleModel.FromEarthText(m, 10 * Kpc));
    }

    // ---- Further checks ----

    [Fact]
    public void FromEarth_NumbersAreTheResearchs()   // §C9.1 "From Earth" [D]: 0.103 nas, 1.95 × 10⁵
    {
        Assert.Equal(C9.TenSolarMassAt10KpcShadowNanoArcsec, BlackHoleModel.ShadowNanoArcsec(10, 10 * Kpc), 3);
        Assert.Equal(C9.TenSolarMassTimesTooSmall, BlackHoleModel.TimesTooSmallForEht(10, 10 * Kpc), C9.TenSolarMassTimesTooSmall * 0.005);
        Assert.Equal(C9.EhtBeamMicroArcsec, BlackHoleModel.EhtResolutionMicroArcsec);
        Assert.Equal("From Earth this shadow would be about 0.1 nano-arcseconds across, about 200,000 times too small for the Event Horizon Telescope",
            BlackHoleModel.FromEarthText(10, 10 * Kpc));
        Assert.DoesNotContain("NaN", BlackHoleModel.FromEarthText(10, 0));
        Assert.Contains("×", BlackHoleModel.FromEarthText(8.3, 7.7e3 * Kpc));   // tiny values in powers of ten
    }

    [Fact]
    public void Formation_StartsAtTheCollapsesHorizon_ThenGrowsToTheOutcomesMass()   // inconsistency #63, handled
    {
        double t0 = FailedSn.Remnant.FormationTimeS!.Value;
        var born = BlackHoleModel.At(FailedSn, obs, 1, t0)!;
        Assert.True(born.Forming);
        double horizonKm = 2 * BlackHoleOptics.GravitationalRadiusCm(born.DrawnMassMsun) / 1e5;
        Assert.Equal(CoreCollapseHistory.HorizonAtFormationKm(FailedSn), horizonKm, 9);   // no size jump at t_BH
        Assert.False(BlackHoleModel.At(FailedSn, obs, 1, t0 + 2 * BlackHoleModel.FormingS)!.Forming);
        var settled = BlackHoleModel.At(FailedSn, obs, 1, 1e3)!;
        Assert.Equal(FailedSn.Remnant.Mass!.Value, settled.MassMsun, 12);
        Assert.Equal(FailedSn.Remnant.Mass!.Value, settled.DrawnMassMsun, 12);
        Assert.Null(BlackHoleModel.At(FailedSn, obs, 1, t0 * 0.999));                            // not yet formed
        Assert.Null(BlackHoleModel.At(Preset("sn1987a"), obs, 1, 1e3));                          // not a black hole
        double prev = 0;
        for (int k = 0; k <= 40; k++)
        {
            double m = BlackHoleModel.At(FailedSn, obs, 1, t0 * Math.Pow(10, k / 100.0))!.MassMsun;
            Assert.True(m >= prev);
            prev = m;
        }
    }

    [Fact]
    public void DrawnSpinAndMass_StepAFewTimes_SoTheRayMapIsRetracedABoundedNumberOfTimes()
    {
        foreach (var (o, end) in new[] { (FailedSn, BlackHoleModel.SettleEndS(FailedSn)), (Grb, BlackHoleModel.SettleEndS(Grb)) })
        {
            double t0 = BlackHoleModel.SettleStartS(o);
            var drawn = new HashSet<(double, double)>();
            for (int k = 0; k <= 2000; k++)
            {
                double t = t0 * Math.Pow(end / t0, k / 2000.0);
                var s = BlackHoleModel.At(o, obs, 1, t)!;
                drawn.Add((s.DrawnSpin, s.DrawnMassMsun));
                // The drawn values lag the hole's by at most one step.
                double fadeSpin = BlackHoleModel.Birth(o) == BlackHoleBirth.Collapsar ? s.Spin : s.Spin * s.SettledFraction;
                Assert.InRange(s.DrawnSpin, fadeSpin - 1.0 / BlackHoleModel.TransitionSteps - 1e-12, fadeSpin + 1e-12);
            }
            Assert.InRange(drawn.Count, 2, BlackHoleModel.TransitionSteps + 1);
            var done = BlackHoleModel.At(o, obs, 1, end * 1.0001)!;
            Assert.Equal(BlackHoleModel.SettledSpin(o), done.DrawnSpin, 12);
            Assert.Equal(1, done.SettledFraction);
        }
        Assert.Equal(0, BlackHoleModel.At(FailedSn, obs, 1, FailedSn.Remnant.FormationTimeS!.Value)!.DrawnSpin);   // fades in from a = 0
    }

    [Fact]
    public void Collapsar_DiskWhileAccreting_FromTheResearch()   // §C8 App (the disk is drawn in Task 11)
    {
        Assert.True(BlackHoleModel.At(Grb, obs, 1, tbh + 5)!.HasDisk);
        Assert.False(BlackHoleModel.At(Grb, obs, 1, tbh + 0.5)!.HasDisk);
        Assert.False(BlackHoleModel.At(Grb, obs, 1, tbh + 20)!.HasDisk);
        Assert.Equal(C8.DiskOuterGm29, BlackHoleModel.DiskOuterGmFor(2.9), 1);
        var accreting = BlackHoleModel.At(Grb, obs, 1, tbh + 5)!;
        Assert.Equal(C8.DiskOuterKm, accreting.DiskOuterGm * BlackHoleOptics.GravitationalRadiusCm(accreting.DrawnMassMsun) / 1e5, 6);
        Assert.False(BlackHoleModel.At(FailedSn, obs, 1, 10)!.HasDisk);
    }

    [Fact]
    public void Birth_AndSettling_ForEachKindOfHole()
    {
        Assert.Equal(BlackHoleBirth.DirectCollapse, BlackHoleModel.Birth(FailedSn));
        Assert.Equal(BlackHoleBirth.Collapsar, BlackHoleModel.Birth(Grb));
        var fb = Fallback();
        Assert.Equal(BlackHoleBirth.Fallback, BlackHoleModel.Birth(fb));
        Assert.Equal(3, BlackHoleModel.SettleStartS(fb));
        Assert.Equal(C8.FallbackSettleS, BlackHoleModel.SettleEndS(fb));
        Assert.Equal(C8.Fast, BlackHoleModel.SettledSpin(fb));
    }

    [Fact]
    public void Kick_DirectCollapseNone_OthersDriftAsKickDrift()   // spec §6.5
    {
        Assert.Equal(default, BlackHoleModel.At(FailedSn, obs, 1, 1e10)!.OffsetCm);
        Assert.Equal(KickDrift.OffsetCm(Grb, obs, 1, 1e10), BlackHoleModel.At(Grb, obs, 1, 1e10)!.OffsetCm);
        Assert.True(BlackHoleModel.At(Fallback(), obs, 1, 1e10)!.OffsetCm.Length > 0);
    }

    [Fact]
    public void Eras_FormingThenSettlingThenSettled()   // plan ruling P3-12
    {
        var scene = SceneFor("n6946bh1");
        double t0 = FailedSn.Remnant.FormationTimeS!.Value;
        Assert.Equal(CloseUpEra.FirstSecond, scene.At(t0 * 0.9999).Era);
        var forming = scene.At(t0);
        Assert.Equal(CloseUpEra.BlackHoleForming, forming.Era);
        Assert.Equal(CloseUpEra.BlackHoleSettling, scene.At(t0 * 1.5).Era);
        Assert.Equal(CloseUpEra.BlackHole, scene.At(t0 * 2.2).Era);
        Assert.DoesNotContain(BlackHoleModel.SettledCaption, scene.At(t0 * 1.5).Notes);
        Assert.Equal(BlackHoleModel.SettledCaption, scene.At(1e6).StageLabel);
        Assert.Contains("visual transition", scene.At(t0 * 1.5).StageLabel, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(forming.BlackHole);
        Assert.True(forming.IsLensed);
        // The collapsar's caption says it spins up, and its spin readout climbs.
        var grb = SceneFor("sn1998bw");
        Assert.Contains("spin", grb.At(tbh + 5).StageLabel, StringComparison.OrdinalIgnoreCase);
        double Spin(CloseUpState s) => s.Readouts.Single(r => r.Label == "Spin").Value.Value;
        Assert.True(Spin(grb.At(tbh + 10)) > Spin(grb.At(tbh + 1)));
        Assert.Equal(CloseUpEra.BlackHole, grb.At(tbh + 16).Era);
    }

    [Fact]
    public void Readouts_AreTheExactFormulas_ForTheHolesMassAndSpin()
    {
        var st = SceneFor("n6946bh1").At(1e6);
        double m = FailedSn.Remnant.Mass!.Value, a = C8.Slow;
        double rgKm = BlackHoleOptics.GravitationalRadiusCm(m) / 1e5;
        var by = st.Readouts.ToDictionary(r => r.Label);
        Assert.Equal(m, by["Mass"].Value.Value);
        Assert.Equal(2 * BlackHoleOptics.HorizonRadius(a) * rgKm, by["Horizon"].Value.Value, 9);
        Assert.Equal(BlackHoleOptics.ApparentShadowDiameterRad(m, 1e5, a), by["Shadow"].Value.Value, 9);
        Assert.Equal(a, by["Spin"].Value.Value);
        Assert.Equal(Confidence.Speculative, by["Spin"].Value.Confidence);
        Assert.Contains(C8.CygX1, by["Spin"].ToolTip);
        Assert.Contains(C8.A0620, by["Spin"].ToolTip);
        Assert.Contains("GWTC-3", by["Spin"].ToolTip);
        Assert.Equal(C9.OgleTooltip, by["Mass"].ToolTip);
        Assert.Equal(0, by["Kick"].Value.Value);
        double da = obs.LuminosityDistanceCm / Math.Pow(1 + obs.Redshift, 2);   // the angular-diameter distance
        Assert.Equal(BlackHoleModel.ShadowNanoArcsec(m, da), by["From Earth"].Value.Value, 12);
        Assert.StartsWith(BlackHoleModel.FromEarthText(m, da), by["From Earth"].ToolTip);
        Assert.Contains(CloseUpScene.NoTelescopeNote, by["From Earth"].ToolTip);   // spec §6.5: why the view says so
        Assert.Equal(a, st.Spin);
        Assert.Equal(m, st.MassMsun);
        Assert.Equal(CloseUpScene.BlackHoleFrameShadowRadii * BlackHoleOptics.ApparentShadowDiameterRad(m, 1e5, a) / 2, st.FrameRadiusKm, 9);
    }
}

/// <summary>Plan Task 10: the black hole's overlay geometry (<see cref="LensOverlay"/>), without OpenGL.</summary>
public class LensOverlayTests
{
    private static readonly OpenTK.Mathematics.Vector3d Eye = 19 * new OpenTK.Mathematics.Vector3d(Math.Cos(0.5), Math.Sin(0.5), 0);

    [Fact]
    public void ShadowEdge_LiesAlongTheExactEdgeDirections_Dashed()
    {
        const double rgKm = 10;
        var eyeKm = Eye * rgKm;
        var dashes = LensOverlay.ShadowEdge(0.9, eyeKm, rgKm);
        Assert.Equal(LensOverlay.EdgePoints / 2, dashes.Count);
        var dirs = BlackHoleOptics.ShadowEdgeDirections(0.9, new Vec3(Eye.X, Eye.Y, Eye.Z), LensOverlay.EdgePoints);
        var first = (dashes[0][0] - eyeKm).Normalized();
        Assert.Equal(dirs[0].X, first.X, 9);
        Assert.Equal(dirs[0].Y, first.Y, 9);
        Assert.Equal(dirs[0].Z, first.Z, 9);
        Assert.Empty(LensOverlay.ShadowEdge(0.9, eyeKm, 0));
    }

    /// <summary>The prograde photon orbit's tick is on the side where the exact edge comes nearest the hole (the flattened side).</summary>
    [Theory, InlineData(0.5), InlineData(2.0), InlineData(4.0)]
    public void PhotonOrbitTicks_ProgradeOnTheFlattenedSide(double yaw)
    {
        var eye = 19 * new OpenTK.Mathematics.Vector3d(Math.Cos(yaw), Math.Sin(yaw), 0.05);
        var towards = eye.Normalized();
        var across = LensOverlay.Across(towards, OpenTK.Mathematics.Vector3d.UnitX);
        var dirs = BlackHoleOptics.ShadowEdgeDirections(0.9, new Vec3(eye.X, eye.Y, eye.Z), 360);
        var c = -towards;
        // The edge's extent across the view on each side of the hole's direction.
        double plus = dirs.Max(d => OpenTK.Mathematics.Vector3d.Dot(new OpenTK.Mathematics.Vector3d(d.X, d.Y, d.Z), across));
        double minus = -dirs.Min(d => OpenTK.Mathematics.Vector3d.Dot(new OpenTK.Mathematics.Vector3d(d.X, d.Y, d.Z), across));
        int flattened = plus < minus ? 1 : -1;
        Assert.Equal(flattened, LensOverlay.ProgradeSide(across, towards));
        var ticks = LensOverlay.PhotonOrbits(0.9, across, towards, 1);
        var (pro, retro) = LensOverlay.PhotonOrbitsGm(0.9);
        Assert.Equal(1.5579, pro, 4);    // §C7: r_ph+ at a = 0.9
        Assert.Equal(3.9103, retro, 4);  // §C7: r_ph− at a = 0.9
        Assert.Equal(flattened * Math.Sqrt(pro * pro + 0.81), OpenTK.Mathematics.Vector3d.Dot(ticks[0][0], across), 9);
        Assert.True(c.Length > 0);
    }

    [Fact]
    public void Ergosphere_MeetsTheHorizonAtThePoles_And2AtTheEquator()
    {
        var across = OpenTK.Mathematics.Vector3d.UnitY;
        var ergo = LensOverlay.Ergosphere(0.9, across, 1);
        var horizon = LensOverlay.Horizon(0.9, across, 1);
        Assert.Equal(BlackHoleOptics.HorizonRadius(0.9), ergo[0].Z, 9);                     // the pole: r_E = r₊
        Assert.Equal(horizon[0].Z, ergo[0].Z, 9);
        double equator = ergo.Max(p => p.Y);
        Assert.Equal(Math.Sqrt(4 + 0.81), equator, 3);                                     // r_E = 2 at the equator, ρ = √(r² + a²)
        Assert.True(horizon.Max(p => p.Y) > horizon.Max(p => p.Z));                         // the horizon is oblate in these coordinates
        Assert.Single(LensOverlay.PhotonOrbits(0, across, OpenTK.Mathematics.Vector3d.UnitX, 1));   // no spin: the photon sphere's circle
        Assert.Equal(3, LensOverlay.PhotonOrbits(0, across, OpenTK.Mathematics.Vector3d.UnitX, 1)[0].Max(p => p.Y), 6);
    }

    [Fact]
    public void Labels_NameTheHorizonPhotonOrbitsAndErgosphere()
    {
        var across = OpenTK.Mathematics.Vector3d.UnitY;
        Assert.Equal(new[] { LensOverlay.HorizonLabel, LensOverlay.PhotonSphereLabel },
            LensOverlay.Labels(0, across, OpenTK.Mathematics.Vector3d.UnitX, 1).Select(l => l.Text));
        Assert.Equal(new[] { LensOverlay.HorizonLabel, LensOverlay.ProgradeLabel, LensOverlay.RetrogradeLabel, LensOverlay.ErgosphereLabel },
            LensOverlay.Labels(0.9, across, OpenTK.Mathematics.Vector3d.UnitX, 1).Select(l => l.Text));
        Assert.All(LensOverlay.Labels(0.998, across, OpenTK.Mathematics.Vector3d.UnitX, 300 * 1.477),
            l => Assert.True(double.IsFinite(l.AtKm.X) && double.IsFinite(l.AtKm.Z)));
        // Task 10's launch: the labels sit at the ends of leader lines outside the shadow (its edge reaches 6.83 GM/c² at a = 0.9), one per
        // label, each starting inside it on the outline it names; exactly one (a photon orbit) is on the left.
        var labels = LensOverlay.Labels(0.9, across, OpenTK.Mathematics.Vector3d.UnitX, 1);
        Assert.All(labels, l => Assert.True(l.AtKm.Length > 6.83 && l.FromKm.Length < 4.3));
        Assert.Single(labels, l => l.LeftOfEnd);
        Assert.Equal(labels.Count, LensOverlay.Leaders(labels).Count);
        Assert.Equal(BlackHoleOptics.HorizonRadius(0.9), Math.Sqrt(Math.Pow(labels[0].FromKm.Y, 2) / (1 + 0.81 / Math.Pow(BlackHoleOptics.HorizonRadius(0.9), 2)) + labels[0].FromKm.Z * labels[0].FromKm.Z), 9);
    }
}
