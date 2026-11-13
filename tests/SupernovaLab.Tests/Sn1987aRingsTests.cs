using OpenTK.Mathematics;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Structure;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>Task 10: SN 1987A's rings (structure §S8): geometry, orientation in the scene, hotspots and when they are drawn.</summary>
public class Sn1987aRingsTests
{
    private const double Day = PhysicalConstants.Day, Ly = PhysicalConstants.LightYear;

    private static Outcome Sn1987a()
    {
        var p = PresetCatalog.Get("sn1987a");
        return TestOutcomes.Engine.Compute(p.Inputs, p.Pins);
    }

    [Fact]
    public void Rings_AreSugermansFitScaledTo51Point4Kpc()
    {
        var rings = Sn1987aRings.Rings;
        Assert.Equal(3, rings.Count);
        var er = rings[0]; var north = rings[1]; var south = rings[2];
        Assert.True(er.Equatorial);
        Assert.False(north.Equatorial);
        Assert.False(south.Equatorial);
        // §S8's App block: 0.665 ly = 6.29e17 cm exactly (not the table row's 6.31e17).
        Assert.Equal(6.29e17, er.RadiusCm);
        Assert.InRange(er.RadiusCm / Ly, 0.664, 0.666);
        Assert.Equal(0, er.AxisOffsetCm);
        Assert.InRange(north.RadiusCm / Ly, 1.459, 1.461);               // 1.460 ly = 1.381e18 cm
        Assert.InRange(north.RadiusCm, 1.380e18, 1.382e18);
        Assert.InRange(north.AxisOffsetCm / Ly, -1.399, -1.397);         // -1.398 ly = -1.323e18 cm
        Assert.InRange(north.AxisOffsetCm, -1.324e18, -1.322e18);
        Assert.InRange(south.RadiusCm / Ly, 1.634, 1.636);               // 1.635 ly = 1.546e18 cm
        Assert.InRange(south.RadiusCm, 1.545e18, 1.547e18);
        Assert.InRange(south.AxisOffsetCm / Ly, 1.027, 1.029);           // +1.028 ly = +9.73e17 cm
        Assert.InRange(south.AxisOffsetCm, 9.72e17, 9.74e17);
        Assert.All(rings, r => Assert.Equal(43.0, r.InclinationRad * 180 / Math.PI, 9));
        // The north ring is on the far side (z0 < 0), the south ring on the near side (z0 > 0).
        Assert.True(north.AxisOffsetCm < 0);
        Assert.True(south.AxisOffsetCm > 0);
        // Table 3 itself is at 50 kpc: every length scales with the distance.
        var at50 = Sn1987aRings.At(50);
        Assert.Equal(1.42, at50[1].RadiusCm / Ly, 6);
        Assert.Equal(-1.36, at50[1].AxisOffsetCm / Ly, 6);
        Assert.Equal(1.59, at50[2].RadiusCm / Ly, 6);
        Assert.Equal(1.00, at50[2].AxisOffsetCm / Ly, 6);
    }

    [Fact]
    public void Axis_IsTheTiltedUnitVectorInSkyCoordinates()
    {
        var (w, n, z) = Sn1987aRings.AxisSky;
        Assert.Equal(-0.1055, w, 4);
        Assert.Equal(-0.6738, n, 4);
        Assert.Equal(0.7314, z, 4);
        Assert.Equal(1, w * w + n * n + z * z, 12);
        // 43.0 deg from the line of sight, toward P.A. 171.1 deg (just east of south).
        Assert.Equal(43.0, Math.Acos(z) * 180 / Math.PI, 9);
        double pa = Math.Atan2(-w, n) * 180 / Math.PI;      // from north through east (east = -west)
        Assert.Equal(171.1, pa < 0 ? pa + 360 : pa, 9);
    }

