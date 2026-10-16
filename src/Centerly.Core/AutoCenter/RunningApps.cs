using Centerly.Core.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Centerly.Core.AutoCenter;

/// <param name="ProcessName">Executable file name, e.g. "notepad.exe".</param>
/// <param name="ExecutablePath">Full path to the executable.</param>
/// <param name="WindowTitle">Title of one of its windows.</param>
public sealed record RunningApp(string ProcessName, string ExecutablePath, string WindowTitle);

public static class RunningApps
{
    /// <summary>Applications that currently have a visible main window (roughly what Alt+Tab shows), one entry per executable.</summary>
    public static IReadOnlyList<RunningApp> GetWithWindows()
    {
        var ownProcessId = (uint)Environment.ProcessId;
        var apps = new Dictionary<string, RunningApp>(StringComparer.OrdinalIgnoreCase);

        PInvoke.EnumWindows((hwnd, _) =>
        {
            if (!Win32Window.IsAppWindow(hwnd, out var isOwned) || isOwned)
                return true;

            var title = Win32Window.GetTitle(hwnd);
            if (title.Length == 0)
                return true;

            if (ProcessIdentity.FromWindow(hwnd) is { IsValid: true } process
                && process.ProcessId != ownProcessId
                && !apps.ContainsKey(process.ImagePath))
            {
                apps[process.ImagePath] = new RunningApp(process.ProcessName, process.ImagePath, title);
            }

            return true;
        }, (LPARAM)0);

        return apps.Values.OrderBy(a => a.ProcessName, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
