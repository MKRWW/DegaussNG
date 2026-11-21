using System;
using System.IO;
using System.Xml.Linq;
using DegaussNG.Core;
using Xunit;

namespace DegaussNG.Tests;

public sealed class VersionTests
{
    [Fact]
    public void CoreAssemblyHasTheVersionFromDirectoryBuildProps()
    {
        string? directory = AppContext.BaseDirectory;

        while (directory != null && !File.Exists(Path.Combine(directory, "Directory.Build.props")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        Assert.NotNull(directory);

        XDocument props = XDocument.Load(Path.Combine(directory, "Directory.Build.props"));
        Version? expected = Version.Parse(props.Descendants("Version").Single().Value);
        Version? actual = typeof(AppConfig).Assembly.GetName().Version;

        Assert.NotNull(expected);
        Assert.NotNull(actual);

        Assert.Equal(new Version(expected.Major, expected.Minor, expected.Build, 0), actual);
    }
}
