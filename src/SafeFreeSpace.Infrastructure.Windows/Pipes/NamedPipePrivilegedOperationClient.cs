namespace SafeFreeSpace.Infrastructure.Windows.Pipes;

using System.Security.Cryptography;
using SafeFreeSpace.Contracts;
using SafeFreeSpace.Core.Interfaces;
using SafeFreeSpace.Core.Models;

public sealed class NamedPipePrivilegedOperationClient : IPrivilegedOperationClient
{
    private readonly ElevatedWorkerLauncher _launcher;
    private WorkerPipeServer? _server;
    private System.Diagnostics.Process? _workerProcess;
    private bool _disposed;

    public NamedPipePrivilegedOperationClient(ElevatedWorkerLauncher? launcher = null)
    {
        _launcher = launcher ?? new ElevatedWorkerLauncher();
    }

    public async Task<bool> ConnectAsync(string operationId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(operationId);

        string pipeName = $"sfs-{Guid.NewGuid():N}";
        string nonce = GenerateNonce();

        // Một nguồn OperationId duy nhất: handshake, launch args và request đều dùng OperationId của operation sẽ chạy.
        _server = new WorkerPipeServer(pipeName, nonce, operationId);

        try
        {
            _workerProcess = _launcher.Launch(pipeName, nonce, operationId);
            await _server.AcceptAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            await DisposeAsync().ConfigureAwait(false);
            return false;
        }
    }

    public async Task<OperationCompletion> RunOperationAsync(
        OperationPlan plan,
        IProgress<string> progress,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_server == null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var request = new WorkerRequest(
            ProtocolConstants.CurrentProtocolVersion,
            plan.OperationId,
            string.Empty,
            MapOperationType(plan.ProposedAction),
            MapToDto(plan.Snapshot.Identity));

        await _server.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

        var progressAdapter = new Progress<OperationProgress>(p =>
        {
            string message = string.IsNullOrEmpty(p.OutputChunk)
                ? p.State
                : $"[{p.State}] {p.OutputChunk}";
            progress.Report(message);
        });

        using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task progressTask = ReadProgressAsync(_server, progressAdapter, progressCts.Token);

        WorkerResponse response;
        try
        {
            response = await _server.ReadResponseAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            progressCts.Cancel();
            try
            {
                await progressTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        OperationResult result = response.Result;
        OperationErrorCategory category = MapErrorCategory(response.ErrorCategory);
        int exitCode = response.Success ? 0 : 1;
        if (!response.Success && category == OperationErrorCategory.None)
        {
            category = OperationErrorCategory.Unexpected;
        }

        return new OperationCompletion(result, exitCode, category, response.Message);
    }

    public async Task CancelOperationAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_server == null)
        {
            return;
        }

        try
        {
            await _server.SendCancellationAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best effort; worker may already be gone.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _server?.Dispose();
        _server = null;

        if (_workerProcess != null && !_workerProcess.HasExited)
        {
            try
            {
                _workerProcess.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
            catch (NotSupportedException)
            {
            }

            _workerProcess.Dispose();
            _workerProcess = null;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().Wait();
    }

    private static async Task ReadProgressAsync(
        WorkerPipeServer server,
        IProgress<OperationProgress> progress,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                OperationProgress p = await server.ReadProgressAsync(cancellationToken).ConfigureAwait(false);
                progress.Report(p);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                break;
            }
        }
    }

    private static WorkerOperationType MapOperationType(ProposedAction action)
    {
        return action switch
        {
            ProposedAction.WipeHddFreeSpace => WorkerOperationType.WipeHddFreeSpace,
            ProposedAction.RetrimSsd => WorkerOperationType.RetrimSsd,
            _ => WorkerOperationType.Unknown
        };
    }

    private static OperationErrorCategory MapErrorCategory(string value)
    {
        return Enum.TryParse<OperationErrorCategory>(value, out var category)
            ? category
            : OperationErrorCategory.Unexpected;
    }

    private static VolumeIdentityDto MapToDto(VolumeIdentity identity)
    {
        return new VolumeIdentityDto(
            identity.DriveLetter,
            identity.VolumeGuid,
            identity.Label,
            identity.FileSystem,
            identity.CapacityBytes,
            identity.FreeBytes,
            identity.IsSystem,
            identity.IsBoot,
            identity.IsReadOnly,
            identity.IsDirty,
            identity.IsNetwork,
            identity.IsRemovable,
            identity.IsOptical,
            identity.BitLockerState.ToString(),
            identity.MediaType.ToString(),
            identity.BusType.ToString(),
            identity.RedactedModel,
            identity.HealthStatus);
    }

    private static string GenerateNonce()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    }
}
