using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SupernovaLab.Core.Compact;

namespace SupernovaLab.Rendering;

/// <summary>
/// Plan Task 9 (spec §3.2): the core's first second in the Close-up, drawn as plain surfaces without the bending of light (a note says
/// so, as ruling R-P3-19 does for the overlays):
/// <list type="bullet">
/// <item>the proto-neutron star (or, before collapse, the iron or O-Ne-Mg core) as an opaque sphere, its brightness the neutrino
/// panel's total luminosity on Project 2's rule (<see cref="CloseUpState.ProtoNsBrightness"/>);</item>
/// <item>the shock as a translucent surface at the research's mean radius, with a schematic low-order distortion of the research's
/// approximate size (<see cref="CoreState.DistortionFraction"/>), swinging once in <see cref="CoreAnimation.SloshPeriodS"/>;</item>
/// <item>the gain radius as a faint shell;</item>
/// <item>infalling particles at the research's speed, slowed down (<see cref="CoreAnimation"/>), on schematic straight-in paths.</item>
/// </list>
/// The surfaces are ray-cast per pixel (<c>coreshock.frag</c>, on <c>shell.vert</c>'s full-screen pair), the particles are points
/// (<c>coreinfall.vert</c>), all into the HDR target before the bloom. Create and use with the GL context current.
/// </summary>
public sealed class CoreCollapseRenderer : IDisposable
{
    /// <summary>[Presentation] How many infalling particles are drawn.</summary>
    public const int InfallParticles = 1500;

    /// <summary>[Presentation] The infalling particles' size, DIP.</summary>
    public const double ParticleDip = 2.2;

    /// <summary>[Schematic] The colours: the shock (pale gold), the gain shell (pale green) and the infalling matter (grey-blue).</summary>
    public static readonly Vector3 ShockColor = new(1.0f, 0.78f, 0.45f), GainColor = new(0.45f, 0.95f, 0.65f),
        InfallColor = new(0.55f, 0.68f, 0.95f);

    /// <summary>[Presentation] The translucent layers' opacity face-on (edge-on they reach about 3 times this).</summary>
    public const float ShockAlpha = 0.28f, GainAlpha = 0.12f, InfallAlpha = 0.55f;

    /// <summary>
    /// [Presentation] The translucent layers' glow (their colour's level against the sphere's). Retuned after Task 9's launch, where at
    /// 1 the shock read as an opaque cream disc that hid the gain shell and dimmed the proto-neutron star (not yet seen in a launch).
    /// </summary>
    public const float ShockGlow = 0.15f, GainGlow = 0.25f;

    /// <summary>[Presentation] The core before collapse is drawn at this brightness (it has no neutrino light yet).</summary>
    public const float PreCollapseBrightness = 0.6f;

    private readonly ShaderProgram _surfaces = ShaderProgram.FromResources("shell.vert", "coreshock.frag");
    private readonly ShaderProgram _infall = ShaderProgram.FromResources("coreinfall.vert", "coreinfall.frag");
    private readonly int _quadVao, _quadVbo, _pointVao, _pointVbo;

    public CoreCollapseRenderer()
    {
        float[] corners = { -1, -1, 1, -1, 1, 1, -1, -1, 1, 1, -1, 1 };
        _quadVao = GL.GenVertexArray();
        _quadVbo = GL.GenBuffer();
        GL.BindVertexArray(_quadVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _quadVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, corners.Length * sizeof(float), corners, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);

        float[] seeds = InfallSeeds(InfallParticles);
        _pointVao = GL.GenVertexArray();
        _pointVbo = GL.GenBuffer();
        GL.BindVertexArray(_pointVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _pointVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, seeds.Length * sizeof(float), seeds, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, 4 * sizeof(float), 3 * sizeof(float));
        GL.BindVertexArray(0);
    }

    /// <summary>The particles' fixed directions (uniform on the sphere) and seeds, 4 floats each; the same every run (display randomness).</summary>
    public static float[] InfallSeeds(int count)
    {
        var rng = new Random(1987);
        var data = new float[4 * count];
        for (int i = 0; i < count; i++)
        {
            double z = 2 * rng.NextDouble() - 1, phi = 2 * Math.PI * rng.NextDouble(), s = Math.Sqrt(1 - z * z);
            data[4 * i] = (float)(s * Math.Cos(phi));
            data[4 * i + 1] = (float)(s * Math.Sin(phi));
            data[4 * i + 2] = (float)z;
            data[4 * i + 3] = (float)rng.NextDouble();
        }
        return data;
    }

