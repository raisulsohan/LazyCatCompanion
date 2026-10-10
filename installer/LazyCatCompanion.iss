#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif
#ifndef InstallerOutput
  #define InstallerOutput "..\artifacts\installer"
#endif

[Setup]
AppId={{391136D1-F4BB-4E93-BE96-5B0E94637E30}
AppName=Lazy Cat Companion
AppVersion=0.3.0
AppPublisher=Lazy Cat Companion
DefaultDirName={autopf}\LazyCatCompanion
DefaultGroupName=Lazy Cat Companion
UninstallDisplayIcon={app}\LazyCatCompanion.exe
SetupIconFile=..\Assets\LazyCatCompanion.ico
CloseApplications=yes
PrivilegesRequired=lowest
OutputDir={#InstallerOutput}
OutputBaseFilename=LazyCatCompanion-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startup"; Description: "Start Lazy Cat Companion when Windows starts"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\LazyCatCompanion.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Lazy Cat Companion"; Filename: "{app}\LazyCatCompanion.exe"
Name: "{autodesktop}\Lazy Cat Companion"; Filename: "{app}\LazyCatCompanion.exe"; Tasks: desktopicon
Name: "{userstartup}\Lazy Cat Companion"; Filename: "{app}\LazyCatCompanion.exe"; Tasks: startup

[Run]
Filename: "{app}\LazyCatCompanion.exe"; Description: "Launch Lazy Cat Companion"; Flags: nowait postinstall skipifsilent
