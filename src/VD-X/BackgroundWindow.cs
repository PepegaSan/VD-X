namespace VDX;

/// <summary>
/// Hidden top-level window that receives shell hook notifications (new windows) and commands from
/// other copies of the exe (second start, uninstaller). Window messages are used instead of named
/// events because they still work when this instance runs as administrator and the sender does not.
/// </summary>
sealed class BackgroundWindow : NativeWindow, IDisposable
{
    const string Title = "VD-X.BackgroundWindow";

    static readonly uint ShowSettingsMessage = NativeMethods.RegisterWindowMessage("VD-X.ShowSettings");
    static readonly uint ExitMessage = NativeMethods.RegisterWindowMessage("VD-X.Exit");
    static readonly uint ShellHookMessage = NativeMethods.RegisterWindowMessage("SHELLHOOK");

    public event Action? ShowSettingsRequested;
    public event Action? ExitRequested;
    public event Action<IntPtr>? WindowCreated;

    public BackgroundWindow()
    {
        CreateHandle(new CreateParams { Caption = Title });
        foreach (uint message in new[] { ShowSettingsMessage, ExitMessage, ShellHookMessage })
            NativeMethods.AllowMessageFromLowerIntegrity(Handle, message);
        NativeMethods.RegisterShellHookWindow(Handle);
    }

    protected override void WndProc(ref Message m)
    {
        uint message = (uint)m.Msg;
        if (message == ShellHookMessage)
        {
            if ((int)m.WParam == NativeMethods.HSHELL_WINDOWCREATED)
                WindowCreated?.Invoke(m.LParam);
            return;
        }
        if (message == ShowSettingsMessage)
        {
            ShowSettingsRequested?.Invoke();
            return;
        }
        if (message == ExitMessage)
        {
            ExitRequested?.Invoke();
            return;
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        NativeMethods.DeregisterShellHookWindow(Handle);
        DestroyHandle();
    }

    /// <summary>Asks the running instance to open its settings. False if no instance is running.</summary>
    public static bool RequestShowSettings()
    {
        // Let the running instance bring its window to the front.
        NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
        return Post(ShowSettingsMessage);
    }

    /// <summary>Asks the running instance to quit and waits until it is gone (or the timeout passes).</summary>
    public static bool RequestExit(TimeSpan timeout)
    {
        if (!Post(ExitMessage))
            return true;
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (NativeMethods.FindWindow(null, Title) == IntPtr.Zero)
                return true;
            Thread.Sleep(100);
        }
        return false;
    }

    static bool Post(uint message)
    {
        IntPtr hwnd = NativeMethods.FindWindow(null, Title);
        return hwnd != IntPtr.Zero && NativeMethods.PostMessage(hwnd, message, IntPtr.Zero, IntPtr.Zero);
    }
}
