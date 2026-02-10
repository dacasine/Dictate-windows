; Dictate for Windows - Inno Setup Script
; Alternative installer for those who prefer EXE over MSI

#define AppName "Dictate for Windows"
#define AppVersion "1.0.0"
#define AppPublisher "Dictate"
#define AppURL "https://github.com/devemperor/dictate"
#define AppExeName "DictateForWindows.exe"
#define BuildOutput "..\DictateForWindows\bin\Release\net8.0-windows10.0.22621.0\win-x64\publish"

[Setup]
; Unique identifier for the application
AppId={{F1E2D3C4-B5A6-7890-CDEF-1234567890AB}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}

; Installation directories
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

; License and readme
LicenseFile=License.rtf
InfoBeforeFile=ReadmeFirst.txt

; Output settings
OutputDir=Output
OutputBaseFilename=DictateForWindows-{#AppVersion}-Setup
SetupIconFile=..\DictateForWindows\Assets\app-icon.ico
Compression=lzma2/ultra64
SolidCompression=yes

; Windows version requirements
MinVersion=10.0.17763
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

; Privileges
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; UI settings
WizardStyle=modern
WizardSizePercent=100

; Uninstaller
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start with Windows"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
; Main application files
Source: "{#BuildOutput}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutput}\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "{#BuildOutput}\*.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutput}\*.pri"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutput}\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{#AppName} Settings"; Filename: "{app}\{#AppExeName}"; Parameters: "--settings"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"

; Desktop shortcut (optional)
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Startup registration (optional)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Dictate for Windows"; ValueData: """{app}\{#AppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: startupicon

; App registration
Root: HKCU; Subkey: "Software\Dictate"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Dictate"; ValueType: string; ValueName: "Version"; ValueData: "{#AppVersion}"

[Run]
; Launch after install
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up user data on uninstall (optional - commented out by default to preserve settings)
; Type: filesandordirs; Name: "{userappdata}\Dictate"

[Code]
// Check for running instances before installation
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  // Try to close running instances
  Exec('taskkill.exe', '/F /IM DictateForWindows.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Check for .NET 8 runtime
function IsDotNet8Installed(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Post-install tasks can be added here
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpReady then
  begin
    // Validate before installation starts
  end;
end;
