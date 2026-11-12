using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Signals;

/// <summary>One spectral line in a <see cref="Spectrum"/>: its label, rest wavelength (Å) and relative strength (0–1).</summary>
public sealed record SpectralLine(string Label, double RestWavelengthAngstrom, double Strength);

/// <summary>
/// A schematic optical spectrum (signals §10, §11 "Spectrograph"): 700 points over 3,000–10,000 Å with the flux scaled to
/// a maximum of 1. All arrays are empty outside the model's range (see <see cref="SpectrumModel.At"/>).
/// <see cref="PhotosphereVelocityKmS"/> is the line-blueshift velocity used; <see cref="ColorTemperatureK"/> the continuum.
/// </summary>
public sealed record Spectrum(double[] WavelengthsAngstrom, double[] RelativeFlux, IReadOnlyList<SpectralLine> Lines,
    double PhotosphereVelocityKmS, double ColorTemperatureK, bool Nebular);

/// <summary>
/// The visible-light panel's spectrum view (signals §10, spec §5): a blackbody at the light curve's colour temperature
/// (<see cref="LightCurveModel.ColourTemperatureK"/>, so the two agree), multiplied by P-Cygni lines from the §10.1 list
/// at the §10.2 velocities. Not an <see cref="ISignalModel"/>; the panel calls it directly. The Nugent templates are NOT
/// used (§10.3: no license is stated).
/// <list type="bullet">
/// <item><b>Velocity</b> (§10.2): v(t) = v_ref (t / t_ref)^−0.46, floored at 0.3 v_ref [APPROX, the research's schematic
/// rule for every type], where t_ref is the light curve's V peak (<see cref="LightCurveModel.PeakTimeS"/>) and v_ref the
/// §10.2 value at V maximum: Ia Si II 6355 10,600 km/s (Wang et al. 2009); IIb Fe II 8,400; Ib Fe II 9,100; Ic Fe II
/// 11,000 (Liu et al. 2016); Ic-BL Fe II 16,700 without a GRB and 19,200 with one (Modjaz et al. 2016; the value that
/// excludes SN 2010bh, whose peculiar 23,800 ± 9,500 average is poorly constrained). Hydrogen-rich types (II-P, II-L, II-n,
/// SLSN-II) anchor Hα at the day-50 median 7,300 km/s (Gutiérrez et al. 2017) and follow the law back to the V peak
/// (ruling R43), holding the value there only before the peak. <b>For the other types, before t_ref the velocity
/// is held at v_ref</b> [APPROX]: the research gives the law only after maximum, and extrapolated back it diverges
/// (an Ia at 1 d would reach 40,000 km/s). Holding underestimates early velocities.</item>
/// <item><b>Line width</b> (§10.2): FWHM = (v / c) λ. For an Ic-BL the lines are therefore wider than 15,000 km/s at
/// peak. Interacting types (II-n, Ib-n, Ic-n) show narrow emission lines of 1,000 km/s FWHM [APPROX] instead.</item>
/// <item><b>P-Cygni shape</b> [APPROX]: continuum × Π(1 − a G(λ; λ₀(1 − v/c))) × (1 + Σ e G(λ; λ₀)), Gaussians of FWHM
/// (v/c) λ₀; the absorption depth a = 0.5 s and emission height e = 0.3 s for line strength s. The velocity convention
/// is the observers' Δλ/λ₀ = v/c.</item>
/// <item><b>Nebular phase</b> [APPROX]: from 150 d (core collapse) or 200 d (Type Ia) the photospheric lines give way
/// to the §10.1 nebular emission lines (core collapse [O I] 6300/6364 and [Ca II] 7291/7324, Hα in hydrogen-rich types;
/// Ia [Fe II]/[Fe III]) on a continuum at 10% of the blackbody; the research gives no switch time. Lines keep the
/// law's width.</item>
/// <item>Line strengths, the weaker lines' wavelengths (Fe II, Ca II, S II, Na I D, [Fe II/III]) and their evolution are
/// [APPROX] choices; the research lists the lines but gives no strengths. SLSN-I and Ic-BL use the Ic list; Iax, 91T- and
/// 91bg-like use the normal Ia list and velocity; pair-instability supernovae use the II or Ic list by envelope.</item>
/// </list>
/// Valid from 1 day to 1 yr after core collapse (ignition for a Type Ia), source time; empty arrays outside that range
/// and for outcomes with no modeled light curve (failed supernovae). Speculative throughout (ruling R24/R27 reasoning).
/// </summary>
public sealed class SpectrumModel
{
    private const double Day = PhysicalConstants.Day;

