namespace SafeFreeSpace.Core.Interfaces;

using SafeFreeSpace.Contracts;
using SafeFreeSpace.Core.Models;

public interface IPrivilegedOperationClient : IAsyncDisposable
{
    Task<bool> ConnectAsync(string operationId, CancellationToken cancellationToken = default);

    Task<OperationCompletion> RunOperationAsync(
        OperationPlan plan,
        IProgress<string> progress,
        CancellationToken cancellationToken = default);

    Task CancelOperationAsync(CancellationToken cancellationToken = default);
}

public sealed record OperationCompletion(
    OperationResult Result,
    int ExitCode,
    OperationErrorCategory ErrorCategory,
    string? Message);
