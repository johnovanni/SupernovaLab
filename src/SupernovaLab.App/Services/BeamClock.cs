using System.Diagnostics;

namespace SupernovaLab.App.Services;

/// <summary>
/// The one wall clock the pulsar beams turn by (plan ruling P3-5). It is made once, at start-up, and handed to both scene controllers
/// (the main view's and the Close-up's), so a beam drawn in each window has the same rotation phase at the same moment:
/// phase = 2π · wall seconds / drawn period.
/// </summary>
public sealed class BeamClock
{
    private readonly Func<double> _seconds;

    /// <summary>A clock that starts now (a stopwatch).</summary>
    public BeamClock()
    {
        var watch = Stopwatch.StartNew();
        _seconds = () => watch.Elapsed.TotalSeconds;
    }

    /// <summary>A clock reading these wall seconds (tests pass a fake).</summary>
    public BeamClock(Func<double> seconds) => _seconds = seconds ?? throw new ArgumentNullException(nameof(seconds));

    /// <summary>Wall seconds since the clock started.</summary>
    public double ElapsedS => _seconds();

    /// <summary>The rotation phase (radians, not wrapped) of something drawn turning once per <paramref name="drawnPeriodS"/>; 0 for a bad period.</summary>
    public double PhaseRad(double drawnPeriodS)
        => drawnPeriodS > 0 && double.IsFinite(drawnPeriodS) ? 2 * Math.PI * ElapsedS / drawnPeriodS : 0;
}
