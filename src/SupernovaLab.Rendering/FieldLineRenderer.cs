using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// Plan Task 6: the Close-up's thin lines (<c>fieldline.vert</c>/<c>.frag</c>): a neutron star's field lines, its light cylinder and the
/// Earth arrow. Polylines are uploaded once into a <see cref="Batch"/> (km, in the batch's own frame) and drawn with a frame matrix
/// (the magnetic frame turned with the spin, for the field lines), one flat colour, and the star hiding what lies inside or behind it
/// (<see cref="StarOcclusion"/>). 1 px lines (the core profile's only portable width). Create and use with the GL context current.
/// </summary>
public sealed class FieldLineRenderer : IDisposable
{
    /// <summary>A set of polylines on the GPU, as line segments.</summary>
    public sealed class Batch : IDisposable
    {
        internal int Vao, Vbo, Vertices;

        public void Dispose()
        {
            if (Vbo != 0) GL.DeleteBuffer(Vbo);
            if (Vao != 0) GL.DeleteVertexArray(Vao);
            Vao = Vbo = Vertices = 0;
        }
    }

    private readonly ShaderProgram _program = ShaderProgram.FromResources("fieldline.vert", "fieldline.frag");

    /// <summary>Uploads these polylines (each point to the next) as one batch.</summary>
    public Batch Upload(IEnumerable<Vector3d[]> polylines)
    {
        var data = new List<float>();
        foreach (var line in polylines)
            for (int i = 0; i + 1 < line.Length; i++)
            {
                Add(data, line[i]);
                Add(data, line[i + 1]);
            }
        var batch = new Batch { Vao = GL.GenVertexArray(), Vbo = GL.GenBuffer(), Vertices = data.Count / 3 };
        GL.BindVertexArray(batch.Vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, batch.Vbo);
        var array = data.ToArray();
        GL.BufferData(BufferTarget.ArrayBuffer, array.Length * sizeof(float), array, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.BindVertexArray(0);
        return batch;
    }

    private static void Add(List<float> data, Vector3d p)
    {
        data.Add((float)p.X);
        data.Add((float)p.Y);
        data.Add((float)p.Z);
    }

    /// <summary>Draws a batch turned into the scene by <paramref name="frame"/> (the batch's x, y and z axes in the scene), alpha-blended.</summary>
    public void Render(OrbitCamera camera, Size viewportPx, Batch batch, LineFrame frame, Vector4 color, StarOccluder? occluder)
    {
        if (batch.Vertices == 0 || !(color.W > 0)) return;
        _program.Use();
        _program.SetCamera(camera, viewportPx);
        // OpenTK stores rows first and GL reads columns first: transposed, the rows given here arrive as the GLSL matrix's columns.
        var m = new Matrix3((Vector3)frame.X, (Vector3)frame.Y, (Vector3)frame.Z);
        GL.UniformMatrix3(_program.Location("u_frame"), false, ref m);
        GL.Uniform4(_program.Location("u_color"), color);
        StarOccluder.Apply(_program, camera, occluder);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.LineWidth(1f);
        GL.BindVertexArray(batch.Vao);
        GL.DrawArrays(PrimitiveType.Lines, 0, batch.Vertices);
        GL.BindVertexArray(0);
    }

    public void Dispose() => _program.Dispose();
}

/// <summary>A batch's axes in the scene (unit vectors): x, y and z of the frame its points are given in.</summary>
public readonly record struct LineFrame(Vector3d X, Vector3d Y, Vector3d Z)
{
    public static LineFrame Identity { get; } = new(Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ);

    /// <summary>
    /// The magnetic frame at this tilt and spin phase: z along <see cref="RemnantRenderer.MagneticAxis"/>, x the magnetic meridian
    /// toward the spin axis' side (so a field line's plane at azimuth 0 contains the spin axis), y completing the right-handed set.
    /// </summary>
    public static LineFrame Magnetic(double tiltRad, double phaseRad)
    {
        var z = RemnantRenderer.MagneticAxis(tiltRad, phaseRad);
        // The tilt's own direction rotated by 90° toward +z: (cos α cos φ, cos α sin φ, −sin α).
        var x = new Vector3d(Math.Cos(tiltRad) * Math.Cos(phaseRad), Math.Cos(tiltRad) * Math.Sin(phaseRad), -Math.Sin(tiltRad));
        return new LineFrame(x, Vector3d.Cross(z, x), z);
    }
}
