using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;
using Microsoft.Win32;

namespace VDX;

enum AutostartMode
{
    Off,
    /// <summary>HKCU\...\Run entry — no admin rights needed.</summary>
    Normal,
    /// <summary>Task Scheduler task with "run with highest privileges", so no UAC prompt at every sign-in.</summary>
    Admin,
}

/// <summary>Start with Windows, either via the Run key or (as administrator) via Task Scheduler.</summary>
static class Autostart
{
    public const string Argument = "--autostart";
    public const string SetArgument = "--set-autostart";

    const string Name = "VD-X";
    const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    // Task Manager's "Startup apps" switch lives here; an odd first byte (0x03, 0x07) means "disabled".
    const string ApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    static string ExePath => Environment.ProcessPath!;
    static string Command => $"\"{ExePath}\" {Argument}";

    public static AutostartMode CurrentMode =>
        AdminTaskExists() ? AutostartMode.Admin : IsRunEntryEnabled() ? AutostartMode.Normal : AutostartMode.Off;

    /// <summary>Creating or removing the Task Scheduler task needs administrator rights.</summary>
    public static bool NeedsElevation(AutostartMode mode) =>
        !Elevation.IsElevated && (mode == AutostartMode.Admin || AdminTaskExists());

    /// <summary>Switches to <paramref name="mode"/>, via an elevated copy of the exe if necessary. False if that failed or UAC was declined.</summary>
    public static bool Change(AutostartMode mode)
    {
        if (NeedsElevation(mode))
            return Elevation.RunElevated($"{SetArgument} {mode}");
        Apply(mode);
        return true;
    }

    /// <summary>Does the actual work; must already be elevated if a task is created or removed.</summary>
    public static void Apply(AutostartMode mode)
    {
        if (mode == AutostartMode.Normal)
            SetRunEntry();
        else
            DeleteRunEntry();

        if (mode == AutostartMode.Admin)
            CreateAdminTask();
        else if (AdminTaskExists())
            RunSchtasks($"/Delete /TN \"{Name}\" /F");
    }

    /// <summary>
    /// Keeps exactly one autostart: drops a leftover Run entry when the admin task exists (e.g. written by the installer),
    /// and if the Run entry points to an exe that no longer exists (app moved or reinstalled elsewhere), points it here.
    /// </summary>
    public static void Repair()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (run?.GetValue(Name) is not string command)
            return;
        if (AdminTaskExists())
        {
            run.DeleteValue(Name, throwOnMissingValue: false);
            return;
        }
        if (command == Command)
            return;
        string oldExe = command.Split('"', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
        if (!File.Exists(oldExe))
            run.SetValue(Name, Command);
    }

    static bool IsRunEntryEnabled()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        if (run?.GetValue(Name) is not string)
            return false;
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath);
        return approved?.GetValue(Name) is not byte[] { Length: > 0 } flags || (flags[0] & 1) == 0;
    }

    static void SetRunEntry()
    {
        using (var run = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            run.SetValue(Name, Command);
        // Clear a "disabled in Task Manager" flag, otherwise Windows would silently skip the entry.
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath, writable: true);
        approved?.DeleteValue(Name, throwOnMissingValue: false);
    }

    static void DeleteRunEntry()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        run?.DeleteValue(Name, throwOnMissingValue: false);
    }

    static bool AdminTaskExists() => RunSchtasks($"/Query /TN \"{Name}\"", throwOnError: false);

    static void CreateAdminTask()
    {
        // Built as XML because schtasks' command-line options cannot turn off the defaults
        // "stop after 3 days" and "don't start on battery".
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        string user = WindowsIdentity.GetCurrent().Name;
        var task = new XElement(ns + "Task", new XAttribute("version", "1.2"),
            new XElement(ns + "RegistrationInfo",
                new XElement(ns + "Description", "VD-X (start with administrator rights)")),
            new XElement(ns + "Triggers",
                new XElement(ns + "LogonTrigger",
                    new XElement(ns + "Enabled", "true"),
                    new XElement(ns + "UserId", user))),
            new XElement(ns + "Principals",
                new XElement(ns + "Principal", new XAttribute("id", "Author"),
                    new XElement(ns + "UserId", user),
                    new XElement(ns + "LogonType", "InteractiveToken"),
                    new XElement(ns + "RunLevel", "HighestAvailable"))),
            new XElement(ns + "Settings",
                new XElement(ns + "MultipleInstancesPolicy", "IgnoreNew"),
                new XElement(ns + "DisallowStartIfOnBatteries", "false"),
                new XElement(ns + "StopIfGoingOnBatteries", "false"),
                new XElement(ns + "ExecutionTimeLimit", "PT0S"),
                new XElement(ns + "Priority", "4")),
            new XElement(ns + "Actions", new XAttribute("Context", "Author"),
                new XElement(ns + "Exec",
                    new XElement(ns + "Command", ExePath),
                    new XElement(ns + "Arguments", Argument),
                    new XElement(ns + "WorkingDirectory", Path.GetDirectoryName(ExePath)))));

        string xmlPath = Path.Combine(Path.GetTempPath(), $"{Name}-task.xml");
        File.WriteAllText(xmlPath, new XDocument(new XDeclaration("1.0", "UTF-16", null), task).ToString(), Encoding.Unicode);
        try
        {
            RunSchtasks($"/Create /TN \"{Name}\" /XML \"{xmlPath}\" /F");
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    static bool RunSchtasks(string arguments, bool throwOnError = true)
    {
        using var process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        string error = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0 && throwOnError)
            throw new IOException($"schtasks {arguments.Split(' ')[0]}: {error.Trim()}");
        return process.ExitCode == 0;
    }
}
