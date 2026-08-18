namespace SafeFreeSpace.Tests.Unit.Core;

using SafeFreeSpace.Core.Models;
using SafeFreeSpace.Core.Services;
using Xunit;

public class SafetyPolicyTests
{
    private readonly SafetyPolicy _policy = new();

    private static VolumeIdentity CreateVolume(
        string driveLetter = "C",
        string? fileSystem = "NTFS",
        DriveMediaType mediaType = DriveMediaType.Hdd,
        DriveBusType busType = DriveBusType.Sata,
        bool isSystem = false,
        bool isBoot = false,
        bool isReadOnly = false,
        bool isDirty = false,
        bool isNetwork = false,
        bool isOptical = false,
        BitLockerState bitLocker = BitLockerState.Unlocked,
        string? healthStatus = "Healthy")
    {
        return new VolumeIdentity(
            driveLetter,
            "{12345678-1234-1234-1234-123456789012}",
            "TESTVOL",
            fileSystem!,
            100_000_000_000,
            50_000_000_000,
            isSystem,
            isBoot,
            isReadOnly,
            isDirty,
            isNetwork,
            false,
            isOptical,
            bitLocker,
            mediaType,
            busType,
            "RedactedModel",
            healthStatus);
    }

    [Fact]
    public void HddNtfs_AllowsWipe()
    {
        var volume = CreateVolume();
        var decision = _policy.Evaluate(volume, false);

        Assert.True(decision.IsEligible);
        Assert.Equal(ProposedAction.WipeHddFreeSpace, decision.Action);
    }

    [Fact]
    public void SsdNtfs_AllowsRetrim()
    {
        var volume = CreateVolume(mediaType: DriveMediaType.Ssd);
        var decision = _policy.Evaluate(volume, false);

        Assert.True(decision.IsEligible);
        Assert.Equal(ProposedAction.RetrimSsd, decision.Action);
    }

    [Fact]
    public void NvmeSsd_AllowsRetrim()
    {
        var volume = CreateVolume(mediaType: DriveMediaType.Ssd, busType: DriveBusType.Nvme);
        var decision = _policy.Evaluate(volume, false);

        Assert.True(decision.IsEligible);
        Assert.Equal(ProposedAction.RetrimSsd, decision.Action);
    }

    [Fact]
    public void UnknownMedia_Blocked()
    {
        var volume = CreateVolume(mediaType: DriveMediaType.Unknown);
        var decision = _policy.Evaluate(volume, false);

        Assert.False(decision.IsEligible);
        Assert.Equal(ProposedAction.Blocked, decision.Action);
    }

    [Fact]
    public void NonNtfs_Blocked()
    {
        var volume = CreateVolume(fileSystem: "FAT32");
        var decision = _policy.Evaluate(volume, false);

        Assert.False(decision.IsEligible);
    }

    [Fact]
    public void NetworkDrive_Blocked()
    {
        var volume = CreateVolume(isNetwork: true);
        var decision = _policy.Evaluate(volume, false);

        Assert.False(decision.IsEligible);
    }

    [Fact]
    public void OpticalDrive_Blocked()
    {
        var volume = CreateVolume(isOptical: true);
        var decision = _policy.Evaluate(volume, false);

        Assert.False(decision.IsEligible);
    }

    [Fact]
    public void ReadOnly_Blocked()
    {
        var volume = CreateVolume(isReadOnly: true);
        var decision = _policy.Evaluate(volume, false);

        Assert.False(decision.IsEligible);
    }

    [Fact]
    public void Dirty_Blocked()
    {
        var volume = CreateVolume(isDirty: true);
        var decision = _policy.Evaluate(volume, false);

        Assert.False(decision.IsEligible);
    }

    [Fact]
    public void BitLockerLocked_Blocked()
    {
        var volume = CreateVolume(bitLocker: BitLockerState.Locked);
        var decision = _policy.Evaluate(volume, false);

        Assert.False(decision.IsEligible);
    }

    [Fact]
    public void BitLockerUnknown_Blocked()
    {
        var volume = CreateVolume(bitLocker: BitLockerState.Unknown);
        var decision = _policy.Evaluate(volume, false);

        Assert.False(decision.IsEligible);
        Assert.Contains(decision.Reasons, r => r.Contains("BitLocker"));
    }

    [Fact]
    public void NullFileSystem_Blocked()
    {
        var volume = CreateVolume(fileSystem: null);
        var decision = _policy.Evaluate(volume, false);

        Assert.False(decision.IsEligible);
        Assert.Equal(ProposedAction.Blocked, decision.Action);
    }

    [Fact]
    public void UnhealthyVolume_Blocked()
    {
        var volume = CreateVolume(healthStatus: "Unhealthy");
        var decision = _policy.Evaluate(volume, false);

        Assert.False(decision.IsEligible);
        Assert.Equal(ProposedAction.Blocked, decision.Action);
    }

    [Fact]
    public void NullHealthStatus_Allowed()
    {
        var volume = CreateVolume(healthStatus: null);
        var decision = _policy.Evaluate(volume, false);

        Assert.True(decision.IsEligible);
    }

    [Fact]
    public void SystemVolume_RequiresAdvancedMode()
    {
        var volume = CreateVolume(isSystem: true);
        var normal = _policy.Evaluate(volume, false);
        var advanced = _policy.Evaluate(volume, true);

        Assert.False(normal.IsEligible);
        Assert.True(advanced.IsEligible);
    }

    [Fact]
    public void BootVolume_RequiresAdvancedMode()
    {
        var volume = CreateVolume(isBoot: true);
        var normal = _policy.Evaluate(volume, false);
        var advanced = _policy.Evaluate(volume, true);

        Assert.False(normal.IsEligible);
        Assert.True(advanced.IsEligible);
    }

    [Fact]
    public void Raid_Blocked()
    {
        var volume = CreateVolume(busType: DriveBusType.Raid);
        var decision = _policy.Evaluate(volume, false);

        Assert.False(decision.IsEligible);
    }

    [Fact]
    public void StorageSpaces_Blocked()
    {
        var volume = CreateVolume(busType: DriveBusType.StorageSpaces);
        var decision = _policy.Evaluate(volume, false);

        Assert.False(decision.IsEligible);
    }

    [Fact]
    public void VirtualBus_Blocked()
    {
        var volume = CreateVolume(busType: DriveBusType.Virtual);
        var decision = _policy.Evaluate(volume, false);

        Assert.False(decision.IsEligible);
    }
}
