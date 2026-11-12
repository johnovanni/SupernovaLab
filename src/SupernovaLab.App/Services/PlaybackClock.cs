using System.Diagnostics;
using System.Windows.Media;
using SupernovaLab.App.ViewModels;

namespace SupernovaLab.App.Services;

/// <summary>Drives <see cref="TimelineViewModel.Tick"/> once per rendered frame with the wall-clock time since the last one.</summary>
public sealed class PlaybackClock
{
    private readonly TimelineViewModel _timeline;
    private readonly Stopwatch _clock = new();
    private TimeSpan _last;

    public PlaybackClock(TimelineViewModel timeline) => _timeline = timeline;

    public void Start()
    {
        _clock.Start();
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        // A long gap (the window was dragged, or the machine slept) should not jump the timeline.
        var elapsed = now - _last;
        _last = now;
        if (elapsed > TimeSpan.FromSeconds(0.25)) elapsed = TimeSpan.FromSeconds(0.25);
        _timeline.Tick(elapsed);
    }
}
