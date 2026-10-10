#ifndef AppVersion
 #define AppVersion "0.5.0"
#endif
#ifndef BuildDir
 #define BuildDir "..\dist\Release-0.5.0"
#endif
[Setup]
AppId={{5B67AC71-9D02-46DE-87AE-642E81AF6374}
AppName=VWP
AppVersion={#AppVersion}
AppPublisher=domovoyproj
AppPublisherURL=https://github.com/domovoyproj/VWP
DefaultDirName={localappdata}\Programs\VWP
DefaultGroupName=VWP
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=VWP-{#AppVersion}-Setup
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\VWP.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
AppMutex=Local\DomovoyVWP
DisableProgramGroupPage=yes
[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; Flags: unchecked
[Files]
Source: "{#BuildDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.log,verification\*,libvlc\win-x86\*,libvlc\win-arm64\*"
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
[InstallDelete]
Type: filesandordirs; Name: "{app}\assets\motion"
Type: filesandordirs; Name: "{app}\assets\spatial"
[Icons]
Name: "{userprograms}\VWP"; Filename: "{app}\VWP.exe"
Name: "{userdesktop}\VWP"; Filename: "{app}\VWP.exe"; Tasks: desktopicon
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "VWP"; Flags: uninsdeletevalue
[Run]
Filename: "{app}\VWP.exe"; Description: "Открыть VWP"; Flags: nowait postinstall skipifsilent
[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var Command: String;
begin
  if CurStep = ssPostInstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'VWP', Command) then
      RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'VWP', '"' + ExpandConstant('{app}\VWP.exe') + '" --autostart');
end;
