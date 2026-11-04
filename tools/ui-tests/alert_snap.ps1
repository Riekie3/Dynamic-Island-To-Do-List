# Reminder pill look: a task comes due at start-up, the island widens (ReminderAlertWidthPercent) with the red border,
# and --snap saves a PNG of the island window after -Delay ms (no screen capture). Debug build only.
# usage: alert_snap.ps1 -Out <png> [-Percent 50] [-Blink true] [-Delay 2200] [-Reduced]
param([string]$Out, [string]$Percent = "50", [string]$Blink = "true", [string]$Delay = "2200", [switch]$Reduced)
$sp = $PSScriptRoot
. "$sp\uia.ps1"
$root = "$env:TEMP\TaskIslandAlertSnap"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @('--pop-seconds', '3600'); Stop-TI; Start-Sleep -Milliseconds 800
python -I "$sp\seed_p5.py" "$root\Data\taskisland.db" - true '1:Call the bank about the card:1' | Out-Null
python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); [c.execute('insert into Settings(SettingKey,SettingValue) values(?,?) on conflict(SettingKey) do update set SettingValue=excluded.SettingValue',(k,v)) for k,v in (('ReminderAlertWidthPercent',sys.argv[2]),('ReminderBlink',sys.argv[3]))]; c.commit()" "$root\Data\taskisland.db" $Percent $Blink
Start-Sleep -Seconds 2
$exe = "D:\Rie\Claude\TaskIsland\src\TaskIsland.App\bin\Debug\net10.0-windows\TaskIsland.exe"
$env:TASKISLAND_ROOT = $root
$appArgs = @("--pop-seconds", "3600", "--snap", $Out, "--snap-delay", $Delay)
if ($Reduced) { $appArgs += "--reduced-motion" }
$p = Start-Process $exe -ArgumentList $appArgs -PassThru
if (-not $p.WaitForExit(15000)) { Stop-Process -Id $p.Id -Force; 'TIMEOUT' }
Get-Content "$root\Logs\taskisland-$(Get-Date -Format yyyyMMdd).log" | Select-String 'reminder pill alert|window width|island placed|snap:'
