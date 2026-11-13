using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// The scene's sky: how an observed orientation (Sugerman et al. 2005's x west, y north, z toward the observer; structure §S8) is
/// placed in the scene's axes. The scene has no fixed Earth direction (the camera orbits freely), so the observer is the camera's
/// default view: <see cref="DefaultYawDeg"/> and <see cref="DefaultPitchDeg"/> (the scene's z is up, as <see cref="OrbitCamera"/>).
/// From that camera, <b>west is screen right, north is screen up, and toward-the-observer is the direction from the target to the eye
/// (<see cref="OrbitCamera.Backward"/>)</b>. That triple is right-handed (right × up = toward the viewer), as is (west, north, toward),
/// and the sky as seen from Earth has north up and west on the right. So at the default view the south ring (offset toward the
/// observer) is the near one, projecting below the centre, and the north ring is behind it.
/// The cutaway turns the default camera by the wedge angle (ruling R-P2-22) but not the rings: with the cutaway on and a wedge
/// angle, the rings are the same in the scene and the default view is rotated to face the notch.
/// </summary>
public static class SkyFrame
{
    /// <summary>The camera's default direction (the view Home restores without the cutaway).</summary>
    public const double DefaultYawDeg = 30, DefaultPitchDeg = 20;

    /// <summary>The unit vectors of west, north and toward-the-observer in scene axes (cached from the default camera).</summary>
    public static (Vector3d West, Vector3d North, Vector3d Toward) Axes { get; } = ComputeAxes();

    private static (Vector3d, Vector3d, Vector3d) ComputeAxes()
    {
        var camera = new OrbitCamera { YawDeg = DefaultYawDeg, PitchDeg = DefaultPitchDeg };
        var (right, up) = camera.ScreenAxes();
        return (right, up, camera.Backward);
    }

    /// <summary>A sky-coordinate vector (west, north, toward the observer) in scene axes.</summary>
    public static Vector3d ToScene(double west, double north, double toward)
        => west * Axes.West + north * Axes.North + toward * Axes.Toward;
}
