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
            MessageBox.Show(ex.Message, "SupernovaLab --snapshot", MessageBoxButton.OK, MessageBoxImage.Error);
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
}
