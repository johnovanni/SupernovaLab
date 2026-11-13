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
    public void Fingers_AreTheConesCutByThePlane_NickelOutToItsTipsAndHydrogenInward()
    {
        var (faces, cones, s, _) = Faces(Bsg());
        Assert.True(faces.Cones.Axes.Count > 0);
        Assert.True(faces.NickelMaxRadiusCm > faces.Layers[0].OuterRadiusCm, "nickel tips reach beyond the unmixed nickel layer");
        var axis = cones.Axes[0];

        // On a cone's axis, beyond the nickel layer but inside the tips: nickel (the finger).
        double inTip = 0.5 * (faces.Layers[0].OuterRadiusCm + faces.NickelMaxRadiusCm);
        Assert.Equal(ElementLayer.NickelIron, faces.LayerAt(inTip, axis.X, axis.Y, axis.Z));
        // Just past the tips on the axis: not nickel any more (the layer there, or hydrogen pulled in).
        Assert.NotEqual(ElementLayer.NickelIron, faces.LayerAt(faces.NickelMaxRadiusCm * 1.001, axis.X, axis.Y, axis.Z));
        // Hydrogen fingers: on the axis at HydrogenMin's radius, past the nickel tips, hydrogen.
        if (faces.HydrogenMinRadiusCm > faces.NickelMaxRadiusCm)
            Assert.Equal(ElementLayer.Hydrogen, faces.LayerAt(faces.HydrogenMinRadiusCm * 1.001, axis.X, axis.Y, axis.Z));
        Assert.True(s.Mixing.HasFingers);
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
        Assert.True(faces.InFinger(0.5 * faces.NickelMaxRadiusCm, axis.X, axis.Y, axis.Z));
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
}
