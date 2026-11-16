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
/// Plan Task 11: the collapsar's accretion disk and jets, and the fallback black hole, in the Close-up (spec §6.2, §6.4; compact §C7,
/// §C8). The disk's geometry is exact (its inner edge at the ISCO of the drawn spin; its outer edge from MW99's 200 km torus); its
/// brightness is g⁴ × a schematic radial profile, with g the exact redshift of a circular Kerr orbit. (The plan names
/// BlackHoleModelTests.cs; these live beside it, in their own file, as the Task 10 file is already long.)
/// </summary>
public class CollapsarDiskTests
{
    private static readonly ObserverSetup obs = ObserverSetup.Default;

    private static Outcome Preset(string id)
    {
        var p = PresetCatalog.Get(id);
        return TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
    }

    private static Outcome Grb => Preset("sn1998bw");
    private static Outcome FailedSn => Preset("n6946bh1");
    private static double tbh => Grb.Remnant.FormationTimeS!.Value;

    /// <summary>The lab's own fallback black hole: a 27.4 M☉ star at the default inputs (explodability table: FallbackBlackHole).</summary>
    private static Outcome FallbackOutcome => TestOutcomes.Engine.Compute(LabInputs.Default with { InitialMass = 27.4 }, null);

    // ---- Brief tests (plan Task 11, Step 1) ----

    [Fact]
    public void Collapsar_HasDiskDuringAccretion_NotAfter()
    {
        Assert.True(BlackHoleModel.At(Grb, obs, 1, tbh + 5)!.HasDisk);
        Assert.False(BlackHoleModel.At(Grb, obs, 1, BlackHoleModel.SettleEndS(Grb) * 2)!.HasDisk);
    }

    [Fact]
    public void Disk_InnerEdgeAtIsco()
    {
        var scene = new CloseUpScene(Grb, obs, 1);
        foreach (double dt in new[] { 1.0, 2.0, 5.0, 10.0, 14.0 })
        {
            var s = scene.At(tbh + dt);
            Assert.True(s.HasDisk, $"{dt} s");
            Assert.Equal(BlackHoleOptics.IscoRadius(s.Spin), s.DiskInnerGm, 12);    // the ISCO of the drawn spin (the picture's)
            Assert.Equal(s.BlackHole!.DiskOuterGm, s.DiskOuterGm, 12);
            Assert.Equal(BlackHoleOptics.IscoRadius(s.BlackHole.DrawnSpin), s.BlackHole.DiskInnerGm, 12);
            Assert.True(s.DiskOuterGm > s.DiskInnerGm);
        }
        var after = scene.At(tbh + 20);
        Assert.False(after.HasDisk);
        Assert.Equal(0, after.DiskBrightness);
    }

    [Fact]
    public void FailedSn_NoDisk() => Assert.False(BlackHoleModel.At(FailedSn, obs, 1, 10)!.HasDisk);

    [Fact]
    public void Fallback_DriftsWithMomentumScaledKick()
    {
        var o = FallbackOutcome;
        Assert.Equal(BlackHoleBirth.Fallback, BlackHoleModel.Birth(o));
        Assert.True(BlackHoleModel.At(o, obs, 1, 1e10)!.OffsetCm.Length > 0);
        Assert.Equal(KickDrift.OffsetCm(o, obs, 1, 1e10), BlackHoleModel.At(o, obs, 1, 1e10)!.OffsetCm);
        // The lab's momentum-scaled kick (outcomes §6): the neutron stars' mean scaled by 1.4 M☉ / M_BH, so a heavier hole moves slower.
        var heavier = TestOutcomes.Engine.Compute(LabInputs.Default with { InitialMass = 29.6 }, null);
        Assert.Equal(BlackHoleBirth.Fallback, BlackHoleModel.Birth(heavier));
        double p = o.Remnant.KickKmS!.Value * o.Remnant.Mass!.Value;
        Assert.Equal(p, heavier.Remnant.KickKmS!.Value * heavier.Remnant.Mass!.Value, p * 1e-6);
    }

