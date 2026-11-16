using System.Windows;
using OpenTK.Mathematics;

namespace SupernovaLab.Rendering;

/// <summary>
/// Plan Task 6: what the Close-up draws round a neutron star besides its surface (which the lensing pass shades, <c>nsurface.frag</c>):
/// <list type="bullet">
/// <item>the beams, the jets' cones (<see cref="JetRenderer.RenderCone"/>) along ± the magnetic axis
/// (<see cref="RemnantRenderer.MagneticAxis"/>: the main view's tilt, phase and half-width ρ(P)), from
/// <see cref="CloseUpState.ShowBeams"/> on;</item>
/// <item>the dipole field lines (<see cref="FieldLines"/>) in the magnetic frame turned with the spin, the open ones in their own
/// colour;</item>
/// <item>the light cylinder, a ring of radius c P / 2π round the spin axis in the equatorial plane;</item>
/// <item>the "To Earth" arrow along <see cref="CloseUpState.EarthDirection"/>.</item>
/// </list>
/// The geometry is uploaded once in units of the star's radius and scaled by the frame (the field lines again only when the light
/// cylinder moves by more than 2%, as the star slows). The star hides what lies inside or behind it (<see cref="StarOcclusion"/>).
/// Nothing here is bent by the star's gravity (a note says so). Create and use with the GL context current.
/// </summary>
public sealed class NeutronStarRenderer : IDisposable
{
    /// <summary>The Earth arrow runs from this many radii to its tip, along Earth's direction.</summary>
    public const double ArrowFromRadii = 1.15, ArrowTipRadii = 4;

    /// <summary>The arrowhead's length and half-width, radii.</summary>
    private const double HeadRadii = 0.5, HeadHalfWidthRadii = 0.2;

    private const int CircleSegments = 256;

    /// <summary>[schematic] The colours: closed field lines, open ones, the light cylinder, the Earth arrow (alpha in W).</summary>
    public static readonly Vector4 ClosedLineColor = new(0.45f, 0.66f, 1f, 0.6f), OpenLineColor = new(1f, 0.62f, 0.3f, 0.85f),
        LightCylinderColor = new(0.9f, 0.88f, 0.55f, 0.75f), EarthArrowColor = new(1f, 0.92f, 0.35f, 0.95f);

    private readonly JetRenderer _cones;
    private readonly FieldLineRenderer _lines = new();
    private readonly FieldLineRenderer.Batch _circle, _arrow;
    private FieldLineRenderer.Batch? _closed, _open;
    private (double Field, double LightCylinderRadii) _fieldKey = (double.NaN, double.NaN);

    public NeutronStarRenderer()
    {
        _cones = new JetRenderer();
        var circle = new Vector3d[CircleSegments + 1];
        for (int i = 0; i <= CircleSegments; i++)
        {
            double a = 2 * Math.PI * i / CircleSegments;
            circle[i] = new Vector3d(Math.Cos(a), Math.Sin(a), 0);
        }
        _circle = _lines.Upload(new[] { circle });
        _arrow = _lines.Upload(EarthArrow());
    }

    /// <summary>
    /// The Earth arrow in units of the star's radius, along +z: the shaft from <see cref="ArrowFromRadii"/> to <see cref="ArrowTipRadii"/>
    /// and a four-sided head, so it reads from any side.
    /// </summary>
    public static IReadOnlyList<Vector3d[]> EarthArrow()
    {
        var tip = new Vector3d(0, 0, ArrowTipRadii);
        var lines = new List<Vector3d[]> { new[] { new Vector3d(0, 0, ArrowFromRadii), tip } };
        foreach (var side in new[] { Vector3d.UnitX, -Vector3d.UnitX, Vector3d.UnitY, -Vector3d.UnitY })
            lines.Add(new[] { tip, new Vector3d(0, 0, ArrowTipRadii - HeadRadii) + HeadHalfWidthRadii * side });
        return lines;
    }

