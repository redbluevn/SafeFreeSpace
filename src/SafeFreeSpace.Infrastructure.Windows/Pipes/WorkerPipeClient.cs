namespace SafeFreeSpace.Infrastructure.Windows.Pipes;

using System.IO.Pipes;
using System.Security.Principal;
using SafeFreeSpace.Contracts;

public sealed class WorkerPipeClient : IDisposable
{
    private readonly string _pipeName;
    private NamedPipeClientStream? _client;
    private NamedPipeProtocol? _protocol;

    public WorkerPipeClient(string pipeName)
    {
        _pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
    }

    public async Task ConnectAsync(int timeoutMilliseconds = 30000, CancellationToken cancellationToken = default)
    {
        _client = new NamedPipeClientStream(
            ".",
            _pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous,
            TokenImpersonationLevel.Identification);

        await _client.ConnectAsync(timeoutMilliseconds, cancellationToken);
        _protocol = new NamedPipeProtocol(_client);
    }

    public async Task SendHandshakeAsync(ProtocolHandshake handshake, CancellationToken cancellationToken = default)
    {
        EnsureProtocol();
        await _protocol!.WriteAsync(handshake, cancellationToken);
    }

    public async Task<WorkerRequest> ReadRequestAsync(CancellationToken cancellationToken = default)
    {
        EnsureProtocol();
        return await _protocol!.ReadAsync<WorkerRequest>(cancellationToken)
            ?? throw new InvalidOperationException("Request missing.");
    }

    public async Task SendProgressAsync(OperationProgress progress, CancellationToken cancellationToken = default)
    {
        EnsureProtocol();
        await _protocol!.WriteAsync(progress, cancellationToken);
    }

    public async Task SendResponseAsync(WorkerResponse response, CancellationToken cancellationToken = default)
    {
        EnsureProtocol();
        await _protocol!.WriteAsync(response, cancellationToken);
    }

    public void Dispose()
    {
        _protocol?.Dispose();
        _client?.Dispose();
    }

    private void EnsureProtocol()
    {
        if (_protocol == null)
        {
            throw new InvalidOperationException("Pipe not connected.");
        }
    }
}
