using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Compact;

/// <summary>What became of a ray traced back from the camera (<see cref="BlackHoleOptics.TraceRay"/>).</summary>
public enum RayFate
{
    /// <summary>Fell through the horizon: r &lt; r₊ + 0.01. Drawn black.</summary>
    Captured,
    /// <summary>Left the escape sphere: r &gt; R_esc. Sample the surroundings by the exit direction.</summary>
    Escaped,
    /// <summary>Reached the neutron star's surface: r &lt; surfaceRadius (spin-0 use only). Shade the surface there.</summary>
    HitSurface,
    /// <summary>Still inside after <see cref="BlackHoleOptics.MaxSteps"/> steps (a ray looping near the photon sphere).</summary>
    StepLimit,
}

/// <summary>
/// The end of a traced ray, in Kerr–Schild Cartesian coordinates (GM/c²). <paramref name="ExitDirection"/> is the unit
/// coordinate velocity dx/dλ there (towards where the light came from); <paramref name="Steps"/> counts RK4 steps.
/// </summary>
public readonly record struct RayResult(RayFate Fate, Vec3 ExitPosition, Vec3 ExitDirection, int Steps);

/// <summary>
/// Black-hole optics (spec §6.6, criterion 2; formulas and check values from compact.md §C7). Lengths are in M = GM/c²
/// unless a name says otherwise; spin a = Jc/GM² with 0 ≤ a ≤ 1, about +z. Pure.
/// <para>
/// <see cref="TraceRay"/> is ruling P3-7's ray tracer, and the C# mirror of <c>lensing.frag</c> (Task 4). The shader
/// must reproduce it exactly:
/// </para>
/// <list type="number">
/// <item><b>Coordinates.</b> Cartesian Kerr–Schild, M = 1: g_μν = η_μν + f l_μ l_ν, η = diag(−1, 1, 1, 1),
/// f = 2r³/(r⁴ + a²z²), l_μ = (1, (rx + ay)/(r² + a²), (ry − ax)/(r² + a²), z/r), and r from
/// r² = ½(w + √(w² + 4a²z²)), w = x² + y² + z² − a² (for w &lt; 0 use the equal form 2a²z²/(√(w² + 4a²z²) − w)).
/// The inverse is g^μν = η^μν − f l^μ l^ν with l^μ = (−1, l_x, l_y, l_z).</item>
/// <item><b>Backwards in time by time reversal.</b> A photon arriving at the camera along −d, around spin a, has the same
/// spatial path as a <i>future-directed</i> photon leaving the camera along +d around spin −a (the Kerr metric is
/// unchanged by t → −t with a → −a). The tracer therefore integrates forwards in λ with the metric's a set to
/// <b>−spin</b>. In ingoing Kerr–Schild coordinates such a ray crosses the horizon smoothly (no coordinate singularity),
/// which is why the capture test can sit just inside r₊ + 0.01. Captured, Escaped and the screen orientation of the
/// Kerr shadow (Bardeen's α = −ξ/sin i) come out right: <c>KerrShadow_TracerMatchesBardeen</c> checks the asymmetric
/// edge-on outline.</item>
/// <item><b>Hamiltonian.</b> H = ½ g^μν p_μ p_ν, integrated in x^i and p_i (6 numbers); p_t = −1 is constant (E = 1),
/// so far from the hole |dx/dλ| ≈ 1 and the step h is roughly a coordinate length. With L = l^μ p_μ = 1 + l·p:
/// dx^i/dλ = p_i − f L l_i and dp_i/dλ = ½ L² ∂_i f + f L ∂_i L, using the analytic derivatives in <c>Derivatives</c>
/// (∂_i r, ∂_i f, ∂_i L in closed form, plain arithmetic).</item>
/// <item><b>Start.</b> d = normalize(direction). With s = l·d at the camera, the null vector v = (T, d) has
/// T = (1 + f s²)/(√(1 − f(1 − s²)) − f s) (the future-directed root of −T² + 1 + f(T + s)² = 0, stable form);
/// p_i = (d_i + f (T + s) l_i)/E with E = T − f (T + s), so p_t = −1. If the root does not exist or E ≤ 0 (a camera
/// inside the ergosphere, out of scope: the Close-up camera stays beyond 2 GM/c²), the ray is Captured at step 0.</item>
/// <item><b>Fate tests</b>, at the camera (step 0) and after every step, in this order: HitSurface if surfaceRadius &gt; 0
/// and r &lt; surfaceRadius; Captured if r &lt; r₊ + 0.01 (r₊ = 1 + √(1 − a²)); Escaped if r &gt; R_esc (the caller
/// passes R_esc = max(60, 1.2 × camera distance), ruling P3-7). A camera already outside R_esc is Escaped at step 0
/// with its own direction. After <see cref="MaxSteps"/> steps with none of these: StepLimit.</item>
/// <item><b>Step.</b> Classical RK4 on (x, p) with h = 0.05 · max(r − r₊, 0.5) · (1 + r/20), capped at 2.0, r taken at
/// the start of the step. At most 400 steps. No allocation in the loop.</item>
/// <item><b>Exit.</b> ExitPosition = x at the stop; ExitDirection = normalize(dx/dλ) there (the k₁ derivative of the
/// would-be next step).</item>
/// </list>
/// <para>
/// Budget: a ray from a camera at distance D travels about 2.2 D (in, then out to R_esc = 1.2 D) at h = 2.0, plus about
/// 100 smaller steps near the hole, so 400 steps cover a camera within about 250 GM/c² (measured, a = 0.5, b ≤ 20: no
/// StepLimit at D ≤ 250, 3 of 81 rays at 300, 56 of 81 at 400). Rays that loop near the photon sphere take small steps
/// there (h ≈ 0.06 at r = 3, a = 0) and may end as StepLimit; they lie in a thin band at the shadow edge
/// (<c>SchwarzschildShadow_ShaderBudgetMatchesRoot27</c> bounds it below 0.5% of √27 for a camera at 50). The exact
/// outline overlay comes from <see cref="KerrShadowOutline"/> (ruling P3-9), not the tracer.
/// </para>
/// </summary>
public static class BlackHoleOptics
{
    /// <summary>The Schwarzschild shadow radius (critical impact parameter) √27 GM/c² (§C7).</summary>
    public const double SchwarzschildShadowRadius = 5.196152422706632;
    /// <summary>The Schwarzschild photon sphere, 3 GM/c² (§C7).</summary>
    public const double PhotonSphereRadius = 3;
    /// <summary>The tracer's step cap (ruling P3-7).</summary>
    public const int MaxSteps = 400;

