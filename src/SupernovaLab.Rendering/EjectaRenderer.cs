using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// The expanding ejecta (spec §6): about 50,000 points in a spherical shell, drawn additively. The points are fixed in a
/// unit shell, uniform in volume between 0.7 and 1 of the radius and isotropic in direction, from a fixed seed, so the
/// picture is the same on every run (not tied to the run number); the shader scales them to the current radius. Create
/// and use with the GL context current.
/// </summary>
public sealed class EjectaRenderer : IDisposable
{
    public const int PointCount = 50_000;

    /// <summary>The shell's inner edge as a fraction of its outer radius (schematic).</summary>
    public const double InnerFraction = 0.7;

    private const int Seed = 1987;

    /// <summary>The points' size and opacity: soft 2.5 px dots at 35% (at full brightness).</summary>
    private const float PointDip = 2.5f, Alpha = 0.35f;

    private readonly ShaderProgram _program = ShaderProgram.FromResources("ejecta.vert", "ejecta.frag");
    private readonly int _vao, _vbo;

    public EjectaRenderer()
    {
        var points = UnitShell(PointCount, Seed);
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, points.Length * sizeof(float), points, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.BindVertexArray(0);
    }

    /// <summary>
    /// x, y, z of <paramref name="count"/> points: directions uniform on the sphere, radii uniform in volume between
    /// <see cref="InnerFraction"/> and 1. Deterministic for a seed.
    /// </summary>
    public static float[] UnitShell(int count, int seed)
    {
        var random = new Random(seed);
        double inner3 = InnerFraction * InnerFraction * InnerFraction;
        var xyz = new float[3 * count];
        for (int i = 0; i < count; i++)
        {
            double z = 2 * random.NextDouble() - 1, phi = 2 * Math.PI * random.NextDouble();
            double s = Math.Sqrt(1 - z * z);
            double r = Math.Cbrt(inner3 + (1 - inner3) * random.NextDouble());
            xyz[3 * i] = (float)(r * s * Math.Cos(phi));
            xyz[3 * i + 1] = (float)(r * s * Math.Sin(phi));
            xyz[3 * i + 2] = (float)(r * z);
        }
        return xyz;
    }

    /// <summary>Draws the shell at this outer radius (scene units), colour and brightness (0–1).</summary>
    public void Render(OrbitCamera camera, Size viewportPx, double pxPerDip, double radius, (float R, float G, float B) color,
        float brightness)
    {
        _program.Use();
        _program.SetCamera(camera, viewportPx);
        _program.Set("u_radius", (float)radius);
        _program.Set("u_pointPx", (float)(PointDip * pxPerDip));
        _program.Set("u_color", new Vector3(color.R, color.G, color.B) * brightness);
        _program.Set("u_alpha", Alpha);
        GL.Enable(EnableCap.ProgramPointSize);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Points, 0, PointCount);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(_vao);
        GL.DeleteBuffer(_vbo);
        _program.Dispose();
    }
}
