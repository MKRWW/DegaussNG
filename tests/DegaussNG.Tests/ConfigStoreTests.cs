using DegaussNG.Core;
using Xunit;

namespace DegaussNG.Tests;

public sealed class ConfigStoreTests
{
    private static string NewPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "DegaussNG.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, "config.json");
    }

    [Fact]
    public void LoadReturnsDefaultsWhenTheFileIsMissing()
    {
        AppConfig config = new ConfigStore(NewPath()).Load();

        Assert.Equal(0.3, config.Volume);
        Assert.Equal("Ctrl+Alt+D", config.Hotkey);
        Assert.False(config.ReducedMotion);
        Assert.True(config.DegaussEnabled);
    }

    [Fact]
    public void CorruptFileIsMovedToBackupAndDefaultsAreReturned()
    {
        string path = NewPath();
        File.WriteAllText(path, "{ this is not json");

        AppConfig config = new ConfigStore(path).Load();

        Assert.Equal(0.3, config.Volume);
        Assert.Equal("Ctrl+Alt+D", config.Hotkey);
        Assert.True(File.Exists(path + ".bak"));
    }

    [Fact]
    public void NoExceptionEscapesAndTheDefaultsAreReturnedAgainAfterTheBackup()
    {
        string path = NewPath();
        File.WriteAllText(path, "not json at all");

        ConfigStore store = new(path);

        AppConfig first = store.Load();
        Assert.True(File.Exists(path + ".bak"));

        AppConfig second = store.Load();
        Assert.Equal(first, second);
    }

    [Fact]
    public void VolumeAboveOneIsClampedOnLoad()
    {
        string path = NewPath();
        File.WriteAllText(path, "{\"Volume\": 7}");

        Assert.Equal(1.0, new ConfigStore(path).Load().Volume);
    }

    [Fact]
    public void VolumeBelowZeroIsClampedOnLoad()
    {
        string path = NewPath();
        File.WriteAllText(path, "{\"Volume\": -2}");

        Assert.Equal(0.0, new ConfigStore(path).Load().Volume);
    }

    [Fact]
    public void AnInvalidHotkeyIsReplacedByTheDefaultOnLoad()
    {
        string path = NewPath();
        File.WriteAllText(path, "{\"Hotkey\": \"Ctrl+Foo\"}");

        Assert.Equal("Ctrl+Alt+D", new ConfigStore(path).Load().Hotkey);
    }

    [Fact]
    public void DefaultPathPointsIntoAppData()
    {
        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DegaussNG",
            "config.json");

        Assert.Equal(expected, ConfigStore.DefaultPath);
    }

    [Fact]
    public void SaveWritesIndentedJsonAndCreatesTheFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), "DegaussNG.Tests", Guid.NewGuid().ToString("N"), "nested", "config.json");

        ConfigStore store = new(path);
        store.Save(new AppConfig { Volume = 0.5, Hotkey = "Ctrl+Alt+X" });

        Assert.True(File.Exists(path));

        string text = File.ReadAllText(path);
        Assert.Contains(Environment.NewLine, text);

        AppConfig loaded = store.Load();
        Assert.Equal(0.5, loaded.Volume);
        Assert.Equal("Ctrl+Alt+X", loaded.Hotkey);
    }
}
