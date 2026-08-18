namespace SafeFreeSpace.ElevatedWorker.Executors;

using SafeFreeSpace.Contracts;

public interface IOperationExecutor
{
    WorkerOperationType OperationType
    {
        get;
    }

    Task<WorkerResponse> ExecuteAsync(
        WorkerRequest request,
        IProgress<OperationProgress> progress,
        CancellationToken cancellationToken);
}
