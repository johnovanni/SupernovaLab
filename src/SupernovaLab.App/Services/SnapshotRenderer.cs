using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Presets;

namespace SupernovaLab.App.Services;

/// <summary>A window size in device-independent pixels.</summary>
public readonly record struct SnapshotSize(int Width, int Height)
{
    public override string ToString() => $"{Width}x{Height}";
}

/// <summary>The windows the --snapshot diagnostic can render.</summary>
public enum SnapshotWindow { Main, Instruments, Details, PresetInfo }

/// <summary>
/// Ruling R19's diagnostic: <c>--snapshot &lt;out-dir&gt; [--preset &lt;id&gt;] [--time &lt;s&gt;] [--size &lt;WxH&gt;]
/// [--window main|instruments|details|presetinfo]</c>. To batch captures into one launch, <c>--size</c> and
/// <c>--window</c> may be repeated or given comma-separated lists. Sizes apply to the main window (its outer size, as a
/// user would set it); the other windows render at their own default size.
/// </summary>
public sealed record SnapshotRequest(string OutDirectory, string? PresetId, double? ObserverTimeS,
    IReadOnlyList<SnapshotSize> Sizes, IReadOnlyList<SnapshotWindow> Windows)
{
    /// <summary>The request in these command-line arguments; null without <c>--snapshot</c>. Throws <see cref="ArgumentException"/> for bad arguments.</summary>
    public static SnapshotRequest? Parse(IReadOnlyList<string> args)
    {
        int at = args.ToList().IndexOf("--snapshot");
        if (at < 0) return null;
        if (at + 1 >= args.Count || args[at + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException("--snapshot needs an output directory.");

        string outDir = args[at + 1];
        string? preset = null;
        double? time = null;
        var sizes = new List<SnapshotSize>();
        var windows = new List<SnapshotWindow>();
        for (int i = 0; i < args.Count; i++)
        {
            if (i == at) { i++; continue; }
            string option = args[i];
            if (i + 1 >= args.Count) throw new ArgumentException($"{option} needs a value.");
            string value = args[++i];
            switch (option)
            {
                case "--preset":
                    preset = value;
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
                default:
                    throw new ArgumentException($"Unknown option '{option}'.");
            }
        }
        if (sizes.Count == 0) sizes.Add(new SnapshotSize(1366, 768));
        if (windows.Count == 0) windows.Add(SnapshotWindow.Main);
        return new SnapshotRequest(outDir, preset, time, sizes, windows);
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
        _ => throw new ArgumentException($"--window must be main, instruments, details or presetinfo, not '{text}'."),
    };
}

/// <summary>
/// Renders windows to PNG with <see cref="RenderTargetBitmap"/> (ruling R19: never a screen capture). The windows are
/// placed off-screen, laid out, given one rendered frame, drawn into a bitmap at 96 dpi, written to the output directory
/// and closed; then the app exits. A log of what was written goes to snapshot.log in the same directory.
/// </summary>
public static class SnapshotRenderer
{
    public static async Task RunAsync(SnapshotRequest request, Window main, MainViewModel vm, WindowService windows)
    {
        Directory.CreateDirectory(request.OutDirectory);
        var log = new List<string> { $"SupernovaLab snapshot {DateTime.Now:O}" };
        try
        {
            if (request.PresetId is { } id) vm.Lab.SelectedPreset = PresetCatalog.Get(id);
            if (request.ObserverTimeS is double t) vm.Timeline.ObserverTimeS = t;
            string suffix = request.PresetId is { } p ? $"-{p}" : "";

            PlaceOffScreen(main);
            main.Show();
            foreach (var kind in request.Windows)
            {
                if (kind == SnapshotWindow.Main)
                {
                    foreach (var size in request.Sizes)
                    {
                        main.Width = size.Width;
                        main.Height = size.Height;
                        await NextFrame(main);
                        log.Add(Save(main, Path.Combine(request.OutDirectory, $"main{suffix}-{size}.png")));
                    }
                    continue;
                }

                Window? window = Open(kind, windows);
                if (window is null) { log.Add($"{kind}: no window opened"); continue; }
                PlaceOffScreen(window);
                await NextFrame(window);
                log.Add(Save(window, Path.Combine(request.OutDirectory, $"{kind.ToString().ToLowerInvariant()}{suffix}.png")));
            }
        }
        catch (Exception e)
        {
            log.Add($"ERROR: {e}");
        }
        finally
        {
            File.WriteAllLines(Path.Combine(request.OutDirectory, "snapshot.log"), log);
            Application.Current.Shutdown();
        }
    }

    private static Window? Open(SnapshotWindow kind, WindowService windows)
    {
        switch (kind)
        {
            case SnapshotWindow.Instruments: windows.ShowInstruments(); return windows.Instruments;
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
