using System.Globalization;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Compact;

/// <summary>How a black hole was made, which sets how it settles (spec §6.4; compact §C8 App).</summary>
public enum BlackHoleBirth
{
    /// <summary>The core collapsed without exploding (a failed supernova, a pulsational pair-instability collapse): no kick.</summary>
    DirectCollapse,
    /// <summary>A collapsar (an outcome with a gamma-ray burst): its disk feeds and spins up the hole for about 15 s.</summary>
    Collapsar,
    /// <summary>An explosion whose fallback tipped the proto-neutron star over: it settles once the fallback ends.</summary>
    Fallback,
}

/// <summary>
/// The black hole at one source time (spec §6.1–§6.5). <see cref="MassMsun"/> and <see cref="Spin"/> are the hole's own (the readouts):
/// the mass grows from <see cref="FormationMassMsun"/> to the outcome's during the settling (inconsistency #63), and a collapsar's
/// spin climbs during its spin-up. <see cref="SpinQuantity"/> is the spin with its confidence (always Speculative) and range.
/// <see cref="SettledFraction"/> is how far the settling has gone (0 at t_BH, 1 once settled). <see cref="Forming"/> is true for
/// the first <see cref="BlackHoleModel.FormingS"/> after t_BH. <see cref="HasDisk"/> and <see cref="DiskOuterGm"/> (the disk's outer
/// edge in the hole's GM/c² now) are the collapsar's accretion disk (§C8; drawn from Task 11). <see cref="OffsetCm"/> is the drift
/// from the explosion's centre (<see cref="KickDrift"/>).
/// <para>What the picture uses: <see cref="DrawnSpin"/> and <see cref="DrawnMassMsun"/>, the settling stepped in
/// <see cref="BlackHoleModel.TransitionSteps"/> steps, so the cached ray map is retraced a bounded number of times however finely the
/// time moves (plan Task 10's budget). For a direct collapse or a fallback hole the drawn spin is <see cref="Spin"/> ×
/// <see cref="SettledFraction"/> (the fade from the non-spinning image into the spinning one, a visual transition); for a collapsar it
/// is the spin as it climbs (the picture follows the spin-up as it happens).</para>
/// </summary>
public sealed record BlackHoleState(double MassMsun, double Spin, Quantity SpinQuantity, double SettledFraction,
    bool Forming, bool HasDisk, double DiskOuterGm, Vec3 OffsetCm)
{
    /// <summary>How the hole was made.</summary>
    public BlackHoleBirth Birth { get; init; }

    /// <summary>The spin the lensing pass and the overlays draw (stepped; see the record's notes).</summary>
    public double DrawnSpin { get; init; }

    /// <summary>The mass the lensing pass and the overlays draw (stepped like <see cref="DrawnSpin"/>), M☉.</summary>
    public double DrawnMassMsun { get; init; }

    /// <summary>The mass at formation: the heaviest neutron star's gravitational mass, where the first second's collapse ends (M☉).</summary>
    public double FormationMassMsun { get; init; }

    /// <summary>The outcome's black-hole mass, reached once settled (M☉).</summary>
    public double FinalMassMsun { get; init; }

    /// <summary>The source time (s after bounce) and the hole's formation time t_BH.</summary>
    public double SourceTimeS { get; init; }
    public double FormationTimeS { get; init; }

    /// <summary>Whether the settling is over (the caption "Settled: spinning black hole").</summary>
    public bool IsSettled => SettledFraction >= 1;
}

/// <summary>
/// The black hole's formation, settling and spin (spec §6.4; compact §C8, §C9; plan rulings P3-11, P3-13). Pure functions of the outcome.
/// <list type="bullet">
/// <item><b>Spin</b> (§C8 App, all Speculative: the lab has no birth-spin model): Rotation Slow 0.1, Fast 0.5, Extreme 0.9; any outcome
/// with a gamma-ray burst 0.9. A collapsar starts at 0.5 (MacFadyen &amp; Woosley 1999's a_init) and spins up over 15 s as
/// a(Δt) = a₀ + (a₁ − a₀) ln(1 + Δt/0.3 s)/ln(1 + 15 s/0.3 s) [APPROX: MW99's shape].</item>
/// <item><b>Settling:</b> from t_BH to <see cref="SettleEndS"/>: a direct collapse over 1/3 decade of log source time (P3-11, a visual
/// transition); a collapsar with its spin-up; a fallback hole to 1,000 s (§C8 [APPROX]: "minutes to hours"), linearly in log time.</item>
/// <item><b>Mass</b> (inconsistency #63): the first second's collapse ends at the horizon of the heaviest neutron star (2.2 M☉), while
/// the outcome's hole is heavier (it grows as the rest of the star falls in; the lab has no accretion history). So the hole is drawn
/// from that formation mass, growing to the outcome's mass with the settling's progress: for a collapsar this is MW99's picture (the
/// mass and spin "grow rapidly in the first second", 0.07 M☉/s for about 15 s); for the others the growth's timing is schematic.</item>
/// </list>
/// </summary>
public static class BlackHoleModel
{
    /// <summary>The plan's exact caption once the hole has settled (spec §6.4).</summary>
    public const string SettledCaption = "Settled: spinning black hole";

