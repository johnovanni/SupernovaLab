using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using SupernovaLab.App.Services;
using SupernovaLab.App.ViewModels;
using SupernovaLab.App.Windows;
using SupernovaLab.Core.Units;
using SupernovaLab.Core.Presets;
using SupernovaLab.Core.Timeline;
using SupernovaLab.Rendering;

namespace SupernovaLab.Tests;

/// <summary>
/// Task 14b (ruling R-P3-28's main-view regression): the bench's per-subsystem timing line, and the Close-up's readout rows kept and
/// updated in place (replacing the bound list rebuilt every row's elements on every frame, shown or hidden), showing exactly the
/// readouts the model gives.
/// </summary>
public class Task14bPerfTests
{
    // ---- The bench's timing line ----

    [Fact]
    public void BenchLine_GivesEachSubsystemPerFrame_AndTheRenderPassRemainder()
    {
        // 100 frames: render pass 2.5 s; time step 0.8 s (panels 0.2, main scene 0.1, Close-up VM 0.4 incl. controller 0.05);
        // pulse trace 0.03 s; GL OnRender main 0.3 s, Close-up 0.1 s. Remainder 2.5 − 0.8 − 0.03 − 0.3 − 0.1 = 1.27 s.
        string line = BenchTimings.Line(100, 2.5, 0.8, 0.2, 0.1, 0.4, 0.05, 0.03, 0.3, 0.1);
        Assert.Equal("UI thread per composition frame (ms): time step 8.00 (panels 2.00, main scene 1.00, Close-up view model 4.00 " +
                     "incl. its controller 0.50, other listeners 1.00); pulse trace 0.30; GL OnRender main 3.00, Close-up 1.00; " +
                     "rest of the render pass (WPF layout, element rendering, composition hand-off) 12.70", line);
    }

    [Fact]
    public void BenchLine_NeverNegative_AndNoFramesIsNotANumber()
    {
        string line = BenchTimings.Line(10, 0.1, 0.5, 0.6, 0, 0, 0, 0, 0, 0);
        Assert.Contains("other listeners 0.00", line);
        Assert.EndsWith("composition hand-off) 0.00", line);
        Assert.Contains("time step n/a", BenchTimings.Line(0, 1, 1, 0, 0, 0, 0, 0, 0, 0));
    }

    [Fact]
    public void BenchTimings_OffByDefault_RecordNothing()
    {
        Assert.False(BenchTimings.Enabled);   // only the bench turns them on, for its runs
        var stats = new CpuTimeStats();
        long t0 = BenchTimings.Begin();
        Assert.Equal(0, t0);
        BenchTimings.End(stats, t0);
        Assert.Equal(0, stats.Count);
    }

    // ---- The readout rows ----

    private static ReadoutRow Row(string label, string value, Confidence c = Confidence.Established)
        => new(label, label + " tip", value, c, "badge " + label, false, "");

    [Fact]
    public void Sync_UpdatesInPlace_WhenOnlyValuesChange()
    {
        var views = new System.Collections.ObjectModel.ObservableCollection<ReadoutRowView>();
        ReadoutRowView.Sync(views, new[] { Row("Spin", "33.4 ms"), Row("Age", "937 yr") });
        var first = views.ToList();
        var events = new List<NotifyCollectionChangedAction>();
        views.CollectionChanged += (_, e) => events.Add(e.Action);
        var raised = new List<string?>();
        first[0].PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        var next = new[] { Row("Spin", "33.5 ms"), Row("Age", "938 yr") };
        ReadoutRowView.Sync(views, next);
        Assert.Empty(events);   // no row added, removed or replaced: the window keeps its elements
        Assert.Same(first[0], views[0]);
        Assert.Same(first[1], views[1]);
        Assert.Equal(next, views.Select(v => v.Row));
        Assert.Equal(new[] { nameof(ReadoutRowView.ValueText) }, raised);   // only the field that changed

        raised.Clear();
        ReadoutRowView.Sync(views, next);   // the same rows again: nothing raised
        Assert.Empty(raised);
    }

    [Fact]
    public void Sync_AddsAndRemovesAtTheEnd_AndShowsEveryField()
    {
        var views = new System.Collections.ObjectModel.ObservableCollection<ReadoutRowView>();
        ReadoutRowView.Sync(views, new[] { Row("A", "1"), Row("B", "2"), Row("C", "3") });
        var a = views[0];
        var shorter = new[] { new ReadoutRow("A2", null, "9", Confidence.Speculative, "t", true, "published") };
        ReadoutRowView.Sync(views, shorter);
        Assert.Single(views);
        Assert.Same(a, views[0]);
        var v = views[0];
        Assert.Equal(("A2", (string?)null, "9", Confidence.Speculative, "t", true, "published"),
            (v.Label, v.LabelToolTip, v.ValueText, v.Confidence, v.BadgeToolTip, v.Published, v.PublishedToolTip));
        ReadoutRowView.Sync(views, new[] { Row("A", "1"), Row("B", "2") });
        Assert.Equal(2, views.Count);
        Assert.Same(a, views[0]);
        Assert.Equal("B", views[1].Label);
        ReadoutRowView.Sync(views, Array.Empty<ReadoutRow>());
        Assert.Empty(views);
    }

