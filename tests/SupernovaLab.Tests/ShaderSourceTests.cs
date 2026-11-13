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
    public void TheWedgeShaders_ShareTheWedgeFrame(string shader)
    {
        // Ruling R-P2-22: the particle drop, the face quads and the star all use the wedge basis (WedgeOrientation's).
        string text = System.Text.Encoding.ASCII.GetString(Shaders().Single(s => s.Name.EndsWith("." + shader)).Bytes);
        Assert.Contains("uniform vec3 u_wedgeX", text);
        Assert.Contains("uniform vec3 u_wedgeZ", text);
    }
}
