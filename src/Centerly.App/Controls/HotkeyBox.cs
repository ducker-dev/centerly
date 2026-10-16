using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Centerly.App.Resources;
using Centerly.Core.Hotkeys;

namespace Centerly.App.Controls;

/// <summary>
/// Read-only text box that records a key combination when focused.
/// Esc cancels, Backspace/Delete without modifiers clears the hotkey.
/// </summary>
public sealed class HotkeyBox : TextBox
{
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey), typeof(Hotkey), typeof(HotkeyBox),
        new FrameworkPropertyMetadata(Hotkey.None, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((HotkeyBox)d).UpdateText()));

    public static readonly DependencyProperty IsRecordingProperty = DependencyProperty.Register(
        nameof(IsRecording), typeof(bool), typeof(HotkeyBox),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public HotkeyBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsUndoEnabled = false;
        Cursor = Cursors.Arrow;
        ContextMenu = null;
        InputMethod.SetIsInputMethodEnabled(this, false);
        UpdateText();
    }

    public Hotkey Hotkey
    {
        get => (Hotkey)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    /// <summary>True while the box has keyboard focus and waits for a combination.</summary>
    public bool IsRecording
    {
        get => (bool)GetValue(IsRecordingProperty);
        set => SetValue(IsRecordingProperty, value);
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        IsRecording = true;
        UpdateText();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        IsRecording = false;
        UpdateText();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };
        // WPF ModifierKeys values match the MOD_* flags of RegisterHotKey.
        var modifiers = (HotkeyModifiers)Keyboard.Modifiers;

        if (modifiers == HotkeyModifiers.None && key == Key.Tab)
            return; // keep keyboard navigation working

        e.Handled = true;

        if (IsModifierKey(key))
        {
            Text = modifiers == HotkeyModifiers.None ? Strings.HotkeyBoxPrompt : $"{Hotkey.FormatModifiers(modifiers)} + …";
            return;
        }

        if (modifiers == HotkeyModifiers.None)
        {
            switch (key)
            {
                case Key.Escape:
                    StopRecording();
                    return;
                case Key.Back or Key.Delete:
                    Hotkey = Hotkey.None;
                    StopRecording();
                    return;
            }
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0)
            return;

        Hotkey = new Hotkey(modifiers, (uint)virtualKey);
        StopRecording();
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        e.Handled = true;
        // All modifiers released without a main key: back to the prompt.
        if (IsKeyboardFocused && Keyboard.Modifiers == ModifierKeys.None)
            UpdateText();
    }

    private void StopRecording()
    {
        // Move focus to the window itself so the recorded hotkey becomes active immediately.
        if (Window.GetWindow(this) is { } window)
            FocusManager.SetFocusedElement(window, window);
        Keyboard.ClearFocus();
    }

    private void UpdateText()
    {
        Text = IsKeyboardFocused ? Strings.HotkeyBoxPrompt
            : Hotkey.IsNone ? Strings.HotkeyBoxEmpty
            : Hotkey.ToDisplayString();
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
}
