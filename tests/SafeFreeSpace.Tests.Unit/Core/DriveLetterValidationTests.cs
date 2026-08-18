namespace SafeFreeSpace.Tests.Unit.Core;

using SafeFreeSpace.Core.Models;
using SafeFreeSpace.Core.Services;
using Xunit;

public class DriveLetterValidationTests
{
    private readonly CommandBuilder _builder = new();

    [Theory]
    [InlineData('C')]
    [InlineData('A')]
    [InlineData('Z')]
    public void ValidDriveLetter_Accepted(char letter)
    {
        var cmd = _builder.BuildHddWipe(letter);
        Assert.Single(cmd.Arguments);
        Assert.Contains(letter.ToString(), cmd.Arguments[0]);
    }

    [Theory]
    [InlineData('1')]
    [InlineData('[')]
    [InlineData(' ')]
    [InlineData('\0')]
    public void InvalidDriveLetter_Rejected(char letter)
    {
        Assert.Throws<ArgumentException>(() => _builder.BuildHddWipe(letter));
        Assert.Throws<ArgumentException>(() => _builder.BuildSsdRetrim(letter));
    }

    [Fact]
    public void ConfirmationPhraseService_NormalizesLowercase()
    {
        var service = new ConfirmationPhraseService();
        string phrase = service.Generate(ProposedAction.WipeHddFreeSpace, "c");
        Assert.Equal("WIPE FREE SPACE C:", phrase);
        Assert.True(service.Validate("WIPE FREE SPACE C:", ProposedAction.WipeHddFreeSpace, "c"));
    }

    [Fact]
    public void ConfirmationPhraseService_RejectsWrongLetter()
    {
        var service = new ConfirmationPhraseService();
        Assert.False(service.Validate("WIPE FREE SPACE D:", ProposedAction.WipeHddFreeSpace, "C"));
    }

    [Fact]
    public void ConfirmationPhraseService_RejectsInjectedCharacters()
    {
        var service = new ConfirmationPhraseService();
        Assert.False(service.Validate("WIPE FREE SPACE C:\\;", ProposedAction.WipeHddFreeSpace, "C"));
    }
}
