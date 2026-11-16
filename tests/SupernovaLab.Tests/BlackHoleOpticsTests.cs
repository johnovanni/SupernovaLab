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

    // ---- Task 4: ruling R-P3-6 (far cameras) and the Task 3 review's budget cases ----

    /// <summary>
    /// Ruling R-P3-6: a camera at 700 GM/c² (beyond 200) has its rays advanced along their straight lines to the sphere
    /// r = 200 and traced from there with R_esc = 200, inside the 400-step budget. The edge of the Captured region, as an
    /// impact parameter b = 700 sin θ, is √27 within 0.5%.
    /// </summary>
    [Fact]
    public void FarCamera_At700_ShadowEdgeIsRoot27()
    {
        const double d = 700;
        double lo = 0, hi = 0.02; // θ from the direction of the hole
        Assert.Equal(RayFate.Captured, TraceAt(lo).Fate);
        Assert.NotEqual(RayFate.Captured, TraceAt(hi).Fate);
        for (int k = 0; k < 30; k++)
        {
            double mid = 0.5 * (lo + hi);
            if (TraceAt(mid).Fate == RayFate.Captured) lo = mid; else hi = mid;
        }
        double b = d * Math.Sin(lo);
        Assert.InRange(b, C7.ShadowRadius * (1 - C7.ShadowFractionalTolerance), C7.ShadowRadius * (1 + C7.ShadowFractionalTolerance));
        Assert.True(TraceAt(1.2 * lo).Steps < BlackHoleOptics.MaxSteps, "a ray just outside the edge fits the 400-step budget");

        static RayResult TraceAt(double theta)
            => BlackHoleOptics.TraceRay(0, new(d, 0, 0), new(-Math.Cos(theta), Math.Sin(theta), 0), 1.2 * d);
    }

    /// <summary>
    /// Ruling R-P3-6: a far ray whose line misses the r = 200 sphere escapes at once with the weak-field deflection toward
    /// the hole, (2/b)(1 − cos γ) for a half-line leaving the camera at angle γ from its position vector: 4/b for a line
    /// passing the hole from far away, nothing for one pointing straight out.
    /// </summary>
    [Fact]
    public void FarCamera_LineMissingTheSphere_GetsTheWeakFieldDeflection()
    {
        // From 10⁶ GM/c², a line passing the hole at b = 1,000: γ ≈ π, so the deflection is 4/b = 0.004 rad (within 1%).
        const double d = 1e6, b = 1000;
        var dir = new Vec3(-d, b, 0).Normalized();
        var r = BlackHoleOptics.TraceRay(0.5, new(d, 0, 0), dir, 1.2 * d);
        Assert.Equal(RayFate.Escaped, r.Fate);
        Assert.Equal(0, r.Steps);
        double bLine = d * b / Math.Sqrt(d * d + b * b);
        Assert.Equal(4 / bLine, Angle(dir, r.ExitDirection), 4 / bLine * 0.01);
        Assert.True(r.ExitDirection.Y < dir.Y, "bent toward the hole (−y side of the line)");
        // Pointing straight away: no deflection, and never NaN (b = 0).
        var away = BlackHoleOptics.TraceRay(0.5, new(d, 0, 0), new(1, 0, 0), 1.2 * d);
        Assert.Equal(RayFate.Escaped, away.Fate);
        Assert.Equal(1, away.ExitDirection.X, 12);
    }

    /// <summary>A far ray that does reach the sphere starts on it (r just under 200) and is traced with R_esc = 200.</summary>
    [Fact]
    public void FarCamera_RayReachingTheSphere_IsTracedFromIt()
    {
        var r = BlackHoleOptics.TraceRay(0, new(1000, 0, 0), new Vec3(-1, 0.02, 0).Normalized(), 1200);
        Assert.Equal(RayFate.Escaped, r.Fate);
        Assert.True(r.Steps > 100, $"traced through the sphere ({r.Steps} steps)");
        Assert.InRange(r.ExitPosition.Length, BlackHoleOptics.FarCameraRadius, BlackHoleOptics.FarCameraRadius + 2.5);
    }

    /// <summary>Task 3 review: the NeverNaN cases for a far camera (a = 0.5, camera at 1,000, R_esc 1,200).</summary>
    [Fact]
    public void Tracer_NeverNaN_FarCamera()
    {
        foreach (var dir in GridDirections(64))
        {
            var r = BlackHoleOptics.TraceRay(0.5, new(1000, 0, 0.1), dir, 1200);
            Assert.True(r.Steps <= BlackHoleOptics.MaxSteps);
            Assert.True(double.IsFinite(r.ExitDirection.Length) && double.IsFinite(r.ExitPosition.Length));
            Assert.NotEqual(RayFate.StepLimit, r.Fate);
        }
    }

    /// <summary>
    /// Task 3 review: Kerr at the shader's own 400-step budget (a = 0.9, seen edge-on from a camera at 50, R_esc 60).
    /// Along 24 lines through Bardeen's outline, parallel rays at 0.9 of the outline radius are Captured and at 1.1 are
    /// not; StepLimit rays (ruling R-P3-7: drawn as the photon ring) sit only in a narrow band at the edge.
    /// </summary>
    [Fact]
    public void KerrShadow_AtTheShaderBudget_FatesAreSensible_StepLimitBandNarrow()
    {
        const double a = 0.9, cam = 50, inc = Math.PI / 2;
        Vec3 n = Normal(inc);
        var outline = BlackHoleOptics.KerrShadowOutline(a, inc, 24);
        int stepLimited = 0, samples = 0;
        foreach (var (alpha, beta) in outline)
        {
            RayResult At(double s) => BlackHoleOptics.TraceRay(a, cam * n + s * alpha * EAlpha + s * beta * EBeta(inc), -n, 60);
            Assert.Equal(RayFate.Captured, At(0.9).Fate);
            Assert.Equal(RayFate.Escaped, At(1.1).Fate);
            for (int k = 0; k <= 100; k++, samples++)
                if (At(0.9 + 0.2 * k / 100).Fate == RayFate.StepLimit) stepLimited++;
        }
        // Band width as a fraction of the outline radius: (StepLimit samples / samples) × 0.2.
        double band = 0.2 * stepLimited / samples;
        Assert.True(band < 0.01, $"StepLimit band {band:P2} of the shadow radius ({stepLimited} of {samples} samples)");
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

    // ---- Plan Task 10: the exact shadow edge seen from the Close-up's camera (the overlay, ruling P3-9) ----

    /// <summary>
    /// Plan Task 10: the overlay's shadow edge is exact for a camera at a finite distance. Each direction from
    /// <see cref="BlackHoleOptics.ShadowEdgeDirections"/> is checked against the tracer itself: along the great circle from the
    /// hole's direction through it, the tracer's Captured/escaped boundary (bisection, with a step budget large enough that no
    /// ray runs out) lies at the same angle, within 0.5%. Close cameras (12–60 GM/c²), spins 0 to 0.998, several inclinations.
    /// </summary>
    [Theory]
    [InlineData(0, 90, 20)]
    [InlineData(0.9, 90, 20)]
    [InlineData(0.9, 60, 20)]
    [InlineData(0.9, 120, 20)]
    [InlineData(0.9, 90, 60)]
    [InlineData(0.5, 30, 12)]
    [InlineData(0.95, 75, 25)]
    public void ShadowEdgeDirections_MatchTheTracersEdge_FromACloseCamera(double a, double incDeg, double r)
        => AssertEdgeMatchesTracer(a, incDeg, r, 0.005);

    /// <summary>
    /// At spin 0.998 the prograde photon orbit (r_ph+ = 1.0744) sits at the tracer's capture radius r₊ + 0.01 = 1.0732 (ruling P3-7), so
    /// the tracer swallows rays that would still escape and its shadow is about 1.2% too wide on the flattened side (inconsistency
    /// #64). The overlay keeps the exact edge; the tracer agrees within 2% there.
    /// </summary>
    [Fact]
    public void ShadowEdgeDirections_AtSpin0998_TheTracersCaptureMarginWidensItsEdgeSlightly()
        => AssertEdgeMatchesTracer(0.998, 90, 20, 0.02);

    private static void AssertEdgeMatchesTracer(double a, double incDeg, double r, double tolerance)
    {
        double inc = incDeg * Math.PI / 180;
        var cam = r * new Vec3(Math.Sin(inc) * Math.Cos(0.7), Math.Sin(inc) * Math.Sin(0.7), Math.Cos(inc));
        var dirs = BlackHoleOptics.ShadowEdgeDirections(a, cam, 36);
        Assert.Equal(36, dirs.Count);
        Vec3 c = -cam.Normalized();
        foreach (var d in dirs)
        {
            Assert.Equal(1, d.Length, 9);
            double psi = Angle(c, d);
            Vec3 u = (d - d.Dot(c) * c).Normalized();
            bool Captured(double ang) => BlackHoleOptics.Trace(a, cam, Math.Cos(ang) * c + Math.Sin(ang) * u,
                BlackHoleOptics.EscapeRadiusFor(r), 0, 20_000).Fate == RayFate.Captured;
            double lo = 0.7 * psi, hi = 1.3 * psi;
            Assert.True(Captured(lo), $"a={a} i={incDeg} r={r}: {0.7 * psi * 180 / Math.PI:F2}° should be captured");
            Assert.False(Captured(hi));
            for (int k = 0; k < 22; k++)
            {
                double mid = 0.5 * (lo + hi);
                if (Captured(mid)) lo = mid; else hi = mid;
            }
            double edge = 0.5 * (lo + hi);
            Assert.True(Math.Abs(edge - psi) < tolerance * psi,
                $"a={a} i={incDeg} r={r}: overlay {psi * 180 / Math.PI:F4}°, tracer {edge * 180 / Math.PI:F4}°");
        }
    }

    /// <summary>
    /// From far away the edge directions turn into Bardeen's outline (its α, β, point by point): the directions are the outline's own
    /// orbits seen from the camera, with α on e_α = (0, 1, 0) and β on e_β (the class notes' convention), within 1% of 5.2.
    /// </summary>
    [Theory, InlineData(0.9, 90), InlineData(0.9, 60), InlineData(0, 75), InlineData(0.5, 17)]
    public void ShadowEdgeDirections_FromFarAway_AreBardeensOutline(double a, double incDeg)
    {
        double inc = incDeg * Math.PI / 180;
        const double d = 1e5;
        var n = Normal(inc);
        var dirs = BlackHoleOptics.ShadowEdgeDirections(a, d * n, 72);
        var outline = BlackHoleOptics.KerrShadowOutline(a, inc, 72);
        for (int k = 0; k < 72; k++)
        {
            var v = dirs[k];
            double along = -v.Dot(n);
            double alpha = d * v.Dot(EAlpha) / along, beta = d * v.Dot(EBeta(inc)) / along;
            Assert.True(Math.Abs(alpha - outline[k].Alpha) < C7.OutlineTolerance && Math.Abs(beta - outline[k].Beta) < C7.OutlineTolerance,
                $"a={a} i={incDeg} k={k}: ({alpha:F4}, {beta:F4}) against Bardeen ({outline[k].Alpha:F4}, {outline[k].Beta:F4})");
        }
    }

    /// <summary>
    /// The overlay also matches the shader's own tracer (the public 400-step <see cref="BlackHoleOptics.TraceRay"/>, with ruling R-P3-6's
    /// far-camera start) out to the Close-up's farthest framing (677 GM/c²), within 0.1%. And inconsistency #65: the picture is a
    /// coordinate camera's, so from 20 GM/c² its shadow is 5.2% wider than a static observer's sin θ = √27 √(1 − 2/r)/r.
    /// </summary>
    [Theory, InlineData(19), InlineData(100), InlineData(300), InlineData(677)]
    public void ShadowEdgeDirections_MatchTheShadersTracer_NearAndFar(double r)
    {
        var cam = new Vec3(r, 0, 0);
        var d = BlackHoleOptics.ShadowEdgeDirections(0, cam, 8)[0];
        Vec3 c = -cam.Normalized(), u = (d - d.Dot(c) * c).Normalized();
        double psi = Angle(c, d), lo = 0.8 * psi, hi = 1.2 * psi;
        bool Captured(double ang) => BlackHoleOptics.TraceRay(0, cam, Math.Cos(ang) * c + Math.Sin(ang) * u,
            BlackHoleOptics.EscapeRadiusFor(r)).Fate == RayFate.Captured;
        Assert.True(Captured(lo));
        Assert.False(Captured(hi));
        for (int k = 0; k < 30; k++)
        {
            double mid = 0.5 * (lo + hi);
            if (Captured(mid)) lo = mid; else hi = mid;
        }
        Assert.Equal(psi, 0.5 * (lo + hi), psi * 1e-3);
        if (r == 19)
        {
            double staticObserver = Math.Asin(Math.Sqrt(27) * Math.Sqrt(1 - 2 / r) / r);
            Assert.InRange(psi / staticObserver, 1.050, 1.056);   // #65
        }
    }

    /// <summary>Review Focus 4: the edge directions stay finite near the camera's floor (3 GM/c²), on the axis and at spin 0.998.</summary>
    [Fact]
    public void ShadowEdgeDirections_StayFinite_InTheCornerCases()
    {
        foreach (double a in new[] { 0, 1e-5, 0.5, 0.9, 0.998 })
            foreach (var cam in new[] { new Vec3(3, 0, 0), new Vec3(0, 0.002, 20), new Vec3(0, 0, -20), new Vec3(150, 40, -30), new Vec3(600, 0, 1) })
            {
                var dirs = BlackHoleOptics.ShadowEdgeDirections(a, cam, 64);
                Assert.All(dirs, v => Assert.True(double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z) && Math.Abs(v.Length - 1) < 1e-9));
            }
        Assert.Empty(BlackHoleOptics.ShadowEdgeDirections(0.9, new Vec3(1, 0, 0), 64));   // inside the horizon: no outline
    }

    // ---- Plan Task 10: the grid sphere (ruling P3-8), mirrored by lensing.frag ----

    [Fact]
    public void Grid_RayCrossingOutward_RecordsWhereOnTheSphere()
    {
        Assert.Equal(50, BlackHoleOptics.GridRadius);
        Assert.Equal(10, BlackHoleOptics.GridStepDeg);
        // Straight away from a non-spinning hole: the crossing is on the camera's own line, at r = 50 (within one step's interpolation).
        var r = BlackHoleOptics.TraceRay(0, new Vec3(20, 0, 0), new Vec3(1, 0, 0), 60);
        Assert.Equal(RayFate.Escaped, r.Fate);
        Assert.NotNull(r.GridCrossing);
        Assert.Equal(50, r.GridCrossing!.Value.Length, 1);
        Assert.Equal(0, r.GridCrossing!.Value.Y, 6);
        // A ray that falls in never crosses outward; a camera outside the sphere looking away never crosses it either.
        Assert.Null(BlackHoleOptics.TraceRay(0, new Vec3(20, 0, 0), new Vec3(-1, 0, 0), 60).GridCrossing);
        Assert.Null(BlackHoleOptics.TraceRay(0, new Vec3(80, 0, 0), new Vec3(1, 0, 0), 96).GridCrossing);
        // From outside, a ray bent round the hole crosses outward on the far side.
        var behind = BlackHoleOptics.TraceRay(0, new Vec3(80, 0, 0), new Vec3(-1, 0.1, 0).Normalized(), 96);
        Assert.NotNull(behind.GridCrossing);
        Assert.True(behind.GridCrossing!.Value.X < 0);
        // The lines: every 10° of latitude and longitude, distances in degrees.
        var (lat, lon) = BlackHoleOptics.GridLineDistanceDeg(50 * new Vec3(Math.Cos(0.1), Math.Sin(0.1), 0));
        Assert.Equal(0, lat, 9);                                   // the equator is a latitude line
        Assert.Equal(10 - 0.1 * 180 / Math.PI, lon, 9);            // 5.73° of longitude: 4.27° from the 10° line
    }

    /// <summary>
    /// Spec §6.3: near a spinning hole the grid lines twist (frame dragging). Rays from a camera at 20 GM/c² passing either side of the
    /// hole in its equatorial plane reach the grid sphere at longitudes that, against the non-spinning hole's, are shifted on average
    /// in the spin's sense (13° here): the light passing against the spin is bent further round the hole than the light passing with it
    /// (the shadow's retrograde edge is at α = 6.8, its prograde edge at −2.8), so both sides' lines are carried the same way round.
    /// </summary>
    [Fact]
    public void Grid_FrameDragging_TwistsTheLines()
    {
        double Lon(double spin, double side)
        {
            var cam = new Vec3(20, 0, 0);
            var d = new Vec3(-1, side * 0.45, 0).Normalized();   // passes the hole at b ≈ 8.2, outside both edges (α −2.8 to 6.8)
            var res = BlackHoleOptics.TraceRay(spin, cam, d, 60);
            Assert.NotNull(res.GridCrossing);
            return Math.Atan2(res.GridCrossing!.Value.Y, res.GridCrossing!.Value.X);
        }
        double shift = 0.5 * (Math.IEEERemainder(Lon(0.9, 1) - Lon(0, 1), 2 * Math.PI) + Math.IEEERemainder(Lon(0.9, -1) - Lon(0, -1), 2 * Math.PI));
        Assert.True(shift > 0.05, $"mean longitude shift {shift * 180 / Math.PI:F2}°");
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
