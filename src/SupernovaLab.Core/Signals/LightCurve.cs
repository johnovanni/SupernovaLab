using System.Runtime.CompilerServices;
using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>
/// One outcome's optical light curve for <see cref="LightCurveModel"/>: its heating, diffusion time, plateau (if any),
/// shock cooling and colours. Built once per outcome and cached (it is a pure function of the outcome). Source time in
/// s from bounce (ignition for a Type Ia); luminosities in erg/s.
/// </summary>
internal sealed class LightCurve
{
    // ---------------------------------------------------------------- constants

    private const double Day = PhysicalConstants.Day;

    /// <summary>[APPROX] The width of the Fermi-Dirac drop at the plateau's end: "a free display choice" (§4.3).</summary>
    private const double PlateauDropWidthS = 3 * Day;

    /// <summary>
    /// [APPROX] Photospheric colour temperatures (§4.5): hydrogen recombination, 5,000–7,000 K, for hydrogen-rich
    /// photospheres; 8,000–12,000 K for Ia/Ibc near peak. The middles of the ranges, held through the tail (the research
    /// gives no later colours, except the Lira relation for Type Ia).
    /// </summary>
    private const double HydrogenPhotosphereK = 6_000, HotPhotosphereK = 10_000;

    /// <summary>The Lira relation holds 30–90 days after V maximum (§4.5).</summary>
    private const double LiraStartDays = 30, LiraEndDays = 90;

    /// <summary>[APPROX] §11: the light curve runs from about 1 hour (the start for a Type Ia, which has no breakout).</summary>
    private const double EarliestStartS = 3600;

    private static readonly ShockBreakoutModel Breakout = new();

    private static readonly ConditionalWeakTable<Outcome, Holder> Cache = new();

    private sealed record Holder(LightCurve? Value);

    // ---------------------------------------------------------------- state

    public bool Thermonuclear { get; private init; }
    public bool Plateau { get; private init; }
    public bool Interacting { get; private init; }
    public bool PairInstability { get; private init; }
    public bool HasMagnetar => _magEnergy > 0 && _magTimeS > 0;
    public BcSample BcSample { get; private init; }
    public double StartS { get; private init; }
    public double DiffusionTimeS { get; private init; }
    public double PlateauEndS { get; private init; }

    private double _nickel, _trappingS, _magEnergy, _magTimeS, _l50, _ejecta, _energy, _kappa;
    private ShockBreakoutModel.CoolingPhase? _cooling;

    // Derived once, on first use; Lazy keeps concurrent first uses safe.
    private readonly Lazy<double> _mainPeakS;
    private readonly Lazy<(double TimeS, double AbsV)> _peak;
    private readonly Lazy<bool> _visiblePlateau;

    private LightCurve()
    {
        _mainPeakS = new(() => ArnettDiffusion.Maximize(Main, Math.Max(StartS, 0.05 * DiffusionTimeS),
            Math.Min(LightCurveModel.EndS, 10 * DiffusionTimeS)).TimeS);
        _peak = new(FindPeak);
        _visiblePlateau = new(() => Plateau && _l50 > Diffused(PlateauEndS));
    }

    /// <summary>The light curve of this outcome; null when it has no modeled explosion.</summary>
    public static LightCurve? Of(Outcome o) => Cache.GetValue(o, x => new Holder(Build(x))).Value;

