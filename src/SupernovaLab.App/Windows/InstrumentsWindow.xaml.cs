using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SupernovaLab.App.ViewModels;
using SupernovaLab.App.Views;
using SupernovaLab.Core.Signals;

namespace SupernovaLab.App.Windows;

/// <summary>
/// The Instruments window (spec §5.0): the six panels in a 3×2 grid, bound to the shared panel view models, so closing
/// and reopening it loses nothing. Double-clicking a panel maximizes it within the window; Esc restores the grid.
/// Which panel is enlarged is a view state, kept here.
/// </summary>
public partial class InstrumentsWindow : Window
{
    public InstrumentsWindow()
    {
        InitializeComponent();
        // Belt and braces with the views' own Unloaded: a closed window's views stop listening to the shared panels.
        Closed += (_, _) =>
        {
            foreach (var view in PanelViews) view.Detach();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || MaximizedPanel.Visibility != Visibility.Visible) return;
            RestoreGrid();
            e.Handled = true;
        };
    }

    /// <summary>The panel shown enlarged, or null when the grid is shown.</summary>
    public InstrumentPanelViewModel? Maximized => MaximizedPanel.Visibility == Visibility.Visible
        ? MaximizedPanel.DataContext as InstrumentPanelViewModel
        : null;

    /// <summary>Enlarges the panel of this messenger (the strip's tiles), or shows the grid for null (the View menu).</summary>
    public void ShowPanel(Messenger? messenger)
    {
        var panel = messenger is { } m && DataContext is MainViewModel vm ? vm.Panels.FirstOrDefault(p => p.Messenger == m) : null;
        if (panel is null) RestoreGrid();
        else Maximize(panel);
    }

    public void Maximize(InstrumentPanelViewModel panel)
    {
        MaximizedPanel.DataContext = panel;
        MaximizedPanel.Visibility = Visibility.Visible;
        PanelGrid.Visibility = Visibility.Collapsed;
    }

    public void RestoreGrid()
    {
        MaximizedPanel.Visibility = Visibility.Collapsed;
        MaximizedPanel.DataContext = null;
        PanelGrid.Visibility = Visibility.Visible;
    }

    /// <summary>The six panel views of the grid (for the --snapshot diagnostic).</summary>
    public IEnumerable<InstrumentPanel> PanelViews => Descendants<InstrumentPanel>(PanelGrid).Append(MaximizedPanel);

    private void OnPanelMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not InstrumentPanel view || IsOnAControl(e.OriginalSource as DependencyObject, view)) return;
        if (ReferenceEquals(view, MaximizedPanel)) RestoreGrid();
        else if (view.DataContext is InstrumentPanelViewModel panel) Maximize(panel);
        e.Handled = true;
    }

    /// <summary>A double-click on a tab, the picker or a scroll bar is theirs, not a request to enlarge the panel.</summary>
    private static bool IsOnAControl(DependencyObject? node, DependencyObject root)
    {
        for (; node is not null && !ReferenceEquals(node, root); node = node is Visual or System.Windows.Media.Media3D.Visual3D
                 ? VisualTreeHelper.GetParent(node)
                 : LogicalTreeHelper.GetParent(node))
            if (node is ButtonBase or ComboBox or ListBoxItem or ScrollBar or TextBoxBase) return true;
        return false;
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
}
