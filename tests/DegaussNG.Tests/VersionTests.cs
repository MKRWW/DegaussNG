using System;
using DegaussNG.Core;
using Xunit;

namespace DegaussNG.Tests;

public sealed class VersionTests
{
    [Fact]
    public void CoreAssemblyHasVersionOneZeroZeroZero()
    {
        Version? version = typeof(AppConfig).Assembly.GetName().Version;

        Assert.Equal(new Version(1, 0, 0, 0), version);
    }
}
