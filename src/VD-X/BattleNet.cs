using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace VDX;

/// <summary>
/// Blizzard games: found through their uninstall entries and started through the Battle.net client, because a game exe
/// started directly gets no login token and cannot connect.
/// </summary>
static class BattleNet
{
    /// <param name="LaunchCode">Product code for the client's "launch" command, or null if this app does not know it yet.</param>
    public sealed record Game(string Name, string Uid, string InstallDir, string? LaunchCode);

    /// <summary>Blizzard's install id (the uid in the uninstall entry) → product code of the client's "--exec=launch" command.</summary>
    static readonly Dictionary<string, string> LaunchCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["wow"] = "WoW",                        // World of Warcraft (retail)
        // The Classic versions share one family: "WoWC" starts whichever of them is selected in the client,
        // the launch command cannot name a single version.
        ["wow_classic"] = "WoWC",               // WoW Classic (progression)
        ["wow_classic_era"] = "WoWC",           // WoW Classic Era
        ["wow_classic_anniversary"] = "WoWC",   // WoW Classic Anniversary
        ["hs_beta"] = "WTCG",                                           // Hearthstone
        ["heroes"] = "Hero",                                            // Heroes of the Storm
        ["prometheus"] = "Pro",                                         // Overwatch
        ["fenris"] = "Fen",                                             // Diablo IV
        ["diablo3"] = "D3",                                             // Diablo III
        ["osi"] = "OSI",                                                // Diablo II: Resurrected
        ["anbs"] = "ANBS",                                              // Diablo Immortal
        ["s2"] = "S2",                                                  // StarCraft II
        ["s1"] = "S1",                                                  // StarCraft: Remastered
        ["w3"] = "W3",                                                  // Warcraft III: Reforged
        ["gryphon"] = "GRY",                                            // Warcraft Rumble
        ["wlby"] = "WLBY",                                              // Crash Bandicoot 4
        ["rtro"] = "RTRO",                                              // Blizzard Arcade Collection
    };

    const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    /// <summary>Battle.net.exe from the client's own uninstall entry, else the default install folder.</summary>
    public static string ClientPath
    {
        get
        {
            string folder = Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Battle.net");
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(UninstallKeyPath + @"\Battle.net");
            if (key?.GetValue("InstallLocation") is string location && location.Trim().Length > 0)
                folder = location.Trim().TrimEnd('\\');
            return Path.Combine(folder, "Battle.net.exe");
        }
    }

    public static string LaunchArguments(string launchCode) => $"--exec=\"launch {launchCode}\"";

    /// <summary>Games that share a key (launch code, install folder) become one entry named after all of them.</summary>
    public static List<Game> MergeBy(IEnumerable<Game> games, Func<Game, string> key, string suffix = "") =>
        games
            .GroupBy(key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Count() == 1 ? group.First() : group.First() with
            {
                Name = string.Join(" / ", group.Select(g => g.Name).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)) + suffix,
            })
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>Installed Blizzard games, one per Blizzard install id, sorted by name.</summary>
    public static List<Game> InstalledGames()
    {
        var games = new List<Game>();
        foreach (var (hive, view) in new[] { (RegistryHive.LocalMachine, RegistryView.Registry32), (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.CurrentUser, RegistryView.Default) })
        {
            using var uninstall = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(UninstallKeyPath);
            if (uninstall is null)
                continue;
            foreach (string subKey in uninstall.GetSubKeyNames())
            {
                using var key = uninstall.OpenSubKey(subKey);
                if (key?.GetValue("UninstallString") is not string command || !command.Contains("Blizzard Uninstaller", StringComparison.OrdinalIgnoreCase))
                    continue;
                var uid = Regex.Match(command, @"--uid=(\S+)");
                string installDir = (key.GetValue("InstallLocation") as string ?? "").Trim().TrimEnd('\\');
                if (!uid.Success || uid.Groups[1].Value.Equals("battle.net", StringComparison.OrdinalIgnoreCase) || installDir.Length == 0
                    || games.Any(g => g.Uid.Equals(uid.Groups[1].Value, StringComparison.OrdinalIgnoreCase)))
                    continue;
                games.Add(new Game(
                    key.GetValue("DisplayName") as string ?? uid.Groups[1].Value,
                    uid.Groups[1].Value,
                    installDir,
                    LaunchCodes.GetValueOrDefault(uid.Groups[1].Value)));
            }
        }
        return games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