    [Fact]
    public void TheEquatorialRingsNorthSide_IsTheNearSide()
    {
        // §S8: the ER's point at P.A. 351 deg is the nearest to the observer, at +R sin 43 deg (about +0.45 ly).
        var axis = new Vector3d(Sn1987aRings.AxisSky.West, Sn1987aRings.AxisSky.North, Sn1987aRings.AxisSky.Toward);
        var u = Vector3d.Cross(axis, Vector3d.UnitX).Normalized();
        var v = Vector3d.Cross(axis, u);
        double r = Sn1987aRings.EquatorialRadiusCm, best = double.MinValue, bestPa = 0;
        for (int i = 0; i < 3600; i++)
        {
            double a = 2 * Math.PI * i / 3600;
            var p = r * (Math.Cos(a) * u + Math.Sin(a) * v);
            if (p.Z > best) { best = p.Z; bestPa = Math.Atan2(-p.X, p.Y) * 180 / Math.PI; }
        }
        Assert.Equal(1.0, best / (r * Math.Sin(43 * Math.PI / 180)), 5);
        Assert.InRange(best / Ly, 0.44, 0.46);
        Assert.InRange(bestPa < 0 ? bestPa + 360 : bestPa, 350.9, 351.3);
    }

    [Fact]
    public void SceneMapping_TheSouthRingIsInFront_TheNorthRingBehind()
    {
        // The mapping (west, north, toward the observer) -> scene axes: from the default camera, west is screen right, north is
        // screen up and toward-the-observer is the direction to the eye (SkyFrame).
        var cam = new OrbitCamera { YawDeg = SkyFrame.DefaultYawDeg, PitchDeg = SkyFrame.DefaultPitchDeg };
        var (right, up) = cam.ScreenAxes();
        Assert.True((SkyFrame.Axes.West - right).Length < 1e-12);
        Assert.True((SkyFrame.Axes.North - up).Length < 1e-12);
        Assert.True((SkyFrame.Axes.Toward - cam.Backward).Length < 1e-12);
        // North is up in the scene's own sense too (z up), and the triple is right-handed.
        Assert.True(SkyFrame.Axes.North.Z > 0.9);
        Assert.True(Vector3d.Dot(Vector3d.Cross(SkyFrame.Axes.West, SkyFrame.Axes.North), SkyFrame.Axes.Toward) > 0.999999);

        var rings = Sn1987aRings.Rings;
        var (w, n, z) = Sn1987aRings.AxisSky;
        Vector3d Centre(RingGeometry r) => SkyFrame.ToScene(r.AxisOffsetCm * w, r.AxisOffsetCm * n, r.AxisOffsetCm * z);
        var north = Centre(rings[1]); var south = Centre(rings[2]);
        // The centre's line-of-sight component is its offset times the axis' toward-the-observer component (+0.7314).
        Assert.True(Vector3d.Dot(south, cam.Backward) > 0, "the south ring is in front");
        Assert.True(Vector3d.Dot(north, cam.Backward) < 0, "the north ring is behind");
        Assert.Equal(1.0, Vector3d.Dot(south, cam.Backward) / (rings[2].AxisOffsetCm * z), 9);
        Assert.Equal(1.0, Vector3d.Dot(north, cam.Backward) / (rings[1].AxisOffsetCm * z), 9);
        // On screen: the south ring's centre is below the SN, the north ring's above (the sky's north is up).
        Assert.True(Vector3d.Dot(south, up) < 0);
        Assert.True(Vector3d.Dot(north, up) > 0);
    }

    [Fact]
    public void Rings_OnlyWhenTheOutcomeHasTheEquatorialRing()
    {
        var o = Sn1987a();
        Assert.True(o.HasEquatorialRing);
        var s = new ExplosionScene(o).At(1000 * Day);
        Assert.True(s.ShowRings1987A);
        Assert.False(new ExplosionScene(o with { HasEquatorialRing = false }).At(1000 * Day).ShowRings1987A);
        var crab = PresetCatalog.Get("sn1054");
        Assert.False(new ExplosionScene(TestOutcomes.Engine.Compute(crab.Inputs, crab.Pins)).At(1000 * Day).ShowRings1987A);
        Assert.False(new ExplosionScene(TestOutcomes.Star(15)).At(1000 * Day).ShowRings1987A);
        Assert.False(new ExplosionScene(o).At(1e-3).ShowRings1987A, "the rings are drawn with the debris, not the star");
    }

