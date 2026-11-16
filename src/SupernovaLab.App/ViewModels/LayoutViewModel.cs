using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SupernovaLab.App.Services;

namespace SupernovaLab.App.ViewModels;

/// <summary>
/// Which panels are open (Task 15): the lab panel (left) and the outcome column (right) each fold to a thin edge strip; the Focus view
/// hides both and the instrument strip, leaving the 3D view and the timeline; the 3D view's colour key, notes and the tour caption fold too.
/// Focus view is separate from the folds, so turning it off brings back exactly what was open before. Every change is saved to the
/// <see cref="LayoutStore"/> (when there is one), and the saved state is read at startup.
/// </summary>
public sealed partial class LayoutViewModel : ObservableObject
{
    /// <summary>The width (DIP) of a folded column's strip, and of the fold button's strip beside an open one.</summary>
    public const double StripWidth = 20;

    /// <summary>The width (DIP) of the lab panel and of the outcome column when open.</summary>
    public const double PanelWidth = 280;

    /// <summary>The least time between two writes of layout.json; changes inside it are coalesced into one later write.</summary>
    public static readonly TimeSpan SaveInterval = TimeSpan.FromMilliseconds(500);

    private readonly LayoutStore? _store;
    private readonly Func<TimeSpan> _clock;
    private readonly object _saveLock = new();
    private System.Threading.Timer? _saveTimer;
    private LayoutSettings? _pending;
    private TimeSpan _lastSave = TimeSpan.MinValue;
    private bool _loading;

    /// <param name="clock">Monotonic time, for the write interval (tests pass a fake one); null = the system's.</param>
    public LayoutViewModel(LayoutStore? store = null, LayoutSettings? settings = null, Func<TimeSpan>? clock = null)
    {
        _store = store;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        _clock = clock ?? (() => watch.Elapsed);
        Apply(settings ?? store?.Load() ?? new LayoutSettings());
        ToggleLabCommand = new RelayCommand(() => IsLabFolded = !IsLabFolded);
        ToggleOutcomeCommand = new RelayCommand(() => IsOutcomeFolded = !IsOutcomeFolded);
        ToggleFocusCommand = new RelayCommand(() => IsFocusView = !IsFocusView);
        ToggleKeyCommand = new RelayCommand(() => IsKeyFolded = !IsKeyFolded);
    }

    public IRelayCommand ToggleLabCommand { get; }
    public IRelayCommand ToggleOutcomeCommand { get; }
    public IRelayCommand ToggleFocusCommand { get; }
    public IRelayCommand ToggleKeyCommand { get; }

    [ObservableProperty] private bool _isLabFolded;
    [ObservableProperty] private bool _isOutcomeFolded;
    [ObservableProperty] private bool _isFocusView;
    [ObservableProperty] private bool _isKeyFolded;
    [ObservableProperty] private bool _isNotesExpanded;
    [ObservableProperty] private bool _isCaptionCollapsed;

    /// <summary>The Close-up's light-bending detail (Task 4b, ruling R-P3-9); the Close-up's view model reads and sets it here.</summary>
    [ObservableProperty] private SupernovaLab.Rendering.LensingDetail _closeUpLensingDetail;

    public LayoutSettings Settings => new()
    {
        LabPanelFolded = IsLabFolded, OutcomeColumnFolded = IsOutcomeFolded, FocusView = IsFocusView, KeyFolded = IsKeyFolded,
        NotesExpanded = IsNotesExpanded, CaptionCollapsed = IsCaptionCollapsed, CloseUpLensingDetail = CloseUpLensingDetail,
    };

    /// <summary>Sets every state from settings (the saved file, the snapshot tool's <c>--layout</c>); nothing is saved.</summary>
    public void Apply(LayoutSettings s)
    {
        _loading = true;
        try
        {
            IsLabFolded = s.LabPanelFolded;
            IsOutcomeFolded = s.OutcomeColumnFolded;
            IsFocusView = s.FocusView;
            IsKeyFolded = s.KeyFolded;
            IsNotesExpanded = s.NotesExpanded;
            IsCaptionCollapsed = s.CaptionCollapsed;
            CloseUpLensingDetail = s.CloseUpLensingDetail;
        }
        finally
        {
            _loading = false;
        }
    }

    // ---- What the window shows ----

    /// <summary>The lab panel's column: 0 in the Focus view, a thin strip when folded, else the panel and its fold strip.</summary>
    public GridLength LabColumnWidth => new(IsFocusView ? 0 : IsLabFolded ? StripWidth : PanelWidth + StripWidth);

    public GridLength OutcomeColumnWidth => new(IsFocusView ? 0 : IsOutcomeFolded ? StripWidth : PanelWidth + StripWidth);

