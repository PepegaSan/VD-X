namespace VDX;

enum HotkeyAction
{
    NextDesktop,
    PreviousDesktop,
    CreateDesktop,
    MoveWindowNext,
    MoveWindowPrevious,
    TogglePinWindow,
    TogglePinApp,
}

sealed record HotkeyActionInfo(HotkeyAction Action, string Title, string Description, Hotkey? DefaultHotkey);

/// <summary>Everything a hotkey can do, in the order shown in the settings window. Defaults: F14–F17.</summary>
static class HotkeyActions
{
    public static IReadOnlyList<HotkeyActionInfo> All { get; } =
    [
        new(HotkeyAction.NextDesktop,
            L.T("Next desktop", "Nächster Desktop"),
            L.T("Switches to the next virtual desktop. After the last one it starts again at the first.",
                "Wechselt zum nächsten virtuellen Desktop. Nach dem letzten geht es wieder beim ersten weiter."),
            new Hotkey(Keys.F14, HotkeyModifiers.None)),
        new(HotkeyAction.PreviousDesktop,
            L.T("Previous desktop", "Vorheriger Desktop"),
            L.T("Switches to the previous virtual desktop. From the first one it jumps to the last.",
                "Wechselt zum vorherigen virtuellen Desktop. Vom ersten springt es zum letzten."),
            null),
        new(HotkeyAction.CreateDesktop,
            L.T("New desktop", "Neuer Desktop"),
            L.T("Creates a new virtual desktop and switches to it right away.",
                "Legt einen neuen virtuellen Desktop an und wechselt sofort dorthin."),
            new Hotkey(Keys.F17, HotkeyModifiers.None)),
        new(HotkeyAction.MoveWindowNext,
            L.T("Move window to next desktop", "Fenster auf nächsten Desktop"),
            L.T("Moves the active window to the next desktop (after the last one: to the first). You stay on the current desktop.",
                "Verschiebt das aktive Fenster auf den nächsten Desktop (nach dem letzten: auf den ersten). Du selbst bleibst auf dem aktuellen Desktop."),
            new Hotkey(Keys.F16, HotkeyModifiers.None)),
        new(HotkeyAction.MoveWindowPrevious,
            L.T("Move window to previous desktop", "Fenster auf vorherigen Desktop"),
            L.T("Moves the active window to the previous desktop (from the first one: to the last). You stay on the current desktop.",
                "Verschiebt das aktive Fenster auf den vorherigen Desktop (vom ersten: auf den letzten). Du selbst bleibst auf dem aktuellen Desktop."),
            null),
        new(HotkeyAction.TogglePinWindow,
            L.T("Show window on all desktops", "Fenster auf allen Desktops"),
            L.T("Pins the active window so it is visible on every desktop; press again to undo. Same as “Show this window on all desktops” in Task View.",
                "Heftet das aktive Fenster an, sodass es auf jedem Desktop sichtbar ist; nochmal drücken hebt es wieder auf. Entspricht „Dieses Fenster auf allen Desktops anzeigen“ in der Aktivitätsansicht."),
            new Hotkey(Keys.F15, HotkeyModifiers.None)),
        new(HotkeyAction.TogglePinApp,
            L.T("Show app on all desktops", "App auf allen Desktops"),
            L.T("Like the option above, but for all windows of the active app (e.g. every Explorer window).",
                "Wie die Option darüber, aber für alle Fenster der aktiven App (z. B. alle Explorer-Fenster)."),
            null),
    ];

    public static HotkeyActionInfo Get(HotkeyAction action) => All.First(info => info.Action == action);
}