    [Fact]
    public void Rings_NeverDriveTheFraming_AndTheirNoteWaitsUntilTheViewCanReachThem()
    {
        var o = Sn1987a();
        var without = new ExplosionScene(o with { HasEquatorialRing = false });
        var with = new ExplosionScene(o);
        foreach (double d in new[] { 1.0, 100, 1000, 3000, 8000, 12000 })
        {
            var a = with.At(d * Day);
            Assert.Equal(without.At(d * Day).FrameRadiusCm, a.FrameRadiusCm);
            bool noted = a.Notes.Contains(Sn1987aRings.Note);
            Assert.Equal(a.FrameRadiusCm >= ExplosionScene.RingsNoteMinFrameFraction * Sn1987aRings.EquatorialRadiusCm, noted);
        }
        Assert.DoesNotContain(Sn1987aRings.Note, with.At(1 * Day).Notes);
        Assert.Equal("SN 1987A's rings: observed geometry (preset), not predicted by the lab", Sn1987aRings.Note);
    }

    [Fact]
    public void Rings_HotspotsRiseFromDay3000_PlateauByDay10000_ThenFadeToZeroBy2035()
    {
        Assert.Equal(0f, Sn1987aRings.HotspotBrightness(2999 * Day));
        Assert.Equal(0f, Sn1987aRings.HotspotBrightness(1000 * Day));
        Assert.Equal(0f, Sn1987aRings.HotspotBrightness(0));
        // The rise is signals §6.3's logistic, normalised to the peak: 5% of the plateau at day 3000, 95% by 9000.
        float at3000 = Sn1987aRings.HotspotBrightness(3000 * Day), at5000 = Sn1987aRings.HotspotBrightness(5000 * Day),
              at8000 = Sn1987aRings.HotspotBrightness(8000 * Day), at10000 = Sn1987aRings.HotspotBrightness(10_000 * Day);
        Assert.InRange(at3000, 0.04f, 0.06f);
        Assert.True(at3000 < at5000 && at5000 < at8000 && at8000 <= at10000);
        // It follows the ring's X-ray luminosity itself on the way up.
        double peak = CsmInteractionModel.RingXrayLuminosityErgS(10_000 * Day);
        Assert.Equal(CsmInteractionModel.RingXrayLuminosityErgS(5000 * Day) / peak, at5000, 5);
        // Near the peak by day 8000 (the 2009 plateau); 1 at the end of the plateau, 10,000 d; fading after, gone by 2035.
        Assert.InRange(at8000, 0.85f, 1f);
        Assert.Equal(1f, at10000, 5);
        Assert.True(Sn1987aRings.HotspotBrightness(12_000 * Day) < at10000);
        Assert.True(Sn1987aRings.HotspotBrightness(12_000 * Day) > Sn1987aRings.HotspotBrightness(16_000 * Day));
        Assert.Equal(0f, Sn1987aRings.HotspotBrightness(Sn1987aRings.FadeEndDays * Day));
        Assert.Equal(0f, Sn1987aRings.HotspotBrightness(100 * PhysicalConstants.Year));
        // The maximum over the whole timeline is 1 and is reached in the plateau (8,000-10,000 d).
        float best = 0; double bestDay = 0;
        for (double d = 0; d <= 20_000; d += 10)
        {
            float b = Sn1987aRings.HotspotBrightness(d * Day);
            Assert.InRange(b, 0f, 1f);
            if (b > best) { best = b; bestDay = d; }
        }
        Assert.Equal(1f, best, 5);
        Assert.InRange(bestDay, 8000, 10_000);
    }

