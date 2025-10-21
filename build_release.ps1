<#
.SYNOPSIS
  构建并打包 NetworkAdapterHelper 的四种发行版本：
  1) 自包含单一可执行文件
  2) 框架依赖单一可执行文件
  3) 完整安装包（含 .NET 8 运行时，Inno Setup）
  4) 精简安装包（不含运行时，Inno Setup）

.DESCRIPTION
  - 使用 dotnet publish 生成两种单文件版本（自包含与框架依赖）
  - 调用 Inno Setup Compiler(ISCC.exe) 生成两种安装包
  - 支持通过参数设置版本号、构建配置、运行时标识(RID)和运行时安装包路径
  - 输出清晰的构建日志，并确保脚本可重复执行

.PARAMETER Version
  应用版本号（例如：1.2.3）。将写入程序集版本与安装包版本。

.PARAMETER Configuration
  构建配置（Release/Debug）。默认：Release。

.PARAMETER RuntimeIdentifier
  运行时标识（win-x64/win-x86/win-arm64）。默认：win-x64。

.PARAMETER RuntimeInstallerPath
  完整版安装包内置的 .NET 8 运行时安装器路径（例如：C:\redist\dotnet-runtime-8.0.x-win-x64.exe）。
  如果未提供，将尝试继续，但构建完整安装包会失败并给出明确错误。

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\build_release.ps1 -Version 1.0.0

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\build_release.ps1 -Version 1.0.0 -RuntimeIdentifier win-x64 -RuntimeInstallerPath "C:\redist\dotnet-runtime-8.0.14-win-x64.exe"

.NOTES
  - 需要已安装 .NET SDK (8.x) 和 Inno Setup 6（ISCC.exe）。
  - Inno Setup 默认位置：
      C:\Program Files (x86)\Inno Setup 6\ISCC.exe
      或 C:\Program Files\Inno Setup 6\ISCC.exe
  - 发布目录与安装包输出位于 artifacts/ 下。
#>

param(
  [Parameter(Mandatory=$false)]
  [string]$Version,
  
  [Parameter(Mandatory=$false)]
  [ValidateSet('Release','Debug')]
  [string]$Configuration,
  
  [Parameter(Mandatory=$false)]
  [ValidateSet('win-x64','win-x86','win-arm64')]
  [string]$RuntimeIdentifier,
  
  [Parameter(Mandatory=$false)]
  [string]$RuntimeInstallerPath,
  
  [switch]$Help
)

# 设置默认值
if ([string]::IsNullOrWhiteSpace($Configuration)) {
  $Configuration = 'Release'
}

# 处理运行时标识符：如果未指定，则默认为所有支持的平台
if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
  $RuntimeIdentifiers = @('win-x64', 'win-x86', 'win-arm64')
  Write-Host "No RuntimeIdentifier specified. Building for all platforms: $($RuntimeIdentifiers -join ', ')" -ForegroundColor Yellow
} else {
  $RuntimeIdentifiers = @($RuntimeIdentifier)
  Write-Host "Building for specified platform: $RuntimeIdentifier" -ForegroundColor Green
}

if ([string]::IsNullOrWhiteSpace($RuntimeInstallerPath)) {
  $RuntimeInstallerPath = ''
}

