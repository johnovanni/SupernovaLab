using System.Diagnostics;
using System.Windows.Media;
using SupernovaLab.App.Tour;

namespace SupernovaLab.App.Services;

/// <summary>
/// Drives <see cref="TourDirector.Tick"/> once per rendered frame with the wall-clock time since the last one, only while the
/// tour is travelling or dwelling (a paused or finished tour does not keep WPF rendering continuously). Like
/// <see cref="PlaybackClock"/>, a long gap is capped so a dragged window does not jump the tour.
/// </summary>
public sealed class TourClock
{
    private readonly TourDirector _director;
    private readonly Stopwatch _clock = new();
    private TimeSpan _last;
    private bool _subscribed;

    public TourClock(TourDirector director) => _director = director;

    public void Start()
    {
        _director.Changed += (_, _) => Follow();
        Follow();
    }

    private void Follow()
    {
        bool running = _director.State is TourState.Playing or TourState.Dwelling;
        if (running == _subscribed) return;
        _subscribed = running;
        if (running)
        {
            _clock.Restart();
            _last = TimeSpan.Zero;
            CompositionTarget.Rendering += OnRendering;
        }
        else
        {
            CompositionTarget.Rendering -= OnRendering;
            _clock.Stop();
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var elapsed = now - _last;
        _last = now;
        if (elapsed > TimeSpan.FromSeconds(0.25)) elapsed = TimeSpan.FromSeconds(0.25);
        _director.Tick(elapsed);
    }
}
