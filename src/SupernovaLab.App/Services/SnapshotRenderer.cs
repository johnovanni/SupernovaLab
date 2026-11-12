using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SupernovaLab.App.ViewModels;
using SupernovaLab.App.Windows;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Core.Presets;

namespace SupernovaLab.App.Services;

/// <summary>A window size in device-independent pixels.</summary>
public readonly record struct SnapshotSize(int Width, int Height)
{
    public override string ToString() => $"{Width}x{Height}";
}

/// <summary>
/// The windows the --snapshot diagnostic can render. <see cref="InstrumentTabs"/> is the Instruments grid again with every
/// panel on each of its other sub-tabs in turn (instruments-tab1.png, -tab2, -tab3). <see cref="MainAdvanced"/> is the main
/// window with the lab's Advanced section expanded, at the top of the lab panel and scrolled to its end.
/// </summary>
public enum SnapshotWindow { Main, Instruments, Details, PresetInfo, InstrumentTabs, MainAdvanced }

/// <summary>
/// Ruling R19's diagnostic: <c>--snapshot &lt;out-dir&gt; [--preset &lt;id&gt;] [--time &lt;s&gt;] [--size &lt;WxH&gt;]
/// [--window main|instruments|details|presetinfo]</c>. To batch captures into one launch, <c>--preset</c>,
/// <c>--size</c> and <c>--window</c> may be repeated or given comma-separated lists: every requested window is captured
/// for each preset in turn; a preset may carry its own time as "id@seconds". Sizes apply to the main window (its outer size, as a user would set it); the other windows
/// render at their own default size, and again scrolled to the end when they scroll. <c>--bench &lt;seconds&gt;</c> then
/// plays the whole timeline once over that many seconds with the Instruments window open, counts rendered frames and
/// writes the frame rate to bench.log (the redraw-rate check of spec §8, measured in-app: no screen capture).
/// </summary>
public sealed record SnapshotRequest(string OutDirectory, IReadOnlyList<string> PresetIds, double? ObserverTimeS,
    IReadOnlyList<SnapshotSize> Sizes, IReadOnlyList<SnapshotWindow> Windows, double? BenchSeconds = null)
{
    /// <summary>The first preset, or null for the lab's defaults.</summary>
    public string? PresetId => PresetIds.Count > 0 ? PresetIds[0] : null;

    /// <summary>The request in these command-line arguments; null without <c>--snapshot</c>. Throws <see cref="ArgumentException"/> for bad arguments.</summary>
    public static SnapshotRequest? Parse(IReadOnlyList<string> args)
    {
        int at = args.ToList().IndexOf("--snapshot");
        if (at < 0) return null;
        if (at + 1 >= args.Count || args[at + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException("--snapshot needs an output directory.");

        string outDir = args[at + 1];
        var presets = new List<string>();
        double? time = null;
        var sizes = new List<SnapshotSize>();
        var windows = new List<SnapshotWindow>();
        double? bench = null;
        for (int i = 0; i < args.Count; i++)
        {
            if (i == at) { i++; continue; }
            string option = args[i];
            if (i + 1 >= args.Count) throw new ArgumentException($"{option} needs a value.");
            string value = args[++i];
            switch (option)
            {
                case "--preset":
                    foreach (string entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (entry.Contains('@') && SplitPreset(entry).TimeS is null)
                            throw new ArgumentException($"--preset {entry}: the time after '@' must be a positive number of seconds.");
                        presets.Add(entry);
                    }
                    break;
                case "--time":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double t) || !(t > 0))
                        throw new ArgumentException($"--time needs a positive number of seconds, not '{value}'.");
                    time = t;
                    break;
                case "--size":
                    sizes.AddRange(value.Split(',').Select(ParseSize));
                    break;
                case "--window":
                    windows.AddRange(value.Split(',').Select(ParseWindow));
                    break;
                case "--bench":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double b) || !(b > 0) || b > 600)
                        throw new ArgumentException($"--bench needs a number of seconds between 0 and 600, not '{value}'.");
                    bench = b;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{option}'.");
            }
        }
        if (sizes.Count == 0) sizes.Add(new SnapshotSize(1366, 768));
        if (windows.Count == 0) windows.Add(SnapshotWindow.Main);
        return new SnapshotRequest(outDir, presets, time, sizes, windows, bench);
    }

    /// <summary>A preset entry, "id" or "id@seconds" (its own time, overriding --time for that preset).</summary>
    public static (string? Id, double? TimeS) SplitPreset(string? entry)
    {
        if (entry is null) return (null, null);
        int at = entry.IndexOf('@');
        if (at < 0) return (entry, null);
        return double.TryParse(entry[(at + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out double t) && t > 0
            ? (entry[..at], t)
            : (entry[..at], null);
    }

    /// <summary>
    /// The file-name suffix for a preset entry: "" for the lab's defaults, "-sn1987a", or with its own time
    /// "-sn1987a-t86400" (so one preset at several times gives several files).
    /// </summary>
    public static string FileSuffix(string? entry)
    {
        var (id, own) = SplitPreset(entry);
        if (id is null) return "";
        return own is double t ? $"-{id}-t{t.ToString("0.###", CultureInfo.InvariantCulture)}" : $"-{id}";
    }

    private static SnapshotSize ParseSize(string text)
    {
        var parts = text.Split('x', 'X');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int w) &&
            int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int h) && w > 0 && h > 0)
            return new SnapshotSize(w, h);
        throw new ArgumentException($"--size needs WxH, e.g. 1366x768, not '{text}'.");
    }

    private static SnapshotWindow ParseWindow(string text) => text.ToLowerInvariant() switch
    {
        "main" => SnapshotWindow.Main,
        "instruments" => SnapshotWindow.Instruments,
        "details" => SnapshotWindow.Details,
        "presetinfo" => SnapshotWindow.PresetInfo,
        "instrument-tabs" => SnapshotWindow.InstrumentTabs,
        "main-advanced" => SnapshotWindow.MainAdvanced,
        _ => throw new ArgumentException(
            $"--window must be main, main-advanced, instruments, instrument-tabs, details or presetinfo, not '{text}'."),
    };
}