    [Fact]
    public void Jets_AlongSpinAxis_SameAsMainView()
    {
        var s = new CloseUpScene(Grb, obs, 1).At(tbh + 5);
        Assert.True(s.ShowJets);
        Assert.Equal(new Vec3(0, 0, 1), s.JetAxis);                                        // the spin axis: +z in the tracer's chart
        Assert.Equal(OpenTK.Mathematics.Vector3d.UnitZ, JetRenderer.JetAxis);              // the main view's jets (RenderJets) along ±z
        Assert.Equal(new Vec3(JetRenderer.JetAxis.X, JetRenderer.JetAxis.Y, JetRenderer.JetAxis.Z), s.JetAxis);
        Assert.Equal(Core.Signals.GrbModel.JetHalfAngleRad, s.JetHalfAngleRad);           // the main view's 5° cones
        Assert.False(new CloseUpScene(Grb, obs, 1).At(tbh + 20).ShowJets);                // the base goes with the disk (the engine)
        Assert.False(new CloseUpScene(FailedSn, obs, 1).At(10).ShowJets);
    }

    // ---- Further checks ----

    /// <summary>§C8 App: the disk fades over the last 20% of its life (and fades in over 0.5 s, a presentation choice).</summary>
    [Fact]
    public void DiskBrightness_FadesOverTheLastFifth_WithNoFlicker()
    {
        Assert.Equal(C8.DiskFadeFraction, BlackHoleModel.DiskFadeFraction);
        double fadeFrom = C8.DiskToS - C8.DiskFadeFraction * (C8.DiskToS - C8.DiskFromS);   // 12.2 s after t_BH
        double B(double dt) => BlackHoleModel.At(Grb, obs, 1, tbh + dt)!.DiskBrightness;
        Assert.Equal(0, B(0.5));
        Assert.Equal(1, B(5), 12);
        Assert.Equal(1, B(fadeFrom - 1e-6), 5);
        Assert.Equal(0.5, B(0.5 * (fadeFrom + C8.DiskToS)), 9);
        Assert.Equal(0, B(C8.DiskToS), 9);
        Assert.InRange(B(C8.DiskFromS + 0.25), 0.01, 0.99);   // the fade-in
        // Flash safety: one rise and one fall, nothing periodic (no orbiting texture either: the shading has no time input).
        int reversals = 0;
        double prev = 0, slope = 0;
        for (int k = 0; k <= 4000; k++)
        {
            double b = B(20.0 * k / 4000);
            double d = b - prev;
            if (Math.Abs(d) > 1e-12)
            {
                if (slope != 0 && Math.Sign(d) != Math.Sign(slope)) reversals++;
                slope = d;
            }
            prev = b;
        }
        Assert.Equal(1, reversals);
    }

    [Fact]
    public void DiskNotes_ExactWords_WhileTheDiskShows_ThenWhyItIsGone()
    {
        var scene = new CloseUpScene(Grb, obs, 1);
        var on = scene.At(tbh + 5);
        Assert.Contains("Accretion disk: inner edge at the ISCO (exact); size from simulations; brightness schematic", on.Notes);
        Assert.Equal("Accretion disk: inner edge at the ISCO (exact); size from simulations; brightness schematic", CloseUpScene.DiskNote);
        Assert.Contains(CloseUpScene.DiskShadingNote, on.Notes);
        Assert.Contains(scene.JetsNote!, on.Notes);
        Assert.Contains(on.StageKey, k => k.Contains("disk", StringComparison.OrdinalIgnoreCase));
        var off = scene.At(tbh + 20);
        Assert.DoesNotContain(CloseUpScene.DiskNote, off.Notes);
        Assert.Contains(CloseUpScene.DiskEndedNote, off.Notes);
        Assert.DoesNotContain(CloseUpScene.DiskEndedNote, scene.At(tbh + 0.5).Notes);   // before the disk forms
        Assert.DoesNotContain(CloseUpScene.DiskEndedNote, new CloseUpScene(FailedSn, obs, 1).At(1e3).Notes);
        // Every shown number stays a Quantity with a confidence and a source (the disk adds none to the readouts).
        Assert.Equal(new[] { "Mass", "Horizon", "Shadow", "Spin", "Kick", "From Earth" }, on.Readouts.Select(r => r.Label));
    }

