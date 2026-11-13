using SupernovaLab.Core.Signals;

namespace SupernovaLab.App.Tour;

/// <summary>
/// One stop of the guided tour (Project 2, Task 12). <see cref="ObserverTimeS"/> is an OBSERVER time (source time × (1 + z),
/// ruling R-P2-6), the timeline's own clock; the first stop is at 1 ms, the timeline's start. A <see cref="Cutaway"/> stop
/// shows the star's interior (between bounce and breakout) and the camera orbits it at <see cref="OrbitDegPerS"/>.
/// <see cref="Highlight"/> names the instrument panel (and strip tile) to brighten while the stop is shown, or null.
/// </summary>
public sealed record TourStop(double ObserverTimeS, string Caption, bool Cutaway, double DwellS = 4,
    double OrbitDegPerS = 0, Messenger? Highlight = null);
