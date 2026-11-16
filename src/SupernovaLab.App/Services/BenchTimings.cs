using System.Diagnostics;
using System.Globalization;
using SupernovaLab.Rendering;

namespace SupernovaLab.App.Services;

/// <summary>
/// Task 14b (ruling R-P3-36's regression hunt): the bench's per-subsystem timings of the UI thread's work in each composition frame.
/// Off unless <c>--bench</c> turns it on for a run: then <see cref="Begin"/> reads the clock and <see cref="End"/> adds the span; off,
/// <see cref="Begin"/> returns 0 and <see cref="End"/> does nothing, so a normal run pays one static read per measured call.
/// UI thread only (the time step, the controllers and the pulse trace all run there).
/// </summary>
public static class BenchTimings
{
    /// <summary>Whether the timings are recorded (the bench sets it for its runs only).</summary>
    public static bool Enabled { get; set; }

    /// <summary>The whole timeline time step, from the playback clock's CompositionTarget.Rendering handler (all its listeners).</summary>
    public static CpuTimeStats TimeStep { get; } = new();

    /// <summary>The six instrument panels' time step (cursor, readouts, detection, the spectrum and pulse views).</summary>
    public static CpuTimeStats Panels { get; } = new();

    /// <summary>The main view's frame preparation on a time step (ExplosionScene.At, the overlay's texts and labels).</summary>
    public static CpuTimeStats MainScene { get; } = new();

    /// <summary>The Close-up view model's time step (CloseUpScene.At, readout rows, notes, pulses), with its controller inside it.</summary>
    public static CpuTimeStats CloseUpViewModel { get; } = new();

    /// <summary>The Close-up controller's part of a time step (whether to redraw, labels), inside <see cref="CloseUpViewModel"/>.</summary>
    public static CpuTimeStats CloseUpController { get; } = new();

    /// <summary>The Close-up's pulse trace, once per composition frame while pulses are shown.</summary>
    public static CpuTimeStats PulseTrace { get; } = new();

    /// <summary>A timestamp to pass to <see cref="End"/>, or 0 when the timings are off.</summary>
    public static long Begin() => Enabled ? Stopwatch.GetTimestamp() : 0;

    /// <summary>Adds the time since <paramref name="timestamp"/> to <paramref name="stats"/>; nothing for a 0 timestamp (timings off).</summary>
    public static void End(CpuTimeStats stats, long timestamp)
    {
        if (timestamp != 0) stats.Add(CpuTimeStats.Since(timestamp));
    }

    public static void Reset()
    {
        foreach (var s in new[] { TimeStep, Panels, MainScene, CloseUpViewModel, CloseUpController, PulseTrace }) s.Reset();
    }

    /// <summary>
    /// The bench.log line: each subsystem's UI-thread time per composition frame (ms), and the render pass's remainder once the time
    /// step, the pulse trace and the two GL controls' OnRender are taken out of it: WPF's layout, the other elements' rendering and the
    /// hand-off to the composition thread. All arguments are totals over the run, in seconds.
    /// </summary>
    public static string Line(int frames, double renderPassS, double timeStepS, double panelsS, double mainSceneS, double closeUpVmS,
        double closeUpControllerS, double pulseTraceS, double mainGlS, double closeUpGlS)
    {
        var inv = CultureInfo.InvariantCulture;
        string Ms(double totalS) => frames > 0 ? (totalS * 1000 / frames).ToString("0.00", inv) : "n/a";
        double otherListeners = Math.Max(0, timeStepS - panelsS - mainSceneS - closeUpVmS);
        double remainder = Math.Max(0, renderPassS - timeStepS - pulseTraceS - mainGlS - closeUpGlS);
        return $"UI thread per composition frame (ms): time step {Ms(timeStepS)} (panels {Ms(panelsS)}, main scene {Ms(mainSceneS)}, " +
               $"Close-up view model {Ms(closeUpVmS)} incl. its controller {Ms(closeUpControllerS)}, other listeners {Ms(otherListeners)}); " +
               $"pulse trace {Ms(pulseTraceS)}; GL OnRender main {Ms(mainGlS)}, Close-up {Ms(closeUpGlS)}; " +
               $"rest of the render pass (WPF layout, element rendering, composition hand-off) {Ms(remainder)}";
    }
}