    /// <summary>The number of wavelength points and the range, Å.</summary>
    public const int PointCount = 700;
    public const double MinAngstrom = 3000, MaxAngstrom = 10000;

    /// <summary>[APPROX] Time the nebular lines take over: core collapse 150 d, Type Ia 200 d.</summary>
    public const double CoreCollapseNebularDays = 150, ThermonuclearNebularDays = 200;

    /// <summary>§10.2 [APPROX]: the exponent of v(t) ∝ t^−0.46 and the floor 0.3 v_ref.</summary>
    public const double VelocityExponent = 0.46, VelocityFloor = 0.3;

    /// <summary>[APPROX] The width of a circumstellar-interaction (narrow) line, km/s FWHM.</summary>
    public const double NarrowLineKmS = 1000;

    private const double AbsorptionDepthPerStrength = 0.5, EmissionPerStrength = 0.3, NebularContinuum = 0.1;
    private const double HydrogenDay50KmS = 7300, HydrogenReferenceDays = 50;

    private static readonly LightCurveModel Curves = new();

    public Confidence Confidence => Confidence.Speculative;

    public IReadOnlyList<Source> Sources { get; } = new Source[]
    {
        new("Filippenko 1997, ARA&A 35, 309; Gal-Yam 2017, Handbook of Supernovae (arXiv:1611.09353): line list by " +
            "type. Line strengths, the weaker lines' wavelengths and their evolution are invented [APPROX]", "signals §10.1"),
        new("Velocities at V maximum: Wang et al. 2009, ApJ 699, L139 (Ia); Gutiérrez et al. 2017, ApJ 850, 89 (II, Hα day 50); " +
            "Liu et al. 2016, ApJ 827, 90 (IIb, Ib, Ic); Modjaz et al. 2016, ApJ 832, 108 (Ic-BL). Time law v ∝ t^-0.46 with a " +
            "0.3 v_peak floor [APPROX]; held at v_peak before the reference time [APPROX]; FWHM = v/c × λ", "signals §10.2"),
        new("Schematic P-Cygni profile (Gaussian absorption blueshifted by v, emission at rest), the nebular switch times " +
            "(150 d core collapse, 200 d Type Ia) and the nebular continuum are [APPROX]. The Nugent templates are not used " +
            "(no license stated)", "signals §10.3, §11"),
        new("Continuum: a blackbody (Planck 1901; CODATA 2018 h, c, k_B) at the colour temperature of the light curve " +
            "(Lyman et al. 2014 colours, §4.5)", "signals §4.5"),
    };

    // ---------------------------------------------------------------- line lists

    private enum Family { HydrogenRich, IIb, Ib, Ic, Ia, Interacting }

    private sealed record LineDef(string Label, double Angstrom, double Base, Func<double, double>? Evolution = null);

    private static double Ramp(double days, double start, double full, double lo = 0)
        => Math.Clamp(lo + (1 - lo) * (days - start) / (full - start), lo, 1);

    private static readonly LineDef[] CaHK =
    {
        new("Ca II K 3934", 3934, 0.5), new("Ca II H 3968", 3968, 0.4),
    };

    private static LineDef[] CaIr(double s, Func<double, double>? evo = null) => new LineDef[]
    {
        new("Ca II 8498", 8498, s * 0.8, evo), new("Ca II 8542", 8542, s, evo), new("Ca II 8662", 8662, s * 0.9, evo),
    };

