namespace VDX;

/// <summary>Registers global hotkeys on a hidden message-only window and reports when one is pressed.</summary>
sealed class HotkeyManager : NativeWindow, IDisposable
{
    static readonly IntPtr HWND_MESSAGE = new(-3);

    readonly Dictionary<int, HotkeyAction> _actions = [];

    public event Action<HotkeyAction>? HotkeyPressed;

    public HotkeyManager() => CreateHandle(new CreateParams { Parent = HWND_MESSAGE });

    public int Count => _actions.Count;

    /// <summary>Replaces all hotkeys. Returns the actions Windows refused, usually because another program owns that key.</summary>
    public List<HotkeyAction> Register(IEnumerable<(HotkeyAction Action, Hotkey Hotkey)> hotkeys)
    {
        UnregisterAll();
        var failed = new List<HotkeyAction>();
        int id = 1;
        foreach (var (action, hotkey) in hotkeys)
        {
            uint modifiers = (uint)hotkey.Modifiers | NativeMethods.MOD_NOREPEAT;
            if (NativeMethods.RegisterHotKey(Handle, id, modifiers, (uint)hotkey.Key))
                _actions[id] = action;
            else
                failed.Add(action);
            id++;
        }
        return failed;
    }

    public void UnregisterAll()
    {
        foreach (int id in _actions.Keys)
            NativeMethods.UnregisterHotKey(Handle, id);
        _actions.Clear();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && _actions.TryGetValue((int)m.WParam, out var action))
        {
            HotkeyPressed?.Invoke(action);
            return;
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterAll();
        DestroyHandle();
    }
}
