using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SupernovaLab.Core.Compact;

namespace SupernovaLab.Rendering;

/// <summary>
/// Plan Task 7 (spec §4.2): draws the neutron star's cutaway, <c>nscut.frag</c>, as a full-screen pass over the lensed scene (call it
/// in <see cref="HdrPipeline.End"/>'s after-bloom hook, before the lines). The star is a sphere of its radius with the quarter
/// x &gt; 0, z &gt; 0 removed (the scene's +x, +z quarter), ray-cast analytically; the cut faces show the layers at their true radii in
/// schematic colours, with the neutron drip line. It is not bent by the star's gravity (ruling R-P3-19): the controller hides the
/// lensed image of the surface while it is on. Create and use with the GL context current.
/// </summary>
public sealed class NeutronStarCutawayRenderer : IDisposable
{
    private readonly ShaderProgram _program = ShaderProgram.FromResources("fullscreen.vert", "nscut.frag");
    private readonly int _vao = GL.GenVertexArray();

    /// <summary>
    /// The layers' outer radii over the star's, centre outward for the five solid layers (inner core, outer core, pasta, inner crust,
    /// outer crust): what the shader's u_bound reads.
    /// </summary>
    public static float[] Bounds(CutawayInfo info)
    {
        var b = new float[5];
        for (int i = 0; i < 5; i++) b[i] = info.Layers.Count > i ? (float)(info.Layers[i].OuterRadiusKm / info.RadiusKm) : 1f;
        return b;
    }

    /// <summary>
    /// Draws the cutaway. <paramref name="toneMapped"/> turns a colour (normalized sRGB) into what the HDR target should hold
    /// (<c>CloseUpSceneController.ToneMappedSurface</c>), so the layers read at their key's colours; <paramref name="surface"/> is the
    /// surface's colour, already tone-mapped.
    /// </summary>
    public void Render(OrbitCamera camera, Size viewportPx, CutawayInfo info, Vector3 surface, Func<(float R, float G, float B), Vector3> toneMapped)
    {
        if (!(info.RadiusKm > 0) || !(viewportPx.Width > 0) || !(viewportPx.Height > 0)) return;
        double r = info.RadiusKm;
        var (right, up) = camera.ScreenAxes();
        var forward = -camera.Backward;
        double tanY = Math.Tan(camera.FieldOfViewDeg * Math.PI / 360);
        double aspect = viewportPx.Width / viewportPx.Height;
        var eye = camera.Eye / r;

        _program.Use();
        _program.Set("u_eye", (Vector3)eye);
        _program.Set("u_forward", (Vector3)forward);
        _program.Set("u_right", (Vector3)right);
        _program.Set("u_up", (Vector3)up);
        _program.Set("u_tanHalf", new Vector2((float)(tanY * aspect), (float)tanY));
        _program.Set("u_viewport", new Vector2((float)viewportPx.Width, (float)viewportPx.Height));
        _program.Set("u_layered", info.HasCrust ? 1 : 0);
        var bounds = Bounds(info);
        GL.Uniform1(_program.Location("u_bound"), 5, bounds);
        var colors = new float[15];
        string[] names = { "Inner core", "Outer core", "Nuclear pasta", "Inner crust", "Outer crust" };
        for (int i = 0; i < 5; i++)
        {
            var c = toneMapped(CutawayPalette.Rgb(CutawayPalette.For(names[i])));
            (colors[3 * i], colors[3 * i + 1], colors[3 * i + 2]) = (c.X, c.Y, c.Z);
        }
        GL.Uniform3(_program.Location("u_color"), 5, colors);
        _program.Set("u_dripColor", toneMapped(CutawayPalette.Rgb(CutawayPalette.DripLine)));
        _program.Set("u_drip", double.IsFinite(info.DripRadiusFraction) ? (float)info.DripRadiusFraction : 1f);
        _program.Set("u_atmoColor", toneMapped(CutawayPalette.Rgb(CutawayPalette.Atmosphere)));
        _program.Set("u_hotColor", toneMapped(CutawayPalette.Rgb(CutawayPalette.Hot)));
        _program.Set("u_surfaceColor", surface);

        GL.Disable(EnableCap.Blend);
        GL.Disable(EnableCap.DepthTest);
        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(_vao);
        _program.Dispose();
    }
}
