using System.Diagnostics;
using System.Windows.Media;
using OpenTK.Wpf;

namespace SupernovaLab.Rendering;

/// <summary>
/// Wall-clock seconds spent in some repeated piece of work (the bench's attribution of the per-frame cost, Project 3 Task 4b):
/// <see cref="Add"/> one sample per call; <see cref="Reset"/> starts over. Used on the UI thread only.
/// </summary>
public sealed class CpuTimeStats
{
    public int Count { get; private set; }
    public double TotalS { get; private set; }
    public double MaxS { get; private set; }
    public double MeanS => Count > 0 ? TotalS / Count : double.NaN;

    public void Add(double seconds)
    {
        if (!(seconds >= 0) || !double.IsFinite(seconds)) return;
        Count++;
        TotalS += seconds;
        MaxS = Math.Max(MaxS, seconds);
    }

    public void Reset()
    {
        Count = 0;
        TotalS = MaxS = 0;
    }

    /// <summary>Seconds since a <see cref="Stopwatch.GetTimestamp"/>.</summary>
    public static double Since(long timestamp) => (Stopwatch.GetTimestamp() - timestamp) / (double)Stopwatch.Frequency;
}

/// <summary>
/// The GL control with its whole WPF render timed (Project 3 Task 4b's bench): <see cref="GLWpfControl"/>'s OnRender locks the
/// shared D3D surface, makes the context current, raises Render (the app's drawing), unlocks and hands the surface to WPF. The
/// difference between <see cref="RenderTime"/> and the app's own drawing (<see cref="GlHost.DrawTime"/>) is the control's interop
/// cost on the UI thread. Behaves exactly as GLWpfControl otherwise.
/// </summary>
public class TimedGLWpfControl : GLWpfControl
{
    /// <summary>The UI-thread time of each OnRender (the control's whole frame).</summary>
    public CpuTimeStats RenderTime { get; } = new();

    protected override void OnRender(DrawingContext drawingContext)
    {
        long t0 = Stopwatch.GetTimestamp();
        base.OnRender(drawingContext);
        RenderTime.Add(CpuTimeStats.Since(t0));
    }
}