/// <summary>
/// Renders windows to PNG with <see cref="RenderTargetBitmap"/> (ruling R19: never a screen capture). The windows are
/// placed off-screen, laid out, given one rendered frame, drawn into a bitmap at 96 dpi, written to the output directory
/// and closed; then the app exits, with exit code 1 if anything failed. A log of what was written (and any error) goes
/// to snapshot.log in the same directory.
/// </summary>
public static class SnapshotRenderer
{
    public static async Task RunAsync(SnapshotRequest request, Window main, MainViewModel vm, WindowService windows)
    {
        Directory.CreateDirectory(request.OutDirectory);
        var log = new List<string> { $"SupernovaLab snapshot {DateTime.Now:O}" };
        int exitCode = 0;
        try
        {
            PlaceOffScreen(main);
            main.Show();
            var presets = request.PresetIds.Count > 0 ? request.PresetIds.Cast<string?>().ToList() : new List<string?> { null };
            foreach (string? entry in presets)
            {
                var (id, own) = SnapshotRequest.SplitPreset(entry);
                if (id is not null) vm.Lab.SelectedPreset = PresetCatalog.Get(id);
                if ((own ?? request.ObserverTimeS) is double t) vm.Timeline.ObserverTimeS = t;
                string suffix = SnapshotRequest.FileSuffix(entry);
                await Capture(request, main, windows, suffix, log);
            }
            await RunBench(request, main, vm, windows, log);
        }
        catch (Exception e)
        {
            log.Add($"ERROR: {e}");
            exitCode = 1;
        }
        finally
        {
            File.WriteAllLines(Path.Combine(request.OutDirectory, "snapshot.log"), log);
            Application.Current.Shutdown(exitCode);
        }
    }

