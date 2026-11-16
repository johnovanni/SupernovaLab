using System.Windows;
using OpenTK.Mathematics;
using SupernovaLab.Core.Compact;

namespace SupernovaLab.Rendering;

/// <summary>
/// A label of the black hole's overlay: its text, the scene point (km) at the end of its leader line where it is placed, the point on the
/// outline the leader starts from, and whether it sits to the left of its end (on the left of the hole).
/// </summary>
public readonly record struct OverlayLabel(string Text, Vector3d AtKm, Vector3d FromKm, bool LeftOfEnd = false);

/// <summary>
/// Plan Task 10 (spec §6.1, §6.3; ruling P3-9): the black hole's overlay lines, as scene polylines in km (the scene's axes are the
/// tracer's chart, ruling R-P3-8: spin along +z). Pure, so the geometry is tested without OpenGL; <see cref="LensOverlayRenderer"/> draws it.
/// <list type="bullet">
/// <item><b>The shadow's exact edge</b> (always): <see cref="BlackHoleOptics.ShadowEdgeDirections"/> from the camera's own position, so it
/// sits on the lensing pass's shadow at full resolution whatever the light-bending detail (P3-9). Each direction is placed at the
/// camera's distance from the eye, so the camera's projection puts it on the pixel whose ray it is. Dashed, so it is not mistaken for
/// the photon ring (the bright light at the edge, drawn by the lensing pass).</item>
/// <item><b>With the grid on</b> (spec §6.3): the horizon r₊, the ergosphere r_E(θ) = GM/c² [1 + √(1 − a² cos²θ)] and the photon
/// orbits, each where it is (not bent), as its cut through the plane that holds the spin axis and faces the camera. A point at
/// Boyer–Lindquist (r, θ) lies at ρ = √(r² + a²) sin θ from the axis and z = r cos θ (Kerr–Schild). For no spin the photon sphere is a
/// circle of 3 GM/c²; with spin there are two equatorial photon orbits, with the spin (prograde, r_ph+) on the side turning towards
/// the camera and against it (retrograde, r_ph−) on the other, marked by ticks.</item>
/// </list>
/// </summary>
public static class LensOverlay
{
    /// <summary>The number of points round the shadow's edge (half of them start a dash).</summary>
    public const int EdgePoints = 192;

    /// <summary>The meridian outlines' points per half (pole to pole).</summary>
    private const int MeridianPoints = 90;

    /// <summary>The photon-orbit ticks' half-length, GM/c².</summary>
    private const double TickHalfGm = 0.35;

    /// <summary>The labels (spec §6.3: the grid labels the horizon and the photon sphere; §6.3: the ergosphere is a labelled outline).</summary>
    public const string HorizonLabel = "Horizon", PhotonSphereLabel = "Photon sphere", ErgosphereLabel = "Ergosphere",
        ProgradeLabel = "Photon orbit, with the spin", RetrogradeLabel = "Photon orbit, against the spin";

    /// <summary>
    /// The shadow's edge seen from <paramref name="eyeKm"/> (scene km) round a hole of this spin and GM/c² (km): dashes, each two points
    /// on the edge's directions placed at the camera's distance from the eye. Empty when the camera is inside the horizon.
    /// </summary>
    public static IReadOnlyList<Vector3d[]> ShadowEdge(double spin, Vector3d eyeKm, double rgKm, int points = EdgePoints)
    {
        var lines = new List<Vector3d[]>();
        if (!(rgKm > 0) || !double.IsFinite(rgKm)) return lines;
        var eyeGm = eyeKm / rgKm;
        var dirs = BlackHoleOptics.ShadowEdgeDirections(spin, new Vec3(eyeGm.X, eyeGm.Y, eyeGm.Z), points);
        double reach = eyeKm.Length;
        for (int k = 0; k + 1 < dirs.Count; k += 2)
            lines.Add(new[] { At(eyeKm, dirs[k], reach), At(eyeKm, dirs[k + 1], reach) });
        return lines;
    }

    private static Vector3d At(Vector3d eye, Vec3 d, double reach) => eye + new Vector3d(d.X, d.Y, d.Z) * reach;

    /// <summary>
    /// The horizontal unit vector across the view in the meridian plane facing the camera: ẑ × (towards the eye), or the camera's right
    /// when it looks along the axis.
    /// </summary>
    public static Vector3d Across(Vector3d towardsEye, Vector3d screenRight)
    {
        var e = Vector3d.Cross(Vector3d.UnitZ, towardsEye);
        return e.Length > 1e-6 ? e.Normalized() : screenRight.Normalized();
    }

    /// <summary>
    /// The side (+1 along <paramref name="across"/>, or −1) whose matter turns towards the camera with the spin (+z): where the prograde
    /// photon orbit is seen.
    /// </summary>
    public static int ProgradeSide(Vector3d across, Vector3d towardsEye)
        => Vector3d.Dot(Vector3d.Cross(Vector3d.UnitZ, across), towardsEye) >= 0 ? 1 : -1;

