; OpenBroadcaster Inno Setup Script
; Requires Inno Setup 6.0 or later (https://jrsoftware.org/isinfo.php)

#define MyAppName "OpenBroadcaster"
#define MyAppVersion "3.1"
#define MyAppPublisher "WickedMediaSolutions"
#define MyAppURL "https://github.com/WickedMediaSolutions/openbroadcaster"
#define MyAppExeName "OpenBroadcaster.Avalonia.exe"
#define MyAppAssocName "OpenBroadcaster Project"
#define MyAppAssocExt ".obproj"
#define MyAppAssocKey StringChange(MyAppAssocName, " ", "") + MyAppAssocExt

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
AppId={{8A7B3C9D-4E5F-6A7B-8C9D-0E1F2A3B4C5D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; Output settings
OutputDir=..\bin\InstallerOutput
OutputBaseFilename=OpenBroadcaster-{#MyAppVersion}-Setup
SetupIconFile=..\Assets\app-icon.ico
; Compression
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
; Appearance
WizardStyle=modern
WizardSizePercent=120
; Privileges - install for current user by default, but allow admin install
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; Minimum Windows version (Windows 10)
MinVersion=10.0
; Architecture
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Uninstaller
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Main application files from the publish output
Source: "..\bin\Installer\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Application icon for shortcuts
Source: "..\Assets\app-icon.ico"; DestDir: "{app}"; Flags: ignoreversion
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app-icon.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app-icon.ico"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app-icon.ico"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up user data folder on uninstall (optional - commented out to preserve settings)
; Type: filesandordirs; Name: "{userappdata}\OpenBroadcaster"

[Code]
// Check if .NET 8 Desktop Runtime is installed (for framework-dependent builds)
// Not needed for self-contained builds, but kept for reference
function IsDotNet8DesktopInstalled(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

// Custom initialization
function InitializeSetup(): Boolean;
begin
  Result := True;
  // Add any pre-installation checks here
end;

// Post-installation tasks
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Create AppData folder for settings
    CreateDir(ExpandConstant('{userappdata}\OpenBroadcaster'));
    CreateDir(ExpandConstant('{userappdata}\OpenBroadcaster\logs'));
  end;
end;
