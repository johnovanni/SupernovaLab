using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Structure;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>
/// Ruling R-P2-21: what the debris cutaway's cut faces show (pure C#; cutface.frag mirrors <see cref="CutFaceModel.At"/>).
/// Task 16: the mixing is drawn as Rayleigh-Taylor-like nickel fingers and hydrogen tongues inside the cones' chords across
/// each face, with ragged layer boundaries; grazing cones draw nothing.
/// </summary>
public class CutFaceModelTests
{
    private const double Day = 86400;

    private static EjectaStructure Bsg() => EjectaStructureModel.For(TestOutcomes.Custom(SupernovaType.IIP, 1.5e51, 15, 0.07, 3.9e12))!;
    private static EjectaStructure Rsg() => EjectaStructureModel.For(TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.05, 3.5e13))!;
    private static EjectaStructure Stripped() => EjectaStructureModel.For(TestOutcomes.Custom(SupernovaType.Ib, 1e51, 3, 0.07, 1e11))!;

    private static (CutFaceModel Faces, FingerCones Cones, EjectaStructure S, double Vmax) Faces(EjectaStructure s, double sinceS = 30 * Day, double starCm = 3.9e12)
    {
        var set = ParticleSampler.Sample(s, 2000);
        var cones = MixingModel.Cones(s.Mixing, ParticleSampler.DefaultSeed);
        double phi = WedgeOrientation.AngleFor(cones);
        return (CutFaceModel.Build(s, cones, sinceS, starCm, set.MaxCmS, null, phi)!, cones, s, set.MaxCmS);
    }

    /// <summary>The faces the 3D view draws for a preset (null: the lab as launched) at this observer time, as SceneController builds them.</summary>
    internal static CutFaceModel SceneFaces(string? preset, double observerS)
    {
        var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        if (preset is not null) vm.Lab.SelectedPreset = PresetCatalog.Get(preset);
        var scene = new ExplosionScene(vm.CurrentOutcome);
        var state = scene.At(observerS);
        var set = scene.ParticlesFor(2000)!;
        var cones = MixingModel.Cones(scene.Structure!.Mixing, ParticleSampler.DefaultSeed);
        double star = double.IsFinite(state.StarRadiusCm) ? state.StarRadiusCm : 0;
        return CutFaceModel.Build(scene.Structure, cones, state.SinceBreakoutS, star, set.MaxCmS, state.Shocks, WedgeOrientation.AngleFor(cones))!;
    }

    /// <summary>Every face point on a grid of both faces (normalised face coordinates a in [-1, 1], b in [0, 1]).</summary>
    private static IEnumerable<(int Plane, float A, float B, CutFacePoint P)> Grid(CutFaceModel faces, int n = 300)
    {
        for (int plane = 0; plane < 2; plane++)
            for (int i = 0; i <= 2 * n; i++)
                for (int j = 0; j <= n; j++)
                {
                    float a = -1f + i / (float)n, b = j / (float)n;
                    if (faces.At(plane, a, b) is { } p) yield return (plane, a, b, p);
                }
    }

    /// <summary>The scene direction of a face point (normalised face coordinates).</summary>
    private static (double X, double Y, double Z) Direction(CutFaceModel faces, int plane, double a, double b)
    {
        var (_, o) = WedgeOrientation.FaceAxes(plane, faces.WedgeAngleRad);
        double r = Math.Sqrt(a * a + b * b);
        return ((b * o.X) / r, (a + b * o.Y) / r, (b * o.Z) / r);
    }

    [Fact]
    public void LayerRadii_FollowTheMassCoordinate_ThroughTheProfileAndTheRadiusRule()
    {
        var (faces, _, s, vmax) = Faces(Bsg());
        double cumulative = 0;
        Assert.Equal(s.Layers.Select(l => l.Layer), faces.Layers.Select(l => l.Layer));
        for (int k = 0; k < s.Layers.Count; k++)
        {
            cumulative += s.Layers[k].MassMsun;
            double v = EjectaStructureModel.VelocityAtMassFraction(s, EjectaStructureModel.TruncationFraction * Math.Min(1, cumulative / s.EjectaMassMsun));
            double expected = ParticleKinematics.RadiusCm(v, 30 * Day, 3.9e12, vmax, null);
            if (k < s.Layers.Count - 1) Assert.Equal(expected, faces.Layers[k].OuterRadiusCm, expected * 1e-12);
        }
        for (int k = 1; k < faces.Layers.Count; k++) Assert.True(faces.Layers[k].OuterRadiusCm > faces.Layers[k - 1].OuterRadiusCm);
        // The outer face edge is the fastest particle's radius.
        Assert.True(faces.OuterRadiusCm >= ParticleKinematics.RadiusCm(vmax, 30 * Day, 3.9e12, vmax, null) * (1 - 1e-12));
    }

    [Fact]
    public void AwayFromTheClusters_TheFaceShowsTheLayersByRadius_AndNothingBeyondTheDebris()
    {
        var (faces, _, _, _) = Faces(Bsg());
        // A face angle far from every cluster on face 0.
        var clusters = faces.ClustersOn(0);
        float psi = Enumerable.Range(-80, 161).Select(d => d * MathF.PI / 180)
            .First(p => clusters.All(c => MathF.Abs(p - c.CentreRad) > c.HalfWidthRad + 0.4f));
        CutFacePoint? At(double r) => faces.At(0, (float)(r * Math.Sin(psi)), (float)(r * Math.Cos(psi)));

        for (int k = 0; k < faces.Layers.Count; k++)
        {
            double lo = k == 0 ? 0 : faces.Layers[k - 1].OuterRadiusCm, hi = faces.Layers[k].OuterRadiusCm;
            var p = At(0.5 * (lo + hi) / faces.OuterRadiusCm)!.Value;
            Assert.Equal(faces.Layers[k].Layer, p.Layer);
            Assert.Equal(CutFaceMaterial.Layer, p.Material);
        }
        Assert.Equal(faces.Layers[^1].Layer, At(0.999)!.Value.Layer);
        Assert.Null(At(1.001));
        Assert.Null(faces.At(0, 0.1f, -0.01f));   // the plane's other half is not a face
    }

    [Fact]
    public void EachCluster_HasThreeToFiveFingers_TipsBetween07And1OfNickelMax_TheLongestExactly()
    {
        foreach (var s in new[] { Bsg(), Rsg(), Stripped() })
        {
            var (faces, cones, _, _) = Faces(s);
            float niMax = (float)(faces.NickelMaxRadiusCm / faces.OuterRadiusCm);
            int clusters = 0;
            for (int plane = 0; plane < 2; plane++)
                foreach (var c in faces.ClustersOn(plane))
                {
                    clusters++;
                    Assert.InRange(c.Fingers.Count, 3, 5);
                    Assert.Equal(niMax, c.Fingers.Max(f => f.TipRadius));
                    Assert.All(c.Fingers, f => Assert.InRange(f.TipRadius, 0.7f * niMax, niMax));
                    Assert.All(c.Fingers, f => Assert.InRange(f.CentreRad, c.CentreRad - c.HalfWidthRad, c.CentreRad + c.HalfWidthRad));
                    // Fingers in order across the chord, a tongue in each gap between neighbours.
                    for (int j = 1; j < c.Fingers.Count; j++) Assert.True(c.Fingers[j].CentreRad > c.Fingers[j - 1].CentreRad);
                    if (double.IsFinite(faces.HydrogenMinRadiusCm))
                    {
                        Assert.Equal(c.Fingers.Count - 1, c.Tongues.Count);
                        for (int j = 0; j < c.Tongues.Count; j++)
                            Assert.InRange(c.Tongues[j].CentreRad, c.Fingers[j].CentreRad, c.Fingers[j + 1].CentreRad);
                        Assert.Equal((float)(faces.HydrogenMinRadiusCm / faces.OuterRadiusCm), c.Tongues.Min(t => t.TipRadius));
                    }
                    else Assert.Empty(c.Tongues);
                    // Seeds from the cone index only: a face's cluster for cone k has the same fingers on the other face.
                    Assert.InRange(c.Cone, 0, cones.Axes.Count - 1);
                }
            Assert.True(clusters >= 1);
        }
    }

    [Fact]
    public void TheFaces_AreDeterministic()
    {
        var a = SceneFaces("sn1987a", 85 * Day);
        var b = SceneFaces("sn1987a", 85 * Day);
        var pa = Grid(a, 120).ToList();
        var pb = Grid(b, 120).ToList();
        Assert.Equal(pa.Count, pb.Count);
        for (int i = 0; i < pa.Count; i++) Assert.Equal(pa[i].P, pb[i].P);
    }

    public static IEnumerable<object[]> Scenes() => new[]
    {
        new object[] { "sn1987a", 85.0 }, new object[] { "casa", 21.4 }, new object[] { "default", 30.0 }, new object[] { "default", 365.25 },
    };

    [Theory]
    [MemberData(nameof(Scenes))]
    public void NoNickelBeyondNickelMax_NoTongueBelowHydrogenMin_AllMixingInsideTheCones_LayersInOrder(string preset, double days)
    {
        var faces = SceneFaces(preset == "default" ? null : preset, days * Day);
        float niMax = (float)(faces.NickelMaxRadiusCm / faces.OuterRadiusCm), hMin = (float)(faces.HydrogenMinRadiusCm / faces.OuterRadiusCm);
        int fingers = 0, tongues = 0;
        var lastLayer = new Dictionary<(int, int), int>();
        foreach (var (plane, a, b, p) in Grid(faces))
        {
            float r = MathF.Sqrt(a * a + b * b);
            if (p.Layer == ElementLayer.NickelIron)
                Assert.True(r <= niMax, $"{preset}: nickel at {r} beyond NickelMax {niMax}");
            if (p.Material == CutFaceMaterial.HydrogenTongue)
            {
                tongues++;
                Assert.True(r >= hMin, $"{preset}: hydrogen tongue at {r} below HydrogenMin {hMin}");
                Assert.Equal(ElementLayer.Hydrogen, p.Layer);
            }
            if (p.Material == CutFaceMaterial.NickelFinger) fingers++;
            if (p.Material != CutFaceMaterial.Layer && r > 1e-3f)
            {
                var d = Direction(faces, plane, a, b);
                Assert.True(faces.Cones.Contains(d.X, d.Y, d.Z), $"{preset}: mixing pixel outside every cone at ({plane}, {a}, {b})");
            }
        }
        Assert.True(fingers > 0, $"{preset}: no nickel-finger pixel on the faces");
        Assert.True(tongues > 0, $"{preset}: no hydrogen-tongue pixel on the faces");

        // Layer order: along every ray of both faces the unmixed (smeared) layer never steps back inward.
        for (int plane = 0; plane < 2; plane++)
            for (int k = -90; k <= 90; k++)
            {
                double psi = k * Math.PI / 180;
                int previous = -1;
                for (int i = 1; i <= 2000; i++)
                {
                    float r = i / 2000f;
                    if (faces.At(plane, (float)(r * Math.Sin(psi)), (float)(r * Math.Cos(psi))) is not { } p) continue;
                    int index = faces.Layers.Select(l => l.Layer).ToList().IndexOf(p.Unmixed);
                    Assert.True(index >= previous, $"{preset}: layer order broken at plane {plane}, psi {k} deg, r {r}");
                    previous = index;
                }
            }
    }

    [Fact]
    public void AGrazingCone_DrawsNothing_ASquareOneDrawsAFingerCluster()
    {
        // Hand-built (units arbitrary): Ni to 1, O to 2, He to 3, H to 10; nickel tips at 5, hydrogen down to 1.5; one cone.
        const double theta = 0.3, phi = 0.2;
        var layers = new[] { (ElementLayer.NickelIron, 1.0), (ElementLayer.CarbonOxygenCore, 2.0), (ElementLayer.Helium, 3.0), (ElementLayer.Hydrogen, 10.0) };
        CutFaceModel With(double offPlane)
        {
            var axis = WedgeOrientationTests.AxisOffFace(0, phi, 0.2, offPlane);
            return new CutFaceModel(layers, 5.0, 1.5, new FingerCones(new[] { axis }, theta), 6.0, 10.0) { WedgeAngleRad = phi };
        }
        foreach (double off in new[] { 0.75 * theta, 0.9 * theta, theta })
        {
            var grazing = With(off);
            Assert.Empty(grazing.ClustersOn(0));
            Assert.Empty(grazing.ClustersOn(1));
            Assert.DoesNotContain(Grid(grazing, 150), g => g.P.Material != CutFaceMaterial.Layer);
        }
        var square = With(0.2 * theta);
        Assert.Single(square.ClustersOn(0));
        Assert.Contains(Grid(square, 150), g => g.P.Material == CutFaceMaterial.NickelFinger);
        Assert.Contains(Grid(square, 150), g => g.P.Material == CutFaceMaterial.HydrogenTongue);
    }

    [Fact]
    public void FingersAndTongues_AreOpaque_TheRestFadesWithTheDensity()
    {
        var faces = SceneFaces("sn1987a", 85 * Day);
        bool sawFadedLayer = false;
        foreach (var (plane, a, b, p) in Grid(faces, 150))
        {
            double alpha = faces.OpacityAt(plane, a, b);
            double r = MathF.Sqrt(a * a + b * b) * faces.OuterRadiusCm;
            if (p.Material != CutFaceMaterial.Layer) Assert.Equal(1, alpha);
            else
            {
                Assert.Equal(faces.Opacity(r, false), alpha);
                if (alpha < 0.5) sawFadedLayer = true;
            }
        }
        Assert.True(sawFadedLayer);
    }

    [Fact]
    public void TheMixingLooksLikeMixing_RaggedBoundaries_MushroomCaps_TonguesInTheGaps()
    {
        var faces = SceneFaces(null, 30 * Day);
        var c = faces.ClustersOn(0).Concat(faces.ClustersOn(1)).First();
        int plane = faces.ClustersOn(0).Contains(c) ? 0 : 1;
        CutFacePoint? At(double r, double psi) => faces.At(plane, (float)(r * Math.Sin(psi)), (float)(r * Math.Cos(psi)));

        // The longest finger's cap is wider than its stalk: the nickel run around its centre at the cap's widest radius vs
        // at the middle of the stalk.
        var longest = c.Fingers.OrderByDescending(f => f.TipRadius).First();
        double Run(double r)
        {
            double best = 0;
            for (double centre = longest.CentreRad - 0.1; centre <= longest.CentreRad + 0.1; centre += 0.002)
            {
                if (At(r, centre)?.Material != CutFaceMaterial.NickelFinger) continue;
                double lo = centre, hi = centre;
                while (At(r, lo - 1e-4)?.Material == CutFaceMaterial.NickelFinger) lo -= 1e-4;
                while (At(r, hi + 1e-4)?.Material == CutFaceMaterial.NickelFinger) hi += 1e-4;
                best = Math.Max(best, (hi - lo) * r);
                centre = hi;
            }
            return best;
        }
        double capWidest = longest.TipRadius * (1 - CutFaceModel.CapLength);
        double stalkMiddle = 0.5 * (capWidest + faces.FingerRootRadius);
        Assert.True(Run(capWidest) > 1.5 * Run(stalkMiddle), $"cap {Run(capWidest)} vs stalk {Run(stalkMiddle)}");

        // A ragged boundary: the oxygen/helium boundary's radius varies along the face, by more inside the cluster than outside.
        int k = faces.Layers.Select(l => l.Layer).ToList().IndexOf(ElementLayer.CarbonOxygenCore);
        double R = faces.Layers[k].OuterRadiusCm / faces.OuterRadiusCm;
        double Boundary(double psi)
        {
            for (double r = 0.9 * R; r < 1.1 * R; r += 1e-4 * R)
                if (At(r, psi)?.Unmixed != ElementLayer.CarbonOxygenCore) return r / R;
            return 1.1;
        }
        var inside = Enumerable.Range(0, 40).Select(i => (double)c.CentreRad - c.HalfWidthRad + i * c.HalfWidthRad / 20)
            .Where(p => Math.Abs(p) < Math.PI / 2).Select(Boundary).ToList();
        // Outside: face angles at least 0.3 rad beyond every cluster's chord on this face (the in-cone strength has faded by then).
        bool Far(double p) => faces.ClustersOn(plane).All(o => Math.Abs(p - o.CentreRad) > o.HalfWidthRad + 0.3);
        var outside = Enumerable.Range(-155, 311).Select(i => i * 0.01).Where(Far).Select(Boundary).ToList();
        Assert.True(inside.Count >= 20 && outside.Count >= 20);
        Assert.True(inside.Max() - inside.Min() > 0.02, "the boundary is ragged in the cone");
        Assert.True(inside.Max() - inside.Min() > outside.Max() - outside.Min(), "and more ragged in the cone than outside");
        Assert.True(outside.Max() - outside.Min() > 0.002, "a little ragged outside too");

        // Tongues lie in the gaps: at each tongue's deepest point (just above its tip) the nearest finger centre is farther than the tongue is wide.
        Assert.Contains(Enumerable.Range(0, 400), i =>
        {
            var t = c.Tongues[0];
            double r = t.TipRadius + (faces.TongueRootRadius - t.TipRadius) * i / 400.0;
            return At(r, t.CentreRad)?.Material == CutFaceMaterial.HydrogenTongue;
        });
    }

    [Fact]
    public void TypeIa_HasLayersButNoFingers()
    {
        var s = EjectaStructureModel.For(TestOutcomes.TypeIa())!;
        var (faces, _, _, _) = Faces(s, 20 * Day, 1e9);
        Assert.Empty(faces.Cones.Axes);
        Assert.Empty(faces.ClustersOn(0));
        Assert.True(double.IsNaN(faces.NickelMaxRadiusCm));
        Assert.True(double.IsNaN(faces.HydrogenMinRadiusCm));
        float r0 = (float)(0.1 * faces.Layers[0].OuterRadiusCm / faces.OuterRadiusCm);
        Assert.Equal(ElementLayer.NickelIron, faces.At(0, 0, r0)!.Value.Layer);
        Assert.Equal(ElementLayer.UnburnedCarbonOxygen, faces.At(1, 0.999f, 0)!.Value.Layer);
        Assert.DoesNotContain(Grid(faces, 100), g => g.P.Material != CutFaceMaterial.Layer);
    }

    [Fact]
    public void AFailedSupernovaShell_HasNoCutFaces()
    {
        var failed = TestOutcomes.Custom(SupernovaType.None, null, null, null, 5e13, TestOutcomes.BlackHole(10, 1.5), Fate.FailedSupernova);
        var shell = EjectaStructureModel.For(failed);
        if (shell is { IsEnvelopeShell: true })
            Assert.Null(CutFaceModel.Build(shell, FingerCones.Empty, 1, 5e13, shell.MaxCmS));
    }

    [Fact]
    public void Opacity_FollowsTheDensityProfile_AndFingersAreOpaque()
    {
        // Ruling R-P2-23: 1 inside v_t, (v / v_t)^-n outside, radius proportional to speed.
        var (faces, _, s, _) = Faces(Bsg());
        double rt = faces.TransitionRadiusCm, n = s.N;
        Assert.True(n > 5);
        Assert.Equal(1, faces.Opacity(0.9 * rt, false));
        Assert.Equal(1, faces.Opacity(rt, false));
        Assert.Equal(Math.Pow(1.2, -n), faces.Opacity(1.2 * rt, false), 12);
        Assert.Equal(Math.Pow(2, -n), faces.Opacity(2 * rt, false), 12);
        Assert.Equal(1, faces.Opacity(3 * rt, true));
        Assert.True(faces.Opacity(faces.OuterRadiusCm, false) < 0.01, "the face fades out with the cloud");
        // Radius is proportional to speed in the radius rule, so the radius ratio is the speed ratio.
        double r1 = ParticleKinematics.RadiusCm(1.2 * s.TransitionCmS, 30 * Day, 0, 1e9, null);
        double r0 = ParticleKinematics.RadiusCm(s.TransitionCmS, 30 * Day, 0, 1e9, null);
        Assert.Equal(1.2, r1 / r0, 12);
    }

    [Fact]
    public void WithShocks_TheFacesAreConfinedLikeTheParticles_AndFadeByTheFreeRadius()
    {
        // Task 8: the faces' radii go through the same confinement as the particles (they stop at the contact surface when
        // fully confined), and the opacity reads the speed back, so a speed has the same opacity wherever it is drawn.
        var s = Bsg();
        var set = ParticleSampler.Sample(s, 2000);
        var cones = MixingModel.Cones(s.Mixing, ParticleSampler.DefaultSeed);
        double since = 30 * Day, star = 3.9e12, k = star + set.MaxCmS * since;
        var shocks = new ShockRadii(0.6 * k, 0.5 * k, 0.45 * k) { ConfinementWeight = 0.7 };
        var faces = CutFaceModel.Build(s, cones, since, star, set.MaxCmS, shocks)!;
        var free = CutFaceModel.Build(s, cones, since, star, set.MaxCmS)!;
        double edge = ParticleKinematics.RadiusCm(set.MaxCmS, since, star, set.MaxCmS, shocks);
        Assert.Equal(1, faces.OuterRadiusCm / edge, 12);
        for (int i = 0; i < faces.Layers.Count - 1; i++)
            Assert.True(faces.Layers[i].OuterRadiusCm <= free.Layers[i].OuterRadiusCm * (1 + 1e-12));
        foreach (double frac in new[] { 0.3, 0.45, 0.6, 0.9, 1.0 })
        {
            double v = frac * set.MaxCmS;
            double drawn = ParticleKinematics.RadiusCm(v, since, star, set.MaxCmS, shocks);
            double freeR = ParticleKinematics.RadiusCm(v, since, star, set.MaxCmS, null);
            Assert.Equal(free.Opacity(freeR, false), faces.Opacity(drawn, false), 9);
        }
        var full = CutFaceModel.Build(s, cones, since, star, set.MaxCmS, shocks with { ConfinementWeight = 1 })!;
        Assert.Equal(1, full.OuterRadiusCm / (0.5 * k), 12);
    }
}
