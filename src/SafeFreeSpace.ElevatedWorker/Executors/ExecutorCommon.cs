namespace SafeFreeSpace.ElevatedWorker.Executors;

using SafeFreeSpace.Contracts;
using SafeFreeSpace.Core.Models;

internal static class ExecutorCommon
{
    private const int MaxErrorDetailLength = 200;

    public static char ValidateDriveLetter(string driveLetter)
    {
        if (string.IsNullOrEmpty(driveLetter) || driveLetter.Length != 1)
        {
            throw new ArgumentException("Drive letter must be a single character.", nameof(driveLetter));
        }

        char c = char.ToUpperInvariant(driveLetter[0]);
        if (c is < 'A' or > 'Z')
        {
            throw new ArgumentException("Drive letter must be A-Z.", nameof(driveLetter));
        }

        return c;
    }

    public static WorkerResponse Fail(OperationErrorCategory category, string message)
    {
        return new WorkerResponse(false, OperationResult.Failed, category.ToString(), message);
    }

    public static WorkerResponse ExitCodeFailure(int exitCode, string standardError)
    {
        string detail = LastLines(standardError, 3);
        if (detail.Length == 0)
        {
            return Fail(OperationErrorCategory.ProcessExitedWithError, $"Exit code {exitCode}.");
        }

        detail = SanitizeOutput(detail);
        if (detail.Length > MaxErrorDetailLength)
        {
            detail = detail[..MaxErrorDetailLength];
        }

        return Fail(OperationErrorCategory.ProcessExitedWithError, $"Exit code {exitCode}. {detail}");
    }

    public static string SanitizeOutput(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return string.Empty;
        }

        // Avoid leaking user paths that may appear in tool output.
        return line
            .Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "[USERPROFILE]", StringComparison.OrdinalIgnoreCase)
            .Replace("\\", "/");
    }

    private static string LastLines(string text, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int start = Math.Max(0, lines.Length - maxLines);
        return string.Join("; ", lines[start..]);
    }
}
