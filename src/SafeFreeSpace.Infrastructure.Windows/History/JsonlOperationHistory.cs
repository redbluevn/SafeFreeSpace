namespace SafeFreeSpace.Infrastructure.Windows.History;

using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using SafeFreeSpace.Contracts;
using SafeFreeSpace.Core.Interfaces;
using SafeFreeSpace.Core.Models;

public sealed class JsonlOperationHistory : IOperationHistory
{
    private readonly string _logDirectory;
    private readonly IClock _clock;
    private readonly IHashService _hashService;
    private readonly ILogRedactor _redactor;
    private readonly byte[] _salt;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonlOperationHistory(
        string? logDirectory = null,
        IClock? clock = null,
        IHashService? hashService = null,
        ILogRedactor? redactor = null)
    {
        _logDirectory = logDirectory ?? GetDefaultLogDirectory();
        _clock = clock ?? new SystemClock();
        _hashService = hashService ?? new Security.LocalHashService();
        _redactor = redactor ?? new Security.LogRedactor();
        _salt = LoadOrCreateSalt();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        Directory.CreateDirectory(_logDirectory);
    }

    public async Task AppendAsync(OperationJournalEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        string filePath = GetCurrentFilePath();
        OperationJournalEntry sanitized = SanitizeEntry(entry);
        string line = JsonSerializer.Serialize(sanitized, _jsonOptions);

        SemaphoreSlim semaphore = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(filePath, line + Environment.NewLine, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<IReadOnlyList<OperationJournalEntry>> ReadRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        var entries = new List<OperationJournalEntry>();
        foreach (string filePath in GetLogFiles())
        {
            await ReadFileAsync(filePath, entries, cancellationToken).ConfigureAwait(false);
        }

        return entries
            .OrderByDescending(e => e.TimestampUtc)
            .Take(limit)
            .ToList();
    }

    public async Task MarkAbandonedAsync(CancellationToken cancellationToken = default)
    {
        foreach (string filePath in GetLogFiles())
        {
            await MarkAbandonedInFileAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ApplyRetentionAsync(TimeSpan retention, CancellationToken cancellationToken = default)
    {
        DateTimeOffset cutoff = _clock.UtcNow - retention;
        foreach (string filePath in Directory.GetFiles(_logDirectory, "operations-*.jsonl"))
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            if (fileName.StartsWith("operations-", StringComparison.OrdinalIgnoreCase) &&
                DateTime.TryParseExact(fileName["operations-".Length..], "yyyyMM", null, DateTimeStyles.None, out DateTime fileMonth))
            {
                DateTimeOffset fileEnd = new DateTimeOffset(fileMonth.AddMonths(1), TimeSpan.Zero);
                if (fileEnd < cutoff)
                {
                    await DeleteLogFileAsync(filePath, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    public async Task<string> ExportDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        string targetPath = Path.Combine(_logDirectory, $"diagnostics-{_clock.UtcNow:yyyyMMdd-HHmmss}.json");
        IReadOnlyList<OperationJournalEntry> entries = await ReadRecentAsync(1000, cancellationToken).ConfigureAwait(false);
        var sanitized = entries.Select(e => new
        {
            e.OperationId,
            e.TimestampUtc,
            e.AppVersion,
            e.ActionType,
            e.DriveLetter,
            e.MediaType,
            e.BusType,
            e.FileSystem,
            e.Result,
            e.ErrorCategory,
            e.Duration
        });

        await File.WriteAllTextAsync(
            targetPath,
            JsonSerializer.Serialize(sanitized, _jsonOptions),
            cancellationToken).ConfigureAwait(false);

        return targetPath;
    }

    public async Task ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        foreach (string filePath in Directory.GetFiles(_logDirectory, "operations-*.jsonl"))
        {
            await DeleteLogFileAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task DeleteLogFileAsync(string filePath, CancellationToken cancellationToken)
    {
        // Acquire lock của file trước khi xóa để tránh race với AppendAsync/ReadFileAsync đang chạy.
        SemaphoreSlim semaphore = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            File.Delete(filePath);
            // Gỡ semaphore của file đã xóa khỏi dictionary để không giữ lock chết.
            _fileLocks.TryRemove(filePath, out _);
        }
        finally
        {
            semaphore.Release();
            semaphore.Dispose();
        }
    }

    private OperationJournalEntry SanitizeEntry(OperationJournalEntry entry)
    {
        string hashedGuid = _hashService.HashWithSalt(entry.HashedVolumeGuid, _salt);
        return entry with
        {
            HashedVolumeGuid = hashedGuid,
            Output = _redactor.RedactOutput(entry.Output)
        };
    }

    private string GetCurrentFilePath()
    {
        return Path.Combine(_logDirectory, $"operations-{_clock.UtcNow:yyyyMM}.jsonl");
    }

    private IEnumerable<string> GetLogFiles()
    {
        return Directory.GetFiles(_logDirectory, "operations-*.jsonl")
            .OrderByDescending(f => f);
    }

    private async Task ReadFileAsync(string filePath, List<OperationJournalEntry> entries, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        SemaphoreSlim semaphore = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(stream);
            while (true)
            {
                string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    OperationJournalEntry? entry = JsonSerializer.Deserialize<OperationJournalEntry>(line, _jsonOptions);
                    if (entry != null)
                    {
                        entries.Add(entry);
                    }
                }
                catch (JsonException)
                {
                }
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task MarkAbandonedInFileAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var lines = new List<string>();
        bool changed = false;

        SemaphoreSlim semaphore = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var reader = new StreamReader(stream);
            while (true)
            {
                string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                    continue;
                }

                try
                {
                    OperationJournalEntry? entry = JsonSerializer.Deserialize<OperationJournalEntry>(line, _jsonOptions);
                    if (entry != null &&
                        (entry.Result == OperationResult.None.ToString() || string.IsNullOrEmpty(entry.Result)))
                    {
                        entry = entry with
                        {
                            Result = OperationResult.Abandoned.ToString(),
                            EndTime = _clock.UtcNow
                        };
                        changed = true;
                    }

                    lines.Add(entry != null ? JsonSerializer.Serialize(entry, _jsonOptions) : line);
                }
                catch (JsonException)
                {
                    lines.Add(line);
                }
            }

            if (changed)
            {
                stream.SetLength(0);
                using var writer = new StreamWriter(stream);
                foreach (string line in lines)
                {
                    await writer.WriteLineAsync(line).ConfigureAwait(false);
                }

                await writer.FlushAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private byte[] LoadOrCreateSalt()
    {
        string saltPath = Path.Combine(_logDirectory, "salt.bin");
        if (File.Exists(saltPath))
        {
            return File.ReadAllBytes(saltPath);
        }

        byte[] salt = new byte[32];
        RandomNumberGenerator.Fill(salt);
        Directory.CreateDirectory(_logDirectory);
        File.WriteAllBytes(saltPath, salt);
        return salt;
    }

    private static string GetDefaultLogDirectory()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "SafeFreeSpace", "Logs");
    }
}