#
# 函数: Show-Help
# 说明: 显示脚本使用帮助信息
# 参数: 无
# 返回: 无
#
function Show-Help {
  $usage = @"
Usage:
  powershell -ExecutionPolicy Bypass -File .\build_release.ps1 -Version <version> [-Configuration Release|Debug] [-RuntimeIdentifier win-x64|win-x86|win-arm64] [-RuntimeInstallerPath <path>] [-Help]

Examples:
  powershell -ExecutionPolicy Bypass -File .\build_release.ps1 -Version 1.0.0
  powershell -ExecutionPolicy Bypass -File .\build_release.ps1 -Version 1.0.0 -RuntimeIdentifier win-x86
  powershell -ExecutionPolicy Bypass -File .\build_release.ps1 -Version 1.0.0 -RuntimeIdentifier win-arm64

Notes:
  Installer output file names include architecture and contain "Setup":
  NetworkAdapterHelper_Full_x64_Setup_1.0.0.exe / NetworkAdapterHelper_Slim_arm64_Setup_1.0.0.exe
"@
  Write-Host $usage
}
if ($Help -or ($args -contains '--help') -or ($args -contains '/?')) {
  Show-Help
  exit 0
}
if ([string]::IsNullOrWhiteSpace($Version)) {
  Write-Host "Version is required." -ForegroundColor Red
  Show-Help
  exit 1
}
# 解决路径变量
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$CsprojPath = Join-Path $ScriptRoot 'NetworkAdapterHelper.csproj'
$ArtifactsDir = Join-Path $ScriptRoot 'artifacts'
$LogsDir = Join-Path $ArtifactsDir 'logs'
$PublishSelfContainedMultiDir = Join-Path $ArtifactsDir "sc-multi\$RuntimeIdentifier"
$PublishFrameworkDependentDir = Join-Path $ArtifactsDir "single-file-framework-dependent\$RuntimeIdentifier"
$PublishFrameworkDependentMultiDir = Join-Path $ArtifactsDir "fde-multi\$RuntimeIdentifier"
$InstallersOutputDir = $ArtifactsDir
$InstallerDir = Join-Path $ScriptRoot 'installer'

#
# 函数: Get-ArchLabel
# 说明: 根据运行时标识符获取架构标签
# 参数: [string] $Rid - 运行时标识符
# 返回: [string] 架构标签
#
function Get-ArchLabel {
  param([string]$Rid)
  switch ($Rid) {
    'win-x64'   { return 'x64' }
    'win-x86'   { return 'x86' }
    'win-arm64' { return 'arm64' }
    default     { return $Rid }
  }
}

#
# 函数: Get-InnoArchIdentifier
# 说明: 根据运行时标识符获取 Inno Setup 架构标识符
# 参数: [string] $Rid - 运行时标识符
# 返回: [string] Inno Setup 架构标识符
#
function Get-InnoArchIdentifier {
  param([string]$Rid)
  switch ($Rid) {
    'win-x64'   { return 'x64compatible' }
    'win-x86'   { return 'x86' }
    'win-arm64' { return 'arm64' }  # Inno Setup 6.5+ 支持 arm64
    default     { return 'x64compatible' }
  }
}

#
# 函数: Get-InnoArchInstallMode
# 说明: 根据运行时标识符获取 Inno Setup 安装模式
# 参数: [string] $Rid - 运行时标识符
# 返回: [string] Inno Setup 安装模式
#
function Get-InnoArchInstallMode {
  param([string]$Rid)
  switch ($Rid) {
    'win-x64'   { return 'x64' }
    'win-x86'   { return '' }  # x86 不需要 64-bit 安装模式
    'win-arm64' { return 'arm64' }
    default     { return 'x64' }
  }
}

#
# 函数: Write-Log
# 说明: 标准化输出日志，含时间戳与级别
# 参数: [string] $Message, [string] $Level
# 返回: 无
#
function Write-Log {
  param(
    [string]$Message,
    [ValidateSet('INFO','WARN','ERROR','DEBUG')]
    [string]$Level = 'INFO'
  )
  $ts = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
  Write-Host "[$ts][$Level] $Message"
}

#
# 函数: Ensure-Directory
# 说明: 确保目录存在，不存在则创建
# 参数: [string] $Path
# 返回: 无
#
function Ensure-Directory {
  param([string]$Path)
  if (-not (Test-Path -LiteralPath $Path)) {
    New-Item -ItemType Directory -Path $Path | Out-Null
  }
}

#
# 函数: Find-InnoCompiler
# 说明: 查找 Inno Setup 编译器(ISCC.exe)路径
# 参数: 无
# 返回: [string] ISCC.exe 路径或空字符串
#
function Find-InnoCompiler {
  $cmd = $null
  try { $cmd = Get-Command iscc -ErrorAction SilentlyContinue } catch {}
  if ($cmd -and $cmd.Source) { return $cmd.Source }

  try { $cmd = Get-Command ISCC -ErrorAction SilentlyContinue } catch {}
  if ($cmd -and $cmd.Source) { return $cmd.Source }

  $commonPaths = @(
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
  )
  foreach ($p in $commonPaths) {
    if (Test-Path -LiteralPath $p) { return $p }
  }
  return ''
}

