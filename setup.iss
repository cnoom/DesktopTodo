; DesktopTodo 安装脚本 - Inno Setup 6

#define MyAppName "DesktopTodo"
#define MyAppVersion "1.3.0"
#define MyAppPublisher "DesktopTodo"
#define MyAppExeName "DesktopTodo.exe"
#define MyAppCopyright "Copyright (C) 2025"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
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

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startupicon

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
