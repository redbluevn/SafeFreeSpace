namespace SafeFreeSpace.Core.Models;

public sealed record VolumeSnapshot(
    VolumeIdentity Identity,
    DateTimeOffset CapturedAt);
