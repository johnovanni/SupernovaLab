using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SupernovaLab.App.ViewModels;
using SupernovaLab.App.Windows;

namespace SupernovaLab.Tests;

/// <summary>
/// Review I3: the panel view models live as long as the app, so an Instruments window's panel views must stop listening
/// to them when the window closes; otherwise every reopen adds views that keep redrawing. The windows are shown
/// off-screen (no screen capture; ruling R19).
/// </summary>
public class InstrumentsWindowTests
{
    /// <summary>
    /// The panel views listening to a panel's PropertyChanged (the toolkit's field-like event). WPF's own binding manager
    /// and the strip's tile also listen; only the views are counted.
    /// </summary>
    private static int Subscribers(InstrumentPanelViewModel panel)
    {
        var field = typeof(ObservableObject).GetField("PropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (field.GetValue(panel) as PropertyChangedEventHandler)?.GetInvocationList()
            .Count(d => d.Target is SupernovaLab.App.Views.InstrumentPanel) ?? 0;
    }

    [Fact]
    public void ClosingAndReopening_LeavesOneLiveViewPerPanel()
    {
        Exception? error = null;
        var counts = new List<int[]>();
        var thread = new Thread(() =>
        {
            try
            {
                EnsureApplication();
                var vm = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
                counts.Add(vm.Panels.Select(Subscribers).ToArray()); // no window: no view
                for (int round = 0; round < 3; round++)
                {
                    var w = new InstrumentsWindow
                    {
                        DataContext = vm, WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000,
                        ShowActivated = false, ShowInTaskbar = false,
                    };
                    w.Show();
                    Pump();
                    counts.Add(vm.Panels.Select(Subscribers).ToArray()); // one view each
                    w.Close();
                    Pump();
                }
                counts.Add(vm.Panels.Select(Subscribers).ToArray()); // closed: none again
            }
            catch (Exception e)
            {
                error = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(error);
        Assert.All(counts[0], n => Assert.Equal(0, n));
        foreach (var open in new[] { counts[1], counts[2], counts[3] }) Assert.All(open, n => Assert.Equal(1, n));
        Assert.All(counts[4], n => Assert.Equal(0, n));
    }

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    /// <summary>
    /// The windows' StaticResources come from the application's theme. One Application per process: it is made once,
    /// with every brush frozen and every style sealed, so other tests on other threads can still read its resources.
    /// </summary>
    internal static void EnsureApplication()
    {
        if (Application.Current is not null) return;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var theme = (ResourceDictionary)Application.LoadComponent(
            new Uri("/SupernovaLab.App;component/Resources/Theme.xaml", UriKind.Relative));
        foreach (object key in theme.Keys.Cast<object>().ToList())
        {
            switch (theme[key])
            {
                case Freezable f when f.CanFreeze: f.Freeze(); break;
                case Style s: s.Seal(); break;
            }
        }
        app.Resources.MergedDictionaries.Add(theme);
    }
}