    /// <summary>Capture margin outside the horizon (ruling P3-7).</summary>
    private const double CaptureMargin = 0.01;
    /// <summary>Largest RK4 step (ruling P3-7).</summary>
    private const double MaxStep = 2.0;

    /// <summary>GM/c² in cm for this mass (M☉), from <see cref="PhysicalConstants"/>: 1.47700 × 10⁵ cm per M☉ (§C7).</summary>
    public static double GravitationalRadiusCm(double massMsun)
        => PhysicalConstants.G * PhysicalConstants.MSun * massMsun / (PhysicalConstants.C * PhysicalConstants.C);

    /// <summary>The outer horizon r₊ = 1 + √(1 − a²) (§C7); a = 0.9 gives 1.4359.</summary>
    public static double HorizonRadius(double spin) => 1 + Math.Sqrt(Math.Max(0, 1 - spin * spin));

    /// <summary>
    /// The innermost stable circular orbit (Bardeen, Press &amp; Teukolsky 1972; §C7):
    /// Z₁ = 1 + (1 − a²)^⅓ [(1 + a)^⅓ + (1 − a)^⅓], Z₂ = √(3a² + Z₁²),
    /// r = 3 + Z₂ ∓ √((3 − Z₁)(3 + Z₁ + 2Z₂)), − prograde, + retrograde. 6 at a = 0; 2.3209 (prograde) at a = 0.9.
    /// </summary>
    public static double IscoRadius(double spin, bool prograde = true)
    {
        double a = Math.Clamp(spin, 0, 1);
        double z1 = 1 + Math.Cbrt(1 - a * a) * (Math.Cbrt(1 + a) + Math.Cbrt(1 - a));
        double z2 = Math.Sqrt(3 * a * a + z1 * z1);
        double root = Math.Sqrt(Math.Max(0, (3 - z1) * (3 + z1 + 2 * z2)));
        return prograde ? 3 + z2 - root : 3 + z2 + root;
    }

