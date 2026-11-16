using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Media;
using SupernovaLab.App.ViewModels;

namespace SupernovaLab.App.Services;

/// <summary>
/// Drives <see cref="TimelineViewModel.Tick"/> once per rendered frame with the wall-clock time since the last one. It
/// listens to frames only while the timeline plays, so a paused app does not keep WPF rendering continuously.
/// </summary>
public sealed class PlaybackClock
{
    private readonly TimelineViewModel _timeline;
    private readonly Stopwatch _clock = new();
    private TimeSpan _last;
    private bool _subscribed;

    public PlaybackClock(TimelineViewModel timeline) => _timeline = timeline;

    public void Start()
    {
        _timeline.PropertyChanged += OnTimelineChanged;
        Follow();
    }

    private void OnTimelineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TimelineViewModel.IsPlaying)) Follow();
    }

    private void Follow()
    {
        if (_timeline.IsPlaying == _subscribed) return;
        _subscribed = _timeline.IsPlaying;
        if (_subscribed)
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
        // A long gap (the window was dragged, or the machine slept) should not jump the timeline.
        var elapsed = now - _last;
        _last = now;
        if (elapsed > TimeSpan.FromSeconds(0.25)) elapsed = TimeSpan.FromSeconds(0.25);
        long t0 = BenchTimings.Begin();   // Task 14b: the bench's time-step timing (off in a normal run)
        _timeline.Tick(elapsed);
        BenchTimings.End(BenchTimings.TimeStep, t0);
    }
}
