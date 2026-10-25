using System.Text.Json;
using System.Text.Json.Serialization;

namespace VDX;

/// <summary>User settings, stored as JSON in %AppData%\VD-X\settings.json.</summary>
sealed class AppSettings
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    static string FolderPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VD-X");

    public static string FilePath => Path.Combine(FolderPath, "settings.json");

    /// <summary>Action name → hotkey ("F14", "Ctrl+Alt+Right", …). An empty string means "no hotkey".</summary>
    public Dictionary<string, string> Hotkeys { get; set; } = [];

    /// <summary>Started (and moved to their desktop) when the app starts with Windows.</summary>
    public List<StartupProgram> StartupPrograms { get; set; } = [];

    public NewWindowSettings NewWindows { get; set; } = new();

    /// <summary>Run elevated so windows of elevated programs can be moved and pinned too.</summary>
    public bool RunAsAdmin { get; set; }

    public static AppSettings Load(out bool firstRun)
    {
        firstRun = !File.Exists(FilePath);
        if (firstRun)
        {
            // Write the defaults out explicitly so the settings file shows what is active.
            var defaults = new AppSettings();
            foreach (var info in HotkeyActions.All)
                defaults.SetHotkey(info.Action, info.DefaultHotkey);
            return defaults;
        }
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(FolderPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public Hotkey? GetHotkey(HotkeyAction action)
    {
        if (!Hotkeys.TryGetValue(action.ToString(), out string? text))
            return HotkeyActions.Get(action).DefaultHotkey;
        return Hotkey.TryParse(text, out var hotkey) ? hotkey : null;
    }

    public void SetHotkey(HotkeyAction action, Hotkey? hotkey) =>
        Hotkeys[action.ToString()] = hotkey?.ToString() ?? "";
}

sealed class StartupProgram
{
    /// <summary>1 = first desktop.</summary>
    public int Desktop { get; set; } = 1;
    public string Path { get; set; } = "";
    public string Arguments { get; set; } = "";
}

enum RouteMode
{
    Off,
    /// <summary>Move new windows, stay on the current desktop.</summary>
    Silent,
    /// <summary>Move new windows and switch to their desktop.</summary>
    Active,
}

sealed class NewWindowSettings
{
    public RouteMode Mode { get; set; } = RouteMode.Off;
    /// <summary>1 = first desktop.</summary>
    public int TargetDesktop { get; set; } = 1;
    /// <summary>Exe names whose windows are left alone, e.g. "msedge.exe".</summary>
    public List<string> Exclude { get; set; } = [];
}
