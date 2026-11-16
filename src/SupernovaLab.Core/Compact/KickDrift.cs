using SupernovaLab.Core.Observer;
using SupernovaLab.Core.Outcomes;

namespace SupernovaLab.Core.Compact;

/// <summary>
/// The compact object's drift from its kick (spec §4.6; compact §C4 App). The speed is <c>Remnant.KickKmS</c>; the
/// direction is the preset's measured position angle placed in the sky plane, else drawn from the run number alone
/// (display randomness, not physics).
/// </summary>
public static class KickDrift
{
    /// <summary>The readout's note on a measured, sky-plane direction.</summary>
    public const string LineOfSightNote = "Kick: direction measured on the sky; its line-of-sight part is unknown and set to zero";

    /// <summary>
    /// The unit direction of the kick in the scene frame. With a pinned position angle θ: d = (−sin θ, cos θ cos ζ, cos θ sin ζ),
    /// scene north cos θ · N plus scene east sin θ · (−x), with the line-of-sight part zero (§C4 App). Otherwise a direction
    /// drawn isotropically from <paramref name="runNumber"/> only.
    /// </summary>
    public static Vec3 Direction(Outcome o, ObserverSetup obs, int runNumber)
    {
        if (o.KickPositionAngleDeg is not double angleDeg) return SeededIsotropic(runNumber);
        double theta = angleDeg * Math.PI / 180, zeta = Magnetosphere.EarthAngleRad(o, obs);
        return new Vec3(-Math.Sin(theta), Math.Cos(theta) * Math.Cos(zeta), Math.Cos(theta) * Math.Sin(zeta));
    }

    /// <summary>The compact object's displacement from the explosion's centre, cm: direction × kick speed × time. Zero without a kick.</summary>
    public static Vec3 OffsetCm(Outcome o, ObserverSetup obs, int runNumber, double sourceTimeS)
    {
        if (o.Remnant.KickKmS is not { Value: > 0 } kick || !(sourceTimeS > 0)) return default;
        return Direction(o, obs, runNumber) * (kick.Value * 1e5 * sourceTimeS);
    }

    /// <summary>A uniformly random direction on the sphere from a SplitMix64 stream seeded by the run number.</summary>
    private static Vec3 SeededIsotropic(int runNumber)
    {
        ulong state = (ulong)(uint)runNumber * 0x9E3779B97F4A7C15UL + 0x2545F4914F6CDD1DUL;
        double u1 = Next(ref state), u2 = Next(ref state);
        double cosTheta = 2 * u1 - 1, sinTheta = Math.Sqrt(Math.Max(0, 1 - cosTheta * cosTheta)), phi = 2 * Math.PI * u2;
        return new Vec3(sinTheta * Math.Cos(phi), sinTheta * Math.Sin(phi), cosTheta);
    }

    /// <summary>The next uniform number in [0, 1) (SplitMix64; the top 53 bits).</summary>
    private static double Next(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return (z >> 11) * (1.0 / (1UL << 53));
    }
}
