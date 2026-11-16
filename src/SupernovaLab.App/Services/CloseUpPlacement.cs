using System.Windows;

namespace SupernovaLab.App.Services;

/// <summary>
/// Ruling R-P3-43 (Task 15, the owner's request): where the Close-up window first opens, so it does not cover the first changes in the main
/// view. A pure function of the screen's work area, the main window's bounds and the Close-up's size, all in DIP: the lower-right corner of
/// the work area of the screen that holds the main window, <see cref="MarginDip"/> in from both edges.
/// <para>WPF alone (no P/Invoke in the app, a build constraint) knows only the primary screen's work area
/// (<see cref="SystemParameters.WorkArea"/>). A main window whose centre is off it is on another screen, whose work area WPF cannot give;
/// the Close-up then goes to the lower right of the main window itself, the nearest WPF-only stand-in, held inside the virtual screen
/// (all monitors' bounding box, <see cref="SystemParameters.VirtualScreenLeft"/> and its kin) so it cannot open partly off every screen.</para>
/// </summary>
public static class CloseUpPlacement
{
    /// <summary>The gap from the work area's right and bottom edges, DIP.</summary>
    public const double MarginDip = 16;

    /// <summary>True when the main window's centre is on the primary screen's work area (or the main window is empty or unknown).</summary>
    public static bool OnPrimaryScreen(Rect primaryWorkArea, Rect mainWindow)
    {
        if (mainWindow.IsEmpty || !double.IsFinite(mainWindow.Width) || !double.IsFinite(mainWindow.Height)
            || !(mainWindow.Width > 0) || !(mainWindow.Height > 0))
            return true;
        var centre = new Point(mainWindow.Left + mainWindow.Width / 2, mainWindow.Top + mainWindow.Height / 2);
        return primaryWorkArea.Contains(centre);
    }

    /// <summary>
    /// The Close-up's top-left corner. A window larger than the area is held at the area's left or top edge, so its title bar stays on
    /// screen. An empty or unknown main window means the primary screen. On another screen (the main window's own lower right) the corner
    /// is then held inside <paramref name="virtualScreen"/>, unless that is empty (a --snapshot run, whose windows stay off the screen).
    /// </summary>
    public static Point LowerRight(Rect primaryWorkArea, Rect mainWindow, Size window, double margin = MarginDip, Rect? virtualScreen = null)
    {
        bool primary = OnPrimaryScreen(primaryWorkArea, mainWindow);
        Rect area = primary ? primaryWorkArea : mainWindow;
        double left = Math.Max(area.Left, area.Right - window.Width - margin);
        double top = Math.Max(area.Top, area.Bottom - window.Height - margin);
        if (!primary && virtualScreen is { IsEmpty: false } vs)
        {
            left = Math.Max(vs.Left, Math.Min(left, vs.Right - window.Width));
            top = Math.Max(vs.Top, Math.Min(top, vs.Bottom - window.Height));
        }
        return new Point(left, top);
    }
}
