using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace VDX;

/// <summary>Running this exe with administrator rights (UAC prompt).</summary>
static class Elevation
{
    public const string RestartedArgument = "--restarted";

    public static bool IsElevated { get; } =
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>Starts an elevated copy that takes over once this instance exits. False if the user declined UAC.</summary>
    public static bool TryRestartElevated()
    {
        using var process = StartElevated(RestartedArgument);
        return process is not null;
    }

    /// <summary>Runs this exe elevated with the given arguments and waits. True if it ran and exited with code 0.</summary>
    public static bool RunElevated(string arguments)
    {
        using var process = StartElevated(arguments);
        if (process is null)
            return false;
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    static Process? StartElevated(string arguments)
    {
        try
        {
            return Process.Start(new ProcessStartInfo(Environment.ProcessPath!, arguments) { UseShellExecute = true, Verb = "runas" });
        }
        catch (Win32Exception)
        {
            return null;   // UAC prompt cancelled
        }
    }
}