    /// <summary>The requested windows for the current preset: the main window at each size, the others top and end.</summary>
    private static async Task Capture(SnapshotRequest request, Window main, WindowService windows, string suffix, List<string> log)
    {
        foreach (var kind in request.Windows)
        {
            if (kind == SnapshotWindow.Main)
            {
                foreach (var size in request.Sizes)
                {
                    main.Width = size.Width;
                    main.Height = size.Height;
                    await NextFrame(main);
                    await SaveMain(main, Path.Combine(request.OutDirectory, $"main{suffix}-{size}.png"), log);
                }
                continue;
            }
            if (kind == SnapshotWindow.MainAdvanced)
            {
                await CaptureAdvanced(request, main, suffix, log);
                continue;
            }

            Window? window = Open(kind, windows);
            if (window is null) { log.Add($"{kind}: no window opened"); continue; }
            PlaceOffScreen(window);
            if (window is InstrumentsWindow instruments)
            {
                if (kind == SnapshotWindow.InstrumentTabs) await CaptureTabs(instruments, request, suffix, log);
                else
                {
                    await RedrawPanels(instruments);
                    log.Add(Save(instruments, Path.Combine(request.OutDirectory, $"instruments{suffix}.png")));
                }
                continue;
            }
            string name = $"{kind.ToString().ToLowerInvariant()}{suffix}";
            var scroller = MainScrollViewer(window);
            scroller?.ScrollToHome();
            await NextFrame(window);
            log.Add(Save(window, Path.Combine(request.OutDirectory, $"{name}.png")));
            if (scroller is { ScrollableHeight: > 0 })
            {
                scroller.ScrollToEnd();
                await NextFrame(window);
                log.Add(Save(window, Path.Combine(request.OutDirectory, $"{name}-end.png")));
                scroller.ScrollToHome();
            }
        }
    }

    /// <summary>The main window with the lab's Advanced expander open: the lab panel at its top and scrolled to its end.</summary>
    private static async Task CaptureAdvanced(SnapshotRequest request, Window main, string suffix, List<string> log)
    {
        var size = request.Sizes[0];
        main.Width = size.Width;
        main.Height = size.Height;
        var expander = Descendants<Expander>(main).FirstOrDefault(e => e.Header is string h && h.StartsWith("Advanced", StringComparison.Ordinal));
        if (expander is null)
        {
            log.Add("main-advanced: no Advanced expander found");
            return;
        }
        bool was = expander.IsExpanded;
        expander.IsExpanded = true;
        await NextFrame(main);
        await SaveMain(main, Path.Combine(request.OutDirectory, $"main-advanced{suffix}-{size}.png"), log);
        var scroller = Ancestor<ScrollViewer>(expander);
        if (scroller is { ScrollableHeight: > 0 })
        {
            scroller.ScrollToEnd();
            await NextFrame(main);
            log.Add(Save(main, Path.Combine(request.OutDirectory, $"main-advanced{suffix}-{size}-end.png")));
            scroller.ScrollToHome();
        }
        else log.Add("main-advanced: the lab panel does not scroll with Advanced open");
        expander.IsExpanded = was;
        await NextFrame(main);
    }

