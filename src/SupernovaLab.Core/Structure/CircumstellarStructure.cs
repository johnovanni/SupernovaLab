using SupernovaLab.Core.Outcomes;
using SupernovaLab.Core.Signals;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Structure;

/// <summary>
/// The gas around the star the 3D view draws (structure §S7): the star's own wind, ρ ∝ 1/r² from <see cref="WindInnerCm"/> (R*)
/// out to <see cref="WindOuterCm"/> (where its ram pressure meets the interstellar pressure; beyond it, empty), and, for an
/// outcome with dense circumstellar gas (<see cref="CsmInteractionModel.HasDenseCsm"/>), a dense shell from
/// <see cref="ShellInnerCm"/> (R*) to <see cref="ShellOuterCm"/>. Both are null without a dense shell.
/// <see cref="ShellConfidence"/> is <see cref="Confidence.Speculative"/> where §S7 gives no extent and the 1e15 cm fallback is used.
/// </summary>
public sealed record CircumstellarStructure(double WindAStar, double WindInnerCm, double WindOuterCm, double? ShellInnerCm,
    double? ShellOuterCm, Confidence ShellConfidence, Source Source);

/// <summary>The gas around the star for an outcome (structure §S7), and how brightly the shock lights it up.</summary>
public static class CircumstellarStructureModel
{
    /// <summary>[APPROX] §S7: the interstellar pressure p/k = 10⁴ cm⁻³ K that stops the wind (Chevalier 2005, eq. 11).</summary>
    public const double AmbientPressureOverK = 1e4;

    /// <summary>§S7: the dense shell's outer radius for a Type IIn, cm [D] (4,000 km/s × 1,000 d, long-lasting SN 2010jl-like class).</summary>
    public const double IInShellOuterCm = 3e16;

    /// <summary>§S7: the dense shell's outer radius for every other dense-gas outcome, cm (SN 2023ixf's confined gas; [APPROX] where not researched).</summary>
    public const double ConfinedShellOuterCm = 1e15;

    /// <summary>The glow spans this many decades of luminosity below its maximum (brief: 4 decades).</summary>
    public const double GlowDecades = 4;

    /// <summary>The wind extent's source, cited alone by the haze's note when no dense shell is drawn.</summary>
    public const string WindSourceCitation = "Chevalier 2005, ApJ 619, 839 (wind, p/k = 1e4 [APPROX])";

    private static readonly CsmInteractionModel Csm = new();

    private static bool Thermonuclear(Outcome o)
        => o.Type is SupernovaType.Ia or SupernovaType.Ia91T or SupernovaType.Ia91bg or SupernovaType.Iax;

    /// <summary>
    /// The wind's outer extent, cm: r_w = (Ṁ v_w / (4π p))^½ with p = k × <see cref="AmbientPressureOverK"/> (§S7's App block;
    /// ≈ 0.6–2 pc for red supergiants, ≈ 20 pc for a Wolf–Rayet wind). NaN without a wind.
    /// </summary>
    public static double WindOuterExtentCm(double massLossMsunPerYr, double windSpeedKmS)
    {
        if (!(massLossMsunPerYr > 0) || !(windSpeedKmS > 0)) return double.NaN;
        double mdot = massLossMsunPerYr * PhysicalConstants.MSun / PhysicalConstants.Year;   // g/s
        double pressure = AmbientPressureOverK * PhysicalConstants.Boltzmann;                // dyn/cm²
        return Math.Sqrt(mdot * windSpeedKmS * 1e5 / (4 * Math.PI * pressure));
    }

    /// <summary>The surroundings of this outcome; null for a Type Ia (clean surroundings) and for no star.</summary>
    public static CircumstellarStructure? For(Outcome o)
    {
        if (o.Star is not { } star || Thermonuclear(o)) return null;
        double r0 = star.RadiusCm.Value;
        double aStar = CsmInteractionModel.WindAStar(o);
        double outer = WindOuterExtentCm(star.MassLossRateMsunPerYr.Value, star.WindSpeedKmS.Value);
        // No wind (A_* not positive): nothing to draw, the outer radius is the inner.
        bool wind = aStar > 0 && double.IsFinite(outer);
        double inner = double.IsFinite(r0) && r0 > 0 ? r0 : 0;
        double windOuter = wind ? Math.Max(outer, inner) : inner;

        const string windSource = WindSourceCitation;
        bool pinned = o.ConfinedCsmOuterCm is > 0;
        if (!CsmInteractionModel.HasDenseCsm(o) && !pinned)
            return new CircumstellarStructure(wind ? aStar : double.NaN, inner, windOuter, null, null, Confidence.ModelDependent,
                new Source(windSource, "structure §S7"));

        double shellOuter;
        Confidence confidence;
        string shellSource;
        if (pinned)
        {
            // Ruling R-P2-27: a preset's observed confined gas (SN 2023ixf), geometry only; from R* to the pinned radius.
            shellOuter = o.ConfinedCsmOuterCm!.Value;
            confidence = Confidence.ModelDependent;
            shellSource = "Jacobson-Galán et al. 2023, ApJL 954, L42 (SN 2023ixf)";
        }
        else if (o.Type == SupernovaType.IIn)
        {
            shellOuter = IInShellOuterCm;
            confidence = Confidence.ModelDependent;
            shellSource = "Chevalier & Fransson 2017 (SN 2010jl-like IIn [D])";
        }
        else if (o.Type is SupernovaType.IIP or SupernovaType.IIL && o.Fate != Fate.PulsationalPairInstability)
        {
            shellOuter = ConfinedShellOuterCm;
            confidence = Confidence.ModelDependent;
            shellSource = "Jacobson-Galán et al. 2023, ApJL 954, L42 (SN 2023ixf)";
        }
        else
        {
            // §S7 gives no researched extent (Ibn, Icn, pulsational pair instability, other dense-gas inputs): the 1e15 cm fallback.
            shellOuter = ConfinedShellOuterCm;
            confidence = Confidence.Speculative;
            shellSource = "SN 2023ixf's 1e15 cm used as a fallback [APPROX]";
        }
        double shellInner = Math.Min(inner, shellOuter);
        return new CircumstellarStructure(wind ? aStar : double.NaN, inner, windOuter, shellInner, shellOuter, confidence,
            new Source(wind ? $"{shellSource}; wind: {windSource}" : shellSource, "structure §S7"));
    }

    /// <summary>
    /// How brightly the shock lights up the gas it meets now, 0 to 1: the circumstellar model's thermal X-ray luminosity
    /// (<see cref="CsmInteractionModel.ThermalXrayLuminosity"/>) mapped logarithmically over <see cref="GlowDecades"/> decades below
    /// its maximum over the model's range (which is at its start, since L ∝ 1/t). Zero before the shock arrives (the model's
    /// <see cref="CsmInteractionModel.StartTimeS"/>) and for an outcome the model does not apply to.
    /// </summary>
    public static float Glow(Outcome o, double sourceTimeS)
    {
        if (!Csm.Applies(o) || !(CsmInteractionModel.WindAStar(o) > 0)) return 0;
        double start = Csm.StartTimeS(o);
        if (!(sourceTimeS >= start)) return 0;
        double aStar = CsmInteractionModel.WindAStar(o);
        double max = CsmInteractionModel.ThermalXrayLuminosity(aStar, start);
        double l = CsmInteractionModel.ThermalXrayLuminosity(aStar, sourceTimeS);
        double x = 1 + Math.Log10(l / max) / GlowDecades;
        return (float)Math.Clamp(x, 0, 1);
    }
}
