using System.Runtime.InteropServices;
using Centerly.Core.Geometry;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Centerly.Core.Interop;

internal readonly record struct WindowGeometry(RectI WindowRect, RectI VisibleRect, RectI WorkArea, RectI MonitorRect);

/// <summary>Thin helpers over user32/dwmapi for a single window.</summary>
internal static unsafe class Win32Window
{
    public static bool TryGetGeometry(HWND hwnd, out WindowGeometry geometry)
    {
        geometry = default;
        if (!PInvoke.GetWindowRect(hwnd, out var windowRect))
            return false;

        // Win10/11 windows have ~7px invisible resize borders; the DWM frame is what the user actually sees.
        RECT visibleRect;
        if (PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &visibleRect, (uint)sizeof(RECT)).Failed)
            visibleRect = windowRect;

        var monitor = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        if (!PInvoke.GetMonitorInfo(monitor, ref monitorInfo))
            return false;

        geometry = new WindowGeometry(
            ToRect(windowRect),
            ToRect(visibleRect),
            ToRect(monitorInfo.rcWork),
            ToRect(monitorInfo.rcMonitor));
        return true;
    }

    public static string GetClassName(HWND hwnd)
    {
        const int MaxClassName = 256;
        var buffer = stackalloc char[MaxClassName];
        var length = PInvoke.GetClassName(hwnd, buffer, MaxClassName);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    public static bool IsCloaked(HWND hwnd)
    {
        // Cloaked = present but not shown: windows on other virtual desktops, suspended UWP frames, etc.
        uint cloaked = 0;
        return PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint)).Succeeded
               && cloaked != 0;
    }

    /// <summary>
    /// Whether the window looks like a real application window a user would want centered: visible top-level window
    /// with a frame, not a tool palette, popup menu, tooltip or splash screen.
    /// </summary>
    public static bool IsAppWindow(HWND hwnd, out bool isOwned)
    {
        isOwned = false;
        if (PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT) != hwnd || !PInvoke.IsWindowVisible(hwnd) || IsCloaked(hwnd))
            return false;

        var style = Style(hwnd);
        if ((style & WINDOW_STYLE.WS_CHILD) != 0)
            return false;

        // Splash screens and popups are typically WS_POPUP without caption or sizing border.
        if ((style & WINDOW_STYLE.WS_CAPTION) != WINDOW_STYLE.WS_CAPTION && (style & WINDOW_STYLE.WS_THICKFRAME) == 0)
            return false;

        var exStyle = ExStyle(hwnd);
        if ((exStyle & (WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE)) != 0)
            return false;

        isOwned = !PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_OWNER).IsNull;
        return true;
    }

    public static bool TryGetWindowRect(HWND hwnd, out RectI rect)
    {
        rect = default;
        if (!PInvoke.GetWindowRect(hwnd, out var r))
            return false;

        rect = ToRect(r);
        return true;
    }

    public static string GetTitle(HWND hwnd)
    {
        var length = PInvoke.GetWindowTextLength(hwnd);
        if (length <= 0)
            return string.Empty;

        var buffer = new char[length + 1];
        fixed (char* p = buffer)
        {
            var copied = PInvoke.GetWindowText(hwnd, p, buffer.Length);
            return new string(p, 0, copied);
        }
    }

    /// <summary>
    /// Moves a maximized window from the monitor with <paramref name="fromWorkArea"/> to the one with
    /// <paramref name="toWorkArea"/>, keeping it maximized. Its restore rectangle becomes
    /// <paramref name="normalTarget"/> applied to the current one, so un-maximizing leaves it on the new monitor.
    /// </summary>
    /// <remarks>
    /// Rewriting the restore rectangle alone does not move a window that is already maximized, and restoring then
    /// maximizing again would animate and flicker. So the maximized window is moved directly, keeping the same
    /// overhang beyond the work area (its invisible borders), and then re-maximized in place so the app lays itself
    /// out for the new monitor.
    /// </remarks>
    public static bool TryMoveMaximized(HWND hwnd, RectI fromWorkArea, RectI toWorkArea, Func<RectI, RectI> normalTarget, out WIN32_ERROR error)
    {
        var placement = new WINDOWPLACEMENT { length = (uint)sizeof(WINDOWPLACEMENT) };
        if (!PInvoke.GetWindowPlacement(hwnd, ref placement) || !PInvoke.GetWindowRect(hwnd, out var windowRect))
            return Failed(out error);

        var (dx, dy) = WorkspaceOffset(hwnd);
        var normal = ToRect(placement.rcNormalPosition);
        var target = normalTarget(new RectI(normal.Left + dx, normal.Top + dy, normal.Right + dx, normal.Bottom + dy));
        placement.rcNormalPosition = new RECT(target.Left - dx, target.Top - dy, target.Right - dx, target.Bottom - dy);
        placement.showCmd = SHOW_WINDOW_CMD.SW_SHOWMAXIMIZED;
        if (!PInvoke.SetWindowPlacement(hwnd, in placement))
            return Failed(out error);

        var current = ToRect(windowRect);
        var maximized = new RectI(
            toWorkArea.Left - (fromWorkArea.Left - current.Left),
            toWorkArea.Top - (fromWorkArea.Top - current.Top),
            toWorkArea.Right + (current.Right - fromWorkArea.Right),
            toWorkArea.Bottom + (current.Bottom - fromWorkArea.Bottom));
        const SET_WINDOW_POS_FLAGS Flags = SET_WINDOW_POS_FLAGS.SWP_NOZORDER
                                           | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER
                                           | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
        if (!PInvoke.SetWindowPos(hwnd, HWND.Null, maximized.Left, maximized.Top, maximized.Width, maximized.Height, Flags))
            return Failed(out error);

        // Now on the new monitor: let Windows recompute the maximized rect there (DPI, taskbar position).
        if (!PInvoke.SetWindowPlacement(hwnd, in placement))
            return Failed(out error);

        error = WIN32_ERROR.NO_ERROR;
        return true;
    }

    private static bool Failed(out WIN32_ERROR error)
    {
        error = (WIN32_ERROR)Marshal.GetLastPInvokeError();
        return false;
    }

    public static bool IsChild(HWND hwnd) =>
        (Style(hwnd) & WINDOW_STYLE.WS_CHILD) != 0;

    public static bool HasCaption(HWND hwnd) =>
        (Style(hwnd) & WINDOW_STYLE.WS_CAPTION) == WINDOW_STYLE.WS_CAPTION;

    /// <summary>"process.exe (ClassName) 0x1234" for logs.</summary>
    public static string Describe(HWND hwnd)
    {
        var name = ProcessIdentity.FromWindow(hwnd) is { IsValid: true } identity
            ? identity.ProcessName
            : $"pid {ProcessIdentity.GetProcessId(hwnd)}";
        return $"{name} ({GetClassName(hwnd)}) 0x{(nint)hwnd.Value:X}";
    }

    /// <summary>Handle as a plain number, for use as a dictionary key outside unsafe code.</summary>
    public static nint ToKey(HWND hwnd) => (nint)hwnd.Value;

    private static WINDOW_STYLE Style(HWND hwnd) =>
        (WINDOW_STYLE)(uint)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);

    private static WINDOW_EX_STYLE ExStyle(HWND hwnd) =>
        (WINDOW_EX_STYLE)(uint)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

    /// <summary>
    /// WINDOWPLACEMENT uses workspace coordinates for regular top-level windows: relative to the primary monitor's
    /// work area, which differs from screen coordinates when the taskbar is docked at the top or left.
    /// </summary>
    private static (int X, int Y) WorkspaceOffset(HWND hwnd)
    {
        if ((ExStyle(hwnd) & WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) != 0)
            return (0, 0);

        var primary = PInvoke.MonitorFromPoint(default, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        return PInvoke.GetMonitorInfo(primary, ref info)
            ? (info.rcWork.left - info.rcMonitor.left, info.rcWork.top - info.rcMonitor.top)
            : (0, 0);
    }

    private static RectI ToRect(RECT rect) => new(rect.left, rect.top, rect.right, rect.bottom);
}
