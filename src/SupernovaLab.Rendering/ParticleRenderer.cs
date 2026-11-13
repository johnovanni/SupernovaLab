using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// What the particle shader needs each frame, in scene units (physical cm divided by the view's unit on the CPU):
/// <paramref name="StarRadius"/> is R* and <paramref name="Expansion"/> is v_max · (t − t_bo), so a particle at speed
/// ratio s = v / v_max is at s · (R* + Expansion) (<c>ParticleKinematics.RadiusCm</c> with no shocks). Later tasks add the
/// shock radii, the per-layer colours and the wedge.
/// </summary>
public readonly record struct ParticleUniforms(float StarRadius, float Expansion, Vector3 Color, float Brightness)
{
    /// <summary>The dots' size (dip) and opacity at full brightness: soft 2.5 dip dots at 35%, as Project 1.</summary>
    public const float PointDip = 2.5f, Alpha = 0.35f;
}

/// <summary>
/// The debris as GPU point sprites (Project 2), drawn additively with a soft Gaussian falloff. The final vertex layout is
/// location 0 = vec3 velocity in units of v_max (|v| at most 1), location 1 = float layer index (0 to 5, <c>ElementLayer</c>
/// order), location 2 = float seed in [0, 1), interleaved. Create, upload and draw with the GL context current.
/// </summary>
public sealed class ParticleRenderer : IDisposable
{
    private const int FloatsPerParticle = 5;

    private readonly ShaderProgram _program = ShaderProgram.FromResources("particle.vert", "particle.frag");
    private readonly int _vao, _vbo;
    private int _count;

    public ParticleRenderer(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, capacity * FloatsPerParticle * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);
        int stride = FloatsPerParticle * sizeof(float);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, stride, 4 * sizeof(float));
        GL.BindVertexArray(0);
    }

    public int Capacity { get; }

    /// <summary>Particles currently held.</summary>
    public int Count => _count;

    /// <summary>
    /// Replaces the particles: <paramref name="velocity"/> holds x, y, z per particle (units of v_max), <paramref name="layer"/>
    /// and <paramref name="seed"/> one value each. Only the first <paramref name="count"/> (at most the capacity) are kept.
    /// </summary>
    public void Upload(float[] velocity, float[] layer, float[] seed, int count)
    {
        ArgumentNullException.ThrowIfNull(velocity);
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(seed);
        count = Math.Clamp(count, 0, Capacity);
        if (velocity.Length < 3 * count || layer.Length < count || seed.Length < count)
            throw new ArgumentException("The arrays are shorter than the particle count.");
        _count = count;
        if (count == 0) return;
        var packed = Interleave(velocity, layer, seed, count);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, packed.Length * sizeof(float), packed);
    }

    /// <summary>The interleaved vertex data: vx, vy, vz, layer, seed for each of the first <paramref name="count"/> particles.</summary>
    public static float[] Interleave(float[] velocity, float[] layer, float[] seed, int count)
    {
        var packed = new float[FloatsPerParticle * count];
        for (int i = 0; i < count; i++)
        {
            packed[FloatsPerParticle * i] = velocity[3 * i];
            packed[FloatsPerParticle * i + 1] = velocity[3 * i + 1];
            packed[FloatsPerParticle * i + 2] = velocity[3 * i + 2];
            packed[FloatsPerParticle * i + 3] = layer[i];
            packed[FloatsPerParticle * i + 4] = seed[i];
        }
        return packed;
    }

    public void Render(OrbitCamera cam, Size viewportPx, double pxPerDip, ParticleUniforms u)
    {
        if (_count == 0) return;
        _program.Use();
        _program.SetCamera(cam, viewportPx);
        _program.Set("u_starRadius", u.StarRadius);
        _program.Set("u_expansion", u.Expansion);
        _program.Set("u_shocks", 0);
        _program.Set("u_pointPx", (float)(ParticleUniforms.PointDip * pxPerDip));
        _program.Set("u_color", u.Color * u.Brightness);
        _program.Set("u_alpha", ParticleUniforms.Alpha);
        GL.Enable(EnableCap.ProgramPointSize);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Points, 0, _count);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(_vao);
        GL.DeleteBuffer(_vbo);
        _program.Dispose();
    }
}
