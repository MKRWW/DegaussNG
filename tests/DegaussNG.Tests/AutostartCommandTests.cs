using DegaussNG.Core;
using Xunit;

namespace DegaussNG.Tests;

public sealed class AutostartCommandTests
{
    private const string ExecutablePath = @"C:\Apps\DegaussNG.exe";

    [Fact]
    public void BuildQuotesThePathAndAddsTheArgument()
    {
        Assert.Equal("\"C:\\Apps\\DegaussNG.exe\" --autostart", AutostartCommand.Build(ExecutablePath));
        Assert.Equal("--autostart", AutostartCommand.Argument);
    }

    [Fact]
    public void OurOwnCommandIsRecognisedInOtherLetterCase()
    {
        Assert.True(AutostartCommand.IsOurs(AutostartCommand.Build(ExecutablePath), ExecutablePath));
        Assert.True(AutostartCommand.IsOurs("\"c:\\apps\\degaussng.exe\" --autostart", ExecutablePath));
    }

    [Fact]
    public void NullAndEmptyAreNotOurs()
    {
        Assert.False(AutostartCommand.IsOurs(null, ExecutablePath));
        Assert.False(AutostartCommand.IsOurs(string.Empty, ExecutablePath));
    }

    [Fact]
    public void AnotherPathAndThePathWithoutTheArgumentAreNotOurs()
    {
        Assert.False(AutostartCommand.IsOurs(AutostartCommand.Build(@"C:\Other\DegaussNG.exe"), ExecutablePath));
        Assert.False(AutostartCommand.IsOurs("\"C:\\Apps\\DegaussNG.exe\"", ExecutablePath));
        Assert.False(AutostartCommand.IsOurs("\"C:\\Apps\\DegaussNG.exe\" --boot", ExecutablePath));
    }
}
