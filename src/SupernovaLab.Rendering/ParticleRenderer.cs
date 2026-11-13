using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// What the particle shader needs each frame, in scene units (physical cm divided by the view's unit on the CPU):
/// <paramref name="StarRadius"/> is R* and <paramref name="Expansion"/> is v_max · (t − t_bo), so a particle at speed
/// ratio s = v / v_max is at s · (R* + Expansion) (<c>ParticleKinematics.RadiusCm</c> with no shocks). With
/// <paramref name="CutHalfWidth"/> above 0 the wedge cutaway is on (plan ruling P7): particles in the removed quarter
/// (x &gt; 0 and z &gt; 0, scene coordinates) are not drawn, and those within <paramref name="CutHalfWidth"/> (3% of the framing
/// radius, scene units) of a cut face are left to <see cref="ParticleRenderer.RenderCutFaces"/>. Later tasks add the shock radii.
/// </summary>
public readonly record struct ParticleUniforms(float StarRadius, float Expansion, Vector3 Color, float Brightness,
    float CutHalfWidth = 0)
{
    /// <summary>The cut-face share of the framing radius (plan ruling P7): 3%.</summary>
    public const float CutFraction = 0.03f;

    /// <summary>The cut-face dots are this many times the size of the ordinary dots, so a face reads as a surface.</summary>
    public const float FacePointScale = 1.6f;

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
        _program.Set("u_cut", u.CutHalfWidth > 0 ? 1 : 0);
        _program.Set("u_cutHalf", u.CutHalfWidth);
        GL.Enable(EnableCap.ProgramPointSize);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Points, 0, _count);
        GL.BindVertexArray(0);
    }

    /// <summary>
    /// The cutaway's cut faces (plan ruling P7): the particles within <see cref="ParticleUniforms.CutHalfWidth"/> of either cut
    /// plane, each in its element colour (<paramref name="layerColors"/>, six HDR colours in <c>ElementLayer</c> order) at full
    /// opacity, opaque soft discs blended over what is there. Outer layers are drawn first and nickel last, so the nickel
    /// fingers show on top. Draw it after the bloom is built (<see cref="HdrPipeline.End"/>'s hook). Does nothing without a wedge.
    /// </summary>
    public void RenderCutFaces(OrbitCamera cam, Size viewportPx, double pxPerDip, ParticleUniforms u, float[] layerColors)
    {
        if (_count == 0 || !(u.CutHalfWidth > 0)) return;
        if (layerColors.Length < 3 * LayerColors.Count) throw new ArgumentException("Six layer colours are needed.", nameof(layerColors));
        _program.Use();
        _program.SetCamera(cam, viewportPx);
        _program.Set("u_starRadius", u.StarRadius);
        _program.Set("u_expansion", u.Expansion);
        _program.Set("u_shocks", 0);
        _program.Set("u_pointPx", (float)(ParticleUniforms.PointDip * ParticleUniforms.FacePointScale * pxPerDip));
        _program.Set("u_color", u.Color);
        _program.Set("u_alpha", ParticleUniforms.Alpha);
        _program.Set("u_cut", 2);
        _program.Set("u_cutHalf", u.CutHalfWidth);
        GL.Uniform3(_program.Location("u_layerColor"), LayerColors.Count, layerColors);
        GL.Enable(EnableCap.ProgramPointSize);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.BindVertexArray(_vao);
        for (int layer = LayerColors.Count - 1; layer >= 0; layer--)
        {
            _program.Set("u_faceLayer", layer);
            GL.DrawArrays(PrimitiveType.Points, 0, _count);
        }
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(_vao);
        GL.DeleteBuffer(_vbo);
        _program.Dispose();
    }
}
