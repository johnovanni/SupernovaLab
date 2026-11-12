using System.Windows;
using SupernovaLab.App.ViewModels;

namespace SupernovaLab.App.Views;

/// <summary>The timeline (spec §4). DataContext: TimelineViewModel. It tells the view model the label track's width.</summary>
public partial class TimelineBar : System.Windows.Controls.UserControl
{
    public TimelineBar()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Layout();
    }

    private void OnTrackSizeChanged(object sender, SizeChangedEventArgs e) => Layout();

    private void Layout()
    {
        if (DataContext is TimelineViewModel vm) vm.LayoutMarkerLabels(Track.ActualWidth);
    }
}
