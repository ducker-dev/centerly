using Centerly.Core.Geometry;
using Centerly.Core.Interop;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Centerly.Core.AutoCenter;

/// <param name="App">Application owning the window.</param>
/// <param name="VisibleRect">Visible frame of the window in physical pixels, for highlighting.</param>
public sealed record PickedWindow(RunningApp App, RectI VisibleRect);

/// <summary>Finds the application window under the mouse cursor ("drag the crosshair onto a window").</summary>
public static class WindowPicker
{
    /// <summary>Top-level window under the cursor, or null for the desktop, the taskbar and our own windows.</summary>
    public static PickedWindow? AtCursor()
    {
        if (!PInvoke.GetCursorPos(out var point))
            return null;

        var hwnd = PInvoke.WindowFromPoint(point);
        if (hwnd.IsNull)
            return null;

        hwnd = PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT);
        if (hwnd == PInvoke.GetShellWindow() || hwnd == PInvoke.GetDesktopWindow())
            return null;

        if (ProcessIdentity.FromWindow(hwnd) is not { IsValid: true } process || process.ProcessId == Environment.ProcessId)
            return null;

        // The taskbar and desktop belong to explorer.exe, but so do File Explorer windows; filter by class instead.
        var className = Win32Window.GetClassName(hwnd);
        if (className is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW")
            return null;

        if (!Win32Window.TryGetGeometry(hwnd, out var geometry))
            return null;

        return new PickedWindow(
            new RunningApp(process.ProcessName, process.ImagePath, Win32Window.GetTitle(hwnd)),
            geometry.VisibleRect);
    }
}
