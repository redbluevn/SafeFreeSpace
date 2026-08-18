namespace SafeFreeSpace.Core.Models;

public sealed record OperationPlan(
    string OperationId,
    DateTimeOffset CreatedAt,
    VolumeSnapshot Snapshot,
    ProposedAction ProposedAction,
    string ConfirmationPhrase);