    /// <summary>
    /// Saves the main window with its 3D view: the GL frame is read back with GL.ReadPixels and shown in place of the GL
    /// control for the RenderTargetBitmap (which cannot see GL content), then the live control returns.
    /// </summary>
    private static async Task SaveMain(Window main, string path, List<string> log)
    {
        if (main is MainWindow mw)
        {
            log.Add(await mw.ShowSceneFrameForSnapshotAsync());
            await NextFrame(main);
        }
        log.Add(Save(main, path));
        if (main is MainWindow shown) shown.HideSceneFrameForSnapshot();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) yield return t;
            foreach (var d in Descendants<T>(child)) yield return d;
        }
    }

    private static T? Ancestor<T>(DependencyObject node) where T : DependencyObject
    {
        for (var n = VisualTreeHelper.GetParent(node); n is not null; n = VisualTreeHelper.GetParent(n))
            if (n is T t) return t;
        return null;
    }

    /// <summary>Draws every panel now and waits for the plots and their cursors to render.</summary>
    private static async Task RedrawPanels(InstrumentsWindow window)
    {
        window.UpdateLayout();
        foreach (var view in window.PanelViews) view.Redraw();
        await NextFrame(window);
        await NextFrame(window);
    }

    /// <summary>The grid with every panel on its k-th sub-tab (k = 1, 2, 3; a panel with fewer stays on its last).</summary>
    private static async Task CaptureTabs(InstrumentsWindow window, SnapshotRequest request, string suffix, List<string> log)
    {
        if (window.DataContext is not MainViewModel vm) return;
        var original = vm.Panels.Select(p => p.SelectedSubTab).ToList();
        int most = vm.Panels.Max(p => p.SubTabs.Count);
        for (int k = 1; k < most; k++)
        {
            foreach (var p in vm.Panels) p.ShowSubTabForSnapshot(p.SubTabs[Math.Min(k, p.SubTabs.Count - 1)]);
            await RedrawPanels(window);
            log.Add(Save(window, Path.Combine(request.OutDirectory, $"instruments-tab{k}{suffix}.png")));
        }
        for (int i = 0; i < vm.Panels.Count; i++) vm.Panels[i].ShowSubTabForSnapshot(original[i]);

        // One panel enlarged within the window (spec §5.0: double-click maximizes; Esc restores).
        window.Maximize(vm.Panels.First(p => p.Messenger == Core.Signals.Messenger.VisibleLight));
        await RedrawPanels(window);
        log.Add(Save(window, Path.Combine(request.OutDirectory, $"instruments-maximized{suffix}.png")));
        window.RestoreGrid();
    }

    /// <summary>
    /// Plays the whole timeline once over the requested seconds with the main and Instruments windows open, and counts
    /// CompositionTarget.Rendering frames (the redraw-rate check; spec §8, target ≥ 30 fps). Writes bench.log.
    /// </summary>
    private static async Task RunBench(SnapshotRequest request, Window main, MainViewModel vm, WindowService windows, List<string> log)
    {
        if (request.BenchSeconds is not double seconds) return;
        windows.ShowInstruments();
        if (windows.Instruments is { } w) PlaceOffScreen(w);
        main.Width = request.Sizes[0].Width;
        main.Height = request.Sizes[0].Height;
        await NextFrame(main);

        // The worst case: the two views that redraw as the time moves (the schematic spectrum and the pulse train) are
        // shown, beside the other four panels' curves and the strip.
        vm.Panels.OfType<VisiblePanelViewModel>().Single().ShowSubTabForSnapshot(VisiblePanelViewModel.SpectrumTab);
        vm.Panels.OfType<RadioPanelViewModel>().Single().ShowSubTabForSnapshot(RadioPanelViewModel.PulsarTab);
        if (windows.Instruments is InstrumentsWindow iw) await RedrawPanels(iw);

        double decades = Math.Log10(TimeAxis.MaxS / TimeAxis.MinS);
        vm.Timeline.DecadesPerSecond = decades / seconds;
        vm.Timeline.ObserverTimeS = TimeAxis.MinS;
        new PlaybackClock(vm.Timeline).Start();
        var intervals = new List<double>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        double last = 0;
        int frames = 0;
        void OnRendering(object? s, EventArgs e)
        {
            double now = watch.Elapsed.TotalSeconds;
            if (frames > 0) intervals.Add(now - last);
            last = now;
            frames++;
        }
        var scene = (main as MainWindow)?.Scene;
        long sceneFrames0 = scene?.FramesDrawn ?? 0;
        CompositionTarget.Rendering += OnRendering;
        vm.Timeline.IsPlaying = true;
        while (vm.Timeline.IsPlaying && watch.Elapsed.TotalSeconds < seconds * 1.5 + 5)
            await Task.Delay(100);
        CompositionTarget.Rendering -= OnRendering;
        double elapsed = watch.Elapsed.TotalSeconds;
        long sceneFrames = (scene?.FramesDrawn ?? 0) - sceneFrames0;
        intervals.Sort();
        double p95 = intervals.Count > 0 ? intervals[(int)(0.95 * (intervals.Count - 1))] * 1000 : double.NaN;
        double worst = intervals.Count > 0 ? intervals[^1] * 1000 : double.NaN;
        var inv = CultureInfo.InvariantCulture;
        string preset = vm.Lab.SelectedPreset?.Id ?? "default";
        var lines = new[]
        {
            $"SupernovaLab bench {DateTime.Now:O}",
            $"preset {preset}; main window {main.ActualWidth:0}x{main.ActualHeight:0} and the Instruments window open (off-screen), " +
            "the visible panel on its schematic spectrum and the radio panel on its pulses; " +
            $"playing the whole timeline ({decades.ToString("0.0", inv)} decades) at {vm.Timeline.DecadesPerSecond.ToString("0.###", inv)} decades/s",
            $"frames {frames} in {elapsed.ToString("0.00", inv)} s: {(frames / elapsed).ToString("0.0", inv)} fps",
            $"frame interval p95 {p95.ToString("0.0", inv)} ms, worst {worst.ToString("0.0", inv)} ms",
            scene is null ? "3D view: none" : scene.IsFailed ? $"3D view: failed ({scene.FailureMessage})" :
                $"3D view frames drawn {sceneFrames} in {elapsed.ToString("0.00", inv)} s: " +
                $"{(sceneFrames / elapsed).ToString("0.0", inv)} fps (GL draws counted by the scene controller)",
            $"reached the end of the timeline: {(vm.Timeline.ObserverTimeS >= TimeAxis.MaxS ? "yes" : "no")}",
        };
        File.WriteAllLines(Path.Combine(request.OutDirectory, "bench.log"), lines);
        log.AddRange(lines);
    }

    /// <summary>The window's tallest scroll viewer (its main content), or null.</summary>
    private static ScrollViewer? MainScrollViewer(DependencyObject root)
    {
        ScrollViewer? best = null;
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is ScrollViewer sv && (best is null || sv.ExtentHeight > best.ExtentHeight)) best = sv;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) stack.Push(VisualTreeHelper.GetChild(node, i));
        }
        return best;
    }

    private static Window? Open(SnapshotWindow kind, WindowService windows)
    {
        switch (kind)
        {
            case SnapshotWindow.Instruments or SnapshotWindow.InstrumentTabs: windows.ShowInstruments(); return windows.Instruments;
            case SnapshotWindow.Details: windows.ShowOutcomeDetails(); return windows.OutcomeDetails;
            case SnapshotWindow.PresetInfo: windows.ShowPresetInfo(); return windows.PresetInfo;
            default: return null;
        }
    }

    /// <summary>Moves the window left of the whole virtual screen, so nothing appears on the user's monitors.</summary>
    private static void PlaceOffScreen(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.Left = SystemParameters.VirtualScreenLeft - Math.Max(window.Width, 2000) - 200;
        window.Top = SystemParameters.VirtualScreenTop;
    }

    /// <summary>Waits for layout and one rendered frame.</summary>
    private static async Task NextFrame(Window window)
    {
        window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        var rendered = new TaskCompletionSource();
        void OnRendering(object? s, EventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
            rendered.TrySetResult();
        }
        CompositionTarget.Rendering += OnRendering;
        await rendered.Task;
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
    }

    /// <summary>Renders the window's client area (its content over its background) to a PNG at 96 dpi.</summary>
    private static string Save(Window window, string path)
    {
        // The window template's root fills the client area, so margins around the content are included.
        if ((VisualTreeHelper.GetChildrenCount(window) > 0 ? VisualTreeHelper.GetChild(window, 0) : window.Content)
            is not FrameworkElement root) return $"{path}: the window has no content";
        int w = (int)Math.Ceiling(root.ActualWidth), h = (int)Math.Ceiling(root.ActualHeight);
        if (w <= 0 || h <= 0) return $"{path}: empty layout ({w}x{h})";

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var rect = new Rect(0, 0, w, h);
            dc.DrawRectangle(window.Background ?? Brushes.Black, null, rect);
            dc.DrawRectangle(new VisualBrush(root) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                null, rect);
        }
        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
        return $"{path}: {w}x{h} (window {window.ActualWidth:0}x{window.ActualHeight:0})";
    }
}
