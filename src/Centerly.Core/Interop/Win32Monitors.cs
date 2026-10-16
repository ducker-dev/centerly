using Centerly.Core.Geometry;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Centerly.Core.Interop;

internal static unsafe class Win32Monitors
{
    public static IReadOnlyList<MonitorArea> GetAll()
    {
        var monitors = new List<MonitorArea>();
        PInvoke.EnumDisplayMonitors(HDC.Null, (RECT?)null, (monitor, _, _, _) =>
        {
            var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
            if (PInvoke.GetMonitorInfo(monitor, ref info))
            {
                monitors.Add(new MonitorArea(
                    new RectI(info.rcMonitor.left, info.rcMonitor.top, info.rcMonitor.right, info.rcMonitor.bottom),
                    new RectI(info.rcWork.left, info.rcWork.top, info.rcWork.right, info.rcWork.bottom)));
            }

            return true;
        }, (LPARAM)0);
        return monitors;
    }
}
