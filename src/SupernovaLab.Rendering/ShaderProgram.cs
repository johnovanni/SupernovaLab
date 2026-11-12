using System.IO;
using System.Reflection;
using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>A linked GL program built from shader sources embedded under <c>Shaders/</c>. Copied from Universe3D (spec §7),
/// with the namespace changed, its GlMath helper folded in and the setters and stage this app does not use removed.</summary>
public sealed class ShaderProgram : IDisposable
{
    private readonly Dictionary<string, int> _uniforms = new();

    private ShaderProgram(int handle) => Handle = handle;

    public int Handle { get; private set; }

    /// <summary>Compiles and links the named resources, e.g. <c>"points.vert", "points.frag"</c>.</summary>
    public static ShaderProgram FromResources(params string[] fileNames)
    {
        var shaders = new List<int>();
        int program = GL.CreateProgram();
        try
        {
            foreach (var name in fileNames)
            {
                int shader = GL.CreateShader(TypeOf(name));
                shaders.Add(shader);
                GL.ShaderSource(shader, ReadSource(name));
                GL.CompileShader(shader);
                GL.GetShader(shader, ShaderParameter.CompileStatus, out int ok);
                if (ok == 0)
                    throw new InvalidOperationException($"Shader {name} failed to compile:\n{GL.GetShaderInfoLog(shader)}");
                GL.AttachShader(program, shader);
            }
            GL.LinkProgram(program);
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int linked);
            if (linked == 0)
                throw new InvalidOperationException(
                    $"Program {string.Join(" + ", fileNames)} failed to link:\n{GL.GetProgramInfoLog(program)}");
        }
        catch
        {
            GL.DeleteProgram(program);
            throw;
        }
        finally
        {
            foreach (int s in shaders)
            {
                GL.DetachShader(program, s);
                GL.DeleteShader(s);
            }
        }
        return new ShaderProgram(program);
    }

    public static string ReadSource(string fileName)
    {
        var asm = Assembly.GetExecutingAssembly();
        string resource = $"{typeof(ShaderProgram).Namespace}.Shaders.{fileName}";
        using var stream = asm.GetManifestResourceStream(resource)
            ?? throw new FileNotFoundException($"Shader resource '{resource}' is not embedded.", fileName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void Use() => GL.UseProgram(Handle);

    /// <summary>Uniform location, or −1 when the linker removed it (setting −1 is a no-op in GL).</summary>
    public int Location(string name)
    {
        if (!_uniforms.TryGetValue(name, out int loc))
            _uniforms[name] = loc = GL.GetUniformLocation(Handle, name);
        return loc;
    }

    public void Set(string name, float v) => GL.Uniform1(Location(name), v);
    public void Set(string name, int v) => GL.Uniform1(Location(name), v);
    public void Set(string name, Vector3 v) => GL.Uniform3(Location(name), v);

    public void Set(string name, Matrix4 m) => GL.UniformMatrix4(Location(name), false, ref m);

    /// <summary>
    /// Sets the shared camera uniforms: <c>u_eyeHigh</c>/<c>u_eyeLow</c> (relative-to-eye split of the eye),
    /// <c>u_viewProj</c> (rotation-only view times projection).
    /// </summary>
    public void SetCamera(OrbitCamera cam, Size viewportPx)
    {
        var (eyeHigh, eyeLow) = OrbitCamera.Split(cam.Eye);
        Set("u_eyeHigh", eyeHigh);
        Set("u_eyeLow", eyeLow);
        double aspect = viewportPx.Height > 0 ? viewportPx.Width / viewportPx.Height : 1;
        Set("u_viewProj", ToMatrix4(cam.View * cam.Projection(aspect)));
    }

    /// <summary>Double-to-single conversion for matrices sent to the GPU (Universe3D's GlMath).</summary>
    public static Matrix4 ToMatrix4(Matrix4d m) => new(
        (float)m.M11, (float)m.M12, (float)m.M13, (float)m.M14,
        (float)m.M21, (float)m.M22, (float)m.M23, (float)m.M24,
        (float)m.M31, (float)m.M32, (float)m.M33, (float)m.M34,
        (float)m.M41, (float)m.M42, (float)m.M43, (float)m.M44);

    private static ShaderType TypeOf(string fileName) => Path.GetExtension(fileName) switch
    {
        ".vert" => ShaderType.VertexShader,
        ".frag" => ShaderType.FragmentShader,
        _ => throw new ArgumentException($"Unknown shader stage for '{fileName}'.", nameof(fileName)),
    };

    public void Dispose()
    {
        if (Handle != 0) GL.DeleteProgram(Handle);
        Handle = 0;
    }
}
