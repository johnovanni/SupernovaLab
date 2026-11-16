using System.Windows;
using System.Windows.Threading;
using SupernovaLab.App.Services;
using SupernovaLab.App.ViewModels;

namespace SupernovaLab.Tests;

/// <summary>Task 17 (the owner: "open the close up window right from the start ... I'd like to see it all the time").</summary>
public class CloseUpAtStartTests
{
    [Fact]
    public void TheCloseUp_OpensOnceTheMainWindowIsFirstShown_AndOnlyThen()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                InstrumentsWindowTests.EnsureApplication();
                var service = new WindowService(closeUpFactory: _ => OffScreen(new Window()), audioFactory: () => new FakePulseAudio());
                var vm = new MainViewModel(TestOutcomes.Engine, service);
                var owner = OffScreen(new Window { DataContext = vm, Width = 400, Height = 300, Content = new System.Windows.Controls.Grid() });
                service.Attach(owner);
                service.OpenCloseUpWhenShown(owner);
                Assert.Null(service.CloseUp);                  // not before the main window is on screen
                owner.Show();
                PumpUntil(() => service.CloseUp is not null, TimeSpan.FromSeconds(5));   // the first render runs on the message loop
                Assert.NotNull(service.CloseUp);
                Assert.Equal(1, service.CloseUpWindowsMade);

                // Closed by the user, it stays closed: the main window rendering again does not reopen it.
                service.CloseUp!.Close();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                owner.Width = 420;
                owner.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.Null(service.CloseUp);
                Assert.Equal(1, service.CloseUpWindowsMade);
                owner.Close();
            }
            catch (Exception e) { error = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.Null(error);
    }

    private static void PumpUntil(Func<bool> done, TimeSpan limit)
    {
        var frame = new DispatcherFrame();
        var started = DateTime.UtcNow;
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(20), DispatcherPriority.Background, (_, _) =>
        {
            if (done() || DateTime.UtcNow - started > limit) frame.Continue = false;
        }, Dispatcher.CurrentDispatcher);
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
    }

    private static Window OffScreen(Window w)
    {
        w.WindowStartupLocation = WindowStartupLocation.Manual;
        w.Left = -30000;
        w.Top = -30000;
        w.ShowActivated = false;
        w.ShowInTaskbar = false;
        return w;
    }
}
