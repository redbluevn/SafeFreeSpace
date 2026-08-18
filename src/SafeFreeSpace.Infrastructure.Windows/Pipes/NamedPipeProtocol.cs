namespace SafeFreeSpace.Infrastructure.Windows.Pipes;

using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using SafeFreeSpace.Contracts;

public sealed class NamedPipeProtocol : IDisposable
{
    private readonly Stream _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public NamedPipeProtocol(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public async Task WriteAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message);
        if (payload.Length > ProtocolConstants.MaxMessageLengthBytes)
        {
            throw new InvalidOperationException("Message exceeds maximum length.");
        }

        byte[] frame = new byte[4 + payload.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame, payload.Length);
        payload.CopyTo(frame, 4);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _stream.WriteAsync(frame, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<T?> ReadAsync<T>(CancellationToken cancellationToken = default)
    {
        byte[] lengthBytes = new byte[4];
        await ReadExactAsync(lengthBytes, cancellationToken);
        int length = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);

        if (length < 0 || length > ProtocolConstants.MaxMessageLengthBytes)
        {
            throw new InvalidOperationException("Invalid message length.");
        }

        byte[] payload = new byte[length];
        await ReadExactAsync(payload, cancellationToken);
        return JsonSerializer.Deserialize<T>(payload);
    }

    private async Task ReadExactAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await _stream.ReadAsync(buffer.AsMemory(totalRead), cancellationToken);
            if (read == 0)
            {
                throw new IOException("Pipe closed before message complete.");
            }

            totalRead += read;
        }
    }

    public void Dispose()
    {
        _writeLock.Dispose();
        _stream.Dispose();
    }
}
