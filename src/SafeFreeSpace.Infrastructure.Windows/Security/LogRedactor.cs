namespace SafeFreeSpace.Infrastructure.Windows.Security;

using System.Text.RegularExpressions;
using SafeFreeSpace.Core.Interfaces;

public sealed class LogRedactor : ILogRedactor
{
    private readonly string _userProfile;

    public LogRedactor()
    {
        _userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public string RedactPath(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string redacted = value;
        if (!string.IsNullOrEmpty(_userProfile))
        {
            redacted = redacted.Replace(_userProfile, "[USERPROFILE]", StringComparison.OrdinalIgnoreCase);
        }

        redacted = Regex.Replace(redacted, @"[A-Za-z]:\\[^\s]*", "[PATH]");
        redacted = Regex.Replace(redacted, @"\\\\[^\s\\]+\\[^\s]*", "[UNC]");
        return redacted;
    }

    public string RedactSerial(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Length <= 4)
        {
            return value;
        }

        return $"{value[..2]}...{value[^2..]}";
    }

    public string RedactOutput(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Redact prefix NT device path (\\?\C:\) trước RedactPath, vì regex path thường
        // sẽ thay phần "C:\..." trước và khiến pattern này không bao giờ match.
        string redacted = Regex.Replace(value, @"\\\\\?\\[A-Za-z]:\\", "[VOLUME]");
        redacted = RedactPath(redacted);
        redacted = Regex.Replace(redacted, @"[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}", "[GUID]");
        return redacted;
    }
}