    public bool ShowLabPanel => !IsFocusView && !IsLabFolded;
    public bool ShowOutcomeColumn => !IsFocusView && !IsOutcomeFolded;
    public bool ShowInstrumentStrip => !IsFocusView;

    /// <summary>The fold button's arrow: it points the way the column will go.</summary>
    public string LabToggleText => IsLabFolded ? "▸" : "◂";
    public string OutcomeToggleText => IsOutcomeFolded ? "◂" : "▸";

    public string LabToggleToolTip => IsLabFolded ? "Show the lab panel" : "Hide the lab panel";
    public string OutcomeToggleToolTip => IsOutcomeFolded ? "Show the outcome column" : "Hide the outcome column";

    /// <summary>The Focus view button's text and tooltip (the shortcut is shown in both).</summary>
    public string FocusToggleText => IsFocusView ? "Exit focus (F)" : "Focus view (F)";

    public string FocusToggleToolTip => IsFocusView
        ? "Bring back the panels and the instrument strip (shortcut: F)"
        : "Hide the lab panel, the outcome column and the instrument strip, leaving the 3D view and the timeline (shortcut: F)";

    /// <summary>The key's folded tab, "Key" with a right arrow, and its open header, with a down arrow.</summary>
    public string KeyToggleText => IsKeyFolded ? "Key ▸" : "Key ▾";

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        switch (e.PropertyName)
        {
            case nameof(IsLabFolded):
                Raise(nameof(LabColumnWidth), nameof(ShowLabPanel), nameof(LabToggleText), nameof(LabToggleToolTip));
                break;
            case nameof(IsOutcomeFolded):
                Raise(nameof(OutcomeColumnWidth), nameof(ShowOutcomeColumn), nameof(OutcomeToggleText), nameof(OutcomeToggleToolTip));
                break;
            case nameof(IsFocusView):
                Raise(nameof(LabColumnWidth), nameof(OutcomeColumnWidth), nameof(ShowLabPanel), nameof(ShowOutcomeColumn),
                    nameof(ShowInstrumentStrip), nameof(FocusToggleText), nameof(FocusToggleToolTip));
                break;
            case nameof(IsKeyFolded):
                Raise(nameof(KeyToggleText));
                break;
        }
        if (!_loading && (e.PropertyName is nameof(IsLabFolded) or nameof(IsOutcomeFolded) or nameof(IsFocusView) or nameof(IsKeyFolded)
                or nameof(IsNotesExpanded) or nameof(IsCaptionCollapsed) or nameof(CloseUpLensingDetail)))
            RequestSave();
    }

    /// <summary>
    /// Saves the layout now, unless one was written less than <see cref="SaveInterval"/> ago: then the newest state waits for a one-shot
    /// timer that writes it (off the UI thread) when the interval is over. <see cref="Flush"/> writes what is waiting.
    /// </summary>
    private void RequestSave()
    {
        if (_store is null) return;
        var settings = Settings;
        lock (_saveLock)
        {
            var wait = _lastSave == TimeSpan.MinValue ? TimeSpan.Zero : _lastSave + SaveInterval - _clock();
            if (wait <= TimeSpan.Zero)
            {
                _pending = null;
                _saveTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                _lastSave = _clock();
                _store.Save(settings);
                return;
            }
            _pending = settings;
            _saveTimer ??= new System.Threading.Timer(_ => Flush(), null, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            _saveTimer.Change(wait, System.Threading.Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Writes any change still waiting (the window calls this on close) and stops the timer.</summary>
    public void Flush()
    {
        lock (_saveLock)
        {
            _saveTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            if (_pending is { } p)
            {
                _pending = null;
                _lastSave = _clock();
                _store?.Save(p);
            }
        }
    }

    private void Raise(params string[] names)
    {
        foreach (string n in names) base.OnPropertyChanged(new PropertyChangedEventArgs(n));
    }

    /// <summary>
    /// Whether the F key should toggle the Focus view while this element has the keyboard focus: not while a text box, combo box
    /// (or its open drop-down list, whose focus is a ComboBoxItem inside a popup), password box or menu item has it.
    /// </summary>
    public static bool FocusShortcutApplies(object? focused)
    {
        if (focused is TextBoxBase or ComboBox or ComboBoxItem or PasswordBox or MenuItem) return false;
        for (var d = focused as DependencyObject; d is not null; d = ParentOf(d))
            if (d is ComboBox or ComboBoxItem or Popup or MenuItem or ContextMenu || d.GetType().Name == "PopupRoot") return false;
        return true;
    }

    private static DependencyObject? ParentOf(DependencyObject d)
        => d is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
            ? System.Windows.Media.VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d)
            : LogicalTreeHelper.GetParent(d);
}
