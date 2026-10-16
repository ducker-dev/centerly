using System.Runtime.InteropServices;
using Centerly.Core.Geometry;
using Centerly.Core.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Centerly.Core.Centering;

public enum MonitorDirection
{
    Next,
    Previous,
}

/// <summary>Centers top-level windows inside the work area of the monitor they are on.</summary>
/// <remarks>
/// SetWindowPos sends messages synchronously to the target thread, so call this off the UI thread:
/// a window that hangs mid-call must not freeze our own UI.
/// </remarks>
/// <param name="history">Records every move so it can be undone; shared between centerers.</param>
public sealed class WindowCenterer(WindowPositionHistory? history = null)
{
    // Shell surfaces that can be foreground but must never be moved.
    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland",
        "XamlExplorerHostIslandWindow",
        "Windows.UI.Core.CoreWindow",
        "MultitaskingViewFrame",
        "ForegroundStaging",
    };

    public CenterResult CenterForeground(OversizeBehavior oversize) =>
        Center(PInvoke.GetForegroundWindow(), oversize, direction: null);

    /// <summary>
    /// Moves the foreground window to the next or previous monitor (in left-to-right order, wrapping) and centers it
    /// there. A maximized window stays maximized on the new monitor.
    /// </summary>
    public CenterResult MoveForegroundToMonitor(MonitorDirection direction, OversizeBehavior oversize) =>
        Center(PInvoke.GetForegroundWindow(), oversize, direction);

    public CenterResult Center(nint handle, OversizeBehavior oversize) =>
        Center((HWND)handle, oversize, direction: null);

    /// <summary>
    /// Puts the foreground window back where it was before Centerly last moved it. Undoing again redoes the move.
    /// </summary>
    public CenterResult UndoForeground()
    {
        var hwnd = PInvoke.GetForegroundWindow();
        if (hwnd.IsNull || !PInvoke.IsWindow(hwnd))
            return new(CenterOutcome.NoWindow);

        hwnd = PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT);
        var info = Win32Window.Describe(hwnd);
        var key = Win32Window.ToKey(hwnd);

        if (history is null || !history.TryGet(key, out var saved))
            return new(CenterOutcome.NothingToUndo, null, info);

        // Maximized-ness must match: restoring a normal rect onto a maximized window (or the other way round) would
        // fight the user, who changed the window state on purpose since our move.
        var isMaximized = PInvoke.IsZoomed(hwnd);
        if (GetIneligibilityReason(hwnd, allowMaximized: saved.IsMaximized) is { } reason)
            return new(CenterOutcome.NotEligible, reason, info);
        if (saved.IsMaximized && !isMaximized)
            return new(CenterOutcome.NotEligible, "no longer maximized", info);

        if (PInvoke.IsHungAppWindow(hwnd))
            return new(CenterOutcome.NotResponding, null, info);

        if (!Win32Window.TryGetGeometry(hwnd, out var geometry))
            return new(CenterOutcome.Failed, "cannot read window geometry", info);

        var current = geometry.WindowRect;
        if (saved.IsMaximized)
        {
            if (MonitorMath.Containing(Win32Monitors.GetAll(), saved.Rect) is not { } monitor)
                return new(CenterOutcome.Failed, "no monitors", info);

            if (monitor.WorkArea != geometry.WorkArea &&
                !Win32Window.TryMoveMaximized(hwnd, geometry.WorkArea, monitor.WorkArea,
                    normal => CenterIn(normal, monitor.WorkArea, OversizeBehavior.ShrinkToFit), out var error))
            {
                return Fail(error, info);
            }
        }
        else
        {
            if (!Move(hwnd, current, saved.Rect, out var error))
                return Fail(error, info);

            // Landing on a monitor with another DPI makes the window rescale itself; put it back to the exact rect.
            if (Win32Window.TryGetWindowRect(hwnd, out var after) && after != saved.Rect && !Move(hwnd, after, saved.Rect, out error))
                return Fail(error, info);
        }

        history.Remember(key, new SavedPosition(current, isMaximized));
        return new(CenterOutcome.Centered, "restored", info);
    }

    private CenterResult Center(HWND hwnd, OversizeBehavior oversize, MonitorDirection? direction)
    {
        if (hwnd.IsNull || !PInvoke.IsWindow(hwnd))
            return new(CenterOutcome.NoWindow);

        hwnd = PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT);
        var info = Win32Window.Describe(hwnd);

        if (GetIneligibilityReason(hwnd, allowMaximized: direction is not null) is { } reason)
            return new(CenterOutcome.NotEligible, reason, info);

        // Do not even try to talk to a frozen window, SetWindowPos would block until it recovers.
        if (PInvoke.IsHungAppWindow(hwnd))
            return new(CenterOutcome.NotResponding, null, info);

        if (!Win32Window.TryGetGeometry(hwnd, out var geometry))
            return new(CenterOutcome.Failed, "cannot read window geometry", info);

        var isMaximized = PInvoke.IsZoomed(hwnd);
        if (geometry.VisibleRect == geometry.MonitorRect && !Win32Window.HasCaption(hwnd))
            return new(CenterOutcome.NotEligible, "fullscreen", info);

        var current = new MonitorArea(geometry.MonitorRect, geometry.WorkArea);
        var monitor = direction switch
        {
            MonitorDirection.Next => MonitorMath.Next(Win32Monitors.GetAll(), current),
            MonitorDirection.Previous => MonitorMath.Previous(Win32Monitors.GetAll(), current),
            _ => current,
        };

        if (isMaximized)
            return MoveMaximized(hwnd, geometry.WindowRect, current, monitor, oversize, info);

        var target = CenterIn(geometry.WindowRect, geometry.VisibleRect, monitor.WorkArea, oversize);
        if (target == geometry.WindowRect)
            return new(CenterOutcome.AlreadyCentered, null, info);

        if (!Move(hwnd, geometry.WindowRect, target, out var error))
            return Fail(error, info);

        history?.Remember(Win32Window.ToKey(hwnd), new SavedPosition(geometry.WindowRect, IsMaximized: false));

        // Crossing monitors with different DPI makes the window rescale itself after the move, recenter once more.
        if (Win32Window.TryGetGeometry(hwnd, out var after) &&
            (after.VisibleRect.Width != geometry.VisibleRect.Width || after.VisibleRect.Height != geometry.VisibleRect.Height))
        {
            var corrected = CenterIn(after.WindowRect, after.VisibleRect, after.WorkArea, oversize);
            if (corrected != after.WindowRect && !Move(hwnd, after.WindowRect, corrected, out error))
                return Fail(error, info);
        }

        return new(CenterOutcome.Centered, null, info);
    }

    private CenterResult MoveMaximized(HWND hwnd, RectI windowRect, MonitorArea from, MonitorArea to, OversizeBehavior oversize, string info)
    {
        if (to.Bounds == from.Bounds)
            return new(CenterOutcome.AlreadyCentered, "single monitor", info);

        // Its restore rect is centered on the new monitor too, so un-maximizing later does not jump back.
        if (!Win32Window.TryMoveMaximized(hwnd, from.WorkArea, to.WorkArea, normal => CenterIn(normal, to.WorkArea, oversize), out var error))
            return Fail(error, info);

        history?.Remember(Win32Window.ToKey(hwnd), new SavedPosition(windowRect, IsMaximized: true));
        return new(CenterOutcome.Centered, "maximized", info);
    }

    // The restore rect has no invisible borders to account for, it is used as both window and visible rect.
    private static RectI CenterIn(RectI rect, RectI workArea, OversizeBehavior oversize) =>
        CenteringMath.ComputeTarget(rect, rect, workArea, oversize);

    private static RectI CenterIn(RectI windowRect, RectI visibleRect, RectI workArea, OversizeBehavior oversize) =>
        CenteringMath.ComputeTarget(windowRect, visibleRect, workArea, oversize);

    private static string? GetIneligibilityReason(HWND hwnd, bool allowMaximized)
    {
        if (hwnd == PInvoke.GetShellWindow() || hwnd == PInvoke.GetDesktopWindow())
            return "desktop";

        var className = Win32Window.GetClassName(hwnd);
        if (ShellClasses.Contains(className))
            return $"shell window ({className})";

        if (!PInvoke.IsWindowVisible(hwnd) || Win32Window.IsCloaked(hwnd))
            return "hidden";

        if (PInvoke.IsIconic(hwnd))
            return "minimized";

        if (!allowMaximized && PInvoke.IsZoomed(hwnd))
            return "maximized";

        if (Win32Window.IsChild(hwnd))
            return "child window";

        return null;
    }

    private static bool Move(HWND hwnd, RectI current, RectI target, out WIN32_ERROR error)
    {
        var flags = SET_WINDOW_POS_FLAGS.SWP_NOZORDER
                    | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER
                    | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
        if (target.Width == current.Width && target.Height == current.Height)
            flags |= SET_WINDOW_POS_FLAGS.SWP_NOSIZE;

        if (PInvoke.SetWindowPos(hwnd, HWND.Null, target.Left, target.Top, target.Width, target.Height, flags))
        {
            error = WIN32_ERROR.NO_ERROR;
            return true;
        }

        error = (WIN32_ERROR)Marshal.GetLastPInvokeError();
        return false;
    }

    private static CenterResult Fail(WIN32_ERROR error, string info) =>
        error == WIN32_ERROR.ERROR_ACCESS_DENIED
            ? new(CenterOutcome.AccessDenied, "window belongs to an elevated process", info)
            : new(CenterOutcome.Failed, $"moving the window failed: {error}", info);
}
