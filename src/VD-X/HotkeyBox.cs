namespace VDX;

/// <summary>Read-only text box that records the next key combination pressed while it has focus.</summary>
sealed class HotkeyBox : TextBox
{
    Hotkey? _hotkey;

    public event EventHandler? HotkeyChanged;

    public HotkeyBox()
    {
        ReadOnly = true;
        BackColor = SystemColors.Window;
        TextAlign = HorizontalAlignment.Center;
        ShortcutsEnabled = false;
        Cursor = Cursors.Hand;
        UpdateText();
    }

    public Hotkey? Hotkey
    {
        get => _hotkey;
        set
        {
            if (_hotkey == value)
                return;
            _hotkey = value;
            UpdateText();
            HotkeyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    void UpdateText() => Text = _hotkey?.ToDisplayString() ?? L.T("None", "Keiner");

    // ProcessCmdKey sees every key including Alt combinations and F10, before WinForms acts on them.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        var modifiers = ToModifiers(keyData);

        if (key == Keys.Tab && (modifiers & ~HotkeyModifiers.Shift) == HotkeyModifiers.None)
            return base.ProcessCmdKey(ref msg, keyData);   // keep Tab / Shift+Tab for navigation
        if (key == Keys.Escape)
            return true;
        if (key is Keys.Back or Keys.Delete && modifiers == HotkeyModifiers.None)
        {
            Hotkey = null;
            return true;
        }
        if (key is Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            // Only modifiers so far: show them as a preview until the real key follows.
            if (modifiers != HotkeyModifiers.None)
                Text = VDX.Hotkey.ModifiersToDisplayString(modifiers) + "…";
            return true;
        }

        Hotkey = new Hotkey(key, modifiers);
        return true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (ToModifiers(e.KeyData) == HotkeyModifiers.None)
            UpdateText();
    }

    protected override void OnKeyPress(KeyPressEventArgs e) => e.Handled = true;

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        UpdateText();
    }

    static HotkeyModifiers ToModifiers(Keys keyData)
    {
        var modifiers = HotkeyModifiers.None;
        if (keyData.HasFlag(Keys.Control)) modifiers |= HotkeyModifiers.Control;
        if (keyData.HasFlag(Keys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (keyData.HasFlag(Keys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (NativeMethods.IsWinKeyDown()) modifiers |= HotkeyModifiers.Win;
        return modifiers;
    }
}
