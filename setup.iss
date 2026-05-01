; DesktopTodo 安装脚本 - Inno Setup 6

#define MyAppName "DesktopTodo"
#define MyAppVersion "1.3.1"
#define MyAppPublisher "DesktopTodo"
#define MyAppExeName "DesktopTodo.exe"
#define MyAppCopyright "Copyright (C) 2025"
#define MyAppAppId "A1B2C3D4-E5F6-7890-ABCD-EF1234567890"

[Setup]
AppId={{{#MyAppAppId}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppCopyright={#MyAppCopyright}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=G:\Projects\DesktopTodo\installer
OutputBaseFilename=DesktopTodo_Setup_{#MyAppVersion}
SetupIconFile=
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Messages]
chinesesimplified.WelcomeLabel2=这将在您的计算机上安装 {#MyAppName} {#MyAppVersion}。%n%n一个轻量级的桌面待办事项管理工具。%n%n建议在继续之前关闭所有其他应用程序。
chinesesimplified.SelectDirLabel3=安装程序将把 {#MyAppName} 安装到以下文件夹。
chinesesimplified.SelectDirBrowseLabel=如需安装到其他文件夹，请点击"浏览"。
chinesesimplified.SetupWindowTitle=安装 - {#MyAppName}

[CustomMessages]
chinesesimplified.LaunchProgram=立即运行 {#MyAppName}

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式(&D)"; GroupDescription: "附加图标:"; Flags: unchecked
Name: "startupicon"; Description: "开机自动启动(&S)"; GroupDescription: "附加选项:"; Flags: unchecked

[Files]
Source: "G:\Projects\DesktopTodo\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram}"; Flags: nowait postinstall skipifsilent

[Code]
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppAppId}_is1';

// 从注册表读取已安装版本号
function GetInstalledVersion(out Version: String): Boolean;
begin
  Result := RegQueryStringValue(HKCU, UninstallKey, 'DisplayVersion', Version);
  if not Result then
    Result := RegQueryStringValue(HKLM, UninstallKey, 'DisplayVersion', Version);
end;

// 检查已安装版本，如有旧版本则提示
function InitializeSetup(): Boolean;
var
  InstalledVersion: String;
  Key: String;
begin
  Result := True;

  Key := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}';

  if RegQueryStringValue(HKCU, Key, 'DisplayVersion', InstalledVersion) or
     RegQueryStringValue(HKLM64, Key, 'DisplayVersion', InstalledVersion) or
     RegQueryStringValue(HKLM, Key, 'DisplayVersion', InstalledVersion) then
  begin
    if InstalledVersion <> '{#MyAppVersion}' then
    begin
      if MsgBox(
        '检测到已安装版本：' + InstalledVersion + #13#10 +
        '即将安装版本：{#MyAppVersion}' + #13#10 + #13#10 +
        '是否继续安装？',
        mbConfirmation, MB_YESNO) = IDNO then
        Result := False;
    end;
  end;
end;

[Registry]
; 写入版本号到卸载注册表，供安装时版本对比
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}"; ValueType: string; ValueName: "DisplayVersion"; ValueData: "{#MyAppVersion}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startupicon

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
