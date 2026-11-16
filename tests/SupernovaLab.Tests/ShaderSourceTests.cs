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
                     "uniform float u_pointPx", "in vec3 a_pos", "in float a_alpha",   // Task 20: positions and fades from InfallSwarm
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
        Assert.Contains("min(0.05 * max(r - rPlus, stepFloor) * (1.0 + r / 20.0), MAX_STEP)", text);   // the step rule
        // Inconsistency #64 (Task 10 fix round 1): the floor mirrors BlackHoleOptics.StepFloor.
        Assert.Contains($"const float STEP_FLOOR_PER_PHOTON_GAP = {SupernovaLab.Core.Compact.BlackHoleOptics.StepFloorPerPhotonGap.ToString("0.0", inv)};", text);
        Assert.Contains("float rPro = 2.0 * (1.0 + cos(2.0 / 3.0 * acos(-min(u_spin, 1.0))));", text);
        Assert.Contains("clamp(STEP_FLOOR_PER_PHOTON_GAP * (rPro - rPlus), 0.02, 0.5)", text);
        Assert.Contains("2.0 * b / (c2 * (1.0 + cosG)) : 2.0 / b * (1.0 - cosG)", text);         // ruling R-P3-6's deflection
        // Ruling R-P3-7: step-limited rays sample the surroundings, as escaped ones do.
        Assert.Contains("fate == FATE_ESCAPED || fate == FATE_STEP_LIMIT", text);
        Assert.NotNull(Shaders().SingleOrDefault(s => s.Name.EndsWith(".upsample.frag")).Bytes);
    }

    /// <summary>
    /// Plan Task 10 (ruling P3-8): lensing.frag's grid mirrors BlackHoleOptics' (the sphere's radius, the lines' spacing, the outward
    /// crossing interpolated between steps), and the shading pass reads the grid's coverage from the ray map's alpha.
    /// </summary>
    [Fact]
    public void LensingShader_GridMirrorsTheCSharpTracer()
    {
        string Text(string name) => System.Text.Encoding.ASCII.GetString(Shaders().Single(s => s.Name.EndsWith("." + name)).Bytes);
        string text = Text("lensing.frag");
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        Assert.Contains($"const float GRID_RADIUS = {SupernovaLab.Core.Compact.BlackHoleOptics.GridRadius.ToString("0.0", inv)};", text);
        Assert.Contains($"const float GRID_STEP_DEG = {SupernovaLab.Core.Compact.BlackHoleOptics.GridStepDeg.ToString("0.0", inv)};", text);
        Assert.Contains("rPrev >= 0.0 && rPrev < GRID_RADIUS && r >= GRID_RADIUS", text);
        Assert.Contains("gridHit = mix(xPrev, x, (GRID_RADIUS - rPrev) / (r - rPrev));", text);
        Assert.Contains("uniform vec3 u_gridColor", text);
        Assert.Contains("3.0 + min(grid, 0.999)", text);
        string shade = Text("lensing_shade.frag");
        Assert.Contains("ray.a > 2.5", shade);
        Assert.Contains("uniform vec3 u_gridColor", shade);
    }

    /// <summary>
    /// Plan Task 11: the disk in lensing.frag mirrors BlackHoleOptics.Trace (the first crossing of the equatorial plane, linear between
    /// steps, inside the annulus [u_diskInner, u_diskOuter], tested before the other fates; ℓ from the tracer's momentum, sign flipped by the
    /// time reversal; g = 1/(u^t (1 − Ω ℓ)), Bardeen, Press &amp; Teukolsky 1972); the ray map keeps (g, r) with alpha 5, and disk.frag,
    /// linked into both programs, shades it with the C# DiskRenderer's profile and ramp.
    /// </summary>
    [Fact]
    public void LensingShader_DiskMirrorsTheCSharpTracer_AndDiskFragTheShading()
    {
        string Text(string name) => System.Text.Encoding.ASCII.GetString(Shaders().Single(s => s.Name.EndsWith("." + name)).Bytes);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string trace = Text("lensing.frag");
        foreach (string needle in new[]
                 {
                     "uniform float u_diskInner", "uniform float u_diskOuter",
                     "u_diskOuter > 0.0 && zPrev * x.z < 0.0",
                     "vec3 xc = mix(xPrev, x, zPrev / (zPrev - x.z));",
                     "rc >= u_diskInner && rc <= u_diskOuter",
                     "float ell = -(x.x * p.y - x.y * p.x);",
                     "float omega = 1.0 / (sr * rc + u_spin);",
                     "float ut = (sr * rc + u_spin) / (sqrt(sr * rc) * sqrt(sr * rc - 3.0 * sr + 2.0 * u_spin));",
                     "1.0 / (ut * (1.0 - omega * ell))",
                     "o_color = vec4(diskG, diskR, 0.0, 5.0);",
                     "vec3 diskShade(float g, float r);",
                 })
            Assert.Contains(needle, trace);
        string shade = Text("lensing_shade.frag");
        Assert.Contains("ray.a > 4.5", shade);
        Assert.Contains("diskShade(ray.x, ray.y)", shade);
        string disk = Text("disk.frag");
        Assert.Contains($"const int RAMP_SIZE = {DiskRenderer.RampSize};", disk);
        Assert.Contains("uniform vec3 u_diskRamp[RAMP_SIZE];", disk);
        Assert.Contains($"const float PROFILE_PEAK_ISCOS = {DiskRenderer.ProfilePeakIscos.ToString("0.0000", inv)};", disk);
        Assert.Contains($"const float PEAK_TEMPERATURE_K = {DiskRenderer.PeakTemperatureK.ToString("0.0", inv)};", disk);
        Assert.Contains($"const float RAMP_FIRST_K = {DiskRenderer.RampFirstK.ToString("0.0", inv)};", disk);
        Assert.Contains($"const float RAMP_STEP = {DiskRenderer.RampStep.ToString("0.000000", inv)};", disk);
        Assert.Contains("g * g * g * g * diskProfile(r)", disk);
        Assert.Contains("uniform float u_diskLevel", disk);
        // Fix round 1: cone.frag drops what the disk hides (StarOcclusion.HiddenByDisk's mirror).
        string cone = Text("cone.frag");
        Assert.Contains("uniform vec2 u_diskRadii", cone);
        Assert.Contains("float t = -eyeZ / p.z;", cone);
        Assert.Contains("rho >= u_diskRadii.x && rho <= u_diskRadii.y", cone);
    }

    /// <summary>
    /// Task 14 (Task 7's carried minor): nscut.frag repeats the cutaway lines' widths and pixel floors as literals; they must equal
    /// <see cref="NeutronStarCutawayRenderer"/>'s constants, which the tests and the strip use.
    /// </summary>
    [Fact]
    public void Nscut_LineWidthsAndFloors_MatchTheRenderer()
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string text = System.Text.Encoding.ASCII.GetString(Shaders().Single(s => s.Name.EndsWith(".nscut.frag")).Bytes);
        string F(double v) => v.ToString("0.0##", inv);
        Assert.Contains($"const float BOUNDARY_WIDTH = {F(NeutronStarCutawayRenderer.BoundaryWidthRadii)};", text);
        Assert.Contains($"const float DRIP_WIDTH = {F(NeutronStarCutawayRenderer.DripWidthRadii)};", text);
        Assert.Contains($"const float ATMOSPHERE_WIDTH = {F(NeutronStarCutawayRenderer.AtmosphereWidthRadii)};", text);
        Assert.Contains($"max(BOUNDARY_WIDTH, {F(NeutronStarCutawayRenderer.BoundaryMinPx)} * u_radiiPerPx)", text);
        Assert.Contains($"max(DRIP_WIDTH, {F(NeutronStarCutawayRenderer.DripMinPx)} * u_radiiPerPx)", text);
        Assert.Contains($"max(ATMOSPHERE_WIDTH, {F(NeutronStarCutawayRenderer.AtmosphereMinPx)} * u_radiiPerPx)", text);
    }
}
