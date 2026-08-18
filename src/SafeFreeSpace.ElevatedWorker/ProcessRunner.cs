namespace SafeFreeSpace.ElevatedWorker;

using System.Diagnostics;
using System.Text;
using SafeFreeSpace.Core.Services;

public sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool WasCancelled);

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        BuiltCommand command,
        IProgress<string> outputProgress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!File.Exists(command.ExecutablePath))
        {
            throw new FileNotFoundException("Executable not found.", command.ExecutablePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = command.ExecutablePath,
            WorkingDirectory = command.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (string arg in command.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start process.");
        }

        var stdoutBuffer = new StringBuilder();
        var stderrBuffer = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null)
            {
                return;
            }

            stdoutBuffer.AppendLine(e.Data);
            outputProgress.Report(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null)
            {
                return;
            }

            stderrBuffer.AppendLine(e.Data);
            outputProgress.Report(e.Data);
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        bool wasCancelled = false;
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
            try
            {
                process.Kill(true);
            }
            catch (Exception)
            {
                // Already known to be a cancellation; never let Kill failures leak out.
            }

            await Task.Delay(500, CancellationToken.None);
        }

        process.WaitForExit();

        return new ProcessResult(
            process.ExitCode,
            stdoutBuffer.ToString(),
            stderrBuffer.ToString(),
            wasCancelled);
    }
}
