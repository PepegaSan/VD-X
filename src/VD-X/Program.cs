namespace VDX;

static class Program
{
    public const string AppName = "VD-X";

    const string MutexName = "VD-X_SingleInstance";
    const string ExitArgument = "--exit";
    const string UninstallArgument = "--uninstall";

    [STAThread]
    static int Main(string[] args)
    {
        // Command-line helpers used by the installer/uninstaller and by the elevated autostart switch.
        if (Has(args, ExitArgument))
            return BackgroundWindow.RequestExit(TimeSpan.FromSeconds(10)) ? 0 : 1;
        if (Has(args, UninstallArgument))
            return Uninstall();
        int setIndex = Array.FindIndex(args, a => a.Equals(Autostart.SetArgument, StringComparison.OrdinalIgnoreCase));
        if (setIndex >= 0)
            return SetAutostart(args.ElementAtOrDefault(setIndex + 1));

        bool restarted = Has(args, Elevation.RestartedArgument);
        bool startedByAutostart = Has(args, Autostart.Argument);
        using var mutex = AcquireSingleInstance(waitForPrevious: restarted);
        if (mutex is null)
        {
            // Already running: open its settings window instead (but not for a duplicate autostart at sign-in).
            if (!startedByAutostart)
                BackgroundWindow.RequestShowSettings();
            return 0;
        }

        try
        {
            ApplicationConfiguration.Initialize();

            if (!WaitForVirtualDesktops(startedByAutostart, out string error))
            {
                MessageBox.Show(error, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            var settings = AppSettings.Load(out bool firstRun);
            if (settings.RunAsAdmin && !Elevation.IsElevated && !restarted && Elevation.TryRestartElevated())
                return 0;   // the elevated copy takes over as soon as we release the mutex

            Application.Run(new TrayApp(settings, firstRun, startedByAutostart));
            return 0;
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    static bool Has(string[] args, string argument) => args.Contains(argument, StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the owned mutex, or null if another instance is running.</summary>
    static Mutex? AcquireSingleInstance(bool waitForPrevious)
    {
        Mutex mutex;
        try
        {
            mutex = new Mutex(true, MutexName, out bool createdNew);
            if (createdNew)
                return mutex;
        }
        catch (UnauthorizedAccessException)
        {
            return null;   // held by an instance running as administrator
        }

        // After an elevated restart the previous instance is still shutting down.
        try
        {
            if (waitForPrevious && mutex.WaitOne(TimeSpan.FromSeconds(10)))
                return mutex;
        }
        catch (AbandonedMutexException)
        {
            return mutex;
        }
        mutex.Dispose();
        return null;
    }

    /// <summary>Right after sign-in Explorer may not be ready yet, so autostart retries for a while.</summary>
    static bool WaitForVirtualDesktops(bool startedByAutostart, out string error)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(startedByAutostart ? 60 : 0);
        while (true)
        {
            if (Vda.TryLoad(out error))
                return true;
            if (DateTime.UtcNow >= deadline)
                return false;
            Thread.Sleep(2000);
        }
    }

    static int SetAutostart(string? mode)
    {
        if (!Enum.TryParse(mode, ignoreCase: true, out AutostartMode parsed))
            return 2;
        try
        {
            Autostart.Apply(parsed);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return 1;
        }
    }

    /// <summary>Called by the uninstaller: stop the running app and remove autostart (task removal asks for admin rights).</summary>
    static int Uninstall()
    {
        BackgroundWindow.RequestExit(TimeSpan.FromSeconds(10));
        try
        {
            return Autostart.Change(AutostartMode.Off) ? 0 : 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return 1;
        }
    }
}