    /// <summary>
    /// Fix round 1 (review item 1): the jets note reads its times from Core, the same source the main view reads: the jets break out (and
    /// the main view starts drawing them) at the GRB model's onset, and their base empties when the disk ends at t_BH + 15 s.
    /// </summary>
    [Fact]
    public void JetsNote_TimesAreTheCoreValues_TheMainViewReads()
    {
        var o = Grb;
        var grb = new Core.Signals.GrbModel();
        double breakout = grb.ValidRange(o).StartS, ends = tbh + BlackHoleModel.DiskToS;
        Assert.Equal(grb.OnsetS(o), breakout);
        var scene = new CloseUpScene(o, obs, 1);
        Assert.Equal(breakout, scene.JetsBreakOutS);
        Assert.Equal(ends, scene.JetBaseEndsS);
        Assert.Equal(string.Format(System.Globalization.CultureInfo.InvariantCulture, CloseUpScene.JetsNoteFormat,
            UnitFormat.Time(breakout), UnitFormat.Time(ends)), scene.JetsNote);
        Assert.Contains(UnitFormat.Time(breakout), scene.JetsNote);
        Assert.Contains(UnitFormat.Time(ends), scene.JetsNote);
        // The main view draws the jets from that same time.
        var main = new ExplosionScene(o);
        Assert.False(main.At(breakout * 0.999).ShowJets);
        Assert.True(main.At(breakout * 1.001).ShowJets);
        Assert.Null(new CloseUpScene(FailedSn, obs, 1).JetsNote);
    }

    /// <summary>Fix round 1 (review item 5): the disk-ended note shows in the early settled era only, up to ten times the disk's end time.</summary>
    [Fact]
    public void DiskEndedNote_OnlyUntilTenTimesTheDisksEnd()
    {
        var scene = new CloseUpScene(Grb, obs, 1);
        double ends = tbh + BlackHoleModel.DiskToS;
        Assert.Equal(10, CloseUpScene.DiskEndedNoteUntilFactor);
        Assert.Contains(CloseUpScene.DiskEndedNote, scene.At(ends * 1.2).Notes);
        Assert.Contains(CloseUpScene.DiskEndedNote, scene.At(ends * 9.9).Notes);
        Assert.DoesNotContain(CloseUpScene.DiskEndedNote, scene.At(ends * 10.1).Notes);
        Assert.DoesNotContain(CloseUpScene.DiskEndedNote, scene.At(3.15e7).Notes);
    }

    /// <summary>Spec §6.4: a fallback hole settles once the fallback ends (§C8: 1,000 s); it has no disk and no jets.</summary>
    [Fact]
    public void Fallback_SettlesAtTheResearchsTime_WithoutDiskOrJets()
    {
        var o = FallbackOutcome;
        Assert.Equal(C8.FallbackSettleS, BlackHoleModel.SettleEndS(o));
        var scene = new CloseUpScene(o, obs, 1);
        var settling = scene.At(30);
        Assert.Equal(CloseUpEra.BlackHoleSettling, settling.Era);
        Assert.Contains(CloseUpScene.FallbackSettlingNote, settling.Notes);
        Assert.Contains(CloseUpScene.ScaledKickNote, settling.Notes);
        Assert.False(settling.HasDisk);
        Assert.False(settling.ShowJets);
        Assert.Equal(CloseUpEra.BlackHole, scene.At(C8.FallbackSettleS * 1.01).Era);
        Assert.True(scene.At(1e9).Readouts.Single(r => r.Label == "Kick").Second!.Value > 0);
    }

