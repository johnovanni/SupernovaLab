using System.Globalization;
using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Compact;

/// <summary>
/// The neutron star the close-up shows at one source time (spec §4): its size, early surface temperature, spin, field,
/// whether it has a crust yet, and where the kick has carried it.
/// </summary>
/// <param name="SourceTimeS">Seconds after core bounce, source frame.</param>
/// <param name="MassMsun">Gravitational mass, M☉.</param>
/// <param name="RadiusKm">Radius, km.</param>
/// <param name="SurfaceTemperatureK">T_s^∞, seen from afar (a band while the crust is still relaxing, §C3).</param>
/// <param name="PeriodS">Spin period at this time, s (<see cref="PulsarModel.Spin"/>).</param>
/// <param name="PeriodDerivative">Ṗ at this time, s/s.</param>
/// <param name="FieldG">The dipole field, G.</param>
/// <param name="IsMagnetar">The remnant is a magnetar.</param>
/// <param name="HasCrust">The crust has crystallised (§C5.3).</param>
/// <param name="AgeS">Age since bounce, s (the source time).</param>
/// <param name="OffsetCm">Drift from the explosion's centre, cm, in the scene frame (§4.6).</param>
public sealed record NeutronStarState(double SourceTimeS, double MassMsun, double RadiusKm, Quantity SurfaceTemperatureK,
    double PeriodS, double PeriodDerivative, double FieldG, bool IsMagnetar, bool HasCrust, double AgeS, Vec3 OffsetCm);

/// <summary>
/// The close-up's neutron star (spec §4; compact §C3 early cooling, §C4, §C9). The star is the outcome's remnant; its
/// spin is <see cref="PulsarModel.Spin"/> exactly (a magnetar uses the engine's spin-down time, as the pulsar model does),
/// and its late temperature is <see cref="NeutronStarCoolingModel"/>.
/// </summary>
public static class NeutronStarModel
{
    /// <summary>§C3: the early temperature band starts here, 1 minute after bounce.</summary>
    public const double EarlyStartS = 60;

    /// <summary>
    /// §C5.3 / App: the crust crystallises about 1 day after bounce [D/APPROX], the same for every neutron star (range 1 h to
    /// 1 month). Ledger ruling S1: held here until Task 7 moves it to <c>NeutronStarLayers.CrustFormationTimeS</c>.
    /// </summary>
    public const double CrustFormationTimeS = 86_400;

    private const double Year = PhysicalConstants.Year;
    private const double PlateauK = 2.9e6;                   // §C3: Gnedin et al.'s ≈ 250 eV plateau
    private const double PlateauEndS = 10 * Year;            // §C3: flat to 10 yr, then log-linear down to the model's 30-yr value

    private static readonly NeutronStarCoolingModel Cooling = new();

    private static readonly Source EarlySource = new(
        "Gnedin, Yakovlev & Potekhin 2001; Yakovlev & Pethick 2004; the lab's slow-cooling law extended [APPROX]; " +
        "T_s^∞ = T_s √(1 − r_g/R). A band from the lab's 100-yr value up to Gnedin's ≈ 2.9 MK plateau, joining the " +
        "cooling model's own band exactly at 30 yr", "compact §C3");

    /// <summary>
    /// The neutron star at <paramref name="sourceTimeS"/>, or null when the outcome leaves none (or the time is negative).
    /// <paramref name="runNumber"/> seeds only the kick direction (display randomness).
    /// </summary>
    public static NeutronStarState? At(Outcome o, ObserverSetup obs, int runNumber, double sourceTimeS)
    {
        if (!Applies(o) || !(sourceTimeS >= 0)) return null;
        var spin = PulsarModel.Spin.Of(o);
        var r = o.Remnant;
        return new NeutronStarState(sourceTimeS, r.Mass!.Value, r.RadiusKm!.Value, SurfaceTemperature(o, sourceTimeS),
            spin.PeriodS(sourceTimeS), spin.PeriodDerivative(sourceTimeS), r.MagneticFieldG!.Value,
            r.Kind == RemnantKind.Magnetar, sourceTimeS >= CrustFormationTimeS, sourceTimeS,
            KickDrift.OffsetCm(o, obs, runNumber, sourceTimeS));
    }

    /// <summary>
    /// T_s^∞ (K, seen from afar) at this source time. From 60 s to 30 yr: §C3's band, valued by the cooling model's own law;
    /// 30–100 yr: the cooling model's band; after: its single value. Before 60 s the 60-s value is held (the research does not
    /// cover the first minute). A NaN quantity when the outcome leaves no neutron star.
    /// </summary>
    public static Quantity SurfaceTemperature(Outcome o, double sourceTimeS)
    {
        double t = Math.Max(sourceTimeS, EarlyStartS);
        var model = Cooling.SurfaceTemperatureAt(o, t);
        if (t >= NeutronStarCoolingModel.StartS || double.IsNaN(model.Value)) return model;

        double modelAt30 = Cooling.SurfaceTemperatureAt(o, NeutronStarCoolingModel.StartS).Value;
        double plateau = t <= PlateauEndS
            ? PlateauK
            : PlateauK + (modelAt30 - PlateauK) * Math.Log(t / PlateauEndS) / Math.Log(NeutronStarCoolingModel.StartS / PlateauEndS);
        return new Quantity(model.Value, "K", Confidence.ModelDependent, EarlySource, model.Low, Math.Max(model.Value, plateau));
    }

    /// <summary>
    /// The close-up's tooltip for the Cas A preset (inconsistency #54, ruling R-P3-1; compact §C9): the observed surface
    /// temperature, the observed value seen from afar, the lab's, and how warm the lab is. Null for any other outcome.
    /// </summary>
    public static string? CasAComparison(Outcome o, double sourceTimeS)
    {
        if (!Applies(o) || !o.Discloses(54)) return null;
        double lab = SurfaceTemperature(o, sourceTimeS).Value / 1e6;
        var inv = CultureInfo.InvariantCulture;
        double warmLow = (lab / 1.5 - 1) * 100, warmHigh = (lab / 1.4 - 1) * 100;
        string warm = warmLow >= 0
            ? $"about {warmLow.ToString("0", inv)}–{warmHigh.ToString("0", inv)}% warm"
            : "about as observed";
        return "Observed (Heinke & Ho 2010): 2.04–2.12 million K at the surface, about 1.4–1.5 million K seen from afar; " +
               $"the lab gives {lab.ToString("0.0", inv)} million K seen from afar, {warm} (#54).";
    }

    private static bool Applies(Outcome o)
        => o.Fate == Fate.Supernova && o.Remnant.Kind is RemnantKind.NeutronStar or RemnantKind.Magnetar
           && o.Remnant.Mass is { Value: > 0 } && o.Remnant.RadiusKm is { Value: > 0 }
           && o.Remnant.SpinPeriodS is { Value: > 0 } && o.Remnant.MagneticFieldG is { Value: > 0 };

    internal static bool HasNeutronStar(Outcome o) => Applies(o);
}
