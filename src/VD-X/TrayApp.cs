namespace VDX;

/// <summary>The running app: tray icon, global hotkeys, new-window routing and the settings window on demand.</summary>
sealed class TrayApp : ApplicationContext
{
    readonly AppSettings _settings;
    readonly HotkeyManager _hotkeys = new();
    readonly BackgroundWindow _background = new();
    readonly NewWindowRouter _router;
    readonly NotifyIcon _tray;
    readonly ToolStripMenuItem _settingsItem;
    readonly ToolStripMenuItem _runProgramsItem;
    readonly ToolStripMenuItem _autostartItem;
    readonly ToolStripMenuItem _exitItem;
    SettingsForm? _settingsForm;
    List<HotkeyAction> _failedHotkeys = [];
    bool _exiting;

    public TrayApp(AppSettings settings, bool firstRun, bool startedByAutostart)
    {
        _settings = settings;
        if (firstRun)
            TryRun(_settings.Save);
        TryRun(Autostart.Repair);

        _settingsItem = new ToolStripMenuItem("", null, (_, _) => ShowSettings());
        _settingsItem.Font = new Font(_settingsItem.Font, FontStyle.Bold);
        _runProgramsItem = new ToolStripMenuItem("", null, (_, _) => RunStartupPrograms());
        _autostartItem = new ToolStripMenuItem("", null, (_, _) => ToggleAutostart());
        _exitItem = new ToolStripMenuItem("", null, (_, _) => ExitThread());
        UpdateMenuTexts();
        var menu = new ContextMenuStrip();
        menu.Items.AddRange([
            _settingsItem,
            _runProgramsItem,
            _autostartItem,
            new ToolStripSeparator(),
            _exitItem,
        ]);
        menu.Opening += (_, _) =>
        {
            _autostartItem.Checked = Autostart.CurrentMode != AutostartMode.Off;
            _runProgramsItem.Enabled = _settings.StartupPrograms.Any(p => p.Enabled);
        };

        _tray = new NotifyIcon
        {
            Icon = AppIcon.Load(SystemInformation.SmallIconSize),
            Text = Program.AppName,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                ShowSettings();
        };

        _hotkeys.HotkeyPressed += OnHotkeyPressed;
        ApplyHotkeys();

        _router = new NewWindowRouter(_settings);
        _router.Error += message => Notify(ToolTipIcon.Warning, message);
        _background.WindowCreated += _router.OnWindowCreated;
        _background.ShowSettingsRequested += ShowSettings;
        _background.ExitRequested += ExitThread;

        if (firstRun)
            ShowSettings();
        else if (!startedByAutostart)
            Notify(ToolTipIcon.Info, L.T(
                $"Running in the system tray with {_hotkeys.Count} hotkey(s). Click the icon for settings.",
                $"Läuft im Infobereich mit {_hotkeys.Count} Hotkey(s). Klick auf das Symbol öffnet die Einstellungen."));

        if (startedByAutostart)
            RunStartupPrograms();
    }

    /// <summary>Set here rather than once, so a language change in the settings shows right away.</summary>
    void UpdateMenuTexts()
    {
        _settingsItem.Text = L.T("Settings…", "Einstellungen…");
        _runProgramsItem.Text = L.T("Start programs now", "Programme jetzt starten");
        _autostartItem.Text = L.T("Start with Windows", "Mit Windows starten");
        _exitItem.Text = L.T("Exit", "Beenden");
    }

    /// <summary>Registers the saved hotkeys and reports the ones Windows refused.</summary>
    void ApplyHotkeys()
    {
        RegisterHotkeys();
        if (_failedHotkeys.Count > 0)
            Notify(ToolTipIcon.Warning,
                L.T("Already used by another program:\n", "Schon von einem anderen Programm belegt:\n") +
                string.Join("\n", _failedHotkeys.Select(a => $"{_settings.GetHotkey(a)?.ToDisplayString()} – {HotkeyActions.Get(a).Title}")));
    }

