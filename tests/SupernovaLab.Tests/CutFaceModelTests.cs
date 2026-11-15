using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Structure;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>
/// Ruling R-P2-21: what the debris cutaway's cut faces show (pure C#; cutface.frag mirrors <see cref="CutFaceModel.At"/>).
/// Task 16 and ruling R-P2-35: mixing at two scales, small fingers both ways along every layer boundary and broad nickel plumes
/// with sheaths and hydrogen downflows within twice each cone's chord on a face; grazing cones draw no plumes.
/// </summary>
public class CutFaceModelTests(Xunit.Abstractions.ITestOutputHelper output)
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
        // A face angle far from every cluster's band on face 0.
        var clusters = faces.ClustersOn(0);
        float psi = Enumerable.Range(-80, 161).Select(d => d * MathF.PI / 180)
            .First(p => clusters.All(c => MathF.Abs(p - c.CentreRad) > CutFaceModel.Band * c.HalfWidthRad + 0.1f));
        CutFacePoint? At(double r) => faces.At(0, (float)(r * Math.Sin(psi)), (float)(r * Math.Cos(psi)));

        // Mid-layer (boundary fingers and undulation use under half of each gap).
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
    public void EachCluster_HasOneOrTwoBroadPlumes_TipsBetween07And1OfNickelMax_TheLongestExactly_AndTonguesBesideAndBetween()
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
                    float band = CutFaceModel.Band * c.HalfWidthRad;
                    Assert.InRange(c.Fingers.Count, CutFaceModel.MinPlumes, CutFaceModel.MaxPlumes);
                    Assert.Equal(niMax, c.Fingers.Max(f => f.TipRadius));
                    Assert.All(c.Fingers, f => Assert.InRange(f.TipRadius, 0.7f * niMax, niMax));
                    Assert.All(c.Fingers, f => Assert.InRange(f.CentreRad, c.CentreRad - band, c.CentreRad + band));
                    // Broad: length (root to tip) to head width between 1:1.5 and 1:4 (R-P2-35: about 1:2 to 1:3).
                    Assert.All(c.Fingers, f =>
                    {
                        double aspect = (f.TipRadius - faces.FingerRootRadius) / (2 * CutFaceModel.CapWidth * f.HalfWidth);
                        Assert.InRange(aspect, 1.5, 4.0);
                    });
                    for (int j = 1; j < c.Fingers.Count; j++) Assert.True(c.Fingers[j].CentreRad > c.Fingers[j - 1].CentreRad);
                    if (double.IsFinite(faces.HydrogenMinRadiusCm))
                    {
                        Assert.Equal(c.Fingers.Count + 1, c.Tongues.Count);
                        Assert.All(c.Tongues, t => Assert.InRange(t.CentreRad, c.CentreRad - band, c.CentreRad + band));
                        Assert.True(c.Tongues[0].CentreRad < c.Fingers[0].CentreRad && c.Tongues[^1].CentreRad > c.Fingers[^1].CentreRad);
                        Assert.Equal((float)(faces.HydrogenMinRadiusCm / faces.OuterRadiusCm), c.Tongues.Min(t => t.TipRadius));
                    }
                    else Assert.Empty(c.Tongues);
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

    /// <summary>The layer index the unperturbed boundaries give at radius r (normalised).</summary>
    private static int Nominal(CutFaceModel faces, float r)
    {
        for (int k = 0; k < faces.NormalisedOuter.Count; k++)
            if (r <= faces.NormalisedOuter[k]) return k;
        return faces.NormalisedOuter.Count - 1;
    }

    [Theory]
    [MemberData(nameof(Scenes))]
    public void NoNickelBeyondNickelMax_NoTongueBelowHydrogenMin_MixingWithinTheBands_LayersInOrder(string preset, double days)
    {
        var faces = SceneFaces(preset == "default" ? null : preset, days * Day);
        float niMax = (float)(faces.NickelMaxRadiusCm / faces.OuterRadiusCm), hMin = (float)(faces.HydrogenMinRadiusCm / faces.OuterRadiusCm);
        var order = faces.Layers.Select(l => l.Layer).ToList();
        int fingers = 0, tongues = 0;
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
            // Below the nickel layer's (fingered) boundary the face is the plain core: no plume, sheath or tongue there.
            if (p.Unmixed == ElementLayer.NickelIron)
                Assert.True(p.Material == CutFaceMaterial.Layer && p.Layer == ElementLayer.NickelIron, $"{preset}: mixing drawn inside the core at {r}");
            // R-P2-35: plume nickel and tongues stay within twice the cone's chord on the face (Band chord half-widths).
            if (p.Material is CutFaceMaterial.NickelFinger or CutFaceMaterial.HydrogenTongue && r > 1e-3f)
            {
                float psi = MathF.Atan2(a, b);
                Assert.True(faces.ClustersOn(plane).Any(c => MathF.Abs(psi - c.CentreRad) <= CutFaceModel.Band * c.HalfWidthRad),
                    $"{preset}: mixing pixel outside every band at ({plane}, {a}, {b})");
            }
            // Layer order: a point's (fingered) layer is the nominal one at its radius or a neighbour, never two layers away.
            Assert.InRange(order.IndexOf(p.Unmixed), Nominal(faces, r) - 1, Nominal(faces, r) + 1);
        }
        Assert.True(fingers > 0, $"{preset}: no nickel-plume pixel on the faces");
        Assert.True(tongues > 0, $"{preset}: no hydrogen-tongue pixel on the faces");

        // Along every ray the layer steps by at most one at a time (fingers may overhang, boundaries never cross).
        for (int plane = 0; plane < 2; plane++)
            for (int k = -90; k <= 90; k++)
            {
                double psi = k * Math.PI / 180;
                int previous = 0;
                for (int i = 1; i <= 2000; i++)
                {
                    float r = i / 2000f;
                    if (faces.At(plane, (float)(r * Math.Sin(psi)), (float)(r * Math.Cos(psi))) is not { } p) continue;
                    int index = order.IndexOf(p.Unmixed);
                    Assert.True(Math.Abs(index - previous) <= 1, $"{preset}: layer jumps from {previous} to {index} at plane {plane}, psi {k} deg, r {r}");
                    previous = index;
                }
            }
    }

    [Theory]
    [MemberData(nameof(Scenes))]
    public void EveryBoundary_HasFingersBothWays_AllAroundEachFace(string preset, double days)
    {
        // R-P2-35: along every interior boundary, fingers of the inner layer push outward and of the outer layer inward, several
        // of each on every face, of aspect 1:1.5 to 1:4 and a wavelength of about 0.15 to 0.25 of the boundary's radius (shorter
        // where a thin layer caps the finger length).
        var faces = SceneFaces(preset == "default" ? null : preset, days * Day);
        var order = faces.Layers.Select(l => l.Layer).ToList();
        for (int k = 0; k < faces.Boundaries.Count; k++)
        {
            var bf = faces.Boundaries[k];
            Assert.True(bf.LengthUp > 0 && bf.LengthDown > 0 && bf.Period > 0, $"{preset}: boundary {k} has no fingers");
            // The boundary's profile along each face: the radius rho(psi) where a ray first leaves layers 0..k. A finger is where the
            // profile stands out from its own moving average over one cell (so the slow undulation does not count): outward by more
            // than 0.2 of the outward nominal length, or inward by more than 0.2 of the inward one.
            float rLo = bf.Radius * (1f - bf.UndulationDown) - bf.LengthDown - 0.002f, rHi = bf.Radius * (1f + bf.UndulationUp) + bf.LengthUp + 0.002f;
            const float step = 0.002f;
            int half = Math.Max(2, (int)(0.5f * bf.Period / step));
            bool Inside(int plane, float r, float psi) => faces.At(plane, r * MathF.Sin(psi), r * MathF.Cos(psi)) is { } p && order.IndexOf(p.Unmixed) <= k;
            var spacings = new List<float>();
            var ratios = new List<double>();
            for (int plane = 0; plane < 2; plane++)
            {
                var rho = new List<float>();
                for (int i = -750; i <= 750; i++)
                {
                    float psi = i * step;
                    float found = rHi;
                    for (int j = 0; j <= 300; j++)
                    {
                        float r = rLo + (rHi - rLo) * j / 300f;
                        if (!Inside(plane, r, psi)) { found = r; break; }
                    }
                    rho.Add(found);
                }
                float Psi(int i) => (i - 750) * step;
                // Runs that stand out from the moving average, each with its extreme (the finger's top or bottom).
                var outward = new List<int>();
                var inward = new List<int>();
                int upAt = -1, downAt = -1;
                for (int i = half; i < rho.Count - half; i++)
                {
                    float avg = 0;
                    for (int j = i - half; j <= i + half; j++) avg += rho[j];
                    avg /= 2 * half + 1;
                    bool up = rho[i] - avg > 0.2f * bf.LengthUp, down = avg - rho[i] > 0.2f * bf.LengthDown;
                    if (up) upAt = upAt < 0 || rho[i] > rho[upAt] ? i : upAt;
                    else if (upAt >= 0) { outward.Add(upAt); upAt = -1; }
                    if (down) downAt = downAt < 0 || rho[i] < rho[downAt] ? i : downAt;
                    else if (downAt >= 0) { inward.Add(downAt); downAt = -1; }
                }
                Assert.True(outward.Count >= 4, $"{preset}: boundary {k}, face {plane}: {outward.Count} outward fingers");
                Assert.True(inward.Count >= 4, $"{preset}: boundary {k}, face {plane}: {inward.Count} inward fingers");

                // Measured wavelength: the spacing of neighbouring outward fingers (a missing finger makes a double spacing; the
                // median ignores those).
                for (int j = 1; j < outward.Count; j++) spacings.Add(Psi(outward[j]) - Psi(outward[j - 1]));

                // Measured shape: length = the extreme's distance from the local base (the profile's median over two cells, where
                // the base dominates), width = the run across the finger at 0.4 of its length from the base, sampled with At().
                int cell = (int)(bf.Period / step);
                foreach (var (list, sign) in new[] { (outward, 1f), (inward, -1f) })
                    foreach (int i in list)
                    {
                        var window = rho.Skip(Math.Max(0, i - cell)).Take(2 * cell + 1).OrderBy(v => v).ToList();
                        float baseR = window[window.Count / 2], length = sign * (rho[i] - baseR);
                        if (length < 0.3f * (sign > 0 ? bf.LengthUp : bf.LengthDown)) continue;   // too short to sample
                        float r = baseR + sign * 0.4f * length, psi0 = Psi(i);
                        bool InFinger(float psi) => sign > 0 ? Inside(plane, r, psi) : !Inside(plane, r, psi);
                        if (!InFinger(psi0)) continue;   // leaning away from its top's angle at this height
                        float lo = psi0, hi = psi0;
                        while (InFinger(lo - 1e-4f) && psi0 - lo < bf.Period) lo -= 1e-4f;
                        while (InFinger(hi + 1e-4f) && hi - psi0 < bf.Period) hi += 1e-4f;
                        ratios.Add(length / ((hi - lo) * r));
                    }
            }
            spacings.Sort();
            float wavelength = spacings[spacings.Count / 2];
            bool uncapped = Math.Abs(Math.Max(bf.LengthUp, bf.LengthDown) - CutFaceModel.FingerLength * bf.Radius) < 1e-6f;
            if (uncapped)
                Assert.InRange(wavelength, 0.15f, 0.25f);
            else   // capped by a thin neighbouring layer: the cell shrinks with the finger, so check against its own period
                Assert.InRange(wavelength, 0.7f * bf.Period, 1.3f * bf.Period);
            ratios.Sort();
            double median = ratios[ratios.Count / 2], inRange = ratios.Count(x => x >= 1.5 && x <= 4) / (double)ratios.Count;
            output.WriteLine($"{preset} {days} d, boundary {k}: wavelength {wavelength:F3} rad (period {bf.Period:F3}, {(uncapped ? "uncapped" : "capped")}), length/width median {median:F2} of {ratios.Count}, {inRange:P0} within 1.5-4");
            Assert.True(ratios.Count >= 8, $"{preset}: boundary {k}: only {ratios.Count} fingers measured");
            Assert.InRange(median, 1.5, 4.0);
            Assert.True(inRange >= 0.7, $"{preset}: boundary {k}: {inRange:P0} of {ratios.Count} fingers within 1:1.5-1:4 (median {median:F2})");
        }
    }

    [Theory]
    [MemberData(nameof(Scenes))]
    public void PlumesRiseFromABroadBase_AndTonguesAreBroadAndVisible(string preset, double days)
    {
        // A plume leaves the nickel layer with a base covering at least 25% of its chord there; a tongue's root is at least 30% of the
        // gap it sits in; and at the default zoom (about 150 px per outer radius) a tongue shows as more than a few pixels just inside
        // the hydrogen layer's boundary.
        var faces = SceneFaces(preset == "default" ? null : preset, days * Day);
        var b0 = faces.Boundaries[0];
        float above = (b0.Radius * (1f + b0.UndulationUp) + b0.LengthUp) * 1.05f;   // beyond the nickel boundary's farthest reach
        double widestTongue = 0;
        for (int plane = 0; plane < 2; plane++)
            foreach (var c in faces.ClustersOn(plane))
            {
                int inChord = 0, nickel = 0;
                for (int i = -200; i <= 200; i++)
                {
                    float psi = c.CentreRad + c.HalfWidthRad * i / 200f;
                    if (Math.Abs(psi) > Math.PI / 2) continue;
                    if (faces.At(plane, above * MathF.Sin(psi), above * MathF.Cos(psi)) is not { } p) continue;
                    inChord++;
                    if (p.Layer == ElementLayer.NickelIron) nickel++;
                }
                if (inChord > 200) Assert.True(nickel >= 0.25 * inChord, $"{preset}: base covers {nickel}/{inChord} of the chord");

                // Tongue roots against the gap they sit in: between the neighbouring plume stalks or a stalk and the band's edge.
                float root = faces.TongueRootRadius, band = CutFaceModel.Band * c.HalfWidthRad;
                foreach (var t in c.Tongues)
                {
                    var left = c.Fingers.Where(f => f.CentreRad < t.CentreRad).Select(f => (f.CentreRad * root + f.HalfWidth))
                        .DefaultIfEmpty((c.CentreRad - band) * root).Max();
                    var right = c.Fingers.Where(f => f.CentreRad > t.CentreRad).Select(f => (f.CentreRad * root - f.HalfWidth))
                        .DefaultIfEmpty((c.CentreRad + band) * root).Min();
                    Assert.True(2 * t.HalfWidth >= 0.3 * (right - left), $"{preset}: tongue root {2 * t.HalfWidth} vs gap {right - left}");
                }
                foreach (double frac in new[] { 0.95, 0.9, 0.85, 0.8 })
                {
                    float rr = (float)(faces.HydrogenInnerRadiusCm / faces.OuterRadiusCm * frac);
                    int run = 0;
                    for (int i = -800; i <= 800; i++)
                    {
                        float psi = c.CentreRad + band * i / 800f;
                        var p = faces.At(plane, rr * MathF.Sin(psi), rr * MathF.Cos(psi));
                        run = p?.Material == CutFaceMaterial.HydrogenTongue ? run + 1 : 0;
                        widestTongue = Math.Max(widestTongue, run * band / 800.0 * rr);
                    }
                }
            }
        Assert.True(widestTongue >= 3.0 / 150, $"{preset}: widest tongue {widestTongue * 150:F1} px at 150 px per outer radius");
    }

    [Fact]
    public void AGrazingCone_DrawsNoPlumes_ASquareOneDrawsAPlumeCluster()
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
            // Only the boundary fingers (drawn as their layers) remain: no plume, sheath or tongue.
            Assert.DoesNotContain(Grid(grazing, 150), g => g.P.Material != CutFaceMaterial.Layer);
        }
        var square = With(0.2 * theta);
        Assert.Single(square.ClustersOn(0));
        Assert.Contains(Grid(square, 150), g => g.P.Material == CutFaceMaterial.NickelFinger);
        Assert.Contains(Grid(square, 150), g => g.P.Material == CutFaceMaterial.HydrogenTongue);
    }

    [Theory]
    [InlineData("sn1987a", 85.0)]
    [InlineData("casa", 21.4)]
    public void NickelIsOpaque_TonguesAndSheathsFadeToAFloor_TheRestFadesWithTheDensity(string preset, double days)
    {
        // Ruling R-P2-36 (review I1): a nickel plume stays opaque; hydrogen tongues, their helium rims and the plumes' sheaths take the
        // density fade with a floor of 0.5; the layer takes the fade.
        var faces = SceneFaces(preset, days * Day);
        bool fadedLayer = false, nickelBeyond = false, fadedMixed = false;
        foreach (var (plane, a, b, p) in Grid(faces, 200))
        {
            double alpha = faces.OpacityAt(plane, a, b);
            double r = MathF.Sqrt(a * a + b * b) * faces.OuterRadiusCm, fade = faces.Opacity(r, false);
            switch (p.Material)
            {
                case CutFaceMaterial.NickelFinger:
                    Assert.Equal(1, alpha);
                    if (fade < 1) nickelBeyond = true;
                    break;
                case CutFaceMaterial.Layer:
                    Assert.Equal(fade, alpha);
                    if (alpha < 0.5) fadedLayer = true;
                    break;
                default:
                    Assert.Equal(Math.Max(fade, CutFaceModel.MixedOpacityFloor), alpha);
                    Assert.InRange(alpha, 0.5, 1.0);
                    if (fade < 1) fadedMixed = true;
                    break;
            }
        }
        Assert.True(fadedLayer, $"{preset}: no faded layer pixel");
        Assert.True(nickelBeyond, $"{preset}: no nickel pixel beyond v_t (to show it stays opaque)");
        if (preset == "casa") Assert.True(fadedMixed, "casa: a tongue or sheath pixel beyond v_t has alpha < 1");
    }

    [Fact]
    public void APlumesHead_IsWiderThanItsStalk()
    {
        var faces = SceneFaces(null, 30 * Day);
        var c = faces.ClustersOn(0).Concat(faces.ClustersOn(1)).First();
        int plane = faces.ClustersOn(0).Contains(c) ? 0 : 1;
        CutFacePoint? At(double r, double psi) => faces.At(plane, (float)(r * Math.Sin(psi)), (float)(r * Math.Cos(psi)));
        var longest = c.Fingers.OrderByDescending(f => f.TipRadius).First();
        double Run(double r)
        {
            double best = 0;
            for (double centre = longest.CentreRad - 0.3; centre <= longest.CentreRad + 0.3; centre += 0.002)
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
        double capWidest = longest.TipRadius - CutFaceModel.CapHeight * CutFaceModel.CapWidth * longest.HalfWidth;
        double stalk = faces.FingerRootRadius + 0.8 * (longest.TipRadius - faces.FingerRootRadius);   // solid by then (ClumpSolidTo)
        Assert.True(Run(capWidest) > 1.4 * Run(stalk), $"head {Run(capWidest)} vs stalk {Run(stalk)}");
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
        Assert.All(faces.Boundaries, bf => Assert.True(bf.LengthUp == 0 && bf.LengthDown == 0 && bf.Period == 0));   // stratified: no fingers
        // R-P2-36: no undulation either; the layers are smooth circles, exactly at their radii at every face angle.
        Assert.All(faces.Boundaries, bf => Assert.True(bf.UndulationUp == 0 && bf.UndulationDown == 0));
        for (int k = 0; k < faces.Boundaries.Count; k++)
            for (int i = -150; i <= 150; i += 3)
            {
                float psi = i * 0.01f, R = faces.Boundaries[k].Radius;
                Assert.Equal(faces.Layers[k].Layer, faces.At(i % 2 == 0 ? 0 : 1, 0.999f * R * MathF.Sin(psi), 0.999f * R * MathF.Cos(psi))!.Value.Unmixed);
                Assert.Equal(faces.Layers[k + 1].Layer, faces.At(i % 2 == 0 ? 0 : 1, 1.001f * R * MathF.Sin(psi), 1.001f * R * MathF.Cos(psi))!.Value.Unmixed);
            }
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

    [Fact]
    public void TheCutawayNote_SaysTheSmallFingersAreSchematic()
    {
        // R-P2-35 and R-P2-36: the overlay note says the plume shapes, the undulation and the small boundary fingers are schematic.
        Assert.StartsWith("Cutaway: colours show elements (schematic)", SupernovaLab.App.Controllers.SceneController.CutawayNote);
        Assert.Contains("the plume shapes, the boundary undulation and the small fingers along every layer boundary are schematic: " +
            "only the reach of the nickel plumes and hydrogen downflows is from the lab's model",
            SupernovaLab.App.Controllers.SceneController.CutawayNote);
    }

    [Fact]
    public void PairInstability_StaysSmoothlyStratified()
    {
        // R-P2-36: no mixing (R-P2-17), so no plumes, no boundary fingers and no undulation.
        var outcome = TestOutcomes.Engine.Compute(LabInputs.Default with { InitialMass = 200, Metallicity = 0.1 });
        var s = EjectaStructureModel.For(outcome)!;
        Assert.False(s.Mixing.HasFingers);
        var (faces, _, _, _) = Faces(s, 100 * Day, 1e13);
        Assert.Empty(faces.ClustersOn(0));
        Assert.Empty(faces.ClustersOn(1));
        Assert.All(faces.Boundaries, bf => Assert.True(bf.LengthUp == 0 && bf.LengthDown == 0 && bf.Period == 0
            && bf.UndulationUp == 0 && bf.UndulationDown == 0));
        Assert.DoesNotContain(Grid(faces, 100), g => g.P.Material != CutFaceMaterial.Layer);
    }
}