    /// <summary>
    /// The sphere's HDR colour: its schematic colour, linearised, times its brightness and the colour gain (a level ÷ the exposure).
    /// </summary>
    public static Vector3 SphereColor(CloseUpState s, float gain)
    {
        static float Linear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        float brightness = s.Era == CloseUpEra.PreCollapse ? PreCollapseBrightness
            : float.IsFinite(s.ProtoNsBrightness) ? s.ProtoNsBrightness : PreCollapseBrightness;
        var c = s.CoreColor;
        return new Vector3(Linear(c.R), Linear(c.G), Linear(c.B)) * (brightness * gain);
    }

    /// <summary>
    /// Draws the core's stage (<see cref="CloseUpState.IsCoreStage"/>): the infalling particles first (the surfaces in front of them
    /// cover them), then the surfaces. <paramref name="sloshPhaseRad"/> swings the shock's shape; <paramref name="infallCycles"/> is the
    /// particles' accumulated fall phase (cycles, wrapped to [0, 1)); <paramref name="gain"/> scales every colour into the HDR target.
    /// </summary>
    public void Render(OrbitCamera cam, Size viewportPx, double pxPerDip, CloseUpState s, double sloshPhaseRad, double infallCycles, float gain)
    {
        if (s.Core is not { } core || !(viewportPx.Height > 0) || !(viewportPx.Width > 0)) return;
        bool shock = double.IsFinite(core.ShockRadiusKm) && core.ShockRadiusKm > 0;
        GL.Enable(EnableCap.Blend);

        if (shock && double.IsFinite(core.InfallSpeedKmS) && s.Era == CloseUpEra.FirstSecond)
        {
            _infall.Use();
            _infall.SetCamera(cam, viewportPx);
            _infall.Set("u_rIn", (float)core.ShockRadiusKm);
            _infall.Set("u_rOut", (float)(CoreAnimation.InfallOuterFactor * core.ShockRadiusKm));
            _infall.Set("u_cycles", (float)(infallCycles - Math.Floor(infallCycles)));
            _infall.Set("u_pointPx", (float)(ParticleDip * pxPerDip));
            _infall.Set("u_color", InfallColor * gain);
            _infall.Set("u_alpha", InfallAlpha);
            GL.Enable(EnableCap.ProgramPointSize);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            GL.BindVertexArray(_pointVao);
            GL.DrawArrays(PrimitiveType.Points, 0, InfallParticles);
        }

        var (right, up) = cam.ScreenAxes();
        double tanY = Math.Tan(cam.FieldOfViewDeg * Math.PI / 360);
        double aspect = viewportPx.Width / viewportPx.Height;
        _surfaces.Use();
        _surfaces.Set("u_forward", (Vector3)(-cam.Backward));
        _surfaces.Set("u_right", (Vector3)right);
        _surfaces.Set("u_up", (Vector3)up);
        _surfaces.Set("u_tanHalf", new Vector2((float)(tanY * aspect), (float)tanY));
        _surfaces.Set("u_eye", (Vector3)cam.Eye);
        _surfaces.Set("u_sphereR", Finite(s.CoreSphereRadiusKm));
        _surfaces.Set("u_sphereColor", SphereColor(s, gain));
        _surfaces.Set("u_gainR", Finite(core.GainRadiusKm));
        _surfaces.Set("u_gainColor", GainColor * (gain * GainGlow));
        _surfaces.Set("u_gainAlpha", GainAlpha);
        _surfaces.Set("u_shockR", shock ? (float)core.ShockRadiusKm : 0f);
        _surfaces.Set("u_shockColor", ShockColor * (gain * ShockGlow));
        _surfaces.Set("u_shockAlpha", ShockAlpha);
        _surfaces.Set("u_amp", (float)Math.Clamp(core.DistortionFraction, 0, 0.5));
        _surfaces.Set("u_phase", (float)(sloshPhaseRad % (2 * Math.PI * 4)));   // the spiral turns at half the sloshing's rate
        GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        GL.BindVertexArray(_quadVao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        GL.BindVertexArray(0);
    }

    private static float Finite(double v) => double.IsFinite(v) && v > 0 ? (float)v : 0f;

    public void Dispose()
    {
        GL.DeleteVertexArray(_quadVao);
        GL.DeleteBuffer(_quadVbo);
        GL.DeleteVertexArray(_pointVao);
        GL.DeleteBuffer(_pointVbo);
        _surfaces.Dispose();
        _infall.Dispose();
    }
}
