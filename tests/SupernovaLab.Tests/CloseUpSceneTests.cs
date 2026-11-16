using OpenTK.Mathematics;
using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Rendering;
using static SupernovaLab.Tests.CompactResearch;

namespace SupernovaLab.Tests;

/// <summary>
/// Plan Task 4: what the Close-up draws (spec §2.3, §6.1), the environment's refresh rule (ruling P3-6), the lensing levels
/// (ruling P3-9) and the cube-map faces, all without OpenGL.
/// </summary>
public class CloseUpSceneTests
{
    private static Outcome Lab(double mass) => TestOutcomes.Engine.Compute(LabInputs.Default with { InitialMass = mass });

    private static CloseUpScene SceneFor(Outcome outcome, double spin = 0)
        => new(outcome, ObserverSetup.Default, runNumber: 1, placeholderSpin: spin);

    private static CloseUpScene SceneFor(string presetId)
    {
        var preset = PresetCatalog.Get(presetId);
        var outcome = TestOutcomes.Engine.Compute(preset.Inputs, preset.Pins);
        return SceneFor(outcome);
    }

    // ---- Brief tests (plan Task 4, Step 1) ----

    [Theory, InlineData("sn2011fe"), InlineData("tycho")]        // Type Ia: destroyed
    public void NoRemnant_Destroyed(string id) => Assert.Equal(CloseUpScene.DestroyedMessage, SceneFor(id).At(1e7).NoRemnantMessage);

    [Fact]
    public void WhiteDwarfOutcome_GetsWhiteDwarfMessage() => Assert.Equal(CloseUpScene.WhiteDwarfMessage, SceneFor(Lab(mass: 5)).At(1).NoRemnantMessage);

    [Fact]
    public void EnvironmentCapture_RefreshRule()   // ruling P3-6, as a pure function
    {
        Assert.False(EnvironmentCapture.NeedsRefresh(prevLogT: 3.000, logT: 3.004, sameOutcome: true, wallSinceLast: 1));
        Assert.True(EnvironmentCapture.NeedsRefresh(3.000, 3.006, true, 1));
        Assert.False(EnvironmentCapture.NeedsRefresh(3.0, 4.0, true, wallSinceLast: 0.2));   // ≤ 4 per s
    }

    // ---- Further checks ----

    [Fact]
    public void Labels_AreTheExactStrings()
    {
        Assert.Equal("Camera where no telescope could be", CloseUpScene.NoTelescopeNote);
        Assert.Equal("No compact remnant: the star was destroyed completely.", CloseUpScene.DestroyedMessage);
        Assert.Equal("This star leaves a white dwarf. The white-dwarf close-up is not part of the lab.", CloseUpScene.WhiteDwarfMessage);
    }

    [Fact]
    public void NoSupernova_StarNotYetDead_AndPairInstability_GetTheirMessages()
    {
        var young = TestOutcomes.Engine.Compute(LabInputs.Default with { InitialMass = 0.5 });
        Assert.Equal(Fate.NotDeadYet, young.Fate);
        Assert.Equal(CloseUpScene.WhiteDwarfMessage, SceneFor(young).At(1).NoRemnantMessage);
        var pisn = TestOutcomes.Custom(SupernovaType.Pisn, 1e52, 100, 10, 1e13, Remnant.None, Fate.PairInstabilitySupernova);
        Assert.Equal(CloseUpScene.DestroyedMessage, SceneFor(pisn).At(1).NoRemnantMessage);
        var s = SceneFor(pisn).At(1);
        Assert.Equal(CloseUpEra.None, s.Era);
        Assert.False(s.IsLensed);
        Assert.Empty(s.Readouts);
    }

