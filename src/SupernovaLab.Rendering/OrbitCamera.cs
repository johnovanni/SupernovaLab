using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// A camera orbiting a target point (z up). The view matrix is rotation only: positions reach the GPU relative to the
/// eye (see <see cref="Split"/>), so precision holds far from the origin.
/// <para>Copied from Universe3D (spec §7) with the namespace changed and its own vector type replaced by OpenTK's
/// <see cref="Vector3d"/>. Universe3D measures in Mpc; here the distances are in the 3D view's scene units (the
/// framing radius is 1), so the "Mpc" names read "scene units". Panning and fly-to, which this view does not use,
/// are removed.</para>
/// </summary>
public sealed class OrbitCamera
{
    public const double MinDistanceMpc = 1e-4;
    public const double MaxDistanceMpc = 1e5;
    public const double MaxPitchDeg = 89.9;
    public const double ZoomPerNotch = 1.15;
    public const double DegreesPerPixel = 0.25;

    private double _pitchDeg;
    private double _distanceMpc = 10;
    private Vector3d? _fixedBackward;
    private Vector3d _up = Vector3d.UnitZ;

    public Vector3d Target { get; set; }

    public double YawDeg { get; set; }

    public double PitchDeg
    {
        get => _pitchDeg;
        set => _pitchDeg = Math.Clamp(value, -MaxPitchDeg, MaxPitchDeg);
    }

    public double DistanceMpc
    {
        get => _distanceMpc;
        set => _distanceMpc = Math.Clamp(value, MinDistanceMpc, MaxDistanceMpc);
    }

    public double FieldOfViewDeg { get; set; } = 45;

    /// <summary>Positive notches move closer: the distance is divided by 1.15 per notch.</summary>
    public void Zoom(int notches)
    {
        DistanceMpc /= Math.Pow(ZoomPerNotch, notches);
    }

    public void Rotate(double dxPx, double dyPx)
    {
        YawDeg = NormalizeDeg(YawDeg - dxPx * DegreesPerPixel);
        PitchDeg += dyPx * DegreesPerPixel;
    }

    /// <summary>
    /// Points the camera along −<paramref name="backward"/> with this screen-up direction, in place of its yaw and pitch (Project 3:
    /// the Close-up's environment cube map renders its six faces with the GL cube-map orientations, which yaw and pitch cannot reach).
    /// </summary>
    public void Orient(Vector3d backward, Vector3d up)
    {
        _fixedBackward = backward.Normalized();
        _up = up.Normalized();
    }

    /// <summary>Unit vector from the target toward the eye (from the yaw and pitch, or from <see cref="Orient"/>).</summary>
    public Vector3d Backward
    {
        get
        {
            if (_fixedBackward is { } fixedBackward) return fixedBackward;
            double yaw = YawDeg * Math.PI / 180, pitch = PitchDeg * Math.PI / 180;
            return new Vector3d(Math.Cos(pitch) * Math.Cos(yaw), Math.Cos(pitch) * Math.Sin(yaw), Math.Sin(pitch));
        }
    }

    public Vector3d Eye => Target + Backward * DistanceMpc;

    /// <summary>World-to-view rotation (no translation), for row vectors as OpenTK uses.</summary>
    public Matrix4d View
    {
        get
        {
            var b = Backward;
            return Matrix4d.LookAt(Vector3d.Zero, -b, _up);
        }
    }

    public double NearMpc => DistanceMpc * 1e-3;

    public double FarMpc => DistanceMpc + 2 * MaxDistanceMpc;

    public Matrix4d Projection(double aspect) =>
        Matrix4d.CreatePerspectiveFieldOfView(FovRad, aspect > 0 ? aspect : 1, NearMpc, FarMpc);

    /// <summary>
    /// Relative-to-eye encoding: <c>high</c> is v rounded to float, <c>low</c> the float of the remainder,
    /// so <c>(p.high − eye.high) + (p.low − eye.low)</c> keeps about 1e-12 relative precision on the GPU.
    /// </summary>
    public static (Vector3 high, Vector3 low) Split(Vector3d v)
    {
        var high = new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        var low = new Vector3((float)(v.X - high.X), (float)(v.Y - high.Y), (float)(v.Z - high.Z));
        return (high, low);
    }

    private double FovRad => FieldOfViewDeg * Math.PI / 180;

    /// <summary>Unit screen-right and screen-up directions in the display frame (public here: billboards use them). The up hint is +z
    /// unless <see cref="Orient"/> gave another.</summary>
    public (Vector3d right, Vector3d up) ScreenAxes()
    {
        var forward = -Backward;
        var right = Vector3d.Cross(forward, _up).Normalized();
        var up = Vector3d.Cross(right, forward);
        return (right, up);
    }

    private static double NormalizeDeg(double deg)
    {
        deg %= 360;
        return deg < 0 ? deg + 360 : deg;
    }
}
