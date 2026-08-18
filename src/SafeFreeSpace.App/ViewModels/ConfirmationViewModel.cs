namespace SafeFreeSpace.App.ViewModels;

using System.Windows.Input;
using SafeFreeSpace.App.Mvvm;
using SafeFreeSpace.Core.Models;

public sealed class ConfirmationViewModel : ObservableObject
{
    private VolumeCardViewModel? _volume;
    private string _phrase = string.Empty;
    private int _countdownSeconds;
    private bool _canStart;

    public ConfirmationViewModel(Action startAction, Action cancelAction)
    {
        StartCommand = new RelayCommand(_ => startAction(), _ => CanStart);
        CancelCommand = new RelayCommand(_ => cancelAction());
    }

    public VolumeCardViewModel? Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, value))
            {
                OnPropertyChanged(nameof(ExpectedPhrase));
                OnPropertyChanged(nameof(Disclaimer));
                OnPropertyChanged(nameof(VolumeSummary));
                ValidatePhrase();
            }
        }
    }

    public string Phrase
    {
        get => _phrase;
        set
        {
            if (SetProperty(ref _phrase, value))
            {
                ValidatePhrase();
            }
        }
    }

    public int CountdownSeconds
    {
        get => _countdownSeconds;
        set
        {
            if (SetProperty(ref _countdownSeconds, value))
            {
                OnPropertyChanged(nameof(CountdownText));
            }
        }
    }

    public string CountdownText => CountdownSeconds > 0
        ? $"Chờ {CountdownSeconds}s trước khi bắt đầu"
        : string.Empty;

    public bool CanStart
    {
        get => _canStart;
        private set
        {
            if (SetProperty(ref _canStart, value))
            {
                ((RelayCommand)StartCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string ExpectedPhrase => Volume?.Decision.Action switch
    {
        ProposedAction.WipeHddFreeSpace => $"WIPE FREE SPACE {Volume.DriveLetter}:",
        ProposedAction.RetrimSsd => $"RETRIM {Volume.DriveLetter}:",
        _ => string.Empty
    };

    public string VolumeSummary => Volume == null
        ? string.Empty
        : $"Ổ {Volume.DriveLetter}: - {Volume.Label ?? "(không có nhãn)"} - {Volume.CapacityText}";

    public string EstimatedDurationText => Volume?.EstimatedDurationText ?? string.Empty;

    public string Disclaimer => Volume?.Decision.Action switch
    {
        ProposedAction.WipeHddFreeSpace =>
            "Thao tác này ghi đè vùng trống của volume bằng công cụ hệ thống Windows. " +
            "Nội dung tệp đang tồn tại không được chọn để xóa. " +
            "Các bản sao trong Recycle Bin, Shadow Copies, File History, backup, cloud sync, pagefile, hibernation, " +
            "ứng dụng khác hoặc metadata hệ thống có thể vẫn tồn tại.",
        ProposedAction.RetrimSsd =>
            "Thao tác này gửi lại yêu cầu TRIM cho vùng trống. " +
            "TRIM không phải Secure Erase và không bảo đảm mọi ô nhớ vật lý đã được xóa. " +
            "Muốn bảo đảm cao hơn phải sao lưu dữ liệu cần giữ, sanitize toàn ổ bằng công cụ phù hợp rồi chép dữ liệu trở lại.",
        _ => string.Empty
    };

    public ICommand StartCommand
    {
        get;
    }

    public ICommand CancelCommand
    {
        get;
    }

    public void Reset()
    {
        Phrase = string.Empty;
        CountdownSeconds = 3;
        CanStart = false;
        Volume = null;
    }

    private void ValidatePhrase()
    {
        bool phraseOk = !string.IsNullOrWhiteSpace(Phrase)
                        && Phrase.Trim().Equals(ExpectedPhrase, StringComparison.Ordinal);
        CanStart = phraseOk && CountdownSeconds <= 0;
    }

    public void OnCountdownChanged()
    {
        OnPropertyChanged(nameof(CountdownText));
        ValidatePhrase();
    }
}
