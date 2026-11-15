using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// What <c>lensing.frag</c> needs for one frame (all lengths in GM/c², in the tracer's chart, ruling R-P3-8: the scene's axes,
/// spin along +z): the spin, the camera's position and basis (forward, right, up: unit vectors, as <see cref="OrbitCamera"/>
/// gives them), tan of half the field of view across and up, the escape radius
/// (<see cref="Core.Compact.BlackHoleOptics.EscapeRadiusFor"/>), a neutron star's surface radius (0 for a black hole) and its
/// colour, and the grid switch (plan ruling P3-8).
/// </summary>
public readonly record struct LensingUniforms(float Spin, Vector3 CameraPosition, Vector3 Forward, Vector3 Right, Vector3 Up,
    Vector2 TanHalf, float EscapeRadius, float SurfaceRadius = 0, Vector3 SurfaceColor = default, bool Grid = false);

/// <summary>
/// The Close-up's light bending (spec §6.6, plan rulings P3-7 and P3-9). <c>lensing.frag</c>, the GLSL mirror of
/// <see cref="Core.Compact.BlackHoleOptics.TraceRay"/>, traces one ray per pixel of an offscreen target at the requested
/// <see cref="LensingLevel"/> (RGBA16F with the HDR path, RGBA8 without), sampling the environment cube map
/// (<see cref="EnvironmentCapture"/>) where rays escape; <c>upsample.frag</c> then stretches it bilinearly over whatever framebuffer
/// and viewport were current when <see cref="Render"/> was called (the HDR target, or the GL control's own). OpenGL 4.1 has no
/// compute shaders, so this is a fragment-shader pass. Create and use with the GL context current.
/// </summary>
public sealed class LensingPass : IDisposable
{
    private readonly ShaderProgram _trace = ShaderProgram.FromResources("fullscreen.vert", "lensing.frag");
    private readonly ShaderProgram _upsample = ShaderProgram.FromResources("fullscreen.vert", "upsample.frag");
    private readonly int _vao = GL.GenVertexArray();
    private readonly bool _hdr;
    private int _fbo, _tex, _width, _height;
    private readonly int[] _viewport = new int[4];

    /// <param name="hdr">True for an RGBA16F target (the HDR path), false for RGBA8 (the no-HDR path).</param>
    public LensingPass(bool hdr) => _hdr = hdr;

    /// <summary>The traced image's size at the last <see cref="Render"/> (pixels).</summary>
    public (int Width, int Height) TargetSize => (_width, _height);

    /// <summary>Traces the view at this level into the offscreen target and upsamples it into the current framebuffer.</summary>
    public void Render(Size viewportPx, LensingLevel level, in LensingUniforms u, int environmentCubeMap)
    {
        int vw = (int)viewportPx.Width, vh = (int)viewportPx.Height;
        if (vw <= 0 || vh <= 0) return;
        var (w, h) = LensingLevels.TargetSize(vw, vh, level);
        GL.GetInteger(GetPName.FramebufferBinding, out int previousFbo);
        GL.GetInteger(GetPName.Viewport, _viewport);
        if ((w != _width || h != _height) && !CreateTarget(w, h))
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, previousFbo);
            return;
        }

        GL.Disable(EnableCap.Blend);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        GL.Viewport(0, 0, w, h);
        _trace.Use();
        _trace.Set("u_spin", u.Spin);
        _trace.Set("u_camPos", u.CameraPosition);
        _trace.Set("u_camForward", u.Forward);
        _trace.Set("u_camRight", u.Right);
        _trace.Set("u_camUp", u.Up);
        _trace.Set("u_tanHalf", u.TanHalf);
        _trace.Set("u_escapeRadius", u.EscapeRadius);
        _trace.Set("u_surfaceRadius", u.SurfaceRadius);
        _trace.Set("u_surfaceColor", u.SurfaceColor);
        _trace.Set("u_grid", u.Grid ? 1 : 0);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.TextureCubeMap, environmentCubeMap);
        _trace.Set("u_env", 0);
        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, previousFbo);
        GL.Viewport(_viewport[0], _viewport[1], _viewport[2], _viewport[3]);
        _upsample.Use();
        GL.BindTexture(TextureTarget.Texture2D, _tex);
        _upsample.Set("u_src", 0);
        GL.ColorMask(true, true, true, false);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GL.ColorMask(true, true, true, true);
        GL.BindVertexArray(0);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        GL.BindTexture(TextureTarget.TextureCubeMap, 0);
    }

    private bool CreateTarget(int w, int h)
    {
        ReleaseTarget();
        _tex = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _tex);
        if (_hdr)
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, w, h, 0, PixelFormat.Rgba, PixelType.HalfFloat, IntPtr.Zero);
        else
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, w, h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        _fbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _tex, 0);
        if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
        {
            ReleaseTarget();
            return false;
        }
        (_width, _height) = (w, h);
        return true;
    }

    private void ReleaseTarget()
    {
        if (_fbo != 0) GL.DeleteFramebuffer(_fbo);
        if (_tex != 0) GL.DeleteTexture(_tex);
        _fbo = _tex = _width = _height = 0;
    }

    public void Dispose()
    {
        ReleaseTarget();
        GL.DeleteVertexArray(_vao);
        _trace.Dispose();
        _upsample.Dispose();
    }
}
