using System.Runtime.InteropServices;
using Centerly.Core.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Centerly.Core.Hotkeys;

public enum HotkeyRegistrationStatus
{
    Registered,

    /// <summary>Another application (or Windows itself) already owns this combination.</summary>
    AlreadyInUse,

    Failed,
}

/// <summary>
/// Registers system-wide hotkeys via RegisterHotKey. Must be created and used on a thread that pumps messages
/// (the WPF UI thread); callbacks are raised on that thread.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    // MOD_NOREPEAT: holding the combination down fires once instead of auto-repeating.
    private const uint ModNoRepeat = 0x4000;

    private readonly MessageWindow _window;
    private readonly Dictionary<int, Action> _callbacks = [];
    private int _nextId = 1;

    public HotkeyManager()
    {
        _window = new MessageWindow("Centerly.Hotkeys", OnMessage, ex => CallbackFailed?.Invoke(this, ex));
    }

    /// <summary>Raised on the UI thread when a hotkey callback throws.</summary>
    public event EventHandler<Exception>? CallbackFailed;

    /// <summary>Registers the hotkey. On success <paramref name="id"/> identifies it for <see cref="Unregister"/>.</summary>
    public HotkeyRegistrationStatus Register(Hotkey hotkey, Action callback, out int id)
    {
        ObjectDisposedException.ThrowIf(_window.IsDisposed, this);
        ArgumentNullException.ThrowIfNull(callback);
        id = 0;
        if (hotkey.IsNone)
            return HotkeyRegistrationStatus.Failed;

        var candidate = _nextId++;
        var modifiers = (HOT_KEY_MODIFIERS)((uint)hotkey.Modifiers | ModNoRepeat);
        if (!PInvoke.RegisterHotKey(_window.Handle, candidate, modifiers, hotkey.VirtualKey))
        {
            return (WIN32_ERROR)Marshal.GetLastPInvokeError() == WIN32_ERROR.ERROR_HOTKEY_ALREADY_REGISTERED
                ? HotkeyRegistrationStatus.AlreadyInUse
                : HotkeyRegistrationStatus.Failed;
        }

        _callbacks[candidate] = callback;
        id = candidate;
        return HotkeyRegistrationStatus.Registered;
    }

    public void Unregister(int id)
    {
        if (_callbacks.Remove(id) && !_window.IsDisposed)
            PInvoke.UnregisterHotKey(_window.Handle, id);
    }

    public void UnregisterAll()
    {
        foreach (var id in _callbacks.Keys.ToArray())
            Unregister(id);
    }

    public void Dispose()
    {
        if (_window.IsDisposed)
            return;

        UnregisterAll();
        _window.Dispose();
    }

    private bool OnMessage(uint message, WPARAM wParam, LPARAM lParam)
    {
        if (message != PInvoke.WM_HOTKEY)
            return false;

        if (_callbacks.TryGetValue((int)wParam.Value, out var callback))
            callback();
        return true;
    }
}
