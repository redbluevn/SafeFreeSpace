namespace SafeFreeSpace.Tests.Unit.ElevatedWorker;

using SafeFreeSpace.Contracts;
using SafeFreeSpace.Core.Interfaces;
using SafeFreeSpace.Core.Models;
using SafeFreeSpace.Core.Services;
using SafeFreeSpace.ElevatedWorker;
using SafeFreeSpace.ElevatedWorker.Executors;
using Xunit;

public class RetrimExecutorTests
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
            "Ssd",
            "Sata",
            "Model",
            "Healthy");
    }

    private static VolumeIdentity CreateIdentity(string driveLetter = "C", string volumeGuid = "{GUID}", DriveMediaType mediaType = DriveMediaType.Ssd)
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
            mediaType,
            DriveBusType.Sata,
            "Model",
            "Healthy");
    }

    [Fact]
    public async Task HddMediaType_Fails()
    {
        var inventory = new FakeInventory(CreateIdentity(mediaType: DriveMediaType.Hdd));
        var runner = new FakeRunner(0);
        var executor = new RetrimExecutor(runner, inventory);

        var request = new WorkerRequest(1, "op", "nonce", WorkerOperationType.RetrimSsd, CreateDto());
        var response = await executor.ExecuteAsync(request, new Progress<OperationProgress>(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("SSD", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DirtyVolume_Fails()
    {
        var identity = CreateIdentity();
        identity = identity with
        {
            IsDirty = true
        };
        var inventory = new FakeInventory(identity);
        var runner = new FakeRunner(0);
        var executor = new RetrimExecutor(runner, inventory);

        var request = new WorkerRequest(1, "op", "nonce", WorkerOperationType.RetrimSsd, CreateDto());
        var response = await executor.ExecuteAsync(request, new Progress<OperationProgress>(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(OperationResult.Failed, response.Result);
        Assert.Contains("dirty", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(runner.WasCalled);
    }

    [Fact]
    public async Task NvmeBus_AllowsRetrim()
    {
        var identity = CreateIdentity(mediaType: DriveMediaType.Unknown, volumeGuid: "{GUID}");
        identity = identity with
        {
            BusType = DriveBusType.Nvme
        };
        var inventory = new FakeInventory(identity);
        var runner = new FakeRunner(0);
        var executor = new RetrimExecutor(runner, inventory);

        var request = new WorkerRequest(1, "op", "nonce", WorkerOperationType.RetrimSsd, CreateDto());
        var response = await executor.ExecuteAsync(request, new Progress<OperationProgress>(), CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(runner.WasCalled);
    }

    [Fact]
    public async Task SuccessfulRun_ReturnsCompleted()
    {
        var inventory = new FakeInventory(CreateIdentity());
        var runner = new FakeRunner(0);
        var executor = new RetrimExecutor(runner, inventory);

        var request = new WorkerRequest(1, "op", "nonce", WorkerOperationType.RetrimSsd, CreateDto());
        var response = await executor.ExecuteAsync(request, new Progress<OperationProgress>(), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(OperationResult.Completed, response.Result);
        Assert.True(runner.WasCalled);
        Assert.Contains("powershell.exe", runner.ReceivedExecutable, StringComparison.OrdinalIgnoreCase);
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

        public FakeRunner(int exitCode)
        {
            _exitCode = exitCode;
        }

        public bool WasCalled
        {
            get; private set;
        }

        public string ReceivedExecutable { get; private set; } = string.Empty;

        public Task<ProcessResult> RunAsync(BuiltCommand command, IProgress<string> outputProgress, CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedExecutable = command.ExecutablePath;
            return Task.FromResult(new ProcessResult(_exitCode, string.Empty, string.Empty, false));
        }
    }
}