    /// <summary>§C8 App: the settled spins (ruling P3-13, confirmed by the research).</summary>
    public const double SlowSpin = 0.1, FastSpin = 0.5, ExtremeSpin = 0.9, GrbSpin = 0.9;

    /// <summary>§C8 App: the collapsar's starting spin (MW99's a_init ≈ 0.5).</summary>
    public const double CollapsarStartSpin = 0.5;

    /// <summary>§C8 App: the spin-up lasts 15 s (MW99's ≈ 15 s of accretion) on a 0.3 s log scale [APPROX].</summary>
    public const double SpinUpS = 15, SpinUpScaleS = 0.3;

    /// <summary>§C8 App: a fallback hole settles at 1,000 s [APPROX: "minutes to hours", MacFadyen, Woosley &amp; Heger 2001].</summary>
    public const double FallbackSettleS = 1000;

    /// <summary>Plan ruling P3-11: the direct-collapse fade, in decades of source time after t_BH.</summary>
    public const double DirectFadeDecades = 1.0 / 3;

    /// <summary>§C8 App: the collapsar disk's outer edge (MW99's 200 km torus) and when it exists (t_BH + 1 s to t_BH + 15 s).</summary>
    public const double DiskOuterKm = 200, DiskFromS = 1, DiskToS = 15;

    /// <summary>
    /// The formation (spec §6.4: the new hole rings down to a smooth Kerr state "within milliseconds"; §C8: the quasi-normal period is
    /// ~0.5 ms for 10 M☉): the Close-up's forming era lasts this long after t_BH (s). The ring-down itself is not drawn (spec §9).
    /// </summary>
    public const double FormingS = 1e-3;

    /// <summary>
    /// Plan Task 10's retrace budget [presentation]: the drawn spin and mass move in this many equal steps of the settling's progress,
    /// so the light-bending ray map (which depends on them) is retraced at most this many times during a settling, plus the camera's own
    /// moves. At 24 steps a direct collapse's ≈ 1 s fade retraces about 24 times a second at the moving level (as an orbiting camera
    /// does), and the spin and shadow change by at most 1/24 of their change per step.
    /// </summary>
    public const int TransitionSteps = 24;

    /// <summary>§C9.1: "a 20 µas beam, the nominal resolution of the EHT" (EHT Sgr A* Paper VI).</summary>
    public const double EhtResolutionMicroArcsec = 20;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>The spin's source (§C8 App).</summary>
    public static readonly Source SpinSource = new(
        "The lab's Rotation input mapped to a settled spin (no birth-spin model): Slow 0.1 (Fuller & Ma 2019 ~0.01; GWTC-3 half below 0.25), " +
        "Fast 0.5 (MacFadyen & Woosley 1999's a_init), Extreme or any gamma-ray burst 0.9 (MW99: spun up to a ≳ 0.9) [APPROX]", "compact §C8");

    /// <summary>The collapsar spin-up's source.</summary>
    public static readonly Source SpinUpSource = new(
        "Collapsar spin-up from a ≈ 0.5 to 0.9 over ≈ 15 s of accretion (MacFadyen & Woosley 1999: a = 0.86–0.985 at 20 s); shape approximate",
        "compact §C8");

    /// <summary>The hole's growth while settling (inconsistency #63).</summary>
    public static readonly Source GrowthSource = new(
        "Drawn growing from the heaviest neutron star's mass at formation to the outcome's mass as the star falls in; the lab has no " +
        "accretion history, so the timing is schematic (a collapsar's follows MacFadyen & Woosley 1999's ≈ 15 s of accretion) [#63]",
        "compact §C8");

    /// <summary>The kind of hole (null for any other remnant).</summary>
    public static BlackHoleBirth? BirthOrNull(Outcome o)
    {
        ArgumentNullException.ThrowIfNull(o);
        if (o.Remnant.Kind != RemnantKind.BlackHole) return null;
        if (o.HasGrb) return BlackHoleBirth.Collapsar;
        return CoreCollapseHistory.TrackFor(o) is CoreTrack.Exploding or CoreTrack.Magnetorotational
            ? BlackHoleBirth.Fallback
            : BlackHoleBirth.DirectCollapse;
    }

