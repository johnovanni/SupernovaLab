using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SupernovaLab.Core.Structure;

namespace SupernovaLab.Rendering;

/// <summary>
/// SN 1987A's three rings (structure §S8) as thin glowing tori, and the equatorial ring's 24 hotspot beads. The geometry is observed
/// (<see cref="Sn1987aRings"/>), placed in the scene by <see cref="SkyFrame"/>: all three are circles in planes perpendicular to the
/// symmetry axis â = (−0.1055, −0.6738, +0.7314) in (west, north, toward-the-observer) coordinates, each centred z₀·â along it
/// (the north ring's z₀ &lt; 0, behind the equatorial plane; the south ring's z₀ &gt; 0, in front). The rings' in-plane axes are drawn
/// along the equatorial ring's major axis (P.A. 81.1°); the rings are circles (schematic). Drawn additively with no depth test, into
/// the HDR target before the bloom; the tube is at least <see cref="MinTubePx"/> pixels across so a ring stays visible in a wide view.
/// Create and use with the GL context current.
/// </summary>
public sealed class TorusRingRenderer : IDisposable
{
    public const int RingSegments = 192, TubeSegments = 10;

    /// <summary>[Schematic] The tube's radius as a fraction of the ring's radius, and its least half-width in pixels.</summary>
    public const double TubeFraction = 0.04, MinTubePx = 1.2;

    /// <summary>[Schematic] The rings' colour (a warm amber) and HDR brightness (Task 11 retune: 1.6 to 1.1 and 0.7 to 0.5, so the tubes stay rings and the hotspot beads stand out against the debris at 8,000 to 12,000 d): the equatorial ring, then the two outer rings (dimmer).</summary>
    public static readonly Vector3 RingColor = new(1.00f, 0.62f, 0.38f);
    public const float EquatorialIntensity = 1.1f, OuterIntensity = 0.5f;

    /// <summary>[Schematic] The hotspots: a hot yellow-white, at most this HDR brightness (times the hotspot brightness, 0 to 1) and this many device-independent pixels across.</summary>
    public static readonly Vector3 BeadColor = new(1.00f, 0.88f, 0.62f);
    public const float BeadIntensity = 7f;
    public const double BeadPx = 9;

    /// <summary>
    /// How much of a ring is shown, 0 to 1: the rings are far larger than the debris for decades, and the camera sits only 1 / tan(fov / 2)
    /// = 2.4 view half-heights from the centre, so a ring bigger than that would wrap around the camera. A ring whose farthest point from
    /// the centre (<paramref name="extentUnits"/>, its radius plus its offset, in scene units) is within <see cref="FullBelow"/> of the
    /// camera's distance is drawn in full; it fades out by <see cref="GoneAbove"/> times that. So the rings appear as the view grows to
    /// contain them (the director does not frame them).
    /// </summary>
    public static double Visibility(double extentUnits, double cameraDistanceUnits)
        => cameraDistanceUnits > 0 ? Math.Clamp((GoneAbove * cameraDistanceUnits - extentUnits) / ((GoneAbove - FullBelow) * cameraDistanceUnits), 0, 1) : 0;

    public const double FullBelow = 0.9, GoneAbove = 1.2;

    /// <summary>The beads' seed: each bead has a fixed strength (0.6 to 1), so the picture is the same every run.</summary>
    public const int BeadSeed = 1987;

    private readonly ShaderProgram _torus = ShaderProgram.FromResources("torus.vert", "torus.frag");
    private readonly ShaderProgram _bead = ShaderProgram.FromResources("bead.vert", "bead.frag");
    private readonly int _torusVao, _torusVbo, _torusCount, _beadVao, _beadVbo;

    public TorusRingRenderer()
    {
        var mesh = new List<float>();
        for (int i = 0; i < RingSegments; i++)
            for (int j = 0; j < TubeSegments; j++)
            {
                float t0 = (float)(2 * Math.PI * i / RingSegments), t1 = (float)(2 * Math.PI * (i + 1) / RingSegments);
                float p0 = (float)(2 * Math.PI * j / TubeSegments), p1 = (float)(2 * Math.PI * (j + 1) / TubeSegments);
                mesh.AddRange(new[] { t0, p0, t1, p0, t1, p1, t0, p0, t1, p1, t0, p1 });
            }
        var data = mesh.ToArray();
        _torusCount = data.Length / 2;
        (_torusVao, _torusVbo) = Upload(data);

        var rng = new Random(BeadSeed);
        var beads = new float[Sn1987aRings.HotspotCount * 2];
        for (int k = 0; k < Sn1987aRings.HotspotCount; k++)
        {
            beads[2 * k] = (float)BeadAngle(k);
            beads[2 * k + 1] = (float)(0.6 + 0.4 * rng.NextDouble());
        }
        (_beadVao, _beadVbo) = Upload(beads);
    }