    /// <summary>A surface r(θ) (GM/c²) cut by the meridian plane: the closed outline over both halves, in km.</summary>
    public static Vector3d[] Meridian(Func<double, double> radiusGm, double spin, Vector3d across, double rgKm)
    {
        var pts = new List<Vector3d>(2 * MeridianPoints + 1);
        double a2 = spin * spin;
        foreach (int side in new[] { 1, -1 })
            for (int i = 0; i <= MeridianPoints; i++)
            {
                double t = Math.PI * (side > 0 ? i : MeridianPoints - i) / MeridianPoints;
                double r = radiusGm(t);
                double rho = Math.Sqrt(r * r + a2) * Math.Sin(t);
                pts.Add((side * rho * across + r * Math.Cos(t) * Vector3d.UnitZ) * rgKm);
            }
        return pts.ToArray();
    }

    /// <summary>The horizon's cut (r = r₊), km.</summary>
    public static Vector3d[] Horizon(double spin, Vector3d across, double rgKm)
    {
        double rp = BlackHoleOptics.HorizonRadius(spin);
        return Meridian(_ => rp, spin, across, rgKm);
    }

    /// <summary>The ergosphere's cut (r = r_E(θ)), km: it meets the horizon at the poles and reaches 2 GM/c² at the equator.</summary>
    public static Vector3d[] Ergosphere(double spin, Vector3d across, double rgKm)
        => Meridian(t => BlackHoleOptics.ErgosphereRadius(spin, t), spin, across, rgKm);

    /// <summary>The equatorial photon orbits: r_ph± = 2 GM/c² [1 + cos(⅔ arccos(∓a))] (prograde, retrograde).</summary>
    public static (double Prograde, double Retrograde) PhotonOrbitsGm(double spin)
    {
        double a = Math.Clamp(Math.Abs(spin), 0, 0.9999);
        return (2 * (1 + Math.Cos(2.0 / 3 * Math.Acos(-a))), 2 * (1 + Math.Cos(2.0 / 3 * Math.Acos(a))));
    }

    /// <summary>
    /// The photon orbits, km: for spin below 10⁻⁴ the photon sphere's circle (3 GM/c²) in the plane facing the camera; otherwise a tick
    /// across the equator at each equatorial photon orbit, the prograde one on <see cref="ProgradeSide"/>.
    /// </summary>
    public static IReadOnlyList<Vector3d[]> PhotonOrbits(double spin, Vector3d across, Vector3d towardsEye, double rgKm)
    {
        if (Math.Abs(spin) < 1e-4) return new[] { Meridian(_ => BlackHoleOptics.PhotonSphereRadius, 0, across, rgKm) };
        var (pro, retro) = PhotonOrbitsGm(spin);
        int side = ProgradeSide(across, towardsEye);
        Vector3d[] Tick(double r, int s)
        {
            var c = s * Math.Sqrt(r * r + spin * spin) * across;
            return new[] { (c - TickHalfGm * Vector3d.UnitZ) * rgKm, (c + TickHalfGm * Vector3d.UnitZ) * rgKm };
        }
        return new[] { Tick(pro, side), Tick(retro, -side) };
    }

    /// <summary>
    /// [Presentation] The labels' leader lines end on a circle of this radius (GM/c²) round the hole, outside the shadow (whose edge
    /// reaches 6.8 GM/c² from the hole on the retrograde side at the largest spins): Task 10's launch put labels on the outlines
    /// themselves, where they piled up over a hole only about 40 px across.
    /// </summary>
    public const double LeaderEndGm = 8.6;

    /// <summary>[Presentation] The ergosphere is labelled from this spin up (its outline is drawn for any spin): at 0.3 it reaches 0.05 GM/c²
    /// past the horizon at the equator, at 0.1 only 0.005, where the label would name a line on top of the horizon's.</summary>
    public const double ErgosphereLabelMinSpin = 0.3;

    /// <summary>
    /// The labels with the grid on, each at the end of a leader line from the outline it names: the horizon (up and right), the photon
    /// sphere (right) or the two equatorial photon orbits (right and left, from their ticks), the ergosphere (down and right). A label on
    /// the left (<see cref="OverlayLabel.LeftOfEnd"/>) is set to end at its leader.
    /// </summary>
    public static IReadOnlyList<OverlayLabel> Labels(double spin, Vector3d across, Vector3d towardsEye, double rgKm)
    {
        double a = Math.Abs(spin), rp = BlackHoleOptics.HorizonRadius(spin);
        Vector3d End(double deg) => LeaderEndGm * rgKm * (Math.Cos(deg * Math.PI / 180) * across + Math.Sin(deg * Math.PI / 180) * Vector3d.UnitZ);
        Vector3d On(double r, double thetaDeg, int side)
        {
            double t = thetaDeg * Math.PI / 180;
            return (side * Math.Sqrt(r * r + a * a) * Math.Sin(t) * across + r * Math.Cos(t) * Vector3d.UnitZ) * rgKm;
        }
        var labels = new List<OverlayLabel> { new(HorizonLabel, End(55), On(rp, 35, 1)) };
        if (a < 1e-4)
        {
            labels.Add(new(PhotonSphereLabel, End(15), On(BlackHoleOptics.PhotonSphereRadius, 75, 1)));
            return labels;
        }
        var (pro, retro) = PhotonOrbitsGm(a);
        int side = ProgradeSide(across, towardsEye);
        Vector3d TickTop(double r, int s) => (s * Math.Sqrt(r * r + a * a) * across + TickHalfGm * Vector3d.UnitZ) * rgKm;
        labels.Add(new(ProgradeLabel, End(side > 0 ? 15 : 165), TickTop(pro, side), side < 0));
        labels.Add(new(RetrogradeLabel, End(side > 0 ? 165 : 15), TickTop(retro, -side), side > 0));
        if (a >= ErgosphereLabelMinSpin)   // fix round 1: at a = 0.1 the ergosphere is only 0.005 GM/c² outside the horizon
            labels.Add(new(ErgosphereLabel, End(-55), On(BlackHoleOptics.ErgosphereRadius(a, 145 * Math.PI / 180), 145, 1)));
        return labels;
    }

