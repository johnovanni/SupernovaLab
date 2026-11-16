using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// What <c>lensing.frag</c> needs for one frame (all lengths in GM/c², in the tracer's chart, ruling R-P3-8: the scene's axes,
/// spin along +z): the spin, the camera's position and basis (forward, right, up: unit vectors, as <see cref="OrbitCamera"/>
/// gives them), tan of half the field of view across and up, the escape radius
/// (<see cref="Core.Compact.BlackHoleOptics.EscapeRadiusFor"/>), a neutron star's surface radius (0 for a black hole) and its
/// colour, and the grid switch (plan ruling P3-8). Plan Task 6: the surface's shading (<c>nsurface.frag</c>): the spin phase about +z,
/// the magnetic tilt, the cosine of the polar cap's angular radius, and whether the schematic markings are drawn. The shading is
/// applied when the ray map is sampled, so it never causes a retrace (<see cref="RayMapPlanner.SameRays"/>). Plan Task 10: the grid lines' colour
/// (<see cref="GridColor"/>), likewise applied when the map is sampled.
/// </summary>
public readonly record struct LensingUniforms(float Spin, Vector3 CameraPosition, Vector3 Forward, Vector3 Right, Vector3 Up,
    Vector2 TanHalf, float EscapeRadius, float SurfaceRadius = 0, Vector3 SurfaceColor = default, bool Grid = false,
    float SurfacePhaseRad = 0, float SurfaceTiltRad = 0, float SurfaceCapCos = 1, bool SurfaceMarkings = false, Vector3 GridColor = default);

/// <summary>
/// The Close-up's light bending (spec §6.6, plan rulings P3-7 and P3-9, ruling R-P3-10). <c>lensing.frag</c>, the GLSL mirror of
/// <see cref="Core.Compact.BlackHoleOptics.TraceRay"/>, traces one ray per pixel of an offscreen target at a
/// <see cref="LensingLevel"/>; <c>upsample.frag</c> then stretches the image bilinearly over whatever framebuffer and viewport were
/// current when the pass was called (the HDR target, or the GL control's own). OpenGL 4.1 has no compute shaders, so the passes are
/// fragment-shader passes. Create and use with the GL context current.
/// <para><b>The ray cache (R-P3-10).</b> When an RGBA32F target can be made (<see cref="CachesRays"/>), the tracer writes each
/// ray's fate and exit direction (or surface hit point) into a kept <i>ray map</i>, only on the frames a
/// <see cref="RayMapPlanner"/> asks for (<see cref="Render(Size, RayMapStep, in LensingUniforms, int)"/>): the whole map, or one
/// band of rows of a finer map built behind the shown one (with the scissor test, so only that band is traced). Every frame then
/// colours the shown map from the environment as it is now (<c>lensing_shade.frag</c>, into an RGBA16F target with the HDR path,
/// RGBA8 without) and upsamples it. Without RGBA32F the pass traces colour directly every frame, as before
/// (<see cref="RenderDirect"/>).</para>
/// </summary>
public sealed class LensingPass : ILensingPass
{
    // Plan Task 6: the tracer (for its direct path) and the shading pass both link nsurface.frag, the neutron star's surface shading.
    private readonly ShaderProgram _trace = ShaderProgram.FromResources("fullscreen.vert", "lensing.frag", "nsurface.frag");
    private readonly ShaderProgram _shade = ShaderProgram.FromResources("fullscreen.vert", "lensing_shade.frag", "nsurface.frag");
    private readonly ShaderProgram _upsample = ShaderProgram.FromResources("fullscreen.vert", "upsample.frag");
    private readonly int _vao = GL.GenVertexArray();
    private readonly bool _hdr;
    private Target _color, _shown, _back;
    private readonly int[] _viewport = new int[4];

    /// <summary>An offscreen colour target: its framebuffer, texture and size.</summary>
    private struct Target
    {
        public int Fbo, Tex, Width, Height;
    }

    /// <param name="hdr">True for an RGBA16F colour target (the HDR path), false for RGBA8 (the no-HDR path).</param>
    public LensingPass(bool hdr)
    {
        _hdr = hdr;
        // Probe: the ray map needs a renderable RGBA32F target (core since OpenGL 3.0; checked, not assumed).
        var probe = new Target();
        CachesRays = Create(ref probe, 4, 4, PixelInternalFormat.Rgba32f);
        Release(ref probe);
    }

    /// <summary>Whether the ray map can be cached (an RGBA32F target works); false: <see cref="RenderDirect"/> every frame.</summary>
    public bool CachesRays { get; }

    /// <summary>The traced image's size at the last frame (pixels): the shown ray map, or the direct path's target.</summary>
    public (int Width, int Height) TargetSize => (_color.Width, _color.Height);

