namespace SupernovaLab.Core.Structure;

/// <summary>
/// The shock structure at one moment, cm: the forward shock, the contact discontinuity and the reverse shock
/// (R_r at most R_c, R_c at most R_f). Task 8 fills it from <c>ShockStructureModel</c>; until then
/// <see cref="ParticleKinematics.RadiusCm"/> is called with null.
/// </summary>
public sealed record ShockRadii(double ForwardCm, double ContactCm, double ReverseCm);
