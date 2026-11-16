using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Compact;

/// <summary>Which first-second history a core follows (spec §3.1 items 1–6).</summary>
public enum CoreTrack
{
    /// <summary>A neutrino-driven explosion (a neutron star, or a black hole by fallback later).</summary>
    Exploding,
    /// <summary>The shock never revives; the proto-neutron star collapses to a black hole (failed supernova, collapsar, PPISN).</summary>
    Failed,
    /// <summary>A magnetorotational explosion (outcomes step 7c): the exploding track, with its note.</summary>
    Magnetorotational,
}

/// <summary>Where the core is in its first second.</summary>
public enum CorePhase { PreCollapse, Bounce, Stalled, Revived, Collapsing, BlackHole, Cooling }

/// <summary>
/// The core at one source time (compact §C1, §C2). Radii in km (NaN: not there, or not drawn), the infall speed ahead of the shock in
/// km/s (NaN without a shock). <see cref="ProtoNsRadiusKm"/> is the research's radius (density 10¹¹ g/cm³); the drawn sphere is
/// <see cref="CoreCollapseHistory.DrawnProtoNsRadiusKm"/>. <see cref="GainRadiusKm"/> is NaN while no gain shell is drawn (§C1 App).
/// <see cref="DistortionFraction"/> is the shock's distortion as a fraction of its radius; <see cref="DistortionSchematicOnly"/> is
/// true when no amplitude is stated (the research gives an approximate one, so it is false).
/// </summary>
public sealed record CoreState(double SourceTimeS, CorePhase Phase, double ShockRadiusKm, double GainRadiusKm,
    double ProtoNsRadiusKm, double InfallSpeedKmS, double DistortionFraction, bool DistortionSchematicOnly);

/// <summary>
/// The core's first second (spec §3; compact §C1 and §C2): the typical shock, gain-radius and proto-neutron-star tracks of an exploding
/// or a failed core, interpolated linearly in log t and log R between §C1.2's rows; the failed track stretched in time to end at the
/// remnant's <see cref="Remnant.FormationTimeS"/> (the neutrino panel's cut-off, criterion 4); the contraction to the remnant's radius
/// by one minute (§C2); and, for a black hole by fallback, §C2's contraction until it collapses. Pure functions of the outcome.
/// No published dependence of the tracks on the progenitor was found (§C1.1), so they are labelled "typical".
/// </summary>
public static class CoreCollapseHistory
{
    /// <summary>Spec §1 and §3.2, the plan's exact words: nothing in the first second is observed directly except SN 1987A's neutrinos.</summary>
    public const string FromSimulationsLabel = "From simulations";

    /// <summary>Spec §3.1 item 6, the plan's exact words.</summary>
    public const string MagnetorotationalNote = "A magnetar-driven explosion revives the shock differently (Speculative)";

    /// <summary>§C1 App: the failed track uses the exploding rows up to 100 ms.</summary>
    public const double FailedJoinS = 0.1;

    /// <summary>§C1 App: the collapse to the horizon is drawn over the last 1 ms before t_BH (O'Connor &amp; Ott 2011: ≈ 1 ms).</summary>
    public const double CollapseDurationS = 0.001;

    /// <summary>§C1 App: Stalled from 30 ms (the gain region has formed); Bounce before.</summary>
    public const double StalledFromS = 0.03;

    /// <summary>§C1 App: revival at 0.3 s for an iron core, 0.07 s for an O-Ne-Mg (electron-capture) core.</summary>
    public const double IronRevivalS = 0.3, OxygenNeonMagnesiumRevivalS = 0.07;

    /// <summary>§C1 App: the exploding stage ends when the typical shock passes 3,000 km, at 1 s in §C1.2's table.</summary>
    public const double ExplodingStageEndS = 1.0;

    /// <summary>§C2 App: from 60 s the proto-neutron star has the remnant's radius.</summary>
    public const double ContractionEndS = 60;

    /// <summary>§C2: the power law's exponent, and the lab's neutron-star radius used for a black hole's proto-neutron star.</summary>
    private const double ContractionExponent = 1.7, DefaultFinalRadiusKm = 12;

    /// <summary>§C1 App: DistortionFraction 0.1 [APPROX] (Hanke et al. 2013's figure ranges).</summary>
    public const double DistortionFraction = 0.1;

    /// <summary>
    /// [APPROX, my call] The distortion grows from nothing at 30 ms (no gain layer before: the bounce shock is round) to its full 10%
    /// at 100 ms, linearly in log t: about the 3.5 e-folds of Burrows et al. 2020's ≈ 20 ms growth of the shock dipole (§C1.1).
    /// </summary>
    public const double DistortionFullS = 0.1;

