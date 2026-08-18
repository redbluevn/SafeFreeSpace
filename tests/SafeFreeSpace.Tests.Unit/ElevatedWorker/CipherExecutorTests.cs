namespace SafeFreeSpace.Tests.Unit.ElevatedWorker;

using SafeFreeSpace.Contracts;
using SafeFreeSpace.Core.Interfaces;
using SafeFreeSpace.Core.Models;
using SafeFreeSpace.Core.Services;
using SafeFreeSpace.ElevatedWorker;
using SafeFreeSpace.ElevatedWorker.Executors;
using Xunit;

public class CipherExecutorTests
{
    private static VolumeIdentityDto CreateDto(string driveLetter = "C", string volumeGuid = "{GUID}")
    {
        return new VolumeIdentityDto(
            driveLetter,
            volumeGuid,
            "VOL",
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
            "Unlocked",
            "Hdd",
            "Sata",
            "Model",
            "Healthy");
    }

    private static VolumeIdentity CreateIdentity(string driveLetter = "C", string volumeGuid = "{GUID}")
    {
        return new VolumeIdentity(
            driveLetter,
            volumeGuid,
            "VOL",
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

    [Fact]
    public async Task VolumeGuidMismatch_Fails()
    {
        var inventory = new FakeInventory(CreateIdentity(volumeGuid: "{OTHER}"));
        var runner = new FakeRunner(0);
        var executor = new CipherExecutor(runner, inventory);

        var request = new WorkerRequest(1, "op", "nonce", WorkerOperationType.WipeHddFreeSpace, CreateDto(volumeGuid: "{GUID}"));
        var response = await executor.ExecuteAsync(request, new Progress<OperationProgress>(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(OperationResult.Failed, response.Result);
        Assert.Contains("Volume GUID", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MediaTypeNotHdd_Fails()
    {
        var identity = CreateIdentity();
        identity = identity with
        {
            MediaType = DriveMediaType.Ssd
        };
        var inventory = new FakeInventory(identity);
        var runner = new FakeRunner(0);
        var executor = new CipherExecutor(runner, inventory);

        var request = new WorkerRequest(1, "op", "nonce", WorkerOperationType.WipeHddFreeSpace, CreateDto());
        var response = await executor.ExecuteAsync(request, new Progress<OperationProgress>(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("HDD", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuccessfulRun_ReturnsCompleted()
    {
        var inventory = new FakeInventory(CreateIdentity());
        var runner = new FakeRunner(0);
        var executor = new CipherExecutor(runner, inventory);

        var request = new WorkerRequest(1, "op", "nonce", WorkerOperationType.WipeHddFreeSpace, CreateDto());
        var response = await executor.ExecuteAsync(request, new Progress<OperationProgress>(), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(OperationResult.Completed, response.Result);
        Assert.True(runner.WasCalled);
        Assert.Equal('C', runner.ReceivedDriveLetter);
    }

    [Fact]
    public async Task NonZeroExitCode_Fails()
    {
        var inventory = new FakeInventory(CreateIdentity());
        var runner = new FakeRunner(1);
        var executor = new CipherExecutor(runner, inventory);

        var request = new WorkerRequest(1, "op", "nonce", WorkerOperationType.WipeHddFreeSpace, CreateDto());
        var response = await executor.ExecuteAsync(request, new Progress<OperationProgress>(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(OperationResult.Failed, response.Result);
    }

    [Fact]
    public async Task CancelledRun_ReturnsInterrupted()
    {
        var inventory = new FakeInventory(CreateIdentity());
        var runner = new FakeRunner(0, wasCancelled: true);
        var executor = new CipherExecutor(runner, inventory);

        var request = new WorkerRequest(1, "op", "nonce", WorkerOperationType.WipeHddFreeSpace, CreateDto());
        var response = await executor.ExecuteAsync(request, new Progress<OperationProgress>(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(OperationResult.Interrupted, response.Result);
    }

    private sealed class FakeInventory : IVolumeInventory
    {
        private readonly VolumeIdentity? _identity;

        public FakeInventory(VolumeIdentity? identity)
        {
            _identity = identity;
        }

        public Task<IReadOnlyList<VolumeIdentity>> GetVolumesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<VolumeIdentity>>(_identity == null ? Array.Empty<VolumeIdentity>() : new[] { _identity });
        }

        public Task<VolumeIdentity?> RefreshVolumeAsync(string driveLetter, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_identity);
        }
    }

    private sealed class FakeRunner : IProcessRunner
    {
        private readonly int _exitCode;
        private readonly bool _wasCancelled;

        public FakeRunner(int exitCode, bool wasCancelled = false)
        {
            _exitCode = exitCode;
            _wasCancelled = wasCancelled;
        }

        public bool WasCalled
        {
            get; private set;
        }

        public char ReceivedDriveLetter
        {
            get; private set;
        }

        public Task<ProcessResult> RunAsync(BuiltCommand command, IProgress<string> outputProgress, CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedDriveLetter = command.Arguments[0][3];
            return Task.FromResult(new ProcessResult(_exitCode, string.Empty, string.Empty, _wasCancelled));
        }
    }
}