    private static LightCurve? Build(Outcome o)
    {
        if (o.Fate is not (Fate.Supernova or Fate.PairInstabilitySupernova)) return null;
        if (o.Star is not { } star || o.ExplosionEnergyErg is not { Value: > 0 } energy ||
            o.EjectaMass is not { Value: > 0 } ejecta) return null;

        double e = energy.Value, m = ejecta.Value;
        bool thermonuclear = o.Type is SupernovaType.Ia or SupernovaType.Ia91T or SupernovaType.Ia91bg or SupernovaType.Iax;
        double envelope = star.HydrogenEnvelopeMass.Value;
        bool hydrogenType = o.Type is SupernovaType.IIP or SupernovaType.IIL or SupernovaType.IIn or SupernovaType.SlsnII
                            || (o.Type == SupernovaType.Pisn && envelope > 0);
        bool plateau = !thermonuclear && hydrogenType && envelope >= PlateauModel.MinEnvelopeMsun;
        var cooling = thermonuclear ? null : Breakout.Cooling(o);
        double breakoutS = thermonuclear ? double.NaN : Breakout.BreakoutTimeS(o);
        double start = cooling?.StartS ?? (double.IsFinite(breakoutS) ? Math.Max(breakoutS, EarliestStartS) : EarliestStartS);
        bool magnetar = o.MagnetarEnergyErg is { Value: > 0 } && o.MagnetarTimescaleS is { Value: > 0 };
        // Ruling R31: a fixed κ for thermonuclear ejecta. Rulings R32/R33a: a magnetar-powered outcome uses the
        // magnetar ejecta opacity, the one the engine's peak and SLSN-II cut use, for its ⁵⁶Ni too (same ejecta, same
        // τ_m). Otherwise the core-collapse slider.
        double kappa = thermonuclear ? LightCurveModel.ThermonuclearOpacity
            : magnetar ? MagnetarModel.EjectaOpacity(o.Inputs.InitialMass, envelope, m)
            : o.Inputs.Opacity;
        double nickel = Math.Max(o.NickelMass?.Value ?? 0, 0);
        double r = star.RadiusCm.Value;

        return new LightCurve
        {
            Thermonuclear = thermonuclear,
            Plateau = plateau,
            Interacting = o.Inputs.DenseCsm || o.Type is SupernovaType.IIn or SupernovaType.Ibn or SupernovaType.Icn,
            PairInstability = o.Fate == Fate.PairInstabilitySupernova,
            BcSample = plateau ? BcSample.TypeII : BcSample.StrippedEnvelope,
            StartS = start,
            DiffusionTimeS = ArnettDiffusion.DiffusionTimeS(kappa, m, e),
            PlateauEndS = plateau ? PlateauModel.DurationS(e, m, nickel, r) : double.NaN,
            _nickel = nickel,
            _trappingS = LightCurveModel.GammaRayTrappingTimeS(m, e),
            _magEnergy = o.MagnetarEnergyErg?.Value ?? 0,
            _magTimeS = o.MagnetarTimescaleS?.Value ?? 0,
            _ejecta = m,
            _energy = e,
            _kappa = kappa,
            _l50 = plateau ? PlateauModel.LuminosityErgS(e, m, r) : 0,
            _cooling = cooling,
        };
    }

    // ---------------------------------------------------------------- luminosity

    /// <summary>The ⁵⁶Ni/⁵⁶Co heating deposited in the ejecta, erg/s, with gamma-ray trapping.</summary>
    private double NickelHeating(double t) => LightCurveModel.DepositedNickelHeatingErgS(_nickel, t, _trappingS);

    /// <summary>
    /// The diffused heating, erg/s. The kernel is linear, so it is the diffused ⁵⁶Ni plus the magnetar's
    /// <see cref="MagnetarModel.DiffusedLuminosityErgS"/>: the very function the outcome engine's peak and SLSN-II cut use
    /// (ruling R32; both with the outcome's E_K, M_ej and ejecta opacity, hence the same τ_m).
    /// </summary>
    private double Diffused(double t)
        => ArnettDiffusion.LuminosityErgS(NickelHeating, DiffusionTimeS, t)
           + (HasMagnetar ? MagnetarModel.DiffusedLuminosityErgS(t, _magEnergy, _magTimeS, _ejecta, _energy, _kappa) : 0);

    /// <summary>The Fermi-Dirac step at the plateau's end, 1 / (1 + e^((t − t_p)/w)): 1 on the plateau, 0 after.</summary>
    private double PlateauStep(double t)
    {
        double x = (t - PlateauEndS) / PlateauDropWidthS;
        return x > 700 ? 0 : 1 / (1 + Math.Exp(x));
    }

    /// <summary>The plateau L50 held until t_p, then the step onto the deposited ⁵⁶Ni power (§4.3 steps 2–4).</summary>
    private double PlateauPart(double t)
    {
        double s = PlateauStep(t);
        return _l50 * s + NickelHeating(t) * (1 - s);
    }

    /// <summary>
    /// Everything except shock cooling. Without a plateau, the diffused heating. With a visible plateau, the brighter of
    /// the plateau and the diffused heating while the plateau lasts; ruling R33d: as it drops, the diffused heating is
    /// faded out by the same step, so the tail is §4.3 step 4's L_dep (PlateauPart alone from t_p + 3w, where the step's
    /// 0.047 × the diffused light is below PlateauPart). [APPROX] A compact blue supergiant's plateau is not visible
    /// (the ⁵⁶Ni dome outshines it), and its dome keeps its diffusion: the research gives no recipe for it.
    /// </summary>
    private double Main(double t)
    {
        if (!Plateau) return Diffused(t);
        if (!HasVisiblePlateau) return Math.Max(PlateauPart(t), Diffused(t));
        double s = PlateauStep(t);
        return Math.Max(PlateauPart(t), s > 0 ? s * Diffused(t) : 0);
    }

    private double Cooling(double t)
        => _cooling is { } c && t >= c.StartS && c.FadingLuminosityErgS(t) is var l && l > 0 ? l : 0;

    /// <summary>Bolometric luminosity at source time t (s), erg/s: the brighter of shock cooling and the rest.</summary>
    public double Luminosity(double t) => Math.Max(Cooling(t), Main(t));

