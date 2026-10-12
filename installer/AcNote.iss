#ifndef AppVersion
  #define AppVersion "0.1.1"
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
var Versions: TArrayOfString; I: Integer; RuntimeRoot, VersionPath: String; FindRec: TFindRec;
begin
  Result := False;
  if RegGetSubkeyNames(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Versions) or
     RegGetSubkeyNames(HKLM32, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Versions) then
    for I := 0 to GetArrayLength(Versions) - 1 do
      if Pos('10.0.', Versions[I]) = 1 then Result := True;
  if Result then Exit;

  { The .NET installer can complete successfully even when the sharedfx
    registry key is absent or registered in the other registry view. Check
    the actual x64 runtime files as the authoritative fallback. }
  RuntimeRoot := ExpandConstant('{pf64}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if DirExists(RuntimeRoot) then
  begin
    if FindFirst(RuntimeRoot + '\10.0.*', FindRec) then
    begin
      try
        repeat
          VersionPath := FindRec.Name;
          if (Pos('10.0.', VersionPath) = 1) and DirExists(RuntimeRoot + '\' + VersionPath) then
            Result := True;
        until not FindNext(FindRec);
      finally
        FindClose(FindRec);
      end;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code: Integer;
begin
  Result := '';
  if HasDesktopRuntime then Exit;
  if MsgBox('AcNote 需要 Microsoft .NET 10 Desktop Runtime 才能运行。现在将启动微软签名的运行环境安装程序；完成后 AcNote 安装会自动继续。是否继续？', mbConfirmation, MB_YESNO) <> IDYES then
  begin
    Result := '请先安装 .NET 10 Desktop Runtime，然后重新运行 AcNote 安装程序。';
    Exit;
  end;
  ExtractTemporaryFile('windowsdesktop-runtime-win-x64.exe');
  if not ShellExec('runas', ExpandConstant('{tmp}\windowsdesktop-runtime-win-x64.exe'), '/install /norestart', '', SW_SHOWNORMAL, ewWaitUntilTerminated, Code) then
    Result := 'AcNote requires .NET 10 Desktop Runtime. Please allow its installation and retry.'
  else if (Code <> 0) and (Code <> 3010) then
    Result := 'The .NET Desktop Runtime installation failed. Error: ' + IntToStr(Code)
  else if not HasDesktopRuntime then
    Result := '运行环境安装程序已结束，但仍未找到 .NET 10 Desktop Runtime。请关闭此窗口，重新运行运行环境安装包；无需先重启 Windows。'
  else NeedsRestart := False;
end;
