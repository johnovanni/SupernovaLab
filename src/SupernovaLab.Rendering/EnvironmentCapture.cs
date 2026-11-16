using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// The surroundings the Close-up's black hole bends (spec §6.2, §6.6; plan ruling P3-6): Project 2's scene rendered from the
/// remnant's position into a <see cref="FaceSize"/>² cube map, RGBA16F with the HDR path and RGBA8 without, in the Close-up's own GL
/// context. The scene itself is drawn by the caller's delegate (the main view's renderers, at Low quality) once per face, with a
/// 90° camera turned to the face's GL cube-map orientation (<see cref="Faces"/>), so <c>texture(samplerCube, d)</c> in
/// <c>lensing.frag</c> returns what lies in scene direction d. Refreshing is the caller's decision, by <see cref="NeedsRefresh"/>.
/// Create and use with the GL context current.
/// </summary>
public sealed class EnvironmentCapture : IDisposable
{
    /// <summary>Each face's size in pixels (ruling P3-6).</summary>
    public const int FaceSize = 256;

    /// <summary>A refresh is due once log₁₀ of the source time has moved by more than this (ruling P3-6).</summary>
    public const double LogTimeStep = 0.005;

    /// <summary>At most this many refreshes per wall-clock second (ruling P3-6).</summary>
    public const double MaxRefreshesPerSecond = 4;

    /// <summary>
    /// The six faces in GL order (+x, −x, +y, −y, +z, −z): the direction each camera looks and its screen-up. These are the cube-map
    /// conventions of the GL specification (for face +x, s runs along −z and t along −y; and so on), so a camera with
    /// right = forward × up draws each face the way the sampler reads it.
    /// </summary>
    public static readonly (Vector3d Forward, Vector3d Up)[] Faces =
    {
        (new Vector3d(1, 0, 0), new Vector3d(0, -1, 0)),
        (new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0)),
        (new Vector3d(0, 1, 0), new Vector3d(0, 0, 1)),
        (new Vector3d(0, -1, 0), new Vector3d(0, 0, -1)),
        (new Vector3d(0, 0, 1), new Vector3d(0, -1, 0)),
        (new Vector3d(0, 0, -1), new Vector3d(0, -1, 0)),
    };

    /// <summary>
    /// Whether the environment shown is out of date: the first capture, another outcome (or observer, or run: the caller's key), or
    /// log₁₀ source time moved by more than <see cref="LogTimeStep"/>.
    /// </summary>
    public static bool HasChanged(double prevLogT, double logT, bool sameOutcome)
        => !sameOutcome || !double.IsFinite(prevLogT) || !(Math.Abs(logT - prevLogT) <= LogTimeStep);

    /// <summary>
    /// Ruling P3-6 as a pure function: refresh when <see cref="HasChanged"/> and at least 1 / <see cref="MaxRefreshesPerSecond"/> s of
    /// wall time has passed since the last refresh.
    /// </summary>
    public static bool NeedsRefresh(double prevLogT, double logT, bool sameOutcome, double wallSinceLast)
        => HasChanged(prevLogT, logT, sameOutcome) && wallSinceLast >= 1 / MaxRefreshesPerSecond;

    private readonly int _tex, _fbo;
    private readonly OrbitCamera _camera = new() { FieldOfViewDeg = 90, DistanceMpc = OrbitCamera.MinDistanceMpc };
    private readonly int[] _viewport = new int[4];

    /// <param name="hdr">True for RGBA16F faces (the HDR path), false for RGBA8.</param>
    public EnvironmentCapture(bool hdr)
    {
        Hdr = hdr;
        _tex = GL.GenTexture();
        GL.BindTexture(TextureTarget.TextureCubeMap, _tex);
        for (int i = 0; i < 6; i++)
        {
            var target = TextureTarget.TextureCubeMapPositiveX + i;
            if (hdr) GL.TexImage2D(target, 0, PixelInternalFormat.Rgba16f, FaceSize, FaceSize, 0, PixelFormat.Rgba, PixelType.HalfFloat, IntPtr.Zero);
            else GL.TexImage2D(target, 0, PixelInternalFormat.Rgba8, FaceSize, FaceSize, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        }
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
        GL.BindTexture(TextureTarget.TextureCubeMap, 0);
        GL.Enable(EnableCap.TextureCubeMapSeamless);

        GL.GetInteger(GetPName.FramebufferBinding, out int previous);
        _fbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.TextureCubeMapPositiveX, _tex, 0);
        Available = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == FramebufferErrorCode.FramebufferComplete;
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, previous);
        Clear(Hdr ? HdrPipeline.Background : NoHdrBackground);
    }

    /// <summary>The no-HDR background, sRGB (0.01, 0.01, 0.03), as the GL host clears the main view.</summary>
    public static readonly (float R, float G, float B) NoHdrBackground = (0.01f, 0.01f, 0.03f);

    /// <summary>The cube map's GL texture name.</summary>
    public int Texture => _tex;

    /// <summary>True for RGBA16F faces.</summary>
    public bool Hdr { get; }

    /// <summary>False when the faces cannot be rendered to (an incomplete framebuffer): the map then stays at its background.</summary>
    public bool Available { get; }

    /// <summary>How many times the map has been rendered.</summary>
    public int Captures { get; private set; }

    /// <summary>
    /// Renders the six faces from <paramref name="position"/> (the drawing delegate's scene units): each is cleared to the background,
    /// then <paramref name="drawScene"/> draws the scene with that face's camera into a <see cref="FaceSize"/>² viewport. The framebuffer,
    /// viewport and blending are restored afterwards.
    /// </summary>
    public void Capture(Vector3d position, Action<OrbitCamera, Size> drawScene)
    {
        if (!Available) return;
        GL.GetInteger(GetPName.FramebufferBinding, out int previous);
        GL.GetInteger(GetPName.Viewport, _viewport);
        var size = new Size(FaceSize, FaceSize);
        var bg = Hdr ? HdrPipeline.Background : NoHdrBackground;
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        GL.Viewport(0, 0, FaceSize, FaceSize);
        _camera.Target = position;
        for (int i = 0; i < 6; i++)
        {
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.TextureCubeMapPositiveX + i, _tex, 0);
            GL.ColorMask(true, true, true, true);
            GL.ClearColor(bg.R, bg.G, bg.B, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            var (forward, up) = Faces[i];
            _camera.Orient(-forward, up);
            drawScene(_camera, size);
        }
        GL.Disable(EnableCap.Blend);
        GL.ColorMask(true, true, true, true);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, previous);
        GL.Viewport(_viewport[0], _viewport[1], _viewport[2], _viewport[3]);
        Captures++;
    }

    /// <summary>
    /// Ruling R-P3-21: fills all six faces with one colour (the haze inside the star before the shock breaks out), drawing nothing.
    /// Counted in <see cref="Fills"/>, not <see cref="Captures"/>.
    /// </summary>
    public void Fill((float R, float G, float B) color)
    {
        Clear(color);
        Fills++;
    }

    /// <summary>How many times the map has been filled with one colour (<see cref="Fill"/>).</summary>
    public int Fills { get; private set; }

    private void Clear((float R, float G, float B) bg)
    {
        if (!Available) return;
        GL.GetInteger(GetPName.FramebufferBinding, out int previous);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        for (int i = 0; i < 6; i++)
        {
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.TextureCubeMapPositiveX + i, _tex, 0);
            GL.ClearColor(bg.R, bg.G, bg.B, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit);
        }
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, previous);
    }

    public void Dispose()
    {
        GL.DeleteFramebuffer(_fbo);
        GL.DeleteTexture(_tex);
    }
}
