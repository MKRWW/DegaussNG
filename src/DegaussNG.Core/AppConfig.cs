namespace DegaussNG.Core;

/// <summary>
/// The two modem sound modes. Short is the default, Full is for purists.
/// </summary>
public static class ModemModes
{
    public const string Short = "Short";
    public const string Full = "Full";
}

/// <summary>
/// The settings of the tray app. All values are stored as JSON and are always
/// read back through <see cref="Normalized"/>.
/// </summary>
public sealed record AppConfig
{
    private const string DefaultHotkeyText = "Ctrl+Alt+D";
    private const double DefaultVolume = 0.3;

    public bool DegaussEnabled { get; init; } = true;

    public bool ReducedMotion { get; init; } = false;

    public double Volume { get; init; } = 0.3;

    public string Hotkey { get; init; } = "Ctrl+Alt+D";

    public bool ModemEnabled { get; init; } = true;

    public string ModemMode { get; init; } = ModemModes.Short;

    /// <summary>
    /// Returns a copy with Volume clamped to 0..1, an invalid Hotkey replaced by the default and
    /// ModemMode mapped to Short or Full.
    /// </summary>
    public AppConfig Normalized()
    {
        double volume = double.IsNaN(Volume) || double.IsInfinity(Volume) ? DefaultVolume : Math.Clamp(Volume, 0.0, 1.0);

        string hotkey = HotkeyGesture.TryParse(Hotkey, out HotkeyGesture gesture)
            ? gesture.ToString()
            : DefaultHotkeyText;

        string mode = string.Equals(ModemMode, ModemModes.Full, StringComparison.OrdinalIgnoreCase)
            ? ModemModes.Full
            : ModemModes.Short;

        return new AppConfig
        {
            DegaussEnabled = DegaussEnabled,
            ReducedMotion = ReducedMotion,
            Volume = volume,
            Hotkey = hotkey,
            ModemEnabled = ModemEnabled,
            ModemMode = mode,
        };
    }
}
