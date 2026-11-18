using DegaussNG.Core;
using Xunit;

namespace DegaussNG.Tests;

public sealed class HotkeyGestureTests
{
    [Fact]
    public void ParsesCaseInsensitiveAndRendersInCanonicalOrder()
    {
        Assert.True(HotkeyGesture.TryParse("ctrl+alt+d", out HotkeyGesture gesture));
        Assert.Equal("Ctrl+Alt+D", gesture.ToString());
    }

    [Fact]
    public void RendersModifiersInCanonicalOrderRegardlessOfTheInputOrder()
    {
        Assert.True(HotkeyGesture.TryParse("shift+ctrl+a", out HotkeyGesture gesture));
        Assert.Equal("Ctrl+Shift+A", gesture.ToString());
    }

    [Fact]
    public void AcceptsDigitsAndFunctionKeys()
    {
        Assert.True(HotkeyGesture.TryParse("alt+shift+win+f12", out HotkeyGesture gesture));
        Assert.Equal("Alt+Shift+Win+F12", gesture.ToString());

        Assert.True(HotkeyGesture.TryParse("Ctrl+9", out HotkeyGesture digit));
        Assert.Equal("Ctrl+9", digit.ToString());
    }

    [Fact]
    public void RejectsTwoKeys()
    {
        Assert.False(HotkeyGesture.TryParse("Ctrl+A+B", out _));
    }

    [Theory]
    [InlineData("D")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Foo")]
    [InlineData("Ctrl+F25")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void RejectsAnythingWithoutExactlyOneKeyAndModifier(string? text)
    {
        Assert.False(HotkeyGesture.TryParse(text, out _));
    }
}