    /// <summary>The frame that turns +z of the arrow's geometry onto Earth's direction, scaled by the radius.</summary>
    public static LineFrame EarthFrame(Vector3d earth, double radiusKm)
    {
        var z = earth.Normalized();
        var helper = Math.Abs(z.X) < 0.9 ? Vector3d.UnitX : Vector3d.UnitY;
        var x = Vector3d.Cross(helper, z).Normalized();
        var y = Vector3d.Cross(z, x);
        return new LineFrame(x * radiusKm, y * radiusKm, z * radiusKm);
    }

    /// <summary>The beams (from <see cref="CloseUpState.ShowBeams"/>), this long (km), at this colour gain.</summary>
    public void RenderBeams(OrbitCamera camera, Size viewportPx, CloseUpState s, double phaseRad, double lengthKm, float gain,
        StarOccluder occluder)
    {
        if (!s.ShowBeams || !(s.BeamHalfAngleRad > 0) || !(lengthKm > 0)) return;
        var axis = RemnantRenderer.MagneticAxis(s.BeamTiltRad, phaseRad);
        var color = RemnantRenderer.BeamColor * gain;
        _cones.RenderCone(camera, viewportPx, axis, lengthKm, s.BeamHalfAngleRad, color, 0.45f, default, occluder);
        _cones.RenderCone(camera, viewportPx, -axis, lengthKm, s.BeamHalfAngleRad, color, 0.45f, default, occluder);
    }

    /// <summary>The field lines (if <paramref name="fieldLines"/>), the light cylinder (if <paramref name="lightCylinder"/>) and the Earth arrow.</summary>
    public void RenderLines(OrbitCamera camera, Size viewportPx, CloseUpState s, double phaseRad, bool fieldLines, bool lightCylinder,
        float gain, StarOccluder occluder)
    {
        double r = s.SurfaceRadiusKm;
        if (!(r > 0) || s.NeutronStar is null) return;
        var scale = new Vector4(gain, gain, gain, 1);
        if (fieldLines)
        {
            EnsureFieldLines(s.FieldG, s.LightCylinderKm / r);
            var m = LineFrame.Magnetic(s.BeamTiltRad, phaseRad);
            var frame = new LineFrame(m.X * r, m.Y * r, m.Z * r);
            _lines.Render(camera, viewportPx, _closed!, frame, ClosedLineColor * scale, occluder);
            _lines.Render(camera, viewportPx, _open!, frame, OpenLineColor * scale, occluder);
        }
        if (lightCylinder && double.IsFinite(s.LightCylinderKm))
        {
            double rl = s.LightCylinderKm;
            _lines.Render(camera, viewportPx, _circle, new LineFrame(Vector3d.UnitX * rl, Vector3d.UnitY * rl, Vector3d.UnitZ * rl),
                LightCylinderColor * scale, occluder);
        }
        var e = s.EarthDirection;
        _lines.Render(camera, viewportPx, _arrow, EarthFrame(new Vector3d(e.X, e.Y, e.Z), r), EarthArrowColor * scale, occluder);
    }

    /// <summary>Remakes the field lines (in radii) when the field or the light cylinder (by more than 2%) has changed.</summary>
    private void EnsureFieldLines(double fieldG, double lightCylinderRadii)
    {
        bool same = _closed is not null && fieldG == _fieldKey.Field
                    && Math.Abs(Math.Log(lightCylinderRadii / _fieldKey.LightCylinderRadii)) < 0.02;
        if (same) return;
        _closed?.Dispose();
        _open?.Dispose();
        var set = FieldLines.For(1, fieldG, lightCylinderRadii);
        _closed = _lines.Upload(set.Closed);
        _open = _lines.Upload(set.Open);
        _fieldKey = (fieldG, lightCylinderRadii);
    }

    public void Dispose()
    {
        _closed?.Dispose();
        _open?.Dispose();
        _circle.Dispose();
        _arrow.Dispose();
        _lines.Dispose();
        _cones.Dispose();
    }
}
