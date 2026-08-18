namespace SafeFreeSpace.App.ViewModels;

using System.Collections.ObjectModel;
using System.Windows.Input;
using SafeFreeSpace.App.Mvvm;
using SafeFreeSpace.Contracts;
using SafeFreeSpace.Core.Interfaces;
using SafeFreeSpace.Core.Models;
using SafeFreeSpace.Core.Services;

public enum MainViewState
{
    Dashboard,
    Confirmation,
    Progress,
    Result,
    History,
    Settings,
    Help
}

public sealed class MainViewModel : ObservableObject
{
    private readonly IVolumeInventory _inventory;
    private readonly Func<IPrivilegedOperationClient> _clientFactory;
    private readonly IOperationHistory _history;
    private readonly SafetyPolicy _safetyPolicy;
    private readonly ConfirmationPhraseService _phraseService;
    private readonly IClock _clock;
    private readonly IUiDispatcher _dispatcher;

    private MainViewState _currentState = MainViewState.Dashboard;
    private VolumeCardViewModel? _selectedVolume;
    private bool _isBusy;
    private string _errorMessage = string.Empty;
    private string _resultMessage = string.Empty;
    private bool _resultIsSuccess;
    private bool _isAdvancedMode;

    private CancellationTokenSource? _operationCts;
    private CancellationTokenSource? _countdownCts;
    private IPrivilegedOperationClient? _currentClient;
    private VolumeSnapshot? _currentSnapshot;
    private DateTimeOffset _operationStartTime;

