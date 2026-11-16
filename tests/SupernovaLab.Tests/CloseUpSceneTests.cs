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
        Assert.Equal(new[] { "Mass", "Radius", "Spin", "Field", "Surface", "Age", "Kick" }, s.Readouts.Select(r => r.Label));
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

    /// <summary>
    /// Ruling R-P3-14: at the end of a window the governor predicts one level down (a quarter of the rays) as the interval less 3/4 of
    /// the lensing's GPU time; if that would still miss 50 fps, or 12 ms of GPU time, it steps two levels at once.
    /// </summary>
    [Fact]
    public void LensingGovernor_StepsTwoLevels_WhenOneWouldStillMissTheTarget()
    {
        static LensingGovernor Run(LensingLevel start, double gpuMs, double intervalMs)
        {
            var g = new LensingGovernor(start);
            for (int k = 0; k * intervalMs < 3000 + 1e-6; k++) g.Update(gpuMs / 1000, intervalMs / 1000);
            return g;
        }
        // Task 4b's 1920x1080 Full orbit (18.5 fps: 54 ms apart, 33 ms of lensing): 54 - 24.75 = 29.25 ms predicted: straight to 1/4.
        Assert.Equal(LensingLevel.Quarter, Run(LensingLevel.Full, 33, 54).Level);
        // Its 1366x768 Full orbit (22.6 fps: 44 ms, 30.4 ms): 44 - 22.8 = 21.2 ms, still over 20: 1/4 too.
        Assert.Equal(LensingLevel.Quarter, Run(LensingLevel.Full, 30.4, 44).Level);
        // A borderline case: 30 ms apart with 15 ms of lensing predicts 18.75 ms at 1/2 (53 fps): one level.
        Assert.Equal(LensingLevel.Half, Run(LensingLevel.Full, 15, 30).Level);
        // A very slow card that is the whole frame: 60 ms of lensing, 1/2 would still be 15 ms of GPU: two levels.
        Assert.Equal(LensingLevel.Quarter, Run(LensingLevel.Full, 60, 16.7).Level);
        // Already under the target: no step at all.
        Assert.Equal(LensingLevel.Full, Run(LensingLevel.Full, 8, 16.7).Level);
        // From 1/2 there is only one level left.
        Assert.Equal(LensingLevel.Quarter, Run(LensingLevel.Half, 33, 54).Level);
    }

    [Fact]
    public void RayMap_NamesWhyItTracedAWholeMap()
    {
        var p = new RayMapPlanner();
        p.Next(Rays(), 900, 600, LensingLevel.Full, LensingLevel.Full);
        Assert.Equal(RayMapTraceReason.First, p.LastTraceReason);
        p.Next(Rays(), 901, 600, LensingLevel.Full, LensingLevel.Full);
        Assert.Equal(RayMapTraceReason.Viewport, p.LastTraceReason);
        p.Next(Rays(yaw: 0.1f), 901, 600, LensingLevel.Full, LensingLevel.Full);
        Assert.Equal(RayMapTraceReason.Rays, p.LastTraceReason);
        p.Next(Rays(yaw: 0.1f), 901, 600, LensingLevel.Quarter, LensingLevel.Quarter);
        Assert.Equal(RayMapTraceReason.CoarserLevel, p.LastTraceReason);
    }

    [Fact]
    public void RayMap_TheSurfacesShadingIsNotTraced()   // plan Task 6: the spin phase turns the surface's markings every frame
    {
        var p = new RayMapPlanner();
        p.Next(Rays(surface: 5), 900, 600, LensingLevel.Full, LensingLevel.Full);
        var turned = Rays(surface: 5) with { SurfacePhaseRad = 1.3f, SurfaceTiltRad = 1.08f, SurfaceCapCos = 0.99f, SurfaceMarkings = true };
        Assert.Equal(RayMapWork.None, p.Next(turned, 900, 600, LensingLevel.Full, LensingLevel.Full).Work);
    }

    // ---- Plan Task 6: the neutron star (spec §4.1, §4.3, §4.5, §4.6) ----

    /// <summary>The Crab's age in 1991, when its 33.39 ms period was measured (the preset's fact).</summary>
    private const double CrabAgeS = 2.9568e10;

    private const double Year = Core.Units.PhysicalConstants.Year;

    private static CloseUpScene PresetScene(string presetId, int run = 1)
    {
        var preset = PresetCatalog.Get(presetId);
        return new CloseUpScene(TestOutcomes.Engine.Compute(preset.Inputs, preset.Pins), preset.Observer, run);
    }

    private static Outcome Crab()
    {
        var preset = PresetCatalog.Get("sn1054");
        return TestOutcomes.Engine.Compute(preset.Inputs, preset.Pins);
    }

    [Fact]
    public void NeutronStarEra_ReadoutsAndNotes()
    {
        var st = SceneFor("sn1054").At(CrabAgeS);
        Assert.Equal(CloseUpEra.NeutronStar, st.Era);
        Assert.StartsWith("Close-up: Neutron star (pulsar, 33", $"Close-up: {st.Title}");
        Assert.Equal(new[] { "Mass", "Radius", "Spin", "Field", "Surface", "Age", "Kick" }, st.Readouts.Select(r => r.Label));
        Assert.Contains(st.Notes, n => n.Contains("mostly in X-rays"));
        Assert.Contains("Shown 60× slower", st.SpeedLabel);
    }

    [Fact]
    public void NeutronStar_ColourIsBlackbodyOfSurfaceTemperature()
    {
        var o = Crab();
        foreach (double t in new[] { 100.0, 1e5, 10 * Year, CrabAgeS })
        {
            var st = SceneFor(o).At(t);
            double kelvin = NeutronStarModel.SurfaceTemperature(o, t).Value;
            Assert.Equal(BlackbodyColor.FromTemperature(kelvin), st.SurfaceColor);
            Assert.Equal(kelvin, st.NeutronStar!.SurfaceTemperatureK.Value);
        }
        var notes = SceneFor(o).At(CrabAgeS).Notes;
        Assert.Contains(notes, n => n.Contains("tone-mapped", StringComparison.Ordinal));   // spec C12
        Assert.Contains(notes, n => n.Contains("blackbody", StringComparison.Ordinal));
    }

    [Fact]
    public void NeutronStar_ReadoutsCarryTheirQuantities_WithConfidenceAndSource()
    {
        var o = Crab();
        var st = SceneFor(o).At(CrabAgeS);
        var r = st.Readouts.ToDictionary(x => x.Label);
        Assert.Same(o.Remnant.Mass, r["Mass"].Value);
        Assert.Same(o.Remnant.RadiusKm, r["Radius"].Value);
        Assert.Same(o.Remnant.MagneticFieldG, r["Field"].Value);
        Assert.Same(st.NeutronStar!.SurfaceTemperatureK, r["Surface"].Value);
        Assert.Equal(st.NeutronStar.PeriodS, r["Spin"].Value.Value);
        Assert.Equal("s", r["Spin"].Value.Unit);
        Assert.Equal(st.NeutronStar.PeriodDerivative, r["Spin"].Second!.Value);
        Assert.Equal(CrabAgeS, r["Age"].Value.Value);
        Assert.Equal(o.Remnant.KickKmS!.Value, r["Kick"].Value.Value);
        Assert.Equal(st.NeutronStar.OffsetCm.Length, r["Kick"].Second!.Value, 1e-9 * r["Kick"].Second!.Value);
        Assert.All(st.Readouts, x => Assert.False(string.IsNullOrEmpty(x.Value.Source.Citation)));
        Assert.All(st.Readouts.Where(x => x.Second is not null), x => Assert.False(string.IsNullOrEmpty(x.Second!.Source.Citation)));
        // The column's text: the spin with its Pdot, the kick with its distance, the field in powers of ten, the age in years.
        Assert.StartsWith("33.", r["Spin"].ValueText);
        Assert.Contains("ms", r["Spin"].ValueText);
        Assert.Contains("Ṗ", r["Spin"].ValueText);
        Assert.Contains("× 10¹²", r["Field"].ValueText);
        Assert.Contains("km/s", r["Kick"].ValueText);
        Assert.Contains("pc", r["Kick"].ValueText);
        Assert.Contains("years", r["Age"].ValueText);
        Assert.Contains("MK", r["Surface"].ValueText);
        // The Crab's measured kick direction: the line-of-sight part is unknown (spec §4.6).
        Assert.Contains("line-of-sight", r["Kick"].ToolTip);
    }

    [Fact]
    public void NeutronStar_EarlyTemperature_IsShownAsItsBand()
    {
        var st = SceneFor(Crab()).At(5 * Year);   // before 30 yr: the early-cooling band (spec §4.1)
        var surface = st.Readouts.Single(x => x.Label == "Surface");
        Assert.Equal(Core.Units.Confidence.ModelDependent, surface.Value.Confidence);
        Assert.NotNull(surface.Value.Low);
        Assert.Contains("–", surface.ValueText);
    }

    [Fact]
    public void CasA_SurfaceTooltip_CarriesTheComparison_OnlyNearItsObservedAge()   // #54
    {
        var at340 = PresetScene("casa").At(340 * Year).Readouts.Single(x => x.Label == "Surface");
        Assert.Contains("#54", at340.ToolTip);
        Assert.Contains("Heinke", at340.ToolTip);
        var at900 = PresetScene("casa").At(900 * Year).Readouts.Single(x => x.Label == "Surface");
        Assert.DoesNotContain("#54", at900.ToolTip ?? "");
    }

    [Fact]
    public void Beams_FromTheMagnetosphereFormation_WithTheNoteOnTheMainView()   // spec C6
    {
        var o = Crab();
        double forms = Magnetosphere.MagnetosphereFormsS(o);
        Assert.Equal(30, forms);
        var before = SceneFor(o).At(forms * 0.5);
        var after = SceneFor(o).At(forms * 2);
        Assert.False(before.ShowBeams);
        Assert.True(after.ShowBeams);
        Assert.Contains(CloseUpScene.BeamsFromBirthNote, after.Notes);
        Assert.Equal("Beams exist from birth; the main view draws them once the pulses escape the debris", CloseUpScene.BeamsFromBirthNote);
        Assert.StartsWith("Proto-neutron star", before.Title);
        Assert.StartsWith("Neutron star (pulsar", after.Title);
    }

    [Fact]
    public void Geometry_IsTheMagnetospheres()
    {
        var o = Crab();
        var obs = PresetCatalog.Get("sn1054").Observer;
        var st = new CloseUpScene(o, obs, 1).At(CrabAgeS);
        Assert.Equal(Magnetosphere.TiltRad(o), st.BeamTiltRad);
        Assert.Equal(Magnetosphere.BeamHalfAngleRad(st.NeutronStar!.PeriodS), st.BeamHalfAngleRad);
        Assert.Equal(Magnetosphere.EarthDirection(o, obs), st.EarthDirection);
        Assert.Equal(Magnetosphere.BeamCanCrossEarth(o, obs), st.BeamCanCrossEarth);
        Assert.Equal(Magnetosphere.LightCylinderKm(st.NeutronStar.PeriodS), st.LightCylinderKm);
        Assert.Equal(Magnetosphere.SpeedLabel(st.NeutronStar.PeriodS), st.SpeedLabel);
        Assert.Equal(2, st.DrawnPeriodS);
        Assert.Contains(st.Notes, n => n.Contains("62°") && n.Contains("Romani"));   // the published tilt, named (spec C5)
        var plain = SceneFor(TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.07, 3e13)).At(1e4);
        Assert.Contains(plain.Notes, n => n.Contains("schematic") && n.Contains("45°"));
    }

    [Fact]
    public void Magnetar_IsTitledAndShownAtRealSpeed()
    {
        var m = TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.07, 3e13, TestOutcomes.Pulsar(2.5, 1e15, RemnantKind.Magnetar));
        var st = SceneFor(m).At(100 * Year);
        Assert.StartsWith("Magnetar (", st.Title);
        Assert.Equal("Shown at real speed", st.SpeedLabel);
        Assert.Equal(st.NeutronStar!.PeriodS, st.DrawnPeriodS);
    }

    [Fact]
    public void NeutronStar_StateFollowsTheTime_TheLensedBodyDoesNot()
    {
        var scene = SceneFor(Crab());
        var young = scene.At(100 * Year);
        var old = scene.At(900 * Year);
        Assert.True(old.NeutronStar!.PeriodS > young.NeutronStar!.PeriodS);
        Assert.True(old.LightCylinderKm > young.LightCylinderKm);
        Assert.Equal((young.MassMsun, young.SurfaceRadiusKm, young.Spin), (old.MassMsun, old.SurfaceRadiusKm, old.Spin));
    }

    // ---- The field lines (plan Task 6: dipole lines r = L sin²θ, schematic count, open beyond R_L) ----

    [Fact]
    public void FieldLines_AreDipoleLines_TheirNumberGrowsWithTheField()
    {
        var weak = FieldLines.For(12, 1e11, 1600);
        var crab = FieldLines.For(12, 3.8e12, 1600);
        var magnetar = FieldLines.For(12, 1e15, 2e5);
        Assert.True(weak.Closed.Count < crab.Closed.Count && crab.Closed.Count < magnetar.Closed.Count);
        Assert.Equal(FieldLines.ShellCount(3.8e12), FieldLines.ShellsKm(12, 3.8e12).Count);
        foreach (var line in crab.Closed)
        {
            // r = L sin²θ with θ from the magnetic axis (+z): one L along the whole line, footpoints on the surface.
            double l = line[line.Length / 2].Length;
            foreach (var p in line)
            {
                double sin2 = (p.X * p.X + p.Y * p.Y) / p.LengthSquared;
                Assert.Equal(l * sin2, p.Length, 1e-6 * l);
            }
            Assert.Equal(12, line[0].Length, 1e-6);
            Assert.Equal(12, line[^1].Length, 1e-6);
        }
    }

    [Fact]
    public void FieldLines_BeyondTheLightCylinder_AreOpen_AndStopThere()
    {
        var set = FieldLines.For(12, 3.8e12, 100);   // a light cylinder at 100 km: the outer shells open
        Assert.NotEmpty(set.Open);
        Assert.All(set.Closed, line => Assert.All(line, p => Assert.True(p.Length <= 100 + 1e-6)));
        foreach (var line in set.Open)
        {
            Assert.Equal(12, line[0].Length, 1e-6);       // from the surface...
            Assert.Equal(100, line[^1].Length, 1e-6);     // ...to the light cylinder, no further
        }
        Assert.True(FieldLines.PolarCapRad(12, 1600) < 6 * Math.PI / 180);   // the Crab's polar cap: about 5°
        Assert.Equal(Math.Asin(Math.Sqrt(12.0 / 1600)), FieldLines.PolarCapRad(12, 1600), 12);
    }

    /// <summary>The overlays the star hides: inside or behind its sphere, and behind its bent (larger-looking) disc; never in front.</summary>
    [Fact]
    public void StarOcclusion_HidesWhatIsInsideOrBehindTheStar()
    {
        var centre = new Vector3d(100, 0, 0);   // the star 100 km ahead of the eye, along +x
        const double r = 12, apparent = 14.9;
        Assert.False(StarOcclusion.Hidden(new Vector3d(80, 0, 0), centre, r, apparent));        // in front
        Assert.False(StarOcclusion.Hidden(new Vector3d(88, 0, 0), centre, r, apparent));        // on the near surface
        Assert.True(StarOcclusion.Hidden(new Vector3d(95, 0, 0), centre, r, apparent));         // inside
        Assert.True(StarOcclusion.Hidden(new Vector3d(130, 0, 0), centre, r, apparent));        // behind
        Assert.True(StarOcclusion.Hidden(new Vector3d(130, 13.5 * 1.3, 0), centre, r, apparent)); // behind the bent rim (b ≈ 13.5 km)
        Assert.False(StarOcclusion.Hidden(new Vector3d(130, 20 * 1.3, 0), centre, r, apparent));  // beside it
        Assert.False(StarOcclusion.Hidden(new Vector3d(95, 13.5 * 0.95, 0), centre, r, apparent)); // nearer than the centre, outside the sphere
        Assert.False(StarOcclusion.Hidden(new Vector3d(130, 0, 0), centre, 0, 0));                // no star
        // From afar the 1.4 M☉, 12 km star looks R / √(1 − 2GM/(c²R)) = 1.24 R across.
        double rg = BlackHoleOptics.GravitationalRadiusCm(1.4) / 1e5;
        Assert.Equal(12 / Math.Sqrt(1 - 2 * rg / 12), StarOcclusion.ApparentRadiusKm(12, rg, 1e9), 6);
        Assert.InRange(StarOcclusion.ApparentRadiusKm(12, rg, 1e9) / 12, 1.22, 1.26);
        Assert.Equal(12, StarOcclusion.ApparentRadiusKm(12, double.NaN, 50));
    }

    [Fact]
    public void LineFrames_AreOrthonormal_TheMagneticOneAlongTheBeamsAxis()
    {
        var m = LineFrame.Magnetic(62 * Math.PI / 180, 1.1);
        Assert.Equal(RemnantRenderer.MagneticAxis(62 * Math.PI / 180, 1.1), m.Z);
        foreach (var (a, b) in new[] { (m.X, m.Y), (m.Y, m.Z), (m.Z, m.X) }) Assert.Equal(0, Vector3d.Dot(a, b), 12);
        foreach (var v in new[] { m.X, m.Y, m.Z }) Assert.Equal(1, v.Length, 12);
        Assert.Equal(1, Vector3d.Dot(Vector3d.Cross(m.X, m.Y), m.Z), 12);   // right-handed
        var e = new Vector3d(0, -Math.Sin(1.08), Math.Cos(1.08));
        var earth = NeutronStarRenderer.EarthFrame(e, 12);
        Assert.True((earth.Z - 12 * e).Length < 1e-12, "the arrow's +z is Earth's direction, scaled by the radius");
        Assert.Equal(0, Vector3d.Dot(earth.X, earth.Z), 9);
        Assert.Equal(NeutronStarRenderer.ArrowTipRadii, NeutronStarRenderer.EarthArrow()[0][1].Z);
    }
}
