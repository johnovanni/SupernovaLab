using System.Windows;
using SupernovaLab.App.Windows;
using SupernovaLab.Core.Signals;

namespace SupernovaLab.App.Services;

/// <summary>
/// Opens the non-modal windows of spec §5.0, owned by the main window (so they close with it) and bound to its
/// DataContext (the shared MainViewModel). At most one of each; asking again activates it. The Instruments window's size
/// and position are remembered for the session.
/// </summary>
public sealed class WindowService : IWindowService
{
    private Window? _owner;
    private InstrumentsWindow? _instruments;
    private OutcomeDetailsWindow? _details;
    private PresetInfoWindow? _presetInfo;
    private Rect? _instrumentsBounds;

    /// <summary>The main window: the owner of every window this service opens, and their DataContext.</summary>
    public void Attach(Window owner) => _owner = owner ?? throw new ArgumentNullException(nameof(owner));

    /// <summary>The open windows (null when closed), for the --snapshot diagnostic.</summary>
    public Window? Instruments => _instruments;
    public Window? OutcomeDetails => _details;
    public Window? PresetInfo => _presetInfo;

    /// <summary>The Instruments window's focus request. Task 15 maximizes that panel; here it is only recorded.</summary>
    public Messenger? LastInstrumentsFocus { get; private set; }

    public void ShowInstruments(Messenger? focus = null)
    {
        LastInstrumentsFocus = focus;
        _instruments = ShowOrActivate(_instruments, () =>
        {
            var w = new InstrumentsWindow();
            if (_instrumentsBounds is { } b)
            {
                w.WindowStartupLocation = WindowStartupLocation.Manual;
                (w.Left, w.Top, w.Width, w.Height) = (b.Left, b.Top, b.Width, b.Height);
            }
            w.Closing += (_, _) => _instrumentsBounds = w.RestoreBounds;
            w.Closed += (_, _) => _instruments = null;
            return w;
        });
    }

    public void ShowOutcomeDetails() => _details = ShowOrActivate(_details, () =>
    {
        var w = new OutcomeDetailsWindow();
        w.Closed += (_, _) => _details = null;
        return w;
    });

    public void ShowPresetInfo() => _presetInfo = ShowOrActivate(_presetInfo, () =>
    {
        var w = new PresetInfoWindow();
        w.Closed += (_, _) => _presetInfo = null;
        return w;
    });

    private T ShowOrActivate<T>(T? existing, Func<T> create) where T : Window
    {
        if (existing is not null)
        {
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            existing.Activate();
            return existing;
        }
        var owner = _owner ?? throw new InvalidOperationException("WindowService.Attach must be called before showing windows.");
        var window = create();
        window.Owner = owner;
        window.DataContext = owner.DataContext;
        window.Show();
        return window;
    }
}
