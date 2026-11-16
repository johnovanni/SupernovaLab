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
    /// <summary>Plan Task 11: crossed the equatorial plane inside the accretion disk's annulus (opaque, thin). Shade the disk there.</summary>
    HitDisk,
}

/// <summary>
/// The end of a traced ray, in Kerr–Schild Cartesian coordinates (GM/c²). <paramref name="ExitDirection"/> is the unit
/// coordinate velocity dx/dλ there (towards where the light came from); <paramref name="Steps"/> counts RK4 steps.
/// Plan Task 10 (ruling P3-8): <see cref="GridCrossing"/> is where the ray first crossed the grid sphere r = 50 outward (linear between
/// the two steps either side), or null.
/// </summary>
public readonly record struct RayResult(RayFate Fate, Vec3 ExitPosition, Vec3 ExitDirection, int Steps)
{
    /// <summary>Where the ray first crossed r = <see cref="BlackHoleOptics.GridRadius"/> outward, or null (ruling P3-8).</summary>
    public Vec3? GridCrossing { get; init; }

    /// <summary>
    /// Plan Task 11: for <see cref="RayFate.HitDisk"/>, the Boyer–Lindquist r where the ray met the disk (GM/c²) and the light's redshift
    /// factor g = ν_received/ν_emitted there (<see cref="BlackHoleOptics.DiskRedshift"/>); null otherwise.
    /// </summary>
    public (double RadiusGm, double Redshift)? DiskHit { get; init; }
}

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
/// <item><b>Backwards in time by time reversal; the chart (ruling R-P3-8).</b> A photon arriving at the camera along −d,
/// around spin a, has the same spatial path as a <i>future-directed</i> photon leaving the camera along +d around spin −a
/// (the Kerr metric is unchanged by t → −t with a → −a). The tracer therefore integrates forwards in λ with the metric's a
/// set to <b>−spin</b>, in ingoing Kerr–Schild coordinates of spin −a, where such a ray crosses the horizon smoothly (no
/// coordinate singularity), which is why the capture test can sit just inside r₊ + 0.01. Read back with t → −t, that chart
/// is <b>outgoing Kerr–Schild coordinates of the hole's own spin +a</b>: positions, directions and the scene's axes
/// (spin along +z) are all in it. Its azimuth φ differs from Boyer–Lindquist's by about a/r (≈ 1° at r = 60 for a = 0.9),
/// which does not matter for drawing; r and θ are Boyer–Lindquist's. Captured, Escaped and the screen orientation of the
/// Kerr shadow (Bardeen's α = −ξ/sin i) come out right: <c>KerrShadow_TracerMatchesBardeen</c> checks the asymmetric
/// edge-on outline.</item>
/// <item><b>Hamiltonian.</b> H = ½ g^μν p_μ p_ν, integrated in x^i and p_i (6 numbers); p_t = −1 is constant (E = 1),
/// so far from the hole |dx/dλ| ≈ 1 and the step h is roughly a coordinate length. With L = l^μ p_μ = 1 + l·p:
/// dx^i/dλ = p_i − f L l_i and dp_i/dλ = ½ L² ∂_i f + f L ∂_i L, using the analytic derivatives in <c>Derivatives</c>
/// (∂_i r, ∂_i f, ∂_i L in closed form, plain arithmetic).</item>
/// <item><b>Far cameras (ruling R-P3-6).</b> When the camera is farther than <see cref="FarCameraRadius"/> (200, by
/// |x|), the ray first runs along its straight line to the sphere |x| = <see cref="FarStartRadius"/> (199.99, just inside
/// 200 so the step-0 escape test cannot fire on rounding) and is traced from there with R_esc = 200, whatever the caller
/// passed. A line whose forward half misses that sphere escapes at step 0 with the weak-field deflection applied
/// analytically, toward the hole: Δ = (2/b)(1 − cos γ), b the line's distance from the hole and γ the angle between the
/// camera's position vector and d (the 4/b of a line passing the hole from far away, integrated from the camera onwards
/// along the straight line; computed as 2b/(|x|²(1 + cos γ)) when cos γ &gt; 0, without cancellation). Its ExitPosition is
/// the camera. [APPROX] The bending outside r = 200 on the way in (about 4/b, up to about 1° in a thin ring near b ≈ 200 GM/c²,
/// seen only when zoomed out about 10× past the default view; well under that elsewhere) is
/// neglected; the 400-step budget then covers every camera distance.</item>
/// <item><b>Start.</b> d = normalize(direction). With s = l·d at the camera, the null vector v = (T, d) has
/// T = (1 + f s²)/(√(1 − f(1 − s²)) − f s) (the future-directed root of −T² + 1 + f(T + s)² = 0, stable form);
/// p_i = (d_i + f (T + s) l_i)/E with E = T − f (T + s), so p_t = −1. If the root does not exist or E ≤ 0 (a camera
/// inside the ergosphere, out of scope: the Close-up camera stays beyond 2 GM/c²), the ray is Captured at step 0.</item>
/// <item><b>Fate tests</b>, at the camera (step 0) and after every step, in this order: HitSurface if surfaceRadius &gt; 0
/// and r &lt; surfaceRadius; Captured if r &lt; r₊ + 0.01 (r₊ = 1 + √(1 − a²)); Escaped if r &gt; R_esc (the caller
/// passes R_esc = max(60, 1.2 × camera distance), ruling P3-7; 200 for a far camera, item 4). A camera already outside
/// R_esc is Escaped at step 0 with its own direction. After <see cref="MaxSteps"/> steps with none of these: StepLimit
/// (ruling R-P3-7: shaded by the surroundings along the exit direction; these rays orbit near the photon sphere, so they
/// read as the photon ring).</item>
/// <item><b>Step.</b> Classical RK4 on (x, p) with h = 0.05 · max(r − r₊, floor) · (1 + r/20), capped at 2.0 (floor = <see cref="StepFloor"/>:
/// 0.5 below a ≈ 0.987, then 16 (r_ph+ − r₊); inconsistency #64), r taken at
/// the start of the step. At most 400 steps. No allocation in the loop.</item>
/// <item><b>Exit.</b> ExitPosition = x at the stop; ExitDirection = normalize(dx/dλ) there (the k₁ derivative of the
/// would-be next step).</item>
/// <item><b>Grid</b> (plan Task 10, ruling P3-8). The first outward crossing of the sphere r = <see cref="GridRadius"/> (50), the
/// point linear between the two steps either side (<see cref="RayResult.GridCrossing"/>); the shader draws the grid lines there
/// (<see cref="GridLineDistanceDeg"/>).</item>
/// <item><b>Disk</b> (plan Task 11; spec §6.2). With an annulus [diskInner, diskOuter] (diskOuter &gt; 0), before the fate tests of each
/// step after the first: if z changed sign over the step (z_prev · z &lt; 0), the crossing point is linear between the two points
/// (u = z_prev/(z_prev − z)), and if its r (the Kerr–Schild r at z = 0, √(x² + y² − a²)) lies in the annulus the ray ends there as
/// HitDisk: a thin, opaque disk, so the first crossing is the one seen, and a ray through the plunging region inside the ISCO goes on.
/// The light's redshift is g = 1/(u^t (1 − Ω ℓ)) (<see cref="DiskRedshift"/>), with ℓ = L/E the photon's own angular momentum about +z.
/// The tracer's momentum is the time-reversed ray's (p_t = −1, item 2), so the light's is ℓ = −(x p_y − y p_x), the rotation Killing
/// component x p_y − y p_x being conserved and the same in the outgoing chart (the charts differ only in t and in a shift of φ by a
/// function of r).</item>
/// </list>
/// <para>
/// Budget: a ray from a camera at distance D travels about 2.2 D (in, then out to R_esc = 1.2 D) at h = 2.0, plus about
/// 100 smaller steps near the hole, so 400 steps cover a camera within about 250 GM/c² (measured, a = 0.5, b ≤ 20: no
/// StepLimit at D ≤ 250, 3 of 81 rays at 300, 56 of 81 at 400); the far-camera rule (item 4) starts every farther ray
/// at 200. Rays that loop near the photon sphere take small steps
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
    /// <summary>Ruling R-P3-6: a camera farther than this (|x|, GM/c²) starts its rays on the sphere of this radius, and R_esc is this.</summary>
    public const double FarCameraRadius = 200;
    /// <summary>Ruling R-P3-6: where a far camera's ray starts, just inside <see cref="FarCameraRadius"/>.</summary>
    public const double FarStartRadius = 199.99;

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
        => Outline(spin, inclinationRad, points, null);

    /// <summary>
    /// <see cref="KerrShadowOutline"/>, also filling <paramref name="orbitRadii"/> (when given) with each point's spherical photon
    /// orbit radius r (3 for the a → 0 circle).
    /// </summary>
    private static List<(double Alpha, double Beta)> Outline(double spin, double inclinationRad, int points, List<double>? orbitRadii)
    {
        var result = new List<(double Alpha, double Beta)>(Math.Max(points, 0));
        if (points <= 0) return result;
        double a = Math.Min(Math.Abs(spin), 0.9999);
        double sinI = Math.Sin(inclinationRad), cosI = Math.Cos(inclinationRad);

        if (a < 1e-4)
        {
            for (int k = 0; k < points; k++) orbitRadii?.Add(PhotonSphereRadius);
            return Circle(SchwarzschildShadowRadius, points, result);
        }

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
            for (int k = 0; k < points; k++) orbitRadii?.Add(r0);
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
            for (int k = 0; k < points; k++) { result.Add((-Xi(a, rBest) / sinI, 0)); orbitRadii?.Add(rBest); }
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
            orbitRadii?.Add(r);
        }
        return result;
    }

    /// <summary>
    /// Plan Task 10 (ruling P3-9's exact outline): the shadow's edge as the Close-up's camera sees it, at its finite distance. Returns
    /// <paramref name="points"/> unit directions at <paramref name="camera"/> (Kerr–Schild Cartesian, GM/c², the tracer's chart:
    /// ruling R-P3-8) in the tracer's convention: a pixel whose direction is one of these lies exactly on the edge
    /// <see cref="TraceRay"/> draws between Captured and escaping rays. Empty inside the horizon.
    /// <para>Each direction is the ray that, traced back, winds onto one of Bardeen's spherical photon orbits, the ones of
    /// <see cref="KerrShadowOutline"/> at the camera's own inclination i = θ (in the same order, so from far away the directions turn
    /// into that outline's α, β). In the tracer's time-reversed spacetime (spin a' = −a, ingoing Kerr–Schild coordinates with
    /// x + iy = (r + i a') sin θ e^{iφ̃}, z = r cos θ; class notes item 2) the orbit's constants are ξ' = −ξ = α sin i, η' = η =
    /// β² − a² cos² i + ξ² cot² i, and at the camera the Boyer–Lindquist momentum is p_t = −1, p_φ = ξ', p_θ = −β (= ∓√Θ(θ): β up the screen is towards −θ) and
    /// p_r = σ √R / Δ, R = (r² + a² − a'ξ')² − Δ (η' + (ξ' − a')²), Δ = r² − 2r + a², with σ = −1 (inward) when the camera is outside
    /// the orbit and +1 inside it. Ingoing Kerr–Schild time and azimuth add (2r/Δ) dr and (a'/Δ) dr, so the covariant radial
    /// momentum there is p̃_r = (σ√R + 2r − a'ξ')/Δ, finite at the horizon; p̃_θ and p̃_φ are unchanged. The Cartesian p_i solve
    /// Σ_i (∂x^i/∂q) p_i = p̃_q for q = r, θ, φ̃, and the direction is that of dx^i/dλ = p_i − f L l_i (L = 1 + l·p), exactly as
    /// the tracer starts a ray (it inverts the same relation). <c>ShadowEdgeDirections_MatchTheTracersEdge_FromACloseCamera</c>
    /// checks every direction against the tracer's own edge.</para>
    /// <para>A camera within 0.002 rad of the spin axis is handled by computing the edge 0.0025 rad off the axis and turning it onto
    /// the camera (the outline there is a circle to that accuracy); the Close-up's camera never comes closer than 0.1° to the axis.
    /// Spin is capped at 0.9999, as in <see cref="KerrShadowOutline"/>.</para>
    /// </summary>
    public static IReadOnlyList<Vec3> ShadowEdgeDirections(double spin, Vec3 camera, int points)
    {
        var result = new List<Vec3>(Math.Max(points, 0));
        double a = Math.Min(Math.Abs(spin), 0.9999);
        double r = KerrSchildR(a * a, camera.X, camera.Y, camera.Z);
        if (points <= 0 || !double.IsFinite(r) || !(r > HorizonRadius(a) + CaptureMargin)) return result;
        double cosT = Math.Clamp(camera.Z / r, -1, 1);
        double sinT = Math.Sqrt(Math.Max(0, 1 - cosT * cosT));
        const double nearAxis = 0.002;
        if (sinT < nearAxis)
        {
            // Off the axis by 0.002 rad in the camera's own meridian (or the x-z plane), then turned back onto the camera.
            double phi = Math.Abs(camera.X) + Math.Abs(camera.Y) > 0 ? Math.Atan2(camera.Y, camera.X) : 0;
            double t = cosT >= 0 ? 1.25 * nearAxis : Math.PI - 1.25 * nearAxis;   // safely past the threshold
            var moved = camera.Length * new Vec3(Math.Sin(t) * Math.Cos(phi), Math.Sin(t) * Math.Sin(phi), Math.Cos(t));
            foreach (var d in ShadowEdgeDirections(a, moved, points)) result.Add(Turn(d, moved.Normalized(), camera.Normalized()));
            return result;
        }

        double ap = -a;   // the tracer's metric spin (time reversal)
        double inc = Math.Acos(cosT);
        var radii = new List<double>(points);
        var outline = Outline(a, inc, points, radii);
        double sinI = Math.Sin(inc), cot2 = cosT * cosT / (sinT * sinT);

        // The camera's azimuth in the chart: (x + iy) / ((r + i a') sin θ) = e^{iφ̃}.
        double s2 = (r * r + ap * ap) * sinT;
        double cosP = (camera.X * r + camera.Y * ap) / s2, sinP = (camera.Y * r - camera.X * ap) / s2;
        var jr = new Vec3(sinT * cosP, sinT * sinP, cosT);
        var jt = new Vec3(cosT * (r * cosP - ap * sinP), cosT * (r * sinP + ap * cosP), -r * sinT);
        var jp = new Vec3(-camera.Y, camera.X, 0);
        Vec3 ct = jt.Cross(jp), cp = jp.Cross(jr), cr = jr.Cross(jt);
        double det = jr.Dot(ct);
        MetricParts(ap, camera.X, camera.Y, camera.Z, out _, out double f, out double lx, out double ly, out double lz);
        var l = new Vec3(lx, ly, lz);
        double delta = r * r - 2 * r + a * a;

        for (int k = 0; k < outline.Count; k++)
        {
            var (alpha, beta) = outline[k];
            double xi = -alpha * sinI;
            double eta = beta * beta - a * a * cosT * cosT + xi * xi * cot2;
            double xiP = -xi;
            double w = r * r + a * a - ap * xiP, v = xiP - ap;
            double bigR = Math.Max(0, w * w - delta * (eta + v * v));
            double sigma = r > radii[k] ? -1 : 1;
            double pr = (sigma * Math.Sqrt(bigR) + 2 * r - ap * xiP) / delta;
            var p = (pr * ct - beta * cp + xiP * cr) * (1 / det);   // p_θ = −β: β > 0 is towards +z on the screen, against θ
            double bigL = 1 + l.Dot(p);
            var dx = p - f * bigL * l;
            var d = dx.Normalized();
            if (double.IsFinite(d.X) && double.IsFinite(d.Y) && double.IsFinite(d.Z) && d.Length > 0.5) result.Add(d);
        }
        return result;
    }

    /// <summary>Turns <paramref name="v"/> by the rotation that takes unit <paramref name="from"/> onto unit <paramref name="to"/> (Rodrigues).</summary>
    private static Vec3 Turn(Vec3 v, Vec3 from, Vec3 to)
    {
        var axis = from.Cross(to);
        double s = axis.Length, c = from.Dot(to);
        if (s < 1e-15) return c > 0 ? v : -v;
        var k = axis * (1 / s);
        return (v * c + k.Cross(v) * s + k * (k.Dot(v) * (1 - c))).Normalized();
    }

    /// <summary>The escape radius a caller passes for a camera at this distance (GM/c²): max(60, 1.2 D) (ruling P3-7).</summary>
    public static double EscapeRadiusFor(double cameraDistance) => Math.Max(60, 1.2 * cameraDistance);

    /// <summary>The weak-field deflection 4GM/(bc²) = 4/b radians (Einstein 1915; §C7): 0.004 at b = 1,000.</summary>
    public static double WeakFieldDeflection(double impactParameter) => 4 / impactParameter;

    /// <summary>
    /// Ruling P3-7's tracer: follows the light that reaches <paramref name="cameraPosition"/> from
    /// <paramref name="direction"/> backwards until it is captured, escapes past <paramref name="escapeRadius"/>, hits
    /// the surface at <paramref name="surfaceRadius"/> (spin-0 use only; 0 for none), or runs out of
    /// <see cref="MaxSteps"/> steps. Positions in Kerr–Schild Cartesian coordinates, GM/c², spin axis +z. The algorithm,
    /// which the shader mirrors, is set out on <see cref="BlackHoleOptics"/>.
    /// </summary>
    /// <param name="diskInner">Plan Task 11: the accretion disk's inner edge (the ISCO), GM/c².</param>
    /// <param name="diskOuter">The disk's outer edge; 0 (the default) for no disk.</param>
    public static RayResult TraceRay(double spin, Vec3 cameraPosition, Vec3 direction, double escapeRadius,
                                     double surfaceRadius = 0, double diskInner = 0, double diskOuter = 0)
        => Trace(spin, cameraPosition, direction, escapeRadius, surfaceRadius, MaxSteps, farCameras: true, diskInner, diskOuter);

    /// <summary>
    /// <see cref="TraceRay"/> with a different step budget. Only the tests use another budget (rays fired from
    /// r = 1,000 need more than 400 steps of 2.0 just to arrive), and they integrate the whole way: the far-camera rule
    /// (class notes, item 4) is on only when <paramref name="farCameras"/> is, as <see cref="TraceRay"/> has it.
    /// Everything else is identical.
    /// </summary>
    internal static RayResult Trace(double spin, Vec3 cameraPosition, Vec3 direction, double escapeRadius,
                                    double surfaceRadius, int maxSteps, bool farCameras = false, double diskInner = 0, double diskOuter = 0)
    {
        double a = -spin; // time reversal: the future-directed ray around −a (see the class notes, item 2)
        double rPlus = HorizonRadius(spin), floor = StepFloor(spin);
        double rCapture = rPlus + CaptureMargin;
        Vec3 d = direction.Normalized();
        if (farCameras && cameraPosition.Length > FarCameraRadius)
        {
            if (!FarStart(cameraPosition, d, out Vec3 onSphere, out Vec3 bent))
                return new(RayFate.Escaped, cameraPosition, bent, 0);
            cameraPosition = onSphere;
            escapeRadius = FarCameraRadius;
        }
        double x = cameraPosition.X, y = cameraPosition.Y, z = cameraPosition.Z;

        // Step 0 at the camera, before the momentum is set up (it does not exist inside the horizon).
        double r = KerrSchildR(a * a, x, y, z);
        if (surfaceRadius > 0 && r < surfaceRadius) return new(RayFate.HitSurface, cameraPosition, d, 0);
        if (r < rCapture) return new(RayFate.Captured, cameraPosition, d, 0);
        if (r > escapeRadius) return new(RayFate.Escaped, cameraPosition, d, 0);
        if (!InitialMomentum(a, x, y, z, d, out double px, out double py, out double pz))
            return new(RayFate.Captured, cameraPosition, d, 0);

        // Plan Task 10 (ruling P3-8): the first outward crossing of the grid sphere, between the previous point and this one.
        double rPrev = -1, xPrev = x, yPrev = y, zPrev = z;
        Vec3? grid = null;
        for (int steps = 0; ; steps++)
        {
            Derivatives(a, x, y, z, px, py, pz, out double k1x, out double k1y, out double k1z,
                        out double k1px, out double k1py, out double k1pz, out r);
            if (grid is null && rPrev >= 0 && rPrev < GridRadius && r >= GridRadius)
            {
                double u = (GridRadius - rPrev) / (r - rPrev);
                grid = new Vec3(xPrev + u * (x - xPrev), yPrev + u * (y - yPrev), zPrev + u * (z - zPrev));
            }
            // Plan Task 11 (class notes, the disk item): the thin disk, where the step crossed the equatorial plane inside the annulus.
            if (rPrev >= 0 && diskOuter > 0 && zPrev * z < 0)
            {
                double u = zPrev / (zPrev - z);
                double xc = xPrev + u * (x - xPrev), yc = yPrev + u * (y - yPrev);
                double rc = KerrSchildR(a * a, xc, yc, 0);
                if (rc >= diskInner && rc <= diskOuter)
                {
                    double ell = -(x * py - y * px);
                    return new(RayFate.HitDisk, new Vec3(xc, yc, 0), new Vec3(k1x, k1y, k1z).Normalized(), steps)
                    {
                        GridCrossing = grid, DiskHit = (rc, DiskRedshift(spin, rc, ell)),
                    };
                }
            }
            RayFate fate;
            if (surfaceRadius > 0 && r < surfaceRadius) fate = RayFate.HitSurface;
            else if (r < rCapture) fate = RayFate.Captured;
            else if (r > escapeRadius) fate = RayFate.Escaped;
            else if (steps >= maxSteps) fate = RayFate.StepLimit;
            else
            {
                (rPrev, xPrev, yPrev, zPrev) = (r, x, y, z);
                Rk4Step(a, StepSize(r, rPlus, floor), ref x, ref y, ref z, ref px, ref py, ref pz,
                        k1x, k1y, k1z, k1px, k1py, k1pz);
                continue;
            }
            return new(fate, new Vec3(x, y, z), new Vec3(k1x, k1y, k1z).Normalized(), steps) { GridCrossing = grid };
        }
    }

    /// <summary>Ruling P3-8: the grid sphere's radius (GM/c²) and its lines' spacing in latitude and longitude (degrees).</summary>
    public const double GridRadius = 50, GridStepDeg = 10;

    // ---- Plan Task 11: the accretion disk's light (spec §6.2: "gravitational redshift and the Doppler brightening ... are exact") ----

    /// <summary>
    /// The angular velocity dφ/dt of a prograde circular equatorial orbit of radius r about a hole of spin a (M = 1):
    /// Ω = 1/(r^{3/2} + a) (Bardeen, Press &amp; Teukolsky 1972, ApJ 178, 347, eq. 2.16). The same in Boyer–Lindquist and Kerr–Schild
    /// time, which differ by a function of r only.
    /// </summary>
    public static double CircularOrbitOmega(double spin, double r) => 1 / (r * Math.Sqrt(r) + spin);

    /// <summary>
    /// u^t = dt/dτ of that orbit: (r^{3/2} + a)/(r^{3/4} √(r^{3/2} − 3 r^{1/2} + 2a)), M = 1: Page &amp; Thorne 1974 (ApJ 191, 499)'s
    /// u^t = ℬ/√𝒞 with ℬ = 1 + a r^{−3/2}, 𝒞 = 1 − 3/r + 2a r^{−3/2}, derived from Bardeen, Press &amp; Teukolsky 1972's E and L (eq. 2.12).
    /// Infinite (no circular orbit) where the root's argument is not positive: inside the prograde photon orbit.
    /// </summary>
    public static double CircularOrbitUt(double spin, double r)
    {
        double sr = Math.Sqrt(r);
        double arg = r * sr - 3 * sr + 2 * spin;
        return arg > 0 ? (r * sr + spin) / (Math.Sqrt(r * sr) * Math.Sqrt(arg)) : double.PositiveInfinity;
    }

    /// <summary>
    /// The redshift factor g = ν_received/ν_emitted of light from gas on a prograde circular equatorial orbit at r, reaching an observer at
    /// rest far away: g = 1/(u^t (1 − Ω ℓ)), with ℓ = L/E the photon's conserved angular momentum about the spin axis per unit energy (the
    /// photon's energy measured by the gas is E u^t (1 − Ω ℓ), and E far away; Cunningham 1975, ApJ 202, 788; Luminet 1979, A&amp;A 75,
    /// 228). It holds the gravitational redshift and the Doppler shift together, both exact: 1/u^t = √(1 − 3/r) for ℓ = 0 without spin.
    /// 0 where there is no circular orbit. [The Close-up's camera is a coordinate camera close in (#65); g is the shift to a distant
    /// observer, as the readouts' sizes are.]
    /// </summary>
    public static double DiskRedshift(double spin, double r, double ell)
    {
        double ut = CircularOrbitUt(spin, r);
        if (!double.IsFinite(ut)) return 0;
        return 1 / (ut * (1 - CircularOrbitOmega(spin, r) * ell));
    }

    /// <summary>
    /// Ruling P3-8: how far a point on the grid sphere is from the nearest latitude line (in degrees of the polar angle θ, from the spin
    /// axis) and from the nearest longitude line (in degrees of the chart's azimuth φ), both every <see cref="GridStepDeg"/>. lensing.frag
    /// mirrors it and turns the distances into lines a pixel or so wide.
    /// </summary>
    public static (double LatitudeDeg, double LongitudeDeg) GridLineDistanceDeg(Vec3 point)
    {
        double r = point.Length;
        double theta = r > 0 ? Math.Acos(Math.Clamp(point.Z / r, -1, 1)) * 180 / Math.PI : 90;
        double phi = Math.Atan2(point.Y, point.X) * 180 / Math.PI;
        static double ToLine(double deg)
        {
            double m = deg - GridStepDeg * Math.Floor(deg / GridStepDeg);   // 0 to 10
            return Math.Min(m, GridStepDeg - m);
        }
        return (ToLine(theta), ToLine(phi));
    }

    /// <summary>
    /// For tests: H = ½ g^μν p_μ p_ν at the camera and after <paramref name="steps"/> tracer steps, or at capture
    /// (r &lt; r₊ + 0.01) if that comes first; no other fate tests.
    /// </summary>
    internal static (double Start, double End) HamiltonianDrift(double spin, Vec3 cameraPosition, Vec3 direction, int steps)
    {
        double a = -spin, rPlus = HorizonRadius(spin), floor = StepFloor(spin);
        double x = cameraPosition.X, y = cameraPosition.Y, z = cameraPosition.Z;
        if (!InitialMomentum(a, x, y, z, direction.Normalized(), out double px, out double py, out double pz))
            return (double.NaN, double.NaN);
        double h0 = Hamiltonian(a, x, y, z, px, py, pz);
        for (int k = 0; k < steps; k++)
        {
            Derivatives(a, x, y, z, px, py, pz, out double k1x, out double k1y, out double k1z,
                        out double k1px, out double k1py, out double k1pz, out double r);
            if (r < rPlus + CaptureMargin) break;
            Rk4Step(a, StepSize(r, rPlus, floor), ref x, ref y, ref z, ref px, ref py, ref pz, k1x, k1y, k1z, k1px, k1py, k1pz);
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

    /// <summary>
    /// Ruling R-P3-6 (class notes, item 4): where the straight line from a far camera <paramref name="c"/> along unit
    /// <paramref name="d"/> first meets the sphere |x| = <see cref="FarStartRadius"/> (true), or, when its forward half
    /// misses it, the direction after the analytic weak-field deflection toward the hole (false).
    /// </summary>
    private static bool FarStart(Vec3 c, Vec3 d, out Vec3 onSphere, out Vec3 bent)
    {
        double cd = c.Dot(d), c2 = c.Dot(c);
        Vec3 perp = c - cd * d;                   // from the hole to the line's closest point
        double b = perp.Length;
        if (cd < 0 && b < FarStartRadius)
        {
            onSphere = c + (-cd - Math.Sqrt(FarStartRadius * FarStartRadius - b * b)) * d;
            bent = d;
            return true;
        }
        double cosG = cd / Math.Sqrt(c2);
        double delta = cosG > 0 ? 2 * b / (c2 * (1 + cosG)) : 2 / b * (1 - cosG);
        onSphere = c;
        bent = b > 0 ? (Math.Cos(delta) * d - Math.Sin(delta) / b * perp).Normalized() : d;
        return false;
    }

    /// <summary>Inconsistency #64's fix: the step floor is this many times the gap r_ph+ − r₊ between the prograde photon orbit and the horizon.</summary>
    public const double StepFloorPerPhotonGap = 16;

    /// <summary>
    /// The step rule's floor (ruling P3-7's 0.5; inconsistency #64, Task 10 fix round 1): min(0.5, 16 (r_ph+ − r₊)), never below 0.02,
    /// with r_ph+ = 2 [1 + cos(⅔ arccos(−a))]. It is 0.5 for a below about 0.987 (every spin the lab's model gives), and shrinks near a = 1,
    /// where the prograde photon orbit closes in on the horizon (0.011 above it at a = 0.998) and a step of 0.026 let RK4's error carry
    /// rays skimming that orbit below the capture radius. lensing.frag mirrors it.
    /// </summary>
    public static double StepFloor(double spin)
    {
        double a = Math.Min(Math.Abs(spin), 1);
        double gap = 2 * (1 + Math.Cos(2.0 / 3 * Math.Acos(-a))) - HorizonRadius(a);
        return Math.Clamp(StepFloorPerPhotonGap * gap, 0.02, 0.5);
    }

    /// <summary>h = 0.05 · max(r − r₊, floor) · (1 + r/20), capped at 2.0 (ruling P3-7; floor <see cref="StepFloor"/>, 0.5 below a ≈ 0.987).</summary>
    private static double StepSize(double r, double rPlus, double floor)
        => Math.Min(0.05 * Math.Max(r - rPlus, floor) * (1 + r / 20), MaxStep);

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
    /// p_i for a future-directed null ray leaving (x, y, z) along unit d, scaled to p_t = −1 (class notes, item 5).
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
