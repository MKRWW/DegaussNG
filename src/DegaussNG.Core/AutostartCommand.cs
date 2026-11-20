namespace DegaussNG.Core;

/// <summary>
/// The command the app writes into the sign-in key. Comparing the whole string means an
/// entry of another program is never mistaken for ours.
/// </summary>
public static class AutostartCommand
{
    public const string Argument = "--autostart";

    public static string Build(string executablePath)
    {
        return "\"" + executablePath + "\" " + Argument;
    }

    /// <summary>
    /// True when the stored value is exactly the command we would write for this executable.
    /// Null and an empty value are not ours.
    /// </summary>
    public static bool IsOurs(string? value, string executablePath)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return string.Equals(value, Build(executablePath), StringComparison.OrdinalIgnoreCase);
    }
}
