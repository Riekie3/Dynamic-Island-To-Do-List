# usage: memtry.ps1 "NAME=VALUE;NAME2=VALUE2"   (environment variables for the runtime), measures idle memory after 25 s. Release, empty db.
param([string]$EnvVars = '', [int]$Wait = 25, [switch]$Expand)
$sp = $PSScriptRoot
$root = "$env:TEMP\TaskIslandMem"
$exe = 'D:\Rie\Claude\TaskIsland\src\TaskIsland.App\bin\Release\net10.0-windows\TaskIsland.exe'
if (-not (Test-Path "$root\Data\taskisland.db")) { $env:TASKISLAND_ROOT = $root; $q = Start-Process $exe -PassThru; Start-Sleep 4; Stop-Process -Id $q.Id -Force; Start-Sleep 1 }
python -I "$sp\setkey.py" "$root\Data\taskisland.db" Companion Off
$env:TASKISLAND_ROOT = $root
$names = @()
foreach ($kv in ($EnvVars -split ';' | Where-Object { $_ })) { $n, $v = $kv -split '=', 2; Set-Item "env:$n" $v; $names += $n }
$p = Start-Process $exe -PassThru
Start-Sleep -Seconds $Wait
$x = Get-Process -Id $p.Id
"{0,-60} {1,6:N1} MB private {2,6:N1} MB WS" -f ($EnvVars), ($x.PrivateMemorySize64 / 1MB), ($x.WorkingSet64 / 1MB)
Stop-Process -Id $p.Id -Force
foreach ($n in $names) { Remove-Item "env:$n" }
Start-Sleep 1
