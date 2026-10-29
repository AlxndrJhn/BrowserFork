; Inno Setup script for BrowserFork-Setup.exe. Built by CI:
;   iscc /DAppVersion=1.2.3 /DSourceDir=..\dist installer\BrowserFork.iss
; Installs machine-wide (Settings › Default apps only lists browsers registered under HKLM),
; registers BrowserFork and starts the tray icon. The tray's auto-update runs the next setup with /SILENT.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\dist"
#endif

[Setup]
AppId={{8370E51C-70DD-459D-9D06-75A1EC88A308}
AppName=BrowserFork
AppVersion={#AppVersion}
AppVerName=BrowserFork {#AppVersion}
AppPublisher=AlxndrJhn
AppPublisherURL=https://github.com/AlxndrJhn/BrowserFork
AppUpdatesURL=https://github.com/AlxndrJhn/BrowserFork/releases
DefaultDirName={autopf}\BrowserFork
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=BrowserFork-Setup
SetupIconFile=..\src\BrowserFork.ico
UninstallDisplayIcon={app}\BrowserFork.exe
LicenseFile=..\LICENSE
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
; The tray (and any open picker) runs from {app}; let Restart Manager close it.
CloseApplications=force
RestartApplications=no

[Tasks]
Name: autostart; Description: "Show the tray icon when Windows starts (settings and updates)"

[Files]
Source: "{#SourceDir}\BrowserFork.exe"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
; Same value the tray's "Show the tray icon when Windows starts" writes.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BrowserFork"; \
    ValueData: """{app}\BrowserFork.exe"" --tray"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\BrowserFork.exe"; Parameters: "--register-machine --quiet"; StatusMsg: "Registering BrowserFork..."; Flags: runhidden waituntilterminated
Filename: "{app}\BrowserFork.exe"; Parameters: "--register --quiet"; Flags: runasoriginaluser waituntilterminated
; Not skipped when silent, so the tray comes back after an auto-update.
Filename: "{app}\BrowserFork.exe"; Parameters: "--tray"; Flags: runasoriginaluser nowait; Tasks: autostart
Filename: "ms-settings:defaultapps?registeredAppUser=BrowserFork"; Description: "Choose BrowserFork as the default browser"; \
    Flags: postinstall shellexec skipifsilent runasoriginaluser nowait

[UninstallRun]
Filename: "{cmd}"; Parameters: "/c taskkill /im BrowserFork.exe /f"; Flags: runhidden; RunOnceId: "StopTray"
Filename: "{app}\BrowserFork.exe"; Parameters: "--unregister --quiet"; Flags: runhidden waituntilterminated; RunOnceId: "Unregister"

[Code]
// BrowserFork is framework-dependent: it needs the .NET 10 Desktop Runtime (x64).
function HasDesktopRuntime(): Boolean;
var
  FindRec: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), FindRec);
  if Result then FindClose(FindRec);
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if HasDesktopRuntime() then Exit;
  if WizardSilent() then
  begin
    Result := False;
    Exit;
  end;
  if MsgBox('BrowserFork needs the .NET 10 Desktop Runtime (x64), which isn''t installed.' + #13#10#13#10 +
            'Open the download page now? Run this setup again once it''s installed.',
            mbConfirmation, MB_YESNO) = IDYES then
    ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/10.0', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  Result := False;
end;
