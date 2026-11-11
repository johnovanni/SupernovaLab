using SupernovaLab.Core.Units;

namespace SupernovaLab.Core.Observer;

/// <summary>Where the observer is, how much dust lies in the way, and whether the beam points at Earth.</summary>
public sealed record ObserverSetup(double DistancePc, double Av, bool BeamTowardEarth)
{
    /// <summary>Below this distance cosmological effects are ignored (signals §0.3, [APPROX]).</summary>
    private const double CosmologyThresholdMpc = 50;

    /// <summary>10,000 pc, no extinction, beam toward Earth.</summary>
    public static ObserverSetup Default { get; } = new(10_000, 0, true);

    private double DistanceMpc => DistancePc / 1e6;

    /// <summary>0 below 50 Mpc; above, the redshift whose comoving distance equals the distance.</summary>
    public double Redshift => DistanceMpc < CosmologyThresholdMpc
        ? 0
        : CosmologyModel.Default.RedshiftAtComovingDistance(DistanceMpc);

    /// <summary>Luminosity distance D (1+z), cm.</summary>
    public double LuminosityDistanceCm => DistancePc * PhysicalConstants.Pc * (1 + Redshift);

    /// <summary>1 + z.</summary>
    public double TimeDilation => 1 + Redshift;

    /// <summary>5 log10(D_L / 10 pc).</summary>
    public double DistanceModulus => 5 * Math.Log10(LuminosityDistanceCm / (10 * PhysicalConstants.Pc));

    /// <summary>Light travel time at the given distance, years.</summary>
    public double LightTravelYears => DistancePc * PhysicalConstants.Pc / (PhysicalConstants.C * PhysicalConstants.Year);
}
