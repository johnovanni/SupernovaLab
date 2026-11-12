using System.Windows.Controls;
using System.Windows.Input;
using SupernovaLab.App.ViewModels;
using SupernovaLab.Core.Presets;

namespace SupernovaLab.App.Views;

/// <summary>The lab's inputs (spec §2). DataContext: MainViewModel.</summary>
public partial class LabPanel : UserControl
{
    public LabPanel() => InitializeComponent();

    /// <summary>
    /// A ComboBox does not report choosing the item that is already selected, so that click is passed on here: after an
    /// observer edit it restores the preset's published distance, dust and beam (Task 14 review N2); otherwise it does
    /// nothing.
    /// </summary>
    private void OnPresetItemClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is ComboBoxItem { DataContext: Preset p } && DataContext is MainViewModel vm && ReferenceEquals(p, vm.Lab.SelectedPreset))
            vm.Lab.SelectedPreset = p;
    }
}