    /// <summary>§C1 App: the drawn proto-neutron star is at most 0.9 R_sh; in the failed track the gain shell needs R_gain &gt; 1.1 R_PNS.</summary>
    public const double DrawnProtoNsMaxOfShock = 0.9, FailedGainMinOfProtoNs = 1.1;

    /// <summary>§C1.1: in the last 1 ms the shock recedes to ≈ 12 km (O'Connor &amp; Ott 2011).</summary>
    public const double ShockAtCollapseKm = 12;

    /// <summary>§C1 App: the pre-collapse core radii, km (one value each).</summary>
    public const double IronCoreRadiusKm = 2300, OxygenNeonMagnesiumCoreRadiusKm = 1100;

    // §C1.2 exploding track, typical values: t (s), R_sh, R_gain (NaN: none), R_PNS (km). Rows before 30 ms are [APPROX] stand-ins.
    private static readonly double[] ExpT = { 0.001, 0.003, 0.010, 0.030, 0.100, 0.200, 0.300, 0.500, 1.000 };
    private static readonly double[] ExpShock = { 15, 40, 85, 120, 145, 155, 300, 900, 3000 };
    private static readonly double[] ExpGain = { double.NaN, double.NaN, double.NaN, 70, 85, 85, 65, 45, 35 };
    private static readonly double[] ExpPns = { 70, 70, 68, 62, 50, 40, 35, 29, 25 };

    // §C1.2 failed track after the join: fractions of t_BH (the last row is t_BH − 1 ms), typical values.
    private static readonly double[] FailedFraction = { 0.4, 0.7, 0.95 };
    private static readonly double[] FailedShock = { 110, 70, 30, 20 };
    private static readonly double[] FailedGain = { 63, 40, double.NaN, double.NaN };
    private static readonly double[] FailedPns = { 38, 28, 20, 16 };

    /// <summary>The tracks' source (the radii and times).</summary>
    public static readonly Source TrackSource = new(
        "Typical tracks from simulations (O'Connor & Ott 2011; O'Connor et al. 2018; Bollig et al. 2021; Burrows et al. 2020; Müller 2020); " +
        "rows before 30 ms approximate", "compact §C1");

    /// <summary>The contraction's source (§C2).</summary>
    public static readonly Source ContractionSource = new(
        "Power law fitted to ≈ 25 km at 1 s (Burrows et al. 2020) and ≈ 17 km at 2 s (Roberts 2012), ending at the remnant's radius [APPROX]",
        "compact §C2");

    /// <summary>The infall speed's source.</summary>
    public static readonly Source InfallSource = new(
        "|v| ≈ √(G M / r) ahead of the shock (Müller 2020), with M the proto-neutron star's baryonic mass", "compact §C1");

    /// <summary>The distortion's source (approximate amplitude, schematic shape).</summary>
    public static readonly Source DistortionSource = new(
        "SASI and convection: about 10% of the shock radius (Hanke et al. 2013, figure ranges) [APPROX]; the shape is schematic", "compact §C1");

    /// <summary>The pre-collapse core's source.</summary>
    public static readonly Source PreCollapseSource = new(
        "Iron core 2,300 km (a 25 M☉ star's, MacFadyen & Woosley 1999); O-Ne-Mg core 1,100 km (Kitaura et al. 2006); one value for every " +
        "core of each kind [APPROX]", "compact §C1");

    private static readonly NeutrinoModel Neutrinos = new();

    /// <summary>
    /// The track (spec §3.1 items 1–6): Failed for a failed supernova, a pulsational pair-instability collapse, or a collapsar (a
    /// supernova with a gamma-ray burst whose black hole forms at <see cref="RemnantModel.DirectCollapseTime"/>); Magnetorotational for
    /// outcomes step 7c (a black hole after an explosion with a strong field and fast or extreme rotation, no burst); Exploding for
    /// every other supernova that leaves a neutron star, a magnetar or a black hole by fallback. Null when there is no core collapse.
    /// A magnetar remnant (step 7b) is exploding: in the lab its explosion is neutrino-driven, and the magnetar powers the light.
    /// </summary>
    public static CoreTrack? TrackFor(Outcome o)
    {
        ArgumentNullException.ThrowIfNull(o);
        var kind = o.Remnant.Kind;
        if (kind is not (RemnantKind.NeutronStar or RemnantKind.Magnetar or RemnantKind.BlackHole)) return null;
        return o.Fate switch
        {
            Fate.FailedSupernova or Fate.PulsationalPairInstability when kind == RemnantKind.BlackHole => CoreTrack.Failed,
            Fate.Supernova when kind == RemnantKind.BlackHole && o.HasGrb => CoreTrack.Failed,
            Fate.Supernova when kind == RemnantKind.BlackHole && o.Inputs.Field == CoreField.Strong && o.Inputs.Rotation != Rotation.Slow
                => CoreTrack.Magnetorotational,
            Fate.Supernova => CoreTrack.Exploding,
            _ => null,
        };
    }

