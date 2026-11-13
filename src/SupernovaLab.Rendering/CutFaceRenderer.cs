using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SupernovaLab.Core.Structure;

namespace SupernovaLab.Rendering;

/// <summary>
/// What <see cref="CutFaceRenderer"/> needs, in scene units (cm divided by the view's unit on the CPU): the layers' outer
/// radii with their HDR colours (centre outward), the finger tips' radii (NaN or 0 for none), and the finger cones.
/// </summary>
public sealed record CutFaceUniforms(IReadOnlyList<(float OuterRadius, Vector3 Color)> Layers, float NickelMaxRadius, float HydrogenMinRadius,
    Vector3 NickelColor, Vector3 HydrogenColor, IReadOnlyList<Vector3> ConeAxes, float ConeCos);

/// <summary>
/// The debris cutaway's cut faces (ruling R-P2-21): the two quarter planes bounding the removed quarter, x &gt; 0 and
/// z &gt; 0 in scene coordinates, drawn as opaque quads shaded per pixel (<c>cutface.frag</c>, mirroring
/// <see cref="CutFaceModel.LayerAt"/>). Draw them FIRST in the frame, with the depth test and depth write on, so the particles
/// (drawn later with the depth test on and depth write off) are hidden behind them. Alpha 1 marks the face for the tone map's bloom mask.
/// </summary>
public sealed class CutFaceRenderer : IDisposable
{
    public const int MaxCones = 10;

    private readonly ShaderProgram _program = ShaderProgram.FromResources("cutface.vert", "cutface.frag");
    private readonly int _vao, _vbo;

    public CutFaceRenderer()
    {
        // One quad, a in [-1, 1] (along y), b in [0, 1] (along z, or x for the second plane).
        float[] corners = { -1, 0, 1, 0, 1, 1, -1, 0, 1, 1, -1, 1 };
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, corners.Length * sizeof(float), corners, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.BindVertexArray(0);
    }

    /// <summary>Draws both faces out to <paramref name="extent"/> scene units (the debris' outer radius).</summary>
    public void Render(OrbitCamera cam, Size viewportPx, float extent, CutFaceUniforms u)
    {
        int n = Math.Min(u.Layers.Count, LayerColors.Count);
        if (n == 0 || !(extent > 0)) return;
        var outer = new float[LayerColors.Count];
        var colors = new float[3 * LayerColors.Count];
        for (int i = 0; i < n; i++)
        {
            outer[i] = u.Layers[i].OuterRadius;
            colors[3 * i] = u.Layers[i].Color.X;
            colors[3 * i + 1] = u.Layers[i].Color.Y;
            colors[3 * i + 2] = u.Layers[i].Color.Z;
        }
        int cones = Math.Min(u.ConeAxes.Count, MaxCones);
        var axes = new float[3 * MaxCones];
        for (int i = 0; i < cones; i++)
        {
            axes[3 * i] = u.ConeAxes[i].X;
            axes[3 * i + 1] = u.ConeAxes[i].Y;
            axes[3 * i + 2] = u.ConeAxes[i].Z;
        }

        _program.Use();
        _program.SetCamera(cam, viewportPx);
        _program.Set("u_extent", extent);
        _program.Set("u_layerCount", n);
        GL.Uniform1(_program.Location("u_layerOuter"), LayerColors.Count, outer);
        GL.Uniform3(_program.Location("u_layerColor"), LayerColors.Count, colors);
        _program.Set("u_nickelColor", u.NickelColor);
        _program.Set("u_hydrogenColor", u.HydrogenColor);
        _program.Set("u_nickelMax", float.IsFinite(u.NickelMaxRadius) ? u.NickelMaxRadius : -1f);
        _program.Set("u_hydrogenMin", float.IsFinite(u.HydrogenMinRadius) ? u.HydrogenMinRadius : -1f);
        _program.Set("u_coneCount", cones);
        GL.Uniform3(_program.Location("u_coneAxis"), MaxCones, axes);
        _program.Set("u_coneCos", u.ConeCos);

        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Less);
        GL.DepthMask(true);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.BindVertexArray(_vao);
        for (int plane = 0; plane < 2; plane++)
        {
            _program.Set("u_plane", plane);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }
        GL.BindVertexArray(0);
        GL.Disable(EnableCap.DepthTest);   // the particles switch it back on (writing no depth); everything else ignores depth
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(_vao);
        GL.DeleteBuffer(_vbo);
        _program.Dispose();
    }
}
