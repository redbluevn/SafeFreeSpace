namespace SafeFreeSpace.Tests.Unit.Infrastructure;

using SafeFreeSpace.Contracts;
using SafeFreeSpace.Core.Interfaces;
using SafeFreeSpace.Core.Models;
using SafeFreeSpace.Infrastructure.Windows.History;
using SafeFreeSpace.Infrastructure.Windows.Security;
using Xunit;

public class JsonlOperationHistoryTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FakeClock _clock;

    public JsonlOperationHistoryTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"sfs-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _clock = new FakeClock();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_testDirectory, true);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task AppendAndRead_RoundTrip()
    {
        var history = CreateHistory();
        var entry = CreateEntry("op-1", OperationResult.Completed);

        await history.AppendAsync(entry);
        IReadOnlyList<OperationJournalEntry> recent = await history.ReadRecentAsync(10);

        Assert.Single(recent);
        Assert.Equal("op-1", recent[0].OperationId);
        Assert.NotEqual("{GUID}", recent[0].HashedVolumeGuid);
        Assert.True(recent[0].HashedVolumeGuid.Length > 10);
    }

    [Fact]
    public async Task MarkAbandoned_UpdatesNoneResult()
    {
        var history = CreateHistory();
        var entry = CreateEntry("op-2", OperationResult.None);
        await history.AppendAsync(entry);

        await history.MarkAbandonedAsync();
        IReadOnlyList<OperationJournalEntry> recent = await history.ReadRecentAsync(10);

        Assert.Single(recent);
        Assert.Equal(OperationResult.Abandoned.ToString(), recent[0].Result);
        Assert.NotNull(recent[0].EndTime);
    }

    [Fact]
    public async Task ReadRecent_SkipsMalformedLines()
    {
        var history = CreateHistory();
        await history.AppendAsync(CreateEntry("op-3", OperationResult.Completed));
        await File.AppendAllTextAsync(Path.Combine(_testDirectory, $"operations-{_clock.UtcNow:yyyyMM}.jsonl"), "not-json" + Environment.NewLine);

        IReadOnlyList<OperationJournalEntry> recent = await history.ReadRecentAsync(10);
        Assert.Single(recent);
    }

    [Fact]
    public async Task ApplyRetention_DeletesOldFile()
    {
        var history = CreateHistory();
        string oldFile = Path.Combine(_testDirectory, "operations-202501.jsonl");
        await File.WriteAllTextAsync(oldFile, "{}" + Environment.NewLine);

        await history.ApplyRetentionAsync(TimeSpan.FromDays(30));

        Assert.False(File.Exists(oldFile));
    }

    [Fact]
    public async Task ClearHistory_RemovesFiles()
    {
        var history = CreateHistory();
        await history.AppendAsync(CreateEntry("op-4", OperationResult.Completed));

        await history.ClearHistoryAsync();

        Assert.Empty(Directory.GetFiles(_testDirectory, "operations-*.jsonl"));
    }

    [Fact]
    public async Task ClearHistory_ThenAppend_RecreatesFile()
    {
        var history = CreateHistory();
        await history.AppendAsync(CreateEntry("op-5", OperationResult.Completed));
        await history.ClearHistoryAsync();

        // Sau khi xóa file và gỡ lock khỏi dictionary, append tiếp phải hoạt động bình thường.
        await history.AppendAsync(CreateEntry("op-6", OperationResult.Completed));
        IReadOnlyList<OperationJournalEntry> recent = await history.ReadRecentAsync(10);

        Assert.Single(recent);
        Assert.Equal("op-6", recent[0].OperationId);
    }

    private JsonlOperationHistory CreateHistory()
    {
        return new JsonlOperationHistory(_testDirectory, _clock, new LocalHashService(), new LogRedactor());
    }

    private static OperationJournalEntry CreateEntry(string operationId, OperationResult result)
    {
        return new OperationJournalEntry(
            operationId,
            DateTimeOffset.UtcNow,
            "1.0.0",
            "WipeHddFreeSpace",
            "C",
            "{GUID}",
            "Hdd",
            "Sata",
            "NTFS",
            100000000000,
            50000000000,
            DateTimeOffset.UtcNow,
            result == OperationResult.None ? null : DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(1),
            0,
            result.ToString(),
            "None",
            "output line");
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 7, 17, 10, 0, 0, TimeSpan.Zero);
    }
}