#
# 函数: Invoke-DotnetPublish
# 说明: 执行 dotnet publish 生成版本（支持单文件和多文件）
# 参数: [bool] $SelfContained, [string] $OutputDir, [string] $RuntimeId, [bool] $SingleFile
# 返回: 无（抛出异常表示失败）
#
function Invoke-DotnetPublish {
  param(
    [bool]$SelfContained,
    [string]$OutputDir,
    [string]$RuntimeId,
    [bool]$SingleFile = $false
  )
  Ensure-Directory $OutputDir
  $scFlag = if ($SelfContained) { 'true' } else { 'false' }
  $singleFileFlag = if ($SingleFile) { 'true' } else { 'false' }

  Write-Log "Publishing: SelfContained=$scFlag, SingleFile=$singleFileFlag, RID=$RuntimeId, Output=$OutputDir"

  $args = @(
    'publish',
    $CsprojPath,
    '-c', $Configuration,
    '-r', $RuntimeId,
    "-p:Version=$Version",
    "-p:PublishSingleFile=$singleFileFlag",
    "-p:IncludeNativeLibrariesForSelfExtract=$singleFileFlag",
    '-p:PublishTrimmed=false',
    '-p:DebugType=None',
    '-p:PublishReadyToRun=true',
    '-p:SatelliteResourceLanguages=en',
    '--self-contained', $scFlag,
    '-o', $OutputDir
  )

  $pubSuffix = if ($SelfContained) { 
    if ($SingleFile) { 'sc-single' } else { 'sc-multi' }
  } else { 
    'fde' 
  }
  $pubLogPath = Join-Path $LogsDir ("publish_" + $pubSuffix + "_" + $RuntimeId + ".log")
  & dotnet @args | Tee-Object -FilePath $pubLogPath

  Write-Log ("Publish done: " + (Get-Item $OutputDir).FullName)
}

#
# 函数: Build-InstallerFull
# 说明: 使用 Inno Setup 构建完整安装包（含运行时）
# 参数: [string] $IsccPath, [string] $PublishDir, [string] $OutputDir, [string] $RuntimeInstallerPath
# 返回: 无
#
function Build-InstallerFull {
  param(
    [string]$IsccPath,
    [string]$PublishDir,
    [string]$OutputDir,
    [string]$ArchLabel,
    [string]$InnoArchIdentifier,
    [string]$InnoArchInstallMode
  )
  Ensure-Directory $OutputDir
  $iss = Join-Path $InstallerDir 'full.iss'
  if (-not (Test-Path -LiteralPath $iss)) { throw "Missing Inno Setup script: $iss" }

  Write-Log "Building full installer (ISCC): $iss"
  $cmdArgs = @(
    "/DMyAppVersion=$Version",
    "/DPublishDir=$PublishDir",
    "/DArchLabel=$ArchLabel",
    "/DArchIdentifier=$InnoArchIdentifier",
    "/DArchInstallMode=$InnoArchInstallMode",
    "/O$OutputDir",
    $iss
  )
  & "$IsccPath" @cmdArgs | Tee-Object -FilePath (Join-Path $LogsDir "installer_full_${ArchLabel}.log")
  Write-Log "Full installer built. Output: $OutputDir"
}

