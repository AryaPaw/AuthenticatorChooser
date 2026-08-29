#ifndef MyAppVersion
  #define MyAppVersion "0.7.0"
#endif
#ifndef SourceExe
  #define SourceExe "..\AuthenticatorChooser\bin\Release\net8.0-windows\win-x64\publish\AuthenticatorChooser.exe"
#endif
#ifndef OutputBase
  #define OutputBase "AuthenticatorChooser-Setup-win-x64"
#endif
#ifndef InstallArch
  #define InstallArch "x64compatible"
#endif

#define MyAppName "AuthenticatorChooser"
#define MyAppPublisher "AryaPaw"
#define MyAppURL "https://github.com/AryaPaw/AuthenticatorChooser"

[Setup]
AppId={{A4E7C91B-6F2D-4A18-9E3C-0D8B5F7A21E4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
AppCopyright=Copyright (C) 2026 Ben Hutchison; fork (C) 2026 AryaPaw
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\License.txt
OutputDir=..\artifacts
OutputBaseFilename={#OutputBase}
SetupIconFile=..\AuthenticatorChooser\YubiKey.ico
UninstallDisplayIcon={app}\AuthenticatorChooser.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed={#InstallArch}
ArchitecturesInstallIn64BitMode={#InstallArch}
CloseApplications=no
RestartApplications=no
MinVersion=10.0.22621
UsePreviousAppDir=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "AuthenticatorChooser.exe"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\AuthenticatorChooser.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\AuthenticatorChooser.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\AuthenticatorChooser.exe"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent runascurrentuser
Filename: "{app}\AuthenticatorChooser.exe"; Flags: nowait skipifnotsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
function AppImageName: String;
begin
  Result := 'AuthenticatorChooser.exe';
end;

function TaskKillApp: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM ' + AppImageName + ' /T', '', SW_HIDE, ewWaitUntilTerminated, Result);
end;

function WaitUntilAppExited: Boolean;
var
  ResultCode: Integer;
  I: Integer;
begin
  Result := False;
  for I := 1 to 30 do
  begin
    ResultCode := TaskKillApp;
    { 128 = process not found }
    if ResultCode = 128 then
    begin
      Result := True;
      Exit;
    end;
    Sleep(250);
  end;
  Result := TaskKillApp = 128;
end;

function InitializeUninstall(): Boolean;
begin
  Result := WaitUntilAppExited;
  if not Result then
    MsgBox('AuthenticatorChooser is still running and its files are locked. Choose Exit on the tray icon, then uninstall again.', mbError, MB_OK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  NeedsRestart := False;
  if WaitUntilAppExited then
    Result := ''
  else
    Result := 'AuthenticatorChooser is still running and its files are locked. Choose Exit on the tray icon, then run Setup again.';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
  TaskName: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    WaitUntilAppExited;
    TaskName := 'AuthenticatorChooser ' + #$2013 + ' ' + GetUserNameString;
    Exec(ExpandConstant('{sys}\schtasks.exe'), '/Delete /F /TN "' + TaskName + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'AuthenticatorChooser');
    DelTree(ExpandConstant('{userappdata}\AuthenticatorChooser'), True, True, True);
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    DelTree(ExpandConstant('{app}'), True, True, True);
    Exec(ExpandConstant('{sys}\cmd.exe'),
      '/C ping 127.0.0.1 -n 3 >nul & rmdir /S /Q "' + ExpandConstant('{app}') + '"',
      '', SW_HIDE, ewNoWait, ResultCode);
  end;
end;
