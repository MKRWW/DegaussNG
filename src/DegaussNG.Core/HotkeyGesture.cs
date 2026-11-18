namespace DegaussNG.Core;

/// <summary>
/// Hotkey modifier bits, using the values of the Win32 modifier flags.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Ctrl = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>
/// A parsed hotkey: at least one modifier and exactly one key.
/// Keys are A-Z, 0-9 and F1-F24, stored as Win32 virtual key codes.
/// </summary>
public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, uint VirtualKey)
{
    private const uint VkDigit0 = 0x30;
    private const uint VkLetterA = 0x41;
    private const uint VkF1 = 0x70;

    /// <summary>
    /// Case-insensitive parse of a text such as "ctrl+alt+d". Requires at least one
    /// modifier and exactly one key.
    /// </summary>
    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;

        if (text is null)
        {
            return false;
        }

        HotkeyModifiers modifiers = HotkeyModifiers.None;
        uint virtualKey = 0;
        bool keyFound = false;

        foreach (string rawPart in text.Split('+'))
        {
            string part = rawPart.Trim();
            if (part.Length == 0)
            {
                return false;
            }

            if (TryParseModifier(part, out HotkeyModifiers parsedModifier))
            {
                if ((modifiers & parsedModifier) != HotkeyModifiers.None)
                {
                    return false;
                }

                modifiers |= parsedModifier;
                continue;
            }

            if (keyFound)
            {
                return false;
            }

            virtualKey = ParseKey(part);
            if (virtualKey == 0)
            {
                return false;
            }

            keyFound = true;
        }

        if (modifiers == HotkeyModifiers.None || !keyFound)
        {
            return false;
        }

        gesture = new HotkeyGesture(modifiers, virtualKey);
        return true;
    }

    /// <summary>
    /// Canonical form: Ctrl+Alt+Shift+Win+Key.
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string>(5);

        if ((Modifiers & HotkeyModifiers.Ctrl) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((Modifiers & HotkeyModifiers.Alt) != 0)
        {
            parts.Add("Alt");
        }

        if ((Modifiers & HotkeyModifiers.Shift) != 0)
        {
            parts.Add("Shift");
        }

        if ((Modifiers & HotkeyModifiers.Win) != 0)
        {
            parts.Add("Win");
        }

        parts.Add(KeyText(VirtualKey));

        return string.Join("+", parts);
    }

    private static bool TryParseModifier(string part, out HotkeyModifiers modifiers)
    {
        switch (part.ToUpperInvariant())
        {
            case "CTRL":
                modifiers = HotkeyModifiers.Ctrl;
                return true;
            case "ALT":
                modifiers = HotkeyModifiers.Alt;
                return true;
            case "SHIFT":
                modifiers = HotkeyModifiers.Shift;
                return true;
            case "WIN":
                modifiers = HotkeyModifiers.Win;
                return true;
            default:
                modifiers = HotkeyModifiers.None;
                return false;
        }
    }

    private static uint ParseKey(string part)
    {
        string key = part.ToUpperInvariant();

        if (key.Length == 1)
        {
            char c = key[0];
            if (c >= 'A' && c <= 'Z')
            {
                return VkLetterA + (uint)(c - 'A');
            }

            if (c >= '0' && c <= '9')
            {
                return VkDigit0 + (uint)(c - '0');
            }

            return 0;
        }

        if (key[0] == 'F')
        {
            if (!int.TryParse(key.Substring(1), out int number))
            {
                return 0;
            }

            if (number < 1 || number > 24)
            {
                return 0;
            }

            return VkF1 + (uint)(number - 1);
        }

        return 0;
    }

    private static string KeyText(uint virtualKey)
    {
        if (virtualKey >= VkF1 && virtualKey <= VkF1 + 23)
        {
            return "F" + (virtualKey - VkF1 + 1);
        }

        if (virtualKey >= VkLetterA && virtualKey <= VkLetterA + 25)
        {
            return ((char)('A' + (virtualKey - VkLetterA))).ToString();
        }

        if (virtualKey >= VkDigit0 && virtualKey <= VkDigit0 + 9)
        {
            return ((char)('0' + (virtualKey - VkDigit0))).ToString();
        }

        return virtualKey.ToString();
    }
}
