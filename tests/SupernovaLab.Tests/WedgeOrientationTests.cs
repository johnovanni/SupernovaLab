using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Structure;

namespace SupernovaLab.Tests;

/// <summary>Ruling R-P2-22: the wedge turns about the scene's y axis so a cut face always crosses a finger cone.</summary>
public class WedgeOrientationTests
{
    private static FingerCones ConesOf(EjectaStructure s) => MixingModel.Cones(s.Mixing, ParticleSampler.DefaultSeed);

    public static IEnumerable<object[]> Names() => new[] { new object[] { "red supergiant" }, new object[] { "blue supergiant" },
        new object[] { "stripped" }, new object[] { "SN 1987A preset" }, new object[] { "Cas A preset" }, new object[] { "the lab as launched" } };

    private static EjectaStructure Structure(string name)
    {
        switch (name)
        {
            case "red supergiant": return EjectaStructureModel.For(TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.05, 3.5e13))!;
            case "blue supergiant": return EjectaStructureModel.For(TestOutcomes.Custom(SupernovaType.IIP, 1.5e51, 15, 0.07, 3.9e12))!;
            case "stripped": return EjectaStructureModel.For(TestOutcomes.Custom(SupernovaType.Ib, 1e51, 3, 0.07, 1e11))!;
            default:
                var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
                if (name != "the lab as launched") vm.Lab.SelectedPreset = PresetCatalog.Get(name == "Cas A preset" ? "casa" : "sn1987a");
                return EjectaStructureModel.For(vm.CurrentOutcome)!;
        }
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void EveryClassWithFingers_HasAConeCrossingAFace_AtTheChosenAngle(string name)
    {
        var s = Structure(name);
        Assert.True(s.Mixing.HasFingers, name);
        var cones = ConesOf(s);
        double phi = WedgeOrientation.AngleFor(cones);
        Assert.InRange(phi, 0, 2 * Math.PI);
        int best = WedgeOrientation.CrossingCount(phi, cones);
        Assert.True(best >= 1, name);
        // No angle does better, and no smaller angle does as well (ties go to the smallest).
        for (double a = 0; a < 360; a += WedgeOrientation.StepDeg)
        {
            double p = a * Math.PI / 180;
            int n = WedgeOrientation.CrossingCount(p, cones);
            Assert.True(n <= best, $"{name}: {n} at {a} deg beats {best}");
            if (p < phi - 1e-12) Assert.True(n < best, $"{name}: {a} deg ties and is smaller");
        }
        // Deterministic: the same cones give the same angle.
        Assert.Equal(phi, WedgeOrientation.AngleFor(ConesOf(s)));
    }

    [Fact]
    public void WithoutFingers_TheAngleIsZero()
        => Assert.Equal(0, WedgeOrientation.AngleFor(FingerCones.Empty));

    [Fact]
    public void AnyDirection_CanBeBroughtOntoAFace()
    {
        var rng = new Random(11);
        for (int i = 0; i < 200; i++)
        {
            double z = 2 * rng.NextDouble() - 1, t = 2 * Math.PI * rng.NextDouble(), r = Math.Sqrt(1 - z * z);
            var cones = new FingerCones(new[] { (r * Math.Cos(t), r * Math.Sin(t), z) }, 0.05);
            Assert.Equal(1, WedgeOrientation.CrossingCount(WedgeOrientation.AngleFor(cones), cones));
        }
    }

    [Fact]
    public void TheBasis_IsOrthonormal_AndTheDropAndTheFacesShareIt()
    {
        foreach (double phi in new[] { 0, 0.7, 2.1, 5.5 })
        {
            var bx = WedgeOrientation.BasisX(phi);
            var bz = WedgeOrientation.BasisZ(phi);
            Assert.Equal(1, bx.X * bx.X + bx.Y * bx.Y + bx.Z * bx.Z, 12);
            Assert.Equal(0, bx.X * bz.X + bx.Y * bz.Y + bx.Z * bz.Z, 12);

            // The shaders drop a particle when dot(p, BasisX) > 0 and dot(p, BasisZ) > 0; that is IsRemoved.
            var rng = new Random(3);
            for (int i = 0; i < 200; i++)
            {
                double x = rng.NextDouble() - 0.5, y = rng.NextDouble() - 0.5, z = rng.NextDouble() - 0.5;
                bool shader = (x * bx.X + z * bx.Z) > 0 && (x * bz.X + z * bz.Z) > 0;
                Assert.Equal(shader, WedgeOrientation.IsRemoved(phi, x, y, z));
            }

            // The face quads: points b * BasisZ + a * y (plane w.x = 0) and b * BasisX + a * y (plane w.z = 0), b >= 0.
            var onA = WedgeOrientation.ToWedge(phi, 0.4 * bz.X, 0.3, 0.4 * bz.Z);
            var onB = WedgeOrientation.ToWedge(phi, 0.4 * bx.X, 0.3, 0.4 * bx.Z);
            Assert.Equal(0, onA.X, 12);
            Assert.True(onA.Z > 0);
            Assert.Equal(0, onB.Z, 12);
            Assert.True(onB.X > 0);

            var back = WedgeOrientation.FromWedge(phi, 0.2, 0.5, -0.3);
            var again = WedgeOrientation.ToWedge(phi, back.X, back.Y, back.Z);
            Assert.Equal(0.2, again.X, 12);
            Assert.Equal(0.5, again.Y, 12);
            Assert.Equal(-0.3, again.Z, 12);
        }
    }

