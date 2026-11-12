namespace SupernovaLab.App.Windows;

/// <summary>
/// The selected preset's "what was actually seen" story, its measurements and its published values, with citations
/// (spec §2.3, §5.0); "No preset selected" otherwise. Non-modal; DataContext is the shared MainViewModel.
/// </summary>
public partial class PresetInfoWindow : System.Windows.Window
{
    public PresetInfoWindow() => InitializeComponent();
}