    /// <summary>
    /// Over the whole timeline of the two presets whose rows change every frame (the Crab pulsar, the collapsar's growing black hole) and
    /// the static black hole, the column's rows always show exactly the readouts (so no shown value changes), and a step that keeps the
    /// number of rows keeps every row object.
    /// </summary>
    [Theory]
    [InlineData("sn1054")]
    [InlineData("sn1998bw")]
    [InlineData("n6946bh1")]
    public void CloseUpRows_MirrorTheReadouts_AndAreKeptAcrossTimeSteps(string preset)
    {
        var main = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
        main.Lab.SelectedPreset = PresetCatalog.Get(preset);
        var vm = new CloseUpViewModel(main);
        main.Timeline.DecadesPerSecond = Math.Log10(TimeAxis.MaxS / TimeAxis.MinS) / 10;   // the whole timeline in 600 steps
        main.Timeline.ObserverTimeS = TimeAxis.MinS;
        main.Timeline.IsPlaying = true;
        int steps = 0, kept = 0, valueSteps = 0;
        while (main.Timeline.IsPlaying && steps < 1000)
        {
            var before = vm.ReadoutViews.ToList();
            var rowsBefore = vm.Readouts;
            main.Timeline.Tick(TimeSpan.FromSeconds(1 / 60.0));
            steps++;
            Assert.Equal(vm.Readouts, vm.ReadoutViews.Select(v => v.Row));
            Assert.Equal(vm.State.Readouts.Select(ReadoutRow.From), vm.ReadoutViews.Select(v => v.Row));
            if (before.Count == vm.ReadoutViews.Count)
            {
                Assert.True(before.SequenceEqual(vm.ReadoutViews), "a step with the same number of rows keeps every row object");
                kept++;
                if (!ReferenceEquals(rowsBefore, vm.Readouts)) valueSteps++;
            }
        }
        Assert.False(main.Timeline.IsPlaying);   // reached the end
        Assert.True(kept > steps * 0.9, $"{kept} of {steps} steps kept the rows");
        if (preset != "n6946bh1") Assert.True(valueSteps > steps / 2, $"values changed on {valueSteps} of {steps} steps");
        vm.Dispose();
    }

    /// <summary>
    /// The window binds the kept rows: its readout column's ItemsControl shows <see cref="CloseUpViewModel.ReadoutViews"/>, and across
    /// time steps that change values its row elements stay the same objects while their texts follow (off-screen, no GL context).
    /// </summary>
    [Fact]
    public void CloseUpWindow_KeepsItsRowElements_AndShowsTheNewValues()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                InstrumentsWindowTests.EnsureApplication();
                var main = new MainViewModel(TestOutcomes.Engine, new FakeWindowService());
                main.Lab.SelectedPreset = PresetCatalog.Get("sn1054");
                main.Timeline.ObserverTimeS = 1e3;
                var vm = new CloseUpViewModel(main);
                var cw = new CloseUpWindow { DataContext = vm };   // constructed, not shown: no GL context
                Pump();
                var column = FindLogical<ItemsControl>(cw).Single(c => ReferenceEquals(c.ItemsSource, vm.ReadoutViews));
                Assert.Contains(FindLogical<ItemsControl>(cw), c => ReferenceEquals(c.ItemsSource, vm.CutawayReadoutViews));

                // The same template in a plain off-screen window, laid out, to see its elements.
                var items = new ItemsControl { ItemTemplate = column.ItemTemplate };
                items.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(CloseUpViewModel.ReadoutViews)));
                var host = new Window
                {
                    DataContext = vm, Width = 300, Height = 640, WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000,
                    Top = -30000, ShowActivated = false, ShowInTaskbar = false, Content = items,
                };
                host.Show();
                Pump();
                var containers = Enumerable.Range(0, vm.ReadoutViews.Count).Select(i => items.ItemContainerGenerator.ContainerFromIndex(i)).ToList();
                Assert.All(containers, Assert.NotNull);
                var rowsBefore = vm.Readouts;
                main.Timeline.ObserverTimeS = 1e5;   // the young neutron star's readouts change (it cools)
                Pump();
                Assert.NotEqual(rowsBefore, vm.Readouts);
                Assert.Equal(containers, Enumerable.Range(0, vm.ReadoutViews.Count).Select(i => items.ItemContainerGenerator.ContainerFromIndex(i)));
                for (int i = 0; i < vm.Readouts.Count; i++)
                {
                    var texts = FindVisual<TextBlock>((DependencyObject)containers[i]!).Select(t => t.Text).ToList();
                    Assert.Contains(vm.Readouts[i].ValueText, texts);
                    Assert.Contains(vm.Readouts[i].Label, texts);
                }
                host.Close();
                cw.Close();   // closing the window disposes its view model
                vm.Dispose();
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
    }

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    private static IEnumerable<T> FindLogical<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject d) continue;
            if (d is T t) yield return t;
            foreach (var x in FindLogical<T>(d)) yield return x;
        }
    }

    private static IEnumerable<T> FindVisual<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var d = VisualTreeHelper.GetChild(root, i);
            if (d is T t) yield return t;
            foreach (var x in FindVisual<T>(d)) yield return x;
        }
    }
}
