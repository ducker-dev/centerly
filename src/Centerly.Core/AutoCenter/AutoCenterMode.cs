namespace Centerly.Core.AutoCenter;

/// <summary>Which applications get their new windows centered.</summary>
public enum AutoCenterMode
{
    /// <summary>Only applications with a <see cref="RuleAction.Center"/> rule.</summary>
    ListedApps,

    /// <summary>Every application except those with a <see cref="RuleAction.Ignore"/> rule; centering rules are not used.</summary>
    AllApps,
}
