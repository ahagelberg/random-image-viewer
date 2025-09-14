[Setup]
AppName=Random Image Viewer
AppVersion=1.2.0
AppPublisher=Your Company
AppPublisherURL=https://github.com/yourusername/random-image-viewer
AppSupportURL=https://github.com/yourusername/random-image-viewer/issues
AppUpdatesURL=https://github.com/yourusername/random-image-viewer/releases
DefaultDirName={autopf}\Random Image Viewer
DefaultGroupName=Random Image Viewer
AllowNoIcons=yes
LicenseFile=
OutputDir=installer
OutputBaseFilename=RandomImageViewer-Setup
SetupIconFile=RandomImageViewer\app.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked
Name: "quicklaunchicon"; Description: "Create a &Quick Launch shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked; OnlyBelowVersion: 6.1

[Files]
Source: "RandomImageViewer\bin\Release\net8.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "RandomImageViewer\app.ico"; DestDir: "{app}"; Flags: ignoreversion; Check: FileExists('RandomImageViewer\app.ico')

; Include .NET 8.0 runtime files if needed
Source: "RandomImageViewer\bin\Release\net8.0-windows\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "RandomImageViewer\bin\Release\net8.0-windows\*.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "RandomImageViewer\bin\Release\net8.0-windows\*.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "RandomImageViewer\bin\Release\net8.0-windows\*.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "RandomImageViewer\bin\Release\net8.0-windows\*.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Random Image Viewer"; Filename: "{app}\RandomImageViewer.exe"; WorkingDir: "{app}"
Name: "{group}\Uninstall Random Image Viewer"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Random Image Viewer"; Filename: "{app}\RandomImageViewer.exe"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\Random Image Viewer"; Filename: "{app}\RandomImageViewer.exe"; WorkingDir: "{app}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\RandomImageViewer.exe"; Description: "Launch Random Image Viewer"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"