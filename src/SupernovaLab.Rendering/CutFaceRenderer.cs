using System.Windows;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SupernovaLab.Core.Structure;

namespace SupernovaLab.Rendering;

/// <summary>
/// What <see cref="CutFaceRenderer"/> needs: the faces' model (<see cref="CutFaceModel"/>: layers, boundaries, finger clusters,
/// all in face coordinates over the outer radius), the layers' HDR colours (centre outward, as the model's layers), the nickel and
/// hydrogen colours, and, in scene units (cm divided by the view's unit on the CPU), v_t's radius for the opacity. Once there are
/// shocks (Task 8) the radii are confined as the particles are, and <see cref="FreeScale"/> (R* + v_max (t − t_bo)),
/// <see cref="Reverse"/>, <see cref="Contact"/> and <see cref="ShockWeight"/> let the shader read the speed back for the opacity
/// (<see cref="CutFaceModel.FreeRadiusCm"/>); ShockWeight 0 means no shocks.
/// </summary>
public sealed record CutFaceUniforms(CutFaceModel Faces, IReadOnlyList<Vector3> LayerColorList, Vector3 NickelColor, Vector3 HydrogenColor,
    float TransitionRadius, float DensityIndex)
{
    public float FreeScale { get; init; }
    public float Reverse { get; init; }
    public float Contact { get; init; }
    public float ShockWeight { get; init; }
    /// <summary>s_m (<c>ParticleKinematics.ShockedSpeedRatio</c>); 1 or more: nothing shocked.</summary>
    public float ShockRatio { get; init; } = 2;

    private readonly CutFacePlaneArrays?[] _planes = new CutFacePlaneArrays?[2];

    /// <summary>The face's cluster, finger and tongue arrays as the shader reads them (packed once per face).</summary>
    public CutFacePlaneArrays Plane(int plane) => _planes[plane] ??= CutFacePlaneArrays.Pack(Faces, plane);
}

/// <summary>
/// One face's finger clusters packed for <c>cutface.frag</c> (Task 16): u_cluster[c] = (centre, half-width, first finger, finger
/// count); u_finger[i] = (centre, tip radius, stalk half-width (arc), seed); u_tongue[i] = (centre, tip radius, root half-width (arc),
/// seed), cluster c's tongues starting at its first finger − c (each cluster has one tongue fewer than fingers). At most
/// <see cref="CutFaceRenderer.MaxCones"/> clusters (one per cone), <see cref="MaxFingersPerCluster"/> plumes each.
/// </summary>
public sealed record CutFacePlaneArrays(int ClusterCount, float[] Clusters, float[] Fingers, float[] Tongues)
{
    public const int MaxFingersPerCluster = CutFaceModel.MaxPlumes;

    public static CutFacePlaneArrays Pack(CutFaceModel faces, int plane)
    {
        var list = faces.ClustersOn(plane);
        int n = Math.Min(list.Count, CutFaceRenderer.MaxCones);
        var clusters = new float[4 * CutFaceRenderer.MaxCones];
        var fingers = new float[4 * CutFaceRenderer.MaxCones * MaxFingersPerCluster];
        var tongues = new float[4 * CutFaceRenderer.MaxCones * (MaxFingersPerCluster - 1)];
        int first = 0;
        for (int c = 0; c < n; c++)
        {
            var cl = list[c];
            int count = Math.Min(cl.Fingers.Count, MaxFingersPerCluster);
            clusters[4 * c] = cl.CentreRad;
            clusters[4 * c + 1] = cl.HalfWidthRad;
            clusters[4 * c + 2] = first;
            clusters[4 * c + 3] = count;
            for (int j = 0; j < count; j++)
            {
                var f = cl.Fingers[j];
                int i = 4 * (first + j);
                (fingers[i], fingers[i + 1], fingers[i + 2], fingers[i + 3]) = (f.CentreRad, f.TipRadius, f.HalfWidth, f.Seed);
            }
            for (int j = 0; j < Math.Min(cl.Tongues.Count, count - 1); j++)
            {
                var t = cl.Tongues[j];
                int i = 4 * (first - c + j);
                (tongues[i], tongues[i + 1], tongues[i + 2], tongues[i + 3]) = (t.CentreRad, t.TipRadius, t.HalfWidth, t.Seed);
            }
            first += count;
        }
        return new CutFacePlaneArrays(n, clusters, fingers, tongues);
    }
}

/// <summary>
/// The debris cutaway's cut faces (ruling R-P2-21): the two quarter planes bounding the removed quarter, x &gt; 0 and
/// z &gt; 0 in the wedge frame, drawn as quads shaded per pixel (<c>cutface.frag</c>, mirroring
/// <see cref="CutFaceModel.At"/>). Draw them after the bloom is built (<see cref="HdrPipeline.End"/>'s hook): they are translucent where the cloud is thin
/// (<see cref="CutFaceModel.Opacity"/>, ruling R-P2-23), so they dim the glow behind them and add none of their own. Alpha is also the tone map's bloom mask.
/// </summary>
public sealed class CutFaceRenderer : IDisposable
{
    public const int MaxCones = 10;

