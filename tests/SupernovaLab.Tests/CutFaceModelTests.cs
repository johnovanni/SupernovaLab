using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Structure;

namespace SupernovaLab.Tests;

/// <summary>Ruling R-P2-21: what the debris cutaway's cut faces show (pure C#; the GL shader mirrors <see cref="CutFaceModel.LayerAt"/>).</summary>
public class CutFaceModelTests
{
    private const double Day = 86400;

    private static EjectaStructure Bsg() => EjectaStructureModel.For(TestOutcomes.Custom(SupernovaType.IIP, 1.5e51, 15, 0.07, 3.9e12))!;

    private static (CutFaceModel Faces, FingerCones Cones, EjectaStructure S, double Vmax) Faces(EjectaStructure s, double sinceS = 30 * Day, double starCm = 3.9e12)
    {
        var set = ParticleSampler.Sample(s, 2000);
        var cones = MixingModel.Cones(s.Mixing, ParticleSampler.DefaultSeed);
        return (CutFaceModel.Build(s, cones, sinceS, starCm, set.MaxCmS)!, cones, s, set.MaxCmS);
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
    public void LayerAt_GivesTheUnmixedLayerOffTheFingers_AndNothingBeyondTheDebris()
    {
        var (faces, cones, _, _) = Faces(Bsg());
        // A direction outside every cone.
        (double X, double Y, double Z) away = (0, 0, 1);
        var rng = new Random(5);
        for (int i = 0; i < 500 && cones.Contains(away.X, away.Y, away.Z); i++) away = MixingModelTestAccess.Direction(rng);
        Assert.False(cones.Contains(away.X, away.Y, away.Z));

        double r0 = faces.Layers[0].OuterRadiusCm, rLast = faces.OuterRadiusCm;
        Assert.Equal(faces.Layers[0].Layer, faces.LayerAt(0.5 * r0, away.X, away.Y, away.Z));
        Assert.Equal(faces.Layers[1].Layer, faces.LayerAt(0.5 * (r0 + faces.Layers[1].OuterRadiusCm), away.X, away.Y, away.Z));
        Assert.Equal(faces.Layers[^1].Layer, faces.LayerAt(0.999 * rLast, away.X, away.Y, away.Z));
        Assert.Null(faces.LayerAt(1.001 * rLast, away.X, away.Y, away.Z));
    }

    [Fact]
    public void Fingers_AreSegmentsOfTheCones_NickelOutwardAndHydrogenInward()
    {
        var (faces, cones, s, _) = Faces(Bsg());
        Assert.True(faces.Cones.Axes.Count > 0);
        double niIn = faces.NickelInnerRadiusCm, niMax = faces.NickelMaxRadiusCm, hMin = faces.HydrogenMinRadiusCm, hIn = faces.HydrogenInnerRadiusCm;
        Assert.True(niMax > niIn, "nickel tips reach beyond the unmixed nickel layer");
        Assert.True(hIn > hMin, "the hydrogen layer's inner boundary lies beyond where the hydrogen fingers reach");
        var a = cones.Axes[0];

        // Nickel segment: nickel layer's boundary to the tips: opaque nickel.
        double inNi = 0.5 * (niIn + niMax);
        Assert.Equal(ElementLayer.NickelIron, faces.LayerAt(inNi, a.X, a.Y, a.Z));
        Assert.Equal(ElementLayer.NickelIron, faces.FingerAt(inNi, a.X, a.Y, a.Z));
        Assert.Equal(1, faces.Opacity(inNi, faces.InFinger(inNi, a.X, a.Y, a.Z)));

        // Hydrogen segment (beyond the nickel tips, where they do not overlap): opaque hydrogen.
        double inH = 0.5 * (Math.Max(hMin, niMax) + hIn);
        if (hIn > Math.Max(hMin, niMax))
        {
            Assert.Equal(ElementLayer.Hydrogen, faces.FingerAt(inH, a.X, a.Y, a.Z));
            Assert.Equal(ElementLayer.Hydrogen, faces.LayerAt(inH, a.X, a.Y, a.Z));
        }
        Assert.True(s.Mixing.HasFingers);
    }

    [Fact]
    public void BeyondBothSegments_AConePixelIsTheOrdinaryLayer_FadingWithTheDensity()
    {
        var (faces, cones, _, _) = Faces(Bsg());
        var a = cones.Axes[0];
        // A radius beyond the nickel tips, beyond the hydrogen layer's inner boundary and beyond v_t, inside the debris.
        double r = Math.Max(Math.Max(faces.NickelMaxRadiusCm, faces.HydrogenInnerRadiusCm), 1.5 * faces.TransitionRadiusCm);
        r = Math.Min(r * 1.05, 0.99 * faces.OuterRadiusCm);
        Assert.True(r > faces.TransitionRadiusCm * 1.2 && r > faces.NickelMaxRadiusCm && r > faces.HydrogenInnerRadiusCm);
        Assert.False(faces.InFinger(r, a.X, a.Y, a.Z));
        Assert.Equal(faces.Layers[^1].Layer, faces.LayerAt(r, a.X, a.Y, a.Z));
        double alpha = faces.Opacity(r, faces.InFinger(r, a.X, a.Y, a.Z));
        Assert.True(alpha < 1);
        Assert.Equal(Math.Pow(r / faces.TransitionRadiusCm, -faces.OuterIndex), alpha, 12);
    }

    [Fact]
    public void HydrogenSegment_IsOpaqueHydrogen_OnlyBetweenItsInwardReachAndTheLayersInnerBoundary()
    {
        // A hand-built face (units arbitrary): Ni to 1, He to 2, H to 10; nickel tips at 1.5, hydrogen fingers reach in to 1.8;
        // v_t at 3 with n = 10. One cone about +z.
        var cones = new FingerCones(new[] { (0.0, 0.0, 1.0) }, 0.3);
        var faces = new CutFaceModel(new[] { (ElementLayer.NickelIron, 1.0), (ElementLayer.Helium, 2.0), (ElementLayer.Hydrogen, 10.0) },
            1.5, 1.8, cones, 3.0, 10.0);
        Assert.Equal(2.0, faces.HydrogenInnerRadiusCm);
        Assert.Equal(1.0, faces.NickelInnerRadiusCm);

        // In the hydrogen segment (1.8 to 2): opaque hydrogen, though the unmixed layer there is helium.
        Assert.Equal(ElementLayer.Hydrogen, faces.LayerAt(1.9, 0, 0, 1));
        Assert.Equal(1, faces.Opacity(1.9, faces.InFinger(1.9, 0, 0, 1)));
        // Below it (1.6, helium) and beyond the layer's inner boundary: the ordinary layers, no finger.
        Assert.Equal(ElementLayer.Helium, faces.LayerAt(1.6, 0, 0, 1));
        Assert.False(faces.InFinger(2.5, 0, 0, 1));
        // Nickel segment: opaque nickel.
        Assert.Equal(ElementLayer.NickelIron, faces.LayerAt(1.2, 0, 0, 1));
        Assert.True(faces.InFinger(1.2, 0, 0, 1));
        // Far outside in the cone, beyond v_t: the hydrogen layer, faded by the density, not opaque.
        Assert.Equal(ElementLayer.Hydrogen, faces.LayerAt(6, 0, 0, 1));
        Assert.Equal(Math.Pow(2, -10), faces.Opacity(6, faces.InFinger(6, 0, 0, 1)), 12);
        // Off the cone: never a finger.
        Assert.Equal(ElementLayer.Helium, faces.LayerAt(1.9, 1, 0, 0));
        // Where the segments overlap, nickel wins.
        var overlap = new CutFaceModel(faces.Layers, 1.9, 1.8, cones, 3.0, 10.0);
        Assert.Equal(ElementLayer.NickelIron, overlap.LayerAt(1.85, 0, 0, 1));
    }

    [Fact]
    public void TypeIa_HasLayersButNoFingers()
    {
        var s = EjectaStructureModel.For(TestOutcomes.TypeIa())!;
        var (faces, _, _, _) = Faces(s, 20 * Day, 1e9);
        Assert.Empty(faces.Cones.Axes);
        Assert.True(double.IsNaN(faces.NickelMaxRadiusCm));
        Assert.True(double.IsNaN(faces.HydrogenMinRadiusCm));
        Assert.Equal(ElementLayer.NickelIron, faces.LayerAt(0.1 * faces.Layers[0].OuterRadiusCm, 1, 0, 0));
        Assert.Equal(ElementLayer.UnburnedCarbonOxygen, faces.LayerAt(0.999 * faces.OuterRadiusCm, 0, 1, 0));
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
        var (faces, cones, s, _) = Faces(Bsg());
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
        var axis = cones.Axes[0];
        double midTip = 0.5 * (faces.NickelInnerRadiusCm + faces.NickelMaxRadiusCm);
        Assert.True(faces.InFinger(midTip, axis.X, axis.Y, axis.Z));
    }

    /// <summary>A direction uniform on the sphere (the test's own, so MixingModel's internal helper stays internal).</summary>
    private static class MixingModelTestAccess
    {
        public static (double X, double Y, double Z) Direction(Random r)
        {
            double z = 2 * r.NextDouble() - 1, phi = 2 * Math.PI * r.NextDouble(), s = Math.Sqrt(1 - z * z);
            return (s * Math.Cos(phi), s * Math.Sin(phi), z);
        }
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
