#define AppName "gifmaker"
#define AppVersion "1.0.1"
#define SourceDir "build"
#define OutputDir "Release"

[Setup]
AppId={{D27937AC-109A-4BF9-8EBB-9B8B6018A763}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} v{#AppVersion} patch
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
UsePreviousAppDir=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
WizardStyle=modern
OutputDir={#OutputDir}
OutputBaseFilename=gifmaker_v1.0.1_patch
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
CloseApplicationsFilter=gifmaker.exe
RestartApplications=no
MinVersion=10.0
ShowLanguageDialog=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Files]
Source: "{#SourceDir}\gifmaker.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\gifmaker.dll"; DestDir: "{app}"; Flags: ignoreversion

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not FileExists(ExpandConstant('{app}\gifmaker.exe')) or
     not FileExists(ExpandConstant('{app}\gifmaker.dll')) then
    Result := '기존 gifmaker 설치를 찾지 못했습니다. 설치된 폴더를 선택한 후 다시 실행하세요.';
end;