    /// <summary>Whether the core is an O-Ne-Mg core that collapses by electron capture (outcomes step 1).</summary>
    public static bool IsElectronCapture(Outcome o)
        => o.Fate == Fate.Supernova && o.Inputs.Kind == StarKind.MassiveStar
           && ProgenitorModel.IsElectronCaptureMass(o.Inputs.Clamped().InitialMass)
           && o.Remnant.Kind is RemnantKind.NeutronStar or RemnantKind.Magnetar;

    /// <summary>§C1 App: when the shock revives (s after bounce); NaN for the failed track or no collapse.</summary>
    public static double RevivalTimeS(Outcome o) => TrackFor(o) switch
    {
        CoreTrack.Exploding or CoreTrack.Magnetorotational => IsElectronCapture(o) ? OxygenNeonMagnesiumRevivalS : IronRevivalS,
        _ => double.NaN,
    };

    /// <summary>§C1 App: the core before collapse, km: 1,100 for an O-Ne-Mg core, else 2,300; NaN with no core collapse.</summary>
    public static double PreCollapseCoreRadiusKm(Outcome o)
        => TrackFor(o) is null ? double.NaN : IsElectronCapture(o) ? OxygenNeonMagnesiumCoreRadiusKm : IronCoreRadiusKm;

    /// <summary>t_BH (s) when the outcome forms a black hole; NaN otherwise.</summary>
    public static double BlackHoleFormsS(Outcome o)
        => o.Remnant.Kind == RemnantKind.BlackHole && o.Remnant.FormationTimeS is { Value: > 0 } f ? f.Value : double.NaN;

    /// <summary>
    /// When the close-up leaves the first-second era: t_BH for any black-hole outcome (the failed track ends there; an exploding core
    /// that forms a black hole by fallback cools as §C2 says until it collapses, and has no neutron-star era), else 1 s, when the
    /// typical shock passes 3,000 km. NaN with no core collapse.
    /// </summary>
    public static double StageEndS(Outcome o)
    {
        if (TrackFor(o) is null) return double.NaN;
        double tbh = BlackHoleFormsS(o);
        return double.IsNaN(tbh) ? ExplodingStageEndS : tbh;
    }

    /// <summary>The proto-neutron star's baryonic mass (M☉): the neutron star's; for a black hole, the heaviest neutron star's.</summary>
    public static double ProtoNsMassMsun(Outcome o)
        => o.Remnant.Kind != RemnantKind.BlackHole && o.Remnant.Mass is { Value: > 0 } m
            ? RemnantModel.BaryonicMass(m.Value)
            : RemnantModel.MinBlackHoleMass;

    /// <summary>
    /// The horizon the collapse ends at, km: r₊ = 2 G M / c² (no spin yet) of the proto-neutron star's gravitational mass (2.2 M☉ for
    /// the heaviest neutron star, 6.50 km). Fix round 1: the gravitational, not the baryonic, mass.
    /// </summary>
    public static double HorizonAtFormationKm(Outcome o)
        => 2 * BlackHoleOptics.GravitationalRadiusCm(RemnantModel.GravitationalMass(ProtoNsMassMsun(o))) / 1e5;

    /// <summary>§C1 App: the infall speed √(G M / r), km/s, for a mass in M☉ at a radius in km.</summary>
    public static double InfallSpeedKmS(double massMsun, double radiusKm)
        => Math.Sqrt(PhysicalConstants.G * massMsun * PhysicalConstants.MSun / (radiusKm * 1e5)) / 1e5;

    /// <summary>§C2: R(t) = R_f [1 + (25 km / R_f − 1) (t / 1 s)^(−1.7)], km, for t ≥ 1 s (R_f from 60 s).</summary>
    public static double ContractionRadiusKm(double finalRadiusKm, double sourceTimeS)
    {
        if (sourceTimeS >= ContractionEndS) return finalRadiusKm;
        double t = Math.Max(sourceTimeS, ExplodingStageEndS);
        return finalRadiusKm * (1 + (ExpPns[^1] / finalRadiusKm - 1) * Math.Pow(t, -ContractionExponent));
    }

