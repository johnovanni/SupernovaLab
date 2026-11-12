using System.Windows.Input;
using SupernovaLab.App.ViewModels;

namespace SupernovaLab.App.Views;

/// <summary>The outcome map strip (spec §3). Clicking it sets the initial mass at that point.</summary>
public partial class OutcomeMapStrip : System.Windows.Controls.UserControl
{
    public OutcomeMapStrip() => InitializeComponent();

    private void OnStripClick(object sender, MouseButtonEventArgs e)
    {
        if (Strip.ActualWidth <= 0 || Strip.DataContext is not OutcomeViewModel vm) return;
        vm.SetMassFromMapFraction(e.GetPosition(Strip).X / Strip.ActualWidth);
    }
}
