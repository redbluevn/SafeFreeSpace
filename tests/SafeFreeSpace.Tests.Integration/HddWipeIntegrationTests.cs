namespace SafeFreeSpace.Tests.Integration;

using System.Security.Cryptography;
using SafeFreeSpace.Contracts;
using SafeFreeSpace.Core.Interfaces;
using SafeFreeSpace.Core.Models;
using SafeFreeSpace.ElevatedWorker;
using SafeFreeSpace.ElevatedWorker.Executors;
using SafeFreeSpace.Infrastructure.Windows.Storage;
using Xunit;

public class HddWipeIntegrationTests : IDisposable
{
    private readonly VhdTestHelper _vhd;

    public HddWipeIntegrationTests()
    {
        _vhd = new VhdTestHelper();
    }

    public void Dispose()
    {
        _vhd.Dispose();
    }

    [SkippableFact]
    public async Task SentinelFiles_RemainUnchanged()
    {
        Skip.IfNot(VhdTestHelper.IsEnabled(), "VHD tests not enabled.");
        Skip.IfNot(VhdTestHelper.IsAdministrator(), "VHD tests require administrator.");

        _vhd.CreateAndAttach();
        string driveRoot = $"{_vhd.DriveLetter}:\\";

        var files = new Dictionary<string, byte[]>();
        Directory.CreateDirectory(Path.Combine(driveRoot, "nested"));
        string[] paths =
        {
            Path.Combine(driveRoot, "small.txt"),
            Path.Combine(driveRoot, "binary.bin"),
            Path.Combine(driveRoot, "nested", "deep.txt")
        };

        foreach (string path in paths)
        {
            byte[] content = GenerateRandomBytes(path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) ? 1024 * 1024 : 256);
            await File.WriteAllBytesAsync(path, content);
            files[path] = content;
        }

        var runner = new ProcessRunner();
        IVolumeInventory inventory = new WindowsVolumeInventory();
        var executor = new CipherExecutor(runner, inventory);

        var identity = await inventory.RefreshVolumeAsync(_vhd.DriveLetter!);
        Assert.NotNull(identity);
        Assert.Equal(DriveMediaType.Hdd, identity.MediaType);

        var request = BuildWipeRequest(identity);

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var progress = new Progress<OperationProgress>();
        WorkerResponse response = await executor.ExecuteAsync(request, progress, cts.Token);

        Assert.True(response.Success, response.Message);

        foreach (var kvp in files)
        {
            byte[] actual = await File.ReadAllBytesAsync(kvp.Key);
            Assert.Equal(kvp.Value, actual);
        }
    }

    [SkippableFact]
    public async Task DeletedPattern_IsOverwritten()
    {
        Skip.IfNot(VhdTestHelper.IsEnabled(), "VHD tests not enabled.");
        Skip.IfNot(VhdTestHelper.IsAdministrator(), "VHD tests require administrator.");

        _vhd.CreateAndAttach();
        string driveRoot = $"{_vhd.DriveLetter}:\\";

        byte[] pattern = GenerateRandomBytes(64 * 1024);
        string filePath = Path.Combine(driveRoot, "pattern.bin");

        await File.WriteAllBytesAsync(filePath, pattern);
        File.Delete(filePath);

        var runner = new ProcessRunner();
        IVolumeInventory inventory = new WindowsVolumeInventory();
        var executor = new CipherExecutor(runner, inventory);

        var identity = await inventory.RefreshVolumeAsync(_vhd.DriveLetter!);
        Assert.NotNull(identity);

        var request = BuildWipeRequest(identity);

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        WorkerResponse response = await executor.ExecuteAsync(request, new Progress<OperationProgress>(), cts.Token);
        Assert.True(response.Success, response.Message);

        // Detach trước để quét raw VHD, chỉ xóa file sau khi quét xong.
        _vhd.Detach();
        Assert.False(ContainsPattern(_vhd.VhdPath, pattern));
        _vhd.DeleteVhdIfExists();
    }

    private static WorkerRequest BuildWipeRequest(VolumeIdentity identity)
    {
        return new WorkerRequest(
            ProtocolConstants.CurrentProtocolVersion,
            Guid.NewGuid().ToString("N"),
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            WorkerOperationType.WipeHddFreeSpace,
            new VolumeIdentityDto(
                identity.DriveLetter,
                identity.VolumeGuid,
                identity.Label,
                identity.FileSystem,
                identity.CapacityBytes,
                identity.FreeBytes,
                identity.IsSystem,
                identity.IsBoot,
                identity.IsReadOnly,
                identity.IsDirty,
                identity.IsNetwork,
                identity.IsRemovable,
                identity.IsOptical,
                identity.BitLockerState.ToString(),
                identity.MediaType.ToString(),
                identity.BusType.ToString(),
                identity.RedactedModel,
                identity.HealthStatus));
    }

    private static bool ContainsPattern(string filePath, byte[] pattern)
    {
        const int ChunkSize = 8 * 1024 * 1024;
        byte[] buffer = new byte[ChunkSize + pattern.Length - 1];
        int carry = 0;

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkSize, FileOptions.SequentialScan);
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, carry, ChunkSize)) > 0)
        {
            int length = carry + bytesRead;
            if (IndexOf(buffer, length, pattern) >= 0)
            {
                return true;
            }

            // Giữ lại đuôi chunk để bắt cả pattern nằm ngang ranh giới hai chunk.
            carry = Math.Min(pattern.Length - 1, length);
            Buffer.BlockCopy(buffer, length - carry, buffer, 0, carry);
        }

        return false;
    }

    private static int IndexOf(byte[] buffer, int length, byte[] pattern)
    {
        for (int i = 0; i <= length - pattern.Length; i++)
        {
            int j = 0;
            while (j < pattern.Length && buffer[i + j] == pattern[j])
            {
                j++;
            }

            if (j == pattern.Length)
            {
                return i;
            }
        }

        return -1;
    }

    private static byte[] GenerateRandomBytes(int length)
    {
        byte[] bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }
}
