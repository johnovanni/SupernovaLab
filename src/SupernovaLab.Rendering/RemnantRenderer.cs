using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// The remnant marker at the centre (spec §6): a small bright point, with a dark outline so it shows against bright
/// debris, for a neutron star, with two sweeping beam cones
/// when it is a pulsar; a black disk with a thin bright ring for a black hole; a small white point for a white dwarf.
/// Sizes are fixed on screen (no remnant is resolvable at these scales). The beams turn about the rotation axis (scene z)
/// with the magnetic axis tilted 45° from it [schematic], at the phase the caller gives (the real period is slowed
/// down). Create and use with the GL context current.
/// </summary>
public sealed class RemnantRenderer : IDisposable
{
    /// <summary>Marker sizes in device-independent pixels.</summary>
    private const float NeutronStarDip = 13, WhiteDwarfDip = 10, BlackHoleDip = 22;

    /// <summary>[schematic] The beams' tilt from the rotation axis, half-opening angle and length (scene units).</summary>
    private const double BeamTiltRad = Math.PI / 4, BeamHalfAngleRad = 10 * Math.PI / 180, BeamLength = 0.9;

    private static readonly Vector3 NeutronStarColor = new(0.8f, 0.9f, 1f), WhiteDwarfColor = new(1f, 1f, 1f),
        RingColor = new(1f, 0.85f, 0.6f), BeamColor = new(0.6f, 1f, 0.85f);

    private readonly ShaderProgram _program = ShaderProgram.FromResources("marker.vert", "marker.frag");
    private readonly int _vao = GL.GenVertexArray();
    private readonly JetRenderer _cones;

    /// <summary>The beams are drawn with the jets' cones.</summary>
    public RemnantRenderer(JetRenderer cones) => _cones = cones;

    /// <summary>Draws the marker; for a pulsar, its beams at this rotation phase (radians) and length (scene units).</summary>
    public void Render(OrbitCamera camera, Size viewportPx, double pxPerDip, SceneMarker marker, double beamPhaseRad,
        double beamLength = BeamLength)
    {
        if (marker == SceneMarker.None) return;
        if (marker == SceneMarker.Pulsar)
        {
            var axis = new Vector3d(Math.Sin(BeamTiltRad) * Math.Cos(beamPhaseRad), Math.Sin(BeamTiltRad) * Math.Sin(beamPhaseRad),
                Math.Cos(BeamTiltRad));
            _cones.RenderCone(camera, viewportPx, axis, beamLength, BeamHalfAngleRad, BeamColor, 0.8f);
            _cones.RenderCone(camera, viewportPx, -axis, beamLength, BeamHalfAngleRad, BeamColor, 0.8f);
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
