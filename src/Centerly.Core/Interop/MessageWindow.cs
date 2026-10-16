using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Centerly.Core.Interop;

/// <summary>Invisible message-only window (HWND_MESSAGE) used as a target for WM_HOTKEY and similar messages.</summary>
internal sealed unsafe class MessageWindow : IDisposable
{
    /// <summary>Returns true when the message was handled.</summary>
    public delegate bool MessageHandler(uint message, WPARAM wParam, LPARAM lParam);

    private static readonly HWND HwndMessage = new((void*)-3);

    private readonly MessageHandler _handler;
    private readonly Action<Exception> _onError;

    // Must stay referenced for the whole window lifetime, native code holds a pointer to it.
    private readonly WNDPROC _wndProc;
    private readonly string _className;

    /// <param name="name">Window name, also used as the class name prefix.</param>
    /// <param name="handler">Message handler, called on the creating thread.</param>
    /// <param name="onError">Receives exceptions thrown by <paramref name="handler"/>; they must not cross the native boundary.</param>
    public MessageWindow(string name, MessageHandler handler, Action<Exception> onError)
    {
        _handler = handler;
        _onError = onError;
        _wndProc = WndProc;
        _className = $"{name}.{Environment.ProcessId}.{Guid.NewGuid():N}";

        fixed (char* className = _className)
        {
            var windowClass = new WNDCLASSEXW
            {
                // Struct holds a managed delegate, so the native size is the marshaled one.
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = _wndProc,
                hInstance = PInvoke.GetModuleHandle((char*)null),
                lpszClassName = className,
            };
            if (PInvoke.RegisterClassEx(windowClass) == 0)
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "RegisterClassEx failed");
        }

        Handle = PInvoke.CreateWindowEx(0, _className, name, 0, 0, 0, 0, 0, HwndMessage, null, null, null);
        if (Handle.IsNull)
        {
            var error = Marshal.GetLastPInvokeError();
            PInvoke.UnregisterClass(_className, null);
            throw new Win32Exception(error, "CreateWindowEx failed");
        }
    }

    public HWND Handle { get; private set; }

    public bool IsDisposed => Handle.IsNull;

    public void Dispose()
    {
        if (Handle.IsNull)
            return;

        PInvoke.DestroyWindow(Handle);
        PInvoke.UnregisterClass(_className, null);
        Handle = HWND.Null;
    }

    private LRESULT WndProc(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            if (_handler(message, wParam, lParam))
                return new LRESULT(0);
        }
        catch (Exception ex)
        {
            _onError(ex);
            return new LRESULT(0);
        }

        return PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
    }
}
