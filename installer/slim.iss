; 网络适配器助手 - 精简安装包 (不含 .NET 8 运行时)
; 通过命令行注入以下宏：
;  - /DMyAppVersion=1.2.3
;  - /DPublishDir=C:\path\to\publish

#define MyAppName "NetworkAdapterHelper"
#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif
#define MyCompanyName "NetworkAdapterHelper"
#ifndef PublishDir
#define PublishDir "..\\artifacts\\fde-multi\\win-x64"
#endif
#ifndef ArchLabel
#define ArchLabel "x64"
#endif
#ifndef ArchIdentifier
#define ArchIdentifier "x64compatible"
#endif
#ifndef ArchInstallMode
#define ArchInstallMode "x64"
#endif
#define AppExeName "NetworkAdapterHelper.exe"

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyCompanyName}
DefaultDirName={commonpf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputBaseFilename=NetworkAdapterHelper_Slim_{#ArchLabel}_Setup_{#MyAppVersion}
OutputDir=.
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed={#ArchIdentifier}
ArchitecturesInstallIn64BitMode={#ArchInstallMode}
WizardStyle=classic
DisableDirPage=no
DisableProgramGroupPage=no
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked
Name: "startmenuicon"; Description: "创建开始菜单快捷方式"

[Files]
Source: "{#PublishDir}\\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\NetworkAdapterHelper"; Filename: "{app}\{#AppExeName}"; Tasks: startmenuicon
Name: "{group}\Uninstall NetworkAdapterHelper"; Filename: "{uninstallexe}"; Tasks: startmenuicon
Name: "{commondesktop}\NetworkAdapterHelper"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon



[Code]
// 检查是否存在 .NET 8 Desktop Runtime（x64）
function IsDotNetInstalled(): Boolean;
var base, path: string;
begin
  base := ExpandConstant('{reg:HKLM\\SOFTWARE\\dotnet\\Setup\\InstalledVersions\\x64,InstallLocation|}');
  if base = '' then
    base := ExpandConstant('{pf}\\dotnet');
  path := base + '\\shared\\Microsoft.WindowsDesktop.App';
  Result := DirExists(path);
end;

// 安装初始化时提示缺少 .NET 8 运行时
function InitializeSetup(): Boolean;
begin
  if not IsDotNetInstalled() then begin
    MsgBox('未检测到 .NET 8 运行时。请先安装: https://dotnet.microsoft.com/download/dotnet/8.0', mbInformation, MB_OK);
  end;
  Result := True;
end;