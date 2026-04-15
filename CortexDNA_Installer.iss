#define MyAppName "Cortex DNA"
#define MyAppVersion "1.3.5"
#define MyAppPublisher "Cortex"
#define MyAppExeName "CortexDNA.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{CortexDNA-1090-4321-ABCD-1234567890AB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={commonpf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; Require Admin Privileges for Program Files access
PrivilegesRequired=admin
; Force 64-bit Installation Mode (Installs to C:\Program Files, not x86)
ArchitecturesInstallIn64BitMode=x64
OutputDir=E:\Code-Setup\Cortex Core\app\CortexDNA\bin\Release\Installer
OutputBaseFilename=CortexDNA_Installer_v1.3.5_all
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Source is the Framework-Dependent Publish Folder
; We specifically include the .dll and runtimeconfig.json for framework-dependent apps
Source: "E:\Code-Setup\CortexDNA\bin\Release\net10.0-windows\win-x64\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "E:\Code-Setup\CortexDNA\bin\Release\net10.0-windows\win-x64\publish\CortexDNA.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "E:\Code-Setup\CortexDNA\bin\Release\net10.0-windows\win-x64\publish\CortexDNA.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion

; Include all other files (dependencies, assets)
Source: "E:\Code-Setup\CortexDNA\bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; shellexec flag is CRITICAL for launching apps that require Admin rights (UAC)
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallDelete]
; Full Clean: Remove the specs.json cache and log files from LocalAppData
Type: filesandordirs; Name: "{localappdata}\Cortex DNA"
; Ensure the install directory is fully removed
Type: filesandordirs; Name: "{app}"

[Registry]
; Clean up the Startup Registry Key on Uninstall
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "CortexDNA"; Flags: uninsdeletevalue

[Code]
function InitializeUninstall(): Boolean;
var
  ErrorCode: Integer;
begin
  // Force kill the app and all child processes silently
  ShellExec('open', 'taskkill.exe', '/F /IM CortexDNA.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
  Result := True;
end;
