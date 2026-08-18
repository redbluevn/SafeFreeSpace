namespace SafeFreeSpace.Infrastructure.Windows.Pipes;

using System.IO.Pipes;
using System.Security.Principal;
using SafeFreeSpace.Contracts;

public sealed class WorkerPipeServer : IDisposable
{
    private readonly string _pipeName;
    private readonly string _expectedNonce;
    private readonly string _operationId;
    private readonly int _protocolVersion;
    private NamedPipeServerStream? _server;
    private NamedPipeProtocol? _protocol;

    public WorkerPipeServer(string pipeName, string expectedNonce, string operationId, int protocolVersion = ProtocolConstants.CurrentProtocolVersion)
    {
        _pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
        _expectedNonce = expectedNonce ?? throw new ArgumentNullException(nameof(expectedNonce));
        _operationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
        _protocolVersion = protocolVersion;
    }

    public async Task AcceptAsync(CancellationToken cancellationToken = default)
    {
        _server = NamedPipeServerStreamAcl.Create(
            _pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            4096,
            4096,
            PipeSecurityHelper.CreateRestrictedPipeSecurity());

        await _server.WaitForConnectionAsync(cancellationToken);
        _protocol = new NamedPipeProtocol(_server);

        var handshake = await _protocol.ReadAsync<ProtocolHandshake>(cancellationToken)
            ?? throw new InvalidOperationException("Handshake missing.");

        if (handshake.ProtocolVersion != _protocolVersion)
        {
            throw new InvalidOperationException("Protocol version mismatch.");
        }

        if (!string.Equals(handshake.Nonce, _expectedNonce, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Nonce mismatch.");
        }

        if (!string.Equals(handshake.OperationId, _operationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Operation ID mismatch.");
        }
    }

    public async Task SendRequestAsync(WorkerRequest request, CancellationToken cancellationToken = default)
    {
        EnsureProtocol();
        await _protocol!.WriteAsync(request, cancellationToken);
    }

    public async Task<OperationProgress> ReadProgressAsync(CancellationToken cancellationToken = default)
    {
        EnsureProtocol();
        return await _protocol!.ReadAsync<OperationProgress>(cancellationToken)
            ?? throw new InvalidOperationException("Progress message missing.");
    }

    public async Task SendCancellationAsync(CancellationToken cancellationToken = default)
    {
        EnsureProtocol();
        var cancelRequest = new WorkerRequest(
            _protocolVersion,
            _operationId,
            _expectedNonce,
            WorkerOperationType.CancelOperation,
            new VolumeIdentityDto("", "", null, "", 0, 0, false, false, false, false, false, false, false, "Unknown", "Unknown", "Unknown", null, null));
        await _protocol!.WriteAsync(cancelRequest, cancellationToken);
    }

    public async Task<WorkerResponse> ReadResponseAsync(CancellationToken cancellationToken = default)
    {
        EnsureProtocol();
        return await _protocol!.ReadAsync<WorkerResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Response missing.");
    }

    public void Dispose()
    {
        _protocol?.Dispose();
        _server?.Dispose();
    }

    private void EnsureProtocol()
    {
        if (_protocol == null)
        {
            throw new InvalidOperationException("Pipe not accepted.");
        }
    }
}
