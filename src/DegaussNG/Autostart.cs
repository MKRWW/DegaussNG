using System;
using DegaussNG.Core;
using Microsoft.Win32;

namespace DegaussNG;

/// <summary>
/// The sign-in entry of the app. Nothing is written here unless the tray item is switched on,
/// and an entry of another program is left alone.
/// </summary>
internal static class Autostart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DegaussNG";

    internal static bool IsEnabled()
    {
        string path = Environment.ProcessPath ?? string.Empty;

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return AutostartCommand.IsOurs(key?.GetValue(ValueName) as string, path);
        }
        catch (Exception exception)
        {
            Log.Error("autostart: read failed: " + exception.Message);
            return false;
        }
    }

    internal static void Enable()
    {
        string path = Environment.ProcessPath ?? string.Empty;

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null)
            {
                Log.Error("autostart: " + RunKeyPath + " could not be opened");
                return;
            }

            key.SetValue(ValueName, AutostartCommand.Build(path));
        }
        catch (Exception exception)
        {
            Log.Error("autostart: write failed: " + exception.Message);
        }
    }

    /// <summary>
    /// Removes our entry. A missing entry is left as it is.
    /// </summary>
    internal static void Disable()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null || key.GetValue(ValueName) == null)
            {
                return;
            }

            key.DeleteValue(ValueName);
        }
        catch (Exception exception)
        {
            Log.Error("autostart: delete failed: " + exception.Message);
        }
    }
}