    /// <summary>The wedge's angle about the scene's y axis (ruling R-P2-22), radians.</summary>
    public double WedgeAngleRad { get; set; }

    private readonly ShaderProgram _program = ShaderProgram.FromResources("cutface.vert", "cutface.frag");
    private readonly int _vao, _vbo;

    public CutFaceRenderer()
    {
        // One quad, a in [-1, 1] (along y), b in [0, 1] (along z, or x for the second plane).
        float[] corners = { -1, 0, 1, 0, 1, 1, -1, 0, 1, 1, -1, 1 };
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, corners.Length * sizeof(float), corners, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.BindVertexArray(0);
    }

    /// <summary>Draws both faces out to <paramref name="extent"/> scene units (the debris' outer radius).</summary>
    public void Render(OrbitCamera cam, Size viewportPx, float extent, CutFaceUniforms u)
    {
        var faces = u.Faces;
        int n = Math.Min(Math.Min(faces.Layers.Count, u.LayerColorList.Count), LayerColors.Count);
        if (n == 0 || !(extent > 0)) return;
        var outer = new float[LayerColors.Count];
        var capUp = new float[LayerColors.Count];
        var capDown = new float[LayerColors.Count];
        var colors = new float[3 * LayerColors.Count];
        int nickel = -1, oxygen = -1, helium = -1, hydrogen = -1;
        for (int i = 0; i < n; i++)
        {
            outer[i] = faces.NormalisedOuter[i];
            capUp[i] = faces.BoundaryCapUp[i];
            capDown[i] = faces.BoundaryCapDown[i];
            var c = u.LayerColorList[i];
            (colors[3 * i], colors[3 * i + 1], colors[3 * i + 2]) = (c.X, c.Y, c.Z);
            if (faces.Layers[i].Layer == ElementLayer.NickelIron) nickel = i;
            if (faces.Layers[i].Layer == ElementLayer.CarbonOxygenCore) oxygen = i;
            if (faces.Layers[i].Layer == ElementLayer.Helium) helium = i;
            if (faces.Layers[i].Layer == ElementLayer.Hydrogen) hydrogen = i;
        }

        _program.Use();
        _program.SetCamera(cam, viewportPx);
        _program.Set("u_extent", extent);
        _program.Set("u_layerCount", n);
        GL.Uniform1(_program.Location("u_layerOuter"), LayerColors.Count, outer);
        GL.Uniform1(_program.Location("u_layerCapUp"), LayerColors.Count, capUp);
        GL.Uniform1(_program.Location("u_layerCapDown"), LayerColors.Count, capDown);
        GL.Uniform3(_program.Location("u_layerColor"), LayerColors.Count, colors);
        _program.Set("u_nickelLayer", nickel);
        _program.Set("u_oxygenLayer", oxygen);
        _program.Set("u_heliumLayer", helium);
        _program.Set("u_hydrogenLayer", hydrogen);
        _program.Set("u_nickelColor", u.NickelColor);
        _program.Set("u_hydrogenColor", u.HydrogenColor);
        _program.Set("u_fingerRoot", faces.FingerRootRadius);
        _program.Set("u_tongueRoot", faces.TongueRootRadius);
        _program.Set("u_transitionRadius", u.TransitionRadius);
        _program.Set("u_densityIndex", u.DensityIndex);
        _program.Set("u_freeScale", u.FreeScale);
        _program.Set("u_reverse", u.Reverse);
        _program.Set("u_contact", u.Contact);
        _program.Set("u_shockWeight", u.ShockWeight);
        _program.Set("u_shockRatio", float.IsFinite(u.ShockRatio) ? Math.Min(u.ShockRatio, 2f) : 2f);
        WedgeUniforms.Set(_program, WedgeAngleRad);

        // Over what is drawn (alpha = the density profile's opacity); the alpha channel accumulates as the bloom mask.
        GL.Enable(EnableCap.Blend);
        GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
        GL.BindVertexArray(_vao);
        for (int plane = 0; plane < 2; plane++)
        {
            var p = u.Plane(plane);
            _program.Set("u_plane", plane);
            _program.Set("u_clusterCount", p.ClusterCount);
            GL.Uniform4(_program.Location("u_cluster"), p.Clusters.Length / 4, p.Clusters);
            GL.Uniform4(_program.Location("u_finger"), p.Fingers.Length / 4, p.Fingers);
            GL.Uniform4(_program.Location("u_tongue"), p.Tongues.Length / 4, p.Tongues);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        GL.DeleteVertexArray(_vao);
        GL.DeleteBuffer(_vbo);
        _program.Dispose();
    }
}
