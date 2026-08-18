namespace SafeFreeSpace.Infrastructure.Windows.Storage;

using System.Management;
using SafeFreeSpace.Core.Models;

public static class VolumeMapper
{
    public static DriveMediaType MapMediaType(object? value)
    {
        if (value is ushort u)
        {
            return u switch
            {
                3 => DriveMediaType.Hdd,
                4 => DriveMediaType.Ssd,
                5 => DriveMediaType.Scm,
                _ => DriveMediaType.Unknown
            };
        }

        if (value is int i)
        {
            return i switch
            {
                3 => DriveMediaType.Hdd,
                4 => DriveMediaType.Ssd,
                5 => DriveMediaType.Scm,
                _ => DriveMediaType.Unknown
            };
        }

        return DriveMediaType.Unknown;
    }

    public static DriveBusType MapBusType(object? value)
    {
        if (value is ushort u)
        {
            return u switch
            {
                6 => DriveBusType.Sata,
                9 => DriveBusType.Nvme,
                4 or 7 => DriveBusType.Usb,
                3 or 10 or 11 => DriveBusType.Sas,
                8 => DriveBusType.Raid,
                12 or 13 => DriveBusType.StorageSpaces,
                14 or 15 => DriveBusType.Virtual,
                _ => DriveBusType.Unknown
            };
        }

        if (value is int i)
        {
            return i switch
            {
                6 => DriveBusType.Sata,
                9 => DriveBusType.Nvme,
                4 or 7 => DriveBusType.Usb,
                3 or 10 or 11 => DriveBusType.Sas,
                8 => DriveBusType.Raid,
                12 or 13 => DriveBusType.StorageSpaces,
                14 or 15 => DriveBusType.Virtual,
                _ => DriveBusType.Unknown
            };
        }

        return DriveBusType.Unknown;
    }

    public static string? MapHealthStatus(object? value)
    {
        // MSFT_PhysicalDisk.HealthStatus là uint16: 0 = Healthy, 1 = Warning, 2 = Unhealthy.
        // Map sang chuỗi để SafetyPolicy so sánh; null (không đọc được) thì để null — fail-open
        // tương đương fallback path vốn không thu thập được field này.
        ushort? code = value switch
        {
            ushort u => u,
            int i => (ushort)i,
            _ => null
        };

        return code switch
        {
            null => null,
            0 => "Healthy",
            _ => "Unhealthy"
        };
    }

    public static BitLockerState MapBitLockerState(string? protectionStatus)
    {
        // Ngữ nghĩa WMI Win32_EncryptableVolume.ProtectionStatus:
        // 0 = ProtectionOff (không bảo vệ, volume truy cập được), 1 = ProtectionOn (được bảo vệ, đã mở khóa),
        // 2 = ProtectionUnknown. Cả 0 và 1 đều là volume truy cập được nên map về Unlocked.
        // Locked chỉ đến từ LockStatus (hiện không query) nên không suy ra Locked từ ProtectionStatus.
        return protectionStatus?.ToUpperInvariant() switch
        {
            "0" => BitLockerState.Unlocked,
            "1" => BitLockerState.Unlocked,
            "2" => BitLockerState.Unknown,
            _ => BitLockerState.Unknown
        };
    }

    public static string RedactModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return "Unknown";
        }

        const int keepLength = 4;
        if (model.Length <= keepLength * 2)
        {
            return model.Trim();
        }

        return string.Concat(model.AsSpan(0, keepLength), "...", model.AsSpan(model.Length - keepLength));
    }

    public static VolumeIdentity ToVolumeIdentity(
        ManagementObject volume,
        CimPhysicalDiskInfo? diskInfo,
        BitLockerState bitLockerState)
    {
        ArgumentNullException.ThrowIfNull(volume);

        string driveLetter = (volume["DriveLetter"]?.ToString() ?? string.Empty).TrimEnd(':');
        if (driveLetter.Length > 1)
        {
            driveLetter = driveLetter[..1];
        }

        string fileSystem = volume["FileSystem"]?.ToString() ?? string.Empty;
        long capacityBytes = Convert.ToInt64(volume["Size"] ?? 0L);
        long freeBytes = Convert.ToInt64(volume["SizeRemaining"] ?? 0L);
        bool isReadOnly = Convert.ToBoolean(volume["IsReadOnly"] ?? false);
        bool isSystem = Convert.ToBoolean(volume["IsSystem"] ?? false);
        bool isBoot = Convert.ToBoolean(volume["IsBoot"] ?? false);
        bool isDirty = Convert.ToBoolean(volume["VolumeDirty"] ?? false);

        DriveMediaType mediaType = diskInfo?.MediaType ?? DriveMediaType.Unknown;
        DriveBusType busType = diskInfo?.BusType ?? DriveBusType.Unknown;

        if (diskInfo is not null && busType == DriveBusType.Nvme && mediaType == DriveMediaType.Unknown)
        {
            mediaType = DriveMediaType.Ssd;
        }

        string? healthStatus = diskInfo?.HealthStatus;
        string redactedModel = RedactModel(diskInfo?.FriendlyName);

        return new VolumeIdentity(
            driveLetter,
            volume["Path"]?.ToString() ?? volume["ObjectId"]?.ToString() ?? string.Empty,
            volume["FileSystemLabel"]?.ToString(),
            fileSystem,
            capacityBytes,
            freeBytes,
            isSystem,
            isBoot,
            isReadOnly,
            isDirty,
            false,
            false,
            false,
            bitLockerState,
            mediaType,
            busType,
            redactedModel,
            healthStatus);
    }
}