    [Theory, InlineData("n6946bh1"), InlineData("sn1998bw")]
    public void BlackHolePresets_GiveThePlaceholderBlackHole(string id)
    {
        var s = SceneFor(id).At(10);
        Assert.Equal(CloseUpEra.BlackHole, s.Era);
        Assert.Null(s.NoRemnantMessage);
        Assert.True(s.IsLensed);
        Assert.Equal(0, s.Spin);
        Assert.Equal("Black hole", s.Title);
        Assert.Contains(CloseUpScene.PlaceholderBlackHoleNote, s.Notes);
    }

    [Fact]
    public void BlackHoleReadouts_EqualTheExactFormulas()   // criterion 2's numbers, as the readouts show them
    {
        var outcome = TestOutcomes.Custom(SupernovaType.None, null, null, null, 1e13, TestOutcomes.BlackHole(10, 1), Fate.FailedSupernova);
        var s = SceneFor(outcome).At(5);
        double rgKm = BlackHoleOptics.GravitationalRadiusCm(10) / 1e5;
        Assert.Equal(C7.GravitationalRadiusKmPerMsun * 10, rgKm, 3);
        var byLabel = s.Readouts.ToDictionary(r => r.Label);
        Assert.Equal(10, byLabel["Mass"].Value.Value);
        Assert.Equal(4 * rgKm, byLabel["Horizon diameter"].Value.Value, 9);                 // 2 r₊, r₊ = 2 GM/c²
        Assert.InRange(byLabel["Shadow diameter"].Value.Value, C7.TenSolarMassShadowKmLow, C7.TenSolarMassShadowKmHigh);
        Assert.Equal(2 * Math.Sqrt(27) * rgKm, byLabel["Shadow diameter"].Value.Value, 9);
        Assert.All(s.Readouts, r => Assert.False(string.IsNullOrEmpty(r.Value.Source.Citation)));
        Assert.Equal(CloseUpScene.BlackHoleFrameShadowRadii * Math.Sqrt(27) * rgKm, s.FrameRadiusKm, 9);
        Assert.Equal("153 km", byLabel["Shadow diameter"].ValueText);
    }

    [Fact]
    public void BenchSpin_GivesASpinReadout_AndASmallerMeanShadow()
    {
        var outcome = TestOutcomes.Custom(SupernovaType.None, null, null, null, 1e13, TestOutcomes.BlackHole(10, 1), Fate.FailedSupernova);
        var still = SceneFor(outcome).At(5);
        var spinning = SceneFor(outcome, spin: 0.9).At(5);
        Assert.Equal(0.9, spinning.Spin);
        Assert.Contains(spinning.Readouts, r => r.Label == "Spin" && r.Value.Confidence == Core.Units.Confidence.Speculative);
        Assert.Contains(CloseUpScene.BenchSpinNote, spinning.Notes);
        double d0 = still.Readouts.Single(r => r.Label == "Shadow diameter").Value.Value;
        double d9 = spinning.Readouts.Single(r => r.Label == "Shadow diameter").Value.Value;
        Assert.True(d9 < d0);
        Assert.Equal(2 * BlackHoleOptics.HorizonRadius(0.9) * BlackHoleOptics.GravitationalRadiusCm(10) / 1e5,
            spinning.Readouts.Single(r => r.Label == "Horizon diameter").Value.Value, 9);
    }

