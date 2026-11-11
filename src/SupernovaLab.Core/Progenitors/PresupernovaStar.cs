using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Progenitors;

/// <summary>
/// The star at the moment it dies (outcomes §8.2 step 2). Masses in M☉, radius in cm, temperature in K, mass-loss rate
/// in M☉/yr and wind speed in km/s.
/// <list type="bullet">
/// <item><see cref="HeCoreMass"/>: mass inside the point where X(H) = 0.2 (it includes the CO core).</item>
/// <item><see cref="HydrogenEnvelopeMass"/>: the H-rich envelope M_f − M_He, as in outcomes §1.3. This is not the mass
/// of hydrogen itself, which is about X_H × the envelope (X_H ≈ 0.5–0.6, outcomes §1.3).</item>
/// <item><see cref="HeliumEnvelopeMass"/>: the He-rich layer above the CO core, ΔM_He = M_He − M_CO (outcomes §1.3).</item>
/// </list>
/// For a star that has not died, or one that ends as a white dwarf, the record describes that star as it is now.
/// </summary>
public sealed record PresupernovaStar(Quantity FinalMass, Quantity HeCoreMass, Quantity CoCoreMass,
    Quantity HydrogenEnvelopeMass, Quantity HeliumEnvelopeMass, Quantity RadiusCm, Quantity SurfaceTemperatureK,
    Quantity MassLossRateMsunPerYr, Quantity WindSpeedKmS, bool ChemicallyHomogeneous);

/// <summary>What happens to the star before any explosion physics (outcomes §8.2 steps 0–3).</summary>
public enum EarlyFate
{
    /// <summary>Still on the main sequence: younger than its lifetime (step 0).</summary>
    NotDeadYet,
    /// <summary>Ends as a white dwarf with no supernova (step 1), or is the white dwarf of the Type Ia path.</summary>
    WhiteDwarf,
    /// <summary>Electron-capture supernova of an O-Ne-Mg core (step 1).</summary>
    ElectronCapture,
    /// <summary>Iron-core collapse; step 4 decides whether it explodes.</summary>
    CoreCollapse,
    /// <summary>Pulses eject mass, then the core collapses to a black hole (step 3, He core ~35–64 M☉).</summary>
    PulsationalPairInstability,
    /// <summary>Complete disruption, no remnant (step 3, He core 64–133 M☉).</summary>
    PairInstability,
    /// <summary>Direct collapse to a black hole above the pair-instability range (step 3, He core > 133 M☉).</summary>
    DirectCollapseAbovePisn,
}
