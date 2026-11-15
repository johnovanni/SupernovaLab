namespace SupernovaLab.Tests;

/// <summary>
/// The research numbers the Project 3 tests use, copied from docs/research/compact.md (normative), one static class
/// per section (ruling S2). Each constant cites its section; later tasks add their sections here (C1, C5, C8, …).
/// Lengths are in GM/c² unless the name says otherwise.
/// </summary>
public static class CompactResearch
{
    /// <summary>compact.md §C7, black-hole optics (exact results the tests use).</summary>
    public static class C7
    {
        /// <summary>§C7 Schwarzschild: critical impact parameter b_c = √27 M = 5.196152 M.</summary>
        public const double ShadowRadius = 5.196152;
        /// <summary>Spec §1 criterion 2: the non-spinning shadow radius within 0.5%.</summary>
        public const double ShadowFractionalTolerance = 0.005;
        /// <summary>§C7 Schwarzschild: photon sphere r = 3M.</summary>
        public const double PhotonSphere = 3;

        /// <summary>§C7: 10 M☉ shadow diameter 153.49 km; §C7 App: test 152.9–154.0.</summary>
        public const double TenSolarMassShadowKm = 153.49;
        public const double TenSolarMassShadowKmLow = 152.9;
        public const double TenSolarMassShadowKmHigh = 154.0;
        /// <summary>§C7 Units / App: GM☉/c² = 1.47700 km with the lab's constants.</summary>
        public const double GravitationalRadiusKmPerMsun = 1.47700;

        /// <summary>§C7 ISCO table (Bardeen, Press &amp; Teukolsky 1972): spins.</summary>
        public static readonly double[] IscoSpins = [0, 0.1, 0.5, 0.9, 0.95, 0.998, 1];
        /// <summary>§C7 ISCO table: prograde r_ISCO for <see cref="IscoSpins"/>.</summary>
        public static readonly double[] IscoPrograde = [6.0000, 5.6693, 4.2330, 2.3209, 1.9372, 1.2370, 1.0000];
        /// <summary>§C7 ISCO table: retrograde r_ISCO for <see cref="IscoSpins"/>.</summary>
        public static readonly double[] IscoRetrograde = [6.0000, 6.3229, 7.5546, 8.7174, 8.8590, 8.9944, 9.0000];
        /// <summary>§C7 ISCO table: horizon r₊ for <see cref="IscoSpins"/> (a = 0.9 gives 1.4359).</summary>
        public static readonly double[] Horizon = [2.0000, 1.9950, 1.8660, 1.4359, 1.3122, 1.0632, 1.0000];
        /// <summary>The tables are given to 4 decimals.</summary>
        public const double TableTolerance = 0.00006;

        /// <summary>§C7 Kerr outline check values: the spin they are for.</summary>
        public const double OutlineSpin = 0.9;
        /// <summary>§C7 Kerr outline check values at a = 0.9: (i in degrees, α min, α max, β max).</summary>
        public static readonly (double IncDeg, double AlphaMin, double AlphaMax, double BetaMax)[] OutlineRows =
        [
            (90, -2.8444, 6.8316, 5.1962),
            (60, -3.0957, 6.5892, 5.1355),
            (17, -4.3025, 5.5023, 4.9450),
        ];
        /// <summary>§C7 sample outline points (a = 0.9, i = 90°), (α, β), at r = 2.0, 2.5, 3.0, 3.5.</summary>
        public static readonly (double Alpha, double Beta)[] OutlineSamples90 =
            [(-1.7444, 3.4996), (-0.2148, 4.7349), (1.8000, 5.1962), (4.3422, 4.4754)];
        /// <summary>
        /// Tolerance for <see cref="OutlineRows"/>. §C7 sampled 20,000 points uniformly in r and kept real β, so its α
        /// extremes (at the β = 0 ends) sit up to 1.3 × 10⁻³ inside the exact ends found by root refinement: at
        /// i = 17° the exact α range is −4.3032 … 5.5036 (table −4.3025 … 5.5023); at i = 90° η(r_ph−) rounds to
        /// −8 × 10⁻¹⁵, dropping the end sample, so the table's 6.8316 is short of the exact −ξ(r_ph−) = 6.8323.
        /// </summary>
        public const double OutlineTableTolerance = 0.002;
        /// <summary>Spec criterion 2 and §C7 App: the Kerr outline within 1% of the shadow radius (5.2).</summary>
        public const double OutlineTolerance = 0.01 * 5.2;
        /// <summary>§C7: the Kerr shadow's radius "ranges from 4.8 GM/c² to 5.2 GM/c²" (EHT Sgr A* Paper VI).</summary>
        public const double KerrShadowRadiusLow = 4.8;
        public const double KerrShadowRadiusHigh = 5.2;

