namespace VDX;

/// <summary>Moves newly opened windows to a desktop: per-program rules first, then the catch-all mode for everything else.</summary>
sealed class NewWindowRouter(AppSettings settings)
{
    // Give the window a moment to finish appearing; moving it immediately can fail.
    const int DelayMs = 150;
    // Programs often activate a window right after it was moved, which makes Windows switch to that desktop;
    // for this long after a background move such a switch is undone so the user stays where they were.
    static readonly TimeSpan StayGuardTime = TimeSpan.FromSeconds(10);

    public event Action<string>? Error;

    public async void OnWindowCreated(IntPtr hwnd)
    {
        var options = settings.NewWindows;
        if (options.Mode == RouteMode.Off && options.Rules.Count == 0)
            return;
        await Task.Delay(DelayMs);
        try
        {
            await RouteAsync(hwnd, options);
        }
        catch (Exception ex)
        {
            // async void: an escaping exception would crash the app.
            Error?.Invoke(ex.Message);
        }
    }

    static async Task RouteAsync(IntPtr hwnd, NewWindowSettings options)
    {
        if (!NativeMethods.IsWindow(hwnd) || !NativeMethods.IsAppWindow(hwnd) || DesktopActions.IsShellWindow(hwnd))
            return;
        int processId = NativeMethods.GetWindowProcessId(hwnd);
        if (processId == Environment.ProcessId)
            return;
        string processPath = NativeMethods.GetProcessPath(processId);
        if (ProgramLauncher.IsLaunching(processPath))
            return;

        int target;
        bool follow;
        var rule = options.Rules.FirstOrDefault(r => ProgramMatch.Matches(r.Program.Trim(), processPath));
        if (rule is not null)
        {
            target = Math.Max(rule.Desktop, 1) - 1;
            follow = rule.Follow;
        }
        else if (options.Mode != RouteMode.Off && !IsExcluded(processPath, options.Exclude))
        {
            target = Math.Max(options.TargetDesktop, 1) - 1;
            follow = options.Mode == RouteMode.Active;
        }
        else
            return;

        DesktopActions.EnsureDesktopExists(target);
        if (Vda.GetWindowDesktopNumber(hwnd) == target)
            return;
        int current = Vda.GetCurrentDesktopNumber();
        // -1 happens e.g. for windows of elevated programs while we are not elevated; just leave those where they are.
        if (Vda.MoveWindowToDesktopNumber(hwnd, target) == -1)
            return;
        if (follow)
            Vda.GoToDesktopNumber(target);
        else if (current != target)
            await KeepUserOnAsync(current, target, processId);
    }

    // The program usually activates the moved window and Windows follows it to the target desktop; take the user back.
    static async Task KeepUserOnAsync(int desktop, int target, int processId)
    {
        var until = DateTime.UtcNow + StayGuardTime;
        while (DateTime.UtcNow < until)
        {
            await Task.Delay(250);
            if (Vda.GetCurrentDesktopNumber() == target && NativeMethods.GetWindowProcessId(NativeMethods.GetForegroundWindow()) == processId)
                Vda.GoToDesktopNumber(desktop);
        }
    }

    static bool IsExcluded(string processPath, List<string> exclude)
    {
        string exe = Path.GetFileName(processPath);
        return exclude.Select(e => e.Trim()).Any(name => name.Length > 0
            && (exe.Equals(name, StringComparison.OrdinalIgnoreCase) || exe.Equals(name + ".exe", StringComparison.OrdinalIgnoreCase)));
    }
}
