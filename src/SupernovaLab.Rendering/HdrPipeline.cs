using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>One bloom level's size in pixels.</summary>
public readonly record struct BloomLevel(int Width, int Height);

/// <summary>
/// The HDR, bloom and tone-map path (Project 2, plan ruling P9). <see cref="Begin"/> redirects drawing into an RGBA16F target;
/// <see cref="End"/> builds a 5-level dual-filter (Kawase-style) bloom, down then up, then tone-maps (Narkowicz's ACES fit)
/// and applies gamma 2.2 into whatever framebuffer was bound at Begin (the GL control's own, not necessarily 0). Without an
/// RGBA16F colour attachment the pipeline is disabled: <see cref="Begin"/> and <see cref="End"/> do nothing and the scene is
/// drawn straight to the screen, with no bloom. A zero-size viewport also draws nothing through the pipeline. Create and use
/// with the GL context current.
/// </summary>
public sealed class HdrPipeline : IDisposable
{
    public const int MaxBloomLevels = 5;
    public const int MinBloomLevelPx = 8;

    /// <summary>The HDR target's clear colour: the view's dark blue background (sRGB 0.01, 0.01, 0.03) in linear light.</summary>
    private static readonly (float R, float G, float B) Background = (0.00077f, 0.00077f, 0.00232f);

    private readonly ShaderProgram? _down, _up, _tonemap;
    private readonly int _vao;
    private int _sceneFbo, _sceneTex, _width, _height;
    private int[] _levelFbo = Array.Empty<int>(), _levelTex = Array.Empty<int>();
    private BloomLevel[] _levels = Array.Empty<BloomLevel>();
    private int _previousFbo;
    private readonly int[] _previousViewport = new int[4];
    private bool _active;

    /// <summary>Probes RGBA16F with a real 4x4 framebuffer, then (if it works) builds the programs.</summary>
    public HdrPipeline() : this(TryCreateRgba16fTarget) { }

    public HdrPipeline(Func<bool> tryCreateRgba16f)
    {
        Enabled = IsSupported(tryCreateRgba16f);
        if (!Enabled) return;
        _down = ShaderProgram.FromResources("fullscreen.vert", "bloom_down.frag");
        _up = ShaderProgram.FromResources("fullscreen.vert", "bloom_up.frag");
        _tonemap = ShaderProgram.FromResources("fullscreen.vert", "tonemap.frag");
        _vao = GL.GenVertexArray();
    }

    /// <summary>True when drawing goes through the HDR target (RGBA16F works); false is the plain no-bloom path.</summary>
    public bool Enabled { get; }

    /// <summary>Whether RGBA16F can be used: false when the probe says no or throws.</summary>
    public static bool IsSupported(Func<bool> tryCreateRgba16f)
    {
        try { return tryCreateRgba16f(); }
        catch { return false; }
    }

    /// <summary>Narkowicz's ACES fit, (x(2.51x + 0.03)) / (x(2.43x + 0.59) + 0.14), clamped to [0, 1]: the C# mirror of tonemap.frag.</summary>
    public static float Tonemap(float x)
    {
        if (!(x > 0)) return 0f;
        float y = x * (2.51f * x + 0.03f) / (x * (2.43f * x + 0.59f) + 0.14f);
        return Math.Clamp(float.IsNaN(y) ? 1f : y, 0f, 1f);
    }

    /// <summary>
    /// The inverse of <see cref="Tonemap"/> for a value in [0, 0.99]: the x that the fit maps to y (the positive root of
    /// (2.43y − 2.51)x² + (0.59y − 0.03)x + 0.14y = 0). The cutaway uses it to draw element colours that tone-map back to
    /// their hex.
    /// </summary>
    public static float InverseTonemap(float y)
    {
        if (!(y > 0)) return 0f;
        y = Math.Min(y, 0.99f);
        double a = 2.43 * y - 2.51, b = 0.59 * y - 0.03, c = 0.14 * y;
        return (float)((-b - Math.Sqrt(b * b - 4 * a * c)) / (2 * a));
    }

    /// <summary>
    /// The bloom levels for a viewport: each half the size of the one before (the first is half the viewport), at most
    /// <see cref="MaxBloomLevels"/>, stopping before any would be smaller than <see cref="MinBloomLevelPx"/> in either direction.
    /// </summary>
    public static BloomLevel[] BloomLevels(int width, int height)
    {
        var levels = new List<BloomLevel>();
        int w = width, h = height;
        for (int i = 0; i < MaxBloomLevels; i++)
        {
            w /= 2;
            h /= 2;
            if (w < MinBloomLevelPx || h < MinBloomLevelPx) break;
            levels.Add(new BloomLevel(w, h));
        }
        return levels.ToArray();
    }

