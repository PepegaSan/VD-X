using System.ComponentModel;
using System.Diagnostics;

namespace VDX;

/// <summary>Starts programs and moves their windows to a fixed desktop.</summary>
static class ProgramLauncher
{
    static readonly TimeSpan WindowTimeout = TimeSpan.FromSeconds(45);
    // Splash screens and launchers open more windows, so keep watching a little after the first move.
    static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(5);

    static readonly List<string> Launching = [];

    /// <summary>True while windows of this exe are expected from a launch; <see cref="NewWindowRouter"/> leaves them alone.</summary>
    public static bool IsLaunching(string processPath) =>
        Launching.Contains(Path.GetFileName(processPath), StringComparer.OrdinalIgnoreCase);

    /// <summary>Starts all programs in parallel. Returns one message per program that failed. Call on the UI thread.</summary>
    public static async Task<List<string>> RunAllAsync(IEnumerable<StartupProgram> programs)
    {
        var errors = new List<string>();
        await Task.WhenAll(programs.Where(p => p.Path.Trim().Length > 0).Select(async program =>
        {
            try
            {
                await RunAsync(program);
            }
            catch (Exception ex) when (ex is DesktopActionException or Win32Exception or InvalidOperationException)
            {
                errors.Add($"{Path.GetFileName(CleanPath(program.Path))}: {ex.Message}");
            }
        }));
        return errors;
    }

    static async Task RunAsync(StartupProgram program)
    {
        int target = Math.Max(program.Desktop, 1) - 1;
        DesktopActions.EnsureDesktopExists(target);

        string path = CleanPath(program.Path);
        string exeName = Path.GetFileName(path);
        var knownWindows = NativeMethods.GetAppWindows().ToHashSet();
        Launching.Add(exeName);
        try
        {
            var startInfo = new ProcessStartInfo(path, program.Arguments)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.IsPathRooted(path) ? Path.GetDirectoryName(path) ?? "" : "",
            };
            using var process = Process.Start(startInfo);
            int? processId = process?.Id;   // null if the shell handed the file to an already running app

            var started = DateTime.UtcNow;
            DateTime? firstMove = null;
            while (DateTime.UtcNow - started < WindowTimeout && (firstMove is null || DateTime.UtcNow - firstMove < SettleTime))
            {
                await Task.Delay(250);
                foreach (IntPtr hwnd in NativeMethods.GetAppWindows())
                {
                    if (!knownWindows.Add(hwnd))
                        continue;
                    // Match by process, or by exe name for launchers that hand over to a second process.
                    int windowProcessId = NativeMethods.GetWindowProcessId(hwnd);
                    bool ours = windowProcessId == processId
                        || string.Equals(Path.GetFileName(NativeMethods.GetProcessPath(windowProcessId)), exeName, StringComparison.OrdinalIgnoreCase);
                    if (ours && Vda.MoveWindowToDesktopNumber(hwnd, target) != -1)
                        firstMove ??= DateTime.UtcNow;
                }
            }
            if (firstMove is null)
                throw new DesktopActionException(L.T(
                    $"no window appeared within {WindowTimeout.TotalSeconds:0} seconds (or it could not be moved).",
                    $"innerhalb von {WindowTimeout.TotalSeconds:0} Sekunden kam kein Fenster (oder es ließ sich nicht verschieben)."));
        }
        finally
        {
            Launching.Remove(exeName);
        }
    }

    static string CleanPath(string path) => Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
}
