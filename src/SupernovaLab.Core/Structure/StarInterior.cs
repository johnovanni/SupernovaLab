using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Structure;

/// <summary>
/// Inside the star before breakout (structure §S5 and §S6): the radius of the layers the cutaway shows as nested discs, and
/// where the shock is on its way to the surface.
/// </summary>
public static class StarInterior
{
    /// <summary>
    /// [APPROX] The carbon-oxygen core's outer radius, cm: 4.2e9, the geometric mean of the three verified blue-supergiant
    /// values (3.55, 3.5 and 6.1e9 cm: Kifonidis et al. 2003, Ono et al. 2020), used for every class (§S6). No red-supergiant value exists.
    /// </summary>
    public const double CoCoreRadiusCm = 4.2e9;

    /// <summary>
    /// [APPROX] The helium core's outer radius for a hydrogen-rich star, cm: 3.6e10, the geometric mean of the verified
    /// 1.8–7.1e10 cm range, equal to Lovegrove &amp; Woosley 2013's 15 M☉ red supergiant (§S6). The lab has no per-star value.
    /// </summary>
    public const double HeCoreRadiusCm = 3.6e10;

    private static readonly ShockBreakoutModel Breakout = new();

    // §S5's table: R_s / R* at t / t_bo = 0, 0.05, 0.1, 0.2, ..., 0.9, 0.95, 1 (Kistler et al. 2013 eq. 4 on Lane–Emden polytropes
    // with the neutron star's mass mapped to the centre), for n = 3/2 (a convective, red-supergiant envelope) and n = 3.
    private static readonly double[] Tau = { 0, 0.05, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 0.95, 1 };
    private static readonly double[] FractionN15 = { 0, 0.110, 0.180, 0.291, 0.385, 0.470, 0.551, 0.630, 0.709, 0.790, 0.879, 0.930, 1 };
    private static readonly double[] FractionN3 = { 0, 0.079, 0.129, 0.212, 0.287, 0.360, 0.433, 0.510, 0.593, 0.686, 0.798, 0.870, 1 };

    /// <summary>
    /// The layers of the presupernova star, centre outward, with each one's outer radius in cm (§S6): carbon-oxygen core
    /// (<see cref="CoCoreRadiusCm"/>, at most half the star), helium (<see cref="HeCoreRadiusCm"/>, at most the star; the whole
    /// star when stripped), and for a hydrogen-rich star the hydrogen envelope out to R*. Empty without a star or for a
    /// thermonuclear explosion. The iron core is far below a pixel and is not listed.
    /// </summary>
    public static IReadOnlyList<(ElementLayer Layer, double OuterRadiusCm)> Layers(Outcome o)
    {
        ArgumentNullException.ThrowIfNull(o);
        double r = o.Star?.RadiusCm.Value ?? double.NaN;
        if (!(r > 0) || !double.IsFinite(r) || EjectaStructureModel.ClassOf(o) == EjectaClass.TypeIa)
            return Array.Empty<(ElementLayer, double)>();
        double co = Math.Min(CoCoreRadiusCm, 0.5 * r);
        if (!ShockBreakoutModel.IsHydrogenRich(o))
            return new[] { (ElementLayer.CarbonOxygenCore, co), (ElementLayer.Helium, r) };
        return new[]
        {
            (ElementLayer.CarbonOxygenCore, co),
            (ElementLayer.Helium, Math.Min(HeCoreRadiusCm, r)),
            (ElementLayer.Hydrogen, r),
        };
    }

    /// <summary>
    /// The shock's radius, cm, <paramref name="sourceTimeS"/> after bounce (§S5): R* · F_n(t / t_bo) by linear interpolation in
    /// the table, with n chosen as <see cref="ShockBreakoutModel"/> chooses it (3/2 for a hydrogen-rich star of at least 158 R☉,
    /// else 3) and t_bo = <see cref="ShockBreakoutModel.ShockAtSurfaceS"/>. 0 at bounce, R* at t_bo, NaN after (and NaN when
    /// the model does not apply: no exploding envelope).
    /// </summary>
    public static double ShockRadiusCm(Outcome o, double sourceTimeS)
    {
        ArgumentNullException.ThrowIfNull(o);
        double tbo = Breakout.ShockAtSurfaceS(o), r = o.Star?.RadiusCm.Value ?? double.NaN;
        if (!(tbo > 0) || !double.IsFinite(tbo) || !(r > 0) || double.IsNaN(sourceTimeS) || sourceTimeS > tbo * (1 + 1e-12)) return double.NaN;
        if (sourceTimeS <= 0) return 0;
        double tau = Math.Min(1, sourceTimeS / tbo);   // a rounding error past t_bo is still t_bo
        bool convective = ShockBreakoutModel.IsHydrogenRich(o) && r / PhysicalConstants.RSun >= ShockBreakoutModel.ConvectiveMinRadiusRsun;
        var f = convective ? FractionN15 : FractionN3;
        int k = 1;
        while (k < Tau.Length - 1 && tau > Tau[k]) k++;
        double w = (tau - Tau[k - 1]) / (Tau[k] - Tau[k - 1]);
        return r * (f[k - 1] + w * (f[k] - f[k - 1]));
    }
}
