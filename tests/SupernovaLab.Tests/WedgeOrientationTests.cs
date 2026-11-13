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
        new object[] { "stripped" }, new object[] { "SN 1987A preset" } };

    private static EjectaStructure Structure(string name)
    {
        switch (name)
        {
            case "red supergiant": return EjectaStructureModel.For(TestOutcomes.Custom(SupernovaType.IIP, 1e51, 10, 0.05, 3.5e13))!;
            case "blue supergiant": return EjectaStructureModel.For(TestOutcomes.Custom(SupernovaType.IIP, 1.5e51, 15, 0.07, 3.9e12))!;
            case "stripped": return EjectaStructureModel.For(TestOutcomes.Custom(SupernovaType.Ib, 1e51, 3, 0.07, 1e11))!;
            default:
                var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
                vm.Lab.SelectedPreset = PresetCatalog.Get("sn1987a");
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
}
