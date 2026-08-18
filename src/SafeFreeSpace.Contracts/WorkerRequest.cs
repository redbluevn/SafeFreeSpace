namespace SafeFreeSpace.Contracts;

public sealed record WorkerRequest(
    int ProtocolVersion,
    string OperationId,
    string Nonce,
    WorkerOperationType OperationType,
    VolumeIdentityDto Volume);

public enum WorkerOperationType
{
    Unknown = 0,
    RefreshVolumeIdentity,
    WipeHddFreeSpace,
    RetrimSsd,
    CancelOperation
}

public sealed record VolumeIdentityDto(
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
    string BitLockerState,
    string MediaType,
    string BusType,
    string? RedactedModel,
    string? HealthStatus);

public sealed record WorkerResponse(
    bool Success,
    OperationResult Result,
    string ErrorCategory,
    string? Message);

public enum OperationResult
{
    None = 0,
    Completed,
    Failed,
    Interrupted,
    Abandoned
}

public sealed record OperationProgress(
    string OperationId,
    string State,
    string? OutputChunk);

public sealed record ProtocolHandshake(
    int ProtocolVersion,
    string Nonce,
    string OperationId);

public static class ProtocolConstants
{
    public const int CurrentProtocolVersion = 1;
    public const int MaxMessageLengthBytes = 256 * 1024;
}