    void RegisterHotkeys()
    {
        var wanted = HotkeyActions.All
            .Select(info => (info.Action, Hotkey: _settings.GetHotkey(info.Action)))
            .Where(x => x.Hotkey is { IsValid: true })
            .Select(x => (x.Action, x.Hotkey!.Value));
        _failedHotkeys = _hotkeys.Register(wanted);
        _tray.Text = $"{Program.AppName}\n" + L.T($"{_hotkeys.Count} hotkey(s) active", $"{_hotkeys.Count} Hotkey(s) aktiv");
    }

    /// <summary>Globally registered keys never reach a window, so they are released while the user records a new hotkey.</summary>
    void PauseHotkeys()
    {
        _hotkeys.UnregisterAll();
        _tray.Text = $"{Program.AppName}\n" + L.T("Hotkeys paused", "Hotkeys pausiert");
    }

    void OnHotkeyPressed(HotkeyAction action)
    {
        try
        {
            DesktopActions.Run(action);
        }
        catch (DesktopActionException ex)
        {
            Notify(ToolTipIcon.Warning, ex.Message);
        }
        catch (EntryPointNotFoundException)
        {
            Notify(ToolTipIcon.Warning, L.T(
                "This function is not available in the installed VirtualDesktopAccessor.dll.",
                "Diese Funktion fehlt in der installierten VirtualDesktopAccessor.dll."));
        }
    }

    async void RunStartupPrograms()
    {
        var errors = await ProgramLauncher.RunAllAsync(_settings.StartupPrograms);
        if (errors.Count > 0)
            Notify(ToolTipIcon.Warning, string.Join("\n", errors));
    }

    void ShowSettings()
    {
        if (_exiting)
            return;
        if (_settingsForm is not null)
        {
            if (_settingsForm.WindowState == FormWindowState.Minimized)
                _settingsForm.WindowState = FormWindowState.Normal;
            _settingsForm.Activate();
            return;
        }

        var form = new SettingsForm(_settings, _failedHotkeys);
        // Hotkeys keep working while the window is open; only a focused hotkey field needs the keys for itself.
        form.HotkeyRecordingChanged += recording =>
        {
            if (_exiting)
                return;
            if (recording)
                PauseHotkeys();
            else
                RegisterHotkeys();
        };
        form.FormClosed += (_, _) =>
        {
            _settingsForm = null;
            bool restart = form.RestartElevatedRequested;
            form.Dispose();
            if (_exiting)
                return;
            if (restart && Elevation.TryRestartElevated())
            {
                ExitThread();
                return;
            }
            ApplyHotkeys();
            UpdateMenuTexts();   // the language may have changed
        };
        _settingsForm = form;
        form.Show();
        form.Activate();
    }

    void ToggleAutostart()
    {
        var target = Autostart.CurrentMode != AutostartMode.Off
            ? AutostartMode.Off
            : _settings.RunAsAdmin ? AutostartMode.Admin : AutostartMode.Normal;
        TryRun(() =>
        {
            if (!Autostart.Change(target))
                Notify(ToolTipIcon.Warning, L.T("Autostart was not changed (administrator rights declined?).",
                                                "Autostart wurde nicht geändert (Administratorrechte abgelehnt?)."));
        });
    }

    void TryRun(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Notify(ToolTipIcon.Error, ex.Message);
        }
    }

    void Notify(ToolTipIcon icon, string text)
    {
        // The tray may not exist yet while the constructor runs; such messages are not worth a popup.
        if (_tray is null)
            return;
        _tray.ShowBalloonTip(5000, Program.AppName, text.Length > 250 ? text[..250] + "…" : text, icon);
    }

    protected override void ExitThreadCore()
    {
        if (_exiting)
            return;
        _exiting = true;
        _settingsForm?.Close();
        _background.Dispose();
        _hotkeys.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        base.ExitThreadCore();
    }
}