    /// <summary>The leader lines of <see cref="Labels"/>, km.</summary>
    public static IReadOnlyList<Vector3d[]> Leaders(IReadOnlyList<OverlayLabel> labels)
        => labels.Select(l => new[] { l.FromKm, l.AtKm }).ToList();
}

/// <summary>
/// Plan Task 10: draws <see cref="LensOverlay"/>'s lines after the bloom (crisp, tone-mapped), 1 px wide (<see cref="FieldLineRenderer"/>):
/// the shadow's exact edge always, and with the grid on the horizon, the photon orbits and the ergosphere. The geometry is rebuilt only
/// when the camera, the drawn spin or the drawn mass change. Create and use with the GL context current.
/// </summary>
public sealed class LensOverlayRenderer : IDisposable
{
    /// <summary>[Schematic] The colours (alpha in W): the shadow's edge, the horizon, the photon orbits, the ergosphere, the labels' leaders.</summary>
    public static readonly Vector4 EdgeColor = new(0.55f, 0.85f, 1f, 0.75f), HorizonColor = new(0.85f, 0.85f, 0.85f, 0.7f),
        PhotonColor = new(1f, 0.88f, 0.45f, 0.85f), ErgosphereColor = new(1f, 0.5f, 0.3f, 0.85f), LeaderColor = new(0.95f, 0.89f, 0.48f, 0.5f);

    private readonly FieldLineRenderer _lines = new();
    private FieldLineRenderer.Batch? _edge, _horizon, _photon, _ergo, _leaders;
    private (double Spin, double RgKm, Vector3d Eye, Vector3d Across) _key = (double.NaN, double.NaN, default, default);

    /// <summary>How many times the geometry has been rebuilt (diagnostics).</summary>
    public int Rebuilds { get; private set; }

    /// <summary>Draws the overlay for this black hole (the state's drawn spin and mass) at this colour gain.</summary>
    public void Render(OrbitCamera camera, Size viewportPx, CloseUpState s, bool grid, bool labels, float gain)
    {
        if (s.BlackHole is null || !(s.MassMsun > 0)) return;
        double rgKm = s.GravitationalRadiusKm;
        var eye = camera.Eye;
        var (right, _) = camera.ScreenAxes();
        var across = LensOverlay.Across(camera.Backward, right);
        var key = (s.Spin, rgKm, eye, across);
        if (key != _key || _edge is null)
        {
            Release();
            _edge = _lines.Upload(LensOverlay.ShadowEdge(s.Spin, eye, rgKm));
            _horizon = _lines.Upload(new[] { LensOverlay.Horizon(s.Spin, across, rgKm) });
            _photon = _lines.Upload(LensOverlay.PhotonOrbits(s.Spin, across, camera.Backward, rgKm));
            _ergo = _lines.Upload(new[] { LensOverlay.Ergosphere(s.Spin, across, rgKm) });
            _leaders = _lines.Upload(LensOverlay.Leaders(LensOverlay.Labels(s.Spin, across, camera.Backward, rgKm)));
            _key = key;
            Rebuilds++;
        }
        var scale = new Vector4(gain, gain, gain, 1);
        _lines.Render(camera, viewportPx, _edge, LineFrame.Identity, EdgeColor * scale, null);
        if (!grid) return;
        _lines.Render(camera, viewportPx, _horizon!, LineFrame.Identity, HorizonColor * scale, null);
        _lines.Render(camera, viewportPx, _photon!, LineFrame.Identity, PhotonColor * scale, null);
        if (s.Spin >= 1e-4) _lines.Render(camera, viewportPx, _ergo!, LineFrame.Identity, ErgosphereColor * scale, null);
        if (labels) _lines.Render(camera, viewportPx, _leaders!, LineFrame.Identity, LeaderColor * scale, null);
    }

    private void Release()
    {
        _edge?.Dispose();
        _horizon?.Dispose();
        _photon?.Dispose();
        _ergo?.Dispose();
        _leaders?.Dispose();
        _edge = _horizon = _photon = _ergo = _leaders = null;
    }

    public void Dispose()
    {
        Release();
        _lines.Dispose();
    }
}