        /// <summary>§C7 weak field: Δφ = 4M/b; b = 1,000 M gives 0.004 rad.</summary>
        public const double WeakFieldImpactParameter = 1000;
        public const double WeakFieldDeflectionRad = 0.004;
        /// <summary>Plan Task 3: the tracer's weak-field deflection within 1%.</summary>
        public const double WeakFieldFractionalTolerance = 0.01;

        /// <summary>§C7 visible-fraction table, R = 12 km: (M in M☉, exact f). 1.4 M☉ gives 0.7549.</summary>
        public static readonly (double MassMsun, double Fraction)[] VisibleFractions12Km =
            [(1.2, 0.7052), (1.4, 0.7549), (2.2, 0.9879)];
        /// <summary>§C7 visible-fraction table, R = 12 km: ψ_max in degrees for 1.2, 1.4 and 2.2 M☉.</summary>
        public static readonly (double MassMsun, double PsiMaxDeg)[] PsiMax12Km =
            [(1.2, 114.23), (1.4, 120.65), (2.2, 167.39)];
        /// <summary>§C7 App: the 1.4 M☉, 12 km test range 0.74–0.78.</summary>
        public const double VisibleFraction14Low = 0.74;
        public const double VisibleFraction14High = 0.78;
    }

    /// <summary>compact.md §C9, observational checks (C9.1, the Event Horizon Telescope; App block).</summary>
    public static class C9
    {
        /// <summary>§C9 App: M87 independent test uses the stellar-dynamics mass (Gebhardt et al. 2011), M☉.</summary>
        public const double M87StellarDynamicsMassMsun = 6.14e9;
        /// <summary>§C9.1: M87 distance 16.8 Mpc.</summary>
        public const double M87DistanceMpc = 16.8;
        /// <summary>§C9.1: M87 ring diameter 42 ± 3 µas (EHT 2019 Paper VI).</summary>
        public const double M87RingMicroArcsec = 42;
        /// <summary>§C9 App: tolerance 3 µas (EHT's 1σ).</summary>
        public const double TolM87 = 3;
        /// <summary>§C9.1 / App: ring-to-shadow factor from M87, 11.0/(6√3) = 1.06.</summary>
        public const double RingToShadow = 1.06;
        /// <summary>§C9 App: the M87 ring from the stellar-dynamics mass is 39.7 µas.</summary>
        public const double M87RingFromStellarMassMicroArcsec = 39.7;

        /// <summary>§C9.1: GRAVITY's Sgr A* mass 4.297 × 10⁶ M☉.</summary>
        public const double SgrAMassMsun = 4.297e6;
        /// <summary>§C9.1: GRAVITY's R₀ = 8,277 pc.</summary>
        public const double SgrADistanceKpc = 8.277;
        /// <summary>§C9.1: EHT's θ_g = 5.125 µas (VLTI).</summary>
        public const double SgrAThetaGVltiMicroArcsec = 5.125;
        /// <summary>§C9.1: fractional shadow deviation δ = −0.08 ± 0.09 (VLTI).</summary>
        public const double SgrADeltaVlti = -0.08;
        public const double SgrADeltaVltiSigma = 0.09;
        /// <summary>§C9 table / App: the shadow law gives 53.27 µas for Sgr A* (GRAVITY).</summary>
        public const double SgrAShadowMicroArcsec = 53.27;
        /// <summary>§C9 App: the EHT-inferred shadow window 6√3 θ_g (1 + δ), δ ∈ [−0.17, +0.01], is 44.2–53.8 µas.</summary>
        public const double SgrAShadowWindowLow = 44.2;
        public const double SgrAShadowWindowHigh = 53.8;

        /// <summary>§C9.1 "From Earth": a 10 M☉ hole at 10 kpc has a 0.103 nano-arcsecond shadow.</summary>
        public const double TenSolarMassAt10KpcShadowNanoArcsec = 0.103;
        /// <summary>§C9.1: "a 20 µas beam, the nominal resolution of the EHT".</summary>
        public const double EhtBeamMicroArcsec = 20;
        /// <summary>§C9.1 "From Earth": 1.95 × 10⁵ times smaller than 20 µas.</summary>
        public const double TenSolarMassTimesTooSmall = 1.95e5;
    }
}
