namespace VDX;

/// <summary>Moves every newly opened window to one desktop.</summary>
sealed class NewWindowRouter(AppSettings settings)
{
    // Give the window a moment to finish appearing; moving it immediately can fail.
    const int DelayMs = 150;

    public event Action<string>? Error;

    public async void OnWindowCreated(IntPtr hwnd)
    {
        if (settings.NewWindows.Mode == RouteMode.Off)
            return;
        await Task.Delay(DelayMs);
        try
        {
            Route(hwnd, settings.NewWindows);
        }
        catch (Exception ex)
        {
            // async void: an escaping exception would crash the app.
            Error?.Invoke(ex.Message);
        }
    }

    static void Route(IntPtr hwnd, NewWindowSettings options)
    {
        if (options.Mode == RouteMode.Off || !NativeMethods.IsWindow(hwnd) || !NativeMethods.IsAppWindow(hwnd) || DesktopActions.IsShellWindow(hwnd))
            return;
        int processId = NativeMethods.GetWindowProcessId(hwnd);
        if (processId == Environment.ProcessId)
            return;
        string processPath = NativeMethods.GetProcessPath(processId);
        if (ProgramLauncher.IsLaunching(processPath) || IsExcluded(processPath, options.Exclude))
            return;

        int target = Math.Max(options.TargetDesktop, 1) - 1;
        DesktopActions.EnsureDesktopExists(target);
        if (Vda.GetWindowDesktopNumber(hwnd) == target)
            return;
        // -1 happens e.g. for windows of elevated programs while we are not elevated; just leave those where they are.
        if (Vda.MoveWindowToDesktopNumber(hwnd, target) == -1)
            return;
        if (options.Mode == RouteMode.Active)
            Vda.GoToDesktopNumber(target);
    }

    static bool IsExcluded(string processPath, List<string> exclude)
    {
        string exe = Path.GetFileName(processPath);
        return exclude.Select(e => e.Trim()).Any(name => name.Length > 0
            && (exe.Equals(name, StringComparison.OrdinalIgnoreCase) || exe.Equals(name + ".exe", StringComparison.OrdinalIgnoreCase)));
    }
}
