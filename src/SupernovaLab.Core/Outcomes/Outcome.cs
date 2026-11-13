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
/// in erg, the rest-frame peak energy E_p,i in keV and T90 in s. The neutron-star pins (ruling R41; an outcome that
/// leaves a neutron star or magnetar only) are the birth spin period in s, the surface dipole field in G and the pulsar's
/// 1.4 GHz radio luminosity in mJy kpc², and (ruling R46) the neutron star's gravitational mass in M☉, which sets its
/// neutrino binding energy. <see cref="EquatorialRing"/> (ruling R41) gives the supernova SN 1987A's
/// equatorial ring, whose X-rays <c>CsmInteractionModel</c> then draws. <see cref="RemnantMass"/> (ruling R53) is a
/// published black-hole mass in M☉, for an outcome that leaves a black hole. A null value keeps the model's value.
/// <para>Ruling R44: pins are published values and are trusted. The explosion pins (E, M_ej, ⁵⁶Ni, ⁴⁴Ti, the GRB and
/// neutron-star pins, the ring) are ignored, with a note, when the outcome has no explosion (or no GRB, no neutron star)
/// to apply them to; the radius pin always applies. The ⁵⁶Ni ≤ 0.5 M_ej cap is not applied to pinned values, but a pin
/// set that breaks it gets a note.</para>
/// </summary>
/// <para>Ruling R-P2-27: <see cref="ConfinedCsmOuterCm"/> is observed geometry only (SN 2023ixf's confined gas, structure §S7), drawn by
/// the 3D view and read by no signal model; it does not change the outcome's type or any value.</para>
public sealed record PresetPins(double? ExplosionEnergyErg, double? EjectaMass, double? NickelMass, double? RadiusCm, Source Source,
    double? Ti44Mass = null, double? GrbEisoErg = null, double? GrbEpKeV = null, double? GrbT90S = null,
    double? NsSpinPeriodS = null, double? NsFieldG = null, double? PulsarRadioLumMjyKpc2 = null, bool? EquatorialRing = null,
    double? NsGravitationalMass = null, double? RemnantMass = null, double? ConfinedCsmOuterCm = null);

/// <summary>
/// The lab's result (spec §3). <see cref="Inputs"/> are the inputs as used, held inside their slider ranges.
/// Energies in erg, masses in M☉, luminosity in erg/s, times in s. Quantities that do not apply are null.
/// <see cref="UsesPublishedValues"/> is true when preset pins replaced any model value.
/// <see cref="PeakAbsoluteMagB"/> (mag) is outcomes §8.2 step 9's peak B magnitude, estimated from the engine's peak
/// luminosity with BC_B ≈ 0, set for Type Ia (including Iax) only (ruling R21). It does not shape the light curve. Ruling
/// R53: the decline rate Δm15(B) is measured on the drawn light curve (<c>LightCurveModel.DeclineRateDm15B</c>), not here.
/// <see cref="Ti44Mass"/> is the initial ⁴⁴Ti mass (M☉) of a core-collapse supernova with ⁵⁶Ni (ruling R35): a preset's
/// pin, else 2×10⁻³ × the ⁵⁶Ni mass [APPROX, Speculative]; null for every other outcome, Type Ia included.
/// <see cref="GrbIsotropicEnergyErg"/> (erg), <see cref="GrbPeakEnergyKeV"/> (rest-frame E_p,i, keV) and
/// <see cref="GrbT90S"/> (s) are a preset's gamma-ray burst pins (ruling R36), set only when the outcome has a GRB; null
/// means the classical burst of <c>GrbModel</c>.
/// <see cref="PulsarRadioLuminosityMjyKpc2"/> is a preset's measured 1.4 GHz pulsar luminosity (mJy kpc²; ruling R41),
/// null for the population median <c>PulsarModel</c> uses. <see cref="HasEquatorialRing"/> is true when a preset gives the
/// supernova SN 1987A's equatorial ring (ruling R41). <see cref="ConfinedCsmOuterCm"/> (ruling R-P2-27) is a preset's observed outer
/// radius (cm) of confined circumstellar gas (SN 2023ixf), for the 3D view only: no signal model reads it.
/// </summary>
public sealed record Outcome(LabInputs Inputs, Fate Fate, SupernovaType Type, bool HasGrb, Remnant Remnant,
    PresupernovaStar? Star, Quantity? ExplosionEnergyErg, Quantity? EjectaMass, Quantity? NickelMass,
    Quantity? PeakLuminosityErgS, Quantity? MagnetarEnergyErg, Quantity? MagnetarTimescaleS,
    IReadOnlyList<string> Notes, bool UsesPublishedValues,
    Quantity? PeakAbsoluteMagB = null, Quantity? Ti44Mass = null,
    Quantity? GrbIsotropicEnergyErg = null, Quantity? GrbPeakEnergyKeV = null, Quantity? GrbT90S = null,
    Quantity? PulsarRadioLuminosityMjyKpc2 = null, bool HasEquatorialRing = false, double? ConfinedCsmOuterCm = null);
