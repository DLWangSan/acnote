#ifndef AppVersion
  #define AppVersion "0.1.16"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif
#ifndef RuntimeInstaller
  #define RuntimeInstaller "..\.tmp\packaging\windowsdesktop-runtime-win-x64.exe"
#endif
#ifndef RuntimeInstallerX86
  #define RuntimeInstallerX86 "..\.tmp\packaging\windowsdesktop-runtime-win-x86.exe"
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
Source: "{#RuntimeInstallerX86}"; DestName: "windowsdesktop-runtime-win-x86.exe"; Flags: dontcopy

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Icons]
Name: "{userprograms}\AcNote"; Filename: "{app}\AcNote.App.exe"
Name: "{userdesktop}\AcNote"; Filename: "{app}\AcNote.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\AcNote.App.exe"; Description: "Launch AcNote"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\AcNote.App.exe"; Parameters: "--unregister"; Flags: runhidden waituntilterminated; RunOnceId: "UnregisterAcNote"

[Code]
function HasDesktopRuntimeX64: Boolean;
var Versions: TArrayOfString; I: Integer; RuntimeRoot, VersionPath: String; FindRec: TFindRec;
begin
  Result := False;
  if RegGetSubkeyNames(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Versions) then
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

function HasDesktopRuntimeX86: Boolean;
var Versions: TArrayOfString; I: Integer; RuntimeRoot, VersionPath: String; FindRec: TFindRec;
begin
  Result := False;
  if RegGetSubkeyNames(HKLM32, 'SOFTWARE\dotnet\Setup\InstalledVersions\x86\sharedfx\Microsoft.WindowsDesktop.App', Versions) then
    for I := 0 to GetArrayLength(Versions) - 1 do
      if Pos('10.0.', Versions[I]) = 1 then Result := True;
  if Result then Exit;
  RuntimeRoot := ExpandConstant('{pf32}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if DirExists(RuntimeRoot) and FindFirst(RuntimeRoot + '\10.0.*', FindRec) then
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

function IsProcessRunning(const ImageName: String; var CheckFailed: Boolean): Boolean;
var ResultCode: Integer; OutputFile: String; OutputText: AnsiString; CommandLine: String;
begin
  Result := False;
  OutputFile := ExpandConstant('{tmp}\acnote-process-check.txt');
  DeleteFile(OutputFile);
  CommandLine := '/C tasklist /FI "IMAGENAME eq ' + ImageName + '" /FO CSV /NH > "' + OutputFile + '"';
  if not Exec(ExpandConstant('{sys}\cmd.exe'), CommandLine, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or
     (ResultCode <> 0) or not LoadStringFromFile(OutputFile, OutputText) then
  begin
    CheckFailed := True;
    Exit;
  end;
  Result := Pos('"' + Uppercase(ImageName) + '"', Uppercase(String(OutputText))) > 0;
end;

function RunningOfficeApps(var CheckFailed: Boolean): String;
begin
  Result := '';
  if IsProcessRunning('WINWORD.EXE', CheckFailed) then Result := 'Microsoft Word';
  if IsProcessRunning('wps.exe', CheckFailed) then
  begin
    if Result <> '' then Result := Result + '、';
    Result := Result + 'WPS Writer';
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Code: Integer; CheckFailed: Boolean; RunningApps: String;
begin
  Result := '';
  repeat
    CheckFailed := False;
    RunningApps := RunningOfficeApps(CheckFailed);
    if CheckFailed then
    begin
      Result := '安装程序无法确认 Word/WPS 是否已经退出。请稍后重试；为避免插件注册失败，本次安装已取消。';
      Exit;
    end;
    if RunningApps = '' then Break;
    if MsgBox('检测到 ' + RunningApps + ' 正在运行。插件需要在程序启动时加载。请保存文档并完全退出 Word/WPS（包括所有窗口和后台进程），然后点击“确定”继续检查；点击“取消”退出安装。', mbConfirmation, MB_OKCANCEL) <> IDOK then
    begin
      Result := '请先完全退出 Word/WPS，再运行 AcNote 安装程序。';
      Exit;
    end;
  until False;

  if HasDesktopRuntimeX64 and HasDesktopRuntimeX86 then Exit;
  if MsgBox('AcNote 需要 Microsoft .NET 10 Desktop Runtime（x64 和 x86），以支持 64 位 Word 和 32 位 WPS。现在将安装缺少的微软签名运行环境；无需重启，完成后 AcNote 安装会自动继续。是否继续？', mbConfirmation, MB_YESNO) <> IDYES then
  begin
    Result := '请先安装 .NET 10 Desktop Runtime，然后重新运行 AcNote 安装程序。';
    Exit;
  end;
  if not HasDesktopRuntimeX64 then
  begin
    ExtractTemporaryFile('windowsdesktop-runtime-win-x64.exe');
    if not ShellExec('runas', ExpandConstant('{tmp}\windowsdesktop-runtime-win-x64.exe'), '/install /norestart', '', SW_SHOWNORMAL, ewWaitUntilTerminated, Code) then
      Result := '无法启动 .NET 10 x64 Desktop Runtime 安装程序。'
    else if (Code <> 0) and (Code <> 3010) then
      Result := '.NET 10 x64 Desktop Runtime 安装失败，错误码：' + IntToStr(Code)
    else if not HasDesktopRuntimeX64 then
      Result := '安装程序结束后仍未找到 .NET 10 x64 Desktop Runtime。请重试，无需先重启 Windows。';
    if Result <> '' then Exit;
  end;
  if not HasDesktopRuntimeX86 then
  begin
    ExtractTemporaryFile('windowsdesktop-runtime-win-x86.exe');
    if not ShellExec('runas', ExpandConstant('{tmp}\windowsdesktop-runtime-win-x86.exe'), '/install /norestart', '', SW_SHOWNORMAL, ewWaitUntilTerminated, Code) then
      Result := '无法启动 .NET 10 x86 Desktop Runtime 安装程序。'
    else if (Code <> 0) and (Code <> 3010) then
      Result := '.NET 10 x86 Desktop Runtime 安装失败，错误码：' + IntToStr(Code)
    else if not HasDesktopRuntimeX86 then
      Result := '安装程序结束后仍未找到 .NET 10 x86 Desktop Runtime。请重试，无需先重启 Windows。';
  end;
  NeedsRestart := False;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    if not Exec(ExpandConstant('{app}\AcNote.App.exe'), '--register-word', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      MsgBox('AcNote 已安装，但 Word/WPS 插件注册程序未能启动。请从开始菜单启动 AcNote，然后重新打开 Word/WPS。', mbError, MB_OK)
    else if ResultCode <> 0 then
      MsgBox('AcNote 已安装，但 Word/WPS 插件登记失败。请查看 %AppData%\AcNote\word-registration.log，或启动 AcNote 后重试。', mbError, MB_OK);
  end;
end;
