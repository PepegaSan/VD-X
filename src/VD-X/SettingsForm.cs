namespace VDX;

sealed class SettingsForm : Form
{
    const int TextWidth = 380;
    const int ContentWidth = 600;
    const int MaxDesktop = 20;

    readonly AppSettings _settings;
    readonly IReadOnlyCollection<HotkeyAction> _registrationFailures;
    readonly AutostartMode _initialAutostart = Autostart.CurrentMode;
    readonly ToolTip _toolTip = new();
    readonly Font _boldFont;
    readonly Font _headingFont;

    // Hotkeys tab
    readonly Dictionary<HotkeyAction, (HotkeyBox Box, Label Message)> _rows = [];
    // Programs tab
    DataGridView _programs = null!;
    Label _programsResult = null!;
    // New windows tab
    readonly Dictionary<RouteMode, RadioButton> _routeModes = [];
    NumericUpDown _targetDesktop = null!;
    TextBox _exclude = null!;
    // General tab
    CheckBox _autostart = null!;
    CheckBox _runAsAdmin = null!;

    readonly Button _save;

    /// <summary>Set when the user switched on "run as administrator" and wants to restart elevated right away.</summary>
    public bool RestartElevatedRequested { get; private set; }

    public SettingsForm(AppSettings settings, IReadOnlyCollection<HotkeyAction> registrationFailures)
    {
        _settings = settings;
        _registrationFailures = registrationFailures;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Text = L.T("VD-X – Settings", "VD-X – Einstellungen");
        Icon = AppIcon.Load();
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(680, 640);
        MinimumSize = new Size(560, 420);
        _boldFont = new Font(Font, FontStyle.Bold);
        _headingFont = new Font(Font.FontFamily, 11F, FontStyle.Bold);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 4) };
        tabs.TabPages.Add(Page("Hotkeys", BuildHotkeysTab()));
        tabs.TabPages.Add(Page(L.T("Start programs", "Programme starten"), BuildProgramsTab()));
        tabs.TabPages.Add(Page(L.T("New windows", "Neue Fenster"), BuildNewWindowsTab()));
        tabs.TabPages.Add(Page(L.T("General", "Allgemein"), BuildGeneralTab()));

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8, 10, 8, 10),
        };
        _save = DialogButton(L.T("Save", "Speichern"), Save);
        var cancel = DialogButton(L.T("Cancel", "Abbrechen"), Close);
        buttons.Controls.Add(_save);
        buttons.Controls.Add(cancel);

        // Fill first, then Bottom: WinForms docks the last added control first.
        Controls.Add(tabs);
        Controls.Add(buttons);
        AcceptButton = _save;
        CancelButton = cancel;
        ActiveControl = cancel;   // don't start in a hotkey field, the next key press would overwrite it
        ResumeLayout(false);
        PerformLayout();

        ValidateHotkeys();
    }

    // ---------------------------------------------------------------- Hotkeys

    Control BuildHotkeysTab()
    {
        var panel = Column();
        panel.Controls.Add(Hint(L.T(
            "Click a field and press the key or key combination you want. Backspace or Delete removes it. While this window is open, all hotkeys are paused.",
            "Klicke in ein Feld und drücke die gewünschte Taste oder Tastenkombination. Rücktaste oder Entf entfernt sie. Solange dieses Fenster offen ist, sind alle Hotkeys pausiert.")));

        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 4, 0, 4) };
        for (int i = 0; i < 3; i++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < HotkeyActions.All.Count; i++)
            AddHotkeyRow(grid, i, HotkeyActions.All[i]);
        panel.Controls.Add(grid);

        panel.Controls.Add(DialogButton(L.T("Reset hotkeys to defaults", "Hotkeys auf Standard zurücksetzen"), ResetHotkeys));
        return panel;
    }

    void AddHotkeyRow(TableLayoutPanel grid, int row, HotkeyActionInfo info)
    {
        var text = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 6, 16, 6),
        };
        text.Controls.Add(new Label { Text = info.Title, Font = _boldFont, AutoSize = true, Margin = Padding.Empty });
        text.Controls.Add(Hint(info.Description, TextWidth));
        var message = new Label { AutoSize = true, MaximumSize = new Size(TextWidth, 0), Margin = new Padding(0, 4, 0, 0), Visible = false };
        text.Controls.Add(message);

        var box = new HotkeyBox { Width = 150, Hotkey = _settings.GetHotkey(info.Action), Margin = new Padding(0, 8, 4, 0) };
        box.HotkeyChanged += (_, _) => ValidateHotkeys();

        var clear = new Button { Text = "×", Width = 30, Height = box.Height + 2, Margin = new Padding(0, 7, 0, 0) };
        clear.Click += (_, _) => box.Hotkey = null;
        _toolTip.SetToolTip(clear, L.T("Remove hotkey", "Hotkey entfernen"));

        grid.Controls.Add(text, 0, row);
        grid.Controls.Add(box, 1, row);
        grid.Controls.Add(clear, 2, row);
        _rows[info.Action] = (box, message);
    }

    /// <summary>Shows problems under each hotkey; invalid keys and duplicates block saving.</summary>
    void ValidateHotkeys()
    {
        var owners = new Dictionary<Hotkey, HotkeyAction>();
        bool canSave = true;
        foreach (var info in HotkeyActions.All)
        {
            var (box, message) = _rows[info.Action];
            string? error = null, warning = null;
            if (box.Hotkey is Hotkey hotkey)
            {
                if (!hotkey.IsValid)
                    error = L.T("Use a function key (F1–F24) or add Ctrl, Alt, Shift or Win.",
                                "Nimm eine Funktionstaste (F1–F24) oder kombiniere mit Strg, Alt, Shift oder Win.");
                else if (owners.TryGetValue(hotkey, out var other))
                    error = L.T($"Already used for “{HotkeyActions.Get(other).Title}”.",
                                $"Schon belegt für „{HotkeyActions.Get(other).Title}“.");
                else
                {
                    owners[hotkey] = info.Action;
                    if (_registrationFailures.Contains(info.Action) && hotkey == _settings.GetHotkey(info.Action))
                        warning = L.T("Windows refused this hotkey – another program probably uses it.",
                                      "Windows hat diesen Hotkey abgelehnt – vermutlich benutzt ihn ein anderes Programm.");
                }
            }
            message.Text = error ?? warning ?? "";
            message.ForeColor = error is not null ? Color.Firebrick : Color.DarkOrange;
            message.Visible = message.Text.Length > 0;
            canSave &= error is null;
        }
        _save.Enabled = canSave;
    }

    void ResetHotkeys()
    {
        foreach (var info in HotkeyActions.All)
            _rows[info.Action].Box.Hotkey = info.DefaultHotkey;
    }

    // ---------------------------------------------------------------- Start programs

    Control BuildProgramsTab()
    {
        var panel = Column();
        panel.Controls.Add(Hint(L.T(
            "These programs are started when the app starts with Windows, and their windows are moved to the chosen desktop. Missing desktops are created.",
            "Diese Programme werden gestartet, wenn die App mit Windows startet, und ihre Fenster landen auf dem gewählten Desktop. Fehlende Desktops werden angelegt.")));

        _programs = new DataGridView
        {
            Width = ContentWidth,
            Height = 240,
            Margin = new Padding(0, 10, 0, 0),
            AllowUserToAddRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            EditMode = DataGridViewEditMode.EditOnEnter,
        };
        var desktopColumn = new DataGridViewComboBoxColumn
        {
            HeaderText = "Desktop",
            ValueType = typeof(int),
            FillWeight = 14,
            FlatStyle = FlatStyle.Flat,
        };
        desktopColumn.Items.AddRange(Enumerable.Range(1, MaxDesktop).Cast<object>().ToArray());
        _programs.Columns.Add(desktopColumn);
        _programs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = L.T("Program", "Programm"), FillWeight = 56 });
        _programs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = L.T("Arguments", "Argumente"), FillWeight = 24 });
        _programs.Columns.Add(new DataGridViewButtonColumn
        {
            Text = "…",
            UseColumnTextForButtonValue = true,
            FillWeight = 6,
            ToolTipText = L.T("Choose program", "Programm auswählen"),
        });
        _programs.CellContentClick += (_, e) =>
        {
            if (e.ColumnIndex == 3 && e.RowIndex >= 0 && BrowseForProgram() is string path)
                _programs.Rows[e.RowIndex].Cells[1].Value = path;
        };
        _programs.DataError += (_, e) => e.ThrowException = false;
        foreach (var program in _settings.StartupPrograms)
            _programs.Rows.Add(Math.Clamp(program.Desktop, 1, MaxDesktop), program.Path, program.Arguments);
        panel.Controls.Add(_programs);

        var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
        buttons.Controls.Add(DialogButton(L.T("Add program…", "Programm hinzufügen…"), () =>
        {
            if (BrowseForProgram() is string path)
                _programs.Rows.Add(2, path, "");
        }));
        buttons.Controls.Add(DialogButton(L.T("Remove", "Entfernen"), () =>
        {
            foreach (DataGridViewRow row in _programs.SelectedRows)
                _programs.Rows.Remove(row);
        }));
        Button test = null!;
        test = DialogButton(L.T("Start now (test)", "Jetzt starten (Test)"), () => _ = TestPrograms(test));
        buttons.Controls.Add(test);
        panel.Controls.Add(buttons);

        _programsResult = Hint("");
        panel.Controls.Add(_programsResult);
        return panel;
    }

    async Task TestPrograms(Button test)
    {
        test.Enabled = false;
        _programsResult.Text = L.T("Starting…", "Starte…");
        var errors = await ProgramLauncher.RunAllAsync(ReadPrograms());
        if (IsDisposed)
            return;
        test.Enabled = true;
        _programsResult.ForeColor = errors.Count > 0 ? Color.Firebrick : SystemColors.GrayText;
        _programsResult.Text = errors.Count > 0 ? string.Join("\n", errors) : L.T("Done.", "Fertig.");
    }

    List<StartupProgram> ReadPrograms()
    {
        _programs.EndEdit();
        return _programs.Rows.Cast<DataGridViewRow>()
            .Select(row => new StartupProgram
            {
                Desktop = row.Cells[0].Value is int desktop ? desktop : 1,
                Path = (row.Cells[1].Value as string ?? "").Trim(),
                Arguments = (row.Cells[2].Value as string ?? "").Trim(),
            })
            .Where(program => program.Path.Length > 0)
            .ToList();
    }

    string? BrowseForProgram()
    {
        using var dialog = new OpenFileDialog
        {
            Title = L.T("Choose program", "Programm auswählen"),
            Filter = L.T("Programs", "Programme") + " (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|" + L.T("All files", "Alle Dateien") + " (*.*)|*.*",
        };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
    }

    // ---------------------------------------------------------------- New windows

    Control BuildNewWindowsTab()
    {
        var panel = Column();
        panel.Controls.Add(Hint(L.T(
            "Automatically move every newly opened window to one desktop – e.g. to keep a “work” desktop free of everything else.",
            "Verschiebt jedes neu geöffnete Fenster automatisch auf einen bestimmten Desktop – z. B. damit ein „Arbeits“-Desktop frei von allem anderen bleibt.")));

        var options = _settings.NewWindows;
        AddRouteMode(panel, RouteMode.Off, L.T("Off", "Aus"), null);
        AddRouteMode(panel, RouteMode.Silent,
            L.T("Move new windows, I stay where I am", "Neue Fenster verschieben, ich bleibe wo ich bin"),
            L.T("The window goes to the target desktop in the background.", "Das Fenster landet im Hintergrund auf dem Ziel-Desktop."));
        AddRouteMode(panel, RouteMode.Active,
            L.T("Move new windows and switch with them", "Neue Fenster verschieben und mitwechseln"),
            L.T("You are taken to the target desktop together with the new window.", "Du wechselst zusammen mit dem neuen Fenster auf den Ziel-Desktop."));
        _routeModes[options.Mode].Checked = true;

        var target = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
        target.Controls.Add(new Label { Text = L.T("Target desktop:", "Ziel-Desktop:"), AutoSize = true, Margin = new Padding(0, 5, 6, 0) });
        _targetDesktop = new NumericUpDown { Minimum = 1, Maximum = MaxDesktop, Value = Math.Clamp(options.TargetDesktop, 1, MaxDesktop), Width = 60 };
        target.Controls.Add(_targetDesktop);
        panel.Controls.Add(target);

        panel.Controls.Add(new Label { Text = L.T("Exceptions", "Ausnahmen"), Font = _boldFont, AutoSize = true, Margin = new Padding(0, 14, 0, 0) });
        panel.Controls.Add(Hint(L.T(
            "One program per line (e.g. msedge.exe). Their windows stay wherever Windows opens them.",
            "Ein Programm pro Zeile (z. B. msedge.exe). Deren Fenster bleiben, wo Windows sie öffnet.")));
        _exclude = new TextBox
        {
            Multiline = true,
            AcceptsReturn = true,
            ScrollBars = ScrollBars.Vertical,
            Width = 300,
            Height = 110,
            Margin = new Padding(0, 6, 0, 0),
            Text = string.Join(Environment.NewLine, options.Exclude),
        };
        panel.Controls.Add(_exclude);
        panel.Controls.Add(DialogButton(L.T("Add program…", "Programm hinzufügen…"), () =>
        {
            if (BrowseForProgram() is string path)
                _exclude.AppendText((_exclude.TextLength > 0 && !_exclude.Text.EndsWith('\n') ? Environment.NewLine : "") + Path.GetFileName(path));
        }));

        panel.Controls.Add(Hint(L.T(
            "Programs from “Start programs” are not affected while they start. Windows of programs running as administrator are only moved if this app runs as administrator too.",
            "Programme aus „Programme starten“ sind beim Start davon ausgenommen. Fenster von Programmen mit Administratorrechten werden nur verschoben, wenn diese App ebenfalls als Administrator läuft.")));
        return panel;
    }

    void AddRouteMode(Control panel, RouteMode mode, string title, string? description)
    {
        var radio = new RadioButton { Text = title, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
        _routeModes[mode] = radio;
        panel.Controls.Add(radio);
        if (description is not null)
        {
            var hint = Hint(description);
            hint.Margin = new Padding(18, 0, 0, 0);
            panel.Controls.Add(hint);
        }
    }

    // ---------------------------------------------------------------- General

    Control BuildGeneralTab()
    {
        var panel = Column();
        _autostart = new CheckBox
        {
            Text = L.T("Start automatically when I sign in to Windows", "Beim Anmelden an Windows automatisch starten"),
            AutoSize = true,
            Checked = _initialAutostart != AutostartMode.Off,
        };
        panel.Controls.Add(_autostart);
        panel.Controls.Add(Hint(L.T(
            "The app then runs quietly in the system tray (click the tray icon to open these settings).",
            "Die App läuft dann unauffällig im Infobereich der Taskleiste (Klick auf das Symbol öffnet diese Einstellungen).")));

        _runAsAdmin = new CheckBox
        {
            Text = L.T("Run with administrator rights", "Mit Administratorrechten ausführen"),
            AutoSize = true,
            Checked = _settings.RunAsAdmin,
            Margin = new Padding(0, 16, 0, 0),
        };
        panel.Controls.Add(_runAsAdmin);
        panel.Controls.Add(Hint(L.T(
            "Needed to move or pin windows of programs that themselves run as administrator (e.g. Task Manager). Windows asks for permission once (UAC); autostart then uses Task Scheduler and starts without asking.",
            "Nötig, um Fenster von Programmen zu verschieben oder anzuheften, die selbst als Administrator laufen (z. B. Task-Manager). Windows fragt einmal nach (UAC); der Autostart läuft dann über die Aufgabenplanung und startet ohne Nachfrage.")));

        panel.Controls.Add(new Label { Text = "Info", Font = _headingFont, AutoSize = true, Margin = new Padding(0, 24, 0, 0) });
        int desktops = Vda.GetDesktopCount();
        panel.Controls.Add(Hint(
            (Elevation.IsElevated
                ? L.T("Currently running with administrator rights.", "Läuft gerade mit Administratorrechten.")
                : L.T("Currently running without administrator rights.", "Läuft gerade ohne Administratorrechte.")) + "\n" +
            L.T($"Virtual desktops: {desktops}", $"Virtuelle Desktops: {desktops}") + "\n" +
            L.T("Settings file: ", "Einstellungsdatei: ") + AppSettings.FilePath));
        return panel;
    }

    // ---------------------------------------------------------------- Save

    void Save()
    {
        try
        {
            foreach (var (action, row) in _rows)
                _settings.SetHotkey(action, row.Box.Hotkey);
            _settings.StartupPrograms = ReadPrograms();
            _settings.NewWindows = new NewWindowSettings
            {
                Mode = _routeModes.First(pair => pair.Value.Checked).Key,
                TargetDesktop = (int)_targetDesktop.Value,
                Exclude = _exclude.Lines.Select(line => line.Trim()).Where(line => line.Length > 0).ToList(),
            };
            _settings.RunAsAdmin = _runAsAdmin.Checked;
            _settings.Save();

            var autostart = !_autostart.Checked ? AutostartMode.Off
                : _runAsAdmin.Checked ? AutostartMode.Admin
                : AutostartMode.Normal;
            if (autostart != _initialAutostart && !Autostart.Change(autostart))
                MessageBox.Show(this,
                    L.T("Autostart could not be changed (administrator rights declined?). All other settings were saved.",
                        "Der Autostart konnte nicht geändert werden (Administratorrechte abgelehnt?). Alle anderen Einstellungen wurden gespeichert."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (_runAsAdmin.Checked && !Elevation.IsElevated)
            RestartElevatedRequested = MessageBox.Show(this,
                L.T("Restart the app with administrator rights now?", "App jetzt mit Administratorrechten neu starten?"),
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        Close();
    }

    // ---------------------------------------------------------------- Layout helpers

    static TabPage Page(string title, Control content)
    {
        var page = new TabPage(title) { AutoScroll = true, Padding = new Padding(12), UseVisualStyleBackColor = true };
        content.Dock = DockStyle.Top;
        page.Controls.Add(content);
        return page;
    }

    static FlowLayoutPanel Column() => new()
    {
        AutoSize = true,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
    };

    static Label Hint(string text, int width = ContentWidth) => new()
    {
        Text = text,
        ForeColor = SystemColors.GrayText,
        AutoSize = true,
        MaximumSize = new Size(width, 0),
        Margin = new Padding(0, 4, 0, 0),
    };

    static Button DialogButton(string text, Action onClick)
    {
        var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(90, 28), Margin = new Padding(0, 6, 6, 0) };
        button.Click += (_, _) => onClick();
        return button;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
            _boldFont.Dispose();
            _headingFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
