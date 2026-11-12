namespace SupernovaLab.App.Windows;

/// <summary>
/// Every quantity of the current outcome with its value, range, confidence badge and full citation, plus all the notes
/// (spec §3). Non-modal; DataContext is the shared MainViewModel, so it updates live.
/// </summary>
public partial class OutcomeDetailsWindow : System.Windows.Window
{
    public OutcomeDetailsWindow() => InitializeComponent();
}