    // ---- The disk in the tracer (the C# mirror of lensing.frag) ----

    /// <summary>
    /// Ω = 1/(r^{3/2} + a) (Bardeen, Press &amp; Teukolsky 1972, eq. 2.16) and u^t = (r^{3/2} + a)/(r^{3/4} √(r^{3/2} − 3 r^{1/2} + 2a)) (Page &amp; Thorne 1974: ℬ/√𝒞).
    /// A photon with no angular momentum (ℓ = 0, as for a camera on the axis) is redshifted by 1/u^t: √(1 − 3/r) without spin, √½ at the ISCO.
    /// </summary>
    [Fact]
    public void DiskRedshift_IsTheExactCircularOrbitFormula()
    {
        Assert.Equal(Math.Sqrt(0.5), BlackHoleOptics.DiskRedshift(0, 6, 0), 12);
        Assert.Equal(Math.Sqrt(1 - 3.0 / 20), BlackHoleOptics.DiskRedshift(0, 20, 0), 12);
        Assert.Equal(1 / Math.Pow(6, 1.5), BlackHoleOptics.CircularOrbitOmega(0, 6), 12);
        double a = 0.9, r = 4;
        double ut = (Math.Pow(r, 1.5) + a) / (Math.Pow(r, 0.75) * Math.Sqrt(Math.Pow(r, 1.5) - 3 * Math.Sqrt(r) + 2 * a));
        Assert.Equal(ut, BlackHoleOptics.CircularOrbitUt(a, r), 12);
        double ell = 3.2, omega = 1 / (Math.Pow(r, 1.5) + a);
        Assert.Equal(1 / (ut * (1 - omega * ell)), BlackHoleOptics.DiskRedshift(a, r, ell), 12);
        Assert.True(BlackHoleOptics.DiskRedshift(a, r, ell) > BlackHoleOptics.DiskRedshift(a, r, -ell));   // towards us: bluer
        Assert.True(double.IsFinite(BlackHoleOptics.DiskRedshift(0.9, BlackHoleOptics.IscoRadius(0.9), 2)));
    }

    /// <summary>A camera on the spin axis sees every disk ray with ℓ = 0, so the traced g is 1/u^t at the hit radius (any spin).</summary>
    [Theory, InlineData(0.0), InlineData(0.5), InlineData(0.9)]
    public void Tracer_FaceOn_HitsTheAnnulus_WithTheAxisRedshift(double spin)
    {
        double inner = BlackHoleOptics.IscoRadius(spin), outer = 46.7;
        var cam = new Vec3(0, 0, 100);
        int hits = 0;
        foreach (double target in new[] { 3.0, 8, 15, 30, 45 })
        {
            var d = (new Vec3(target, 0, 0) - cam).Normalized();
            var ray = BlackHoleOptics.TraceRay(spin, cam, d, BlackHoleOptics.EscapeRadiusFor(100), 0, inner, outer);
            if (ray.Fate != RayFate.HitDisk) continue;
            hits++;
            var (rHit, g) = ray.DiskHit!.Value;
            Assert.InRange(rHit, inner, outer);
            Assert.Equal(1 / BlackHoleOptics.CircularOrbitUt(spin, rHit), g, 3);
        }
        Assert.True(hits >= 4);
        // Without the disk the same rays are unchanged (the default is no disk).
        var plain = BlackHoleOptics.TraceRay(spin, cam, (new Vec3(15, 0, 0) - cam).Normalized(), 120);
        Assert.NotEqual(RayFate.HitDisk, plain.Fate);
        Assert.Null(plain.DiskHit);
    }

