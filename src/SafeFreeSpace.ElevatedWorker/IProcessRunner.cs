namespace SafeFreeSpace.ElevatedWorker;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        Core.Services.BuiltCommand command,
        IProgress<string> outputProgress,
        CancellationToken cancellationToken);
}
