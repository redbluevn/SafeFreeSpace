namespace SafeFreeSpace.App.ViewModels;

using SafeFreeSpace.App.Mvvm;
using SafeFreeSpace.Core.Interfaces;

public sealed class HistoryEntryViewModel : ObservableObject
{
    public HistoryEntryViewModel(OperationJournalEntry entry)
    {
        Entry = entry;
    }

    public OperationJournalEntry Entry
    {
        get;
    }

    public DateTimeOffset TimestampLocal => Entry.TimestampUtc.ToLocalTime();

    public string ActionType => Entry.ActionType;

    public string DriveLetter => Entry.DriveLetter;

    public string MediaType => Entry.MediaType;

    public string Result => Entry.Result;

    public string Summary => $"[{TimestampLocal:yyyy-MM-dd HH:mm}] {ActionType} ổ {DriveLetter} - {Result}";
}