    /// <summary>
    /// The ergosphere's outer edge r_E(θ) = 1 + √(1 − a² cos²θ), θ from the spin axis (§C7): 2 at the equator, r₊ at
    /// the poles.
    /// </summary>
    public static double ErgosphereRadius(double spin, double polarAngleRad)
    {
        double c = Math.Cos(polarAngleRad);
        return 1 + Math.Sqrt(Math.Max(0, 1 - spin * spin * c * c));
    }

    /// <summary>
    /// The Kerr shadow outline seen from infinity at inclination i from the spin axis (Bardeen 1973; §C7), as a closed
    /// loop of <paramref name="points"/> screen points (α across, β along the projected spin axis; GM/c²). The spherical
    /// photon orbits r ∈ [r_ph+, r_ph−], r_ph± = 2[1 + cos(⅔ arccos(∓a))], give
    /// ξ = −(r³ − 3r² + a²r + a²)/(a(r − 1)), η = r³(4a² − r(r − 3)²)/(a²(r − 1)²),
    /// α = −ξ/sin i, β = ±√(η + a²cos²i − ξ²cot²i), keeping the real β. The r range where β is real is found first, and
    /// the loop runs along its upper half (β ≥ 0) then back along the lower, with cosine spacing in r (the β = 0 ends
    /// are where the outline turns fastest). Spin below 10⁻⁴ gives the a → 0 limit, a circle of radius √27; a pole-on
    /// view (sin i &lt; 10⁻⁶) gives a circle of radius √(η + a²) at the orbit with ξ = 0. Spin is capped at 0.9999 (at
    /// a = 1 the prograde orbit sits on r = 1, where ξ is 0/0).
    /// </summary>
    public static IReadOnlyList<(double Alpha, double Beta)> KerrShadowOutline(double spin, double inclinationRad, int points)
    {
        var result = new List<(double Alpha, double Beta)>(Math.Max(points, 0));
        if (points <= 0) return result;
        double a = Math.Min(Math.Abs(spin), 0.9999);
        double sinI = Math.Sin(inclinationRad), cosI = Math.Cos(inclinationRad);

        if (a < 1e-4) return Circle(SchwarzschildShadowRadius, points, result);

        double rPro = 2 * (1 + Math.Cos(2.0 / 3 * Math.Acos(-a)));   // r_ph+, prograde
        double rRetro = 2 * (1 + Math.Cos(2.0 / 3 * Math.Acos(a)));  // r_ph−, retrograde

        if (Math.Abs(sinI) < 1e-6)
        {
            // Pole-on: only ξ = 0 orbits reach the axis. ξ falls from + (prograde) to − (retrograde) across the range.
            double lo = rPro, hi = rRetro;
            for (int k = 0; k < 100; k++)
            {
                double mid = 0.5 * (lo + hi);
                if (Xi(a, mid) > 0) lo = mid; else hi = mid;
            }
            double r0 = 0.5 * (lo + hi);
            return Circle(Math.Sqrt(Eta(a, r0) + a * a), points, result);
        }

        double cot2 = cosI * cosI / (sinI * sinI), a2c2 = a * a * cosI * cosI;
        double Beta2(double r) { double xi = Xi(a, r); return Eta(a, r) + a2c2 - xi * xi * cot2; }

        // The interval of r where β² ≥ 0: scan, then refine both ends by bisection.
        const int scan = 4000;
        int first = -1, last = -1;
        for (int k = 0; k <= scan; k++)
        {
            double r = rPro + (rRetro - rPro) * k / scan;
            if (Beta2(r) >= 0) { if (first < 0) first = k; last = k; }
        }
        if (first < 0)
        {
            // Not expected for 0 < i < π; fall back to the orbit with the largest β² (a single point).
            double rBest = rPro, best = double.NegativeInfinity;
            for (int k = 0; k <= scan; k++)
            {
                double r = rPro + (rRetro - rPro) * k / scan;
                if (Beta2(r) > best) { best = Beta2(r); rBest = r; }
            }
            for (int k = 0; k < points; k++) result.Add((-Xi(a, rBest) / sinI, 0));
            return result;
        }
        double r1 = rPro + (rRetro - rPro) * first / scan;
        double r2 = rPro + (rRetro - rPro) * last / scan;
        if (first > 0) r1 = RefineRoot(Beta2, rPro + (rRetro - rPro) * (first - 1) / scan, r1);
        if (last < scan) r2 = RefineRoot(Beta2, rPro + (rRetro - rPro) * (last + 1) / scan, r2);

        for (int k = 0; k < points; k++)
        {
            double u = (double)k / points;
            bool upper = u <= 0.5;
            double t = upper ? 2 * Math.PI * u : 2 * Math.PI * (1 - u);
            double r = r1 + (r2 - r1) * 0.5 * (1 - Math.Cos(t));
            double beta = Math.Sqrt(Math.Max(0, Beta2(r)));
            result.Add((-Xi(a, r) / sinI, upper ? beta : -beta));
        }
        return result;
    }

