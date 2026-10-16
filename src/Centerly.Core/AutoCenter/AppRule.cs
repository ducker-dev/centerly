namespace Centerly.Core.AutoCenter;

/// <summary>What a rule does with the windows of its application.</summary>
public enum RuleAction
{
    /// <summary>In the list of <see cref="AutoCenterMode.ListedApps"/> mode: center new windows, with the rule's options.</summary>
    Center,

    /// <summary>In the list of <see cref="AutoCenterMode.AllApps"/> mode: an exclusion, leave the windows alone.</summary>
    Ignore,
}

/// <summary>"Center (or leave alone) windows of this application when they open."</summary>
/// <remarks>Mutable setters for System.Text.Json (see <see cref="Settings.AppSettings"/>); treat as immutable, use <c>with</c>.</remarks>
public sealed record AppRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Executable file name the rule matches, e.g. "notepad.exe". Case-insensitive.</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>Name shown in the UI, e.g. "Блокнот". Falls back to <see cref="ProcessName"/>.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Last known full path, used for the icon only (matching is by <see cref="ProcessName"/>).</summary>
    public string? ExecutablePath { get; set; }

    public bool IsEnabled { get; set; } = true;

    public RuleAction Action { get; set; } = RuleAction.Center;

    /// <summary>Center only the first window of each launch, leave later windows where the app puts them.</summary>
    public bool OnlyFirstWindow { get; set; }

    /// <summary>Also center owned windows (dialogs, settings windows), not just main windows.</summary>
    public bool IncludeDialogs { get; set; }

    public string Title => string.IsNullOrWhiteSpace(DisplayName) ? ProcessName : DisplayName;
}
