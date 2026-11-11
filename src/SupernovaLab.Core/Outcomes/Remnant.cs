using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Outcomes;

/// <summary>What the explosion or collapse leaves behind (spec §3).</summary>
public enum RemnantKind { None, WhiteDwarf, NeutronStar, Magnetar, BlackHole }

/// <summary>
/// The compact remnant (outcomes §6, §8.2 step 8). Mass in M☉ (gravitational for a neutron star), radius in km, spin
/// period in s, field in G, kick in km/s. <see cref="FormationTimeS"/> is t_BH, the time after bounce when the black
/// hole forms (signals §1.3); it is null for other remnants. Properties that do not apply are null.
/// </summary>
public sealed record Remnant(RemnantKind Kind, Quantity? Mass, Quantity? RadiusKm, Quantity? SpinPeriodS,
    Quantity? MagneticFieldG, Quantity? KickKmS, Quantity? FormationTimeS)
{
    /// <summary>No remnant: the star has not died, or it was destroyed completely.</summary>
    public static Remnant None { get; } = new(RemnantKind.None, null, null, null, null, null, null);
}
