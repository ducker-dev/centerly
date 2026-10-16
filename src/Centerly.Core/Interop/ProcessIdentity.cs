using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace Centerly.Core.Interop;

/// <summary>Which process a window belongs to, from the user's point of view.</summary>
/// <param name="ProcessId">Process id.</param>
/// <param name="StartTime">Process creation time (FILETIME ticks); together with the id identifies one launch.</param>
/// <param name="ImagePath">Full path of the executable, empty if it could not be read.</param>
internal readonly record struct ProcessIdentity(uint ProcessId, long StartTime, string ImagePath)
{
    private const string FrameHostName = "ApplicationFrameHost.exe";
    private const string CoreWindowClass = "Windows.UI.Core.CoreWindow";

    public string ProcessName => Path.GetFileName(ImagePath);

    public bool IsValid => ImagePath.Length > 0;

    /// <summary>
    /// Resolves the process of a top-level window. UWP apps are hosted by ApplicationFrameHost.exe; for them the real
    /// app is the owner of the child CoreWindow, which may not be attached yet right after the frame is shown
    /// (then null is returned and the caller should retry later).
    /// </summary>
    public static ProcessIdentity? FromWindow(HWND hwnd)
    {
        var identity = FromProcessId(GetProcessId(hwnd));
        if (identity is not { } frame || !frame.ProcessName.Equals(FrameHostName, StringComparison.OrdinalIgnoreCase))
            return identity;

        var hostedProcessId = FindHostedProcessId(hwnd, frame.ProcessId);
        return hostedProcessId == 0 ? null : FromProcessId(hostedProcessId);
    }

    public static unsafe uint GetProcessId(HWND hwnd)
    {
        uint processId = 0;
        _ = PInvoke.GetWindowThreadProcessId(hwnd, &processId);
        return processId;
    }

    public static ProcessIdentity? FromProcessId(uint processId)
    {
        if (processId == 0)
            return null;

        // Limited access works for elevated and protected processes too.
        using var process = PInvoke.OpenProcess_SafeHandle(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process.IsInvalid)
            return null;

        const int MaxPath = 32767;
        Span<char> buffer = new char[MaxPath];
        var length = (uint)buffer.Length;
        var path = PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref length)
            ? new string(buffer[..(int)length])
            : string.Empty;

        var startTime = PInvoke.GetProcessTimes(process, out var creation, out _, out _, out _)
            ? ((long)(uint)creation.dwHighDateTime << 32) | (uint)creation.dwLowDateTime
            : 0;

        return new ProcessIdentity(processId, startTime, path);
    }

    private static uint FindHostedProcessId(HWND frame, uint frameProcessId)
    {
        uint found = 0;
        PInvoke.EnumChildWindows(frame, (child, _) =>
        {
            if (Win32Window.GetClassName(child) != CoreWindowClass)
                return true;

            var pid = GetProcessId(child);
            if (pid == frameProcessId)
                return true;

            found = pid;
            return false;
        }, 0);
        return found;
    }
}
