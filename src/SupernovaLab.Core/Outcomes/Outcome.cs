using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Outcomes;

/// <summary>How the star ends (spec §3).</summary>
public enum Fate
{
    /// <summary>Still on the main sequence.</summary>
    NotDeadYet,
    /// <summary>Ends as a white dwarf with no supernova.</summary>
    WhiteDwarf,
    /// <summary>A successful explosion (it may still leave a black hole by fallback).</summary>
    Supernova,
    /// <summary>The core collapses to a black hole without an explosion.</summary>
    FailedSupernova,
    /// <summary>The whole star is blown apart by pair instability; no remnant.</summary>
    PairInstabilitySupernova,
    /// <summary>Pair-instability pulses throw off mass, then the core collapses to a black hole.</summary>
    PulsationalPairInstability,
}

/// <summary>
/// Published values that a preset puts in place of the model's own (spec §2.3): explosion energy in erg, ejecta and
/// ⁵⁶Ni masses in M☉ and progenitor radius in cm, and the initial ⁴⁴Ti mass in M☉ (ruling R35; a core-collapse
/// supernova only). The gamma-ray burst pins (ruling R36; an outcome with a GRB only) are the isotropic-equivalent energy
/// in erg, the rest-frame peak energy E_p,i in keV and T90 in s. A null value keeps the model's value.
/// </summary>
public sealed record PresetPins(double? ExplosionEnergyErg, double? EjectaMass, double? NickelMass, double? RadiusCm, Source Source,
    double? Ti44Mass = null, double? GrbEisoErg = null, double? GrbEpKeV = null, double? GrbT90S = null);

/// <summary>
/// The lab's result (spec §3). <see cref="Inputs"/> are the inputs as used, held inside their slider ranges.
/// Energies in erg, masses in M☉, luminosity in erg/s, times in s. Quantities that do not apply are null.
/// <see cref="UsesPublishedValues"/> is true when preset pins replaced any model value.
/// <see cref="PeakAbsoluteMagB"/> (mag) and <see cref="DeclineRateDm15B"/> (Δm15(B), mag) are the outcomes §8.2 step 9
/// Phillips outputs, set for Type Ia (including Iax) only (ruling R21).
/// <see cref="Ti44Mass"/> is the initial ⁴⁴Ti mass (M☉) of a core-collapse supernova with ⁵⁶Ni (ruling R35): a preset's
/// pin, else 2×10⁻³ × the ⁵⁶Ni mass [APPROX, Speculative]; null for every other outcome, Type Ia included.
/// <see cref="GrbIsotropicEnergyErg"/> (erg), <see cref="GrbPeakEnergyKeV"/> (rest-frame E_p,i, keV) and
/// <see cref="GrbT90S"/> (s) are a preset's gamma-ray burst pins (ruling R36), set only when the outcome has a GRB; null
/// means the classical burst of <c>GrbModel</c>.
/// </summary>
public sealed record Outcome(LabInputs Inputs, Fate Fate, SupernovaType Type, bool HasGrb, Remnant Remnant,
    PresupernovaStar? Star, Quantity? ExplosionEnergyErg, Quantity? EjectaMass, Quantity? NickelMass,
    Quantity? PeakLuminosityErgS, Quantity? MagnetarEnergyErg, Quantity? MagnetarTimescaleS,
    IReadOnlyList<string> Notes, bool UsesPublishedValues,
    Quantity? PeakAbsoluteMagB = null, Quantity? DeclineRateDm15B = null, Quantity? Ti44Mass = null,
    Quantity? GrbIsotropicEnergyErg = null, Quantity? GrbPeakEnergyKeV = null, Quantity? GrbT90S = null);
