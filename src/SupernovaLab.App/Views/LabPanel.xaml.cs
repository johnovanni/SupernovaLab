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
        if (sender is ComboBoxItem { DataContext: Preset p } && DataContext is MainViewModel vm) RestoreIfSelected(p, vm);
    }

    /// <summary>
    /// The keyboard's way to choose an item (Task 15 review M6): Enter on the highlighted item of the open list. Choosing
    /// the selected preset that way restores its observer, as a click does. The key is not handled here, so the list
    /// still closes as usual.
    /// </summary>
    private void OnPresetItemKey(object sender, KeyEventArgs e)
    {
        if (IsChooseKey(e.Key) && sender is ComboBoxItem { DataContext: Preset p } && DataContext is MainViewModel vm)
            RestoreIfSelected(p, vm);
    }

    /// <summary>Whether this key chooses the highlighted item of an open preset list.</summary>
    public static bool IsChooseKey(Key key) => key == Key.Enter;

    /// <summary>
    /// Choosing the preset that is already selected: restores its published distance, dust and beam after an observer
    /// edit (it does nothing when they are unchanged). Returns false when <paramref name="item"/> is another preset.
    /// </summary>
    public static bool RestoreIfSelected(Preset item, MainViewModel vm)
    {
        if (!ReferenceEquals(item, vm.Lab.SelectedPreset)) return false;
        vm.Lab.SelectedPreset = item;
        return true;
    }
}
