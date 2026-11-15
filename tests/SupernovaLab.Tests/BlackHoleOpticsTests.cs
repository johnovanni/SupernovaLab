using SupernovaLab.Core.Compact;
using SupernovaLab.Core.Units;
using static SupernovaLab.Tests.CompactResearch;

namespace SupernovaLab.Tests;

/// <summary>
/// Black-hole optics and the ray tracer (plan Task 3, spec §6.6, criterion 2; values from compact.md §C7 and §C9).
/// <para>
/// The test helpers fire rays from far away (r = 1,000 GM/c², escape radius 1.2 × that, as ruling P3-7 sets R_esc)
/// with given screen coordinates and bisect between Captured and not Captured. Such a ray needs more than P3-7's 400
/// steps of at most 2.0 just to arrive, so the helpers call the same integrator through
/// <see cref="BlackHoleOptics.Trace"/> with a larger step budget (<see cref="FarSteps"/>); everything else (Hamiltonian,
/// RK4, step rule, fate tests) is the shader's. <see cref="SchwarzschildShadow_ShaderBudgetMatchesRoot27"/> checks the
/// shadow with the public 400-step <see cref="BlackHoleOptics.TraceRay"/> from a shader-like camera.
/// </para>
/// <para>
/// Screen convention (Bardeen's α, β): an observer at inclination i from the spin axis (+z) sits on
/// n = (sin i, 0, cos i). β runs along the projected spin axis, e_β = (−cos i, 0, sin i); α across it, e_α = (0, 1, 0).
/// A photon reaching the observer at (α, β) then has L_z = −α sin i, as Bardeen's α = −ξ / sin i requires. The rays are
/// parallel (direction −n), started at D n + α e_α + β e_β.
/// </para>
/// </summary>
public class BlackHoleOpticsTests
{
    private const double FarR = 1000;
    private const int FarSteps = 4000;
    private const double Mpc = PhysicalConstants.Mpc;
    private const double Kpc = PhysicalConstants.Kpc;

    // ---- Brief tests (plan Task 3 Step 1), with rulings R-P3-2 and S2 applied ----

    [Fact]
    public void SchwarzschildShadow_TracerMatchesRoot27()
    {
        double b = BisectCaptureEdge(spin: 0, cameraR: FarR); // largest captured impact parameter
        Assert.InRange(b, C7.ShadowRadius * (1 - C7.ShadowFractionalTolerance),
                       C7.ShadowRadius * (1 + C7.ShadowFractionalTolerance));
    }

    [Fact]
    public void TenSolarMasses_ShadowDiameter154Km()
        => Assert.InRange(2 * BlackHoleOptics.SchwarzschildShadowRadius * BlackHoleOptics.GravitationalRadiusCm(10) / 1e5,
                          C7.TenSolarMassShadowKmLow, C7.TenSolarMassShadowKmHigh);

    [Theory, InlineData(0.9, 90), InlineData(0.9, 17)]
    public void KerrShadow_TracerMatchesBardeen(double a, double incDeg) // criterion 2: 1% of the shadow radius
    {
        var outline = BlackHoleOptics.KerrShadowOutline(a, incDeg * Math.PI / 180, 72);
        Assert.Equal(72, outline.Count);
        foreach (var (alpha, beta) in outline)
        {
            double edge = EdgeAlongRay(a, incDeg, alpha, beta);
            Assert.True(Math.Abs(edge - Math.Sqrt(alpha * alpha + beta * beta)) < C7.OutlineTolerance,
                $"a={a} i={incDeg}: Bardeen ({alpha:F4}, {beta:F4}) radius {Math.Sqrt(alpha * alpha + beta * beta):F4}, tracer {edge:F4}");
        }
    }

    [Fact]
    public void Isco_KnownValues()
    {
        Assert.Equal(6, BlackHoleOptics.IscoRadius(0), 6);
        Assert.Equal(2.3209, BlackHoleOptics.IscoRadius(0.9), 3);
        Assert.Equal(1, BlackHoleOptics.IscoRadius(1), 3);
    }

    [Fact]
    public void Ergosphere_EquatorIs2_PoleIsHorizon()
    {
        Assert.Equal(2, BlackHoleOptics.ErgosphereRadius(0.9, Math.PI / 2), 9);
        Assert.Equal(BlackHoleOptics.HorizonRadius(0.9), BlackHoleOptics.ErgosphereRadius(0.9, 0), 9);
    }

