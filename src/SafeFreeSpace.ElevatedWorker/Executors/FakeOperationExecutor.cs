namespace SafeFreeSpace.ElevatedWorker.Executors;

using SafeFreeSpace.Contracts;

public sealed class FakeOperationExecutor : IOperationExecutor
{
    public WorkerOperationType OperationType => WorkerOperationType.WipeHddFreeSpace;

    public async Task<WorkerResponse> ExecuteAsync(
        WorkerRequest request,
        IProgress<OperationProgress> progress,
        CancellationToken cancellationToken)
    {
        progress.Report(new OperationProgress(request.OperationId, "Running", "Fake executor started."));
        await Task.Delay(50, cancellationToken);
        progress.Report(new OperationProgress(request.OperationId, "Running", "Fake executor finished."));

        return new WorkerResponse(true, OperationResult.Completed, "None", null);
    }
}
