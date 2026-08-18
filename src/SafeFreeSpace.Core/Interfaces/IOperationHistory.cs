namespace SafeFreeSpace.Core.Interfaces;

using SafeFreeSpace.Core.Models;

public interface IOperationHistory
{
    Task AppendAsync(OperationJournalEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OperationJournalEntry>> ReadRecentAsync(int limit, CancellationToken cancellationToken = default);

    Task MarkAbandonedAsync(CancellationToken cancellationToken = default);

    Task ApplyRetentionAsync(TimeSpan retention, CancellationToken cancellationToken = default);

    Task ClearHistoryAsync(CancellationToken cancellationToken = default);
}

public sealed record OperationJournalEntry(
    string OperationId,
    DateTimeOffset TimestampUtc,
    string AppVersion,
    string ActionType,
    string DriveLetter,
    string HashedVolumeGuid,
    string MediaType,
    string BusType,
    string FileSystem,
    long CapacityBytes,
    long FreeBytes,
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime,
    TimeSpan? Duration,
    int? ExitCode,
    string Result,
    string ErrorCategory,
    string Output);
