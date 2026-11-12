using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>
/// The size and temperature of the expanding debris, for the 3D view (spec §6). Source time throughout, from core bounce
/// (ignition for a Type Ia).
/// <list type="bullet">
/// <item>Speed (signals §0.4): v_ej = √(10E / (3M_ej)), the outer edge of a uniform-density sphere expanding
/// homologously (its kinetic energy is (3/10) M v_ej²). Signals §0.4 states no further outer-layer factor, so none is
/// applied: the real outermost layers move faster than this.</item>
/// <item>Radius: the star's radius (a preset's pinned radius, which the engine puts on the star) until the shock breaks
/// out; then free expansion from the surface, R* + v_ej (t − t_bo), which is continuous at breakout; then the young
/// remnant's blast wave from <see cref="RemnantEvolutionModel"/> (Leahy &amp; Williams' interpolation, signals §7.2).
/// See <see cref="RadiusCm"/> for the hand-off.</item>
/// <item>Temperature: the Stefan–Boltzmann temperature of a sphere of that radius emitting the light curve's
/// bolometric luminosity, never below 3,000 K.</item>
/// </list>
/// </summary>
public static class EjectaKinematics
{
    private const double MSun = PhysicalConstants.MSun, Year = PhysicalConstants.Year;

    /// <summary>
    /// [APPROX] The hand-off to the remnant model is a geometric blend over this window: free expansion (or the blast
    /// wave where it is smaller) below 3 yr, the remnant model's radius from its start at 10 yr (signals §11).
    /// </summary>
    public const double BlendStartS = 3 * Year;

    /// <summary>[APPROX] The photosphere temperature's floor, K: hydrogen and metals recombine and the gas turns transparent below about this.</summary>
    public const double TemperatureFloorK = 3000;

    private static readonly ShockBreakoutModel Breakout = new();
    private static readonly LightCurveModel LightCurve = new();
    private static readonly RemnantEvolutionModel Remnant = new();

    /// <summary>
    /// v_ej = √(10E / (3M_ej)) in cm/s (signals §0.4: 1e51 erg and 3 M☉ give 7.5e8 cm/s). NaN when the outcome throws
    /// out no ejecta (no explosion energy or ejecta mass).
    /// </summary>
    public static double OuterVelocityCmS(Outcome o)
        => HasEjecta(o) ? Math.Sqrt(10 * o.ExplosionEnergyErg!.Value / (3 * o.EjectaMass!.Value * MSun)) : double.NaN;

    /// <summary>
    /// The radius of the outer edge, cm, at this source time.
    /// <list type="bullet">
    /// <item>No ejecta (the star has not died, a white dwarf with no explosion, a failed supernova, the pulses of
    /// pulsational pair instability): the star's radius at every time. NaN if there is no star.</item>
    /// <item>Before the breakout time t_bo (<see cref="ShockBreakoutModel.BreakoutTimeS"/>): the star's radius R*.
    /// Where the breakout itself happens in dense circumstellar gas and is not modeled, t_bo is when the shock reaches the
    /// star's surface (<see cref="ShockBreakoutModel.ShockAtSurfaceS"/>, the same Kistler et al. delay); t_bo = 0 for a
    /// Type Ia/Iax (no envelope).</item>
    /// <item>After it: R_free = R* + v_ej (t − t_bo).</item>
    /// <item>The remnant model's blast wave R_rem = R* + R_LW(t) (signals §7.2; it adds R*, at most ~1e14 cm against
    /// R_LW ≳ 1e17 cm at 10 yr, so that it starts at the star's surface). Before 3 yr the radius is min(R_free, R_rem):
    /// R_LW's free-expansion limit runs about 9% ahead of v_ej t (it is the forward shock, which leads the ejecta), so
    /// this is free expansion until the blast wave decelerates below it (t ≈ 0.15 t_ch, about 30–600 yr for ordinary
    /// explosions, a few years for 0.1 M☉ at 1e52 erg). From 3 to 10 yr it blends geometrically into R_rem with a
    /// smoothstep in log t [APPROX], and from 10 yr (the remnant model's start) it is R_rem, so the 3D view and the
    /// remnant panel agree. Every piece is continuous, so the radius never jumps.</item>
    /// </list>
    /// </summary>
    public static double RadiusCm(Outcome o, double sourceTimeS)
    {
        if (o.Star is not { } star) return double.NaN;
        double r0 = star.RadiusCm.Value;
        if (!HasEjecta(o)) return r0;
        double tbo = BreakoutTimeS(o);
        if (!(sourceTimeS > tbo)) return r0;

        double free = r0 + OuterVelocityCmS(o) * (sourceTimeS - tbo);
        double remnant = r0 + RemnantEvolutionModel.RadiusCm(o, sourceTimeS);
        double early = Math.Min(free, remnant);
        if (sourceTimeS <= BlendStartS) return early;
        if (sourceTimeS >= RemnantEvolutionModel.StartS) return remnant;
        double x = Math.Log(sourceTimeS / BlendStartS) / Math.Log(RemnantEvolutionModel.StartS / BlendStartS);
        double w = x * x * (3 - 2 * x);
        return Math.Exp((1 - w) * Math.Log(early) + w * Math.Log(remnant));
    }

