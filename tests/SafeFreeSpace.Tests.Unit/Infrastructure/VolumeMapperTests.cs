namespace SafeFreeSpace.Tests.Unit.Infrastructure;

using SafeFreeSpace.Core.Models;
using SafeFreeSpace.Infrastructure.Windows.Storage;
using Xunit;

public class VolumeMapperTests
{
    [Theory]
    [InlineData(3, DriveMediaType.Hdd)]
    [InlineData(4, DriveMediaType.Ssd)]
    [InlineData(5, DriveMediaType.Scm)]
    [InlineData(0, DriveMediaType.Unknown)]
    [InlineData(99, DriveMediaType.Unknown)]
    public void MapMediaType_ReturnsExpected(ushort value, DriveMediaType expected)
    {
        Assert.Equal(expected, VolumeMapper.MapMediaType(value));
    }

    [Theory]
    [InlineData(6, DriveBusType.Sata)]
    [InlineData(9, DriveBusType.Nvme)]
    [InlineData(4, DriveBusType.Usb)]
    [InlineData(8, DriveBusType.Raid)]
    [InlineData(12, DriveBusType.StorageSpaces)]
    [InlineData(14, DriveBusType.Virtual)]
    [InlineData(0, DriveBusType.Unknown)]
    public void MapBusType_ReturnsExpected(ushort value, DriveBusType expected)
    {
        Assert.Equal(expected, VolumeMapper.MapBusType(value));
    }

    [Theory]
    [InlineData("0", BitLockerState.Unlocked)]
    [InlineData("1", BitLockerState.Unlocked)]
    [InlineData("2", BitLockerState.Unknown)]
    [InlineData(null, BitLockerState.Unknown)]
    [InlineData("", BitLockerState.Unknown)]
    public void MapBitLockerState_ReturnsExpected(string? value, BitLockerState expected)
    {
        Assert.Equal(expected, VolumeMapper.MapBitLockerState(value));
    }

    [Theory]
    [InlineData(0, "Healthy")]
    [InlineData(1, "Unhealthy")]
    [InlineData(2, "Unhealthy")]
    public void MapHealthStatus_ReturnsExpected(ushort value, string expected)
    {
        Assert.Equal(expected, VolumeMapper.MapHealthStatus(value));
    }

    [Fact]
    public void MapHealthStatus_Null_ReturnsNull()
    {
        Assert.Null(VolumeMapper.MapHealthStatus(null));
    }

    [Theory]
    [InlineData("Samsung SSD 860 EVO 500GB", "Sams...00GB")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ", "ABCD...WXYZ")]
    [InlineData("WD10EZEX", "WD10EZEX")]
    [InlineData("AB", "AB")]
    [InlineData(null, "Unknown")]
    public void RedactModel_KeepsPrefixAndSuffix(string? model, string expected)
    {
        Assert.Equal(expected, VolumeMapper.RedactModel(model));
    }
}
