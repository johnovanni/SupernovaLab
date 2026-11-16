using System.Diagnostics;

namespace SupernovaLab.App.Services;

/// <summary>
/// The one wall clock the pulsar beams turn by (plan ruling P3-5). It is made once, at start-up, and handed to both scene controllers
/// (the main view's and the Close-up's), so a beam drawn in each window has the same rotation phase at the same moment.
/// <para>Plan Task 6 (flash safety, spec C2): the phase is integrated, phase += 2π · Δ(wall seconds) / drawn period, rather than taken
/// as 2π · wall seconds / drawn period. With a fixed period the two are the same; but a period that changes (a magnetar's, of
/// seconds, drawn at its real speed while the timeline plays or is scrubbed) would make the product jump by many turns ten minutes
/// into a session, flashing the beams far faster than once a second. Integrated, a new period changes only the rate, never the turns
/// already made, so the drawn spin never turns faster than once per <c>Magnetosphere.MinDrawnPeriodS</c>. Both views read the one
/// shared phase, so they still agree.</para>
/// </summary>
public sealed class BeamClock
{
    private readonly Func<double> _seconds;
    private double _lastS, _phaseRad;

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

    /// <summary>
    /// The rotation phase (radians, not wrapped) of something drawn turning once per <paramref name="drawnPeriodS"/>: the phase so far
    /// plus the wall time since the last reading at this period. 0 for a bad period (and the reading is not counted).
    /// </summary>
    public double PhaseRad(double drawnPeriodS)
    {
        if (!(drawnPeriodS > 0) || !double.IsFinite(drawnPeriodS)) return 0;
        double now = ElapsedS;
        _phaseRad += 2 * Math.PI * (now - _lastS) / drawnPeriodS;
        _lastS = now;
        return _phaseRad;
    }
}
