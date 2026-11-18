using System.Text.Json;

namespace DegaussNG.Core;

/// <summary>
/// Reads and writes the JSON config file.
/// </summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public ConfigStore(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// %APPDATA%\DegaussNG\config.json
    /// </summary>
    public static string DefaultPath
    {
        get
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "DegaussNG", "config.json");
        }
    }

    /// <summary>
    /// Returns the stored config, or the defaults when the file is missing. A corrupt file is
    /// moved to config.json.bak and the defaults are returned; no exception escapes.
    /// </summary>
    public AppConfig Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppConfig().Normalized();
        }

        try
        {
            string text = File.ReadAllText(_filePath);
            AppConfig? loaded = JsonSerializer.Deserialize<AppConfig>(text);
            return (loaded ?? new AppConfig()).Normalized();
        }
        catch (Exception)
        {
            try
            {
                File.Move(_filePath, BackupPath, overwrite: true);
            }
            catch (Exception)
            {
                // The backup could not be written; the defaults are still returned.
            }

            return new AppConfig().Normalized();
        }
    }

    /// <summary>
    /// Writes through a temp file in the same folder, then replaces the target.
    /// </summary>
    public void Save(AppConfig config)
    {
        string? directory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrEmpty(directory))
        {
            directory = ".";
        }

        Directory.CreateDirectory(directory);

        string tempPath = Path.Combine(directory, Path.GetFileName(_filePath) + ".tmp");
        File.WriteAllText(tempPath, JsonSerializer.Serialize(config.Normalized(), SerializerOptions));

        try
        {
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception)
        {
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception)
            {
                // Nothing left to clean up.
            }

            throw;
        }
    }

    private string BackupPath => _filePath + ".bak";
}
