namespace SafeFreeSpace.Core.Models;

public enum ProposedAction
{
    None = 0,
    WipeHddFreeSpace,
    RetrimSsd,
    Blocked
}

public sealed record EligibilityDecision(
    ProposedAction Action,
    bool IsEligible,
    IReadOnlyList<string> Reasons,
    bool RequiresAdvancedMode);