    public MainViewModel(
        IVolumeInventory inventory,
        Func<IPrivilegedOperationClient> clientFactory,
        IOperationHistory history,
        SafetyPolicy safetyPolicy,
        ConfirmationPhraseService phraseService,
        IClock clock,
        IUiDispatcher dispatcher)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _safetyPolicy = safetyPolicy ?? throw new ArgumentNullException(nameof(safetyPolicy));
        _phraseService = phraseService ?? throw new ArgumentNullException(nameof(phraseService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        Volumes = new ObservableCollection<VolumeCardViewModel>();
        HistoryEntries = new ObservableCollection<HistoryEntryViewModel>();
        Confirmation = new ConfirmationViewModel(OnStartOperation, OnCancelConfirmation);
        Operation = new OperationViewModel(OnCancelOperation);

        RefreshCommand = new RelayCommand(_ => _ = RefreshAsync(), _ => !IsBusy);
        ShowHistoryCommand = new RelayCommand(_ => _ = ShowHistoryAsync(), _ => !IsBusy);
        ShowSettingsCommand = new RelayCommand(_ => CurrentState = MainViewState.Settings, _ => !IsBusy);
        ShowHelpCommand = new RelayCommand(_ => CurrentState = MainViewState.Help, _ => !IsBusy);
        BackCommand = new RelayCommand(_ => OnBackToDashboard(), _ => !IsBusy);
        ResultOkCommand = new RelayCommand(_ => OnBackToDashboard());
    }

    public ObservableCollection<VolumeCardViewModel> Volumes
    {
        get;
    }

    public ObservableCollection<HistoryEntryViewModel> HistoryEntries
    {
        get;
    }

    public ConfirmationViewModel Confirmation
    {
        get;
    }

    public OperationViewModel Operation
    {
        get;
    }

    public MainViewState CurrentState
    {
        get => _currentState;
        set
        {
            if (SetProperty(ref _currentState, value))
            {
                OnPropertyChanged(nameof(IsDashboardVisible));
                OnPropertyChanged(nameof(IsConfirmationVisible));
                OnPropertyChanged(nameof(IsProgressVisible));
                OnPropertyChanged(nameof(IsResultVisible));
                OnPropertyChanged(nameof(IsHistoryVisible));
                OnPropertyChanged(nameof(IsSettingsVisible));
                OnPropertyChanged(nameof(IsHelpVisible));
            }
        }
    }

    public bool IsAdvancedMode
    {
        get => _isAdvancedMode;
        set
        {
            if (SetProperty(ref _isAdvancedMode, value))
            {
                _ = RefreshAsync();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ShowHistoryCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ShowSettingsCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ShowHelpCommand).RaiseCanExecuteChanged();
                ((RelayCommand)BackCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string ResultMessage
    {
        get => _resultMessage;
        set => SetProperty(ref _resultMessage, value);
    }

    public bool ResultIsSuccess
    {
        get => _resultIsSuccess;
        set => SetProperty(ref _resultIsSuccess, value);
    }

    public ICommand RefreshCommand
    {
        get;
    }

    public ICommand ShowHistoryCommand
    {
        get;
    }

    public ICommand ShowSettingsCommand
    {
        get;
    }

    public ICommand ShowHelpCommand
    {
        get;
    }

    public ICommand BackCommand
    {
        get;
    }

    public ICommand ResultOkCommand
    {
        get;
    }

    public bool IsDashboardVisible => CurrentState == MainViewState.Dashboard;
    public bool IsConfirmationVisible => CurrentState == MainViewState.Confirmation;
    public bool IsProgressVisible => CurrentState == MainViewState.Progress;
    public bool IsResultVisible => CurrentState == MainViewState.Result;
    public bool IsHistoryVisible => CurrentState == MainViewState.History;
    public bool IsSettingsVisible => CurrentState == MainViewState.Settings;
    public bool IsHelpVisible => CurrentState == MainViewState.Help;

    public async Task InitializeAsync()
    {
        await RefreshAsync().ConfigureAwait(true);
    }

    public async Task RefreshAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            IReadOnlyList<VolumeIdentity> volumes = await _inventory.GetVolumesAsync(CancellationToken.None).ConfigureAwait(true);
            Volumes.Clear();
            foreach (VolumeIdentity volume in volumes)
            {
                EligibilityDecision decision = _safetyPolicy.Evaluate(volume, IsAdvancedMode);
                var vm = new VolumeCardViewModel(volume, decision, OnSelectVolume);
                Volumes.Add(vm);
            }

            OnPropertyChanged(nameof(Volumes));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể đọc danh sách volume: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnSelectVolume(VolumeCardViewModel volume)
    {
        if (IsBusy)
        {
            return;
        }

        _selectedVolume = volume;
        Confirmation.Volume = volume;
        Confirmation.CountdownSeconds = 3;
        CancelCountdown();
        _countdownCts = new CancellationTokenSource();
        _ = RunConfirmationCountdownAsync(_countdownCts.Token);
        CurrentState = MainViewState.Confirmation;
    }

    private async Task RunConfirmationCountdownAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (Confirmation.CountdownSeconds > 0 && CurrentState == MainViewState.Confirmation)
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(true);
                Confirmation.CountdownSeconds--;
                Confirmation.OnCountdownChanged();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelCountdown()
    {
        _countdownCts?.Cancel();
        _countdownCts?.Dispose();
        _countdownCts = null;
    }

    private void OnCancelConfirmation()
    {
        CancelCountdown();
        _selectedVolume = null;
        Confirmation.Reset();
        CurrentState = MainViewState.Dashboard;
    }

    private void OnBackToDashboard()
    {
        CancelCountdown();
        _selectedVolume = null;
        Confirmation.Reset();
        Operation.Reset();
        CurrentState = MainViewState.Dashboard;
    }

    private async void OnStartOperation()
    {
        try
        {
            await StartSelectedOperationAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // Fire-and-forget từ command: không được để exception bị nuốt.
            IsBusy = false;
            ErrorMessage = $"Thao tác kết thúc bất thường: {ex.Message}";
            ResultIsSuccess = false;
            ResultMessage = $"Thất bại: {ex.Message}";
            CurrentState = MainViewState.Result;
        }
    }

    public async Task StartSelectedOperationAsync()
    {
        if (_selectedVolume == null || IsBusy)
        {
            return;
        }

        if (!_phraseService.Validate(Confirmation.Phrase, _selectedVolume.Decision.Action, _selectedVolume.DriveLetter))
        {
            ErrorMessage = "Cụm xác nhận không đúng.";
            return;
        }

        VolumeIdentity? refreshed = await _inventory.RefreshVolumeAsync(_selectedVolume.DriveLetter, CancellationToken.None).ConfigureAwait(true);
        if (refreshed == null)
        {
            ErrorMessage = "Volume không còn khả dụng sau khi quét lại.";
            return;
        }

        if (!string.Equals(refreshed.VolumeGuid, _selectedVolume.Identity.VolumeGuid, StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Volume GUID đã thay đổi. Hủy thao tác để tránh chọn nhầm ổ.";
            return;
        }

        EligibilityDecision refreshedDecision = _safetyPolicy.Evaluate(refreshed, IsAdvancedMode);
        if (!refreshedDecision.IsEligible)
        {
            ErrorMessage = $"Volume không còn đủ điều kiện: {string.Join("; ", refreshedDecision.Reasons)}";
            return;
        }

        string operationId = Guid.NewGuid().ToString("N");
        var snapshot = new VolumeSnapshot(refreshed, _clock.UtcNow);
        var plan = new OperationPlan(
            operationId,
            _clock.UtcNow,
            snapshot,
            refreshedDecision.Action,
            Confirmation.Phrase.Trim());

        IsBusy = true;
        Operation.Reset();
        Operation.OperationId = operationId;
        Operation.IsRunning = true;
        Operation.CanCancel = true;
        CurrentState = MainViewState.Progress;

        _operationCts = new CancellationTokenSource();
        await RunOperationAsync(plan, _operationCts.Token).ConfigureAwait(true);
    }

    private async Task RunOperationAsync(OperationPlan plan, CancellationToken cancellationToken)
    {
        _currentSnapshot = plan.Snapshot;
        _operationStartTime = _clock.UtcNow;
        DateTimeOffset startTime = _operationStartTime;
        OperationCompletion completion;
        IPrivilegedOperationClient client = _clientFactory();
        _currentClient = client;

        try
        {
            bool connected = await client.ConnectAsync(plan.OperationId, cancellationToken).ConfigureAwait(true);
            if (!connected)
            {
                completion = new OperationCompletion(OperationResult.Failed, 1, OperationErrorCategory.ElevationDenied, "Không thể kết nối worker đặc quyền.");
            }
            else
            {
                var progress = new Progress<string>(line =>
                {
                    _dispatcher.Invoke(() => Operation.AppendOutput(line));
                });

                DateTimeOffset timerStart = _clock.UtcNow;
                using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task timerTask = RunElapsedTimerAsync(timerStart, timerCts.Token);

                try
                {
                    completion = await client.RunOperationAsync(plan, progress, cancellationToken).ConfigureAwait(true);
                }
                finally
                {
                    timerCts.Cancel();
                    try
                    {
                        await timerTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            completion = new OperationCompletion(OperationResult.Interrupted, 1, OperationErrorCategory.Cancelled, "Thao tác bị hủy.");
        }
        catch (Exception ex)
        {
            completion = new OperationCompletion(OperationResult.Failed, 1, OperationErrorCategory.Unexpected, ex.Message);
        }
        finally
        {
            await client.DisposeAsync().ConfigureAwait(false);
            _currentClient = null;
            Operation.CanCancel = false;
            Operation.IsRunning = false;
            IsBusy = false;
        }

        await AppendHistoryAsync(plan, startTime, completion).ConfigureAwait(true);
        ShowResult(completion);
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task AppendHistoryAsync(OperationPlan plan, DateTimeOffset startTime, OperationCompletion completion)
    {
        var entry = new OperationJournalEntry(
            plan.OperationId,
            _clock.UtcNow,
            GetAppVersion(),
            plan.ProposedAction.ToString(),
            plan.Snapshot.Identity.DriveLetter,
            plan.Snapshot.Identity.VolumeGuid,
            plan.Snapshot.Identity.MediaType.ToString(),
            plan.Snapshot.Identity.BusType.ToString(),
            plan.Snapshot.Identity.FileSystem,
            plan.Snapshot.Identity.CapacityBytes,
            plan.Snapshot.Identity.FreeBytes,
            startTime,
            _clock.UtcNow,
            _clock.UtcNow - startTime,
            completion.ExitCode,
            completion.Result.ToString(),
            completion.ErrorCategory.ToString(),
            string.Join("\n", Operation.OutputLines.TakeLast(20)));

        try
        {
            await _history.AppendAsync(entry, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Operation.AppendOutput($"Lỗi ghi lịch sử: {ex.Message}");
        }
    }

    private void ShowResult(OperationCompletion completion)
    {
        ResultIsSuccess = completion.Result == OperationResult.Completed;
        ResultMessage = completion.Result switch
        {
            OperationResult.Completed => BuildCompletionReport(),
            OperationResult.Interrupted => "Thao tác bị dừng không được coi là hoàn thành.",
            OperationResult.Failed => $"Thất bại: {completion.Message} (mã lỗi {completion.ErrorCategory})",
            _ => $"Kết quả không xác định: {completion.Result}"
        };
        CurrentState = MainViewState.Result;
        ShowCompletionToast(completion);
    }

    private void ShowCompletionToast(OperationCompletion completion)
    {
        try
        {
            string title = completion.Result == OperationResult.Completed
                ? "SafeFreeSpace - Hoàn thành"
                : "SafeFreeSpace - Kết thúc";
            string message = completion.Result == OperationResult.Completed
                ? $"Thao tác {_currentSnapshot?.Identity.DriveLetter ?? "?"}: đã hoàn thành."
                : $"Thao tác {_currentSnapshot?.Identity.DriveLetter ?? "?"}: {completion.Message}";

            var notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Visible = true,
                Icon = System.Drawing.SystemIcons.Information,
                BalloonTipTitle = title,
                BalloonTipText = message
            };
            // Giữ icon sống cho tới khi balloon đóng; dispose ngay sau ShowBalloonTip sẽ khiến balloon không hiện.
            notifyIcon.BalloonTipClosed += (_, _) => notifyIcon.Dispose();
            notifyIcon.BalloonTipClicked += (_, _) => notifyIcon.Dispose();
            notifyIcon.ShowBalloonTip(5000);
            // Fallback: không leak nếu event balloon không bao giờ bắn.
            _ = Task.Delay(TimeSpan.FromMinutes(1))
                    .ContinueWith(_ => notifyIcon.Dispose(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
        catch (Exception)
        {
            // Toast is best-effort; ignore failures.
        }
    }

    private string BuildCompletionReport()
    {
        if (_currentSnapshot == null)
        {
            return "Thao tác đã hoàn thành.";
        }

        TimeSpan duration = _clock.UtcNow - _operationStartTime;
        long freeBytes = _currentSnapshot.Identity.FreeBytes;
        double bytesPerSecond = duration.TotalSeconds > 0 ? freeBytes / duration.TotalSeconds : 0;
        string speedText = FormatBytes((long)bytesPerSecond) + "/s";

        return "Thao tác đã hoàn thành.\n\n"
               + $"Thời gian: {duration:hh\\:mm\\:ss}\n"
               + $"Dung lượng vùng trống: {FormatBytes(freeBytes)}\n"
               + $"Tốc độ trung bình: {speedText}";
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

    private async void OnCancelOperation()
    {
        try
        {
            if (_currentClient != null)
            {
                await _currentClient.CancelOperationAsync(CancellationToken.None).ConfigureAwait(true);
            }

            _operationCts?.Cancel();
            Operation.CanCancel = false;
        }
        catch (Exception ex)
        {
            Operation.AppendOutput($"Lỗi khi hủy thao tác: {ex.Message}");
        }
    }

    private async Task ShowHistoryAsync()
    {
        IsBusy = true;
        try
        {
            IReadOnlyList<OperationJournalEntry> entries = await _history.ReadRecentAsync(50, CancellationToken.None).ConfigureAwait(true);
            HistoryEntries.Clear();
            foreach (OperationJournalEntry entry in entries)
            {
                HistoryEntries.Add(new HistoryEntryViewModel(entry));
            }

            CurrentState = MainViewState.History;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể đọc lịch sử: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string GetAppVersion()
    {
        return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
    }

    private async Task RunElapsedTimerAsync(DateTimeOffset startTime, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            TimeSpan elapsed = _clock.UtcNow - startTime;
            _dispatcher.Invoke(() => Operation.ElapsedText = $"{elapsed:hh\\:mm\\:ss}");
        }
    }
}
