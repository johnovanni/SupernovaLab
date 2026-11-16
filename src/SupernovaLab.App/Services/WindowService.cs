using System.Windows;
using SupernovaLab.App.ViewModels;
using SupernovaLab.App.Windows;
using SupernovaLab.Core.Signals;

namespace SupernovaLab.App.Services;

/// <summary>
/// Opens the non-modal windows of spec §5.0, owned by the main window (so they close with it) and bound to its
/// DataContext (the shared MainViewModel). At most one of each; asking again activates it. The Instruments and Close-up windows'
/// sizes and positions are remembered for the session. The Close-up's DataContext is its own <see cref="CloseUpViewModel"/>,
/// made on the shared one, and it is given the app's one <see cref="Services.BeamClock"/> (plan ruling P3-5).
/// </summary>
public sealed class WindowService : IWindowService
{
    private readonly BeamClock _beamClock;
    private readonly Func<IPulseAudio> _audioFactory;
    private readonly Func<CloseUpViewModel, Window> _closeUpFactory;
    private Window? _owner;
    private InstrumentsWindow? _instruments;
    private OutcomeDetailsWindow? _details;
    private PresetInfoWindow? _presetInfo;
    private Window? _closeUp;
    private Rect? _instrumentsBounds, _closeUpBounds;

    /// <param name="beamClock">The beams' clock, shared by both 3D views; null for one of its own.</param>
    /// <param name="audioFactory">Makes the Close-up's pulse sound when it is first switched on (null: NAudio's); tests pass a fake.</param>
    /// <param name="closeUpFactory">Makes the Close-up window for its view model (null: <see cref="Windows.CloseUpWindow"/>); tests pass
    /// a plain window, so no GL context is made.</param>
    public WindowService(BeamClock? beamClock = null, Func<CloseUpViewModel, Window>? closeUpFactory = null,
        Func<IPulseAudio>? audioFactory = null)
    {
        _beamClock = beamClock ?? new BeamClock();
        _audioFactory = audioFactory ?? (() => new NAudioPulseAudio());
        _closeUpFactory = closeUpFactory ?? (vm => new CloseUpWindow { DataContext = vm, BeamClock = _beamClock });
    }

    /// <summary>The beams' clock this service hands to the Close-up.</summary>
    public BeamClock BeamClock => _beamClock;

    /// <summary>The main window: the owner of every window this service opens, and their DataContext.</summary>
    public void Attach(Window owner) => _owner = owner ?? throw new ArgumentNullException(nameof(owner));

    /// <summary>The open windows (null when closed), for the --snapshot diagnostic.</summary>
    public Window? Instruments => _instruments;
    public Window? OutcomeDetails => _details;
    public Window? PresetInfo => _presetInfo;
    public Window? CloseUp => _closeUp;

    public bool IsCloseUpOpen => _closeUp is not null;

    /// <summary>The open Close-up window, typed (null when closed, or when a test's factory made a plain window).</summary>
    public CloseUpWindow? CloseUpWindow => _closeUp as CloseUpWindow;

    /// <summary>How many Close-up windows this service has made (at most one is open at a time).</summary>
    public int CloseUpWindowsMade { get; private set; }

    /// <summary>The Instruments window's last focus request: the panel it enlarged, or null for the grid.</summary>
    public Messenger? LastInstrumentsFocus { get; private set; }

    /// <summary>The open Instruments window, typed (null when closed).</summary>
    public InstrumentsWindow? InstrumentsWindow => _instruments;

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
        // A tile enlarges its own panel; the View menu (no focus) shows all six.
        _instruments.ShowPanel(focus);
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

    /// <summary>
    /// Spec §2.2: opens the Close-up at most once (asking again brings it to the front), remembers its size and position for the
    /// session, and closes it with the main window (its owner). Its view model follows the shared one and is disposed when it closes.
    /// </summary>
    public void ShowCloseUp() => _closeUp = ShowOrActivate(_closeUp, () =>
    {
        var main = _owner?.DataContext as MainViewModel
                   ?? throw new InvalidOperationException("WindowService.Attach must be called with the main window first.");
        var vm = new CloseUpViewModel(main, _audioFactory);   // the sound stops and is released when the window closes (vm.Dispose)
        var w = _closeUpFactory(vm);
        w.DataContext = vm;
        CloseUpWindowsMade++;
        if (_closeUpBounds is { } b)
        {
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            (w.Left, w.Top, w.Width, w.Height) = (b.Left, b.Top, b.Width, b.Height);
        }
        w.Closing += (_, _) => _closeUpBounds = w.RestoreBounds;
        w.Closed += (_, _) =>
        {
            _closeUp = null;
            vm.Dispose();
        };
        return w;
    }, keepDataContext: true);

    private T ShowOrActivate<T>(T? existing, Func<T> create, bool keepDataContext = false) where T : Window
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
        if (!keepDataContext) window.DataContext = owner.DataContext;
        window.Show();
        return window;
    }
}
