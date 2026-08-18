namespace SafeFreeSpace.Core.Services;

using SafeFreeSpace.Contracts;
using SafeFreeSpace.Core.Interfaces;

public sealed class InMemoryOperationHistory : IOperationHistory
{
    private readonly List<OperationJournalEntry> _entries = new();
    private readonly object _lock = new();

    public Task AppendAsync(OperationJournalEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_lock)
        {
            _entries.Add(entry);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OperationJournalEntry>> ReadRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IReadOnlyList<OperationJournalEntry> result = _entries
                .OrderByDescending(e => e.TimestampUtc)
                .Take(limit)
                .ToList();
            return Task.FromResult(result);
        }
    }

    public Task MarkAbandonedAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                OperationJournalEntry entry = _entries[i];
                if (entry.Result == OperationResult.None.ToString() ||
                    string.IsNullOrEmpty(entry.Result))
                {
                    _entries[i] = entry with
                    {
                        Result = OperationResult.Abandoned.ToString(),
                        EndTime = DateTimeOffset.UtcNow
                    };
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task ApplyRetentionAsync(TimeSpan retention, CancellationToken cancellationToken = default)
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow - retention;
        lock (_lock)
        {
            _entries.RemoveAll(e => e.TimestampUtc < cutoff);
        }

        return Task.CompletedTask;
    }

    public Task ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _entries.Clear();
        }

        return Task.CompletedTask;
    }
}
