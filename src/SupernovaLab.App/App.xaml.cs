using System.Windows;
using SupernovaLab.App.Services;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Outcomes;

namespace SupernovaLab.App;

/// <summary>
/// Composition (Task 14): builds the engine, the shared MainViewModel and the WindowService, and shows MainWindow. With
/// <c>--snapshot</c> (ruling R19) it renders the requested windows to PNG and exits instead.
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        SnapshotRequest? snapshot;
        try
        {
            snapshot = SnapshotRequest.Parse(e.Args);
        }
        catch (ArgumentException ex)
        {
            // An unattended diagnostic run must not block on a dialog: report and exit.
            ReportBadSnapshotArguments(e.Args, ex.Message);
            Shutdown(2);
            return;
        }

        // Plan ruling P3-5: one beam clock, handed to both 3D views (the main window's and the Close-up's).
        var beamClock = new BeamClock();
        // Task 15 (R-P3-43): a snapshot run keeps every window off the screen, so the Close-up's placement is not held inside the screens.
        var windows = snapshot is null ? new WindowService(beamClock) : new WindowService(beamClock, virtualScreen: () => Rect.Empty);
        // Task 15: the panel layout is remembered in %LOCALAPPDATA%\SupernovaLab\layout.json; a snapshot run never reads or writes it.
        var vm = new MainViewModel(new OutcomeEngine(ExplodabilityTable.LoadEmbedded()), windows,
            snapshot is null ? LayoutStore.ForCurrentUser() : null);
        var main = new MainWindow { DataContext = vm, BeamClock = beamClock };
        windows.Attach(main);
        MainWindow = main;

        if (snapshot is not null)
        {
            await SnapshotRenderer.RunAsync(snapshot, main, vm, windows);
            return;
        }

        // Review M1: a last-resort handler. Model calls are guarded where they are made; anything that still escapes is
        // logged and reported instead of ending the app without a word.
        DispatcherUnhandledException += OnUnhandledException;
        new PlaybackClock(vm.Timeline).Start();
        new TourClock(vm.Tour.Director).Start();
        main.Show();
    }

    private bool _reportedUnhandled;

    /// <summary>
    /// Review M1: writes an unhandled exception to %LOCALAPPDATA%\SupernovaLab\error.log (and the trace), tells the user
    /// once per session, and keeps the app running.
    /// </summary>
    private void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        string path = ErrorLog.Write(e.Exception);
        e.Handled = true;
        if (_reportedUnhandled) return;
        _reportedUnhandled = true;
        MessageBox.Show(MainWindow!, $"Something went wrong ({e.Exception.GetType().Name}: {e.Exception.Message}). The details " +
            $"were written to {path}. The app keeps running; if a view looks wrong, restart it.", "SupernovaLab",
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>Writes a bad --snapshot argument to stderr, and to snapshot.log when the output directory was given.</summary>
    private static void ReportBadSnapshotArguments(string[] args, string message)
    {
        Console.Error.WriteLine($"SupernovaLab --snapshot: {message}");
        int at = Array.IndexOf(args, "--snapshot");
        if (at < 0 || at + 1 >= args.Length || args[at + 1].StartsWith("--", StringComparison.Ordinal)) return;
        try
        {
            System.IO.Directory.CreateDirectory(args[at + 1]);
            System.IO.File.WriteAllText(System.IO.Path.Combine(args[at + 1], "snapshot.log"), $"ERROR: {message}{Environment.NewLine}");
        }
        catch (Exception io) when (io is System.IO.IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Nothing more can be reported.
        }
    }
}
