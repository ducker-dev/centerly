namespace Centerly.Core.Centering;

public enum CenterOutcome
{
    Centered,
    AlreadyCentered,
    NoWindow,
    NotEligible,
    NotResponding,
    AccessDenied,
    Failed,

    /// <summary>Undo was requested for a window Centerly has not moved.</summary>
    NothingToUndo,
}

/// <param name="Outcome">What happened.</param>
/// <param name="Detail">Human readable reason for logs (not localized).</param>
/// <param name="WindowInfo">Short description of the target window for logs, if any.</param>
public readonly record struct CenterResult(CenterOutcome Outcome, string? Detail = null, string? WindowInfo = null)
{
    public bool IsSuccess => Outcome is CenterOutcome.Centered or CenterOutcome.AlreadyCentered;

    public override string ToString() =>
        $"{Outcome}{(Detail is null ? "" : $": {Detail}")}{(WindowInfo is null ? "" : $" [{WindowInfo}]")}";
}
