; Inno Setup script cho SafeFreeSpace
; Build: ISCC.exe installer.iss
; Yêu cầu: chạy publish.ps1 (hoặc dotnet publish tương đương) trước để có artifacts/publish/app

#define AppName "SafeFreeSpace"
#define AppVersion "1.0.0-dev"
#define AppPublisher "redbluevn"
#define AppURL "https://github.com/redbluevn/SafeFreeSpace"

[Setup]
AppId={{7F3A9C21-4E5B-4D8A-B2C6-9A1E0F5D3B74}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
; Cài vào Program Files cần admin; cũng bảo vệ worker exe (requireAdministrator) khỏi bị sửa đổi
PrivilegesRequired=admin
OutputDir=artifacts\installer
OutputBaseFilename=SafeFreeSpace-Setup-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#AppName}
LicenseFile=LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "artifacts\publish\app\SafeFreeSpace.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "artifacts\publish\app\SafeFreeSpace.ElevatedWorker.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\SafeFreeSpace.exe"
Name: "{group}\Gỡ cài đặt {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\SafeFreeSpace.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Tạo shortcut ngoài Desktop"; GroupDescription: "Shortcut:"; Flags: unchecked

[Run]
Filename: "{app}\SafeFreeSpace.exe"; Description: "Chạy {#AppName}"; Flags: nowait postinstall skipifsilent
