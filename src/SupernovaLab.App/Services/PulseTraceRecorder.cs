namespace SupernovaLab.App.Services;

/// <summary>
/// The pulse trace's record (spec §4.4): the wall-clock times at which a drawn beam crossed the Earth arrow in the last 10 s. It is
/// fed the shared <see cref="BeamClock"/>'s integrated phase (at the drawn period, so the marks are at the drawn rate and in step
/// with the drawn beams) and the spin phases at which a beam centre crosses Earth. A crossing's time is interpolated within the frame,
/// so the marks are as evenly spaced as the beams: at most one a second, since the drawn spin never turns faster than once per 2 s and
/// at most two beams cross (flash safety, spec C2).
/// </summary>
public sealed class PulseTraceRecorder
{
    /// <summary>How much history the trace shows, s.</summary>
    public const double WindowS = 10;

    private const int MaxCrossingsPerUpdate = 1000;

    private readonly List<double> _ticks = new();
    private double _lastWallS, _lastPhase;
    private bool _hasLast;

    /// <summary>The marks inside the window, oldest first (wall seconds).</summary>
    public IReadOnlyList<double> TickTimesS => _ticks;

    /// <summary>Every mark made since the start (or the last reset).</summary>
    public int TotalTicks { get; private set; }

    /// <summary>
    /// One frame: the wall time, the beam phase (radians, not wrapped, never decreasing), the phases in [0, 2π) at which a beam crosses
    /// Earth, and whether pulses are shown (inactive clears the trace).
    /// </summary>
    public void Update(double wallS, double phaseRad, IReadOnlyList<double> crossingsRad, bool active)
    {
        if (!active || !double.IsFinite(phaseRad) || !double.IsFinite(wallS))
        {
            _ticks.Clear();
            _hasLast = false;
            return;
        }
        if (_hasLast && phaseRad >= _lastPhase && wallS > _lastWallS)
        {
            var found = new List<double>();
            foreach (double c in crossingsRad)
            {
                double first = Math.Floor((_lastPhase - c) / (2 * Math.PI)) + 1, last = Math.Floor((phaseRad - c) / (2 * Math.PI));
                for (double k = first; k <= last && found.Count < MaxCrossingsPerUpdate; k++)
                {
                    double at = c + k * 2 * Math.PI;
                    found.Add(_lastWallS + (at - _lastPhase) / (phaseRad - _lastPhase) * (wallS - _lastWallS));
                }
            }
            found.Sort();
            _ticks.AddRange(found);
            TotalTicks += found.Count;
        }
        (_lastWallS, _lastPhase, _hasLast) = (wallS, phaseRad, true);
        _ticks.RemoveAll(t => t < wallS - WindowS);
    }
}
