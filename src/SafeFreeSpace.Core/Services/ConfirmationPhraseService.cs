namespace SafeFreeSpace.Core.Services;

using System.Globalization;
using SafeFreeSpace.Core.Models;

public sealed class ConfirmationPhraseService
{
    public string Generate(ProposedAction action, string driveLetter)
    {
        if (!IsValidDriveLetter(driveLetter))
        {
            throw new ArgumentException("Ký tự ổ đĩa không hợp lệ.", nameof(driveLetter));
        }

        string upperDrive = driveLetter.ToUpperInvariant();
        return action switch
        {
            ProposedAction.WipeHddFreeSpace => $"WIPE FREE SPACE {upperDrive}:",
            ProposedAction.RetrimSsd => $"RETRIM {upperDrive}:",
            _ => throw new ArgumentOutOfRangeException(nameof(action), "Hành động không yêu cầu xác nhận.")
        };
    }

    public bool Validate(string phrase, ProposedAction action, string driveLetter)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return false;
        }

        string expected = Generate(action, driveLetter);
        return phrase.Trim().Equals(expected, StringComparison.Ordinal);
    }

    private static bool IsValidDriveLetter(string driveLetter)
    {
        if (string.IsNullOrEmpty(driveLetter) || driveLetter.Length != 1)
        {
            return false;
        }

        char c = driveLetter[0];
        return c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }
}