    /// <summary>
    /// Ruling R-P3-10: does this frame's tracing (<paramref name="step"/>, from the <see cref="RayMapPlanner"/>), then colours the shown
    /// ray map from the environment and upsamples it into the current framebuffer. False when a target could not be made (nothing
    /// is drawn; the caller should forget the map).
    /// </summary>
    public bool Render(Size viewportPx, RayMapStep step, in LensingUniforms u, int environmentCubeMap)
    {
        if (!CachesRays || viewportPx.Width <= 0 || viewportPx.Height <= 0) return false;
        GL.GetInteger(GetPName.FramebufferBinding, out int previousFbo);
        GL.GetInteger(GetPName.Viewport, _viewport);
        GL.Disable(EnableCap.Blend);
        GL.ColorMask(true, true, true, true);   // the ray map keeps each ray's fate in alpha
        GL.BindVertexArray(_vao);
        bool ok = true;
        if (step.Work == RayMapWork.Trace)
            ok = Trace(ref _shown, step, u, environmentCubeMap);
        else if (step.Work == RayMapWork.RefineStrip)
        {
            ok = Trace(ref _back, step, u, environmentCubeMap);
            if (ok && step.Completes) (_shown, _back) = (_back, _shown);
        }
        ok = ok && _shown.Tex != 0 && Ensure(ref _color, _shown.Width, _shown.Height, ColorFormat);
        if (ok)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _color.Fbo);
            GL.Viewport(0, 0, _color.Width, _color.Height);
            _shade.Use();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _shown.Tex);
            _shade.Set("u_rays", 0);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.TextureCubeMap, environmentCubeMap);
            _shade.Set("u_env", 1);
            SetSurfaceUniforms(_shade, u);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            GL.BindTexture(TextureTarget.TextureCubeMap, 0);
            GL.ActiveTexture(TextureUnit.Texture0);
        }
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, previousFbo);
        GL.Viewport(_viewport[0], _viewport[1], _viewport[2], _viewport[3]);
        if (ok) Upsample();
        GL.BindVertexArray(0);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        return ok;
    }

    /// <summary>The fallback without a ray cache: traces the view's colour at this level, then upsamples it (Task 4's pass).</summary>
    public void RenderDirect(Size viewportPx, LensingLevel level, in LensingUniforms u, int environmentCubeMap)
    {
        int vw = (int)viewportPx.Width, vh = (int)viewportPx.Height;
        if (vw <= 0 || vh <= 0) return;
        var (w, h) = LensingLevels.TargetSize(vw, vh, level);
        GL.GetInteger(GetPName.FramebufferBinding, out int previousFbo);
        GL.GetInteger(GetPName.Viewport, _viewport);
        bool ok = Ensure(ref _color, w, h, ColorFormat);
        if (ok)
        {
            GL.Disable(EnableCap.Blend);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _color.Fbo);
            GL.Viewport(0, 0, w, h);
            GL.BindVertexArray(_vao);
            SetTraceUniforms(u, environmentCubeMap, output: 0);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, previousFbo);
        GL.Viewport(_viewport[0], _viewport[1], _viewport[2], _viewport[3]);
        if (ok)
        {
            GL.BindVertexArray(_vao);
            Upsample();
        }
        GL.BindVertexArray(0);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        GL.BindTexture(TextureTarget.TextureCubeMap, 0);
    }

    private PixelInternalFormat ColorFormat => _hdr ? PixelInternalFormat.Rgba16f : PixelInternalFormat.Rgba8;

    /// <summary>Traces the step's rows of a ray map of the step's size into <paramref name="map"/> (made or resized as needed).</summary>
    private bool Trace(ref Target map, RayMapStep step, in LensingUniforms u, int environmentCubeMap)
    {
        if (!Ensure(ref map, step.Width, step.Height, PixelInternalFormat.Rgba32f)) return false;
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, map.Fbo);
        GL.Viewport(0, 0, step.Width, step.Height);
        bool band = step.FirstRow > 0 || step.Rows < step.Height;
        if (band)
        {
            GL.Enable(EnableCap.ScissorTest);
            GL.Scissor(0, step.FirstRow, step.Width, step.Rows);
        }
        SetTraceUniforms(u, environmentCubeMap, output: 1);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        if (band) GL.Disable(EnableCap.ScissorTest);
        GL.BindTexture(TextureTarget.TextureCubeMap, 0);
        return true;
    }

    private void SetTraceUniforms(in LensingUniforms u, int environmentCubeMap, int output)
    {
        _trace.Use();
        _trace.Set("u_output", output);
        _trace.Set("u_spin", u.Spin);
        _trace.Set("u_camPos", u.CameraPosition);
        _trace.Set("u_camForward", u.Forward);
        _trace.Set("u_camRight", u.Right);
        _trace.Set("u_camUp", u.Up);
        _trace.Set("u_tanHalf", u.TanHalf);
        _trace.Set("u_escapeRadius", u.EscapeRadius);
        _trace.Set("u_surfaceRadius", u.SurfaceRadius);
        SetSurfaceUniforms(_trace, u);
        _trace.Set("u_grid", u.Grid ? 1 : 0);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.TextureCubeMap, environmentCubeMap);
        _trace.Set("u_env", 0);
    }

    /// <summary>The surface's shading (nsurface.frag): its colour, spin phase, tilt, polar cap and markings switch.</summary>
    private static void SetSurfaceUniforms(ShaderProgram program, in LensingUniforms u)
    {
        program.Set("u_surfaceColor", u.SurfaceColor);
        program.Set("u_surfacePhase", u.SurfacePhaseRad);
        program.Set("u_surfaceTilt", u.SurfaceTiltRad);
        program.Set("u_capCos", u.SurfaceCapCos);
        program.Set("u_markings", u.SurfaceMarkings ? 1 : 0);
        program.Set("u_gridColor", u.GridColor);   // plan Task 10: the grid lines, applied when the map is sampled
    }

    /// <summary>Stretches the colour target over the current framebuffer and viewport (alpha masked off, as before).</summary>
    private void Upsample()
    {
        _upsample.Use();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _color.Tex);
        _upsample.Set("u_src", 0);
        GL.ColorMask(true, true, true, false);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GL.ColorMask(true, true, true, true);
    }

    private static bool Ensure(ref Target t, int w, int h, PixelInternalFormat format)
        => (t.Tex != 0 && t.Width == w && t.Height == h) || Create(ref t, w, h, format);

    private static bool Create(ref Target t, int w, int h, PixelInternalFormat format)
    {
        Release(ref t);
        GL.GetInteger(GetPName.FramebufferBinding, out int previousFbo);
        t.Tex = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, t.Tex);
        var (pixelFormat, type) = format switch
        {
            PixelInternalFormat.Rgba32f => (PixelFormat.Rgba, PixelType.Float),
            PixelInternalFormat.Rgba16f => (PixelFormat.Rgba, PixelType.HalfFloat),
            _ => (PixelFormat.Rgba, PixelType.UnsignedByte),
        };
        GL.TexImage2D(TextureTarget.Texture2D, 0, format, w, h, 0, pixelFormat, type, IntPtr.Zero);
        // The colour target is stretched bilinearly; the ray map is read with texelFetch (its filter is never used).
        var filter = format == PixelInternalFormat.Rgba32f ? (int)TextureMinFilter.Nearest : (int)TextureMinFilter.Linear;
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, filter);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, filter);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        t.Fbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, t.Fbo);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, t.Tex, 0);
        bool complete = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == FramebufferErrorCode.FramebufferComplete;
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, previousFbo);
        if (!complete)
        {
            Release(ref t);
            GL.GetError();   // a probe's failure is an answer, not a frame error
            return false;
        }
        (t.Width, t.Height) = (w, h);
        return true;
    }

    private static void Release(ref Target t)
    {
        if (t.Fbo != 0) GL.DeleteFramebuffer(t.Fbo);
        if (t.Tex != 0) GL.DeleteTexture(t.Tex);
        t = default;
    }

    public void Dispose()
    {
        Release(ref _color);
        Release(ref _shown);
        Release(ref _back);
        GL.DeleteVertexArray(_vao);
        _trace.Dispose();
        _shade.Dispose();
        _upsample.Dispose();
    }
}

/// <summary>
/// The lensing pass as the Close-up's controller uses it (Task 4b fix round 1: a seam, so the per-frame decisions of ruling R-P3-10
/// can be tested without OpenGL). <see cref="LensingPass"/> is the real one.
/// </summary>
public interface ILensingPass : IDisposable
{
    /// <summary>Whether the ray map can be cached; false: <see cref="RenderDirect"/> every frame.</summary>
    bool CachesRays { get; }

    /// <summary>Does this frame's tracing, colours the shown ray map and upsamples it; false when a target could not be made.</summary>
    bool Render(System.Windows.Size viewportPx, RayMapStep step, in LensingUniforms u, int environmentCubeMap);

    /// <summary>The fallback without a ray cache: traces the colour at this level and upsamples it.</summary>
    void RenderDirect(System.Windows.Size viewportPx, LensingLevel level, in LensingUniforms u, int environmentCubeMap);
}