    // §10.1 photospheric line lists. Base strengths [APPROX].
    private static readonly LineDef[] HydrogenRichLines =
    [
        new("Hα", 6563, 1.0), new("Hβ", 4861, 0.7), new("Fe II 5169", 5169, 0.5), new("Na I D", 5893, 0.4),
        // "Ca II IR triplet later" (§10.1): ramps in over days 20–60 [APPROX].
        .. CaIr(0.8, d => Ramp(d, 20, 60)),
        .. CaHK,
    ];

    private static readonly LineDef[] IIbLines =
    [
        // "H early, He I strengthening within weeks" (§10.1) [APPROX]: Hα full to day 10, falling to 0.3 by day 40;
        // He I rising from 0.1 at day 1 to full at day 30.
        new("Hα", 6563, 1.0, d => 1 - 0.7 * Ramp(d, 10, 40)),
        new("He I 5876", 5876, 1.0, d => Ramp(d, 0, 30, 0.1)),
        new("He I 6678", 6678, 0.6, d => Ramp(d, 0, 30, 0.1)),
        new("He I 7065", 7065, 0.7, d => Ramp(d, 0, 30, 0.1)),
        new("Fe II 5169", 5169, 0.5), .. CaHK, .. CaIr(0.6),
    ];

    private static readonly LineDef[] IbLines =
    [
        new("He I 5876", 5876, 1.0), new("He I 6678", 6678, 0.6), new("He I 7065", 7065, 0.7),
        new("Fe II 5169", 5169, 0.5), new("O I 7774", 7774, 0.5), .. CaHK, .. CaIr(0.6),
    ];

    private static readonly LineDef[] IcLines =
    [
        new("O I 7774", 7774, 1.0), new("Fe II 5169", 5169, 0.5), .. CaHK, .. CaIr(0.8),
    ];

    private static readonly LineDef[] IaLines =
    [
        new("Si II 6355", 6355, 1.0), new("S II 5454", 5454, 0.5), new("S II 5640", 5640, 0.5), .. CaHK, .. CaIr(0.7),
    ];

    // §10.1 nebular lines. [O I] 6300:6364 = 3:1 and [Ca II] 7291:7324 = 3:2 (optically thin ratios) [APPROX].
    private static readonly LineDef[] NebularCoreCollapse =
    {
        new("[O I] 6300", 6300, 1.0), new("[O I] 6364", 6364, 1.0 / 3), new("[Ca II] 7291", 7291, 0.6),
        new("[Ca II] 7324", 7324, 0.4),
    };

    private static readonly LineDef[] NebularIa =
    {
        new("[Fe III] 4658", 4658, 0.8), new("[Fe II] 5159", 5159, 0.6), new("[Fe II] 7155", 7155, 0.5),
    };

    private static readonly LineDef[] InteractingH = { new("Hα", 6563, 1.0), new("Hβ", 4861, 0.5) };
    private static readonly LineDef[] InteractingHe = { new("He I 5876", 5876, 1.0), new("He I 6678", 6678, 0.6), new("He I 7065", 7065, 0.7) };
    private static readonly LineDef[] InteractingCO = { new("C III 5696", 5696, 0.8), new("O I 7774", 7774, 0.6) };

    // ---------------------------------------------------------------- public API