    [Fact]
    public void CutFace_IsDrawnOnlyFromItsExposedSide()
    {
        // Task 7 review carry-over: plane 0 (w.x = 0) bounds the removed quarter on its +w.x side, so it is seen only from an eye
        // with w.x > 0; plane 1 (w.z = 0) only from w.z > 0. cutface.vert mirrors this with the camera's eye position.
        Assert.True(WedgeOrientation.FaceExposed(0, 0, 1, 0.2, -1));
        Assert.False(WedgeOrientation.FaceExposed(0, 0, -1, 0.2, 1));
        Assert.True(WedgeOrientation.FaceExposed(1, 0, -1, 0.2, 1));
        Assert.False(WedgeOrientation.FaceExposed(1, 0, 1, 0.2, -1));
        Assert.False(WedgeOrientation.FaceExposed(0, 0, 0, 1, 1));   // edge-on: not drawn
        double phi = 0.7;
        var x = WedgeOrientation.BasisX(phi);
        var z = WedgeOrientation.BasisZ(phi);
        Assert.True(WedgeOrientation.FaceExposed(0, phi, x.X + 0.1 * z.X, 0, x.Z + 0.1 * z.Z));
        Assert.True(WedgeOrientation.FaceExposed(1, phi, x.X + 0.1 * z.X, 0, x.Z + 0.1 * z.Z));
        Assert.False(WedgeOrientation.FaceExposed(1, phi, x.X - 0.1 * z.X, 0, x.Z - 0.1 * z.Z));
    }

    /// <summary>A unit axis in scene coordinates at <paramref name="offPlane"/> radians from face <paramref name="plane"/>'s plane,
    /// its in-plane part at face angle <paramref name="psi"/> (from the face's outward edge towards +y).</summary>
    internal static (double X, double Y, double Z) AxisOffFace(int plane, double phi, double psi, double offPlane)
    {
        double inPlane = Math.Cos(offPlane), a = inPlane * Math.Sin(psi), b = inPlane * Math.Cos(psi), n = Math.Sin(offPlane);
        // Plane 0: normal = BasisX, outward = BasisZ; plane 1: normal = BasisZ, outward = BasisX.
        var (wx, wz) = plane == 0 ? (n, b) : (b, n);
        return WedgeOrientation.FromWedge(phi, wx, a, wz);
    }

    [Fact]
    public void ConeOnFace_GivesTheChordsCentreAndHalfWidth_AndNothingForAGrazingCone()
    {
        // Task 16: a cone whose axis lies in the face plane crosses it with its full half-angle; one tilted by delta from the
        // plane crosses it in a chord of half-width acos(cos theta / cos delta); beyond 2/3 of theta it only grazes: no crossing.
        const double theta = 0.3, phi = 0.4;
        for (int plane = 0; plane < 2; plane++)
        {
            var full = WedgeOrientation.ConeOnFace(plane, phi, AxisOffFace(plane, phi, 0.5, 0), theta);
            Assert.NotNull(full);
            Assert.Equal(0.5, full!.Value.CentreRad, 12);
            Assert.Equal(theta, full.Value.HalfWidthRad, 7);
            Assert.Equal(0, full.Value.OffPlaneRad, 7);

            var tilted = WedgeOrientation.ConeOnFace(plane, phi, AxisOffFace(plane, phi, -0.2, 0.5 * theta), theta);
            Assert.NotNull(tilted);
            Assert.Equal(-0.2, tilted!.Value.CentreRad, 12);
            Assert.Equal(Math.Acos(Math.Cos(theta) / Math.Cos(0.5 * theta)), tilted.Value.HalfWidthRad, 9);
            Assert.Equal(0.5 * theta, tilted.Value.OffPlaneRad, 9);

            Assert.NotNull(WedgeOrientation.ConeOnFace(plane, phi, AxisOffFace(plane, phi, 0.1, 0.66 * theta), theta));
            Assert.Null(WedgeOrientation.ConeOnFace(plane, phi, AxisOffFace(plane, phi, 0.1, 0.68 * theta), theta));
            Assert.Null(WedgeOrientation.ConeOnFace(plane, phi, AxisOffFace(plane, phi, 0.1, 0.95 * theta), theta));
            Assert.Null(WedgeOrientation.ConeOnFace(plane, phi, AxisOffFace(plane, phi, 0.1, -0.95 * theta), theta));
        }
    }

    [Fact]
    public void CrossingCount_CountsOnlySubstantialCrossings_OnTheDrawnHalfOfAFace()
    {
        const double theta = 0.3, phi = 1.1;
        // Grazing: the old test (angle to the half-plane at most theta) says it crosses; the count does not take it.
        var grazing = AxisOffFace(0, phi, 0.3, 0.9 * theta);
        Assert.True(WedgeOrientation.CrossesFace(phi, grazing, theta));
        Assert.Equal(0, WedgeOrientation.CrossingCount(phi, new FingerCones(new[] { grazing }, theta)));
        // Squarely across the drawn half (|psi| <= 90 deg): counted. On the plane's other half (psi = 150 deg): not.
        Assert.Equal(1, WedgeOrientation.CrossingCount(phi, new FingerCones(new[] { AxisOffFace(1, phi, -0.7, 0.2 * theta) }, theta)));
        Assert.Equal(0, WedgeOrientation.CrossingCount(phi, new FingerCones(new[] { AxisOffFace(1, phi, 150 * Math.PI / 180, 0) }, theta)));
    }
}