    /// <summary>The weak-field deflection 4GM/(bc²) = 4/b radians (Einstein 1915; §C7): 0.004 at b = 1,000.</summary>
    public static double WeakFieldDeflection(double impactParameter) => 4 / impactParameter;

    /// <summary>
    /// Ruling P3-7's tracer: follows the light that reaches <paramref name="cameraPosition"/> from
    /// <paramref name="direction"/> backwards until it is captured, escapes past <paramref name="escapeRadius"/>, hits
    /// the surface at <paramref name="surfaceRadius"/> (spin-0 use only; 0 for none), or runs out of
    /// <see cref="MaxSteps"/> steps. Positions in Kerr–Schild Cartesian coordinates, GM/c², spin axis +z. The algorithm,
    /// which the shader mirrors, is set out on <see cref="BlackHoleOptics"/>.
    /// </summary>
    public static RayResult TraceRay(double spin, Vec3 cameraPosition, Vec3 direction, double escapeRadius,
                                     double surfaceRadius = 0)
        => Trace(spin, cameraPosition, direction, escapeRadius, surfaceRadius, MaxSteps);

    /// <summary>
    /// <see cref="TraceRay"/> with a different step budget. Only the tests use another budget (rays fired from
    /// r = 1,000 need more than 400 steps of 2.0 just to arrive); everything else is identical.
    /// </summary>
    internal static RayResult Trace(double spin, Vec3 cameraPosition, Vec3 direction, double escapeRadius,
                                    double surfaceRadius, int maxSteps)
    {
        double a = -spin; // time reversal: the future-directed ray around −a (see the class notes, item 2)
        double rPlus = HorizonRadius(spin);
        double rCapture = rPlus + CaptureMargin;
        Vec3 d = direction.Normalized();
        double x = cameraPosition.X, y = cameraPosition.Y, z = cameraPosition.Z;

        // Step 0 at the camera, before the momentum is set up (it does not exist inside the horizon).
        double r = KerrSchildR(a * a, x, y, z);
        if (surfaceRadius > 0 && r < surfaceRadius) return new(RayFate.HitSurface, cameraPosition, d, 0);
        if (r < rCapture) return new(RayFate.Captured, cameraPosition, d, 0);
        if (r > escapeRadius) return new(RayFate.Escaped, cameraPosition, d, 0);
        if (!InitialMomentum(a, x, y, z, d, out double px, out double py, out double pz))
            return new(RayFate.Captured, cameraPosition, d, 0);

        for (int steps = 0; ; steps++)
        {
            Derivatives(a, x, y, z, px, py, pz, out double k1x, out double k1y, out double k1z,
                        out double k1px, out double k1py, out double k1pz, out r);
            RayFate fate;
            if (surfaceRadius > 0 && r < surfaceRadius) fate = RayFate.HitSurface;
            else if (r < rCapture) fate = RayFate.Captured;
            else if (r > escapeRadius) fate = RayFate.Escaped;
            else if (steps >= maxSteps) fate = RayFate.StepLimit;
            else
            {
                Rk4Step(a, StepSize(r, rPlus), ref x, ref y, ref z, ref px, ref py, ref pz,
                        k1x, k1y, k1z, k1px, k1py, k1pz);
                continue;
            }
            return new(fate, new Vec3(x, y, z), new Vec3(k1x, k1y, k1z).Normalized(), steps);
        }
    }