    /// <summary>
    /// The spectrum at this source time (s after core collapse, or ignition for a Type Ia). Empty arrays before 1 day,
    /// after 1 yr, and when the outcome has no modeled light curve (a failed supernova or no explosion).
    /// </summary>
    public Spectrum At(Outcome o, double sourceTimeS)
    {
        var empty = new Spectrum(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<SpectralLine>(), 0, 0, false);
        if (!(sourceTimeS >= Day && sourceTimeS <= PhysicalConstants.Year)) return empty;
        var lc = Curves;
        double temperature = lc.ColourTemperatureK(o, sourceTimeS);
        if (!double.IsFinite(temperature)) return empty;

        double days = sourceTimeS / Day;
        bool thermonuclear = o.Type is SupernovaType.Ia or SupernovaType.Ia91T or SupernovaType.Ia91bg or SupernovaType.Iax;
        Family family = FamilyOf(o);
        bool nebular = days >= (thermonuclear ? ThermonuclearNebularDays : CoreCollapseNebularDays);

        double vKmS = VelocityKmS(o, family, lc, sourceTimeS);
        bool narrow = family == Family.Interacting;

        // Line selection: strength at this time.
        var defs = nebular ? NebularLines(o, family, thermonuclear) : PhotosphericLines(o, family);
        var lines = new List<(SpectralLine Line, double WidthKmS)>();
        foreach (var d in defs)
        {
            double s = d.Base * (d.Evolution?.Invoke(days) ?? 1);
            if (s > 0) lines.Add((new SpectralLine(d.Label, d.Angstrom, s), narrow && !nebular ? NarrowLineKmS : vKmS));
        }

        var wl = new double[PointCount];
        var flux = new double[PointCount];
        for (int i = 0; i < PointCount; i++) wl[i] = MinAngstrom + (MaxAngstrom - MinAngstrom) * i / (PointCount - 1);

        var bb = new double[PointCount];
        double bbMax = 0;
        for (int i = 0; i < PointCount; i++) { bb[i] = Planck(wl[i], temperature); bbMax = Math.Max(bbMax, bb[i]); }

        for (int i = 0; i < PointCount; i++)
        {
            double cont = bb[i] / bbMax;
            if (nebular)
            {
                double sum = 0;
                foreach (var (line, w) in lines) sum += line.Strength * Gauss(wl[i], line.RestWavelengthAngstrom, FwhmA(line.RestWavelengthAngstrom, w));
                flux[i] = NebularContinuum * cont + sum;
            }
            else
            {
                double absorption = 1, emission = 1;
                // Narrow interacting lines are emission only, as tall as the continuum times the strength [APPROX].
                double eHeight = narrow ? 1.0 : EmissionPerStrength;
                foreach (var (line, w) in lines)
                {
                    double fwhm = FwhmA(line.RestWavelengthAngstrom, w);
                    emission += eHeight * line.Strength * Gauss(wl[i], line.RestWavelengthAngstrom, fwhm);
                    if (!narrow)
                        absorption *= 1 - AbsorptionDepthPerStrength * line.Strength
                                      * Gauss(wl[i], line.RestWavelengthAngstrom * (1 - vKmS / (PhysicalConstants.C * 1e-5)), fwhm);
                }
                flux[i] = cont * absorption * emission;
            }
        }

        double max = flux.Max();
        for (int i = 0; i < PointCount; i++) flux[i] /= max;
        return new Spectrum(wl, flux, lines.Select(l => l.Line).ToArray(), vKmS, temperature, nebular);
    }

    /// <summary>The §10.2 reference velocity v_ref (km/s) for this outcome, before the time law.</summary>
    public static double ReferenceVelocityKmS(Outcome o) => ReferenceVelocityKmS(o, FamilyOf(o));

    // ---------------------------------------------------------------- internals

    private static bool UsesHydrogenClock(Outcome o, Family f)
        => o.Type is SupernovaType.IIn or SupernovaType.IIP or SupernovaType.IIL or SupernovaType.SlsnII
           || (o.Type == SupernovaType.Pisn && f == Family.HydrogenRich);

