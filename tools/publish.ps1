# Builds the portable release: a self-contained, ReadyToRun win-x64 folder plus a zip of it.
# Usage: powershell -File tools\publish.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$out = Join-Path $root 'publish\win-x64'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
$hash = (git rev-parse --short HEAD).Trim()
dotnet publish src/TaskIsland.App -c Release -r win-x64 --self-contained true `
    -p:PublishReadyToRun=true -p:DebugType=none -p:DebugSymbols=false -p:SourceRevisionId=$hash `
    -o $out
if ($LASTEXITCODE -ne 0) { throw "publish failed" }
$exe = Join-Path $out 'TaskIsland.exe'
$ver = (Get-Item $exe).VersionInfo.ProductVersion.Split('+')[0]
$zip = Join-Path $root "publish\TaskIsland-$ver-win-x64-portable.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Copy-Item (Join-Path $root 'LICENSE') $out -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root 'docs\PORTABLE.txt') (Join-Path $out 'README.txt') -ErrorAction SilentlyContinue
Compress-Archive -Path "$out\*" -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Published $out"
Write-Host "Zip: $zip ($([math]::Round((Get-Item $zip).Length/1MB,1)) MB)"