#
# 函数: Build-InstallerSlim
# 说明: 使用 Inno Setup 构建精简安装包（不含运行时）
# 参数: [string] $IsccPath, [string] $PublishDir, [string] $OutputDir
# 返回: 无
#
function Build-InstallerSlim {
  param(
    [string]$IsccPath,
    [string]$PublishDir,
    [string]$OutputDir,
    [string]$ArchLabel,
    [string]$InnoArchIdentifier,
    [string]$InnoArchInstallMode
  )
  Ensure-Directory $OutputDir
  $iss = Join-Path $InstallerDir 'slim.iss'
  if (-not (Test-Path -LiteralPath $iss)) { throw "Missing Inno Setup script: $iss" }

  Write-Log "Building slim installer (ISCC): $iss"
  $cmdArgs = @(
    "/DMyAppVersion=$Version",
    "/DPublishDir=$PublishDir",
    "/DArchLabel=$ArchLabel",
    "/DArchIdentifier=$InnoArchIdentifier",
    "/DArchInstallMode=$InnoArchInstallMode",
    "/O$OutputDir",
    $iss
  )
  & "$IsccPath" @cmdArgs | Tee-Object -FilePath (Join-Path $LogsDir "installer_slim_${ArchLabel}.log")
  Write-Log "Slim installer built. Output: $OutputDir"
}

#
# 函数: Clear-ArtifactsDirectory
# 说明: 清空 artifacts 目录，为新的构建做准备
# 参数: [string] $Path - artifacts 目录路径
# 返回: 无
#
function Clear-ArtifactsDirectory {
  param([string]$Path)
  if (Test-Path -LiteralPath $Path) {
    Write-Log "Clearing artifacts directory: $Path"
    Remove-Item -Path $Path -Recurse -Force
  }
  Ensure-Directory $Path
}

#
# 函数: Create-ZipPackage
# 说明: 将指定目录内容打包为 ZIP 文件
# 参数: [string] $SourceDir - 源目录路径, [string] $ZipPath - 目标 ZIP 文件路径
# 返回: 无
#
function Create-ZipPackage {
  param(
    [string]$SourceDir,
    [string]$ZipPath
  )
  if (-not (Test-Path -LiteralPath $SourceDir)) {
    Write-Log "Source directory not found: $SourceDir" 'WARN'
    return
  }
  
  Write-Log "Creating ZIP package: $ZipPath"
  # 确保目标目录存在
  $zipDir = Split-Path -Parent $ZipPath
  Ensure-Directory $zipDir
  
  # 如果文件已存在，先尝试删除
  if (Test-Path -LiteralPath $ZipPath) {
    try {
      Remove-Item -Path $ZipPath -Force -ErrorAction Stop
      Write-Log "Removed existing ZIP file: $ZipPath"
    }
    catch {
      Write-Log "Failed to remove existing ZIP file: $ZipPath. Error: $($_.Exception.Message)" 'ERROR'
      throw
    }
  }
  
  # 使用 PowerShell 5.0+ 的 Compress-Archive 命令
  try {
    Compress-Archive -Path "$SourceDir\*" -DestinationPath $ZipPath -ErrorAction Stop
    Write-Log "ZIP package created successfully: $ZipPath"
  }
  catch {
    Write-Log "Failed to create ZIP package: $ZipPath. Error: $($_.Exception.Message)" 'ERROR'
    throw
  }
}

#
# 函数: Create-FdeMultiZipPackages
# 说明: 将 fde-multi 目录下的各个子文件夹分别打包为压缩包
# 参数: [string] $FdeMultiDir - fde-multi 根目录路径, [string] $OutputDir - 输出目录, [string] $Version - 版本号
# 返回: 无
#
function Create-FdeMultiZipPackages {
  param(
    [string]$FdeMultiDir,
    [string]$OutputDir,
    [string]$Version
  )
  if (-not (Test-Path -LiteralPath $FdeMultiDir)) {
    Write-Log "FDE multi directory not found: $FdeMultiDir" 'WARN'
    return
  }
  
  Write-Log "Creating FDE multi ZIP packages from: $FdeMultiDir"
  
  # 获取所有子文件夹
  $subDirs = Get-ChildItem -Path $FdeMultiDir -Directory
  foreach ($subDir in $subDirs) {
    $archLabel = Get-ArchLabel -Rid $subDir.Name
    $zipFileName = "NetworkAdapterHelper_Slim_${archLabel}_${Version}.zip"
    $zipPath = Join-Path $OutputDir $zipFileName
    
    Write-Log "Creating ZIP for architecture: $($subDir.Name) -> $zipFileName"
    Create-ZipPackage -SourceDir $subDir.FullName -ZipPath $zipPath
  }
}