    /// <summary>The kind of hole (a direct collapse for any other remnant; callers check the remnant first).</summary>
    public static BlackHoleBirth Birth(Outcome o) => BirthOrNull(o) ?? BlackHoleBirth.DirectCollapse;

    /// <summary>§C8 App (ruling P3-13): 0.9 for any outcome with a gamma-ray burst, else by the Rotation input: 0.1, 0.5 or 0.9.</summary>
    public static double SettledSpin(Outcome o)
    {
        ArgumentNullException.ThrowIfNull(o);
        if (o.HasGrb) return GrbSpin;
        return o.Inputs.Rotation switch
        {
            Rotation.Fast => FastSpin,
            Rotation.Extreme => ExtremeSpin,
            _ => SlowSpin,
        };
    }

    /// <summary>§C8 App: the range shown with the settled spin (Slow 0.01–0.25, Fast 0.25–0.7, Extreme or a burst 0.86–0.985).</summary>
    public static (double Low, double High) SpinRange(Outcome o)
        => o.HasGrb || o.Inputs.Rotation == Rotation.Extreme ? (0.86, 0.985)
            : o.Inputs.Rotation == Rotation.Fast ? (0.25, 0.7)
            : (0.01, 0.25);

    /// <summary>t_BH (the remnant's formation time; 0 when the outcome gives none).</summary>
    public static double SettleStartS(Outcome o)
        => o.Remnant.FormationTimeS is { Value: > 0 } f && double.IsFinite(f.Value) ? f.Value : 0;

    /// <summary>
    /// When the settling ends (s after bounce): a direct collapse t_BH · 10^(1/3) (P3-11); a collapsar t_BH + 15 s (§C8, MW99); a fallback
    /// hole 1,000 s (§C8 [APPROX]; or the direct rule if it formed later than that); t_BH itself when the outcome gives no t_BH.
    /// </summary>
    public static double SettleEndS(Outcome o)
    {
        double t0 = SettleStartS(o);
        if (!(t0 > 0)) return 0;
        return Birth(o) switch
        {
            BlackHoleBirth.Collapsar => t0 + SpinUpS,
            BlackHoleBirth.Fallback when t0 < FallbackSettleS => FallbackSettleS,
            _ => t0 * Math.Pow(10, DirectFadeDecades),
        };
    }

    /// <summary>The settling's progress at this time, 0 at t_BH to 1 at <see cref="SettleEndS"/> (a collapsar's is its spin-up law's).</summary>
    public static double Progress(Outcome o, double sourceTimeS)
    {
        double t0 = SettleStartS(o), t1 = SettleEndS(o);
        if (!(t0 > 0) || !(sourceTimeS < t1)) return 1;
        if (!(sourceTimeS > t0)) return 0;
        double p = Birth(o) == BlackHoleBirth.Collapsar
            ? Math.Log(1 + (sourceTimeS - t0) / SpinUpScaleS) / Math.Log(1 + SpinUpS / SpinUpScaleS)
            : Math.Log(sourceTimeS / t0) / Math.Log(t1 / t0);
        return Math.Clamp(p, 0, 1);
    }

    /// <summary>The settling's progress stepped down to a multiple of 1/<see cref="TransitionSteps"/> (1 stays 1).</summary>
    public static double Stepped(double progress)
        => progress >= 1 ? 1 : Math.Floor(Math.Clamp(progress, 0, 1) * TransitionSteps) / TransitionSteps;

    /// <summary>The disk's outer edge in GM/c² of a hole of this mass (§C8: 46.7 for 2.9 M☉).</summary>
    public static double DiskOuterGmFor(double massMsun) => DiskOuterKm * 1e5 / BlackHoleOptics.GravitationalRadiusCm(massMsun);

