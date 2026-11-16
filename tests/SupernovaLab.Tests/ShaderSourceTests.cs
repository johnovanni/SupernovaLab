using System.IO;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>
/// Guards for the embedded GLSL sources, checked without a GL context. The Intel driver rejects a non-ASCII character even in a
/// comment ("pre-mature EOF"; Task 7 lost a launch to a section sign), so every byte must be below 0x80.
/// </summary>
public class ShaderSourceTests
{
    private static IEnumerable<(string Name, byte[] Bytes)> Shaders()
    {
        var asm = typeof(ShaderProgram).Assembly;
        foreach (string name in asm.GetManifestResourceNames().Where(n => n.EndsWith(".vert") || n.EndsWith(".frag")))
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            yield return (name, ms.ToArray());
        }
    }

    [Fact]
    public void EveryShader_IsPureAscii()
    {
        var all = Shaders().ToList();
        Assert.True(all.Count >= 14, $"expected the embedded shaders, found {all.Count}");
        foreach (var (name, bytes) in all)
        {
            int bad = Array.FindIndex(bytes, b => b >= 0x80);
            Assert.True(bad < 0, $"{name}: non-ASCII byte 0x{(bad >= 0 ? bytes[bad] : 0):X2} at offset {bad}");
        }
    }

    [Fact]
    public void EveryShader_StartsWithTheVersionLine_AndHasBalancedBrackets()
    {
        foreach (var (name, bytes) in Shaders())
        {
            string text = System.Text.Encoding.ASCII.GetString(bytes);
            Assert.StartsWith("#version 410 core", text);
            string code = System.Text.RegularExpressions.Regex.Replace(text, "//[^\n]*", "");
            foreach (var (open, close) in new[] { ('(', ')'), ('[', ']'), ('{', '}') })
                Assert.True(code.Count(c => c == open) == code.Count(c => c == close), $"{name}: unbalanced {open}{close}");
        }
    }

    [Theory]
    [InlineData("particle.vert")]
    [InlineData("cutface.vert")]
    [InlineData("star.frag")]
    [InlineData("shell.frag")]
    public void TheWedgeShaders_ShareTheWedgeFrame(string shader)
    {
        // Ruling R-P2-22: the particle drop, the face quads and the star all use the wedge basis (WedgeOrientation's).
        string text = System.Text.Encoding.ASCII.GetString(Shaders().Single(s => s.Name.EndsWith("." + shader)).Bytes);
        Assert.Contains("uniform vec3 u_wedgeX", text);
        Assert.Contains("uniform vec3 u_wedgeZ", text);
    }

    [Theory]
    [InlineData("particle.vert", "uniform float u_reverse", "uniform float u_contact", "uniform float u_shockWeight")]
    [InlineData("cutface.frag", "uniform float u_reverse", "uniform float u_contact", "uniform float u_freeScale")]
    [InlineData("shell.frag", "float piece(", "float segment(", "uniform float u_peak")]
    public void TheShockShaders_DeclareWhatTheirCSharpMirrorsUse(string shader, params string[] needles)
    {
        // Task 8: particle.vert mirrors ParticleKinematics.RadiusCm, cutface.frag ParticleKinematics.FreeRadiusCm, shell.frag
        // ColumnDensity.Segment.
        string text = System.Text.Encoding.ASCII.GetString(Shaders().Single(s => s.Name.EndsWith("." + shader)).Bytes);
        foreach (string needle in needles) Assert.Contains(needle, text);
    }

    /// <summary>
    /// Plan Task 9: coreshock.frag and coreinfall.vert declare every uniform CoreCollapseRenderer sets, and the infall shader carries
    /// CoreAnimation's free-fall profile (r^1.5 linear in the fall's phase) and its one fade in and out per fall (flash safety).
    /// </summary>
    [Fact]
    public void CoreShaders_DeclareTheRenderersUniforms_AndTheFallProfile()
    {
        string Text(string name) => System.Text.Encoding.ASCII.GetString(Shaders().Single(s => s.Name.EndsWith("." + name)).Bytes);
        string shock = Text("coreshock.frag");
        foreach (string needle in new[]
                 {
                     "uniform vec3 u_forward", "uniform vec3 u_right", "uniform vec3 u_up", "uniform vec2 u_tanHalf", "uniform vec3 u_eye",
                     "uniform float u_sphereR", "uniform vec3 u_sphereColor", "uniform float u_gainR", "uniform vec3 u_gainColor",
                     "uniform float u_gainAlpha", "uniform float u_shockR", "uniform vec3 u_shockColor", "uniform float u_shockAlpha",
                     "uniform float u_amp", "uniform float u_phase", "in vec2 v_ndc",
                 })
            Assert.Contains(needle, shock);
        string infall = Text("coreinfall.vert");
        foreach (string needle in new[]
                 {
                     "uniform float u_rIn", "uniform float u_rOut", "uniform float u_cycles", "uniform float u_pointPx",
                     "float r = pow(outer - tau * (outer - inner), 2.0 / 3.0);", "fract(u_cycles + a_seed)",
                 })
            Assert.Contains(needle, infall);
        Assert.Contains("uniform vec3 u_color", Text("coreinfall.frag"));
    }

    /// <summary>
    /// Plan Task 4: lensing.frag is the GLSL mirror of BlackHoleOptics.TraceRay (ruling P3-7): it declares the uniforms the brief
    /// names (spin, camera position and basis, R_esc, the surface radius, the grid flag) and carries the C# tracer's constants.
    /// </summary>
    [Fact]
    public void LensingShader_DeclaresItsUniforms_AndMirrorsTheTracersConstants()
    {
        string text = System.Text.Encoding.ASCII.GetString(Shaders().Single(s => s.Name.EndsWith(".lensing.frag")).Bytes);
        foreach (string needle in new[]
                 {
                     "uniform float u_spin", "uniform vec3 u_camPos", "uniform vec3 u_camForward", "uniform vec3 u_camRight",
                     "uniform vec3 u_camUp", "uniform float u_escapeRadius", "uniform float u_surfaceRadius", "uniform int u_grid",
                     "uniform samplerCube u_env", "float a = -u_spin",
                 })
            Assert.Contains(needle, text);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        Assert.Contains($"const int MAX_STEPS = {SupernovaLab.Core.Compact.BlackHoleOptics.MaxSteps};", text);
        Assert.Contains($"const float FAR_CAMERA_RADIUS = {SupernovaLab.Core.Compact.BlackHoleOptics.FarCameraRadius.ToString("0.0", inv)};", text);
        Assert.Contains($"const float FAR_START_RADIUS = {SupernovaLab.Core.Compact.BlackHoleOptics.FarStartRadius.ToString("0.00", inv)};", text);
        Assert.Contains("const float CAPTURE_MARGIN = 0.01;", text);
        Assert.Contains("const float MAX_STEP = 2.0;", text);
        Assert.Contains("min(0.05 * max(r - rPlus, 0.5) * (1.0 + r / 20.0), MAX_STEP)", text);   // the step rule
        Assert.Contains("2.0 * b / (c2 * (1.0 + cosG)) : 2.0 / b * (1.0 - cosG)", text);         // ruling R-P3-6's deflection
        // Ruling R-P3-7: step-limited rays sample the surroundings, as escaped ones do.
        Assert.Contains("fate == FATE_ESCAPED || fate == FATE_STEP_LIMIT", text);
        Assert.NotNull(Shaders().SingleOrDefault(s => s.Name.EndsWith(".upsample.frag")).Bytes);
    }
}
