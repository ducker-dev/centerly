using Centerly.Core.AutoCenter;

namespace Centerly.Tests;

public class RuleMatcherTests
{
    private const AutoCenterMode Listed = AutoCenterMode.ListedApps;
    private const AutoCenterMode All = AutoCenterMode.AllApps;

    private static readonly AppRule Notepad = new() { ProcessName = "notepad.exe" };
    private static readonly AppRule Disabled = new() { ProcessName = "calc.exe", IsEnabled = false };
    private static readonly AppRule WithDialogs = new() { ProcessName = "code.exe", IncludeDialogs = true };
    private static readonly AppRule Excluded = new() { ProcessName = "obs64.exe", Action = RuleAction.Ignore, IncludeDialogs = true };

    private static readonly AppRule[] Rules = [Notepad, Disabled, WithDialogs, Excluded];

    [Fact]
    public void Matches_process_name_case_insensitively() =>
        Assert.Same(Notepad, RuleMatcher.Find(Rules, Listed, "Notepad.EXE", isOwnedWindow: false));

    [Fact]
    public void Ignores_unknown_process_in_listed_mode() =>
        Assert.Null(RuleMatcher.Find(Rules, Listed, "explorer.exe", isOwnedWindow: false));

    [Fact]
    public void Ignores_disabled_rule_in_listed_mode() =>
        Assert.Null(RuleMatcher.Find(Rules, Listed, "calc.exe", isOwnedWindow: false));

    [Fact]
    public void Skips_dialogs_unless_rule_includes_them()
    {
        Assert.Null(RuleMatcher.Find(Rules, Listed, "notepad.exe", isOwnedWindow: true));
        Assert.Same(WithDialogs, RuleMatcher.Find(Rules, Listed, "code.exe", isOwnedWindow: true));
    }

    [Fact]
    public void Does_not_match_by_substring() =>
        Assert.Null(RuleMatcher.Find(Rules, Listed, "notepad++.exe", isOwnedWindow: false));

    [Fact]
    public void Exclusion_never_centers()
    {
        Assert.Null(RuleMatcher.Find(Rules, All, "obs64.exe", isOwnedWindow: false));
        Assert.Null(RuleMatcher.Find(Rules, All, "obs64.exe", isOwnedWindow: true));
    }

    [Fact]
    public void Exclusions_do_not_count_as_listed_apps()
    {
        // Exclusions belong to the "all apps" list; in "listed" mode they must not center the app.
        Assert.Null(RuleMatcher.Find(Rules, Listed, "obs64.exe", isOwnedWindow: false));
        Assert.Null(RuleMatcher.Find(Rules, Listed, "obs64.exe", isOwnedWindow: true));
    }

    [Fact]
    public void All_apps_mode_centers_unlisted_main_windows_with_default_rule()
    {
        Assert.Same(RuleMatcher.DefaultRule, RuleMatcher.Find(Rules, All, "explorer.exe", isOwnedWindow: false));
        Assert.Null(RuleMatcher.Find(Rules, All, "explorer.exe", isOwnedWindow: true));
    }

    [Fact]
    public void All_apps_mode_ignores_the_listed_apps_list()
    {
        // The other mode's list is dormant: its per-app options do not apply.
        Assert.Same(RuleMatcher.DefaultRule, RuleMatcher.Find(Rules, All, "code.exe", isOwnedWindow: false));
        Assert.Null(RuleMatcher.Find(Rules, All, "code.exe", isOwnedWindow: true));
    }

    [Fact]
    public void Disabled_exclusion_falls_back_to_centering_in_all_apps_mode()
    {
        AppRule[] rules = [Excluded with { IsEnabled = false }];

        Assert.Same(RuleMatcher.DefaultRule, RuleMatcher.Find(rules, All, "obs64.exe", isOwnedWindow: false));
    }

    [Fact]
    public void Is_active_when_a_centering_rule_is_enabled_or_all_apps_are_centered()
    {
        Assert.True(RuleMatcher.IsActive(Listed, Rules));
        Assert.False(RuleMatcher.IsActive(Listed, [Disabled, Excluded]));
        Assert.False(RuleMatcher.IsActive(Listed, []));
        Assert.True(RuleMatcher.IsActive(All, []));
    }
}