    [Fact]
    public void WeakField_TracerMatches4OverB() // b = 1000: 0.004 rad within 1%
        => Assert.InRange(DeflectionAt(spin: 0, b: C7.WeakFieldImpactParameter),
                          C7.WeakFieldDeflectionRad * (1 - C7.WeakFieldFractionalTolerance),
                          C7.WeakFieldDeflectionRad * (1 + C7.WeakFieldFractionalTolerance));

    [Fact]
    public void NeutronStar_1p4_12km_VisibleFractionNear0p76()
    {
        double f = BlackHoleOptics.VisibleSurfaceFraction(1.4, 12);
        Assert.InRange(f, C7.VisibleFraction14Low, C7.VisibleFraction14High);
        Assert.Equal(f, TracerVisibleFraction(1.4, 12), 2); // tracer agrees with the quadrature
    }

    /// <summary>
    /// §C9 App (ruling R-P3-2): the independent M87 test. The ring from the stellar-dynamics mass, 6.14 × 10⁹ M☉ at
    /// 16.8 Mpc, times RingToShadow = 1.06 is 39.7 µas, within EHT's 42 ± 3 µas.
    /// </summary>
    [Fact]
    public void M87_ShadowLawWithinEhtTolerance()
    {
        double ring = MicroArcsec(BlackHoleOptics.ApparentShadowDiameterRad(C9.M87StellarDynamicsMassMsun,
                                                                             C9.M87DistanceMpc * Mpc)) * C9.RingToShadow;
        Assert.Equal(C9.M87RingFromStellarMassMicroArcsec, ring, 0.1);
        Assert.InRange(ring, C9.M87RingMicroArcsec - C9.TolM87, C9.M87RingMicroArcsec + C9.TolM87);
    }

    /// <summary>
    /// §C9 App (ruling R-P3-2; inconsistency #55): Sgr A*'s shadow from GRAVITY's mass and distance (4.297 × 10⁶ M☉,
    /// 8.277 kpc) is 53.27 µas. It must lie within the EHT's inferred shadow 6√3 θ_g (1 + δ), δ = −0.08 ± 0.09 (VLTI),
    /// i.e. 44.2–53.8 µas. The ring is deliberately not tested against 51.8 ± 2.3 µas with M87's factor (it fails by 2σ).
    /// </summary>
    [Fact]
    public void SgrA_ShadowLawWithinEhtTolerance()
    {
        double shadow = MicroArcsec(BlackHoleOptics.ApparentShadowDiameterRad(C9.SgrAMassMsun, C9.SgrADistanceKpc * Kpc));
        Assert.Equal(C9.SgrAShadowMicroArcsec, shadow, 0.01);
        double sch = 2 * Math.Sqrt(27) * C9.SgrAThetaGVltiMicroArcsec; // 6√3 θ_g
        double low = sch * (1 + C9.SgrADeltaVlti - C9.SgrADeltaVltiSigma);
        double high = sch * (1 + C9.SgrADeltaVlti + C9.SgrADeltaVltiSigma);
        Assert.Equal(C9.SgrAShadowWindowLow, low, 0.05);
        Assert.Equal(C9.SgrAShadowWindowHigh, high, 0.05);
        Assert.InRange(shadow, low, high);
    }

    [Theory, InlineData(0.998, 3.0), InlineData(0, 1.2), InlineData(0.5, 1e6)] // Review Focus 4
    public void Tracer_NeverNaN_AlwaysTerminates(double a, double camR)
    {
        foreach (var dir in GridDirections(64))
        {
            var r = BlackHoleOptics.TraceRay(a, new(camR, 0, 0.1), dir, 60);
            Assert.True(r.Steps <= BlackHoleOptics.MaxSteps);
            Assert.True(double.IsFinite(r.ExitDirection.X) && double.IsFinite(r.ExitDirection.Y)
                        && double.IsFinite(r.ExitDirection.Z));
            Assert.True(double.IsFinite(r.ExitPosition.X) && double.IsFinite(r.ExitPosition.Y)
                        && double.IsFinite(r.ExitPosition.Z));
        }
    }