#
# 函数: Create-ScMultiZipPackages
# 说明: 将 sc-multi 目录下的各个子文件夹分别打包为压缩包
# 参数: [string] $ScMultiDir - sc-multi 根目录路径, [string] $OutputDir - 输出目录, [string] $Version - 版本号
# 返回: 无
#
function Create-ScMultiZipPackages {
  param(
    [string]$ScMultiDir,
    [string]$OutputDir,
    [string]$Version
  )
  if (-not (Test-Path -LiteralPath $ScMultiDir)) {
    Write-Log "SC multi directory not found: $ScMultiDir" 'WARN'
    return
  }
  
  Write-Log "Creating SC multi ZIP packages from: $ScMultiDir"
  
  # 获取所有子文件夹
  $subDirs = Get-ChildItem -Path $ScMultiDir -Directory
  foreach ($subDir in $subDirs) {
    $archLabel = Get-ArchLabel -Rid $subDir.Name
    $zipFileName = "NetworkAdapterHelper_Full_${archLabel}_${Version}.zip"
    $zipPath = Join-Path $OutputDir $zipFileName
    
    Write-Log "Creating ZIP for architecture: $($subDir.Name) -> $zipFileName"
    Create-ZipPackage -SourceDir $subDir.FullName -ZipPath $zipPath
  }
}

#
# 函数: Organize-ReleaseFiles
# 说明: 整理发布文件，统一命名规范并移动到 artifacts 根目录
# 参数: [string] $Version - 版本号
# 返回: 无
#
function Organize-ReleaseFiles {
  param(
    [string]$Version
  )
  Write-Log "Organizing release files with unified naming convention"
  
  # 1. 创建精简版压缩包 (fde-multi 目录下的所有架构)
  $fdeMultiDir = Join-Path $ArtifactsDir "fde-multi"
  Create-FdeMultiZipPackages -FdeMultiDir $fdeMultiDir -OutputDir $ArtifactsDir -Version $Version
  
  # 2. 创建完整版压缩包 (sc-multi 目录下的所有架构)
  $scMultiDir = Join-Path $ArtifactsDir "sc-multi"
  Create-ScMultiZipPackages -ScMultiDir $scMultiDir -OutputDir $ArtifactsDir -Version $Version
  
  # 3. 安装包文件已经直接输出到 artifacts 根目录，无需移动
  Write-Log "Installer files are already in artifacts root directory"
  
  # 4. 清理临时构建目录
  $tempDirs = @(
    "sc-multi",
    "single-file-framework-dependent", 
    "fde-multi"
  )
  
  foreach ($tempDir in $tempDirs) {
    $tempDirPath = Join-Path $ArtifactsDir $tempDir
    if (Test-Path -LiteralPath $tempDirPath) {
      Write-Log "Cleaning up temporary directory: $tempDir"
      Remove-Item -Path $tempDirPath -Recurse -Force
    }
  }
  
  Write-Log "Release files organization completed"
  Write-Log "All release files are now available in: $ArtifactsDir"
}

# 主流程
Write-Log "Init build environment"
Clear-ArtifactsDirectory $ArtifactsDir
Ensure-Directory $LogsDir

# 记录整体构建过程
$transcriptPath = Join-Path $LogsDir ("build_release_" + $Version + ".log")
Start-Transcript -Path $transcriptPath -Append | Out-Null

