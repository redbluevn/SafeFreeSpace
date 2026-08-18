namespace SafeFreeSpace.Core.Services;

using System.Text.RegularExpressions;
using SafeFreeSpace.Core.Models;

public sealed record BuiltCommand(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory);

public sealed class CommandBuilder
{
    private static readonly Regex DriveLetterRegex = new("^[A-Z]$", RegexOptions.CultureInvariant);

    public BuiltCommand BuildHddWipe(char driveLetter)
    {
        ValidateDriveLetter(driveLetter);
        string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string executable = Path.Combine(systemRoot, "System32", "cipher.exe");
        string arg = $"/w:{driveLetter}:\\";
        return new BuiltCommand(
            executable,
            new[] { arg }.AsReadOnly(),
            Path.Combine(systemRoot, "System32"));
    }

    public BuiltCommand BuildSsdRetrim(char driveLetter)
    {
        ValidateDriveLetter(driveLetter);
        string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string executable = Path.Combine(systemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        string script = $"Optimize-Volume -DriveLetter {driveLetter} -ReTrim -Verbose -ErrorAction Stop";
        return new BuiltCommand(
            executable,
            new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script }.AsReadOnly(),
            Path.Combine(systemRoot, "System32"));
    }

    private static void ValidateDriveLetter(char driveLetter)
    {
        if (!DriveLetterRegex.IsMatch(driveLetter.ToString()))
        {
            throw new ArgumentException("Ký tự ổ đĩa phải là một chữ cái A-Z.", nameof(driveLetter));
        }
    }
}