    // ---- Further §C7 / §C9 checks ----

    [Fact]
    public void Constants_AndGravitationalRadiusFromPhysicalConstants()
    {
        double shadow = BlackHoleOptics.SchwarzschildShadowRadius;
        Assert.Equal(Math.Sqrt(27), shadow, 15);
        Assert.Equal(C7.PhotonSphere, BlackHoleOptics.PhotonSphereRadius);
        Assert.Equal(PhysicalConstants.G * PhysicalConstants.MSun / (PhysicalConstants.C * PhysicalConstants.C),
                     BlackHoleOptics.GravitationalRadiusCm(1), 6);
        Assert.Equal(C7.GravitationalRadiusKmPerMsun, BlackHoleOptics.GravitationalRadiusCm(1) / 1e5, 5);
        Assert.Equal(C7.TenSolarMassShadowKm,
                     2 * BlackHoleOptics.SchwarzschildShadowRadius * BlackHoleOptics.GravitationalRadiusCm(10) / 1e5, 2);
    }

    [Fact]
    public void Isco_AndHorizon_MatchC7Table()
    {
        for (int k = 0; k < C7.IscoSpins.Length; k++)
        {
            double a = C7.IscoSpins[k];
            Assert.InRange(BlackHoleOptics.IscoRadius(a), C7.IscoPrograde[k] - C7.TableTolerance, C7.IscoPrograde[k] + C7.TableTolerance);
            Assert.InRange(BlackHoleOptics.IscoRadius(a, prograde: false),
                           C7.IscoRetrograde[k] - C7.TableTolerance, C7.IscoRetrograde[k] + C7.TableTolerance);
            Assert.InRange(BlackHoleOptics.HorizonRadius(a), C7.Horizon[k] - C7.TableTolerance, C7.Horizon[k] + C7.TableTolerance);
        }
    }

    [Fact]
    public void KerrShadowOutline_MatchesC7CheckValues()
    {
        foreach (var (incDeg, alphaMin, alphaMax, betaMax) in C7.OutlineRows)
        {
            var o = BlackHoleOptics.KerrShadowOutline(C7.OutlineSpin, incDeg * Math.PI / 180, 2000);
            Assert.Equal(alphaMin, o.Min(p => p.Alpha), C7.OutlineTableTolerance);
            Assert.Equal(alphaMax, o.Max(p => p.Alpha), C7.OutlineTableTolerance);
            Assert.Equal(betaMax, o.Max(p => p.Beta), C7.OutlineTableTolerance);
            Assert.Equal(-betaMax, o.Min(p => p.Beta), C7.OutlineTableTolerance);
        }
        var o90 = BlackHoleOptics.KerrShadowOutline(C7.OutlineSpin, Math.PI / 2, 720);
        foreach (var (alpha, beta) in C7.OutlineSamples90)
            Assert.True(DistanceToPolygon(o90, alpha, beta) < 0.001, $"({alpha}, {beta}) is not on the outline");
    }

    [Theory, InlineData(0, 90), InlineData(5e-5, 60), InlineData(0.9, 0)]
    public void KerrShadowOutline_DegenerateCasesAreCircles(double a, double incDeg)
    {
        var o = BlackHoleOptics.KerrShadowOutline(a, incDeg * Math.PI / 180, 64);
        Assert.Equal(64, o.Count);
        double r0 = Math.Sqrt(o[0].Alpha * o[0].Alpha + o[0].Beta * o[0].Beta);
        foreach (var (alpha, beta) in o)
        {
            Assert.True(double.IsFinite(alpha) && double.IsFinite(beta));
            Assert.Equal(r0, Math.Sqrt(alpha * alpha + beta * beta), 6);
        }
        if (a < 1e-4) Assert.Equal(Math.Sqrt(27), r0, 9);
        else Assert.InRange(r0, C7.KerrShadowRadiusLow, C7.KerrShadowRadiusHigh); // pole-on Kerr: a circle, 4.8–5.2
    }

    [Theory, InlineData(1.2), InlineData(1.4), InlineData(2.2)]
    public void VisibleSurfaceFraction_MatchesC7Table(double mass)
    {
        double f = C7.VisibleFractions12Km.Single(e => e.MassMsun == mass).Fraction;
        Assert.InRange(BlackHoleOptics.VisibleSurfaceFraction(mass, 12), f - C7.TableTolerance, f + C7.TableTolerance);
    }

