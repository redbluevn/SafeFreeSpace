namespace SafeFreeSpace.Infrastructure.Windows.Storage;

using System.Management;
using SafeFreeSpace.Core.Interfaces;
using SafeFreeSpace.Core.Models;

public sealed class WindowsVolumeInventory : IVolumeInventory
{
    private const string StorageNamespace = @"root\Microsoft\Windows\Storage";
    private const string Cimv2Namespace = @"root\CIMV2";

    private readonly ManagementScope _storageScope;
    private readonly ManagementScope _cimv2Scope;

    public WindowsVolumeInventory()
    {
        _storageScope = new ManagementScope(StorageNamespace);
        _cimv2Scope = new ManagementScope(Cimv2Namespace);
    }

    public WindowsVolumeInventory(ManagementScope storageScope, ManagementScope cimv2Scope)
    {
        _storageScope = storageScope ?? throw new ArgumentNullException(nameof(storageScope));
        _cimv2Scope = cimv2Scope ?? throw new ArgumentNullException(nameof(cimv2Scope));
    }

    public async Task<IReadOnlyList<VolumeIdentity>> GetVolumesAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var results = new List<VolumeIdentity>();

            try
            {
                var disks = TryQueryPhysicalDisks();
                var partitions = TryQueryPartitions();
                var bitlocker = TryQueryBitLockerStates();

                if (TryQueryStorageVolumes(out var volumes))
                {
                    foreach (ManagementObject volume in volumes)
                    {
                        using (volume)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var diskInfo = ResolvePhysicalDisk(volume, partitions, disks);
                            var driveLetter = (volume["DriveLetter"]?.ToString() ?? string.Empty).TrimEnd(':');
                            bool isSystem = Convert.ToBoolean(volume["IsSystem"] ?? false);
                            bool isBoot = Convert.ToBoolean(volume["IsBoot"] ?? false);

                            // Skip cloud/virtual volumes that have no physical disk backing and are not system/boot.
                            if (diskInfo == null && !isSystem && !isBoot)
                            {
                                continue;
                            }

                            bitlocker.TryGetValue(driveLetter, out var blState);
                            results.Add(VolumeMapper.ToVolumeIdentity(volume, diskInfo, blState));
                        }
                    }
                }
                else
                {
                    results.AddRange(FallbackLogicalDisks(bitlocker, cancellationToken));
                }
            }
            catch (OperationCanceledException)
            {
                // Không nuốt yêu cầu hủy: lan truyền để caller xử lý đúng trạng thái bị hủy.
                throw;
            }
            catch (Exception)
            {
                results.Clear();
            }

            // If the primary CIM path produced nothing (or threw), always try the legacy fallback
            // so the user still sees available logical disks.
            if (results.Count == 0)
            {
                results.AddRange(FallbackLogicalDisks(new Dictionary<string, BitLockerState>(StringComparer.OrdinalIgnoreCase), cancellationToken));
            }

            return results.AsReadOnly();
        }, cancellationToken);
    }

    public async Task<VolumeIdentity?> RefreshVolumeAsync(string driveLetter, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(driveLetter);

        var volumes = await GetVolumesAsync(cancellationToken);
        return volumes.FirstOrDefault(v =>
            v.DriveLetter.Equals(driveLetter, StringComparison.OrdinalIgnoreCase));
    }

    private bool TryQueryStorageVolumes(out ManagementObjectCollection volumes)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(_storageScope, new ObjectQuery("SELECT * FROM MSFT_Volume"));
            volumes = searcher.Get();
            return true;
        }
        catch (ManagementException)
        {
            volumes = null!;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            volumes = null!;
            return false;
        }
        catch (Exception)
        {
            volumes = null!;
            return false;
        }
    }

    private Dictionary<uint, CimPhysicalDiskInfo> TryQueryPhysicalDisks()
    {
        var result = new Dictionary<uint, CimPhysicalDiskInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(_storageScope, new ObjectQuery("SELECT * FROM MSFT_PhysicalDisk"));
            using var disks = searcher.Get();
            foreach (ManagementObject disk in disks)
            {
                using (disk)
                {
                    uint number = Convert.ToUInt32(disk["DeviceId"] ?? disk["DiskNumber"] ?? 0);
                    result[number] = new CimPhysicalDiskInfo(
                        number,
                        VolumeMapper.MapMediaType(disk["MediaType"]),
                        VolumeMapper.MapBusType(disk["BusType"]),
                        disk["FriendlyName"]?.ToString(),
                        VolumeMapper.MapHealthStatus(disk["HealthStatus"]));
                }
            }
        }
        catch (ManagementException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (Exception)
        {
        }

        return result;
    }

    private Dictionary<string, uint> TryQueryPartitions()
    {
        var result = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(_storageScope, new ObjectQuery("SELECT * FROM MSFT_Partition"));
            using var partitions = searcher.Get();
            foreach (ManagementObject partition in partitions)
            {
                using (partition)
                {
                    uint diskNumber = Convert.ToUInt32(partition["DiskNumber"] ?? 0);
                    if (partition["AccessPaths"] is string[] paths)
                    {
                        foreach (string path in paths)
                        {
                            result[path] = diskNumber;
                        }
                    }

                    if (partition["ObjectId"] is string objectId)
                    {
                        result[objectId] = diskNumber;
                    }
                }
            }
        }
        catch (ManagementException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (Exception)
        {
        }

        return result;
    }

    private Dictionary<string, BitLockerState> TryQueryBitLockerStates()
    {
        var result = new Dictionary<string, BitLockerState>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var scope = new ManagementScope(@"root\CIMV2\Security\MicrosoftVolumeEncryption");
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM Win32_EncryptableVolume"));
            using var volumes = searcher.Get();
            foreach (ManagementObject volume in volumes)
            {
                using (volume)
                {
                    string? letter = volume["DriveLetter"]?.ToString()?.TrimEnd(':');
                    string? status = volume["ProtectionStatus"]?.ToString();
                    if (!string.IsNullOrEmpty(letter))
                    {
                        result[letter] = VolumeMapper.MapBitLockerState(status);
                    }
                }
            }
        }
        catch (ManagementException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (Exception)
        {
        }

        return result;
    }

    private CimPhysicalDiskInfo? ResolvePhysicalDisk(
        ManagementObject volume,
        Dictionary<string, uint> partitions,
        Dictionary<uint, CimPhysicalDiskInfo> disks)
    {
        string? path = volume["Path"]?.ToString();
        string? objectId = volume["ObjectId"]?.ToString();

        if (!string.IsNullOrEmpty(path) && partitions.TryGetValue(path, out uint diskNumber))
        {
            disks.TryGetValue(diskNumber, out var disk);
            return disk;
        }

        if (!string.IsNullOrEmpty(objectId) && partitions.TryGetValue(objectId, out diskNumber))
        {
            disks.TryGetValue(diskNumber, out var disk);
            return disk;
        }

        return null;
    }

    private List<VolumeIdentity> FallbackLogicalDisks(Dictionary<string, BitLockerState> bitlocker, CancellationToken cancellationToken)
    {
        var results = new List<VolumeIdentity>();
        try
        {
            var diskDrives = TryQueryWin32DiskDrives();
            var diskMapping = TryQueryWin32LogicalDiskToPartition();

            using var searcher = new ManagementObjectSearcher(_cimv2Scope, new ObjectQuery("SELECT * FROM Win32_Volume"));
            using var volumes = searcher.Get();
            foreach (ManagementObject volume in volumes)
            {
                using (volume)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string letter = (volume["DriveLetter"]?.ToString() ?? string.Empty).TrimEnd(':');
                    if (string.IsNullOrEmpty(letter))
                    {
                        continue;
                    }

                    bitlocker.TryGetValue(letter, out var blState);

                    CimPhysicalDiskInfo? diskInfo = null;
                    if (diskMapping.TryGetValue(letter, out uint diskIndex))
                    {
                        diskDrives.TryGetValue(diskIndex, out diskInfo);
                    }

                    bool isSystem = Convert.ToBoolean(volume["SystemVolume"] ?? false);
                    bool isBoot = Convert.ToBoolean(volume["BootVolume"] ?? false);

                    // Skip cloud/virtual volumes that have no physical disk backing and are not system/boot.
                    if (diskInfo == null && !isSystem && !isBoot)
                    {
                        continue;
                    }

                    // Win32_Volume.DriveType: 2=removable, 3=local, 4=network, 5=optical.
                    uint driveType = Convert.ToUInt32(volume["DriveType"] ?? 0);

                    results.Add(ToFallbackVolumeIdentity(volume, driveType, blState, diskInfo));
                }
            }
        }
        catch (ManagementException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (OperationCanceledException)
        {
            // Không nuốt yêu cầu hủy: lan truyền để caller xử lý đúng trạng thái bị hủy.
            throw;
        }
        catch (Exception)
        {
        }

        return results;
    }

    private Dictionary<uint, CimPhysicalDiskInfo> TryQueryWin32DiskDrives()
    {
        var result = new Dictionary<uint, CimPhysicalDiskInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(_cimv2Scope, new ObjectQuery("SELECT * FROM Win32_DiskDrive"));
            using var disks = searcher.Get();
            foreach (ManagementObject disk in disks)
            {
                using (disk)
                {
                    uint index = Convert.ToUInt32(disk["Index"] ?? 0);
                    string? model = disk["Model"]?.ToString();
                    string? interfaceType = disk["InterfaceType"]?.ToString();
                    result[index] = new CimPhysicalDiskInfo(
                        index,
                        MapWin32MediaType(model, interfaceType),
                        MapWin32BusType(model, interfaceType),
                        model,
                        null);
                }
            }
        }
        catch (ManagementException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (Exception)
        {
        }

        return result;
    }

    private Dictionary<string, uint> TryQueryWin32LogicalDiskToPartition()
    {
        var result = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(_cimv2Scope, new ObjectQuery("SELECT * FROM Win32_LogicalDiskToPartition"));
            using var mappings = searcher.Get();
            foreach (ManagementObject mapping in mappings)
            {
                using (mapping)
                {
                    string? antecedent = mapping["Antecedent"]?.ToString();
                    string? dependent = mapping["Dependent"]?.ToString();
                    if (string.IsNullOrEmpty(antecedent) || string.IsNullOrEmpty(dependent))
                    {
                        continue;
                    }

                    // dependent looks like: \\HOST\root\cimv2:Win32_LogicalDisk.DeviceID="C:"
                    string? letter = ExtractQuotedValue(dependent);
                    if (string.IsNullOrEmpty(letter))
                    {
                        continue;
                    }

                    // antecedent looks like: \\HOST\root\cimv2:Win32_DiskPartition.DeviceID="Disk #0, Partition #1"
                    string? diskPartitionId = ExtractQuotedValue(antecedent);
                    if (string.IsNullOrEmpty(diskPartitionId))
                    {
                        continue;
                    }

                    if (TryParseDiskNumber(diskPartitionId, out uint diskNumber))
                    {
                        result[letter.TrimEnd(':')] = diskNumber;
                    }
                }
            }
        }
        catch (ManagementException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (Exception)
        {
        }

        return result;
    }

    private static string? ExtractQuotedValue(string input)
    {
        int start = input.IndexOf('"');
        if (start < 0)
        {
            return null;
        }

        int end = input.IndexOf('"', start + 1);
        if (end < 0)
        {
            return null;
        }

        return input.Substring(start + 1, end - start - 1);
    }

    private static bool TryParseDiskNumber(string diskPartitionId, out uint diskNumber)
    {
        diskNumber = 0;
        const string diskPrefix = "Disk #";
        int index = diskPartitionId.IndexOf(diskPrefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        index += diskPrefix.Length;
        int comma = diskPartitionId.IndexOf(',', index);
        if (comma < 0)
        {
            return false;
        }

        return uint.TryParse(diskPartitionId.Substring(index, comma - index), out diskNumber);
    }

    private static DriveMediaType MapWin32MediaType(string? model, string? interfaceType)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return DriveMediaType.Unknown;
        }

        string lower = model.ToLowerInvariant();
        if (lower.Contains("nvme") || lower.Contains("ssd") || lower.Contains("solid state"))
        {
            return DriveMediaType.Ssd;
        }

        if (lower.Contains("hdd") || lower.Contains("hard disk") || lower.Contains("ata") || lower.Contains("sata"))
        {
            return DriveMediaType.Hdd;
        }

        return DriveMediaType.Unknown;
    }

    private static DriveBusType MapWin32BusType(string? model, string? interfaceType)
    {
        if (!string.IsNullOrWhiteSpace(model))
        {
            string lower = model.ToLowerInvariant();
            if (lower.Contains("nvme"))
            {
                return DriveBusType.Nvme;
            }
        }

        if (!string.IsNullOrWhiteSpace(interfaceType))
        {
            string lower = interfaceType.ToLowerInvariant();
            if (lower.Contains("usb"))
            {
                return DriveBusType.Usb;
            }

            if (lower.Contains("scsi"))
            {
                // NVMe drives commonly report as SCSI in Win32_DiskDrive; model check above catches most NVMe.
                return DriveBusType.Sas;
            }

            if (lower.Contains("ide") || lower.Contains("ata") || lower.Contains("sata"))
            {
                return DriveBusType.Sata;
            }
        }

        return DriveBusType.Unknown;
    }

    private static VolumeIdentity ToFallbackVolumeIdentity(ManagementObject volume, uint driveType, BitLockerState bitLockerState, CimPhysicalDiskInfo? diskInfo)
    {
        string letter = (volume["DriveLetter"]?.ToString() ?? string.Empty).TrimEnd(':');

        bool isNetwork = driveType == 4;
        bool isRemovable = driveType == 2;
        bool isOptical = driveType == 5;

        DriveMediaType mediaType = diskInfo?.MediaType ?? DriveMediaType.Unknown;
        DriveBusType busType = diskInfo?.BusType ?? DriveBusType.Unknown;
        string model = diskInfo?.FriendlyName ?? "Unknown";

        return new VolumeIdentity(
            letter,
            ExtractVolumeGuid(volume["DeviceID"]?.ToString()),
            volume["Label"]?.ToString(),
            volume["FileSystem"]?.ToString() ?? string.Empty,
            Convert.ToInt64(volume["Capacity"] ?? 0L),
            Convert.ToInt64(volume["FreeSpace"] ?? 0L),
            Convert.ToBoolean(volume["SystemVolume"] ?? false),
            Convert.ToBoolean(volume["BootVolume"] ?? false),
            // Win32_Volume không có thuộc tính read-only/dirty nên fallback legacy giữ false (giới hạn đã biết).
            false,
            false,
            isNetwork,
            isRemovable,
            isOptical,
            bitLockerState,
            mediaType,
            busType,
            VolumeMapper.RedactModel(model),
            null);
    }

    private static string ExtractVolumeGuid(string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return string.Empty;
        }

        int start = deviceId.IndexOf('{');
        if (start < 0)
        {
            return string.Empty;
        }

        int end = deviceId.IndexOf('}', start);
        if (end < 0)
        {
            return string.Empty;
        }

        return deviceId.Substring(start + 1, end - start - 1);
    }
}