    /// <summary>
    /// The temperature (K) of a blackbody sphere with the current radius (<see cref="RadiusCm"/>) that emits the
    /// bolometric luminosity: T = (L / (4π σ R²))^(1/4), never below <see cref="TemperatureFloorK"/>. L is the light
    /// curve's (signals §4), or where the light curve has not started (the breakout flash), the breakout model's (§3).
    /// Before breakout, and for a star that does not explode, it is the star's surface temperature. NaN when there is no
    /// star, or no luminosity model at this time (after the light curve ends at 3 yr).
    /// <para>[APPROX] The outer edge is not the photosphere, so this is not the photosphere's temperature: early on the
    /// real photosphere lies outside this uniform-sphere edge (the fastest layers outrun v_ej), which makes this too hot;
    /// later the photosphere recedes inside the thinning ejecta, which makes it too cold. The 3D view colours the ejecta
    /// with the light curve's colour temperature instead and uses this only for an outcome with no light curve.</para>
    /// </summary>
    public static double PhotosphereTemperatureK(Outcome o, double sourceTimeS)
    {
        if (o.Star is not { } star) return double.NaN;
        if (!HasEjecta(o) || !(sourceTimeS > BreakoutTimeS(o))) return star.SurfaceTemperatureK.Value;

        double l = LightCurve.Sample(o, ObserverSetup.Default, sourceTimeS, "lbol");
        if (!(l > 0)) l = Breakout.Sample(o, ObserverSetup.Default, sourceTimeS, "lum");
        if (!(l > 0)) return double.NaN;
        double r = RadiusCm(o, sourceTimeS);
        return Math.Max(TemperatureFloorK, Math.Pow(l / (4 * Math.PI * PhysicalConstants.StefanBoltzmann * r * r), 0.25));
    }

    /// <summary>
    /// When the ejecta leave the star, s after bounce: the breakout time; where the breakout happens in dense gas and is
    /// not modeled, when the shock reaches the star's surface; 0 for a thermonuclear explosion. NaN when there are no ejecta.
    /// </summary>
    public static double BreakoutTimeS(Outcome o)
    {
        if (!HasEjecta(o)) return double.NaN;
        double tbo = Breakout.BreakoutTimeS(o);
        if (!double.IsFinite(tbo)) tbo = Breakout.ShockAtSurfaceS(o);
        return double.IsFinite(tbo) ? tbo : 0;
    }

    /// <summary>
    /// An explosion that throws out ejecta (a supernova or pair-instability supernova with E and M_ej): exactly the
    /// outcomes <see cref="RemnantEvolutionModel"/> applies to, so the radius hand-off always has its blast wave.
    /// </summary>
    public static bool HasEjecta(Outcome o) => Remnant.Applies(o);
}