    /// <summary>Starts a frame: drawing now goes into the HDR target (cleared to the background). Does nothing when disabled or the viewport is empty.</summary>
    public void Begin(Size viewportPx)
    {
        _active = false;
        int w = (int)viewportPx.Width, h = (int)viewportPx.Height;
        if (!Enabled || w <= 0 || h <= 0) return;
        GL.GetInteger(GetPName.FramebufferBinding, out _previousFbo);
        GL.GetInteger(GetPName.Viewport, _previousViewport);
        if (w != _width || h != _height)
        {
            ReleaseTargets();
            bool ok = CreateTargets(w, h);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _previousFbo);
            if (!ok) return;
        }
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _sceneFbo);
        GL.Viewport(0, 0, w, h);
        GL.ClearColor(Background.R, Background.G, Background.B, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        _active = true;
    }

    /// <summary>
    /// Ends the frame: bloom (if <paramref name="bloomStrength"/> is above 0 and the viewport is big enough), tone map and gamma into the original framebuffer.
    /// <paramref name="afterBloom"/>, when given, draws more into the HDR target once the bloom has been built from it, so what
    /// it draws is tone-mapped but adds nothing to the glow (the cutaway's element-coloured cut faces use this, keeping their hex
    /// colours instead of being bloomed towards white). It runs with the target bound and the viewport set, and also runs, drawing
    /// straight to the screen, when the pipeline is disabled.
    /// </summary>
    public void End(float exposure, float bloomStrength, Action? afterBloom = null)
    {
        if (!_active)
        {
            afterBloom?.Invoke();
            return;
        }
        _active = false;
        GL.Disable(EnableCap.DepthTest);
        GL.BindVertexArray(_vao);
        bool bloom = bloomStrength > 0 && _levels.Length > 0;
        if (bloom) BuildBloom();
        if (afterBloom is not null)
        {
            GL.BindVertexArray(0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _sceneFbo);
            GL.Viewport(0, 0, _width, _height);
            afterBloom();
            GL.Disable(EnableCap.Blend);
            GL.BindVertexArray(_vao);
        }

        GL.Disable(EnableCap.Blend);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _previousFbo);
        GL.Viewport(_previousViewport[0], _previousViewport[1], _previousViewport[2], _previousViewport[3]);
        _tonemap!.Use();
        Bind(0, _sceneTex);
        _tonemap.Set("u_scene", 0);
        Bind(1, bloom ? _levelTex[0] : _sceneTex);
        _tonemap.Set("u_bloom", 1);
        _tonemap.Set("u_exposure", exposure);
        _tonemap.Set("u_bloomStrength", bloom ? bloomStrength : 0f);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GL.BindVertexArray(0);
        GL.ActiveTexture(TextureUnit.Texture0);
    }

    private void BuildBloom()
    {
        GL.Disable(EnableCap.Blend);
        _down!.Use();
        _down.Set("u_src", 0);
        int src = _sceneTex;
        for (int i = 0; i < _levels.Length; i++)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _levelFbo[i]);
            GL.Viewport(0, 0, _levels[i].Width, _levels[i].Height);
            Bind(0, src);
            _down.Set("u_halfPixel", new Vector2(0.5f / _levels[i].Width, 0.5f / _levels[i].Height));
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            src = _levelTex[i];
        }
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
        _up!.Use();
        _up.Set("u_src", 0);
        for (int i = _levels.Length - 1; i > 0; i--)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _levelFbo[i - 1]);
            GL.Viewport(0, 0, _levels[i - 1].Width, _levels[i - 1].Height);
            Bind(0, _levelTex[i]);
            _up.Set("u_halfPixel", new Vector2(0.5f / _levels[i].Width, 0.5f / _levels[i].Height));
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }
        GL.Disable(EnableCap.Blend);
    }

    private static void Bind(int unit, int texture)
    {
        GL.ActiveTexture(TextureUnit.Texture0 + unit);
        GL.BindTexture(TextureTarget.Texture2D, texture);
    }

    private bool CreateTargets(int w, int h)
    {
        _levels = BloomLevels(w, h);
        bool ok;
        (_sceneFbo, _sceneTex, ok) = MakeTarget(w, h);
        _levelFbo = new int[_levels.Length];
        _levelTex = new int[_levels.Length];
        for (int i = 0; i < _levels.Length; i++)
        {
            bool levelOk;
            (_levelFbo[i], _levelTex[i], levelOk) = MakeTarget(_levels[i].Width, _levels[i].Height);
            ok &= levelOk;
        }
        if (!ok) { ReleaseTargets(); return false; }
        _width = w;
        _height = h;
        return true;
    }

    private static (int Fbo, int Tex, bool Complete) MakeTarget(int w, int h)
    {
        int tex = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, tex);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, w, h, 0, PixelFormat.Rgba, PixelType.HalfFloat, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        int fbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, tex, 0);
        bool complete = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == FramebufferErrorCode.FramebufferComplete;
        return (fbo, tex, complete);
    }

    private void ReleaseTargets()
    {
        if (_sceneFbo != 0) GL.DeleteFramebuffer(_sceneFbo);
        if (_sceneTex != 0) GL.DeleteTexture(_sceneTex);
        foreach (int f in _levelFbo) GL.DeleteFramebuffer(f);
        foreach (int t in _levelTex) GL.DeleteTexture(t);
        _sceneFbo = _sceneTex = _width = _height = 0;
        _levelFbo = Array.Empty<int>();
        _levelTex = Array.Empty<int>();
        _levels = Array.Empty<BloomLevel>();
    }

    /// <summary>Creates a 4x4 RGBA16F framebuffer and reports whether it is complete (the real GL probe for <see cref="IsSupported"/>).</summary>
    private static bool TryCreateRgba16fTarget()
    {
        GL.GetInteger(GetPName.FramebufferBinding, out int previous);
        int tex = GL.GenTexture(), fbo = GL.GenFramebuffer();
        try
        {
            GL.GetError();   // clear anything stale
            GL.BindTexture(TextureTarget.Texture2D, tex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, 4, 4, 0, PixelFormat.Rgba, PixelType.HalfFloat, IntPtr.Zero);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, tex, 0);
            return GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == FramebufferErrorCode.FramebufferComplete
                   && GL.GetError() == OpenTK.Graphics.OpenGL4.ErrorCode.NoError;
        }
        finally
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, previous);
            GL.DeleteFramebuffer(fbo);
            GL.DeleteTexture(tex);
        }
    }

    public void Dispose()
    {
        if (!Enabled) return;
        ReleaseTargets();
        GL.DeleteVertexArray(_vao);
        _down?.Dispose();
        _up?.Dispose();
        _tonemap?.Dispose();
    }
}
