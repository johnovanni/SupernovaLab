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
/// ⁵⁶Ni masses in M☉ and progenitor radius in cm. A null value keeps the model's value.
/// </summary>
public sealed record PresetPins(double? ExplosionEnergyErg, double? EjectaMass, double? NickelMass, double? RadiusCm, Source Source);

/// <summary>
/// The lab's result (spec §3). <see cref="Inputs"/> are the inputs as used, held inside their slider ranges.
/// Energies in erg, masses in M☉, luminosity in erg/s, times in s. Quantities that do not apply are null.
/// <see cref="UsesPublishedValues"/> is true when preset pins replaced any model value.
/// <see cref="PeakAbsoluteMagB"/> (mag) and <see cref="DeclineRateDm15B"/> (Δm15(B), mag) are the outcomes §8.2 step 9
/// Phillips outputs, set for Type Ia (including Iax) only (ruling R21).
/// </summary>
public sealed record Outcome(LabInputs Inputs, Fate Fate, SupernovaType Type, bool HasGrb, Remnant Remnant,
    PresupernovaStar? Star, Quantity? ExplosionEnergyErg, Quantity? EjectaMass, Quantity? NickelMass,
    Quantity? PeakLuminosityErgS, Quantity? MagnetarEnergyErg, Quantity? MagnetarTimescaleS,
    IReadOnlyList<string> Notes, bool UsesPublishedValues,
    Quantity? PeakAbsoluteMagB = null, Quantity? DeclineRateDm15B = null);
