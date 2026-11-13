using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// Translucent cones from the centre, fading toward their rims: a gamma-ray burst's two jets along the rotation axis
/// (scene z; spec §6), and the pulsar's beams for <see cref="RemnantRenderer"/>. Create and use with the GL context current.
/// </summary>
public sealed class JetRenderer : IDisposable
{
    private const int Segments = 48;

    /// <summary>The jets: bluish white (schematic), reaching 1.6 × the framing radius.</summary>
    private static readonly Vector3 JetColor = new(0.75f, 0.85f, 1f);
    public const double JetLength = 1.6;

    /// <summary>Task 11: through the HDR target the jets are drawn at this multiple of their ordinary strength, so they bloom (scene light, not a fixed colour).</summary>
    public const float HdrGain = 4f;

    /// <summary>The jets' gain: <see cref="HdrGain"/> through the HDR target, 1 without it (where the colour is clamped anyway).</summary>
    public static float GainFor(bool hdr) => hdr ? HdrGain : 1f;

    private readonly ShaderProgram _program = ShaderProgram.FromResources("cone.vert", "cone.frag");
    private readonly int _vao, _vbo;

    public JetRenderer()
    {
        var mesh = new List<float>();
        for (int i = 0; i < Segments; i++)
        {
            double a0 = 2 * Math.PI * i / Segments, a1 = 2 * Math.PI * (i + 1) / Segments;
            mesh.AddRange(new[] { 0f, 0f, 0f });
            mesh.AddRange(new[] { (float)Math.Cos(a0), (float)Math.Sin(a0), 1f });
            mesh.AddRange(new[] { (float)Math.Cos(a1), (float)Math.Sin(a1), 1f });
        }
        var data = mesh.ToArray();
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Length * sizeof(float), data, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.BindVertexArray(0);
    }

    /// <summary>The two jets along ±z, of this half-opening angle, <see cref="JetLength"/> × <paramref name="scale"/> long.</summary>
    public void RenderJets(OrbitCamera camera, Size viewportPx, double halfAngleRad, double scale, bool hdr = false)
    {
        var color = JetColor * GainFor(hdr);
        foreach (var axis in new[] { Vector3d.UnitZ, -Vector3d.UnitZ })
            RenderCone(camera, viewportPx, axis, JetLength * scale, halfAngleRad, color, 0.9f);
    }

    /// <summary>One cone from the origin along <paramref name="axis"/>, additively blended.</summary>
    public void RenderCone(OrbitCamera camera, Size viewportPx, Vector3d axis, double length, double halfAngleRad,
        Vector3 color, float alpha)
    {
        var a = axis.Normalized();
        var helper = Math.Abs(a.Z) < 0.9 ? Vector3d.UnitZ : Vector3d.UnitX;
        var e1 = Vector3d.Cross(a, helper).Normalized();
        var e2 = Vector3d.Cross(a, e1);
        // OpenTK stores rows first and GL reads columns first, so these rows arrive as the GLSL matrix's columns.
        var basis = new Matrix3((Vector3)e1, (Vector3)e2, (Vector3)a);

        _program.Use();
        _program.SetCamera(camera, viewportPx);
        GL.UniformMatrix3(_program.Location("u_basis"), false, ref basis);
        _program.Set("u_length", (float)length);
        _program.Set("u_tanHalf", (float)Math.Tan(halfAngleRad));
        _program.Set("u_color", color);
        _program.Set("u_alpha", alpha);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3 * Segments);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(_vao);
        GL.DeleteBuffer(_vbo);
        _program.Dispose();
    }
}
