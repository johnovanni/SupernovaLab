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
using SupernovaLab.Core.Progenitors;
using SupernovaLab.Core.Units;
using SupernovaLab.Rendering;

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
/// [--window main|instruments|details|presetinfo] [--cutaway] [--tour &lt;stop&gt;[,…]] [--quality high|medium|low] [--bench-tour]</c>. <c>--cutaway</c> (a flag) turns on the 3D view's wedge cutaway for
/// every capture (Task 7); a preset entry ending in "+cutaway" turns it on for that entry only (Task 8). To batch captures into one launch, <c>--preset</c>,
/// <c>--size</c> and <c>--window</c> may be repeated or given comma-separated lists: every requested window is captured
/// for each preset in turn; a preset may carry its own time as "id@seconds", and "default@seconds" is the lab as launched (list it before any preset). Sizes apply to the main window (its outer size, as a user would set it); the other windows
/// render at their own default size, and again scrolled to the end when they scroll. <c>--bench &lt;seconds&gt;</c> then
/// plays the whole timeline once over that many seconds with the Instruments window open, counts rendered frames and
/// writes the frame rate to bench.log (the redraw-rate check of spec §8, measured in-app: no screen capture).
/// <c>--sweep &lt;steps&gt;</c> loads every preset in the catalog and steps the timeline through that many log-spaced times
/// from 1 ms to 1,000 yr (the last step is exactly 1,000 yr), with the Instruments window open, logging any exception, any
/// panel's "Model error" and any 3D-view failure to sweep.log (Task 17's run-through).
/// </summary>
public sealed record SnapshotRequest(string OutDirectory, IReadOnlyList<string> PresetIds, double? ObserverTimeS,
    IReadOnlyList<SnapshotSize> Sizes, IReadOnlyList<SnapshotWindow> Windows, double? BenchSeconds = null,
    int? SweepSteps = null, IReadOnlyList<QualityLevel>? QualityLevelList = null, bool Cutaway = false,
    IReadOnlyList<int>? TourStopList = null, bool BenchTour = false, LayoutSettings? Layout = null)
{
    // Layout (Task 15): <c>--layout focus,lab-folded,outcome-folded,key-folded,notes-open,caption-folded</c> sets the panel states for every
    // capture; a preset entry may add them for itself ("sn1987a+tour+labfolded"). Snapshots never read layout.json (the owner's own state).

    // BenchTour: with <c>--bench-tour</c>, the bench plays the guided tour (Task 14) instead of the plain timeline, once for each
    /// requested preset and size; <c>--bench</c>'s seconds are then only a ceiling on the run (the tour sets its own length).

    /// <summary>The guided-tour stops asked for with <c>--tour</c> (comma-separated or repeated; "last" is <see cref="LastStop"/>);
    /// the windows are captured at each stop in turn, for every preset. Empty without <c>--tour</c>.</summary>
    public IReadOnlyList<int> TourStops => TourStopList ?? Array.Empty<int>();

    /// <summary>Stands for the tour's last stop in <see cref="TourStops"/>.</summary>
    public const int LastStop = int.MaxValue;

    /// <summary>The file-name suffix of a tour stop's captures: "-tour3", or "-tourlast".</summary>
    public static string TourSuffix(int stop) => stop == LastStop ? "-tourlast" : $"-tour{stop}";

    /// <summary>The particle quality levels asked for with <c>--quality high|medium|low</c> (comma-separated for several; the
    /// bench then runs each at every size); just the app's default when not given.</summary>
    public IReadOnlyList<QualityLevel> Qualities => QualityLevelList is { Count: > 0 } q ? q : new[] { QualityLevels.Default };

    /// <summary>The most steps per preset that <c>--sweep</c> accepts.</summary>
    public const int MaxSweepSteps = 2000;

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
        int? sweep = null;
        var qualities = new List<QualityLevel>();
        bool cutaway = false;
        var tourStops = new List<int>();
        bool benchTour = false;
        LayoutSettings? layout = null;
        for (int i = 0; i < args.Count; i++)
        {
            if (i == at) { i++; continue; }
            string option = args[i];
            if (option == "--cutaway") { cutaway = true; continue; }   // a flag: the 3D view's wedge cutaway is on
            if (option == "--bench-tour") { benchTour = true; continue; }   // a flag: the bench plays the guided tour
            if (i + 1 >= args.Count) throw new ArgumentException($"{option} needs a value.");
            string value = args[++i];
            switch (option)
            {
                case "--preset":
                    foreach (string entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (entry.Contains('+') && StripFlags(entry).Contains('+'))
                            throw new ArgumentException(
                                $"--preset {entry}: the options after '+' are '{DenseCsmFlag[1..]}' and '{CutawayFlag[1..]}'.");
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
                        throw new ArgumentException($"--bench needs a number of seconds above 0 and at most 600, not '{value}'.");
                    bench = b;
                    break;
                case "--sweep":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int steps) || steps < 2 || steps > MaxSweepSteps)
                        throw new ArgumentException($"--sweep needs a number of steps between 2 and {MaxSweepSteps}, not '{value}'.");
                    sweep = steps;
                    break;
                case "--tour":
                    foreach (string stop in value.Split(',', StringSplitOptions.TrimEntries))
                    {
                        if (string.Equals(stop, "last", StringComparison.OrdinalIgnoreCase)) tourStops.Add(LastStop);
                        else if (int.TryParse(stop, NumberStyles.None, CultureInfo.InvariantCulture, out int k) && k < LastStop) tourStops.Add(k);
                        else throw new ArgumentException($"--tour needs stop numbers from 0 (or 'last'), comma-separated, not '{stop}'.");
                    }
                    break;
                case "--layout":
                    layout = ParseLayout(value, layout ?? new LayoutSettings());
                    break;
                case "--quality":
                    foreach (string q in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        qualities.Add(QualityLevels.Parse(q) ?? throw new ArgumentException($"--quality needs high, medium or low, not '{q}'."));
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{option}'.");
            }
        }
        if (sizes.Count == 0) sizes.Add(new SnapshotSize(1366, 768));
        if (windows.Count == 0) windows.Add(SnapshotWindow.Main);
        return new SnapshotRequest(outDir, presets, time, sizes, windows, bench, sweep, qualities, cutaway, tourStops, benchTour, layout);
    }

    /// <summary>The preset entry that means the lab as launched, with no preset: "default" or "default@seconds" (ruling R-P2-20).</summary>
    public const string DefaultEntry = "default";

    /// <summary>
    /// A preset entry, "id" or "id@seconds" (its own time, overriding --time for that preset). "default" (alone or with
    /// "@seconds") is the lab as launched, with no preset: its id is null. It does not undo an earlier preset, so list it first.
    /// </summary>
    public static (string? Id, double? TimeS) SplitPreset(string? entry)
    {
        if (entry is null) return (null, null);
        entry = StripFlags(entry);
        int at = entry.IndexOf('@');
        string id = at < 0 ? entry : entry[..at];
        double? time = null;
        if (at >= 0 && double.TryParse(entry[(at + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out double t) && t > 0)
            time = t;
        return (string.Equals(id, DefaultEntry, StringComparison.OrdinalIgnoreCase) ? null : id, time);
    }

    /// <summary>
    /// The file-name suffix for a preset entry: "" for the lab's defaults, "-sn1987a", or with its own time
    /// "-sn1987a-t86400" (so one preset at several times gives several files).
    /// </summary>
    public static string FileSuffix(string? entry)
    {
        var (id, own) = SplitPreset(entry);
        if (id is null && own is null) return "";
        id ??= DefaultEntry;
        string suffix = own is double t ? $"-{id}-t{t.ToString("0.###", CultureInfo.InvariantCulture)}" : $"-{id}";
        if (WantsDenseCsm(entry)) suffix += "-densecsm";
        foreach (string name in LayoutNames)
            if (HasFlag(entry, "+" + name)) suffix += "-" + name;
        return WantsCutaway(entry) ? suffix + "-cutaway" : suffix;
    }

    /// <summary>A preset entry's suffix that turns the 3D view's wedge cutaway on for that entry only (Task 8).</summary>
    public const string CutawayFlag = "+cutaway";

    /// <summary>True for an entry with <see cref="CutawayFlag"/> among its options, e.g. "default@946080000+cutaway".</summary>
    public static bool WantsCutaway(string? entry) => HasFlag(entry, CutawayFlag);

    /// <summary>The entry without its known "+option" suffixes (in any order).</summary>
    private static string StripFlags(string entry)
    {
        bool again = true;
        while (again)
        {
            again = false;
            foreach (string flag in KnownFlags)
                if (entry.EndsWith(flag, StringComparison.Ordinal))
                {
                    entry = entry[..^flag.Length];
                    again = true;
                }
        }
        return entry;
    }

    /// <summary>Whether this known flag is among the entry's trailing "+option" suffixes.</summary>
    private static bool HasFlag(string? entry, string flag)
    {
        if (entry is null) return false;
        while (true)
        {
            if (entry.EndsWith(flag, StringComparison.Ordinal)) return true;
            string? other = KnownFlags.FirstOrDefault(f => f != flag && entry.EndsWith(f, StringComparison.Ordinal));
            if (other is null) return false;
            entry = entry[..^other.Length];
        }
    }

    /// <summary>The panel-state names of <c>--layout</c> (hyphens are ignored: "lab-folded" is "labfolded") and the "+name" flags of a preset entry.</summary>
    public static readonly string[] LayoutNames = { "focus", "labfolded", "outcomefolded", "keyfolded", "notesopen", "captionfolded" };

    /// <summary>The settings after turning on the states named in a comma-separated <c>--layout</c> value.</summary>
    public static LayoutSettings ParseLayout(string value, LayoutSettings from)
    {
        var s = from;
        foreach (string raw in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string name = raw.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
            s = With(s, name) ?? throw new ArgumentException($"--layout needs names from {string.Join(", ", LayoutNames)}, not '{raw}'.");
        }
        return s;
    }

    private static LayoutSettings? With(LayoutSettings s, string name) => name switch
    {
        "focus" => s with { FocusView = true },
        "labfolded" => s with { LabPanelFolded = true },
        "outcomefolded" => s with { OutcomeColumnFolded = true },
        "keyfolded" => s with { KeyFolded = true },
        "notesopen" => s with { NotesExpanded = true },
        "captionfolded" => s with { CaptionCollapsed = true },
        _ => null,
    };

    /// <summary>The panel states for one preset entry: the <c>--layout</c> ones plus the entry's own "+focus", "+labfolded", … flags.</summary>
    public LayoutSettings LayoutFor(string? entry)
    {
        var s = Layout ?? new LayoutSettings();
        foreach (string name in LayoutNames)
            if (HasFlag(entry, "+" + name)) s = With(s, name)!;
        return s;
    }

    /// <summary>A preset entry's suffix that turns on the lab's dense-gas input after the preset is applied.</summary>
    public const string DenseCsmFlag = "+densecsm";

    /// <summary>
    /// A preset entry's suffix (final fix wave) that captures the guided tour's <c>--tour</c> stops for that entry only, so one launch can
    /// take plain captures and tour captures. Without any "+tour" entry, <c>--tour</c> applies to every entry, as before.
    /// </summary>
    public const string TourFlag = "+tour";

    private static readonly string[] KnownFlags = new[] { DenseCsmFlag, CutawayFlag, TourFlag }.Concat(LayoutNames.Select(n => "+" + n)).ToArray();

    /// <summary>True for an entry with <see cref="TourFlag"/> among its options, e.g. "sn1987a+tour".</summary>
    public static bool WantsTour(string? entry) => HasFlag(entry, TourFlag);

    /// <summary>
    /// Whether this entry is captured at the tour's stops: with <c>--tour</c>, every entry when none carries <see cref="TourFlag"/>,
    /// else only the entries that do.
    /// </summary>
    public bool ToursEntry(string? entry)
        => TourStops.Count > 0 && (!PresetIds.Any(WantsTour) || WantsTour(entry));

    /// <summary>
    /// True for an entry ending in <see cref="DenseCsmFlag"/>, e.g. "sn2023ixf@0.001+densecsm": no preset sets the lab's
    /// dense-gas input, so a snapshot of the 3D view's gas shell turns it on (an input edit, so the preset's pins drop).
    /// </summary>
    public static bool WantsDenseCsm(string? entry) => HasFlag(entry, DenseCsmFlag);

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
            if ((main as MainWindow)?.Scene is { } quality)
            {
                quality.AutoQuality = false;   // a snapshot or bench run measures the quality asked for
                quality.Quality = request.Qualities[0];
                quality.IsCutaway = request.Cutaway;
            }
            var presets = request.PresetIds.Count > 0 ? request.PresetIds.Cast<string?>().ToList() : new List<string?> { null };
            foreach (string? entry in presets)
            {
                var (id, own) = SnapshotRequest.SplitPreset(entry);
                if (id is not null) vm.Lab.SelectedPreset = PresetCatalog.Get(id);
                if (SnapshotRequest.WantsDenseCsm(entry)) vm.Lab.DenseCsm = true;
                vm.Layout.Apply(request.LayoutFor(entry));   // Task 15: never the owner's layout.json
                if ((main as MainWindow)?.Scene is { } scene) scene.IsCutaway = request.Cutaway || SnapshotRequest.WantsCutaway(entry);
                if ((own ?? request.ObserverTimeS) is double t) vm.Timeline.ObserverTimeS = t;
                string suffix = SnapshotRequest.FileSuffix(entry);
                if (!request.ToursEntry(entry))
                {
                    await Capture(request, main, windows, suffix, log);
                    continue;
                }
                await CaptureTour(request, main, vm, windows, suffix, log);
            }
            if (await RunSweep(request, main, vm, windows, log) > 0) exitCode = 1;
            await RunBench(request, main, vm, windows, log);
        }
        catch (Exception e)
        {
            log.Add($"ERROR: {e}");
            exitCode = 1;
        }
        finally
        {
            LogSceneRelease(main, log);
            File.WriteAllLines(Path.Combine(request.OutDirectory, "snapshot.log"), log);
            Application.Current.Shutdown(exitCode);
        }
    }

    /// <summary>
    /// Closes the main window (as a user would) and logs whether its 3D view deleted its GL objects with the context current,
    /// and whether the GL control had created its context synchronously in Start (review ⚠️ items).
    /// </summary>
    private static void LogSceneRelease(Window main, List<string> log)
    {
        if (main is not MainWindow mw || mw.Scene is not { } scene) return;
        bool? atStart = mw.GlContextExistsAfterStart;
        try
        {
            main.Close();
            log.Add($"3D view: GL context existed right after Start: {(atStart?.ToString() ?? "no host")}; on closing, " +
                    (scene.GlReleased ? "its GL objects were deleted with the context current" : "its GL objects were NOT released"));
        }
        catch (Exception e)
        {
            log.Add($"3D view: closing the main window failed: {e.Message}");
        }
    }

    /// <summary>
    /// <c>--tour</c>: starts the guided tour of the current explosion (nothing ticks it here) and captures the requested windows at
    /// each requested stop: the caption, the cutaway, the instrument highlight and the time of that stop.
    /// </summary>
    private static async Task CaptureTour(SnapshotRequest request, Window main, MainViewModel vm, WindowService windows, string suffix, List<string> log)
    {
        var director = vm.Tour.Director;
        director.Start();
        int count = director.Stops.Count;
        log.Add($"tour{suffix}: {count} stops: " + string.Join("; ", director.Stops.Select((s, i) =>
            $"{i}: {UnitFormat.Time(s.ObserverTimeS)}{(s.Cutaway ? " cutaway" : "")}{(s.Highlight is { } h ? " " + h : "")}")));
        foreach (int requested in request.TourStops)
        {
            int stop = requested == SnapshotRequest.LastStop ? count - 1 : requested;
            if (stop >= count)
            {
                log.Add($"tour{suffix}: stop {requested} does not exist (the tour has {count} stops)");
                continue;
            }
            director.GoTo(stop);
            // Nothing ticks the tour here, so its orbit never turns: keep the 3D view on demand while capturing (the tour's orbit sets
            // it animating, ruling R-P2-32; the final fix wave's first tour capture drew a stale caption layout while it was).
            if ((main as MainWindow)?.Scene is { } scene) scene.IsTourOrbiting = false;
            log.Add($"tour{suffix}: stop {stop} at {UnitFormat.Time(director.Current!.ObserverTimeS)}: {vm.Tour.Caption}");
            await Capture(request, main, windows, suffix + SnapshotRequest.TourSuffix(stop), log);
        }
        director.Exit();
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
    /// <c>--sweep</c>: for every preset, steps the observer time through log-spaced values from 1 ms to 1,000 yr with the main
    /// and Instruments windows open, giving the 3D view and the panels a frame at each step. Counts exceptions (also
    /// dispatcher exceptions that nothing handled), panels showing "Model error", a failed 3D view, and (Task 14) the model
    /// errors the 3D scene and the timeline markers catch for themselves. Writes sweep.log
    /// and returns the number of problems.
    /// </summary>
    private static async Task<int> RunSweep(SnapshotRequest request, Window main, MainViewModel vm, WindowService windows, List<string> log)
    {
        if (request.SweepSteps is not int steps) return 0;
        windows.ShowInstruments();
        if (windows.Instruments is { } w) PlaceOffScreen(w);
        main.Width = request.Sizes[0].Width;
        main.Height = request.Sizes[0].Height;
        await NextFrame(main);

        var inv = CultureInfo.InvariantCulture;
        var lines = new List<string>
        {
            $"SupernovaLab sweep {DateTime.Now:O}",
            $"{PresetCatalog.LoadEmbedded().Count} presets, {steps} log-spaced times each from {TimeAxis.MinS.ToString("0.###E+0", inv)} s to " +
            $"{TimeAxis.MaxS.ToString("0.###E+0", inv)} s (1,000 yr); main and Instruments windows open, a frame per step",
        };
        int problems = 0;
        var unhandled = new List<string>();
        void OnUnhandled(object s, DispatcherUnhandledExceptionEventArgs e)
        {
            unhandled.Add(e.Exception.ToString());
            e.Handled = true;
        }
        Application.Current.DispatcherUnhandledException += OnUnhandled;
        var scene = (main as MainWindow)?.Scene;
        double logMin = Math.Log(TimeAxis.MinS), logMax = Math.Log(TimeAxis.MaxS);
        try
        {
            foreach (var preset in PresetCatalog.LoadEmbedded())
            {
                int before = problems;
                var seen = new SortedSet<string>();
                // Errors the 3D scene and the timeline markers catch themselves (Project 1's parked minor): counted, not just hidden.
                int sceneErrors0 = scene?.ModelErrorCount ?? 0, markerErrors0 = EventMarkers.Failures;
                try
                {
                    vm.Lab.SelectedPreset = preset;
                    for (int k = 0; k < steps; k++)
                    {
                        double t = k == steps - 1 ? TimeAxis.MaxS : Math.Exp(logMin + (logMax - logMin) * k / (steps - 1));
                        vm.Timeline.ObserverTimeS = t;
                        await NextFrame(main);
                        foreach (var p in vm.Panels)
                            if (p.ErrorText is { } err && seen.Add($"{p.Title}: {err}"))
                            {
                                problems++;
                                lines.Add($"  {preset.Id} at {t.ToString("0.###E+0", inv)} s: panel error: {p.Title}: {err}");
                            }
                        if (scene is not null && scene.ModelErrorCount > sceneErrors0 && seen.Add("scene model error"))
                        {
                            problems++;
                            lines.Add($"  {preset.Id} at {t.ToString("0.###E+0", inv)} s: 3D scene model error caught ({scene.ModelErrorCount - sceneErrors0} so far)");
                        }
                        if (EventMarkers.Failures > markerErrors0 && seen.Add("marker error"))
                        {
                            problems++;
                            lines.Add($"  {preset.Id} at {t.ToString("0.###E+0", inv)} s: timeline-marker model error caught ({EventMarkers.Failures - markerErrors0} so far)");
                        }
                        if (scene is { IsFailed: true } && seen.Add("scene"))
                        {
                            problems++;
                            lines.Add($"  {preset.Id} at {t.ToString("0.###E+0", inv)} s: 3D view failed: {scene.FailureMessage}");
                        }
                    }
                }
                catch (Exception e)
                {
                    problems++;
                    lines.Add($"  {preset.Id}: exception: {e}");
                }
                foreach (string u in unhandled)
                {
                    problems++;
                    lines.Add($"  {preset.Id}: unhandled dispatcher exception: {u}");
                }
                unhandled.Clear();
                lines.Add($"{preset.Id}: {(problems == before ? "ok" : "PROBLEMS")}; outcome {vm.CurrentOutcome.Type}, fate {vm.CurrentOutcome.Fate}; " +
                          $"ended at {UnitFormat.Time(vm.Timeline.ObserverTimeS)}");
            }
        }
        finally
        {
            Application.Current.DispatcherUnhandledException -= OnUnhandled;
        }
        lines.Add($"sweep problems: {problems}");
        File.WriteAllLines(Path.Combine(request.OutDirectory, "sweep.log"), lines);
        log.AddRange(lines);
        return problems;
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

        // The worst case: the two views that redraw as the time moves (the schematic spectrum and the pulse train) are
        // shown, beside the other four panels' curves and the strip.
        vm.Panels.OfType<VisiblePanelViewModel>().Single().ShowSubTabForSnapshot(VisiblePanelViewModel.SpectrumTab);
        vm.Panels.OfType<RadioPanelViewModel>().Single().ShowSubTabForSnapshot(RadioPanelViewModel.PulsarTab);
        if (windows.Instruments is InstrumentsWindow iw) await RedrawPanels(iw);

        // One run for every size with every quality level (one launch for the whole matrix): the timeline plays once per run.
        // With --bench-tour the guided tour plays instead, once for each requested preset (Task 14).
        var clock = new PlaybackClock(vm.Timeline);
        clock.Start();
        if (request.BenchTour) new TourClock(vm.Tour.Director).Start();   // the app's own clock is not started in a snapshot run
        var all = new List<string>();
        var scene = (main as MainWindow)?.Scene;
        var entries = request.BenchTour && request.PresetIds.Count > 0 ? request.PresetIds.Cast<string?>().ToList() : new List<string?> { null };
        foreach (string? entry in entries)
        {
            if (entry is not null)
            {
                var (id, _) = SnapshotRequest.SplitPreset(entry);
                if (id is not null) vm.Lab.SelectedPreset = PresetCatalog.Get(id);
                else ApplyLabDefaults(vm.Lab);   // "default": the lab as launched, whatever an earlier entry loaded
                if (SnapshotRequest.WantsDenseCsm(entry)) vm.Lab.DenseCsm = true;
            }
            foreach (var size in request.Sizes)
                foreach (var quality in request.Qualities)
                {
                    if (scene is not null) scene.Quality = quality;
                    var lines = await RunBenchOnce(main, vm, size, quality, seconds, scene, request.BenchTour);
                    all.AddRange(lines);
                    all.Add("");
                }
        }
        File.WriteAllLines(Path.Combine(request.OutDirectory, "bench.log"), all);
        log.AddRange(all);
    }

    /// <summary>Sets every input of the lab back to <see cref="LabInputs.Default"/> (the bench's "default" entry after a preset was loaded).</summary>
    private static void ApplyLabDefaults(LabViewModel lab)
    {
        var d = LabInputs.Default;
        lab.Kind = d.Kind; lab.InitialMass = d.InitialMass; lab.Metallicity = d.Metallicity; lab.Stripping = d.Stripping;
        lab.Rotation = d.Rotation; lab.Field = d.Field; lab.IaChannel = d.IaChannel; lab.WhiteDwarfMass = d.WhiteDwarfMass;
        lab.Mixing = d.Mixing; lab.Yield = d.Yield; lab.DenseCsm = d.DenseCsm; lab.Opacity = d.Opacity;
    }

    private static async Task<List<string>> RunBenchOnce(Window main, MainViewModel vm, SnapshotSize size, QualityLevel quality,
        double seconds, SupernovaLab.App.Controllers.SceneController? scene, bool tour = false)
    {
        main.Width = size.Width;
        main.Height = size.Height;
        await NextFrame(main);

        double decades = Math.Log10(TimeAxis.MaxS / TimeAxis.MinS);
        vm.Timeline.DecadesPerSecond = decades / seconds;
        vm.Timeline.IsPlaying = false;
        vm.Timeline.ObserverTimeS = TimeAxis.MinS;
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
        long sceneFrames0 = scene?.FramesDrawn ?? 0;
        scene?.ResetGpuStats();
        // The tour's travel and dwell are timed apart: a dwell on a still scene redraws only when something moves.
        var director = vm.Tour.Director;
        double travelS = 0, dwellS = 0;
        long travelFrames = 0, dwellFrames = 0;
        string tourText = "";
        CompositionTarget.Rendering += OnRendering;
        if (tour)
        {
            director.Start();
            tourText = $"the guided tour ({director.Stops.Count} stops) instead of the plain timeline; ";
            long framesBefore = scene?.FramesDrawn ?? 0;
            double wallBefore = watch.Elapsed.TotalSeconds;
            while (director.State != SupernovaLab.App.Tour.TourState.Off && watch.Elapsed.TotalSeconds < 200)
            {
                await Task.Delay(100);
                long f = scene?.FramesDrawn ?? 0;
                double w = watch.Elapsed.TotalSeconds;
                if (director.State == SupernovaLab.App.Tour.TourState.Dwelling) { dwellS += w - wallBefore; dwellFrames += f - framesBefore; }
                else { travelS += w - wallBefore; travelFrames += f - framesBefore; }
                framesBefore = f;
                wallBefore = w;
            }
            if (director.State != SupernovaLab.App.Tour.TourState.Off) director.Exit();
        }
        else
        {
            vm.Timeline.IsPlaying = true;
            while (vm.Timeline.IsPlaying && watch.Elapsed.TotalSeconds < seconds * 1.5 + 5)
                await Task.Delay(100);
        }
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
            $"quality {quality} ({(int)quality:N0} particles); HDR pipeline {(scene is null ? "n/a" : scene.HdrEnabled ? "on (RGBA16F, bloom)" : "off (no-bloom path)")}",
            $"preset {preset}; main window {main.ActualWidth:0}x{main.ActualHeight:0} and the Instruments window open (off-screen), " +
            "the visible panel on its schematic spectrum and the radio panel on its pulses; " +
            (tour ? $"playing {tourText}travelling at {TimelineViewModel.DefaultDecadesPerSecond.ToString("0.###", inv)} decades/s (slowed near each stop)"
                 : $"playing the whole timeline ({decades.ToString("0.0", inv)} decades) at {vm.Timeline.DecadesPerSecond.ToString("0.###", inv)} decades/s"),
            $"frames {frames} in {elapsed.ToString("0.00", inv)} s: {(frames / elapsed).ToString("0.0", inv)} fps",
            $"frame interval p95 {p95.ToString("0.0", inv)} ms, worst {worst.ToString("0.0", inv)} ms",
            scene is null ? "3D view GPU: none" : $"3D view GPU: {(scene.GlInfo.Length > 0 ? scene.GlInfo : "not reported")}",
            scene is null ? "3D view: none" : scene.IsFailed ? $"3D view: failed ({scene.FailureMessage})" :
                $"3D view frames drawn {sceneFrames} in {elapsed.ToString("0.00", inv)} s: " +
                $"{(sceneFrames / elapsed).ToString("0.0", inv)} fps (GL draws counted by the scene controller)",
            tour ? $"reached the end of the tour: {(director.State == SupernovaLab.App.Tour.TourState.Off ? "yes" : "no")}; last time {UnitFormat.Time(vm.Timeline.ObserverTimeS)}"
                 : $"reached the end of the timeline: {(vm.Timeline.ObserverTimeS >= TimeAxis.MaxS ? "yes" : "no")}",
        }.ToList();
        if (tour && scene is not null)
            lines.Add($"3D draws while travelling: {travelFrames} in {travelS.ToString("0.0", inv)} s = {(travelFrames / Math.Max(travelS, 1e-9)).ToString("0.0", inv)} fps; " +
                      $"while dwelling on a stop: {dwellFrames} in {dwellS.ToString("0.0", inv)} s = {(dwellFrames / Math.Max(dwellS, 1e-9)).ToString("0.0", inv)} fps");
        if (scene is not null)
            lines.Add(!scene.GpuTimingAvailable || scene.GpuSamples == 0
                ? "GPU render time (the governor's measurement): not available on this GPU"
                : $"GPU render time (the governor's measurement, GL timer query): mean {(scene.MeanGpuRenderTimeS * 1000).ToString("0.00", inv)} ms, " +
                  $"max {(scene.MaxGpuRenderTimeS * 1000).ToString("0.00", inv)} ms over {scene.GpuSamples} drawn frames " +
                  $"(the governor steps down above {(1000 / QualityGovernor.MinFps).ToString("0", inv)} ms)");
        return lines;
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