    /// <summary>
    /// For tests: H = ½ g^μν p_μ p_ν at the camera and after <paramref name="steps"/> tracer steps, or at capture
    /// (r &lt; r₊ + 0.01) if that comes first; no other fate tests.
    /// </summary>
    internal static (double Start, double End) HamiltonianDrift(double spin, Vec3 cameraPosition, Vec3 direction, int steps)
    {
        double a = -spin, rPlus = HorizonRadius(spin);
        double x = cameraPosition.X, y = cameraPosition.Y, z = cameraPosition.Z;
        if (!InitialMomentum(a, x, y, z, direction.Normalized(), out double px, out double py, out double pz))
            return (double.NaN, double.NaN);
        double h0 = Hamiltonian(a, x, y, z, px, py, pz);
        for (int k = 0; k < steps; k++)
        {
            Derivatives(a, x, y, z, px, py, pz, out double k1x, out double k1y, out double k1z,
                        out double k1px, out double k1py, out double k1pz, out double r);
            if (r < rPlus + CaptureMargin) break;
            Rk4Step(a, StepSize(r, rPlus), ref x, ref y, ref z, ref px, ref py, ref pz, k1x, k1y, k1z, k1px, k1py, k1pz);
        }
        return (h0, Hamiltonian(a, x, y, z, px, py, pz));
    }

    /// <summary>
    /// The fraction of a non-rotating star's surface visible from afar (Schwarzschild, exact; §C7): f = (1 − cos ψ_max)/2,
    /// or 1 if ψ_max ≥ π or the star lies inside its photon sphere (R ≤ 1.5 r_g). ψ_max, the grazing ray's bending
    /// (b_c = R/√(1 − u), u = r_g/R = 2GM/(Rc²)), is ∫ from R to ∞ of dr/(r²√(1/b_c² − (1 − r_g/r)/r²)); with y = R/r it
    /// is ∫₀¹ dy/√((1 − u) − y²(1 − uy)). The substitution y = 1 − t² removes the endpoint singularity exactly:
    /// ψ_max = ∫₀¹ 2 dt/√((1 + y) − u(1 + y + y²)), a smooth integrand, integrated by adaptive Simpson.
    /// 1.4 M☉, 12 km gives 0.7549.
    /// </summary>
    public static double VisibleSurfaceFraction(double massMsun, double radiusKm)
    {
        double u = 2 * GravitationalRadiusCm(massMsun) / (radiusKm * 1e5);
        if (u >= 2.0 / 3) return 1;
        double F(double t) { double y = 1 - t * t; return 2 / Math.Sqrt((1 + y) - u * (1 + y + y * y)); }
        double psi = AdaptiveSimpson(F, 0, 1, 1e-12);
        return psi >= Math.PI ? 1 : 0.5 * (1 - Math.Cos(psi));
    }

