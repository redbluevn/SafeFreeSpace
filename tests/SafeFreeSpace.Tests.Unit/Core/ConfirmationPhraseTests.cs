namespace SafeFreeSpace.Tests.Unit.Core;

using SafeFreeSpace.Core.Models;
using SafeFreeSpace.Core.Services;
using Xunit;

public class ConfirmationPhraseTests
{
    private readonly ConfirmationPhraseService _service = new();

    [Fact]
    public void WipePhrase_ContainsDriveLetter()
    {
        string phrase = _service.Generate(ProposedAction.WipeHddFreeSpace, "D");
        Assert.Equal("WIPE FREE SPACE D:", phrase);
    }

    [Fact]
    public void RetrimPhrase_ContainsDriveLetter()
    {
        string phrase = _service.Generate(ProposedAction.RetrimSsd, "E");
        Assert.Equal("RETRIM E:", phrase);
    }

    [Theory]
    [InlineData("WIPE FREE SPACE D:", ProposedAction.WipeHddFreeSpace, "D", true)]
    [InlineData("WIPE FREE SPACE D: ", ProposedAction.WipeHddFreeSpace, "D", true)]
    [InlineData(" WIPE FREE SPACE D:", ProposedAction.WipeHddFreeSpace, "D", true)]
    [InlineData("WIPE FREE SPACE d:", ProposedAction.WipeHddFreeSpace, "D", false)]
    [InlineData("WIPE FREE SPACE C:", ProposedAction.WipeHddFreeSpace, "D", false)]
    [InlineData("WIPE FREE SPACE D", ProposedAction.WipeHddFreeSpace, "D", false)]
    [InlineData("", ProposedAction.WipeHddFreeSpace, "D", false)]
    public void Validate_ReturnsExpected(string phrase, ProposedAction action, string drive, bool expected)
    {
        Assert.Equal(expected, _service.Validate(phrase, action, drive));
    }

    [Fact]
    public void InvalidDriveLetter_Throws()
    {
        Assert.Throws<ArgumentException>(() => _service.Generate(ProposedAction.WipeHddFreeSpace, "C:"));
        Assert.Throws<ArgumentException>(() => _service.Generate(ProposedAction.WipeHddFreeSpace, "&&"));
    }
}