    /// <summary>The radius the contraction ends at: the neutron star's, or the lab's 12 km for a black hole's proto-neutron star (§C2 App).</summary>
    public static double FinalRadiusKm(Outcome o)
        => o.Remnant.Kind != RemnantKind.BlackHole && o.Remnant.RadiusKm is { Value: > 0 } r ? r.Value : DefaultFinalRadiusKm;

    /// <summary>§C1 App: the drawn proto-neutron star, min(R_PNS, 0.9 R_sh) (bounded by the shock while it is inside the neutrinospheres).</summary>
    public static double DrawnProtoNsRadiusKm(CoreState s)
        => double.IsFinite(s.ShockRadiusKm) ? Math.Min(s.ProtoNsRadiusKm, DrawnProtoNsMaxOfShock * s.ShockRadiusKm) : s.ProtoNsRadiusKm;

    /// <summary>The neutrino panel's total luminosity, ν_e + anti-ν_e + 4 ν_x (erg/s); NaN where the model does not run.</summary>
    public static double NeutrinoLuminosityErgS(Outcome o, double sourceTimeS)
    {
        var obs = ObserverSetup.Default;   // the emitted luminosity does not depend on the observer
        return Neutrinos.Sample(o, obs, sourceTimeS, "lum_nue") + Neutrinos.Sample(o, obs, sourceTimeS, "lum_anue")
               + 4 * Neutrinos.Sample(o, obs, sourceTimeS, "lum_nux");
    }

    /// <summary>The largest total neutrino luminosity over 1 ms–10 s (erg/s), sampled at 400 log-spaced times; NaN when none.</summary>
    public static double PeakNeutrinoLuminosityErgS(Outcome o)
    {
        double peak = double.NaN;
        for (int i = 0; i <= 400; i++)
        {
            double l = NeutrinoLuminosityErgS(o, Math.Pow(10, -3 + 4.0 * i / 400));
            if (l > 0 && !(l <= peak)) peak = l;
        }
        return peak;
    }

    /// <summary>The core at this source time (s after bounce). See the class summary; with no core collapse every radius is NaN.</summary>
    public static CoreState At(Outcome o, double sourceTimeS)
    {
        ArgumentNullException.ThrowIfNull(o);
        double t = sourceTimeS;
        var track = TrackFor(o);
        if (track is null || !(t > 0)) return Empty(t, CorePhase.PreCollapse);
        double tbh = BlackHoleFormsS(o);
        if (t >= tbh) return Empty(t, CorePhase.BlackHole);
        if (t >= tbh - CollapseDurationS) return Collapse(o, track.Value, tbh, t);
        return Before(o, track.Value, tbh, t);
    }

    private static CoreState Empty(double t, CorePhase phase) => new(t, phase, double.NaN, double.NaN, double.NaN, double.NaN, 0, false);

    /// <summary>The track before any collapse.</summary>
    private static CoreState Before(Outcome o, CoreTrack track, double tbh, double t)
    {
        double mass = ProtoNsMassMsun(o);
        if (track == CoreTrack.Failed)
        {
            var (shock, gain, pns) = FailedAt(tbh, t);
            if (!(gain > FailedGainMinOfProtoNs * pns)) gain = double.NaN;
            var phase = t < StalledFromS ? CorePhase.Bounce : CorePhase.Stalled;
            return new CoreState(t, phase, shock, gain, pns, InfallSpeedKmS(mass, shock), Distortion(t), false);
        }
        if (t > ExplodingStageEndS)
            return new CoreState(t, CorePhase.Cooling, double.NaN, double.NaN, ContractionRadiusKm(FinalRadiusKm(o), t), double.NaN, 0, false);
        double tc = Math.Max(t, ExpT[0]);
        double sh = Interp(ExpT, ExpShock, tc), g = Interp(ExpT, ExpGain, tc), p = Interp(ExpT, ExpPns, tc);
        var ph = t < StalledFromS ? CorePhase.Bounce : t < RevivalTimeS(o) ? CorePhase.Stalled : CorePhase.Revived;
        return new CoreState(t, ph, sh, g, p, InfallSpeedKmS(mass, sh), Distortion(t), false);
    }

