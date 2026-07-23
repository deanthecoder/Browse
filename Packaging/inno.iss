; Generated via Installer/pack.py token replacement.

#define MyAppName "{{ProductName}}"
#define MyAppVersion "{{Version}}"
#define MyAppPublisher "{{CompanyName}}"
#define MyAppURL "{{PublisherUrl}}"
#define MyAppExeName "{{Executable}}"
#define MyAppAppId "{{AppId}}"
#define MyAppSourceDir "{{SourceDir}}"
#define MyAppOutputDir "{{OutputDir}}"
#define MyAppOutputBase "{{OutputBase}}"
#define MyAppIcon "{{SetupIconFile}}"
#define MyAppShowRunOnStartupTask {{ShowRunOnStartupTask}}
#define MyAppCommandLine {{CommandLine}}

[Setup]
AppId={#MyAppAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
OutputDir={#MyAppOutputDir}
OutputBaseFilename={#MyAppOutputBase}
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
#if MyAppIcon != ""
SetupIconFile={#MyAppIcon}
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "explorercontext"; Description: "Add 'Browse...' to File Explorer context menus"; GroupDescription: "File Explorer integration:"; Flags: checkedonce
#if MyAppShowRunOnStartupTask
Name: "startup"; Description: "Open {#MyAppName} when Windows starts"; GroupDescription: "Startup options:"; Flags: unchecked
#endif

[Files]
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
#if MyAppShowRunOnStartupTask
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"" --background"; Flags: uninsdeletevalue; Tasks: startup
#endif
Root: HKCR; Subkey: "Directory\shell\Browse"; ValueType: string; ValueName: ""; ValueData: "Browse..."; Flags: uninsdeletekey; Tasks: explorercontext
Root: HKCR; Subkey: "Directory\shell\Browse"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"; Tasks: explorercontext
Root: HKCR; Subkey: "Directory\shell\Browse\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: explorercontext
Root: HKCR; Subkey: "Directory\Background\shell\Browse"; ValueType: string; ValueName: ""; ValueData: "Browse..."; Flags: uninsdeletekey; Tasks: explorercontext
Root: HKCR; Subkey: "Directory\Background\shell\Browse"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"; Tasks: explorercontext
Root: HKCR; Subkey: "Directory\Background\shell\Browse\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%V"""; Tasks: explorercontext
Root: HKCR; Subkey: "Drive\shell\Browse"; ValueType: string; ValueName: ""; ValueData: "Browse..."; Flags: uninsdeletekey; Tasks: explorercontext
Root: HKCR; Subkey: "Drive\shell\Browse"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#MyAppExeName}"",0"; Tasks: explorercontext
Root: HKCR; Subkey: "Drive\shell\Browse\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: explorercontext

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
