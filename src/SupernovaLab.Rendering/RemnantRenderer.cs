using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// The remnant marker (spec §6): a small bright point, with a dark outline so it shows against bright debris, for a neutron star,
/// with two sweeping beam cones when it is a pulsar; a black disk with a thin bright ring for a black hole; a small white point for a
/// white dwarf. Sizes are fixed on screen (no remnant is resolvable at these scales). Plan Task 6 (spec §7.2): the marker sits where
/// the kick has carried the remnant (<see cref="SceneState.RemnantOffsetCm"/>, as the Close-up), and the beams turn about the
/// rotation axis (scene z) with the magnetic axis tilted by <see cref="SceneState.BeamTiltRad"/> and the half-width
/// <see cref="SceneState.BeamHalfAngleRad"/> of the period (both <see cref="Core.Compact.Magnetosphere"/>'s, shared with the
/// Close-up), at the phase the caller gives (from the shared beam clock: the real period is slowed down). Create and use with the GL
/// context current.
/// </summary>
public sealed class RemnantRenderer : IDisposable
{
    /// <summary>Marker sizes in device-independent pixels.</summary>
    private const float NeutronStarDip = 13, WhiteDwarfDip = 10, BlackHoleDip = 22;

    /// <summary>[schematic] The beams' length (scene units).</summary>
    private const double BeamLength = 0.9;

    private static readonly Vector3 NeutronStarColor = new(0.8f, 0.9f, 1f), WhiteDwarfColor = new(1f, 1f, 1f),
        RingColor = new(1f, 0.85f, 0.6f);

    /// <summary>The beams' colour (schematic), shared with the Close-up.</summary>
    public static readonly Vector3 BeamColor = new(0.6f, 1f, 0.85f);

    private readonly ShaderProgram _program = ShaderProgram.FromResources("marker.vert", "marker.frag");
    private readonly int _vao = GL.GenVertexArray();
    private readonly JetRenderer _cones;

    /// <summary>The beams are drawn with the jets' cones.</summary>
    public RemnantRenderer(JetRenderer cones) => _cones = cones;

    /// <summary>
    /// The magnetic axis at this tilt from +z and rotation phase: (sin α cos φ, sin α sin φ, cos α). One beam goes out along it, the
    /// other along its opposite; the Close-up uses the same axis (and nsurface.frag the same convention).
    /// </summary>
    public static Vector3d MagneticAxis(double tiltRad, double phaseRad)
        => new(Math.Sin(tiltRad) * Math.Cos(phaseRad), Math.Sin(tiltRad) * Math.Sin(phaseRad), Math.Cos(tiltRad));

    /// <summary>
    /// Draws the marker at <paramref name="position"/> (scene units); for a pulsar, its beams at this rotation phase (radians), tilt
    /// and half-opening angle, this long (scene units).
    /// </summary>
    public void Render(OrbitCamera camera, Size viewportPx, double pxPerDip, SceneMarker marker, double beamPhaseRad,
        double beamLength, Vector3d position, double beamTiltRad, double beamHalfAngleRad)
    {
        if (marker == SceneMarker.None) return;
        if (marker == SceneMarker.Pulsar && double.IsFinite(beamTiltRad) && beamHalfAngleRad > 0)
        {
            var axis = MagneticAxis(beamTiltRad, beamPhaseRad);
            _cones.RenderCone(camera, viewportPx, axis, beamLength, beamHalfAngleRad, BeamColor, 0.8f, position);
            _cones.RenderCone(camera, viewportPx, -axis, beamLength, beamHalfAngleRad, BeamColor, 0.8f, position);
        }

        var (size, color, kind) = marker switch
        {
            SceneMarker.BlackHole => (BlackHoleDip, RingColor, 1),
            SceneMarker.WhiteDwarf => (WhiteDwarfDip, WhiteDwarfColor, 0),
            _ => (NeutronStarDip, NeutronStarColor, 0),
        };
        _program.Use();
        _program.SetCamera(camera, viewportPx);
        _program.Set("u_sizePx", (float)(size * pxPerDip));
        _program.Set("u_color", color);
        _program.Set("u_kind", kind);
        _program.Set("u_position", (Vector3)position);
        GL.Enable(EnableCap.ProgramPointSize);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Points, 0, 1);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(_vao);
        _program.Dispose();
    }
}
