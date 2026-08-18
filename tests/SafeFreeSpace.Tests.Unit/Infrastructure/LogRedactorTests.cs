namespace SafeFreeSpace.Tests.Unit.Infrastructure;

using SafeFreeSpace.Infrastructure.Windows.Security;
using Xunit;

public class LogRedactorTests
{
    [Fact]
    public void RedactOutput_RedactsNtDevicePathPrefix()
    {
        var redactor = new LogRedactor();

        string result = redactor.RedactOutput(@"\\?\C:\secret\file.txt");

        Assert.StartsWith("[VOLUME]", result);
        Assert.DoesNotContain(@"\\?\", result);
    }

    [Fact]
    public void RedactOutput_RedactsGuidAndRegularPath()
    {
        var redactor = new LogRedactor();

        string result = redactor.RedactOutput(@"Volume D:\data done 12345678-1234-1234-1234-123456789abc");

        Assert.Contains("[PATH]", result);
        Assert.Contains("[GUID]", result);
        Assert.DoesNotContain(@"D:\data", result);
    }
}
