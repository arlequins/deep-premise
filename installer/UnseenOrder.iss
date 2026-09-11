#define AppVersion "0.10.0"
[Setup]
AppId={{A907C2D4-608E-45C0-9D0E-9C72E69F4F63}
AppName=Unseen Order
AppVersion={#AppVersion}
AppPublisher=Arlequins
DefaultDirName={localappdata}\Programs\Unseen Order
DefaultGroupName=Unseen Order
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=Unseen-Order-{#AppVersion}-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Unseen-Order.exe
[Files]
Source: "..\dist\Unseen-Order-{#AppVersion}-Windows-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Unseen Order"; Filename: "{app}\Unseen-Order.exe"
Name: "{userdesktop}\Unseen Order"; Filename: "{app}\Unseen-Order.exe"
[Run]
Filename: "{app}\Unseen-Order.exe"; Description: "Launch Unseen Order"; Flags: nowait postinstall skipifsilent
