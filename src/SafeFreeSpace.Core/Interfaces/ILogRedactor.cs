namespace SafeFreeSpace.Core.Interfaces;

public interface ILogRedactor
{
    string RedactPath(string? value);
    string RedactSerial(string? value);
    string RedactOutput(string? value);
}
