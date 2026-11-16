using System.Diagnostics;
using System.Windows.Threading;
using SupernovaLab.Rendering;

namespace SupernovaLab.App.Services;

/// <summary>
/// The bench's measurement of the UI thread (Project 3 Task 4b, attributing the fixed per-frame cost): the wall time of every
/// dispatcher operation from <see cref="Start"/> to <see cref="Stop"/> (outermost operations only), split into the render pass
/// (<see cref="DispatcherPriority.Render"/>: layout, the CompositionTarget.Rendering handlers, every element's OnRender including
/// the GL controls, and the hand-off to the composition thread) and everything else (timers, bindings, input), and the process's
/// CPU time over the same span (all threads, including WPF's composition thread). Use on the dispatcher's own thread.
/// </summary>
public sealed class UiThreadMeter
{
    private readonly Dispatcher _dispatcher;
    private readonly Stopwatch _watch = new();
    private int _depth;
    private long _opStart;
    private bool _opIsRender;
    private TimeSpan _cpu0;

    public UiThreadMeter(Dispatcher dispatcher) => _dispatcher = dispatcher;

    public CpuTimeStats Render { get; } = new();
    public CpuTimeStats Other { get; } = new();
    public double ElapsedS => _watch.Elapsed.TotalSeconds;
    public double ProcessCpuS { get; private set; }

    public void Start()
    {
        Render.Reset();
        Other.Reset();
        _depth = 0;
        _cpu0 = Process.GetCurrentProcess().TotalProcessorTime;
        _dispatcher.Hooks.OperationStarted += OnStarted;
        _dispatcher.Hooks.OperationCompleted += OnEnded;
        _dispatcher.Hooks.OperationAborted += OnEnded;
        _watch.Restart();
    }

    public void Stop()
    {
        _watch.Stop();
        _dispatcher.Hooks.OperationStarted -= OnStarted;
        _dispatcher.Hooks.OperationCompleted -= OnEnded;
        _dispatcher.Hooks.OperationAborted -= OnEnded;
        ProcessCpuS = (Process.GetCurrentProcess().TotalProcessorTime - _cpu0).TotalSeconds;
    }

    private void OnStarted(object? sender, DispatcherHookEventArgs e)
    {
        if (_depth++ > 0) return;
        _opStart = Stopwatch.GetTimestamp();
        _opIsRender = e.Operation?.Priority == DispatcherPriority.Render;
    }

    private void OnEnded(object? sender, DispatcherHookEventArgs e)
    {
        if (_depth == 0 || --_depth > 0) return;
        (_opIsRender ? Render : Other).Add(CpuTimeStats.Since(_opStart));
    }
}
