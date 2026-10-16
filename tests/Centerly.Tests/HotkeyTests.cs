using Centerly.Core.Hotkeys;

namespace Centerly.Tests;

public class HotkeyTests
{
    [Theory]
    [InlineData("Ctrl+Alt+C", HotkeyModifiers.Control | HotkeyModifiers.Alt, 'C')]
    [InlineData(" ctrl + shift + f5 ", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x74)]
    [InlineData("Win+Num5", HotkeyModifiers.Win, 0x65)]
    [InlineData("Control+Windows+Backslash", HotkeyModifiers.Control | HotkeyModifiers.Win, 0xDC)]
    [InlineData("F12", HotkeyModifiers.None, 0x7B)]
    [InlineData("Alt+VK_E1", HotkeyModifiers.Alt, 0xE1)]
    public void Parses_valid_strings(string text, HotkeyModifiers modifiers, uint key)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(new Hotkey(modifiers, key), hotkey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+A+B")]
    [InlineData("Ctrl+Banana")]
    [InlineData("Ctrl+VK_ZZ")]
    public void Rejects_invalid_strings(string text) =>
        Assert.False(Hotkey.TryParse(text, out _));

    [Fact]
    public void Formats_modifiers_in_canonical_order()
    {
        var hotkey = new Hotkey(HotkeyModifiers.Win | HotkeyModifiers.Shift | HotkeyModifiers.Alt | HotkeyModifiers.Control, 'X');

        Assert.Equal("Ctrl+Alt+Shift+Win+X", hotkey.ToString());
        Assert.Equal("Ctrl + Alt + Shift + Win + X", hotkey.ToDisplayString());
    }

    [Theory]
    [InlineData("Ctrl+Alt+C")]
    [InlineData("Shift+F1")]
    [InlineData("Win+Semicolon")]
    [InlineData("Ctrl+VK_E1")]
    public void Round_trips_through_string(string text)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(text, hotkey.Value.ToString());
    }

    [Theory]
    [InlineData("Ctrl+C", true)]
    [InlineData("Win+Shift+C", true)]
    [InlineData("F9", true)]
    [InlineData("Shift+F9", true)]
    [InlineData("C", false)]
    [InlineData("Shift+C", false)]
    public void Accepts_only_combinations_that_do_not_hijack_typing(string text, bool acceptable)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(acceptable, hotkey.Value.IsAcceptable);
    }

    [Fact]
    public void None_is_not_acceptable_and_formats_empty()
    {
        Assert.True(Hotkey.None.IsNone);
        Assert.False(Hotkey.None.IsAcceptable);
        Assert.Equal(string.Empty, Hotkey.None.ToString());
    }
}