    [Fact]
    public void Rings_AreDrawnOnlyOnceTheViewIsWideEnoughToContainThem()
    {
        // The camera sits 1 / tan(fov/2) = 2.414 half-heights from the centre; a ring bigger than that would wrap round it.
        double d = 1 / Math.Tan(45 * Math.PI / 360);
        Assert.Equal(1.0, TorusRingRenderer.Visibility(0.5 * d, d));
        Assert.Equal(1.0, TorusRingRenderer.Visibility(0.9 * d, d), 12);
        Assert.Equal(0.0, TorusRingRenderer.Visibility(1.2 * d, d));
        Assert.Equal(0.0, TorusRingRenderer.Visibility(7.6, d));              // SN 1987A's equatorial ring at 1,000 d (H = 8e16 cm)
        Assert.InRange(TorusRingRenderer.Visibility(1.05 * d, d), 0.4, 0.6);
        // At the day-1,880 view (H = 5.26e17 cm, the 0.5 ly rung) the equatorial ring is fully drawn and the outer rings are not.
        double h = 5.26e17;
        Assert.Equal(1.0, TorusRingRenderer.Visibility(Sn1987aRings.EquatorialRadiusCm / h, d));
        Assert.Equal(0.0, TorusRingRenderer.Visibility((Sn1987aRings.Rings[1].RadiusCm - Sn1987aRings.Rings[1].AxisOffsetCm) / h, d));
        Assert.Equal(1, TorusRingRenderer.BeadAngle(0) + 1);
        Assert.Equal(2 * Math.PI / 24, TorusRingRenderer.BeadAngle(1), 12);
    }

    [Fact]
    public void SceneState_CarriesTheHotspotBrightness()
    {
        var scene = new ExplosionScene(Sn1987a());
        Assert.Equal(0f, scene.At(1000 * Day).RingBrightness);
        Assert.Equal(Sn1987aRings.HotspotBrightness(8000 * Day), scene.At(8000 * Day).RingBrightness);
        Assert.True(scene.At(8000 * Day).RingBrightness > 0.85f);
        Assert.Equal(24, Sn1987aRings.HotspotCount);
    }

    [Fact]
    public void Notes_DiscloseTheHotspotTiming_AndTheSchematicFade()
    {
        var scene = new ExplosionScene(Sn1987a());
        Assert.DoesNotContain(scene.At(1000 * Day).Notes, n => n.StartsWith("The hotspots'", StringComparison.Ordinal));
        var at3000 = scene.At(3000 * Day).Notes;
        var timing = Assert.Single(at3000, n => n.StartsWith("The hotspots' timing is observed", StringComparison.Ordinal));
        Assert.Contains("#48", timing);
        Assert.DoesNotContain(ExplosionScene.HotspotFadeNote, at3000);
        // The day it names is where the drawn forward shock reaches the ring (about 6,280).
        var day = double.Parse(System.Text.RegularExpressions.Regex.Match(timing, @"day (\d+)").Groups[1].Value);
        Assert.InRange(day, 6200, 6350);
        double ring = Sn1987aRings.EquatorialRadiusCm;
        Assert.True(ShockStructureModel.At(Sn1987a(), (day - 20) * Day)!.ForwardCm < ring);
        Assert.True(ShockStructureModel.At(Sn1987a(), (day + 20) * Day)!.ForwardCm > ring);
        Assert.DoesNotContain(ExplosionScene.HotspotFadeNote, scene.At(9000 * Day).Notes);
        Assert.Contains(ExplosionScene.HotspotFadeNote, scene.At(12_000 * Day).Notes);
    }

    [Fact]
    public void CrabNote_SaysTheDrawnOuterShellIsPredictedButNeverDetected()
    {
        var crab = PresetCatalog.Get("sn1054");
        var o = TestOutcomes.Engine.Compute(crab.Inputs, crab.Pins);
        Assert.Contains(ExplosionScene.CrabShellNote, new ExplosionScene(o).At(954 * PhysicalConstants.Year).Notes);
        Assert.Contains("#31", ExplosionScene.CrabShellNote);
        Assert.DoesNotContain(ExplosionScene.CrabShellNote, new ExplosionScene(Sn1987a()).At(954 * PhysicalConstants.Year).Notes);
        Assert.DoesNotContain(ExplosionScene.CrabShellNote, new ExplosionScene(TestOutcomes.Star(15)).At(954 * PhysicalConstants.Year).Notes);
    }
}
