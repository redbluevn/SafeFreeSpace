namespace SafeFreeSpace.Core.Interfaces;

public interface IHashService
{
    string HashWithSalt(string input, ReadOnlySpan<byte> salt);
}
