namespace SafeFreeSpace.Infrastructure.Windows.Pipes;

using System.Diagnostics;
using System.Reflection;
using System.Text;
using SafeFreeSpace.Contracts;

public sealed class ElevatedWorkerLauncher
{
    private readonly string _workerExecutablePath;

    public ElevatedWorkerLauncher(string? workerExecutablePath = null)
    {
        _workerExecutablePath = workerExecutablePath ?? FindWorkerExecutable();
    }

    public Process Launch(string pipeName, string nonce, string operationId, int protocolVersion = ProtocolConstants.CurrentProtocolVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(pipeName);
        ArgumentException.ThrowIfNullOrEmpty(nonce);
        ArgumentException.ThrowIfNullOrEmpty(operationId);

        string[] args =
        [
            protocolVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ToBase64(pipeName),
            ToBase64(nonce),
            ToBase64(operationId)
        ];

        var startInfo = new ProcessStartInfo
        {
            FileName = _workerExecutablePath,
            UseShellExecute = true,
            Verb = "runas",
            Arguments = string.Join(" ", args),
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(_workerExecutablePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.System)
        };

        var process = Process.Start(startInfo);
        return process ?? throw new InvalidOperationException("Failed to start elevated worker.");
    }

    private static string ToBase64(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }

    private static string FindWorkerExecutable()
    {
        string? assemblyLocation = Assembly.GetEntryAssembly()?.Location;
        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            string directory = Path.GetDirectoryName(assemblyLocation)!;
            string candidate = Path.Combine(directory, "SafeFreeSpace.ElevatedWorker.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string appDirectory = AppContext.BaseDirectory;
        string fallback = Path.Combine(appDirectory, "SafeFreeSpace.ElevatedWorker.exe");
        return fallback;
    }
}
