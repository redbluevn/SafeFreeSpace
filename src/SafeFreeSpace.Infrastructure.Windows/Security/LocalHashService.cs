namespace SafeFreeSpace.Infrastructure.Windows.Security;

using System.Security.Cryptography;
using System.Text;
using SafeFreeSpace.Core.Interfaces;

public sealed class LocalHashService : IHashService
{
    public string HashWithSalt(string input, ReadOnlySpan<byte> salt)
    {
        ArgumentNullException.ThrowIfNull(input);

        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] combined = new byte[inputBytes.Length + salt.Length];
        inputBytes.CopyTo(combined, 0);
        salt.CopyTo(combined.AsSpan(inputBytes.Length));

        byte[] hash = SHA256.HashData(combined);
        return Convert.ToHexString(hash);
    }
}