    private static Family FamilyOf(Outcome o)
    {
        bool hydrogen = o.Star is { } s && s.HydrogenEnvelopeMass.Value > 0;
        return o.Type switch
        {
            SupernovaType.IIP or SupernovaType.IIL or SupernovaType.SlsnII => Family.HydrogenRich,
            SupernovaType.Pisn => hydrogen ? Family.HydrogenRich : Family.Ic,
            SupernovaType.IIn or SupernovaType.Ibn or SupernovaType.Icn => Family.Interacting,
            SupernovaType.IIb => Family.IIb,
            SupernovaType.Ib => Family.Ib,
            SupernovaType.Ic or SupernovaType.IcBL or SupernovaType.SlsnI => Family.Ic,
            SupernovaType.Ia or SupernovaType.Ia91T or SupernovaType.Ia91bg or SupernovaType.Iax => Family.Ia,
            _ => Family.Ic,
        };
    }

    private static double ReferenceVelocityKmS(Outcome o, Family f) => f switch
    {
        Family.Ia => 10_600,
        Family.IIb => 8_400,
        Family.Ib => 9_100,
        Family.HydrogenRich => HydrogenDay50KmS,
        Family.Interacting => o.Type == SupernovaType.IIn ? HydrogenDay50KmS : 11_000,
        _ => o.Type == SupernovaType.IcBL ? (o.HasGrb ? 19_200 : 16_700) : 11_000,
    };

    private static double VelocityKmS(Outcome o, Family f, LightCurveModel lc, double sourceTimeS)
    {
        double vRef = ReferenceVelocityKmS(o, f);
        if (UsesHydrogenClock(o, f))
        {
            // Ruling R43: the law v = 7,300 (t / 50 d)^-0.46 holds back to the light curve's V peak and the velocity is
            // held at its value there only before the peak.
            double peak = lc.PeakTimeS(o);
            double t = double.IsFinite(peak) ? Math.Max(sourceTimeS, peak) : sourceTimeS;
            return vRef * Math.Max(VelocityFloor, Math.Pow(t / (HydrogenReferenceDays * Day), -VelocityExponent));
        }
        double tRef = lc.PeakTimeS(o);
        if (!(sourceTimeS > tRef)) return vRef;
        return vRef * Math.Max(VelocityFloor, Math.Pow(sourceTimeS / tRef, -VelocityExponent));
    }

    private static IEnumerable<LineDef> PhotosphericLines(Outcome o, Family f) => f switch
    {
        Family.HydrogenRich => HydrogenRichLines,
        Family.IIb => IIbLines,
        Family.Ib => IbLines,
        Family.Ic => IcLines,
        Family.Ia => IaLines,
        _ => o.Type switch
        {
            SupernovaType.IIn => InteractingH,
            SupernovaType.Ibn => InteractingHe,
            _ => InteractingCO,
        },
    };

    private static IEnumerable<LineDef> NebularLines(Outcome o, Family f, bool thermonuclear)
    {
        if (thermonuclear) return NebularIa;
        bool hydrogen = f == Family.HydrogenRich || o.Type == SupernovaType.IIn;
        // Residual Hα in a IIb [APPROX] is left out: by 150 d it has faded.
        return hydrogen ? NebularCoreCollapse.Append(new LineDef("Hα", 6563, 1.0)) : NebularCoreCollapse;
    }

    private static double FwhmA(double restA, double widthKmS) => widthKmS / (PhysicalConstants.C * 1e-5) * restA;

    /// <summary>A unit-height Gaussian at <paramref name="centre"/> with this FWHM (Å).</summary>
    private static double Gauss(double x, double centre, double fwhm)
    {
        double u = (x - centre) / fwhm;
        return Math.Exp(-4 * Math.Log(2) * u * u);
    }

    /// <summary>Planck's B_λ up to the constant 2hc²: λ⁻⁵ / (exp(hc/(λ k T)) − 1), λ in Å.</summary>
    private static double Planck(double angstrom, double temperatureK)
    {
        double lambdaCm = angstrom * 1e-8;
        double x = PhysicalConstants.Planck * PhysicalConstants.C / (lambdaCm * PhysicalConstants.Boltzmann * temperatureK);
        return Math.Pow(lambdaCm, -5) / (Math.Exp(x) - 1);
    }
}