    /// <summary>
    /// The side of the disk turning towards the camera is blueshifted (g &gt; 1) and the other redshifted, and it is the side of the prograde
    /// photon orbit (Task 10's <see cref="LensOverlay.ProgradeSide"/>, where the shadow is flattened): the disk turns with the spin, +z.
    /// </summary>
    [Theory, InlineData(0.5), InlineData(2.0), InlineData(4.0)]
    public void Tracer_ApproachingSide_IsBlueshifted_AndIsThePrograde(double yaw)
    {
        const double spin = 0.9;
        var eye = new OpenTK.Mathematics.Vector3d(40 * Math.Cos(yaw), 40 * Math.Sin(yaw), 4);
        var towards = eye.Normalized();
        var across = LensOverlay.Across(towards, OpenTK.Mathematics.Vector3d.UnitX);
        int prograde = LensOverlay.ProgradeSide(across, towards);
        var cam = new Vec3(eye.X, eye.Y, eye.Z);
        double G(int side)
        {
            var target = new Vec3(8 * side * across.X, 8 * side * across.Y, 0);
            var ray = BlackHoleOptics.TraceRay(spin, cam, (target - cam).Normalized(), 60, 0, BlackHoleOptics.IscoRadius(spin), 46.7);
            Assert.Equal(RayFate.HitDisk, ray.Fate);
            return ray.DiskHit!.Value.Redshift;
        }
        Assert.True(G(prograde) > 1, $"approaching {G(prograde)}");
        Assert.True(G(-prograde) < 1, $"receding {G(-prograde)}");
    }

    /// <summary>The plunging region inside the ISCO and the space beyond the outer edge are empty: no ray hits the plane there.</summary>
    [Fact]
    public void Tracer_DiskHits_StayInsideTheAnnulus()
    {
        const double spin = 0.9;
        double inner = BlackHoleOptics.IscoRadius(spin), outer = 30;
        var cam = new Vec3(0, -40, 6);
        int hits = 0;
        for (int i = -20; i <= 20; i++)
            for (int j = -20; j <= 20; j++)
            {
                var d = new Vec3(i * 0.02, 1, j * 0.02 - 0.15).Normalized();
                var ray = BlackHoleOptics.TraceRay(spin, cam, d, 60, 0, inner, outer);
                if (ray.DiskHit is { } hit)
                {
                    hits++;
                    Assert.InRange(hit.RadiusGm, inner, outer);
                }
                Assert.Equal(ray.Fate == RayFate.HitDisk, ray.DiskHit is not null);
            }
        Assert.True(hits > 100);
    }
}

/// <summary>
/// Fix round 1 (review item 3): a jet point is hidden where the straight line from the eye to it crosses the disk's plane inside the
/// annulus (the C# mirror of cone.frag's hiddenByDisk).
/// </summary>
public class JetDiskOcclusionTests
{
    [Fact]
    public void LowerJet_SeenFromAbove_IsHiddenByTheDisk_ButNotSideOn()
    {
        var hole = OpenTK.Mathematics.Vector3d.Zero;
        var eye = new OpenTK.Mathematics.Vector3d(0, -200, 70);   // about 20 degrees above the disk
        double inner = 10, outer = 100;
        // A point on the lower jet (−z) close to the hole: the line of sight crosses the plane inside the annulus.
        var lower = new OpenTK.Mathematics.Vector3d(0, 0, -40);
        Assert.True(StarOcclusion.HiddenByDisk(lower - eye, hole - eye, inner, outer));
        // The upper jet is in front of the plane: never hidden.
        Assert.False(StarOcclusion.HiddenByDisk(new OpenTK.Mathematics.Vector3d(0, 0, 40) - eye, hole - eye, inner, outer));
        // A point far down the lower jet is seen past the disk's outer edge.
        Assert.False(StarOcclusion.HiddenByDisk(new OpenTK.Mathematics.Vector3d(0, 0, -5000) - eye, hole - eye, inner, outer));
        // Side-on (the eye in the plane) nothing is hidden; no disk (outer 0) hides nothing.
        var side = new OpenTK.Mathematics.Vector3d(0, -200, 0);
        Assert.False(StarOcclusion.HiddenByDisk(lower - side, hole - side, inner, outer));
        Assert.False(StarOcclusion.HiddenByDisk(lower - eye, hole - eye, 0, 0));
        // The line through the hole's own inner gap (inside the ISCO) is not hidden.
        var below = new OpenTK.Mathematics.Vector3d(0, -1, -40);
        var straightDown = new OpenTK.Mathematics.Vector3d(0, -2, 300);
        Assert.False(StarOcclusion.HiddenByDisk(below - straightDown, hole - straightDown, inner, outer));
    }
}

