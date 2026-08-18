namespace SafeFreeSpace.Tests.Integration;

using System.Diagnostics;
using System.Security.Principal;

public sealed class VhdTestHelper : IDisposable
{
    public const string EnvironmentVariableName = "RUN_SAFEFREESPACE_VHD_TESTS";
    public const string TestLabelPrefix = "SFS_TEST_";

    private readonly string _vhdPath;
    private readonly string _testId;
    private string? _driveLetter;
    private bool _disposed;

    public VhdTestHelper(long sizeBytes = 512 * 1024 * 1024)
    {
        _testId = Guid.NewGuid().ToString("N")[..8];
        Label = $"{TestLabelPrefix}{_testId}";
        _vhdPath = Path.Combine(Path.GetTempPath(), $"{Label}.vhdx");
        SizeBytes = sizeBytes;
    }

    public string Label
    {
        get;
    }

    public long SizeBytes
    {
        get;
    }

    public string? DriveLetter => _driveLetter;

    public string VhdPath => _vhdPath;

    public static bool IsEnabled()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvironmentVariableName));
    }

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public void CreateAndAttach()
    {
        if (!IsAdministrator())
        {
            throw new InvalidOperationException("VHD tests require administrator privileges.");
        }

        DeleteVhdIfExists();

        RunDiskPart($@"
create vdisk file=""{_vhdPath}"" maximum={SizeBytes / 1024 / 1024} type=fixed
select vdisk file=""{_vhdPath}""
attach vdisk
convert gpt
create partition primary
format fs=ntfs label=""{Label}"" quick
assign
exit
");

        // Drive letter có thể chưa xuất hiện ngay sau khi diskpart assign, thử lại vài lần.
        for (int attempt = 0; attempt < 10 && string.IsNullOrEmpty(_driveLetter); attempt++)
        {
            if (attempt > 0)
            {
                Thread.Sleep(500);
            }

            _driveLetter = FindDriveLetterByLabel(Label);
        }

        if (string.IsNullOrEmpty(_driveLetter))
        {
            throw new InvalidOperationException("Failed to find drive letter for test volume.");
        }
    }

    public void Detach()
    {
        RunDiskPart($@"
select vdisk file=""{_vhdPath}""
detach vdisk
exit
");
    }

    public void DetachAndDelete()
    {
        try
        {
            Detach();
        }
        catch (Exception)
        {
            // Best effort cleanup.
        }

        DeleteVhdIfExists();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            DetachAndDelete();
            _disposed = true;
        }
    }

    public void DeleteVhdIfExists()
    {
        if (File.Exists(_vhdPath))
        {
            File.Delete(_vhdPath);
        }
    }

    private static string? FindDriveLetterByLabel(string label)
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady && drive.VolumeLabel.Equals(label, StringComparison.OrdinalIgnoreCase))
            {
                return drive.Name.TrimEnd(':', '\\');
            }
        }

        return null;
    }

    private static void RunDiskPart(string script)
    {
        string scriptPath = Path.Combine(Path.GetTempPath(), $"sfs_diskpart_{Guid.NewGuid():N}.txt");
        File.WriteAllText(scriptPath, script);
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "diskpart.exe",
                Arguments = $"/s \"{scriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.Start();

            // Đọc đồng thời cả hai stream để tránh deadlock khi buffer đầy.
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            process.WaitForExitAsync().GetAwaiter().GetResult();
            string output = outputTask.GetAwaiter().GetResult();
            string error = errorTask.GetAwaiter().GetResult();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"diskpart failed (exit code {process.ExitCode}). stdout: {output} | stderr: {error}");
            }
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }
}