    /// <summary>
    /// The shadow's apparent diameter (radians) of a hole of this mass (M☉) at this distance (cm): 2√27 GM/(Dc²) for
    /// spin 0 (§C9's shadow law, d_sh = 2√27 θ_g). For spin ≥ 10⁻⁴ (a decision: the signature has no inclination) the
    /// diameter is twice the outline's mean radius seen edge-on (i = 90°, where spin matters most), the mean of the
    /// distance from the outline's centre ((α_min + α_max)/2, 0) over the polar angle about it (Johannsen &amp; Psaltis
    /// 2010's mean radius). a = 0.9 gives about 5.0 GM/c², inside the EHT's quoted 4.8–5.2 (§C7).
    /// </summary>
    public static double ApparentShadowDiameterRad(double massMsun, double distanceCm, double spin = 0)
    {
        double thetaG = GravitationalRadiusCm(massMsun) / distanceCm;
        double radius = Math.Abs(spin) < 1e-4 ? SchwarzschildShadowRadius : MeanShadowRadius(spin, Math.PI / 2);
        return 2 * radius * thetaG;
    }

    // ---- The tracer's pieces (mirrored by lensing.frag) ----

    /// <summary>h = 0.05 · max(r − r₊, 0.5) · (1 + r/20), capped at 2.0 (ruling P3-7).</summary>
    private static double StepSize(double r, double rPlus)
        => Math.Min(0.05 * Math.Max(r - rPlus, 0.5) * (1 + r / 20), MaxStep);

    /// <summary>The Kerr–Schild r: r² = ½(w + √(w² + 4a²z²)), w = ρ² − a², in a form without cancellation.</summary>
    private static double KerrSchildR(double a2, double x, double y, double z)
    {
        double z2 = z * z;
        double w = x * x + y * y + z2 - a2;
        double sq = Math.Sqrt(w * w + 4 * a2 * z2);
        double r2 = w >= 0 ? 0.5 * (w + sq) : 2 * a2 * z2 / (sq - w);
        return Math.Sqrt(r2);
    }

    /// <summary>f and the spatial l_i of the Kerr–Schild metric at (x, y, z), with r.</summary>
    private static void MetricParts(double a, double x, double y, double z,
                                    out double r, out double f, out double lx, out double ly, out double lz)
    {
        double a2 = a * a;
        r = KerrSchildR(a2, x, y, z);
        double r2 = r * r;
        double s = r2 + a2;
        f = 2 * r * r2 / (r2 * r2 + a2 * z * z);
        lx = (r * x + a * y) / s;
        ly = (r * y - a * x) / s;
        lz = z / r;
    }

    /// <summary>
    /// p_i for a future-directed null ray leaving (x, y, z) along unit d, scaled to p_t = −1 (class notes, item 4).
    /// False if no such ray exists there (inside the ergosphere).
    /// </summary>
    private static bool InitialMomentum(double a, double x, double y, double z, Vec3 d,
                                        out double px, out double py, out double pz)
    {
        MetricParts(a, x, y, z, out _, out double f, out double lx, out double ly, out double lz);
        double s = lx * d.X + ly * d.Y + lz * d.Z;
        double disc = 1 - f * (1 - s * s);
        double den = disc >= 0 ? Math.Sqrt(disc) - f * s : -1;
        double t = den > 0 ? (1 + f * s * s) / den : 0;
        double e = t - f * (t + s);
        if (!(den > 0) || !(e > 0)) { px = py = pz = 0; return false; }
        double k = f * (t + s);
        px = (d.X + k * lx) / e;
        py = (d.Y + k * ly) / e;
        pz = (d.Z + k * lz) / e;
        return true;
    }

    /// <summary>H = ½ g^μν p_μ p_ν = ½(|p|² − 1) − ½ f L², with p_t = −1 and L = 1 + l·p.</summary>
    private static double Hamiltonian(double a, double x, double y, double z, double px, double py, double pz)
    {
        MetricParts(a, x, y, z, out _, out double f, out double lx, out double ly, out double lz);
        double l = 1 + lx * px + ly * py + lz * pz;
        return 0.5 * (px * px + py * py + pz * pz - 1) - 0.5 * f * l * l;
    }

