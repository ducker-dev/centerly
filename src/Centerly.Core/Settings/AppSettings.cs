using Centerly.Core.AutoCenter;
using Centerly.Core.Centering;
using Centerly.Core.Hotkeys;

namespace Centerly.Core.Settings;

public enum AppLanguage
{
    /// <summary>Russian if the Windows display language is Russian, English otherwise.</summary>
    System,
    Russian,
    English,
}

/// <remarks>
/// Properties use <c>set</c>, not <c>init</c>: the System.Text.Json source generator assigns <c>default</c> to init-only
/// properties missing from the file, which would wipe defaults such as the hotkey. Treat instances as immutable and use <c>with</c>.
/// </remarks>
public sealed record AppSettings
{
    public const int CurrentVersion = 1;

    /// <summary>Default "center active window" hotkey: Ctrl+Alt+C.</summary>
    public static readonly Hotkey DefaultCenterHotkey = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'C');

    /// <summary>Schema version for future migrations.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Hotkey that centers the foreground window; <see cref="Hotkey.None"/> disables it.</summary>
    public Hotkey CenterHotkey { get; set; } = DefaultCenterHotkey;

    /// <summary>Hotkey that moves the foreground window to the next monitor and centers it; off by default.</summary>
    public Hotkey NextMonitorHotkey { get; set; } = Hotkey.None;

    /// <summary>Hotkey that moves the foreground window to the previous monitor and centers it; off by default.</summary>
    public Hotkey PreviousMonitorHotkey { get; set; } = Hotkey.None;

    /// <summary>Hotkey that puts the foreground window back where it was before Centerly moved it; off by default.</summary>
    public Hotkey UndoHotkey { get; set; } = Hotkey.None;

    public OversizeBehavior Oversize { get; set; } = OversizeBehavior.KeepSize;

    public AppLanguage Language { get; set; } = AppLanguage.System;

    /// <summary>Check GitHub releases for a new version in the background.</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>Whether new windows of all applications are centered, or only of the listed ones.</summary>
    public AutoCenterMode AutoCenterMode { get; set; } = AutoCenterMode.ListedApps;

    /// <summary>
    /// Both lists in one: <see cref="RuleAction.Center"/> rules for <see cref="AutoCenterMode.ListedApps"/> mode and
    /// <see cref="RuleAction.Ignore"/> exclusions for <see cref="AutoCenterMode.AllApps"/>. Switching modes keeps the other list.
    /// </summary>
    public IReadOnlyList<AppRule> Rules { get; set; } = [];

    // Record equality would compare the Rules list by reference.
    public bool Equals(AppSettings? other) =>
        other is not null
        && Version == other.Version
        && CenterHotkey == other.CenterHotkey
        && NextMonitorHotkey == other.NextMonitorHotkey
        && PreviousMonitorHotkey == other.PreviousMonitorHotkey
        && UndoHotkey == other.UndoHotkey
        && Oversize == other.Oversize
        && Language == other.Language
        && CheckForUpdates == other.CheckForUpdates
        && AutoCenterMode == other.AutoCenterMode
        && Rules.SequenceEqual(other.Rules);

    public override int GetHashCode() =>
        HashCode.Combine(
            HashCode.Combine(Version, CenterHotkey, NextMonitorHotkey, PreviousMonitorHotkey, UndoHotkey),
            HashCode.Combine(Oversize, Language, CheckForUpdates, AutoCenterMode, Rules.Count));
}
