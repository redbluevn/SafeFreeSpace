namespace SafeFreeSpace.ElevatedWorker.Executors;

using SafeFreeSpace.Contracts;
using SafeFreeSpace.Core.Interfaces;
using SafeFreeSpace.Core.Models;
using SafeFreeSpace.Core.Services;

public sealed class RetrimExecutor : IOperationExecutor
{
    private readonly IProcessRunner _runner;
    private readonly IVolumeInventory _inventory;

    public RetrimExecutor(IProcessRunner runner, IVolumeInventory inventory)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
    }

    public WorkerOperationType OperationType => WorkerOperationType.RetrimSsd;

    public async Task<WorkerResponse> ExecuteAsync(
        WorkerRequest request,
        IProgress<OperationProgress> progress,
        CancellationToken cancellationToken)
    {
        char driveLetter = ExecutorCommon.ValidateDriveLetter(request.Volume.DriveLetter);
        VolumeIdentity? current = await _inventory.RefreshVolumeAsync(request.Volume.DriveLetter, cancellationToken);

        if (current == null)
        {
            return ExecutorCommon.Fail(OperationErrorCategory.VolumeChanged, "Volume no longer available.");
        }

        if (!string.Equals(current.VolumeGuid, request.Volume.VolumeGuid, StringComparison.OrdinalIgnoreCase))
        {
            return ExecutorCommon.Fail(OperationErrorCategory.VolumeChanged, "Volume GUID mismatch.");
        }

        if (!string.Equals(current.FileSystem, request.Volume.FileSystem, StringComparison.OrdinalIgnoreCase))
        {
            return ExecutorCommon.Fail(OperationErrorCategory.UnsupportedVolume, "File system changed.");
        }

        bool isSsdLike = current.MediaType == DriveMediaType.Ssd ||
                         current.MediaType == DriveMediaType.Scm ||
                         current.BusType == DriveBusType.Nvme;

        if (!isSsdLike)
        {
            return ExecutorCommon.Fail(OperationErrorCategory.UnknownMediaType, "Media type is not SSD/NVMe.");
        }

        if (current.IsReadOnly)
        {
            return ExecutorCommon.Fail(OperationErrorCategory.VolumeReadOnly, "Volume is read-only.");
        }

        if (current.IsDirty)
        {
            return ExecutorCommon.Fail(OperationErrorCategory.VolumeDirty, "Volume is dirty.");
        }

        var commandBuilder = new CommandBuilder();
        BuiltCommand command = commandBuilder.BuildSsdRetrim(driveLetter);

        progress.Report(new OperationProgress(request.OperationId, "Running", "Sending ReTrim request."));

        var outputProgress = new Progress<string>(line =>
        {
            progress.Report(new OperationProgress(request.OperationId, "Running", ExecutorCommon.SanitizeOutput(line)));
        });

        ProcessResult result;
        try
        {
            result = await _runner.RunAsync(command, outputProgress, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return ExecutorCommon.Fail(OperationErrorCategory.ToolNotFound, "PowerShell not found.");
        }
        catch (InvalidOperationException ex)
        {
            return ExecutorCommon.Fail(OperationErrorCategory.ProcessStartFailed, ex.Message);
        }

        if (result.WasCancelled)
        {
            return new WorkerResponse(false, OperationResult.Interrupted, "Cancelled", "Operation was cancelled.");
        }

        if (result.ExitCode != 0)
        {
            return ExecutorCommon.ExitCodeFailure(result.ExitCode, result.StandardError);
        }

        return new WorkerResponse(true, OperationResult.Completed, "None", null);
    }
}
