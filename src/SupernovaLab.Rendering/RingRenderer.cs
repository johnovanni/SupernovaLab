using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// The 3D view's distance rings (plan ruling P5): camera-facing circles centred on the explosion, drawn as 128-segment
/// line loops, grey #888780, 1 px, at each ring's opacity. Their labels are WPF text (ruling P6), not drawn here.
/// Create and use with the GL context current.
/// </summary>
public sealed class RingRenderer : IDisposable
{
    public const int Segments = 128;

    private readonly ShaderProgram _program = ShaderProgram.FromResources("ring.vert", "ring.frag");
    private readonly int _vao, _vbo;

    public RingRenderer()
    {
        var points = new float[Segments * 2];
        for (int i = 0; i < Segments; i++)
        {
            double a = 2 * Math.PI * i / Segments;
            points[2 * i] = (float)Math.Cos(a);
            points[2 * i + 1] = (float)Math.Sin(a);
        }
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, points.Length * sizeof(float), points, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.BindVertexArray(0);
    }

    /// <summary>Draws one ring of this radius (scene units) at this opacity (0 to 1).</summary>
    public void Render(OrbitCamera camera, Size viewportPx, double radius, float opacity)
    {
        if (!(radius > 0) || !double.IsFinite(radius) || !(opacity > 0)) return;
        var (right, up) = camera.ScreenAxes();
        _program.Use();
        _program.SetCamera(camera, viewportPx);
        _program.Set("u_right", (Vector3)right);
        _program.Set("u_up", (Vector3)up);
        _program.Set("u_radius", (float)radius);
        _program.Set("u_opacity", opacity);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.LineWidth(1f);
        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.LineLoop, 0, Segments);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(_vao);
        GL.DeleteBuffer(_vbo);
        _program.Dispose();
    }
}