    /// <summary>
    /// The black hole at this source time, or null when the outcome has no black hole or it has not formed yet (before t_BH).
    /// <paramref name="settledSpinOverride"/> (the bench's <c>--bench-spin</c> diagnostic, 0 to 0.998) replaces the settled spin.
    /// </summary>
    public static BlackHoleState? At(Outcome o, ObserverSetup obs, int runNumber, double sourceTimeS, double? settledSpinOverride = null)
    {
        ArgumentNullException.ThrowIfNull(o);
        ArgumentNullException.ThrowIfNull(obs);
        if (BirthOrNull(o) is not { } birth || o.Remnant.Mass is not { Value: > 0 } mass || !double.IsFinite(mass.Value)) return null;
        double t0 = SettleStartS(o);
        if (sourceTimeS < t0 || double.IsNaN(sourceTimeS)) return null;

        double settled = settledSpinOverride is double s && double.IsFinite(s) ? Math.Clamp(s, 0, 0.998) : SettledSpin(o);
        double start = Math.Min(CollapsarStartSpin, settled);
        double p = Progress(o, sourceTimeS), q = Stepped(p);
        bool collapsar = birth == BlackHoleBirth.Collapsar;
        double spin = collapsar ? start + (settled - start) * p : settled;
        double drawnSpin = collapsar ? start + (settled - start) * q : settled * q;

        double final = mass.Value;
        double formation = Math.Min(RemnantModel.GravitationalMass(CoreCollapseHistory.ProtoNsMassMsun(o)), final);
        double m = formation + (final - formation) * p, drawnM = formation + (final - formation) * q;

        Quantity spinQ;
        if (p >= 1)
        {
            var (lo, hi) = settledSpinOverride is null ? SpinRange(o) : (settled, settled);
            spinQ = new Quantity(settled, "", Confidence.Speculative, settledSpinOverride is null ? SpinSource
                : new Source("Diagnostic (--bench-spin), not the lab's model", "plan Task 4"), lo, hi);
        }
        else spinQ = new Quantity(spin, "", Confidence.Speculative, collapsar ? SpinUpSource : SpinSource);

        double dt = sourceTimeS - t0;
        bool disk = collapsar && dt >= DiskFromS && dt <= DiskToS;
        return new BlackHoleState(m, spin, spinQ, p, dt < FormingS, disk, DiskOuterGmFor(drawnM),
            KickDrift.OffsetCm(o, obs, runNumber, sourceTimeS))
        {
            Birth = birth,
            DrawnSpin = drawnSpin,
            DrawnMassMsun = drawnM,
            FormationMassMsun = formation,
            FinalMassMsun = final,
            SourceTimeS = sourceTimeS,
            FormationTimeS = t0,
        };
    }

    // ---- From Earth (spec §6.5; compact §C9) ----

    /// <summary>The shadow's apparent diameter (nano-arcseconds) of a non-spinning hole of this mass at this distance (cm): 2√27 GM/(Dc²).</summary>
    public static double ShadowNanoArcsec(double massMsun, double distanceCm)
        => BlackHoleOptics.ApparentShadowDiameterRad(massMsun, distanceCm) * (180 / Math.PI) * 3600 * 1e9;

    /// <summary>How many times smaller than the EHT's 20 µas resolution that shadow is.</summary>
    public static double TimesTooSmallForEht(double massMsun, double distanceCm)
        => EhtResolutionMicroArcsec * 1e3 / ShadowNanoArcsec(massMsun, distanceCm);

    /// <summary>
    /// Spec §6.5's comparison, its exact words: "From Earth this shadow would be about X nano-arcseconds across, about N times too small for
    /// the Event Horizon Telescope", X and N to one significant figure (10 M☉ at 10 kpc: about 0.1, about 200,000; §C9 gives 0.103 and
    /// 1.95 × 10⁵). A shadow the EHT could resolve says so; a distance that is not positive gives a sentence without numbers.
    /// </summary>
    public static string FromEarthText(double massMsun, double distanceCm)
    {
        double x = ShadowNanoArcsec(massMsun, distanceCm);
        if (!(distanceCm > 0) || !double.IsFinite(x) || !(x > 0))
            return "From Earth: the shadow's apparent size needs a distance to the source";
        double n = EhtResolutionMicroArcsec * 1e3 / x;
        string size = $"From Earth this shadow would be about {About(x)} nano-arcseconds across";
        return n > 1
            ? $"{size}, about {About(n)} times too small for the Event Horizon Telescope"
            : $"{size}, large enough for the Event Horizon Telescope (20 micro-arcseconds) to resolve";
    }

    /// <summary>One significant figure: "0.1", "200,000"; below 0.001 or from 10¹² as "1 × 10⁻⁴".</summary>
    public static string About(double v)
    {
        if (!(v > 0) || !double.IsFinite(v)) return "0";
        int exp = (int)Math.Floor(Math.Log10(v));
        double mant = Math.Round(v / Math.Pow(10, exp), MidpointRounding.AwayFromZero);
        if (mant >= 10) { mant = 1; exp++; }
        if (exp < -3 || exp >= 12) return $"{mant.ToString("0", Inv)} × 10{Superscript(exp)}";
        double r = mant * Math.Pow(10, exp);
        return exp >= 0 ? r.ToString("N0", Inv) : r.ToString("0." + new string('#', -exp), Inv);
    }

    private static string Superscript(int n)
    {
        const string digits = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        var s = new System.Text.StringBuilder(n < 0 ? "⁻" : "");
        foreach (char ch in Math.Abs(n).ToString(Inv)) s.Append(digits[ch - '0']);
        return s.ToString();
    }
}
