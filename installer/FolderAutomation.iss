#define MyAppName "Folder Automation"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Folder Automation"
#define MyAppExeName "FolderAutomation.exe"

[Setup]
AppId={{F4E3D5B1-9B8B-4C7F-9E31-8B4D6A2C91F0}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}

DefaultDirName={autopf}\Folder Automation
DefaultGroupName=Folder Automation

OutputDir=..\installer-output
OutputBaseFilename=FolderAutomation-Setup

Compression=lzma
SolidCompression=yes

ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

PrivilegesRequired=admin

SetupIconFile=..\src\FolderAutomation.App\Assets\app.ico

UninstallDisplayIcon={app}\FolderAutomation.exe

WizardStyle=modern

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Folder Automation"; Filename: "{app}\FolderAutomation.exe"
Name: "{autodesktop}\Folder Automation"; Filename: "{app}\FolderAutomation.exe"

[Run]
Filename: "{app}\FolderAutomation.exe"; Description: "Launch Folder Automation"; Flags: nowait postinstall skipifsilent