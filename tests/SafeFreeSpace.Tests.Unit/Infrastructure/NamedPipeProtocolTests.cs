namespace SafeFreeSpace.Tests.Unit.Infrastructure;

using System.Buffers.Binary;
using System.Text;
using SafeFreeSpace.Contracts;
using SafeFreeSpace.Infrastructure.Windows.Pipes;
using Xunit;

public class NamedPipeProtocolTests
{
    [Fact]
    public async Task WriteThenRead_PreservesMessage()
    {
        using var stream = new MemoryStream();
        var protocol = new NamedPipeProtocol(stream);

        var handshake = new ProtocolHandshake(1, "nonce123", "op456");
        await protocol.WriteAsync(handshake);

        stream.Position = 0;
        var readProtocol = new NamedPipeProtocol(stream);
        var result = await readProtocol.ReadAsync<ProtocolHandshake>();

        Assert.NotNull(result);
        Assert.Equal(1, result.ProtocolVersion);
        Assert.Equal("nonce123", result.Nonce);
        Assert.Equal("op456", result.OperationId);
    }

    [Fact]
    public async Task ExcessiveMessageLength_Rejected()
    {
        using var stream = new MemoryStream();
        var protocol = new NamedPipeProtocol(stream);

        var huge = new string('x', ProtocolConstants.MaxMessageLengthBytes + 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => protocol.WriteAsync(new { Data = huge }));
    }

    [Fact]
    public async Task ReadInvalidLength_Throws()
    {
        using var stream = new MemoryStream();
        byte[] lengthBytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthBytes, ProtocolConstants.MaxMessageLengthBytes + 1);
        await stream.WriteAsync(lengthBytes);
        stream.Position = 0;

        var protocol = new NamedPipeProtocol(stream);
        await Assert.ThrowsAsync<InvalidOperationException>(() => protocol.ReadAsync<object>());
    }
}
