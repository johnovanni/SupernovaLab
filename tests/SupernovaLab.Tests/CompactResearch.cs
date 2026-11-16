using SupernovaLab.Core.Compact;

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

    /// <summary>compact.md §C3, early cooling (≈ 1 minute to 30 yr).</summary>
    public static class C3
    {
        /// <summary>§C3 App: the early band starts at 60 s.</summary>
        public const double EarlyStartS = 60;
        /// <summary>§C3 table (1.4 M☉, 12 km): T_s^∞ at 60 s, 10.38 MK.</summary>
        public const double At60sMK = 10.38;
        /// <summary>§C3 App check values (1.4 M☉, 12 km), MK: 1 yr 3.11 [2.04, 3.11].</summary>
        public const double At1yrMK = 3.11;
        /// <summary>§C3 App check: 3 yr 2.81 [2.04, 2.90]; the band's top is Gnedin's plateau, 2.9 MK.</summary>
        public const double At3yrMK = 2.81, PlateauMK = 2.9;
        /// <summary>§C3 App check: 10 yr 2.52 [2.04, 2.90].</summary>
        public const double At10yrMK = 2.52;
        /// <summary>§C3 App check: 30 yr 2.276 [2.04, 2.276]: the model's own band, so the join is exact.</summary>
        public const double At30yrMK = 2.276, At100yrMK = 2.04;
    }

    /// <summary>compact.md §C4, pulsars (beam, magnetosphere, inclinations and proper motions).</summary>
    public static class C4
    {
        /// <summary>§C4 App: ρ = 5.6° P^(−1/2) (Biggs 1990; #59), capped at 90°.</summary>
        public const double BeamCoefficientDeg = 5.6;
        /// <summary>§C4.1: ρ values, deg, for the Crab (33.4 ms), 0.1 s, 1 s, 5 s.</summary>
        public const double CrabBeamDeg = 30.6, Beam0p1sDeg = 17.7, Beam1sDeg = 5.6, Beam5sDeg = 2.5;
        /// <summary>§C4 App: the light cylinder at 33.4 ms is 1,593.6 km.</summary>
        public const double CrabLightCylinderKm = 1593.6;
        /// <summary>§C4 App: the magnetosphere forms at 30 s [APPROX].</summary>
        public const double MagnetosphereFormsS = 30;
        /// <summary>§C4.3 / App: the Crab's magnetic inclination α = 62° (Romani &amp; Yadigaroglu 1995).</summary>
        public const double CrabInclinationDeg = 62;
        /// <summary>§C4 App: the Crab's kick position angle, −14.3° from the projected spin axis; Cas A's is 151°.</summary>
        public const double CrabKickPositionAngleDeg = -14.3, CasAKickPositionAngleDeg = 151;
        /// <summary>§C4 App: with the checkbox on (ζ = α = 62°) the Crab's kick is d = (+0.247, +0.455, +0.856).</summary>
        public static readonly (double X, double Y, double Z) CrabKickDirection = (0.247, 0.455, 0.856);
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
        /// <summary>§C9.3 / R-P3-1: seen from afar the Cas A neutron star is T_s^∞ ≈ 1.4–1.5 MK (1.37–1.54 MK); the lab at 340 yr is 20–30% warm (#54).</summary>
        public const double CasAObservedFarLowMK = 1.4, CasAObservedFarHighMK = 1.5;
        /// <summary>§C9.3: the lab at 340 yr, 12 km, 1.4 M☉: 1.82 MK seen from afar.</summary>
        public const double CasALabAt340yrMK = 1.82;
    }

    /// <summary>compact.md §C5, the neutron star's layers (1.2–2.2 M☉, the lab's fixed 12 km).</summary>
    public static class C5
    {
        /// <summary>§C5.1 / App: drip density 4.3 × 10¹¹ g/cm³; the plan's range 4.0–4.4 × 10¹¹.</summary>
        public const double DripDensityGcm3 = 4.3e11, DripDensityLow = 4.0e11, DripDensityHigh = 4.4e11;
        /// <summary>§C5.2 table (Eq. 8): (M☉, drip depth km, crust km, crust/R). Accuracy under 1% of the crust.</summary>
        public static readonly (double Mass, double DripKm, double CrustKm, double CrustOverR)[] Table =
        [
            (1.2, 0.531, 1.220, 0.102),
            (1.4, 0.427, 0.993, 0.083),
            (1.6, 0.348, 0.817, 0.068),
            (1.8, 0.286, 0.676, 0.056),
            (2.0, 0.236, 0.560, 0.047),
            (2.2, 0.194, 0.464, 0.039),
        ];
        /// <summary>§C5.2: the table's tolerance on lengths (km): its 3 decimals plus the 1% Eq. 8 accuracy.</summary>
        public const double LengthToleranceKm = 0.01;
        /// <summary>§C5 App: DripDepthKm(1.4, 12) test range 0.40–0.47 km (Eq. 8 to Hempel &amp; Schaffner-Bielich).</summary>
        public const double DripDepth14Low = 0.40, DripDepth14High = 0.47;
        /// <summary>§C5 App: drip depth over 2.2–1.2 M☉: 0.19–0.53 km.</summary>
        public const double DripDepthLowKm = 0.19, DripDepthHighKm = 0.53;
        /// <summary>§C5 App: crust 8.3% of the radius at 1.4 M☉ (10.2% at 1.2, 3.9% at 2.2).</summary>
        public const double Crust14Fraction = 0.083, Crust12Fraction = 0.102, Crust22Fraction = 0.039;
        /// <summary>§C5 App: the crust forms at 86,400 s (1 day) [D/APPROX]; range 1 h – 1 month.</summary>
        public const double CrustFormationTimeS = 86_400;
        /// <summary>§C5 App: pasta is 0.30 of the inner crust's thickness; the inner core ends at 0.5 R_cc.</summary>
        public const double PastaFractionOfInnerCrust = 0.30, InnerCoreFractionOfCoreRadius = 0.5;
    }

    /// <summary>
    /// compact.md §C1, the first second (C1.2's typical tracks, the App block's rules). The rows before 30 ms are [APPROX] stand-ins
    /// assembled from the anchors (no published track at those times); the rows marked ✓ in C1.2 are [V] anchors.
    /// </summary>
    public static class C1
    {
        /// <summary>One C1.2 row: time after bounce (s), then low–high ranges (km) of R_sh, R_gain (NaN: none) and R_PNS.</summary>
        public readonly record struct Row(double TimeS, double ShockLow, double ShockHigh, double GainLow, double GainHigh,
            double PnsLow, double PnsHigh, bool Approx);

        /// <summary>§C1.2 exploding core (neutrino-driven; revival ≈ 0.3 s). 1–10 ms are [APPROX] stand-ins.</summary>
        public static readonly Row[] Exploding =
        [
            new(0.001, 10, 25, double.NaN, double.NaN, 50, 90, true),
            new(0.003, 25, 60, double.NaN, double.NaN, 50, 90, true),
            new(0.010, 60, 110, double.NaN, double.NaN, 50, 85, true),
            new(0.030, 90, 140, 50, 90, 45, 80, false),
            new(0.100, 110, 170, 60, 110, 35, 65, false),
            new(0.200, 100, 220, 55, 120, 28, 50, false),
            new(0.300, 150, 500, 40, 100, 24, 45, false),
            new(0.500, 400, 3000, 28, 70, 22, 37, false),
            new(1.000, 1400, 13000, 25, 60, 20, 30, false),
        ];

        /// <summary>§C1.2 failed core after 100 ms: (fraction of t_BH, ranges); the last row is t_BH − 1 ms (fraction NaN).</summary>
        public static readonly (double FractionOfTbh, double ShockLow, double ShockHigh, double GainLow, double GainHigh, double PnsLow, double PnsHigh)[] FailedLate =
        [
            (0.4, 70, 150, 40, 100, 30, 50),
            (0.7, 40, 110, 25, 70, 22, 40),
            (0.95, 20, 90, double.NaN, double.NaN, 15, 30),
            (double.NaN, 12, 25, double.NaN, double.NaN, 12, 20),
        ];

        /// <summary>§C1 App: a failed track uses the exploding rows up to 100 ms (or below 0.4 t_BH when t_BH &lt; 0.25 s).</summary>
        public const double FailedJoinS = 0.1;

        /// <summary>§C1 App: the collapse to the horizon is drawn over the last 1 ms before t_BH.</summary>
        public const double CollapseS = 0.001;

        /// <summary>
        /// The shock rows a track must pass through (time, low, high): the exploding rows for an exploding or magnetorotational
        /// track (those before t_BH − 1 ms when a black hole forms by fallback at <paramref name="tBhS"/>), or the failed rows
        /// stretched to <paramref name="tBhS"/>.
        /// </summary>
        public static IEnumerable<(double T, double Lo, double Hi)> ShockTable(CoreTrack track, double tBhS = double.NaN)
            => Rows(track, tBhS).Select(r => (r.TimeS, r.ShockLow, r.ShockHigh));

        /// <summary>The PNS rows (time, low, high), as <see cref="ShockTable"/>.</summary>
        public static IEnumerable<(double T, double Lo, double Hi)> ProtoNsTable(CoreTrack track, double tBhS = double.NaN)
            => Rows(track, tBhS).Select(r => (r.TimeS, r.PnsLow, r.PnsHigh));

        /// <summary>The gain rows that have a gain radius (time, low, high), as <see cref="ShockTable"/>.</summary>
        public static IEnumerable<(double T, double Lo, double Hi)> GainTable(CoreTrack track, double tBhS = double.NaN)
            => Rows(track, tBhS).Where(r => !double.IsNaN(r.GainLow)).Select(r => (r.TimeS, r.GainLow, r.GainHigh));

        private static IEnumerable<Row> Rows(CoreTrack track, double tBhS)
        {
            if (track != CoreTrack.Failed)
                return Exploding.Where(r => double.IsNaN(tBhS) || r.TimeS < tBhS - CollapseS);
            var early = Exploding.Where(r => r.TimeS <= FailedJoinS && r.TimeS < 0.4 * tBhS);
            var late = FailedLate.Select(f => new Row(double.IsNaN(f.FractionOfTbh) ? tBhS - CollapseS : f.FractionOfTbh * tBhS,
                f.ShockLow, f.ShockHigh, f.GainLow, f.GainHigh, f.PnsLow, f.PnsHigh, false));
            return early.Concat(late);
        }

        /// <summary>§C1 App: revival at 0.3 s for an iron core, 0.07 s for an O-Ne-Mg (electron-capture) core; Stalled from 30 ms.</summary>
        public const double RevivalIronS = 0.3, RevivalOneMgS = 0.07, StalledFromS = 0.03;

        /// <summary>§C1 App: the stage ends when the typical R_sh passes 3,000 km (≈ 1 s), or at t_BH for the failed track.</summary>
        public const double StageEndShockKm = 3000, ExplodingStageEndS = 1.0;

        /// <summary>§C1 App: the drawn PNS is at most 0.9 R_sh (the hot core is bounded by the shock in the first ≈ 5 ms).</summary>
        public const double DrawnProtoNsMaxOfShock = 0.9;

        /// <summary>§C1 App: in the failed track the gain shell is drawn only while R_gain &gt; 1.1 R_PNS.</summary>
        public const double FailedGainMinOfProtoNs = 1.1;

        /// <summary>§C1 App: DistortionFraction 0.1 [APPROX] (Hanke et al. 2013's figure ranges).</summary>
        public const double DistortionFraction = 0.1;

        /// <summary>§C1.1 / App: the infall speed √(G M / r): 33,900–36,400 km/s at 150 km for 1.3–1.5 M☉.</summary>
        public const double InfallAt150KmLow = 33_900, InfallAt150KmHigh = 36_400;

        /// <summary>§C1 App: pre-collapse core radii, km: an iron core 2,300 (MacFadyen &amp; Woosley 1999); O-Ne-Mg 1,100 (Kitaura et al. 2006).</summary>
        public const double IronCoreKm = 2300, OneMgCoreKm = 1100;

        /// <summary>§C1.1: in the last 1 ms before t_BH the shock recedes to ≈ 12 km (O'Connor &amp; Ott 2011).</summary>
        public const double ShockAtCollapseKm = 12;
    }

    /// <summary>compact.md §C2, proto-neutron-star contraction (≈ 1 s to ≈ 1 minute).</summary>
    public static class C2
    {
        /// <summary>§C2 [D]: R(t) for R_f = 12 km: (t s, km). 25.0 (1 s), 16.0 (2 s), 14.0 (3 s), 12.8 (5 s), 12.3 (10 s), 12.04 (30 s), 12.01 (60 s).</summary>
        public static readonly (double T, double Km)[] Radius12 =
            [(1, 25.0), (2, 16.0), (3, 14.0), (5, 12.8), (10, 12.3), (30, 12.04), (60, 12.01)];

        /// <summary>§C2's table is given to the decimals shown: within 0.05 km.</summary>
        public const double ToleranceKm = 0.05;

        /// <summary>§C2 App: R(60 s) within 1% of R_f (the plan's 11.9–12.1 km).</summary>
        public const double At60Low = 11.9, At60High = 12.1;

        /// <summary>§C2 App: a fallback black hole at t_BH = 3.0 s has a PNS of 14.0 km just before (R_f = 12 km).</summary>
        public const double FallbackAt3sKm = 14.0;
    }
}
