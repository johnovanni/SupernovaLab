using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// How the breakout flash is put on screen (Task 11; spec §5). The flash has no new physics: its strength is
/// <see cref="SceneState.FlashIntensity"/> (from the breakout model) and its colour <see cref="SceneState.FlashColor"/>.
/// <list type="bullet">
/// <item>Through the HDR target it is scene light (<see cref="HdrCore"/> at the centre, <see cref="HdrVeil"/> everywhere) added before
/// the bloom, so it glows and may blow out the view at its strongest, as intended.</item>
/// <item>Without the HDR target it is a full-screen additive quad at <see cref="NoHdrAlpha"/>, min(1, intensity / 8).</item>
/// </list>
/// </summary>
public static class FlashLayer
{
    /// <summary>
    /// [APPROX, schematic] HDR light per unit of flash intensity at the view's centre, and everywhere (the veil). Tuned on the default preset's
    /// breakout frames (flash intensity 3.5 at 110,990 s, 1.5 at 160,000 s, just after at 221,300 s) and SN 1998bw's breakout (110.2 s): 5 / 0.5 and
    /// 2.5 / 0.25 whited out the whole view even at 1.5.
    /// </summary>
    public const float CorePerIntensity = 1.2f, VeilPerIntensity = 0.12f;

    /// <summary>The additive quad's alpha without the HDR target: min(1, intensity / 8) (0 for no flash).</summary>
    public static float NoHdrAlpha(float intensity)
        => intensity > 0 && float.IsFinite(intensity) ? Math.Min(1f, intensity / ExplosionScene.MaxFlashIntensity) : 0f;

    /// <summary>The HDR light at the centre for this flash intensity.</summary>
    public static float HdrCore(float intensity) => intensity > 0 && float.IsFinite(intensity) ? CorePerIntensity * intensity : 0f;

    /// <summary>The HDR light over the whole view for this flash intensity.</summary>
    public static float HdrVeil(float intensity) => intensity > 0 && float.IsFinite(intensity) ? VeilPerIntensity * intensity : 0f;
}

/// <summary>Draws the flash (<see cref="FlashLayer"/>) as an additive full-screen triangle. Create and use with the GL context current.</summary>
public sealed class FlashRenderer : IDisposable
{
    private readonly ShaderProgram _program = ShaderProgram.FromResources("fullscreen.vert", "flash.frag");
    private readonly int _vao = GL.GenVertexArray();

    /// <summary>Adds the flash as scene light into the HDR target currently bound.</summary>
    public void RenderHdr(Size viewportPx, (float R, float G, float B) color, float intensity)
        => Draw(viewportPx, new Vector3(color.R, color.G, color.B), FlashLayer.HdrCore(intensity), FlashLayer.HdrVeil(intensity));

    /// <summary>Adds the flash as a full-screen quad at this alpha straight to the screen (no HDR target): a veil of the flash colour, brightest at the centre.</summary>
    public void RenderNoHdr(Size viewportPx, (float R, float G, float B) color, float alpha)
        => Draw(viewportPx, new Vector3(color.R, color.G, color.B), 0f, alpha);

    private void Draw(Size viewportPx, Vector3 color, float core, float veil)
    {
        if (!(core > 0 || veil > 0) || !(viewportPx.Height > 0)) return;
        _program.Use();
        _program.Set("u_color", color);
        _program.Set("u_core", core);
        _program.Set("u_veil", veil);
        _program.Set("u_aspect", (float)(viewportPx.Width / viewportPx.Height));
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
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