    [Fact]
    public void VisibleSurfaceFraction_InsidePhotonSphereIsWholeStar()
        => Assert.Equal(1, BlackHoleOptics.VisibleSurfaceFraction(2.2, 9.0)); // 1.5 r_g at 2.2 M☉ is 9.75 km

    [Fact] // Review Focus 4: a 2.2 M☉, 12 km star (photon sphere 9.75 km, inside the surface)
    public void NeutronStar_2p2_12km_TracerAgreesWithQuadrature()
        => Assert.Equal(BlackHoleOptics.VisibleSurfaceFraction(2.2, 12), TracerVisibleFraction(2.2, 12), 2);

    [Fact]
    public void ApparentShadow_TenSolarMassesAt10Kpc_FromEarth()
    {
        double nas = MicroArcsec(BlackHoleOptics.ApparentShadowDiameterRad(10, 10 * Kpc)) * 1e3;
        Assert.Equal(C9.TenSolarMassAt10KpcShadowNanoArcsec, nas, 3);
        Assert.InRange(C9.EhtBeamMicroArcsec * 1e3 / nas, C9.TenSolarMassTimesTooSmall * 0.99, C9.TenSolarMassTimesTooSmall * 1.01);
    }

    [Fact]
    public void ApparentShadow_SpinningIsSlightlySmallerWithinEhtRange()
    {
        double sch = BlackHoleOptics.ApparentShadowDiameterRad(1, 1);
        double kerr = BlackHoleOptics.ApparentShadowDiameterRad(1, 1, spin: 0.9);
        double gm = BlackHoleOptics.GravitationalRadiusCm(1);
        Assert.Equal(2 * Math.Sqrt(27) * gm, sch, 6);
        Assert.True(kerr < sch);
        Assert.InRange(kerr / (2 * gm), C7.KerrShadowRadiusLow, C7.KerrShadowRadiusHigh);
    }

    [Fact]
    public void WeakFieldDeflection_Is4OverB()
        => Assert.Equal(C7.WeakFieldDeflectionRad, BlackHoleOptics.WeakFieldDeflection(C7.WeakFieldImpactParameter), 12);

    /// <summary>
    /// The public tracer with P3-7's own budget (400 steps, step cap 2.0) from a shader-like camera (r = 50, R_esc =
    /// 60): the edge of the Captured region is √27 within 0.5%. Rays near the edge that loop round the photon sphere
    /// can run out of steps; they count as not captured, so this also bounds the StepLimit band's width.
    /// </summary>
    [Fact]
    public void SchwarzschildShadow_ShaderBudgetMatchesRoot27()
    {
        double lo = 0, hi = 10;
        for (int k = 0; k < 30; k++)
        {
            double mid = 0.5 * (lo + hi);
            var r = BlackHoleOptics.TraceRay(0, new(50, mid, 0), new(-1, 0, 0), 60);
            if (r.Fate == RayFate.Captured) lo = mid; else hi = mid;
        }
        Assert.InRange(lo, C7.ShadowRadius * (1 - C7.ShadowFractionalTolerance),
                       C7.ShadowRadius * (1 + C7.ShadowFractionalTolerance));
    }

    [Fact]
    public void Tracer_FatesForSimpleRays()
    {
        var head = BlackHoleOptics.TraceRay(0.9, new(50, 0, 0.5), new(-1, 0, 0), 60);
        Assert.Equal(RayFate.Captured, head.Fate);
        var away = BlackHoleOptics.TraceRay(0.9, new(50, 0, 0.5), new(1, 0, 0), 60);
        Assert.Equal(RayFate.Escaped, away.Fate);
        Assert.True(away.ExitPosition.Length > 60);
        Assert.Equal(1, away.ExitDirection.Length, 9);
        var star = BlackHoleOptics.TraceRay(0, new(50, 1, 0), new(-1, 0, 0), 60, surfaceRadius: 5.8);
        Assert.Equal(RayFate.HitSurface, star.Fate);
        Assert.True(star.ExitPosition.Length < 5.8);
    }

