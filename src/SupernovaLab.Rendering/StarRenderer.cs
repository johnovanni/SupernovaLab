using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// The star before the explosion (spec §6): a disk facing the camera, sized to the star's radius and coloured by its
/// surface temperature, with schematic limb darkening; and, with the same disk, the translucent gas around the star.
/// Create and use with the GL context current.
/// </summary>
public sealed class StarRenderer : IDisposable
{
    private readonly ShaderProgram _program = ShaderProgram.FromResources("star.vert", "star.frag");
    private readonly int _vao, _vbo;

    public StarRenderer()
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

    /// <summary>The gas around the star: pale blue-grey (schematic), at most this opaque.</summary>
    private static readonly (float R, float G, float B) GasColor = (0.55f, 0.65f, 0.85f);
    private const float GasAlpha = 0.35f;

    /// <summary>Draws the star of this radius (scene units) at the origin.</summary>
    public void Render(OrbitCamera camera, Size viewportPx, double radius, (float R, float G, float B) color)
        => Draw(camera, viewportPx, radius, color, mode: 0, alpha: 1);

    /// <summary>
    /// Draws the gas around the star (spec §6) as a translucent ball of this radius (scene units): a schematic haze, densest
    /// through its middle, with a faint rim. Draw it before the star and the ejecta.
    /// </summary>
    public void RenderGas(OrbitCamera camera, Size viewportPx, double radius)
        => Draw(camera, viewportPx, radius, GasColor, mode: 1, alpha: GasAlpha);

    private void Draw(OrbitCamera camera, Size viewportPx, double radius, (float R, float G, float B) color, int mode,
        float alpha)
    {
        var (right, up) = camera.ScreenAxes();
        _program.Use();
        _program.SetCamera(camera, viewportPx);
        _program.Set("u_right", (Vector3)right);
        _program.Set("u_up", (Vector3)up);
        _program.Set("u_radius", (float)radius);
        _program.Set("u_color", new Vector3(color.R, color.G, color.B));
        _program.Set("u_mode", mode);
        _program.Set("u_alpha", alpha);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
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
