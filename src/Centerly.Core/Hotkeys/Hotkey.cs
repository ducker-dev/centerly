using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Centerly.Core.Hotkeys;

/// <summary>Values match the MOD_* flags of RegisterHotKey.</summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

/// <summary>A global hotkey: modifiers plus one virtual-key code. Serialized as "Ctrl+Alt+C".</summary>
public readonly record struct Hotkey(HotkeyModifiers Modifiers, uint VirtualKey)
{
    private const char Separator = '+';

    private static readonly Dictionary<uint, string> KeyNames = BuildKeyNames();
    private static readonly Dictionary<string, uint> KeysByName =
        KeyNames.ToDictionary(p => p.Value, p => p.Key, StringComparer.OrdinalIgnoreCase);

    public static Hotkey None => default;

    public bool IsNone => VirtualKey == 0;

    /// <summary>
    /// Whether the combination is sensible as a global hotkey: a bare letter or Shift+letter would hijack normal typing,
    /// so those need Ctrl/Alt/Win. Function keys are fine on their own.
    /// </summary>
    public bool IsAcceptable =>
        !IsNone && (IsFunctionKey(VirtualKey) || (Modifiers & ~HotkeyModifiers.Shift) != HotkeyModifiers.None);

    /// <summary>Canonical form for settings: "Ctrl+Alt+C".</summary>
    public override string ToString() => Format(string.Empty + Separator);

    /// <summary>Form for UI: "Ctrl + Alt + C".</summary>
    public string ToDisplayString() => Format($" {Separator} ");

    public static bool TryParse(string? text, [NotNullWhen(true)] out Hotkey? hotkey)
    {
        hotkey = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var modifiers = HotkeyModifiers.None;
        uint key = 0;
        foreach (var rawPart in text.Split(Separator))
        {
            var part = rawPart.Trim();
            if (TryParseModifier(part, out var modifier))
            {
                modifiers |= modifier;
                continue;
            }

            // Exactly one non-modifier key.
            if (key != 0 || !TryParseKey(part, out key))
                return false;
        }

        if (key == 0)
            return false;

        hotkey = new Hotkey(modifiers, key);
        return true;
    }

    public static string GetKeyName(uint virtualKey) =>
        KeyNames.TryGetValue(virtualKey, out var name) ? name : $"VK_{virtualKey:X2}";

    private string Format(string separator)
    {
        if (IsNone)
            return string.Empty;

        return string.Join(separator, ModifierNames(Modifiers).Append(GetKeyName(VirtualKey)));
    }

    /// <summary>"Ctrl + Alt" for UI, e.g. while the user is still holding modifiers.</summary>
    public static string FormatModifiers(HotkeyModifiers modifiers) =>
        string.Join($" {Separator} ", ModifierNames(modifiers));

    private static IEnumerable<string> ModifierNames(HotkeyModifiers modifiers)
    {
        if (modifiers.HasFlag(HotkeyModifiers.Control)) yield return "Ctrl";
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) yield return "Alt";
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) yield return "Shift";
        if (modifiers.HasFlag(HotkeyModifiers.Win)) yield return "Win";
    }

    private static bool TryParseModifier(string part, out HotkeyModifiers modifier)
    {
        modifier = part.ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => HotkeyModifiers.Control,
            "ALT" => HotkeyModifiers.Alt,
            "SHIFT" => HotkeyModifiers.Shift,
            "WIN" or "WINDOWS" => HotkeyModifiers.Win,
            _ => HotkeyModifiers.None,
        };
        return modifier != HotkeyModifiers.None;
    }

    private static bool TryParseKey(string part, out uint key)
    {
        if (KeysByName.TryGetValue(part, out key))
            return true;

        return part.StartsWith("VK_", StringComparison.OrdinalIgnoreCase)
               && uint.TryParse(part.AsSpan(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out key)
               && key is > 0 and <= 0xFE;
    }

    private static bool IsFunctionKey(uint vk) => vk is >= 0x70 and <= 0x87;

    private static Dictionary<uint, string> BuildKeyNames()
    {
        var names = new Dictionary<uint, string>
        {
            [0x08] = "Backspace",
            [0x09] = "Tab",
            [0x0D] = "Enter",
            [0x13] = "Pause",
            [0x1B] = "Esc",
            [0x20] = "Space",
            [0x21] = "PageUp",
            [0x22] = "PageDown",
            [0x23] = "End",
            [0x24] = "Home",
            [0x25] = "Left",
            [0x26] = "Up",
            [0x27] = "Right",
            [0x28] = "Down",
            [0x2C] = "PrintScreen",
            [0x2D] = "Insert",
            [0x2E] = "Delete",
            [0x6A] = "NumMultiply",
            [0x6B] = "NumAdd",
            [0x6D] = "NumSubtract",
            [0x6E] = "NumDecimal",
            [0x6F] = "NumDivide",
            [0xBA] = "Semicolon",
            [0xBB] = "Equals",
            [0xBC] = "Comma",
            [0xBD] = "Minus",
            [0xBE] = "Period",
            [0xBF] = "Slash",
            [0xC0] = "Backtick",
            [0xDB] = "LeftBracket",
            [0xDC] = "Backslash",
            [0xDD] = "RightBracket",
            [0xDE] = "Quote",
        };

        for (uint vk = '0'; vk <= '9'; vk++) names[vk] = ((char)vk).ToString();
        for (uint vk = 'A'; vk <= 'Z'; vk++) names[vk] = ((char)vk).ToString();
        for (uint i = 0; i <= 9; i++) names[0x60 + i] = $"Num{i}";
        for (uint i = 1; i <= 24; i++) names[0x6F + i] = $"F{i}";
        return names;
    }
}
