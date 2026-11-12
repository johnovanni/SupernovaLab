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

        var windows = new WindowService();
        var vm = new MainViewModel(new OutcomeEngine(ExplodabilityTable.LoadEmbedded()), windows);
        var main = new MainWindow { DataContext = vm };
        windows.Attach(main);
        MainWindow = main;

        if (snapshot is not null)
        {
            await SnapshotRenderer.RunAsync(snapshot, main, vm, windows);
            return;
        }

        new PlaybackClock(vm.Timeline).Start();
        main.Show();
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
