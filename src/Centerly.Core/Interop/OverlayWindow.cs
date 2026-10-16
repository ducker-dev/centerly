using Centerly.Core.Geometry;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Centerly.Core.Interop;

/// <summary>Helpers for a highlight overlay window that must never take focus or mouse input.</summary>
public static class OverlayWindow
{
    /// <summary>Makes the window click-through, non-activating and hidden from Alt+Tab.</summary>
    public static void MakeClickThrough(nint handle)
    {
        var hwnd = (HWND)handle;
        var exStyle = (WINDOW_EX_STYLE)(uint)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        exStyle |= WINDOW_EX_STYLE.WS_EX_TRANSPARENT | WINDOW_EX_STYLE.WS_EX_LAYERED
                   | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
        _ = PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)exStyle);
    }

    /// <summary>Places the window topmost at <paramref name="bounds"/> (physical pixels) without activating it.</summary>
    public static void Show(nint handle, RectI bounds) =>
        PInvoke.SetWindowPos((HWND)handle, HWND.HWND_TOPMOST, bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
}
