; Inno Setup 6 script — build via ..\build.ps1 (publishes the app to ..\publish first).

#define AppName "VD-X"
#define AppExe "VD-X.exe"
#define PublishDir "..\publish"
#ifndef AppVersion
  #define AppVersion "1.0.3"
#endif

[Setup]
AppId={{88953801-ECF0-4095-BF8C-257795462F08}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=PepegaSan
AppPublisherURL=https://github.com/PepegaSan/VD-X
DefaultDirName={localappdata}\Programs\VD-X
DisableProgramGroupPage=yes
DisableDirPage=auto
; Per-user install: no admin prompt, autostart via HKCU.
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
; Stop a running copy when updating (the exe is in use).
CloseApplications=force
RestartApplications=no
OutputDir=Output
OutputBaseFilename=VD-X-Setup-{#AppVersion}
SetupIconFile=..\src\VD-X\app.ico
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern
Compression=lzma2
SolidCompression=yes

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"

[CustomMessages]
en.AutostartTask=Start automatically when I sign in to Windows
de.AutostartTask=Beim Anmelden an Windows automatisch starten
en.NeedsDotNet=This app needs the .NET Desktop Runtime 8 or newer, which was not found.%n%nOpen the download page now? (Setup continues either way.)
de.NeedsDotNet=Diese App benötigt die .NET Desktop Runtime 8 oder neuer, die nicht gefunden wurde.%n%nDownload-Seite jetzt öffnen? (Die Installation läuft so oder so weiter.)

[Tasks]
Name: "autostart"; Description: "{cm:AutostartTask}"

[Files]
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\VirtualDesktopAccessor.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}\licenses"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\third-party\VirtualDesktopAccessor\LICENSE.txt"; DestDir: "{app}\licenses"; DestName: "VirtualDesktopAccessor-LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Registry]
; Same entry the app writes itself (Settings → "Start automatically …").
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "VD-X"; ValueData: """{app}\{#AppExe}"" --autostart"; Tasks: autostart
; The app can turn autostart on later by itself, so always clean up on uninstall.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "VD-X"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"; ValueType: none; ValueName: "VD-X"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Quit the running app and remove autostart. If autostart runs via Task Scheduler (admin mode),
; Windows asks for administrator rights to delete that task.
Filename: "{app}\{#AppExe}"; Parameters: "--uninstall"; Flags: waituntilterminated skipifdoesntexist; RunOnceId: "Uninstall"

[Code]
// Ask a running copy to quit before files are replaced. The running copy may have administrator
// rights, which the Restart Manager (CloseApplications) cannot close from this non-elevated setup.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExePath: String;
  ResultCode: Integer;
begin
  Result := '';
  ExePath := ExpandConstant('{app}\{#AppExe}');
  if FileExists(ExePath) then
    Exec(ExePath, '--exit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function HasDesktopRuntime: Boolean;
var
  FindRec: TFindRec;
begin
  Result := False;
  if FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\*'), FindRec) then
  try
    repeat
      if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0) and
         (StrToIntDef(Copy(FindRec.Name, 1, Pos('.', FindRec.Name) - 1), 0) >= 8) then
        Result := True;
    until Result or not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not HasDesktopRuntime then
    if SuppressibleMsgBox(CustomMessage('NeedsDotNet'), mbConfirmation, MB_YESNO, IDNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;
