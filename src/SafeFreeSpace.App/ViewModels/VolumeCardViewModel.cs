namespace SafeFreeSpace.App.ViewModels;

using System.Windows.Input;
using SafeFreeSpace.App.Mvvm;
using SafeFreeSpace.Core.Models;

public sealed class VolumeCardViewModel : ObservableObject
{
    private readonly Action<VolumeCardViewModel> _onSelect;

    public VolumeCardViewModel(VolumeIdentity identity, EligibilityDecision decision, Action<VolumeCardViewModel> onSelect)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Decision = decision ?? throw new ArgumentNullException(nameof(decision));
        _onSelect = onSelect ?? throw new ArgumentNullException(nameof(onSelect));
        SelectCommand = new RelayCommand(_ => _onSelect(this), _ => decision.IsEligible);
    }

    public VolumeIdentity Identity
    {
        get;
    }

    public EligibilityDecision Decision
    {
        get;
    }

    public ICommand SelectCommand
    {
        get;
    }

    public string DriveLetter => Identity.DriveLetter;

    public string? Label => Identity.Label;

    public string FileSystem => Identity.FileSystem;

    public string CapacityText => FormatBytes(Identity.CapacityBytes);

    public string FreeText => FormatBytes(Identity.FreeBytes);

    public string? Model => Identity.RedactedModel;

    public string MediaType => Identity.MediaType.ToString();

    public string BusType => Identity.BusType.ToString();

    public string? HealthStatus => Identity.HealthStatus;

    public bool IsSystem => Identity.IsSystem;

    public bool IsBoot => Identity.IsBoot;

    public string BitLockerState => Identity.BitLockerState.ToString();

    public bool IsEligible => Decision.IsEligible;

    public string ActionText => Decision.Action switch
    {
        ProposedAction.WipeHddFreeSpace => "Làm sạch vùng trống",
        ProposedAction.RetrimSsd => "Gửi lại TRIM",
        _ => "Không khả dụng"
    };

    public string StatusText => Decision.IsEligible
        ? ActionText
        : string.Join("; ", Decision.Reasons);

    public string EstimatedDurationText => Decision.IsEligible
        ? $"Ước tính: {EstimateDuration(Identity.FreeBytes, Decision.Action)}"
        : string.Empty;

    private static string EstimateDuration(long freeBytes, ProposedAction action)
    {
        if (freeBytes <= 0)
        {
            return "không xác định";
        }

        double bytesPerSecond = action switch
        {
            ProposedAction.WipeHddFreeSpace => 100.0 * 1024 * 1024, // ~100 MB/s for cipher.exe overwrite
            ProposedAction.RetrimSsd => 500.0 * 1024 * 1024,        // ~500 MB/s effective for ReTrim
            _ => 0
        };

        if (bytesPerSecond <= 0)
        {
            return "không xác định";
        }

        double seconds = freeBytes / bytesPerSecond;
        TimeSpan duration = TimeSpan.FromSeconds(seconds);

        if (duration.TotalHours >= 1)
        {
            return $"~{duration.TotalHours:0.#} giờ";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"~{duration.TotalMinutes:0.#} phút";
        }

        return $"~{duration.TotalSeconds:0} giây";
    }

    private static string FormatBytes(long bytes)
    {
        const long scale = 1024;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unitIndex = 0;
        while (value >= scale && unitIndex < units.Length - 1)
        {
            value /= scale;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }
}
