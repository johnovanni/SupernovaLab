using System.Globalization;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Compact;

/// <summary>
/// The close-up's magnetosphere geometry (spec §4.3; compact §C4; rulings P3-3, R-P3-4). The spin axis is scene +z; Earth
/// lies at angle ζ from it, at azimuth −y. Geometry is fixed per outcome: the beam half-angle uses the birth period (the
/// widest the beam ever is, since ρ ∝ P^(−1/2) only narrows), so a beam placed outside the cone stays outside.
/// </summary>
public static class Magnetosphere
{
    /// <summary>The drawn spin never goes faster than one turn per 2 s, so the eye can follow it.</summary>
    public const double MinDrawnPeriodS = 2;

    /// <summary>Project 2's schematic tilt between the magnetic and spin axes, deg (spec §4.3).</summary>
    public const double DefaultTiltDeg = 45;

    private const double BeamCoefficientDeg = 5.6;            // §C4 App: Biggs 1990, ρ₁₀ = 5.6° P^(−1/2) (#59)
    private const double OutsideConeMarginDeg = 10;           // spec §4.3: |ζ − α| > ρ + 10°
    private const double JetOffAxisRad = Math.PI / 2;         // a burst's jets, checkbox off: Earth far off the jet axis
    private const double JetOnAxisToleranceRad = 1e-9;
    private const double FormationTimeS = 30;                 // §C4 App [APPROX]: range 10–100 s

    /// <summary>The period the close-up draws: max(real, 2 s).</summary>
    public static double DrawnPeriodS(double realPeriodS) => Math.Max(realPeriodS, MinDrawnPeriodS);

    /// <summary>"Shown N× slower" (N to two significant figures) when the drawing is slowed, else "Shown at real speed".</summary>
    public static string SpeedLabel(double realPeriodS)
    {
        if (!(realPeriodS < MinDrawnPeriodS) || !(realPeriodS > 0)) return "Shown at real speed";
        double n = MinDrawnPeriodS / realPeriodS;
        double scale = Math.Pow(10, Math.Floor(Math.Log10(n)) - 1);
        double rounded = Math.Round(n / scale, MidpointRounding.AwayFromZero) * scale;
        return $"Shown {rounded.ToString("N0", CultureInfo.InvariantCulture)}× slower";
    }

    /// <summary>The light cylinder, c P / 2π, in km (§C4 App: 1,593.6 km at 33.4 ms).</summary>
    public static double LightCylinderKm(double periodS) => PhysicalConstants.C * periodS / (2 * Math.PI) / 1e5;

    /// <summary>
    /// The beam's half-opening angle, min(5.6° P^(−1/2), 90°) (Biggs 1990; published fits 4.9°–6.3°; §C4 App, #59).
    /// A non-positive period gives the 90° cap.
    /// </summary>
    public static double BeamHalfAngleRad(double periodS)
        => periodS > 0 ? Math.Min(BeamCoefficientDeg * Math.Pow(periodS, -0.5), 90) * Math.PI / 180 : Math.PI / 2;

    /// <summary>α, the magnetic inclination: the preset's measured value (the Crab's 62°) or the schematic 45°.</summary>
    public static double TiltRad(Outcome o) => (o.MagneticInclinationDeg ?? DefaultTiltDeg) * Math.PI / 180;

    /// <summary>
    /// ζ, the angle from the spin axis to Earth (spec §4.3). Checkbox on: α for a pulsar, 0 for a burst's jets. Off: α + ρ +
    /// 10° for a pulsar (reflected if it exceeds π), 90° for jets.
    /// </summary>
    public static double EarthAngleRad(Outcome o, ObserverSetup obs)
    {
        if (!IsPulsar(o)) return obs.BeamTowardEarth ? 0 : JetOffAxisRad;
        double alpha = TiltRad(o);
        if (obs.BeamTowardEarth) return alpha;
        double zeta = alpha + BeamHalfAngleRad(BirthPeriodS(o)) + OutsideConeMarginDeg * Math.PI / 180;
        return zeta > Math.PI ? 2 * Math.PI - zeta : zeta;
    }

    /// <summary>The unit vector to Earth: (0, −sin ζ, cos ζ) (ruling P3-3).</summary>
    public static Vec3 EarthDirection(Outcome o, ObserverSetup obs)
    {
        double zeta = EarthAngleRad(o, obs);
        return new Vec3(0, -Math.Sin(zeta), Math.Cos(zeta));
    }

    /// <summary>Whether a beam can sweep Earth: |ζ − α| ≤ ρ for a pulsar, ζ = 0 for a burst's jets.</summary>
    public static bool BeamCanCrossEarth(Outcome o, ObserverSetup obs)
    {
        double zeta = EarthAngleRad(o, obs);
        if (!IsPulsar(o)) return zeta <= JetOnAxisToleranceRad;
        return Math.Abs(zeta - TiltRad(o)) <= BeamHalfAngleRad(BirthPeriodS(o));
    }

    /// <summary>When the force-free magnetosphere forms, s after bounce: 30 s [APPROX] (§C4 App). NaN without a neutron star.</summary>
    public static double MagnetosphereFormsS(Outcome o) => NeutronStarModel.HasNeutronStar(o) ? FormationTimeS : double.NaN;

    private static bool IsPulsar(Outcome o) => o.Remnant.Kind is RemnantKind.NeutronStar or RemnantKind.Magnetar;

    /// <summary>The birth period, s; 1 s (ρ = 5.6°) if the remnant has none.</summary>
    private static double BirthPeriodS(Outcome o) => o.Remnant.SpinPeriodS is { Value: > 0 } p ? p.Value : 1;
}