/// <summary>Plan Task 11: the disk's shading (<see cref="DiskRenderer"/>), the C# mirror of disk.frag, without OpenGL.</summary>
public class DiskRendererTests
{
    [Fact]
    public void Brightness_IsGToTheFourth_TimesTheSchematicProfile()
    {
        double isco = 2.32;
        Assert.Equal(16, DiskRenderer.Intensity(1.2, 5, isco) / DiskRenderer.Intensity(0.6, 5, isco), 9);
        Assert.Equal(DiskRenderer.Profile(5, isco), DiskRenderer.Intensity(1, 5, isco), 12);
        Assert.Equal(0, DiskRenderer.Profile(isco, isco), 12);                       // no torque at the inner edge
        Assert.Equal(1, DiskRenderer.Profile(DiskRenderer.ProfilePeakIscos * isco, isco), 9);
        Assert.True(DiskRenderer.Profile(10, isco) > DiskRenderer.Profile(40, isco));
        Assert.True(DiskRenderer.Profile(40, isco) > 0);                              // the outer disk still shows (schematic)
        Assert.Equal(0, DiskRenderer.Profile(isco * 0.9, isco));
    }

    /// <summary>A blackbody stays a blackbody under a frequency shift, with T × g: the approaching side is hotter and bluer.</summary>
    [Fact]
    public void Colour_IsTheBlackbodyOfTheShiftedTemperature()
    {
        double isco = 2.32, r = DiskRenderer.ProfilePeakIscos * isco;
        Assert.Equal(DiskRenderer.PeakTemperatureK, DiskRenderer.EmittedTemperatureK(r, isco), 6);
        Assert.Equal(1.3 * DiskRenderer.PeakTemperatureK, DiskRenderer.ObservedTemperatureK(1.3, r, isco), 6);
        var blue = DiskRenderer.Color(1.3, r, isco);
        var red = DiskRenderer.Color(0.7, r, isco);
        Assert.True(blue.Z / blue.X > red.Z / red.X);
        Assert.Equal(DiskRenderer.RampSize, DiskRenderer.Ramp.Count);
        var (rr, gg, bb) = BlackbodyColor.FromTemperature(DiskRenderer.RampKelvin(3));
        Assert.Equal(DiskRenderer.Linear(rr), DiskRenderer.Ramp[3].X, 6);
        Assert.Equal(DiskRenderer.Linear(bb), DiskRenderer.Ramp[3].Z, 6);
        Assert.True(gg > 0);
    }

    /// <summary>The caching choice: the disk's geometry (inner and outer edge) is part of the rays; its brightness is applied when the map is sampled.</summary>
    [Fact]
    public void RayMap_DiskBrightnessNeverRetraces_ButItsGeometryDoes()
    {
        var u = new LensingUniforms(0.8f, new OpenTK.Mathematics.Vector3(0, -30, 0), OpenTK.Mathematics.Vector3.UnitY,
            OpenTK.Mathematics.Vector3.UnitX, OpenTK.Mathematics.Vector3.UnitZ, new OpenTK.Mathematics.Vector2(0.5f, 0.4f), 60,
            DiskInner: 2.9f, DiskOuter: 46.7f, DiskLevel: 1f);
        Assert.True(RayMapPlanner.SameRays(u, u with { DiskLevel = 0.3f }));
        Assert.False(RayMapPlanner.SameRays(u, u with { DiskInner = 2.8f }));
        Assert.False(RayMapPlanner.SameRays(u, u with { DiskOuter = 40f }));
        Assert.False(RayMapPlanner.SameRays(u, u with { DiskInner = 0, DiskOuter = 0 }));
    }
}
