#define AppName "gifmaker"
#define AppVersion "1.0.1"
#ifndef SourceDir
  #define SourceDir "dist\gifmaker"
#endif
#ifndef OutputDir
  #define OutputDir "Release"
#endif

[Setup]
AppId={{D27937AC-109A-4BF9-8EBB-9B8B6018A763}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppName}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
WizardStyle=modern
UninstallDisplayIcon={app}\gifmaker.exe
OutputDir={#OutputDir}
OutputBaseFilename=gifmaker_v{#AppVersion}_Setup
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
CloseApplicationsFilter=gifmaker.exe
MinVersion=10.0
ShowLanguageDialog=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "tmp\*,gifmaker.language"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\gifmaker"; Filename: "{app}\gifmaker.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\gifmaker"; Filename: "{app}\gifmaker.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "바탕 화면 바로 가기 만들기"; GroupDescription: "추가 작업:"

[Run]
Filename: "{app}\gifmaker.exe"; Description: "gifmaker 실행"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent
