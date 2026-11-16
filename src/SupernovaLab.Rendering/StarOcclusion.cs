using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>A star that hides the overlays behind it: its centre (scene units), radius and apparent radius (<see cref="StarOcclusion"/>).</summary>
public readonly record struct StarOccluder(Vector3d Centre, double Radius, double ApparentRadius)
{
    /// <summary>Sets <c>u_occCentre</c> (relative to the camera's eye) and <c>u_occRadii</c>; an absent occluder (null) hides nothing.</summary>
    internal static void Apply(ShaderProgram program, OrbitCamera camera, StarOccluder? occluder)
    {
        var o = occluder ?? default;
        program.Set("u_occCentre", (Vector3)(o.Centre - camera.Eye));
        program.Set("u_occRadii", new Vector2((float)o.Radius, (float)o.ApparentRadius));
    }
}

/// <summary>
/// Plan Task 6: which parts of the Close-up's overlays (field lines, light cylinder, Earth arrow, beams) the neutron star hides. The
/// overlays are drawn along straight lines, while the lensing pass draws the star bent: it looks larger than it is, about
/// R / √(1 − 2GM/(c²R)) across from afar (the largest impact parameter that reaches the surface; 1.24 R for 1.4 M☉ and 12 km). So a
/// point is hidden when the straight line from the eye enters the true sphere before it, or when it lies beyond the star's centre
/// within that apparent radius (behind the bent disc). The GLSL in <c>fieldline.frag</c> and <c>cone.frag</c> mirrors
/// <see cref="Hidden"/>. Positions are relative to the eye.
/// </summary>
public static class StarOcclusion
{
    /// <summary>How far past the sphere's entry point a point must lie to be hidden, in radii (so the near surface's own points show).</summary>
    public const double SurfaceMargin = 0.01;

    /// <summary>
    /// The star's radius as the straight-line overlays should see it from distance <paramref name="distanceKm"/>: the bent disc's
    /// largest impact parameter, R / √(1 − 2 GM/(c² R)), reduced by √(1 − 2 GM/(c² D)) for a near camera; R itself without a mass.
    /// </summary>
    public static double ApparentRadiusKm(double radiusKm, double gravitationalRadiusKm, double distanceKm)
    {
        if (!(gravitationalRadiusKm > 0) || !(radiusKm > 2 * gravitationalRadiusKm)) return radiusKm;
        double b = radiusKm / Math.Sqrt(1 - 2 * gravitationalRadiusKm / radiusKm);
        double near = distanceKm > 2 * gravitationalRadiusKm ? Math.Sqrt(1 - 2 * gravitationalRadiusKm / distanceKm) : 1;
        return Math.Max(radiusKm, b * near);
    }

    /// <summary>Whether the point <paramref name="p"/> (relative to the eye) is hidden by the star centred at <paramref name="centre"/>.</summary>
    public static bool Hidden(Vector3d p, Vector3d centre, double radius, double apparentRadius)
    {
        double s = p.Length;
        if (!(radius > 0) || !(s > 0)) return false;
        var d = p / s;
        double tc = Vector3d.Dot(centre, d);
        double b2 = (centre - tc * d).LengthSquared;
        if (b2 < radius * radius && s > tc - Math.Sqrt(radius * radius - b2) + SurfaceMargin * radius) return true;
        return b2 < apparentRadius * apparentRadius && s > tc;
    }
}