    private static (int Vao, int Vbo) Upload(float[] data)
    {
        int vao = GL.GenVertexArray(), vbo = GL.GenBuffer();
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Length * sizeof(float), data, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.BindVertexArray(0);
        return (vao, vbo);
    }

    /// <summary>The angle round the equatorial ring of bead k: the 24 beads are evenly spaced.</summary>
    public static double BeadAngle(int k) => 2 * Math.PI * k / Sn1987aRings.HotspotCount;

    /// <summary>The symmetry axis in scene axes: <see cref="Sn1987aRings.AxisSky"/> (west, north, toward) through <see cref="SkyFrame"/>.</summary>
    public static Vector3d SceneAxis
    {
        get
        {
            var (w, n, z) = Sn1987aRings.AxisSky;
            return SkyFrame.ToScene(w, n, z);
        }
    }

    /// <summary>
    /// The rings' plane axes in scene axes: u along the equatorial ring's major axis (P.A. 81.1° east of north, in the sky plane and so
    /// perpendicular to the symmetry axis), v = â × u.
    /// </summary>
    public static (Vector3d U, Vector3d V) PlaneAxes()
    {
        double pa = 81.1 * Math.PI / 180;
        var u = SkyFrame.ToScene(-Math.Sin(pa), Math.Cos(pa), 0);
        return (u, Vector3d.Cross(SceneAxis, u));
    }

    /// <summary>Draws the three rings, and the beads when <paramref name="hotspots"/> is above 0; <paramref name="unitCm"/> is one scene unit.</summary>
    public void Render(OrbitCamera camera, Size viewportPx, double pxPerDip, double unitCm, float hotspots)
    {
        if (!(unitCm > 0) || !double.IsFinite(unitCm) || !(viewportPx.Height > 0)) return;
        var axis = SceneAxis;
        var (u, v) = PlaneAxes();
        double pxUnits = 2 * camera.DistanceMpc * Math.Tan(camera.FieldOfViewDeg * Math.PI / 360) / viewportPx.Height;

        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
        _torus.Use();
        _torus.SetCamera(camera, viewportPx);
        _torus.Set("u_planeU", (Vector3)u);
        _torus.Set("u_planeV", (Vector3)v);
        _torus.Set("u_axis", (Vector3)axis);
        _torus.Set("u_color", RingColor);
        GL.BindVertexArray(_torusVao);
        double erVisibility = 0;
        foreach (var ring in Sn1987aRings.Rings)
        {
            double radius = ring.RadiusCm / unitCm;
            double visible = Visibility(radius + Math.Abs(ring.AxisOffsetCm) / unitCm, camera.DistanceMpc);
            if (ring.Equatorial) erVisibility = visible;
            if (!(visible > 0)) continue;
            double tube = Math.Max(TubeFraction * radius, MinTubePx * pxUnits);
            _torus.Set("u_center", (Vector3)(axis * (ring.AxisOffsetCm / unitCm)));
            _torus.Set("u_radius", (float)radius);
            _torus.Set("u_tube", (float)tube);
            _torus.Set("u_intensity", (float)(visible * (ring.Equatorial ? EquatorialIntensity : OuterIntensity)));
            GL.DrawArrays(PrimitiveType.Triangles, 0, _torusCount);
        }
        GL.BindVertexArray(0);

        if (!(hotspots > 0) || !(erVisibility > 0)) return;
        var er = Sn1987aRings.Rings.First(r => r.Equatorial);
        GL.Enable(EnableCap.ProgramPointSize);
        _bead.Use();
        _bead.SetCamera(camera, viewportPx);
        _bead.Set("u_planeU", (Vector3)u);
        _bead.Set("u_planeV", (Vector3)v);
        _bead.Set("u_radius", (float)(er.RadiusCm / unitCm));
        _bead.Set("u_pointPx", (float)(BeadPx * pxPerDip));
        _bead.Set("u_color", BeadColor);
        _bead.Set("u_intensity", (float)(BeadIntensity * hotspots * erVisibility));
        GL.BindVertexArray(_beadVao);
        GL.DrawArrays(PrimitiveType.Points, 0, Sn1987aRings.HotspotCount);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(_torusVao);
        GL.DeleteBuffer(_torusVbo);
        GL.DeleteVertexArray(_beadVao);
        GL.DeleteBuffer(_beadVbo);
        _torus.Dispose();
        _bead.Dispose();
    }
}