    [Fact]
    public void NeutronStar_IsLensedWithItsSurface()
    {
        var outcome = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.07, 3e13);   // a 1.4 M☉, 12 km neutron star
        var s = SceneFor(outcome).At(100);
        Assert.Equal(CloseUpEra.NeutronStar, s.Era);
        Assert.True(s.IsLensed);
        Assert.Equal(12, s.SurfaceRadiusKm);
        Assert.Equal(1.4, s.MassMsun);
        Assert.Equal(CloseUpScene.NeutronStarFrameRadii * 12, s.FrameRadiusKm);
        Assert.Equal(new[] { "Mass", "Radius" }, s.Readouts.Select(r => r.Label));
    }

    [Fact]
    public void EnvironmentCapture_RefreshRule_FirstCaptureAndNewOutcome()
    {
        Assert.True(EnvironmentCapture.NeedsRefresh(double.NaN, 3.0, sameOutcome: true, wallSinceLast: double.PositiveInfinity));
        Assert.True(EnvironmentCapture.NeedsRefresh(3.0, 3.0, sameOutcome: false, wallSinceLast: 0.25));
        Assert.False(EnvironmentCapture.NeedsRefresh(3.0, 3.0, sameOutcome: false, wallSinceLast: 0.24));
        Assert.True(EnvironmentCapture.HasChanged(3.0, 3.0, sameOutcome: false));
        Assert.True(EnvironmentCapture.NeedsRefresh(3.0, 2.99, true, 1));   // backwards too
        Assert.Equal(256, EnvironmentCapture.FaceSize);
    }

    /// <summary>
    /// The faces are the GL specification's cube-map orientations: for each major axis, the camera's screen-right (forward × up) and
    /// screen-up are the directions in which the face's s and t coordinates grow (e.g. +x: s along −z, t along −y).
    /// </summary>
    [Fact]
    public void EnvironmentFaces_MatchTheGlCubeMapConvention()
    {
        // (major axis, s grows along, t grows along) from the GL 4.1 specification, table 3.19.
        var spec = new (Vector3d Major, Vector3d S, Vector3d T)[]
        {
            (Vector3d.UnitX, -Vector3d.UnitZ, -Vector3d.UnitY),
            (-Vector3d.UnitX, Vector3d.UnitZ, -Vector3d.UnitY),
            (Vector3d.UnitY, Vector3d.UnitX, Vector3d.UnitZ),
            (-Vector3d.UnitY, Vector3d.UnitX, -Vector3d.UnitZ),
            (Vector3d.UnitZ, Vector3d.UnitX, -Vector3d.UnitY),
            (-Vector3d.UnitZ, -Vector3d.UnitX, -Vector3d.UnitY),
        };
        for (int i = 0; i < 6; i++)
        {
            var (forward, up) = EnvironmentCapture.Faces[i];
            var cam = new OrbitCamera();
            cam.Orient(-forward, up);
            var (right, screenUp) = cam.ScreenAxes();
            Assert.Equal(spec[i].Major, -cam.Backward);
            Assert.True((right - spec[i].S).Length < 1e-12, $"face {i}: right {right}");
            Assert.True((screenUp - spec[i].T).Length < 1e-12, $"face {i}: up {screenUp}");
        }
    }

    [Fact]
    public void OrbitCamera_WithoutOrient_IsUnchanged()
    {
        var cam = new OrbitCamera { YawDeg = 30, PitchDeg = 20 };
        var (right, up) = cam.ScreenAxes();
        Assert.Equal(0, right.Z, 12);              // screen-right stays horizontal: the +z up hint
        Assert.True(up.Z > 0);
    }

    [Fact]
    public void LensingLevels_TargetSizesAndSteps()
    {
        Assert.Equal((1350, 960), LensingLevels.TargetSize(1350, 960, LensingLevel.Full));
        Assert.Equal((675, 480), LensingLevels.TargetSize(1350, 960, LensingLevel.Half));
        Assert.Equal((338, 240), LensingLevels.TargetSize(1350, 960, LensingLevel.Quarter));
        Assert.Equal((1, 1), LensingLevels.TargetSize(1, 1, LensingLevel.Quarter));
        Assert.Equal("1/2", LensingLevels.Text(LensingLevel.Half));
        Assert.Null(LensingLevels.NextDown(LensingLevel.Quarter));
    }

    [Fact]
    public void LensingGovernor_StepsDownWhenTheGpuTimeStaysOver12ms()   // ruling P3-9
    {
        var g = new LensingGovernor(LensingLevel.Full);
        for (int k = 0; k < 179; k++) Assert.False(g.Update(0.013, 1.0 / 60));   // just under 3 s at 13 ms
        Assert.True(g.Update(0.013, 1.0 / 60));
        Assert.Equal(LensingLevel.Half, g.Level);
        for (int k = 0; k < 180; k++) g.Update(0.011, 1.0 / 60);                // 11 ms: stays
        Assert.Equal(LensingLevel.Half, g.Level);
        for (int k = 0; k < 400; k++) g.Update(0.05, 1.0 / 60);                 // never below 1/4
        Assert.Equal(LensingLevel.Quarter, g.Level);
        g.Reset(LensingLevel.Full);
        Assert.Equal(LensingLevel.Full, g.Level);
        Assert.False(g.Update(double.NaN, 1.0 / 60));
    }

    [Fact]
    public void EscapeRadius_IsRulingP37s()
    {
        Assert.Equal(60, BlackHoleOptics.EscapeRadiusFor(36));
        Assert.Equal(120, BlackHoleOptics.EscapeRadiusFor(100), 9);
    }

    [Fact]
    public void DistanceRings_RunDownToMetres()   // spec §2.2: the ring ladder starts in metres
    {
        var rungs = DistanceRings.Rungs(500 * 100 / 0.9);   // a 500 m outer ring
        Assert.Equal("500 m", rungs[0].Label);
        Assert.Equal("200 m", rungs[1].Label);
        Assert.Equal("100 m", rungs[2].Label);
        var km = DistanceRings.Rungs(2e5 / 0.9);              // a 2 km outer ring
        Assert.Equal("2 km", km[0].Label);
        Assert.Equal("1 km", km[1].Label);
        Assert.Equal("0.5 km", km[2].Label);   // a view keeps its outer ring's unit, as the main view does
        var director = new CameraDirector();
        director.Jump(30e2);                                  // a 30 m framing radius
        Assert.Equal(50e2, director.OuterRingCm, 6);
    }

    // ---- Task 4b: the cached ray map (ruling R-P3-10) and the light-bending detail (ruling R-P3-9) ----

    private static LensingUniforms Rays(float yaw = 0, float spin = 0.9f, float surface = 0, Vector3 color = default)
        => new(spin, new Vector3(36 * MathF.Cos(yaw), 36 * MathF.Sin(yaw), 0), new Vector3(-MathF.Cos(yaw), -MathF.Sin(yaw), 0),
            new Vector3(-MathF.Sin(yaw), MathF.Cos(yaw), 0), Vector3.UnitZ, new Vector2(0.5f, 0.4f), 60, surface, color);

    [Fact]
    public void RayMap_TracesOnce_ThenOnlySamples_WhileNothingButTimeChanges()   // R-P3-10: time is not an input of the map
    {
        var p = new RayMapPlanner();
        var first = p.Next(Rays(), 900, 600, LensingLevel.Full, LensingLevel.Full);
        Assert.Equal(RayMapWork.Trace, first.Work);
        Assert.Equal((900, 600), (first.Width, first.Height));
        Assert.Equal(LensingLevel.Full, p.ShownLevel);
        for (int k = 0; k < 10; k++) Assert.Equal(RayMapWork.None, p.Next(Rays(), 900, 600, LensingLevel.Full, LensingLevel.Full).Work);
        // A neutron star's surface colour is applied when the map is sampled: it changes nothing traced.
        Assert.Equal(RayMapWork.None, p.Next(Rays(color: Vector3.One), 900, 600, LensingLevel.Full, LensingLevel.Full).Work);
    }

    [Fact]
    public void RayMap_RetracesWhenTheCameraSpinSurfaceViewportOrLevelChanges()
    {
        var cases = new (LensingUniforms U, int W, int H, LensingLevel L)[]
        {
            (Rays(yaw: 0.01f), 900, 600, LensingLevel.Half),   // the camera moved
            (Rays(spin: 0.5f), 900, 600, LensingLevel.Half),   // the spin
            (Rays(surface: 5), 900, 600, LensingLevel.Half),   // a neutron star's surface radius
            (Rays() with { Grid = true }, 900, 600, LensingLevel.Half),
            (Rays(), 901, 600, LensingLevel.Half),             // the viewport
            (Rays(), 900, 600, LensingLevel.Quarter),          // the level (coarser: traced at once)
        };
        foreach (var (u, w, h, level) in cases)
        {
            var p = new RayMapPlanner();
            p.Next(Rays(), 900, 600, LensingLevel.Half, LensingLevel.Half);
            var step = p.Next(u, w, h, level, level);
            Assert.Equal(RayMapWork.Trace, step.Work);
            Assert.Equal(level, step.Level);
            Assert.Equal(LensingLevels.TargetSize(w, h, level), (step.Width, step.Height));
        }
    }

    [Fact]
    public void RayMap_TracesAtTheMovingLevelWhileTheCameraMoves_ThenRefinesInStripsWhenItStops()
    {
        var p = new RayMapPlanner();
        for (int k = 0; k < 5; k++)
        {
            var moving = p.Next(Rays(yaw: 0.01f * k), 900, 600, LensingLevel.Quarter, LensingLevel.Full);
            Assert.Equal(RayMapWork.Trace, moving.Work);
            Assert.Equal(LensingLevel.Quarter, moving.Level);
        }
        var still = Rays(yaw: 0.04f);
        int rows = 0, frames = 0;
        RayMapStep step;
        do
        {
            step = p.Next(still, 900, 600, LensingLevel.Quarter, LensingLevel.Full);
            Assert.Equal(RayMapWork.RefineStrip, step.Work);
            Assert.Equal(LensingLevel.Full, step.Level);
            Assert.Equal(rows, step.FirstRow);
            Assert.True(step.Pixels <= p.PixelsPerFrame, "a strip stays inside one frame's budget");
            Assert.Equal(LensingLevel.Quarter, step.Completes ? LensingLevel.Quarter : p.ShownLevel);   // the coarse map stays shown
            rows += step.Rows;
            frames++;
        } while (!step.Completes);
        Assert.Equal(600, rows);
        Assert.True(frames > 1);
        Assert.Equal(LensingLevel.Full, p.ShownLevel);
        Assert.Equal(RayMapWork.None, p.Next(still, 900, 600, LensingLevel.Quarter, LensingLevel.Full).Work);
    }

    [Fact]
    public void RayMap_AMoveDuringARefineStartsOver()
    {
        var p = new RayMapPlanner();
        p.Next(Rays(), 900, 600, LensingLevel.Quarter, LensingLevel.Full);
        Assert.Equal(RayMapWork.RefineStrip, p.Next(Rays(), 900, 600, LensingLevel.Quarter, LensingLevel.Full).Work);
        Assert.True(p.IsRefining);
        var moved = p.Next(Rays(yaw: 0.2f), 900, 600, LensingLevel.Quarter, LensingLevel.Full);
        Assert.Equal(RayMapWork.Trace, moved.Work);
        Assert.False(p.IsRefining);
        Assert.Equal(0, p.Next(Rays(yaw: 0.2f), 900, 600, LensingLevel.Quarter, LensingLevel.Full).FirstRow);   // the refine restarts
        p.Invalidate();
        Assert.Equal(RayMapWork.Trace, p.Next(Rays(yaw: 0.2f), 900, 600, LensingLevel.Quarter, LensingLevel.Full).Work);
    }

    [Fact]
    public void RayMap_StripBudget_FollowsTheMeasuredCostPerPixel()
    {
        Assert.Equal(RayMapPlanner.DefaultPixelsPerFrame, new RayMapPlanner().PixelsPerFrame);
        Assert.Equal(40_000, RayMapPlanner.PixelsFor(RayMapPlanner.RefineBudgetS / 40_000));
        Assert.Equal(RayMapPlanner.MinPixelsPerFrame, RayMapPlanner.PixelsFor(1));   // a very slow GPU still progresses
        Assert.Equal(RayMapPlanner.DefaultPixelsPerFrame, RayMapPlanner.PixelsFor(double.NaN));
        var p = new RayMapPlanner();
        for (int k = 0; k < 30; k++)
        {
            p.ReportGpuTime(0, 0.001);                    // frames that only sample: 1 ms of other work
            p.ReportGpuTime(100_000, 0.001 + 0.010);      // 10 ms for 100,000 rays: 1e-7 s per ray
        }
        Assert.InRange(p.PixelsPerFrame, 38_000, 42_000);   // 4 ms / 1e-7 s
    }

    [Theory]
    [InlineData(LensingLevel.Full, false, "Light bending: full resolution (your setting)")]
    [InlineData(LensingLevel.Half, false, "Light bending: half resolution (your setting)")]
    [InlineData(LensingLevel.Quarter, false, "Light bending: quarter resolution (your setting)")]
    [InlineData(LensingLevel.Full, true, "Light bending: full resolution (automatic)")]
    [InlineData(LensingLevel.Quarter, true, "Light bending: quarter resolution (automatic)")]
    public void LensingDetail_Note_IsTheExactString(LensingLevel shown, bool automatic, string expected)
        => Assert.Equal(expected, LensingDetails.Note(shown, automatic));

    [Fact]
    public void LensingDetail_Levels_AutoIsTheGovernorMovingAndFullStill()
    {
        Assert.Equal(LensingLevel.Half, LensingDetails.MovingLevel(LensingDetail.Auto, governed: LensingLevel.Half));
        Assert.Equal(LensingLevel.Full, LensingDetails.StillLevel(LensingDetail.Auto));
        foreach (var (d, l) in new[] { (LensingDetail.Full, LensingLevel.Full), (LensingDetail.Half, LensingLevel.Half),
                     (LensingDetail.Quarter, LensingLevel.Quarter) })
        {
            Assert.Equal(l, LensingDetails.MovingLevel(d, governed: LensingLevel.Quarter));
            Assert.Equal(l, LensingDetails.StillLevel(d));
        }
        Assert.Equal(LensingDetail.Auto, default(LensingDetail));
        Assert.Equal(LensingDetail.Half, LensingDetails.Parse("half"));
        Assert.Equal(LensingDetail.Auto, LensingDetails.Parse("Ultra"));
        Assert.Equal(LensingDetail.Auto, LensingDetails.Parse(null));
        Assert.Equal(LensingDetail.Auto, LensingDetails.Parse("7"));
    }

    /// <summary>
    /// Ruling proposal R-P3-12 (Task 4b): Auto holds 50 fps (spec §2.5). With the two GL views drawn one after the other on the UI
    /// thread, the Close-up's GPU time adds to the frame, so a level can stay under 12 ms and still hold the window below 50 fps.
    /// The governor therefore also steps down when the frames it is fed average more than 20 ms apart while the lensing costs more
    /// than 2 ms of them; a slow frame that the lensing is not part of (under 2 ms) never steps it.
    /// </summary>
    [Fact]
    public void LensingGovernor_AlsoStepsDownBelow50fps_WhenTheLensingIsPartOfTheFrame()
    {
        var g = new LensingGovernor(LensingLevel.Full);
        for (int k = 0; k < 130; k++) g.Update(0.0115, 0.025);   // 11.5 ms (under 12) but 40 fps: steps down
        Assert.Equal(LensingLevel.Half, g.Level);
        var slow = new LensingGovernor(LensingLevel.Half);
        for (int k = 0; k < 400; k++) slow.Update(0.0015, 0.030);   // 33 fps, but the lensing is only 1.5 ms of it: stays
        Assert.Equal(LensingLevel.Half, slow.Level);
        var fine = new LensingGovernor(LensingLevel.Half);
        for (int k = 0; k < 400; k++) fine.Update(0.006, 0.019);    // 53 fps: stays
        Assert.Equal(LensingLevel.Half, fine.Level);
    }
}
