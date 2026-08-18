namespace SafeFreeSpace.Tests.Unit.Core;

using SafeFreeSpace.Core.Services;
using Xunit;

public class CommandBuilderTests
{
    private readonly CommandBuilder _builder = new();

    [Fact]
    public void HddWipe_UsesCipherInSystem32()
    {
        var cmd = _builder.BuildHddWipe('X');

        Assert.EndsWith("cipher.exe", cmd.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("System32", cmd.ExecutablePath);
        Assert.Single(cmd.Arguments);
        Assert.Equal("/w:X:\\", cmd.Arguments[0]);
        Assert.DoesNotContain("cmd.exe", cmd.ExecutablePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SsdRetrim_UsesPowerShellWithNoProfile()
    {
        var cmd = _builder.BuildSsdRetrim('Y');

        Assert.EndsWith("powershell.exe", cmd.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WindowsPowerShell", cmd.ExecutablePath);
        Assert.Equal(new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command" }, cmd.Arguments.Take(4));
        Assert.Contains("Optimize-Volume -DriveLetter Y -ReTrim -Verbose -ErrorAction Stop", cmd.Arguments);
    }

    [Fact]
    public void HddWipe_DriveLetterInjected_Rejected()
    {
        Assert.Throws<ArgumentException>(() => _builder.BuildHddWipe('&'));
    }

    [Fact]
    public void Retrim_DriveLetterInjected_Rejected()
    {
        Assert.Throws<ArgumentException>(() => _builder.BuildSsdRetrim(';'));
    }
}
