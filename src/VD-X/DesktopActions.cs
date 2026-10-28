namespace VDX;

sealed class DesktopActionException(string message) : Exception(message);

/// <summary>Implements each <see cref="HotkeyAction"/> on top of VirtualDesktopAccessor.dll.</summary>
static class DesktopActions
{
    public static void Run(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.NextDesktop:
                GoTo(Wrap(Vda.GetCurrentDesktopNumber() + 1));
                break;
            case HotkeyAction.PreviousDesktop:
                GoTo(Wrap(Vda.GetCurrentDesktopNumber() - 1));
                break;
            case HotkeyAction.CreateDesktop:
                CreateDesktopAndGo();
                break;
            case HotkeyAction.MoveWindowNext:
                MoveActiveWindow(+1);
                break;
            case HotkeyAction.MoveWindowPrevious:
                MoveActiveWindow(-1);
                break;
            case HotkeyAction.TogglePinWindow:
                TogglePin(Vda.IsPinnedWindow, Vda.PinWindow, Vda.UnPinWindow);
                break;
            case HotkeyAction.TogglePinApp:
                TogglePin(Vda.IsPinnedApp, Vda.PinApp, Vda.UnPinApp);
                break;
        }
    }

    static int Wrap(int desktopNumber)
    {
        int count = Vda.GetDesktopCount();
        return count < 1 ? 0 : ((desktopNumber % count) + count) % count;
    }

    static void GoTo(int desktopNumber)
    {
        Check(Vda.GoToDesktopNumber(desktopNumber),
            L.T($"Could not switch to desktop {desktopNumber + 1}.", $"Konnte nicht zu Desktop {desktopNumber + 1} wechseln."));
        StayGuard.NoteUserSwitch(desktopNumber);
    }

    static void CreateDesktopAndGo()
    {
        int desktopNumber = Vda.CreateDesktop();
        Check(desktopNumber < 0 ? -1 : 0,
            L.T("Could not create a new desktop.", "Konnte keinen neuen Desktop anlegen."));
        GoTo(desktopNumber);
    }

    static void MoveActiveWindow(int step)
    {
        IntPtr hwnd = ActiveWindow();
        int current = Vda.GetWindowDesktopNumber(hwnd);
        if (current < 0)
            current = Vda.GetCurrentDesktopNumber();
        Check(Vda.MoveWindowToDesktopNumber(hwnd, Wrap(current + step)),
            L.T("Could not move the window. Programs running as administrator can only be moved if this app also runs as administrator.",
                "Konnte das Fenster nicht verschieben. Programme, die als Administrator laufen, lassen sich nur verschieben, wenn diese App ebenfalls als Administrator läuft."));
    }

    static void TogglePin(Func<IntPtr, int> isPinned, Func<IntPtr, int> pin, Func<IntPtr, int> unpin)
    {
        IntPtr hwnd = ActiveWindow();
        int result = isPinned(hwnd) == 1 ? unpin(hwnd) : pin(hwnd);
        Check(result,
            L.T("Could not pin or unpin this window.", "Konnte dieses Fenster nicht anheften oder lösen."));
    }

    static IntPtr ActiveWindow()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || IsShellWindow(hwnd))
            throw new DesktopActionException(L.T("No active window — click the window first.", "Kein aktives Fenster – klicke zuerst das Fenster an."));
        return hwnd;
    }

    /// <summary>The desktop background and the taskbar count as "foreground" but cannot be moved or pinned.</summary>
    public static bool IsShellWindow(IntPtr hwnd) =>
        NativeMethods.GetClassName(hwnd) is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";

    /// <summary>Creates desktops until <paramref name="desktopNumber"/> (0-based) exists.</summary>
    public static void EnsureDesktopExists(int desktopNumber)
    {
        while (Vda.GetDesktopCount() <= desktopNumber)
        {
            if (Vda.CreateDesktop() < 0)
                throw new DesktopActionException(L.T("Could not create a new desktop.", "Konnte keinen neuen Desktop anlegen."));
        }
    }

    static void Check(int result, string message)
    {
        if (result == -1)
            throw new DesktopActionException(message);
    }
}
