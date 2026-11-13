using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Structure;

/// <summary>
/// The density-profile class of the ejecta (structure §S1 App block): a hydrogen-rich star at least
/// √(500 × 50) = 158 R☉ is a red supergiant (convective envelope, n = 12); any other hydrogen-rich star, a compact IIb
/// included, a blue supergiant (n = 10.18); a star that is not hydrogen-rich is stripped (n = 10.18); a thermonuclear
/// explosion is a Type Ia (n = 10). δ = 1 for every class.
/// </summary>
public enum EjectaClass { RedSupergiant, BlueSupergiant, StrippedEnvelope, TypeIa }

/// <summary>One composition layer's mass, M☉.</summary>
public sealed record LayerMass(ElementLayer Layer, double MassMsun);

/// <summary>
/// The Rayleigh–Taylor mixing drawn for a class (structure §S3 App block, ruling R-P2-13). <see cref="NickelMaxCmS"/> is
/// the finger-tip speed nickel is pushed out to; <see cref="HydrogenMinCmS"/> the speed hydrogen is pulled in to (NaN when
/// the class has no hydrogen to mix, and both NaN without mixing). <see cref="FingerCount"/> cones with seeded isotropic
/// axes together cover <see cref="CoveredFraction"/> of the sphere; <see cref="NickelFingerFraction"/> (F_Ni) of the
/// nickel particles are redirected into them. The finger half-angle is not a published number: it follows from K, f and
/// the seeded axes (<see cref="MixingModel.Cones"/>). <see cref="Confidence"/> is the weakest of the row's values.
/// </summary>
public sealed record MixingReach(double NickelMaxCmS, double HydrogenMinCmS, int FingerCount, double CoveredFraction,
    double NickelFingerFraction, Confidence Confidence, Source Source)
{
    /// <summary>No mixing: Type Ia ejecta are stratified (Mazzali et al. 2007, structure §S2.2, §S3).</summary>
    public static MixingReach None { get; } = new(double.NaN, double.NaN, 0, 0, 0, Confidence.ModelDependent,
        new Source("Mazzali et al. 2007, Science 315, 825: stratified ejecta, no fingers", "structure §S2.2, §S3"));

    /// <summary>Whether any fingers are drawn.</summary>
    public bool HasFingers => FingerCount > 0 && CoveredFraction > 0;
}

/// <summary>
/// The ejecta's structure for the 3D view (structure §S1–§S3, §S10). Masses in M☉, energy in erg, speeds in cm/s.
/// <list type="bullet">
/// <item><see cref="Layers"/>: the layers present (mass &gt; 0), centre outward (ruling P3), summing to
/// <see cref="EjectaMassMsun"/>.</item>
/// <item><see cref="Delta"/>, <see cref="N"/>, <see cref="TransitionCmS"/>: the broken power law ρt³ ∝ (v/v_t)^−δ inside
/// v_t and (v/v_t)^−n outside (§S1). <see cref="MaxCmS"/> is the speed enclosing 99.9% of the mass (ruling P1).</item>
/// <item><see cref="Frame99CmS"/>: the speed enclosing 99% of the particle mass after mixing (ruling P1's framing
/// radius), the expectation over the seeded fingers; see <see cref="EjectaStructureModel.For"/>.</item>
/// <item><see cref="LayerScale"/>: ruling P2's common factor on the non-nickel layers (1 for a Type Ia).</item>
/// <item><see cref="Notes"/>: scene notes (the layer-scale note, schematic layering, the §S10 shell label).</item>
/// </list>
/// A failed supernova's weak envelope ejection (§S10) is a thin shell: <see cref="EnvelopeShellLaunchS"/> is set, every
/// particle moves at <see cref="MaxCmS"/> = v_s, and <see cref="Delta"/> and <see cref="N"/> are 0 (unused).
/// </summary>
public sealed record EjectaStructure(IReadOnlyList<LayerMass> Layers, double EjectaMassMsun, double EnergyErg, double Delta,
    double N, double TransitionCmS, double MaxCmS, double Frame99CmS, MixingReach Mixing, double LayerScale,
    IReadOnlyList<string> Notes)
{
    /// <summary>The profile class (§S1).</summary>
    public EjectaClass Class { get; init; }

    /// <summary>
    /// For a failed supernova's envelope shell (§S10): the source time (s after bounce) at which the pressure pulse reaches
    /// the star's surface and the shell starts to coast, r = R* + v_s (t − t_s). Null for an explosion.
    /// </summary>
    public double? EnvelopeShellLaunchS { get; init; }

    /// <summary>Whether this is a failed supernova's thin envelope shell (§S10) rather than an explosion's ejecta.</summary>
    public bool IsEnvelopeShell => EnvelopeShellLaunchS is not null;
}
