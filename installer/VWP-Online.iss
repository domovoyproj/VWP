#ifndef AppVersion
 #error AppVersion is required
#endif
#ifndef ReleaseBaseURL
 #define ReleaseBaseURL "https://github.com/domovoyproj/VWP/releases/download/v" + AppVersion
#endif
[Setup]
AppId={{5B67AC71-9D02-46DE-87AE-642E81AF6374}
AppName=VWP Engine Studio
AppVersion={#AppVersion}
AppPublisher=domovoyproj
AppPublisherURL=https://github.com/domovoyproj/VWP
DefaultDirName={localappdata}\Programs\VWP
DefaultGroupName=VWP
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
#ifdef TestBuild
OutputBaseFilename=VWP-{#AppVersion}-Test-Setup
#else
OutputBaseFilename=VWP-{#AppVersion}-Setup
#endif
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\VWP.exe
Compression=lzma2
SolidCompression=yes
ArchiveExtraction=full
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
AppMutex=Local\DomovoyVWP
DisableProgramGroupPage=yes
DisableWelcomePage=no
[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
[Messages]
russian.WelcomeLabel2=Установщик скачает и проверит пакеты VWP и всех обоев. Если на компьютере нет Microsoft WebView2 Runtime, будет установлена эта среда для живых сцен. Потребуется подключение к интернету. Существующие настройки и личные сцены сохранятся.
english.WelcomeLabel2=Setup will download and verify VWP and all wallpapers. Microsoft WebView2 Runtime will be installed if it is missing. An Internet connection is required. Existing settings and personal scenes are preserved.
[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; Flags: unchecked
[Files]
Source: "..\tools\MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy
#include "..\dist\installer-parts.iss"
[InstallDelete]
Type: filesandordirs; Name: "{app}\assets\motion"
Type: filesandordirs; Name: "{app}\assets\spatial"
Type: filesandordirs; Name: "{app}\assets\vibes\characters"
Type: files; Name: "{app}\assets\living\commuter.png"
Type: files; Name: "{app}\assets\living\couple.png"
Type: files; Name: "{app}\assets\cinematic\train.png"
[Icons]
Name: "{userprograms}\VWP"; Filename: "{app}\VWP.exe"
Name: "{userdesktop}\VWP"; Filename: "{app}\VWP.exe"; Tasks: desktopicon
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "VWP"; Flags: uninsdeletevalue
[Run]
Filename: "{app}\VWP.exe"; Description: "Открыть VWP"; Flags: nowait postinstall skipifsilent
[Code]
// Detection and per-user bootstrap follow Microsoft's Evergreen deployment guide:
// https://learn.microsoft.com/microsoft-edge/webview2/concepts/distribution
function HasWebView2: Boolean;
var Version: String; Key: String;
begin
  Key := 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  Result := (RegQueryStringValue(HKLM32, Key, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'))
    or (RegQueryStringValue(HKCU, Key, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var ExitCode: Integer;
begin
  Result := '';
  if HasWebView2 then exit;
  ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
  if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
    Result := 'Microsoft WebView2 could not be installed. Check your Internet connection and run Setup again.'
  else if not HasWebView2 then
    Result := 'Microsoft WebView2 installation did not complete (code ' + IntToStr(ExitCode) + '). Install WebView2 Runtime from Microsoft and run Setup again.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Command: String;
begin
  if CurStep = ssPostInstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'VWP', Command) then
      RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'VWP', '"' + ExpandConstant('{app}\VWP.exe') + '" --autostart');
end;
