namespace SafeFreeSpace.Core.Models;

public sealed record VolumeIdentity(
    string DriveLetter,
    string VolumeGuid,
    string? Label,
    string FileSystem,
    long CapacityBytes,
    long FreeBytes,
    bool IsSystem,
    bool IsBoot,
    bool IsReadOnly,
    bool IsDirty,
    bool IsNetwork,
    bool IsRemovable,
    bool IsOptical,
    BitLockerState BitLockerState,
    DriveMediaType MediaType,
    DriveBusType BusType,
    string? RedactedModel,
    string? HealthStatus);
