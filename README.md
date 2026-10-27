# VD-X

Hotkeys and automation for **Windows 11 virtual desktops** — switch and create desktops, move the active window or show it on all desktops, start programs on a fixed desktop, and route new windows automatically. A normal Windows app with installer, tray icon and settings window, built on **[VirtualDesktopAccessor](https://github.com/Ciantic/VirtualDesktopAccessor)**.

## Install

1. Download `VD-X-Setup-<version>.exe` from the [Releases](https://github.com/PepegaSan/VD-X/releases) page (or [build it yourself](#build-from-source)).
2. Run it. No admin rights needed — it installs to `%LocalAppData%\Programs\VD-X`.
3. Needs the **[.NET Desktop Runtime](https://dotnet.microsoft.com/download/dotnet) 8 or newer**; the installer tells you if it is missing.

Requires **Windows 11** — VirtualDesktopAccessor recommends **24H2 (build 26100.2605) or newer**.

Windows SmartScreen may warn about an unknown publisher because the installer is not code-signed (**More info → Run anyway**).

## Use

- VD-X lives in the **system tray** (on Windows 11 possibly behind the `^` arrow — drag it onto the taskbar to keep it visible).
- **Left-click** the tray icon → settings. **Right-click** → menu (Settings, Start programs now, Start with Windows, Exit).
- Settings are stored in `%AppData%\VD-X\settings.json`.
- The UI is German on German Windows and English otherwise.

The settings window has four tabs:

| Tab | What it does |
|-----|--------------|
| **Hotkeys** | Click a field and press the key or combination you want; **Backspace/Delete** removes it. Each action has a short description. Duplicate keys are flagged. Hotkeys keep working while the window is open; they are paused only while a hotkey field has the focus. |
| **Start programs** | Programs that are started when VD-X starts with Windows; their windows are moved to the chosen desktop (missing desktops are created). VD-X keeps watching for a few minutes after the start, so a main window that opens long after a splash screen (e.g. DaVinci Resolve) is moved too. If the program is only a launcher, enter the game exe (or its install folder) under **Window of**. **Add Battle.net game…** lists the installed Blizzard games (World of Warcraft incl. Classic, Overwatch, Heroes of the Storm, Hearthstone, Diablo …) and fills in the client, its `--exec="launch …"` command and the game folder for you, because a game exe started directly (even with `-launch`) gets no server connection and the `… Launcher.exe` in the game folder only opens the game page. The WoW Classic versions share one entry: the client starts whichever of them is selected there. A game that needs an update is not started by Battle.net until it is updated. **Start now (test)** tries the list right away. |
| **New windows** | Rules per program: windows of an exe or of an install folder go to a fixed desktop as soon as they open, no matter who started the program — e.g. a game you start from the Battle.net client (**Add Battle.net game…** fills in the game folder). Each rule can also switch you to that desktop; otherwise the window is moved in the background. Below that, optionally move *every other* new window to one desktop — *move only, I stay* or *move and switch with it* — with excluded programs (e.g. `msedge.exe`). |
| **General** | **Start automatically when I sign in** (if it was switched off in Task Manager → *Startup apps*, ticking the box switches it back on) and **Run with administrator rights** (see below). |

### Default hotkeys

| Key | Action |
|-----|--------|
| **F14** | Next desktop (wraps around) |
| **F15** | Show active window on all desktops (toggle) |
| **F16** | Move active window to the next desktop (you stay) |
| **F17** | Create a new desktop and switch to it |

Also available (no key by default): previous desktop, move window to previous desktop, show app on all desktops. F13–F24 are handy for extra mouse buttons or macro keys.

### Administrator mode

Windows only lets a program move or pin windows of programs that run **as administrator** (e.g. Task Manager) if it runs as administrator itself. With **Run with administrator rights** on:

- Windows asks once (UAC) when you save; VD-X offers to restart elevated right away.
- Autostart then uses a **Task Scheduler** task (*VD-X*, “run with highest privileges”) instead of the normal startup entry, so there is no UAC prompt at every sign-in. The task also runs on battery and never times out.
- Starting VD-X manually (Start menu) asks for administrator rights each time.
- Uninstalling removes the task (Windows asks for administrator rights once more).

## Build from source

Requirements: [.NET SDK 8+](https://dotnet.microsoft.com/download/dotnet), optionally [Inno Setup 6](https://jrsoftware.org/isinfo.php) for the installer.

```powershell
.\build.ps1 -Version 1.0.0
```

- App (`VD-X.exe` + `VirtualDesktopAccessor.dll`): `publish\`
- Installer: `installer\Output\VD-X-Setup-1.0.0.exe`

Source code: [`src/VD-X/`](src/VD-X/) (C#, WinForms). Open `VD-X.sln` in Visual Studio or use `dotnet build`.

After a big Windows update, a newer `VirtualDesktopAccessor.dll` may be needed: run `download-vda.ps1` and rebuild.

### Releases

[`.github/workflows/release.yml`](.github/workflows/release.yml) builds VD-X on every push and pull request. Pushing a version tag also builds the installer and attaches it to a GitHub release:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

## License

- **VD-X** (source, scripts, docs): **MIT** — see [`LICENSE`](LICENSE).
- **`VirtualDesktopAccessor.dll`**: third-party **MIT**-licensed binary by **Ciantic**, bundled with its license text in [`third-party/VirtualDesktopAccessor/LICENSE.txt`](third-party/VirtualDesktopAccessor/LICENSE.txt). The installer ships both. Details: [`THIRD_PARTY.md`](THIRD_PARTY.md).

## Disclaimer

Virtual desktop behavior depends on Windows and the upstream DLL, which uses undocumented Windows APIs. This project is provided as-is; see [LICENSE](LICENSE).