    /// <summary>The last millisecond: from the track at t_BH − 1 ms, the shock recedes to 12 km and the star shrinks to r₊, linearly in t.</summary>
    private static CoreState Collapse(Outcome o, CoreTrack track, double tbh, double t)
    {
        double t0 = tbh - CollapseDurationS;
        var start = t0 > 0 ? Before(o, track, tbh, t0) : Before(o, track, tbh, ExpT[0]);
        double u = Math.Clamp((t - t0) / CollapseDurationS, 0, 1);
        double horizonKm = HorizonAtFormationKm(o);
        double pns = start.ProtoNsRadiusKm + (horizonKm - start.ProtoNsRadiusKm) * u;
        double shock = double.IsFinite(start.ShockRadiusKm)
            ? start.ShockRadiusKm + (ShockAtCollapseKm - start.ShockRadiusKm) * u
            : double.NaN;
        double infall = double.IsFinite(shock) ? InfallSpeedKmS(ProtoNsMassMsun(o), shock) : double.NaN;
        return new CoreState(t, CorePhase.Collapsing, shock, double.NaN, pns, infall, start.DistortionFraction, false);
    }

    /// <summary>The failed track's typical values at t (before the collapse): the exploding rows to the join, then the t/t_BH rows.</summary>
    private static (double Shock, double Gain, double Pns) FailedAt(double tbh, double t)
    {
        var (times, shock, gain, pns) = FailedKnots(tbh);
        double tc = Math.Max(t, times[0]);
        return (Interp(times, shock, tc), InterpGain(times, gain, pns, tc), Interp(times, pns, tc));
    }

    /// <summary>
    /// §C1 App: the failed track's rows for this t_BH: the exploding rows up to 100 ms (only those before 0.4 t_BH, so the join is
    /// monotone when t_BH &lt; 0.25 s), then 0.4, 0.7 and 0.95 t_BH and t_BH − 1 ms.
    /// </summary>
    private static (double[] T, double[] Shock, double[] Gain, double[] Pns) FailedKnots(double tbh)
    {
        var t = new List<double>(); var sh = new List<double>(); var g = new List<double>(); var p = new List<double>();
        for (int i = 0; i < ExpT.Length; i++)
        {
            if (ExpT[i] > FailedJoinS || ExpT[i] >= FailedFraction[0] * tbh) break;
            t.Add(ExpT[i]); sh.Add(ExpShock[i]); g.Add(ExpGain[i]); p.Add(ExpPns[i]);
        }
        for (int i = 0; i < FailedShock.Length; i++)
        {
            double ti = i < FailedFraction.Length ? FailedFraction[i] * tbh : tbh - CollapseDurationS;
            if (t.Count > 0 && ti <= t[^1]) continue;   // a very early black hole: keep the rows increasing
            t.Add(ti); sh.Add(FailedShock[i]); g.Add(FailedGain[i]); p.Add(FailedPns[i]);
        }
        return (t.ToArray(), sh.ToArray(), g.ToArray(), p.ToArray());
    }

    /// <summary>
    /// The gain radius: NaN before its first row; after its last row (the gain layer is gone by 0.95 t_BH in the failed track) it runs
    /// down to the proto-neutron star's radius at the next row, where the 1.1 R_PNS rule has already hidden it; NaN after that.
    /// </summary>
    private static double InterpGain(double[] t, double[] gain, double[] pns, double x)
    {
        int first = Array.FindIndex(gain, double.IsFinite), last = Array.FindLastIndex(gain, double.IsFinite);
        if (first < 0 || x < t[first]) return double.NaN;
        if (x <= t[last]) return Interp(t[first..(last + 1)], gain[first..(last + 1)], x);
        if (last + 1 >= t.Length || x > t[last + 1]) return double.NaN;
        return LogLerp(t[last], gain[last], t[last + 1], pns[last + 1], x);
    }

    /// <summary>The shock's distortion fraction: 0 before 30 ms, up to 0.1 at 100 ms, linearly in log t, then 0.1.</summary>
    private static double Distortion(double t)
        => t <= StalledFromS ? 0
            : t >= DistortionFullS ? DistortionFraction
            : DistortionFraction * Math.Log(t / StalledFromS) / Math.Log(DistortionFullS / StalledFromS);

    /// <summary>Linear in log t and log R between rows (NaN rows give NaN), held at the ends.</summary>
    private static double Interp(double[] t, double[] v, double x)
    {
        if (x <= t[0]) return v[0];
        if (x >= t[^1]) return v[^1];
        int i = 1;
        while (t[i] < x) i++;
        if (t[i] == x) return v[i];
        return LogLerp(t[i - 1], v[i - 1], t[i], v[i], x);
    }

    private static double LogLerp(double t0, double v0, double t1, double v1, double x)
    {
        if (!double.IsFinite(v0) || !double.IsFinite(v1)) return double.NaN;
        double f = Math.Log(x / t0) / Math.Log(t1 / t0);
        return Math.Exp(Math.Log(v0) + f * (Math.Log(v1) - Math.Log(v0)));
    }
}
