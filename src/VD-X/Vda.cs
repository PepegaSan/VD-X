using System.Runtime.InteropServices;

namespace VDX;

/// <summary>
/// P/Invoke bindings for VirtualDesktopAccessor.dll (Ciantic, MIT), which must sit next to the exe.
/// https://github.com/Ciantic/VirtualDesktopAccessor — desktop numbers are 0-based, -1 means error.
/// </summary>
static class Vda
{
    const string DllName = "VirtualDesktopAccessor.dll";

    [DllImport(DllName, ExactSpelling = true)] public static extern int GetDesktopCount();
    [DllImport(DllName, ExactSpelling = true)] public static extern int GetCurrentDesktopNumber();
    [DllImport(DllName, ExactSpelling = true)] public static extern int GoToDesktopNumber(int desktopNumber);
    [DllImport(DllName, ExactSpelling = true)] public static extern int CreateDesktop();
    [DllImport(DllName, ExactSpelling = true)] public static extern int GetWindowDesktopNumber(IntPtr hwnd);
    [DllImport(DllName, ExactSpelling = true)] public static extern int MoveWindowToDesktopNumber(IntPtr hwnd, int desktopNumber);
    [DllImport(DllName, ExactSpelling = true)] public static extern int IsPinnedWindow(IntPtr hwnd);
    [DllImport(DllName, ExactSpelling = true)] public static extern int PinWindow(IntPtr hwnd);
    [DllImport(DllName, ExactSpelling = true)] public static extern int UnPinWindow(IntPtr hwnd);
    [DllImport(DllName, ExactSpelling = true)] public static extern int IsPinnedApp(IntPtr hwnd);
    [DllImport(DllName, ExactSpelling = true)] public static extern int PinApp(IntPtr hwnd);
    [DllImport(DllName, ExactSpelling = true)] public static extern int UnPinApp(IntPtr hwnd);

    /// <summary>Checks that the DLL exists, loads, and can talk to the Windows virtual desktop service.</summary>
    public static bool TryLoad(out string error)
    {
        string path = Path.Combine(AppContext.BaseDirectory, DllName);
        if (!File.Exists(path))
        {
            error = L.T(
                $"Missing file:\n{path}\n\nPlease reinstall the app.",
                $"Datei fehlt:\n{path}\n\nBitte die App neu installieren.");
            return false;
        }
        try
        {
            if (GetDesktopCount() > 0)
            {
                error = "";
                return true;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            error = $"{DllName}: {ex.Message}";
            return false;
        }
        error = L.T(
            "The virtual desktop API is not available. This needs Windows 11 (24H2 or newer recommended); after a big Windows update a newer VirtualDesktopAccessor.dll may be required.",
            "Die Schnittstelle für virtuelle Desktops ist nicht verfügbar. Benötigt wird Windows 11 (empfohlen 24H2 oder neuer); nach großen Windows-Updates braucht es eventuell eine neuere VirtualDesktopAccessor.dll.");
        return false;
    }
}