    /// <summary>
    /// Hamilton's equations for H = ½ g^μν p_μ p_ν in Kerr–Schild Cartesian coordinates (M = 1, p_t = −1):
    /// dx^i/dλ = p_i − f L l_i; dp_i/dλ = ½ L² ∂_i f + f L ∂_i L, with Q = r⁴ + a²z², S = r² + a²,
    /// ∂_x r = x r³/Q, ∂_y r = y r³/Q, ∂_z r = z r S/Q;
    /// ∂_i f = (2r²/Q²)[(3a²z² − r⁴) ∂_i r − 2a² r z δ_iz];
    /// ∂_i L = A ∂_i r + {(r p_x − a p_y)/S, (a p_x + r p_y)/S, p_z/r}_i, A = (x p_x + y p_y − 2r(l_x p_x + l_y p_y))/S − z p_z/r².
    /// Also returns r at the point.
    /// </summary>
    private static void Derivatives(double a, double x, double y, double z, double px, double py, double pz,
                                    out double dx, out double dy, out double dz,
                                    out double dpx, out double dpy, out double dpz, out double r)
    {
        double a2 = a * a, z2 = z * z;
        r = KerrSchildR(a2, x, y, z);
        double r2 = r * r;
        double q = r2 * r2 + a2 * z2;
        double s = r2 + a2;
        double f = 2 * r * r2 / q;
        double lx = (r * x + a * y) / s, ly = (r * y - a * x) / s, lz = z / r;
        double l = 1 + lx * px + ly * py + lz * pz;
        double fl = f * l;

        dx = px - fl * lx;
        dy = py - fl * ly;
        dz = pz - fl * lz;

        double r3q = r * r2 / q;
        double drx = x * r3q, dry = y * r3q, drz = z * r * s / q;
        double cf = 2 * r2 / (q * q), kf = 3 * a2 * z2 - r2 * r2;
        double dfx = cf * kf * drx, dfy = cf * kf * dry, dfz = cf * (kf * drz - 2 * a2 * r * z);
        double aa = (x * px + y * py - 2 * r * (lx * px + ly * py)) / s - z * pz / r2;
        double dlx = aa * drx + (r * px - a * py) / s;
        double dly = aa * dry + (a * px + r * py) / s;
        double dlz = aa * drz + pz / r;

        double hl2 = 0.5 * l * l;
        dpx = hl2 * dfx + fl * dlx;
        dpy = hl2 * dfy + fl * dly;
        dpz = hl2 * dfz + fl * dlz;
    }

    /// <summary>One classical RK4 step of size h on (x, p), given k₁ (the derivatives at the start).</summary>
    private static void Rk4Step(double a, double h, ref double x, ref double y, ref double z,
                                ref double px, ref double py, ref double pz,
                                double k1x, double k1y, double k1z, double k1px, double k1py, double k1pz)
    {
        double hh = 0.5 * h;
        Derivatives(a, x + hh * k1x, y + hh * k1y, z + hh * k1z, px + hh * k1px, py + hh * k1py, pz + hh * k1pz,
                    out double k2x, out double k2y, out double k2z, out double k2px, out double k2py, out double k2pz, out _);
        Derivatives(a, x + hh * k2x, y + hh * k2y, z + hh * k2z, px + hh * k2px, py + hh * k2py, pz + hh * k2pz,
                    out double k3x, out double k3y, out double k3z, out double k3px, out double k3py, out double k3pz, out _);
        Derivatives(a, x + h * k3x, y + h * k3y, z + h * k3z, px + h * k3px, py + h * k3py, pz + h * k3pz,
                    out double k4x, out double k4y, out double k4z, out double k4px, out double k4py, out double k4pz, out _);
        double h6 = h / 6;
        x += h6 * (k1x + 2 * k2x + 2 * k3x + k4x);
        y += h6 * (k1y + 2 * k2y + 2 * k3y + k4y);
        z += h6 * (k1z + 2 * k2z + 2 * k3z + k4z);
        px += h6 * (k1px + 2 * k2px + 2 * k3px + k4px);
        py += h6 * (k1py + 2 * k2py + 2 * k3py + k4py);
        pz += h6 * (k1pz + 2 * k2pz + 2 * k3pz + k4pz);
    }