try {
  # 校验 dotnet（只需要检查一次）
  Write-Log "Checking .NET SDK"
  $dotnet = Get-Command dotnet -ErrorAction Stop
  Write-Log "dotnet: $($dotnet.Source)"

  # 查找 ISCC.exe（只需要检查一次）
  Write-Log "Checking Inno Setup Compiler (ISCC.exe)"
  $isccPath = Find-InnoCompiler
  if ([string]::IsNullOrWhiteSpace($isccPath)) {
    throw "ISCC.exe not found. Please install Inno Setup 6 and ensure ISCC is available."
  }
  Write-Log ("ISCC: " + $isccPath)

# 循环处理每个运行时标识符
foreach ($CurrentRuntimeIdentifier in $RuntimeIdentifiers) {
  Write-Log "========================================" 'INFO'
  Write-Log "Building for platform: $CurrentRuntimeIdentifier" 'INFO'
  Write-Log "========================================" 'INFO'
  
  # 重新定义当前平台的路径变量
  $PublishSelfContainedMultiDir = Join-Path $ArtifactsDir "sc-multi\$CurrentRuntimeIdentifier"
  $PublishFrameworkDependentDir = Join-Path $ArtifactsDir "single-file-framework-dependent\$CurrentRuntimeIdentifier"
  $PublishFrameworkDependentMultiDir = Join-Path $ArtifactsDir "fde-multi\$CurrentRuntimeIdentifier"
  
  # 计算架构相关变量
  $ArchLabel = Get-ArchLabel -Rid $CurrentRuntimeIdentifier
  $InnoArchIdentifier = Get-InnoArchIdentifier -Rid $CurrentRuntimeIdentifier
  $InnoArchInstallMode = Get-InnoArchInstallMode -Rid $CurrentRuntimeIdentifier

  Write-Log "DEBUG: Before fallback - ArchLabel='$ArchLabel', InnoArchIdentifier='$InnoArchIdentifier', InnoArchInstallMode='$InnoArchInstallMode'"

  # 防御性兜底，避免空字符串覆盖 Inno 默认值
  if ([string]::IsNullOrWhiteSpace($ArchLabel)) { 
    Write-Log "DEBUG: ArchLabel was empty, setting to x64"
    $ArchLabel = 'x64' 
  }
  if ([string]::IsNullOrWhiteSpace($InnoArchIdentifier)) { 
    Write-Log "DEBUG: InnoArchIdentifier was empty, setting to x64compatible"
    $InnoArchIdentifier = 'x64compatible' 
  }

  Write-Log "Architecture: $CurrentRuntimeIdentifier -> Label=$ArchLabel, Identifier=$InnoArchIdentifier, InstallMode=$InnoArchInstallMode"

  try {
    # 发布自包含多文件版本（Full版本）
    Invoke-DotnetPublish -SelfContained:$true -OutputDir $PublishSelfContainedMultiDir -RuntimeId $CurrentRuntimeIdentifier -SingleFile:$false
    # 发布 FDE 多文件版本（Slim版本）
    Invoke-DotnetPublish -SelfContained:$false -OutputDir $PublishFrameworkDependentMultiDir -RuntimeId $CurrentRuntimeIdentifier -SingleFile:$false

    try {
      Build-InstallerFull -IsccPath $isccPath -PublishDir $PublishSelfContainedMultiDir -OutputDir $InstallersOutputDir -ArchLabel $ArchLabel -InnoArchIdentifier $InnoArchIdentifier -InnoArchInstallMode $InnoArchInstallMode
    }
    catch {
      Write-Log ("Skip full installer for ${CurrentRuntimeIdentifier}: " + $_.Exception.Message) 'WARN'
    }

    Build-InstallerSlim -IsccPath $isccPath -PublishDir $PublishFrameworkDependentMultiDir -OutputDir $InstallersOutputDir -ArchLabel $ArchLabel -InnoArchIdentifier $InnoArchIdentifier -InnoArchInstallMode $InnoArchInstallMode

    Write-Log "Build completed for platform: $CurrentRuntimeIdentifier" 'INFO'
  }
  catch {
    Write-Log ("Build failed for platform ${CurrentRuntimeIdentifier}: " + $_.Exception.Message) 'ERROR'
    # 继续处理下一个平台，不退出整个脚本
  }
}

  # 在所有平台构建完成后，整理发布文件
  Write-Log "All platforms processed. Organizing release files..." 'INFO'
  # 整理发布文件，统一命名规范并移动到 artifacts 根目录
  Organize-ReleaseFiles -Version $Version
  Write-Log "All build steps completed successfully" 'INFO'
}
catch {
  Write-Log $_.Exception.Message 'ERROR'
  Write-Log "Build failed. See log: $transcriptPath" 'ERROR'
  exit 1
}
finally {
  Stop-Transcript | Out-Null
  Write-Log "Log file: $transcriptPath"
}