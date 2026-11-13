namespace SupernovaLab.Core.Structure;

/// <summary>
/// Where the cutaway's wedge sits (ruling R-P2-22). The removed quarter is w.x &gt; 0 and w.z &gt; 0 in a frame turned about the
/// scene's y axis (the common edge of the two cut planes) by an angle phi: w.x = p . <see cref="BasisX"/>, w.z = p . <see cref="BasisZ"/>,
/// w.y = p.y. With phi = 0 it is the scene's own x &gt; 0 and z &gt; 0. A plane through the y axis can contain any direction, so
/// <see cref="AngleFor"/> picks the phi that puts the most finger cones across a cut face (the cone axes are not changed).
/// The particle shader, the cut-face shaders, the star shader and <see cref="CutFaceModel"/> all use this one basis.
/// </summary>
public static class WedgeOrientation
{
    /// <summary>The angles tried, in degrees apart: 0, 0.25, ... up to 360.</summary>
    public const double StepDeg = 0.25;

    public static (double X, double Y, double Z) BasisX(double phi) => (Math.Cos(phi), 0, -Math.Sin(phi));

    public static (double X, double Y, double Z) BasisZ(double phi) => (Math.Sin(phi), 0, Math.Cos(phi));

    /// <summary>A scene point or direction in the wedge frame.</summary>
    public static (double X, double Y, double Z) ToWedge(double phi, double x, double y, double z)
        => (x * Math.Cos(phi) - z * Math.Sin(phi), y, x * Math.Sin(phi) + z * Math.Cos(phi));

    /// <summary>A direction given in the wedge frame, in scene coordinates (the inverse of <see cref="ToWedge"/>).</summary>
    public static (double X, double Y, double Z) FromWedge(double phi, double wx, double wy, double wz)
        => (wx * Math.Cos(phi) + wz * Math.Sin(phi), wy, -wx * Math.Sin(phi) + wz * Math.Cos(phi));

    /// <summary>Whether this scene point is in the removed quarter.</summary>
    public static bool IsRemoved(double phi, double x, double y, double z)
    {
        var w = ToWedge(phi, x, y, z);
        return w.X > 0 && w.Z > 0;
    }

    /// <summary>
    /// Whether a cut face is seen from its exposed side, so it is drawn: plane 0 (w.x = 0, bounding the removed quarter on its
    /// +w.x side) only from an eye with w.x &gt; 0, plane 1 (w.z = 0) only from w.z &gt; 0. The eye is the camera's position
    /// relative to the centre (the planes pass through it). <c>cutface.vert</c> mirrors this.
    /// </summary>
    public static bool FaceExposed(int plane, double phi, double eyeX, double eyeY, double eyeZ)
    {
        var w = ToWedge(phi, eyeX, eyeY, eyeZ);
        return (plane == 0 ? w.X : w.Z) > 0;
    }

    /// <summary>
    /// Whether a cone (unit axis in scene coordinates, half-angle in radians) crosses either cut half-plane: the angle between
    /// the axis and the half-plane (the plane x = 0 with z &gt; 0, or the plane z = 0 with x &gt; 0, in the wedge frame) is at most the half-angle.
    /// </summary>
    public static bool CrossesFace(double phi, (double X, double Y, double Z) axis, double halfAngleRad)
    {
        var a = ToWedge(phi, axis.X, axis.Y, axis.Z);
        double alongY = Math.Acos(Math.Min(1, Math.Abs(a.Y)));   // to the nearest end of the common edge
        double faceA = a.Z >= 0 ? Math.Asin(Math.Min(1, Math.Abs(a.X))) : alongY;   // half-plane w.x = 0, w.z > 0
        double faceB = a.X >= 0 ? Math.Asin(Math.Min(1, Math.Abs(a.Z))) : alongY;   // half-plane w.z = 0, w.x > 0
        return Math.Min(faceA, faceB) <= halfAngleRad;
    }

    /// <summary>How many cones cross a cut face at this angle.</summary>
    public static int CrossingCount(double phi, FingerCones cones)
    {
        int n = 0;
        foreach (var axis in cones.Axes)
            if (CrossesFace(phi, axis, cones.HalfAngleRad)) n++;
        return n;
    }

    /// <summary>
    /// The angle (radians, in [0, 2 pi)) with the most cones across a cut face; ties go to the smallest angle. Deterministic.
    /// 0 when there are no cones.
    /// </summary>
    public static double AngleFor(FingerCones cones)
    {
        if (cones.Axes.Count == 0) return 0;
        double best = 0;
        int bestCount = -1;
        for (int i = 0; i < 360 / StepDeg; i++)
        {
            double phi = i * StepDeg * Math.PI / 180;
            int n = CrossingCount(phi, cones);
            if (n > bestCount) { best = phi; bestCount = n; }
        }
        return best;
    }
}
