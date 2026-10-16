namespace Centerly.Core.AutoCenter;

public static class RuleMatcher
{
    /// <summary>Applies to every not excluded app in <see cref="AutoCenterMode.AllApps"/> mode: main windows, every launch.</summary>
    public static AppRule DefaultRule { get; } = new() { Id = Guid.Empty, ProcessName = "*" };

    /// <summary>The rule a new window should be centered with, or null to leave the window alone.</summary>
    /// <param name="rules">Configured rules.</param>
    /// <param name="mode">Whether unlisted applications are centered too.</param>
    /// <param name="processName">Executable file name, e.g. "notepad.exe".</param>
    /// <param name="isOwnedWindow">Whether the window has an owner (dialogs and other secondary windows).</param>
    public static AppRule? Find(IEnumerable<AppRule> rules, AutoCenterMode mode, string processName, bool isOwnedWindow)
    {
        // Each mode has its own list: centering rules in ListedApps, exclusions in AllApps; the other list is dormant.
        // A disabled rule behaves as if it were not in the list.
        var action = mode == AutoCenterMode.AllApps ? RuleAction.Ignore : RuleAction.Center;
        var rule = rules.FirstOrDefault(r =>
            r.IsEnabled && r.Action == action && string.Equals(r.ProcessName, processName, StringComparison.OrdinalIgnoreCase));

        if (mode == AutoCenterMode.AllApps)
            return rule is null && !isOwnedWindow ? DefaultRule : null;

        return rule is not null && (!isOwnedWindow || rule.IncludeDialogs) ? rule : null;
    }

    /// <summary>Whether any window could ever be centered, i.e. whether window events need to be watched at all.</summary>
    public static bool IsActive(AutoCenterMode mode, IEnumerable<AppRule> rules) =>
        mode == AutoCenterMode.AllApps || rules.Any(r => r.IsEnabled && r.Action == RuleAction.Center);
}