    // ---- Outline and quadrature helpers ----

    private static double Xi(double a, double r) => -(r * r * r - 3 * r * r + a * a * r + a * a) / (a * (r - 1));

    private static double Eta(double a, double r)
    {
        double rm3 = r - 3, rm1 = r - 1;
        return r * r * r * (4 * a * a - r * rm3 * rm3) / (a * a * rm1 * rm1);
    }

    private static List<(double Alpha, double Beta)> Circle(double radius, int points, List<(double Alpha, double Beta)> into)
    {
        for (int k = 0; k < points; k++)
        {
            double t = 2 * Math.PI * k / points;
            into.Add((radius * Math.Cos(t), radius * Math.Sin(t)));
        }
        return into;
    }

    /// <summary>Bisect for the root of g between <paramref name="outside"/> (g &lt; 0) and <paramref name="inside"/> (g ≥ 0).</summary>
    private static double RefineRoot(Func<double, double> g, double outside, double inside)
    {
        for (int k = 0; k < 60; k++)
        {
            double mid = 0.5 * (outside + inside);
            if (g(mid) >= 0) inside = mid; else outside = mid;
        }
        return inside;
    }

    /// <summary>The mean distance of the outline from its centre ((α_min + α_max)/2, 0), averaged over polar angle.</summary>
    private static double MeanShadowRadius(double spin, double inclinationRad)
    {
        var o = KerrShadowOutline(spin, inclinationRad, 720);
        double cx = 0.5 * (o.Min(p => p.Alpha) + o.Max(p => p.Alpha));
        double sum = 0, total = 0;
        for (int k = 0; k < o.Count; k++)
        {
            var (a1, b1) = o[k];
            var (a2, b2) = o[(k + 1) % o.Count];
            double p1 = Math.Atan2(b1, a1 - cx), p2 = Math.Atan2(b2, a2 - cx);
            double dp = Math.Abs(Math.IEEERemainder(p2 - p1, 2 * Math.PI));
            double r1 = Math.Sqrt((a1 - cx) * (a1 - cx) + b1 * b1), r2 = Math.Sqrt((a2 - cx) * (a2 - cx) + b2 * b2);
            sum += 0.5 * (r1 + r2) * dp;
            total += dp;
        }
        return sum / total;
    }

    private static double AdaptiveSimpson(Func<double, double> f, double a, double b, double eps)
    {
        double fa = f(a), fb = f(b), fm = f(0.5 * (a + b));
        double whole = (b - a) / 6 * (fa + 4 * fm + fb);
        return SimpsonStep(f, a, b, fa, fm, fb, whole, eps, 50);
    }

    private static double SimpsonStep(Func<double, double> f, double a, double b, double fa, double fm, double fb,
                                      double whole, double eps, int depth)
    {
        double m = 0.5 * (a + b), lm = 0.5 * (a + m), rm = 0.5 * (m + b);
        double flm = f(lm), frm = f(rm);
        double left = (m - a) / 6 * (fa + 4 * flm + fm);
        double right = (b - m) / 6 * (fm + 4 * frm + fb);
        double delta = left + right - whole;
        if (depth <= 0 || Math.Abs(delta) <= 15 * eps) return left + right + delta / 15;
        return SimpsonStep(f, a, m, fa, flm, fm, left, 0.5 * eps, depth - 1)
             + SimpsonStep(f, m, b, fm, frm, fb, right, 0.5 * eps, depth - 1);
    }
}
