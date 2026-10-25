namespace VDX;

/// <summary>Same bit values as the MOD_* flags of RegisterHotKey.</summary>
[Flags]
enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

/// <summary>A key plus modifiers. ToString() is the settings-file format, e.g. "Ctrl+Alt+F14".</summary>
readonly record struct Hotkey(Keys Key, HotkeyModifiers Modifiers)
{
    /// <summary>Plain keys would be swallowed system-wide, so only "unused" keys may go without a modifier.</summary>
    public bool IsValid => Modifiers != HotkeyModifiers.None || IsStandaloneKey(Key);

    static bool IsStandaloneKey(Keys key) =>
        key is >= Keys.F1 and <= Keys.F24
            or Keys.Pause or Keys.Scroll
            or >= Keys.BrowserBack and <= Keys.LaunchApplication2;

    public override string ToString() => FormatModifiers(Modifiers, "Ctrl") + KeyName(Key);

    public string ToDisplayString() => ModifiersToDisplayString(Modifiers) + KeyName(Key);

    /// <summary>E.g. "Strg+Alt+" — used while the user is still holding only modifiers.</summary>
    public static string ModifiersToDisplayString(HotkeyModifiers modifiers) => FormatModifiers(modifiers, L.T("Ctrl", "Strg"));

    static string FormatModifiers(HotkeyModifiers modifiers, string ctrlName)
    {
        string text = "";
        if (modifiers.HasFlag(HotkeyModifiers.Control)) text += ctrlName + "+";
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) text += "Alt+";
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) text += "Shift+";
        if (modifiers.HasFlag(HotkeyModifiers.Win)) text += "Win+";
        return text;
    }

    static string KeyName(Keys key) =>
        key is >= Keys.D0 and <= Keys.D9 ? ((char)('0' + (key - Keys.D0))).ToString() : key.ToString();

    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var modifiers = HotkeyModifiers.None;
        Keys? key = null;
        foreach (string part in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "strg" or "control": modifiers |= HotkeyModifiers.Control; continue;
                case "alt": modifiers |= HotkeyModifiers.Alt; continue;
                case "shift": modifiers |= HotkeyModifiers.Shift; continue;
                case "win": modifiers |= HotkeyModifiers.Win; continue;
            }
            if (key is not null)
                return false;
            if (part.Length == 1 && char.IsAsciiDigit(part[0]))
                key = Keys.D0 + (part[0] - '0');
            else if (!part.All(char.IsAsciiDigit) && Enum.TryParse(part, ignoreCase: true, out Keys parsed) && (parsed & Keys.Modifiers) == 0)
                key = parsed;
            else
                return false;
        }
        if (key is null)
            return false;
        hotkey = new Hotkey(key.Value, modifiers);
        return true;
    }
}
