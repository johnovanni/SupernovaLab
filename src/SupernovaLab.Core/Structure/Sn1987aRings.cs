using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Structure;

/// <summary>One of SN 1987A's rings: a circle of this radius (cm) in the plane perpendicular to the symmetry axis, centred this far along it (cm; + toward the observer), the plane tilted this far (rad) from the line of sight.</summary>
public sealed record RingGeometry(double RadiusCm, double AxisOffsetCm, double InclinationRad, bool Equatorial);

/// <summary>
/// SN 1987A's triple-ring nebula (structure §S8 and its App block): observed geometry pinned by the preset
/// (<see cref="Outcomes.Outcome.HasEquatorialRing"/>), not predicted by the lab.
/// <list type="bullet">
/// <item>Radii and signed centre offsets are Sugerman et al. 2005's 3D fit (Table 3, at 50 kpc) scaled by D/50 kpc with
/// D = 51.4 kpc, the preset's distance [D]; the equatorial ring's 6.29 × 10¹⁷ cm (0.665 ly) is the App block's single value.
/// The rings are physical, so their size does not follow the observer's distance slider.</item>
/// <item>Sky coordinates are Sugerman's: x west, y north, z toward the observer. The symmetry axis's positive end points toward the
/// observer, 43.0° from the line of sight toward P.A. 171.1° (the equatorial ring's minor axis, 90° from its 81.1° major axis).
/// The north ring is on the far side of the equatorial plane (z₀ &lt; 0), the south ring on the near side (z₀ &gt; 0).</item>
/// <item>Schematic: all three are circles in planes perpendicular to the axis; in-plane ellipticities, each ring's own position angle,
/// the small centroid offsets (x₀, y₀ ≤ 0.26 ly) and Burrows' 0.4″ misalignment are not drawn (inconsistency #38).</item>
/// <item>Hotspots (<see cref="HotspotBrightness"/>): signals §6.3's ring X-ray rise (logistic over 3,000–9,000 d), its plateau, and
/// [APPROX] a fade from about 10,000 d to zero at 2035 (Larsson et al. 2019's projection for the R band).</item>
/// </list>
/// </summary>
public static class Sn1987aRings
{
    /// <summary>Sugerman's 3D fit is at 50 kpc; the preset's distance is 51.4 kpc.</summary>
    public const double FitDistanceKpc = 50, PresetDistanceKpc = 51.4;

    private const double LightYear = PhysicalConstants.LightYear;

    /// <summary>The App block's single equatorial-ring radius: 6.29 × 10¹⁷ cm = 0.665 ly at 51.4 kpc (not the 6.31 × 10¹⁷ cm cross-check).</summary>
    public const double EquatorialRadiusCm = 6.29e17;

    /// <summary>The axis' tilt from the line of sight and the P.A. (from north through east) it points toward: 43.0° and 171.1°.</summary>
    public const double AxisTiltDeg = 43.0, AxisPositionAngleDeg = 171.1;

    /// <summary>Hotspot beads on the equatorial ring in the 3D view (evenly spaced; 26 were counted by 2003).</summary>
    public const int HotspotCount = 24;

    /// <summary>The note shown while the rings are drawn (verbatim from the plan).</summary>
    public const string Note = "SN 1987A's rings: observed geometry (preset), not predicted by the lab";

    public static Source Source { get; } = new(
        "Sugerman et al. 2005, ApJS 159, 60 (Table 3, §5.3, §8.3), scaled to 51.4 kpc", "structure §S8");

    /// <summary>The three rings: equatorial, north (far side), south (near side).</summary>
    public static IReadOnlyList<RingGeometry> Rings { get; } = At(PresetDistanceKpc);

    /// <summary>Sugerman's Table 3 (50 kpc: a = 1.42 and 1.59 ly, z₀ = −1.36 and +1.00 ly) scaled to this distance.</summary>
    public static IReadOnlyList<RingGeometry> At(double distanceKpc)
    {
        double scale = distanceKpc / FitDistanceKpc, tilt = AxisTiltDeg * Math.PI / 180;
        return new[]
        {
            new RingGeometry(EquatorialRadiusCm * scale / (PresetDistanceKpc / FitDistanceKpc), 0, tilt, true),
            new RingGeometry(1.42 * scale * LightYear, -1.36 * scale * LightYear, tilt, false),
            new RingGeometry(1.59 * scale * LightYear, +1.00 * scale * LightYear, tilt, false),
        };
    }

    /// <summary>
    /// The symmetry axis' positive end in sky coordinates (west, north, toward the observer):
    /// sin 43° (−sin θ, cos θ, 0) + cos 43° (0, 0, 1), θ = 171.1° = (−0.1055, −0.6738, +0.7314).
    /// </summary>
    public static (double West, double North, double Toward) AxisSky { get; } = ComputeAxis();

    private static (double, double, double) ComputeAxis()
    {
        double tilt = AxisTiltDeg * Math.PI / 180, pa = AxisPositionAngleDeg * Math.PI / 180;
        return (Math.Sin(tilt) * -Math.Sin(pa), Math.Sin(tilt) * Math.Cos(pa), Math.Cos(tilt));
    }

    /// <summary>The ring's centre in sky coordinates (west, north, toward), cm: its signed offset along <see cref="AxisSky"/>.</summary>
    public static (double West, double North, double Toward) CentreSky(RingGeometry ring)
        => (ring.AxisOffsetCm * AxisSky.West, ring.AxisOffsetCm * AxisSky.North, ring.AxisOffsetCm * AxisSky.Toward);

    // ---------------------------------------------------------------- hotspots

    /// <summary>[APPROX] The hotspots start to fade at about 10,000 d (the App block) and are gone at 2035 (Larsson et al. 2019, R band).</summary>
    public const double FadeStartDays = 10_000;

    /// <summary>Day 17,477: 2035.0 is 47.85 yr after 1987 Feb 23.</summary>
    public const double FadeEndDays = 47.85 * 365.25;

    /// <summary>
    /// Hotspot brightness, 0 to 1, at source time t (s after core bounce): signals §6.3's ring component
    /// (<see cref="CsmInteractionModel.RingXrayLuminosityErgS"/>, zero before 3,000 d, logistic to the plateau) normalised to its peak,
    /// times [APPROX] a linear fade from <see cref="FadeStartDays"/> to <see cref="FadeEndDays"/> (the research gives no rate; §S8 has
    /// "fading since" the 2009 plateau and the R band reaching zero about 2035). It peaks at 1 at <see cref="FadeStartDays"/>.
    /// </summary>
    public static float HotspotBrightness(double sourceTimeS)
    {
        if (!(sourceTimeS > 0)) return 0f;
        double days = sourceTimeS / PhysicalConstants.Day;
        double rise = CsmInteractionModel.RingXrayLuminosityErgS(sourceTimeS) / Peak;
        double fade = days <= FadeStartDays ? 1 : Math.Clamp((FadeEndDays - days) / (FadeEndDays - FadeStartDays), 0, 1);
        return (float)Math.Clamp(rise * fade, 0, 1);
    }

    private static readonly double Peak = CsmInteractionModel.RingXrayLuminosityErgS(FadeStartDays * PhysicalConstants.Day);
}
