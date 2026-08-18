namespace SafeFreeSpace.Core.Services;

using SafeFreeSpace.Core.Models;

public sealed class SafetyPolicy
{
    public EligibilityDecision Evaluate(VolumeIdentity volume, bool advancedMode)
    {
        ArgumentNullException.ThrowIfNull(volume);

        var reasons = new List<string>();
        bool isEligible = true;
        bool requiresAdvanced = false;
        ProposedAction action = ProposedAction.None;

        if (!IsValidDriveLetter(volume.DriveLetter))
        {
            reasons.Add("Ký tự ổ đĩa không hợp lệ.");
            isEligible = false;
        }

        if (string.IsNullOrEmpty(volume.VolumeGuid))
        {
            reasons.Add("Không xác định được Volume GUID.");
            isEligible = false;
        }

        if (volume.IsNetwork)
        {
            reasons.Add("Ổ mạng không được hỗ trợ.");
            isEligible = false;
        }

        if (volume.IsOptical)
        {
            reasons.Add("ổ quang không được hỗ trợ.");
            isEligible = false;
        }

        if (volume.IsReadOnly)
        {
            reasons.Add("Volume ở chế độ chỉ đọc.");
            isEligible = false;
        }

        if (volume.IsDirty)
        {
            reasons.Add("Volume bị đánh dấu dirty; cần kiểm tra ổ trước.");
            isEligible = false;
        }

        if (volume.BitLockerState == BitLockerState.Locked)
        {
            reasons.Add("BitLocker đang khóa.");
            isEligible = false;
        }

        if (volume.BitLockerState == BitLockerState.Unknown)
        {
            reasons.Add("Không xác định được trạng thái BitLocker.");
            isEligible = false;
        }

        if (!string.IsNullOrEmpty(volume.HealthStatus) &&
            !volume.HealthStatus.Equals("Healthy", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("Tình trạng sức khỏe ổ đĩa không bình thường.");
            isEligible = false;
        }

        if (string.Equals(volume.FileSystem, "NTFS", StringComparison.OrdinalIgnoreCase))
        {
            switch (volume.MediaType)
            {
                case DriveMediaType.Hdd:
                    action = ProposedAction.WipeHddFreeSpace;
                    break;
                case DriveMediaType.Ssd:
                case DriveMediaType.Scm:
                    action = ProposedAction.RetrimSsd;
                    break;
                case DriveMediaType.Unknown:
                default:
                    reasons.Add("Không xác định loại ổ.");
                    isEligible = false;
                    action = ProposedAction.Blocked;
                    break;
            }
        }
        else
        {
            reasons.Add($"Hệ thống tệp {volume.FileSystem} không được hỗ trợ.");
            isEligible = false;
        }

        if (volume.BusType == DriveBusType.Raid ||
            volume.BusType == DriveBusType.StorageSpaces ||
            volume.BusType == DriveBusType.Virtual ||
            volume.BusType == DriveBusType.Unknown)
        {
            reasons.Add("Loại bus hoặc ánh xạ ổ chưa được hỗ trợ.");
            isEligible = false;
        }

        if (volume.IsSystem || volume.IsBoot)
        {
            requiresAdvanced = true;
            if (!advancedMode)
            {
                reasons.Add("Volume hệ thống chỉ có thể thao tác trong chế độ nâng cao.");
                isEligible = false;
            }
        }

        if (!isEligible)
        {
            action = ProposedAction.Blocked;
        }

        return new EligibilityDecision(action, isEligible, reasons.AsReadOnly(), requiresAdvanced);
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
