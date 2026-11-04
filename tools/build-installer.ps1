# Builds installer\Output\TaskIsland-Setup-<version>-x64.exe from publish\win-x64 (run tools\publish.ps1 first).
# Usage: powershell -File tools\build-installer.ps1 [-Extra '/DName=Value', ...]   (extra ISCC defines, used by the tests)
param([string[]]$Extra = @())
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'publish\win-x64\TaskIsland.exe'
if (-not (Test-Path $exe)) { throw "publish\win-x64 is missing: run tools\publish.ps1 first" }
$info = (Get-Item $exe).VersionInfo
$ver = $info.ProductVersion.Split('+')[0]
$num = '{0}.{1}.{2}.0' -f $info.FileMajorPart, $info.FileMinorPart, $info.FileBuildPart
$iscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
if (-not (Test-Path $iscc)) { $iscc = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe' }
if (-not (Test-Path $iscc)) { throw "Inno Setup 6 is not installed (winget install JRSoftware.InnoSetup)" }
& $iscc /Q "/DAppVersion=$ver" "/DNumVersion=$num" @Extra (Join-Path $root 'installer\TaskIsland.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }
Get-ChildItem (Join-Path $root 'installer\Output') -Filter *.exe | Sort-Object LastWriteTime | Select-Object -Last 1 |
    ForEach-Object { "Installer: $($_.FullName) ($([math]::Round($_.Length/1MB,1)) MB)" }
