#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif
#ifndef RuntimeInstaller
  #define RuntimeInstaller "..\.tmp\packaging\windowsdesktop-runtime-win-x64.exe"
#endif
#ifndef OutputPath
  #define OutputPath "..\artifacts"
#endif

[Setup]
AppId={{C560D4DE-128E-4AD6-88C8-A438F17BB0A7}
AppName=AcNote
AppVersion={#AppVersion}
AppPublisher=DLWangSan
AppPublisherURL=https://github.com/DLWangSan/acnote
AppSupportURL=https://github.com/DLWangSan/acnote/issues
AppUpdatesURL=https://github.com/DLWangSan/acnote/releases
DefaultDirName={localappdata}\Programs\AcNote
DefaultGroupName=AcNote
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputPath}
OutputBaseFilename=AcNote-Setup-{#AppVersion}-win-x64
SetupIconFile=..\src\AcNote.App\Assets\acnote.ico
UninstallDisplayIcon={app}\AcNote.App.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
AppMutex=AcNote.SingleInstance
ChangesAssociations=yes

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RuntimeInstaller}"; DestName: "windowsdesktop-runtime-win-x64.exe"; Flags: dontcopy

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Icons]
Name: "{userprograms}\AcNote"; Filename: "{app}\AcNote.App.exe"
Name: "{userdesktop}\AcNote"; Filename: "{app}\AcNote.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\AcNote.App.exe"; Parameters: "--register-word"; Flags: runhidden waituntilterminated
Filename: "{app}\AcNote.App.exe"; Description: "Launch AcNote"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\AcNote.App.exe"; Parameters: "--unregister"; Flags: runhidden waituntilterminated; RunOnceId: "UnregisterAcNote"

[Code]
function HasDesktopRuntime: Boolean;
var Versions: TArrayOfString; I: Integer;
begin
  Result := False;
  if RegGetValueNames(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Versions) then
    for I := 0 to GetArrayLength(Versions) - 1 do
      if Pos('10.0.', Versions[I]) = 1 then Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code: Integer;
begin
  Result := '';
  if HasDesktopRuntime then Exit;
  ExtractTemporaryFile('windowsdesktop-runtime-win-x64.exe');
  if not ShellExec('runas', ExpandConstant('{tmp}\windowsdesktop-runtime-win-x64.exe'), '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, Code) then
    Result := 'AcNote requires .NET 10 Desktop Runtime. Please allow its installation and retry.'
  else if (Code <> 0) and (Code <> 3010) then
    Result := 'The .NET Desktop Runtime installation failed. Error: ' + IntToStr(Code)
  else if not HasDesktopRuntime then
    Result := 'The required .NET Desktop Runtime was not detected. Please restart Windows and retry.'
  else NeedsRestart := Code = 3010;
end;