    [Fact]
    public void Tracer_KeepsTheNullCondition()
    {
        // The Hamiltonian H = ½ g^{μν} p_μ p_ν stays ~0 along an RK4 path that grazes the prograde photon orbit
        // (edge at α = −2.84 for a = 0.9 seen edge-on) and escapes, and along one that is captured.
        var pass = BlackHoleOptics.TraceRay(0.9, new(50, -3.0, 0.5), new(-1, 0, 0), 60);
        Assert.Equal(RayFate.Escaped, pass.Fate);
        var (h0, h1) = BlackHoleOptics.HamiltonianDrift(0.9, new(50, -3.0, 0.5), new(-1, 0, 0), pass.Steps);
        Assert.True(Math.Abs(h0) < 1e-12, $"H at the camera {h0}");
        Assert.True(Math.Abs(h1) < 1e-5, $"H after {pass.Steps} steps {h1}");
        var (c0, c1) = BlackHoleOptics.HamiltonianDrift(0.9, new(50, 2, 1), new(-1, 0, 0), BlackHoleOptics.MaxSteps);
        Assert.True(Math.Abs(c0) < 1e-12 && Math.Abs(c1) < 1e-5, $"captured ray: H {c0} → {c1}");
    }

    [Fact]
    public void Vec3_Operations()
    {
        Vec3 a = new(1, 2, 3), b = new(-2, 0.5, 4);
        Assert.Equal(new Vec3(-1, 2.5, 7), a + b);
        Assert.Equal(new Vec3(3, 1.5, -1), a - b);
        Assert.Equal(new Vec3(2, 4, 6), a * 2);
        Assert.Equal(new Vec3(2, 4, 6), 2 * a);
        Assert.Equal(new Vec3(-1, -2, -3), -a);
        Assert.Equal(11, a.Dot(b));
        Assert.Equal(new Vec3(6.5, -10, 4.5), a.Cross(b));
        Assert.Equal(Math.Sqrt(14), a.Length, 15);
        Assert.Equal(1, a.Normalized().Length, 15);
        Assert.Equal(new Vec3(0, 0, 0), new Vec3(0, 0, 0).Normalized());
    }

    // ---- Helpers ----

    private static double MicroArcsec(double rad) => rad * (180 / Math.PI) * 3600 * 1e6;

    private static Vec3 Normal(double incRad) => new(Math.Sin(incRad), 0, Math.Cos(incRad));
    private static Vec3 EAlpha => new(0, 1, 0);
    private static Vec3 EBeta(double incRad) => new(-Math.Cos(incRad), 0, Math.Sin(incRad));

    /// <summary>A parallel ray from a far camera at screen point (α, β), traced with a far-camera step budget.</summary>
    private static RayResult Fire(double spin, double incRad, double alpha, double beta, double d = FarR,
                                  double surfaceRadius = 0)
    {
        Vec3 n = Normal(incRad);
        Vec3 start = d * n + alpha * EAlpha + beta * EBeta(incRad);
        return BlackHoleOptics.Trace(spin, start, -n, 1.2 * d, surfaceRadius, FarSteps);
    }

    /// <summary>Largest captured impact parameter, by bisection in the equatorial plane (i = 90°).</summary>
    private static double BisectCaptureEdge(double spin, double cameraR)
    {
        double lo = 0, hi = 10;
        Assert.Equal(RayFate.Captured, Fire(spin, Math.PI / 2, lo, 0, cameraR).Fate);
        Assert.NotEqual(RayFate.Captured, Fire(spin, Math.PI / 2, hi, 0, cameraR).Fate);
        for (int k = 0; k < 24; k++)
        {
            double mid = 0.5 * (lo + hi);
            if (Fire(spin, Math.PI / 2, mid, 0, cameraR).Fate == RayFate.Captured) lo = mid; else hi = mid;
        }
        return 0.5 * (lo + hi);
    }

