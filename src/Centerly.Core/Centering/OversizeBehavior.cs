namespace Centerly.Core.Centering;

/// <summary>What to do with a window that does not fit into the monitor work area.</summary>
public enum OversizeBehavior
{
    /// <summary>Keep the size, pin the oversized axis to the work area's top/left edge so the title bar stays reachable.</summary>
    KeepSize,

    /// <summary>Shrink the oversized axis to the work area and center.</summary>
    ShrinkToFit,
}
