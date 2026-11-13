using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// The wedge cutaway of the star (plan rulings P7 and P8): the presupernova layers as nested discs (outer radius as a fraction
/// of the star's radius, centre outward, with HDR colours) and the shock front (a fraction of the radius; NaN for none).
/// </summary>
public sealed record StarCutaway(IReadOnlyList<(float OuterFraction, Vector3 Color)> Layers, float ShockFraction)
{
    /// <summary>The gas inside the shock is this much brighter (schematic).</summary>
    public const float InsideBoost = 1.6f;
}

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

    /// <summary>The wedge's angle about the scene's y axis (ruling R-P2-22), radians.</summary>
    public double WedgeAngleRad { get; set; }

    /// <summary>The gas around the star: pale blue-grey (schematic), at most this opaque.</summary>
    private static readonly (float R, float G, float B) GasColor = (0.55f, 0.65f, 0.85f);
    private const float GasAlpha = 0.35f;

    /// <summary>The shock ring's HDR colour: hot white, bright enough to stay white after the exposure (0.2) and tone map.</summary>
    private static readonly Vector3 RingColor = new(30f, 28f, 24f);

    /// <summary>Draws the star of this radius (scene units) at the origin.</summary>
    public void Render(OrbitCamera camera, Size viewportPx, double radius, (float R, float G, float B) color, bool cutaway = false)
        => Draw(camera, viewportPx, radius, color, mode: 0, alpha: 1, cut: cutaway ? 1 : 0, pass: 0, cutaway: null);

    /// <summary>
    /// The star's cut faces (the wedge's two planes seen through the removed quarter), with the layer discs and the shock front.
    /// Draw it after the bloom is built (<see cref="HdrPipeline.End"/>'s hook), so the faces keep their element colours.
    /// </summary>
    public void RenderCutFaces(OrbitCamera camera, Size viewportPx, double radius, StarCutaway cutaway)
        => Draw(camera, viewportPx, radius, (1f, 1f, 1f), mode: 0, alpha: 1, cut: 1, pass: 1, cutaway);

    /// <summary>
    /// Draws the gas around the star (spec §6) as a translucent ball of this radius (scene units): a schematic haze, densest
    /// through its middle, with a faint rim. Draw it before the star and the ejecta.
    /// </summary>
    public void RenderGas(OrbitCamera camera, Size viewportPx, double radius)
        => Draw(camera, viewportPx, radius, GasColor, mode: 1, alpha: GasAlpha, cut: 0, pass: 0, cutaway: null);

    private void Draw(OrbitCamera camera, Size viewportPx, double radius, (float R, float G, float B) color, int mode,
        float alpha, int cut, int pass, StarCutaway? cutaway)
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
        _program.Set("u_cut", cut);
        _program.Set("u_pass", pass);
        WedgeUniforms.Set(_program, WedgeAngleRad);
        var back = camera.Backward;
        _program.Set("u_back", new Vector3((float)back.X, (float)back.Y, (float)back.Z));
        if (cutaway is not null)
        {
            int n = Math.Min(cutaway.Layers.Count, LayerColors.Count);
            var outer = new float[LayerColors.Count];
            var colors = new float[3 * LayerColors.Count];
            for (int i = 0; i < n; i++)
            {
                outer[i] = cutaway.Layers[i].OuterFraction;
                colors[3 * i] = cutaway.Layers[i].Color.X;
                colors[3 * i + 1] = cutaway.Layers[i].Color.Y;
                colors[3 * i + 2] = cutaway.Layers[i].Color.Z;
            }
            _program.Set("u_layerCount", n);
            GL.Uniform1(_program.Location("u_layerOuter"), LayerColors.Count, outer);
            GL.Uniform3(_program.Location("u_layerColor"), LayerColors.Count, colors);
            _program.Set("u_shockR", float.IsFinite(cutaway.ShockFraction) ? cutaway.ShockFraction : -1f);
            _program.Set("u_ringColor", RingColor);
            _program.Set("u_insideBoost", StarCutaway.InsideBoost);
        }
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
