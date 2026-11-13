using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// Translucent shells around the explosion's centre (plan ruling P8): each pixel's brightness is the analytic column density
/// along its line of sight (<see cref="ColumnDensity"/>, mirrored by <c>shell.frag</c>), normalized by the largest column, so a
/// shell is limb-brightened as an optically thin glowing gas is. Drawn additively into the HDR target (before the bloom, so it
/// glows). With the cutaway on, the line of sight's part inside the removed quarter (the wedge frame of
/// <see cref="WedgeAngleRad"/>, as the particles) is not counted. Create and use with the GL context current.
/// </summary>
public sealed class ShellRenderer : IDisposable
{
    private readonly ShaderProgram _program = ShaderProgram.FromResources("shell.vert", "shell.frag");
    private readonly int _vao, _vbo;

    public ShellRenderer()
    {
        float[] corners = { -1, -1, 1, -1, 1, 1, -1, -1, 1, 1, -1, 1 };
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, corners.Length * sizeof(float), corners, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.BindVertexArray(0);
    }

    /// <summary>The wedge's angle about the scene's y axis (ruling R-P2-22), radians.</summary>
    public double WedgeAngleRad { get; set; }

    /// <summary>Whether the cutaway's removed quarter is left out of each line of sight.</summary>
    public bool Cutaway { get; set; }

    /// <summary>
    /// Draws one shell centred on the origin: <paramref name="kind"/> between <paramref name="rInUnits"/> and
    /// <paramref name="rOutUnits"/> (scene units), in this colour, the brightest line of sight at <paramref name="intensity"/> (HDR).
    /// </summary>
    public void Render(OrbitCamera cam, Size viewportPx, ShellKind kind, double rInUnits, double rOutUnits,
        (float R, float G, float B) color, float intensity)
    {
        if (!(rOutUnits > 0) || !(intensity > 0) || !(viewportPx.Height > 0)) return;
        double rIn = Math.Clamp(double.IsFinite(rInUnits) ? rInUnits : 0, 0, rOutUnits);
        double peak = ColumnDensity.Peak(kind, rIn, rOutUnits);
        if (!(peak > 0)) return;
        var (right, up) = cam.ScreenAxes();
        var forward = -cam.Backward;
        double tanY = Math.Tan(cam.FieldOfViewDeg * Math.PI / 360);
        double aspect = viewportPx.Width / viewportPx.Height;

        _program.Use();
        _program.Set("u_forward", (Vector3)forward);
        _program.Set("u_right", (Vector3)right);
        _program.Set("u_up", (Vector3)up);
        _program.Set("u_tanHalf", new Vector2((float)(tanY * aspect), (float)tanY));
        _program.Set("u_eye", (Vector3)cam.Eye);
        _program.Set("u_kind", (int)kind);
        _program.Set("u_rIn", (float)rIn);
        _program.Set("u_rOut", (float)rOutUnits);
        _program.Set("u_peak", (float)peak);
        _program.Set("u_color", new Vector3(color.R, color.G, color.B));
        _program.Set("u_intensity", intensity);
        _program.Set("u_cut", Cutaway ? 1 : 0);
        WedgeUniforms.Set(_program, WedgeAngleRad);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(_vao);
        GL.DeleteBuffer(_vbo);
        _program.Dispose();
    }
}
