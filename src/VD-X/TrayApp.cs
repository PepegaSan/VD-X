namespace VDX;

/// <summary>The running app: tray icon, global hotkeys, new-window routing and the settings window on demand.</summary>
sealed class TrayApp : ApplicationContext
{
    readonly AppSettings _settings;
    readonly HotkeyManager _hotkeys = new();
    readonly BackgroundWindow _background = new();
    readonly NewWindowRouter _router;
    readonly NotifyIcon _tray;
    readonly ToolStripMenuItem _autostartItem;
    readonly ToolStripMenuItem _runProgramsItem;
    SettingsForm? _settingsForm;
    List<HotkeyAction> _failedHotkeys = [];
    bool _exiting;

    public TrayApp(AppSettings settings, bool firstRun, bool startedByAutostart)
    {
        _settings = settings;
        if (firstRun)
            TryRun(_settings.Save);
        TryRun(Autostart.Repair);

        _autostartItem = new ToolStripMenuItem(L.T("Start with Windows", "Mit Windows starten"), null, (_, _) => ToggleAutostart());
        _runProgramsItem = new ToolStripMenuItem(L.T("Start programs now", "Programme jetzt starten"), null, (_, _) => RunStartupPrograms());
        var settingsItem = new ToolStripMenuItem(L.T("Settings…", "Einstellungen…"), null, (_, _) => ShowSettings());
        settingsItem.Font = new Font(settingsItem.Font, FontStyle.Bold);
        var menu = new ContextMenuStrip();
        menu.Items.AddRange([
            settingsItem,
            _runProgramsItem,
            _autostartItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem(L.T("Exit", "Beenden"), null, (_, _) => ExitThread()),
        ]);
        menu.Opening += (_, _) =>
        {
            _autostartItem.Checked = Autostart.CurrentMode != AutostartMode.Off;
            _runProgramsItem.Enabled = _settings.StartupPrograms.Count > 0;
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