    /// <summary>The tracer's shadow edge along the screen ray from (0, 0) through (α, β): its distance from (0, 0).</summary>
    private static double EdgeAlongRay(double spin, double incDeg, double alpha, double beta)
    {
        double inc = incDeg * Math.PI / 180, rho = Math.Sqrt(alpha * alpha + beta * beta);
        double ua = alpha / rho, ub = beta / rho;
        double lo = 0.9 * rho, hi = 1.1 * rho;
        if (Fire(spin, inc, lo * ua, lo * ub).Fate != RayFate.Captured) return lo; // outside the bracket: fails the test
        if (Fire(spin, inc, hi * ua, hi * ub).Fate == RayFate.Captured) return hi;
        for (int k = 0; k < 9; k++)
        {
            double mid = 0.5 * (lo + hi);
            if (Fire(spin, inc, mid * ua, mid * ub).Fate == RayFate.Captured) lo = mid; else hi = mid;
        }
        return 0.5 * (lo + hi);
    }

    /// <summary>
    /// Total deflection of a ray with impact parameter b, fired from 10⁵ GM/c² (so the uncollected deflection beyond
    /// both ends, about 2b/D² of 4/b, is negligible) and escaping at 1.2 × 10⁵.
    /// </summary>
    private static double DeflectionAt(double spin, double b)
    {
        const double d = 1e5;
        var r = BlackHoleOptics.Trace(spin, new(d, b, 0), new(-1, 0, 0), 1.2 * d, 0, 200_000);
        Assert.Equal(RayFate.Escaped, r.Fate);
        return Angle(new Vec3(-1, 0, 0), r.ExitDirection);
    }

    /// <summary>
    /// The visible surface fraction found with the tracer: bisect the impact parameter between HitSurface and Escaped,
    /// then take the grazing ray just outside the edge. Its periapsis is at the surface, so the angle it sweeps from the
    /// observer's direction to the tangent point is half its total sweep: ψ_max = (π + deflection) / 2, and
    /// f = (1 − cos ψ_max) / 2.
    /// </summary>
    private static double TracerVisibleFraction(double massMsun, double radiusKm)
    {
        double rs = radiusKm * 1e5 / BlackHoleOptics.GravitationalRadiusCm(massMsun);
        double lo = 0, hi = 2 * rs;
        Assert.Equal(RayFate.HitSurface, Fire(0, Math.PI / 2, lo, 0, surfaceRadius: rs).Fate);
        Assert.Equal(RayFate.Escaped, Fire(0, Math.PI / 2, hi, 0, surfaceRadius: rs).Fate);
        for (int k = 0; k < 30; k++)
        {
            double mid = 0.5 * (lo + hi);
            if (Fire(0, Math.PI / 2, mid, 0, surfaceRadius: rs).Fate == RayFate.HitSurface) lo = mid; else hi = mid;
        }
        var grazing = Fire(0, Math.PI / 2, hi, 0, surfaceRadius: rs);
        Assert.Equal(RayFate.Escaped, grazing.Fate);
        double psiMax = 0.5 * (Math.PI + Angle(new Vec3(-1, 0, 0), grazing.ExitDirection));
        return psiMax >= Math.PI ? 1 : 0.5 * (1 - Math.Cos(psiMax));
    }

    private static double Angle(Vec3 u, Vec3 v) => Math.Atan2(u.Cross(v).Length, u.Dot(v));

    /// <summary>n directions spread evenly over the sphere (a Fibonacci lattice).</summary>
    private static IEnumerable<Vec3> GridDirections(int n)
    {
        double golden = Math.PI * (3 - Math.Sqrt(5));
        for (int k = 0; k < n; k++)
        {
            double z = 1 - (k + 0.5) * 2.0 / n, s = Math.Sqrt(1 - z * z);
            yield return new Vec3(s * Math.Cos(golden * k), s * Math.Sin(golden * k), z);
        }
    }

    private static double DistanceToPolygon(IReadOnlyList<(double Alpha, double Beta)> poly, double x, double y)
    {
        double best = double.MaxValue;
        for (int k = 0; k < poly.Count; k++)
        {
            var (ax, ay) = poly[k];
            var (bx, by) = poly[(k + 1) % poly.Count];
            double dx = bx - ax, dy = by - ay, len2 = dx * dx + dy * dy;
            double t = len2 > 0 ? Math.Clamp(((x - ax) * dx + (y - ay) * dy) / len2, 0, 1) : 0;
            double ex = ax + t * dx - x, ey = ay + t * dy - y;
            best = Math.Min(best, Math.Sqrt(ex * ex + ey * ey));
        }
        return best;
    }
}
