namespace SafeFreeSpace.ElevatedWorker.Executors;

using SafeFreeSpace.Contracts;

public sealed class CancelOperationExecutor : IOperationExecutor
{
    public WorkerOperationType OperationType => WorkerOperationType.CancelOperation;

    public Task<WorkerResponse> ExecuteAsync(
        WorkerRequest request,
        IProgress<OperationProgress> progress,
        CancellationToken cancellationToken)
    {
        progress.Report(new OperationProgress(request.OperationId, "Cancelling", "Cancellation acknowledged."));
        return Task.FromResult(new WorkerResponse(false, OperationResult.Interrupted, "Cancelled", "Operation cancelled by user."));
    }
}