    // ---------------------------------------------------------------- colour and magnitude

    /// <summary>
    /// [APPROX] The photosphere's temperature once shock cooling is over (§4.5): hydrogen recombination for a plateau,
    /// the hydrogen-free peak value otherwise.
    /// </summary>
    private double PhotosphereK => Plateau ? HydrogenPhotosphereK : HotPhotosphereK;

    /// <summary>
    /// The shock-cooling colour temperature (K), continued past the formula's end, but [APPROX] never below
    /// <see cref="PhotosphereK"/>: the cooling envelope's photosphere stops cooling where its gas recombines. NaN
    /// without cooling.
    /// </summary>
    private double CoolingColourK(double t)
        => _cooling is { } c && t >= c.StartS ? Math.Max(c.FadingColourTemperatureK(t), PhotosphereK) : double.NaN;

    /// <summary>
    /// B−V of everything except shock cooling (§4.5). A plateau shares the cooling envelope's photosphere: the shock-
    /// cooling colour temperature until it reaches hydrogen recombination's 6,000 K [APPROX].
    /// </summary>
    private double MainBMinusV(double t)
    {
        if (Plateau)
            return BolometricCorrection.BlackbodyBMinusV(_cooling is null ? PhotosphereK : CoolingColourK(t));
        double peakColour = BolometricCorrection.BlackbodyBMinusV(HotPhotosphereK);
        if (!Thermonuclear) return peakColour;
        // Type Ia, Iax, 91T- and 91bg-like: [APPROX] the peak colour until bolometric maximum (taken as V maximum), then
        // linear to the Lira relation's start 30 d later; Lira 30–90 d; held at its 90 d value after. The Lira relation
        // was fitted to normal SNe Ia (Phillips et al. 1999); applying it to Iax and 91bg-like events is an extension.
        double days = (t - MainPeakS) / Day;
        if (days <= 0) return peakColour;
        if (days < LiraStartDays)
            return peakColour + (BolometricCorrection.LiraBMinusV(LiraStartDays) - peakColour) * days / LiraStartDays;
        return BolometricCorrection.LiraBMinusV(Math.Min(days, LiraEndDays));
    }

    /// <summary>
    /// Absolute V magnitude at source time t (s): M_bol of the total luminosity less BC_V at the luminosity-weighted
    /// B−V of shock cooling (a blackbody at T_col, continued past the formula's end) and the rest [APPROX: the
    /// weighting keeps the colour continuous]. NaN when there is no light.
    /// </summary>
    public double AbsoluteV(double t)
    {
        double lc = Cooling(t), lm = Main(t), l = Math.Max(lc, lm);
        if (!(l > 0)) return double.NaN;
        return BolometricCorrection.AbsoluteV(l, BlendedBMinusV(t, lc, lm), BcSample);
    }

    /// <summary>The luminosity-weighted B−V of shock cooling and the rest (see <see cref="AbsoluteV"/>).</summary>
    private double BlendedBMinusV(double t, double lc, double lm)
    {
        double bv = MainBMinusV(t);
        if (lc > 0)
            bv = (lc * BolometricCorrection.BlackbodyBMinusV(CoolingColourK(t)) + lm * bv) / (lc + lm);
        return bv;
    }

    /// <summary>
    /// The colour temperature (K) the light curve's magnitudes use at source time t: the blackbody whose B−V is the
    /// blended colour of <see cref="AbsoluteV"/>, so the schematic spectrum and the light curve agree (Task 12).
    /// </summary>
    public double ColourTemperatureK(double t)
        => BolometricCorrection.BlackbodyTemperatureForBMinusV(BlendedBMinusV(t, Cooling(t), Main(t)));

    // ---------------------------------------------------------------- peaks

    /// <summary>The bolometric peak of <see cref="Main"/> (s), by golden-section search; for the Lira relation.</summary>
    private double MainPeakS => _mainPeakS.Value;

    /// <summary>The time (s) of the brightest absolute V, NaN if there is no light.</summary>
    public double PeakTimeS => _peak.Value.TimeS;

    /// <summary>The brightest absolute V.</summary>
    public double PeakAbsoluteV => _peak.Value.AbsV;

    private (double TimeS, double AbsV) FindPeak()
    {
        var (t, negV) = ArnettDiffusion.Maximize(
            t => AbsoluteV(t) is var v && !double.IsNaN(v) ? -v : double.NegativeInfinity, StartS, LightCurveModel.EndS);
        return double.IsFinite(negV) ? (t, -negV) : (double.NaN, double.NaN);
    }

    /// <summary>
    /// A visible plateau: the plateau, not the diffused heating, sets the light as it ends (false for a compact blue
    /// supergiant, whose ⁵⁶Ni dome outshines its faint plateau).
    /// </summary>
    public bool HasVisiblePlateau => _visiblePlateau.Value;
}
