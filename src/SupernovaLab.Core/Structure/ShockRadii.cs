namespace SupernovaLab.Core.Structure;

/// <summary>
/// The shock structure at one moment, cm: the forward shock, the contact discontinuity and the reverse shock
/// (R_r at most R_c, R_c at most R_f), from <see cref="ShockStructureModel.At"/>. <see cref="ConfinementWeight"/> is ruling
/// R-P2-4's w = clamp(log10(t / t_start), 0, 1): how far the particles behind the reverse shock have been moved from free
/// expansion to their confined place (<see cref="ParticleKinematics.RadiusCm"/>), so nothing jumps when the shocks appear.
/// </summary>
public sealed record ShockRadii(double ForwardCm, double ContactCm, double ReverseCm)
{
    /// <summary>Ruling R-P2-4's weight, 0 to 1 (1 for radii built by hand).</summary>
    public double ConfinementWeight { get; init; } = 1;
}
