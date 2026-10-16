using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Centerly.Core.Interop;

/// <summary>
/// Out-of-context SetWinEventHook: no DLL injection, events are delivered to the creating thread's message loop.
/// Only window-level events (OBJID_WINDOW, CHILDID_SELF) are passed through.
/// </summary>
internal sealed class WinEventHook : IDisposable
{
    public delegate void WindowEventHandler(uint eventType, HWND hwnd);

    private const int ObjIdWindow = 0; // OBJID_WINDOW

    private readonly WindowEventHandler _handler;
    private readonly Action<Exception> _onError;

    // Must stay referenced while the hook is installed, native code holds a pointer to it.
    private readonly WINEVENTPROC _proc;
    private HWINEVENTHOOK _hook;

    public WinEventHook(uint eventMin, uint eventMax, WindowEventHandler handler, Action<Exception> onError)
    {
        _handler = handler;
        _onError = onError;
        _proc = OnEvent;
        _hook = PInvoke.SetWinEventHook(eventMin, eventMax, HMODULE.Null, _proc, 0, 0,
            PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS);
        if (_hook.IsNull)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "SetWinEventHook failed");
    }

    public void Dispose()
    {
        if (_hook.IsNull)
            return;

        PInvoke.UnhookWinEvent(_hook);
        _hook = HWINEVENTHOOK.Null;
    }

    private void OnEvent(HWINEVENTHOOK hook, uint eventType, HWND hwnd, int idObject, int idChild, uint eventThread, uint eventTime)
    {
        if (hwnd.IsNull || idObject != ObjIdWindow || idChild != (int)PInvoke.CHILDID_SELF)
            return;

        try
        {
            _handler(eventType, hwnd);
        }
        catch (Exception ex)
        {
            // Exceptions must not cross the native boundary.
            _onError(ex);
        }
    }
}
