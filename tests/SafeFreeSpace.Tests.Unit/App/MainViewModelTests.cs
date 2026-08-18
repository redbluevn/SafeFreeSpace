namespace SafeFreeSpace.Tests.Unit.App;

using SafeFreeSpace.App.Mvvm;
using SafeFreeSpace.App.ViewModels;
using SafeFreeSpace.Contracts;
using SafeFreeSpace.Core.Interfaces;
using SafeFreeSpace.Core.Models;
using SafeFreeSpace.Core.Services;
using Xunit;

public class MainViewModelTests
{
    private static VolumeIdentity CreateHddVolume(string driveLetter = "C")
    {
        return new VolumeIdentity(
            driveLetter,
            $"{{{Guid.NewGuid()}}}",
            "TESTVOL",
            "NTFS",
            100_000_000_000,
            50_000_000_000,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            BitLockerState.Unlocked,
            DriveMediaType.Hdd,
            DriveBusType.Sata,
            "Model",
            "Healthy");
    }

    private static MainViewModel CreateViewModel(
        IVolumeInventory? inventory = null,
        Func<IPrivilegedOperationClient>? clientFactory = null,
        IOperationHistory? history = null,
        IClock? clock = null)
    {
        return new MainViewModel(
            inventory ?? new FakeInventory(),
            clientFactory ?? (() => new FakeClient(OperationResult.Completed)),
            history ?? new InMemoryOperationHistory(),
            new SafetyPolicy(),
            new ConfirmationPhraseService(),
            clock ?? new FakeClock(),
            new ImmediateDispatcher());
    }

    [Fact]
    public async Task Refresh_PopulatesVolumes()
    {
        var inventory = new FakeInventory(CreateHddVolume());
        MainViewModel vm = CreateViewModel(inventory);

        await vm.RefreshAsync();

        Assert.Single(vm.Volumes);
        Assert.Equal("C", vm.Volumes[0].DriveLetter);
    }

    [Fact]
    public async Task SelectVolume_OpensConfirmation()
    {
        var inventory = new FakeInventory(CreateHddVolume());
        MainViewModel vm = CreateViewModel(inventory);
        await vm.RefreshAsync();

        vm.Volumes[0].SelectCommand.Execute(null);

        Assert.Equal(MainViewState.Confirmation, vm.CurrentState);
        Assert.Equal("WIPE FREE SPACE C:", vm.Confirmation.ExpectedPhrase);
    }

    [Fact]
    public async Task Start_WrongPhrase_ShowsError()
    {
        var inventory = new FakeInventory(CreateHddVolume());
        MainViewModel vm = CreateViewModel(inventory);
        await vm.RefreshAsync();
        vm.Volumes[0].SelectCommand.Execute(null);
        vm.Confirmation.Phrase = "WRONG PHRASE";

        await vm.StartSelectedOperationAsync();

        Assert.Contains("Cụm xác nhận", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Start_CorrectPhrase_Completes()
    {
        var inventory = new FakeInventory(CreateHddVolume());
        MainViewModel vm = CreateViewModel(inventory);
        await vm.RefreshAsync();
        vm.Volumes[0].SelectCommand.Execute(null);
        vm.Confirmation.CountdownSeconds = 0;
        vm.Confirmation.OnCountdownChanged();
        vm.Confirmation.Phrase = "WIPE FREE SPACE C:";

        await vm.StartSelectedOperationAsync();

        Assert.Equal(MainViewState.Result, vm.CurrentState);
        Assert.True(vm.ResultIsSuccess);
    }

    [Fact]
    public async Task Start_VolumeGuidChanged_Fails()
    {
        VolumeIdentity first = CreateHddVolume();
        VolumeIdentity changed = first with
        {
            VolumeGuid = $"{{{Guid.NewGuid()}}}"
        };
        Assert.NotEqual(first.VolumeGuid, changed.VolumeGuid);
        var inventory = new FakeInventory(first, changed);
        MainViewModel vm = CreateViewModel(inventory);
        await vm.RefreshAsync();
        vm.Volumes[0].SelectCommand.Execute(null);
        vm.Confirmation.CountdownSeconds = 0;
        vm.Confirmation.OnCountdownChanged();
        vm.Confirmation.Phrase = "WIPE FREE SPACE C:";

        await vm.StartSelectedOperationAsync();

        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage), $"Expected error, state={vm.CurrentState}, message={vm.ErrorMessage}");
        Assert.Contains("Volume GUID", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Start_KeepsIsBusyWhileOperationRunning()
    {
        var inventory = new FakeInventory(CreateHddVolume());
        var gate = new TaskCompletionSource<OperationCompletion>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new BlockingClient(gate);
        MainViewModel vm = CreateViewModel(inventory, () => client);
        await vm.RefreshAsync();
        vm.Volumes[0].SelectCommand.Execute(null);
        vm.Confirmation.CountdownSeconds = 0;
        vm.Confirmation.OnCountdownChanged();
        vm.Confirmation.Phrase = "WIPE FREE SPACE C:";

        Task run = vm.StartSelectedOperationAsync();

        Assert.True(vm.IsBusy);
        Assert.True(vm.Operation.IsRunning);
        Assert.False(vm.RefreshCommand.CanExecute(null));
        // Cancel phải bấm được khi đang chạy.
        Assert.True(vm.Operation.CancelCommand.CanExecute(null));

        gate.SetResult(new OperationCompletion(OperationResult.Completed, 0, OperationErrorCategory.None, null));
        await run;

        Assert.False(vm.IsBusy);
        Assert.False(vm.Operation.IsRunning);
        Assert.True(vm.RefreshCommand.CanExecute(null));
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Invoke(Action action) => action();

        public void BeginInvoke(Action action) => action();
    }

    private sealed class FakeInventory : IVolumeInventory
    {
        private readonly VolumeIdentity[] _volumes;

        public FakeInventory(params VolumeIdentity[] volumes)
        {
            _volumes = volumes;
        }

        public Task<IReadOnlyList<VolumeIdentity>> GetVolumesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<VolumeIdentity>>(_volumes.ToList());
        }

        public Task<VolumeIdentity?> RefreshVolumeAsync(string driveLetter, CancellationToken cancellationToken = default)
        {
            VolumeIdentity? match = _volumes.LastOrDefault(v => v.DriveLetter.Equals(driveLetter, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(match);
        }
    }

    private sealed class FakeClient : IPrivilegedOperationClient
    {
        private readonly OperationResult _result;

        public FakeClient(OperationResult result)
        {
            _result = result;
        }

        public Task<bool> ConnectAsync(string operationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<OperationCompletion> RunOperationAsync(OperationPlan plan, IProgress<string> progress, CancellationToken cancellationToken = default)
        {
            progress.Report("Fake progress");
            return Task.FromResult(new OperationCompletion(_result, 0, OperationErrorCategory.None, null));
        }

        public Task CancelOperationAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    private sealed class BlockingClient : IPrivilegedOperationClient
    {
        private readonly TaskCompletionSource<OperationCompletion> _gate;

        public BlockingClient(TaskCompletionSource<OperationCompletion> gate)
        {
            _gate = gate;
        }

        public Task<bool> ConnectAsync(string operationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<OperationCompletion> RunOperationAsync(OperationPlan plan, IProgress<string> progress, CancellationToken cancellationToken = default)
        {
            return _gate.Task;
        }

        public Task CancelOperationAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 7, 17, 10, 0, 0, TimeSpan.Zero);
    }
}
