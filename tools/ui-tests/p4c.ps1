# Phase 4: database cannot be opened at start-up (section 7.10). Damaged file â†’ dialog â†’ Exit / Open data folder /
# Restore from backup; a locked file â†’ the same dialog. No mouse.
$sp = $PSScriptRoot
. "$sp\uia.ps1"
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" }
}
function Log { Get-Content "$root\Logs\taskisland-$(Get-Date -Format yyyyMMdd).log" -Raw -ErrorAction SilentlyContinue }
function Db([string]$sql) {
    python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print('|'.join(str(x) for r in c.execute(sys.argv[2]).fetchall() for x in r))" "$root\Data\taskisland.db" $sql
}
function Wait-Find([string]$name, [string]$type, [int]$seconds = 10) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $seconds) { $e = Find-TI $name $type; if ($e) { return $e }; Start-Sleep -Milliseconds 300 }
}
function Restore-Btn([switch]$Wait) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    do { $e = Find-TILike 'Restore from backup*' | Select-Object -First 1; if ($e -or -not $Wait) { return $e }; Start-Sleep -Milliseconds 300 } while ($sw.Elapsed.TotalSeconds -lt 10)
}
function Press($e) {
    if (-not $e) { return }
    $ip = $null
    if ($e.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$ip)) { $ip.Invoke() }
    else {
        # Task dialog buttons (CCPushButton) expose no Invoke pattern: focus + Space (Enter presses the default button), only while TaskIsland is in front.
        $e.SetFocus(); Start-Sleep -Milliseconds 200
        if ((Get-FgTitle) -eq $script:TIProc.Id) { [System.Windows.Forms.SendKeys]::SendWait(' ') } else { "info  not foreground, Enter not sent" }
    }
    Start-Sleep -Milliseconds 800
}

$exe = "D:\Rie\Claude\TaskIsland\src\TaskIsland.App\bin\Debug\net10.0-windows\TaskIsland.exe"
$root = "$env:TEMP\TaskIslandP4c"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }

# A good database with one task, and a backup of it.
$p = Start-TI $root @('--pop-seconds','3600')
Stop-TI; Start-Sleep -Milliseconds 800
if ($true) {
    New-Item "$root\Backups" -ItemType Directory -Force | Out-Null
    python -I "$sp\make_backup.py" "$root\Data\taskisland.db" "$root\Backups\manual-good.db"
}
Check 'setup: backup with the task exists' (Test-Path "$root\Backups\manual-good.db")

# Damage the live database.
Remove-Item "$root\Data\taskisland.db-wal", "$root\Data\taskisland.db-shm" -ErrorAction SilentlyContinue
$garbage = 'this file is damaged and is not a database any more ' * 20
Set-Content "$root\Data\taskisland.db" $garbage -NoNewline
$hashBefore = (Get-FileHash "$root\Data\taskisland.db").Hash

# 1) Exit: process ends (code 1), nothing changed.
$p = Start-TI $root @()
$heading = Wait-Find "TaskIsland can't open your tasks" 'Text'
Check 'E1 dialog appears for a damaged database' ($heading -ne $null)
Start-Sleep -Milliseconds 500
Check 'E1 three choices' ((Find-TI 'Open data folder' 'Pane') -and (Restore-Btn) -and (Find-TI 'Exit' 'Pane'))
Check 'E1 no tray icon / island while the dialog is up' ($null -eq (Find-TI 'Add a new task' 'Edit'))
Press (Find-TI 'Exit' 'Pane')
$p.WaitForExit(5000) | Out-Null
Check 'E1 Exit ends the process' ($p.HasExited)
Check 'E1 exit code 1' ($p.ExitCode -eq 1) "$($p.ExitCode)"
Check 'E1 damaged file untouched' ((Get-FileHash "$root\Data\taskisland.db").Hash -eq $hashBefore)
Check 'E1 log names the failure' ((Log) -match 'database could not be opened at start-up')

# 2) Open data folder: Explorer opens on the data folder, the dialog comes back.
$p = Start-TI $root @()
Press (Wait-Find 'Open data folder' 'Pane')
Start-Sleep -Seconds 2
$explorer = $script:AE::RootElement.FindAll($script:Tree::Children, [System.Windows.Automation.Condition]::TrueCondition) |
    Where-Object { $_.Current.ClassName -eq 'CabinetWClass' -and $_.Current.Name -like '*TaskIslandP4c*' } | Select-Object -First 1
Check 'E2 Explorer opened on the data folder' ($explorer -ne $null)
if ($explorer) { $explorer.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() }
Check 'E2 the dialog is shown again' ((Restore-Btn -Wait) -ne $null)

# 3) Restore from backup: pick the file, the broken file is kept, the app starts with the backup.
Press (Restore-Btn)
Start-Sleep -Seconds 1
$picker = Wait-Find 'Choose a TaskIsland backup' 'Window'
Check 'E3 file dialog opens' ($picker -ne $null)
if ($picker -and (Get-FgTitle) -eq $p.Id) {
    [System.Windows.Forms.SendKeys]::SendWait('%n'); Start-Sleep -Milliseconds 400
    [System.Windows.Forms.SendKeys]::SendWait("$root\Backups\manual-good.db"); Start-Sleep -Milliseconds 300
    if ((Get-FgTitle) -eq $p.Id) { [System.Windows.Forms.SendKeys]::SendWait('{ENTER}') }
    Start-Sleep -Seconds 2
}
$ok = Wait-Find 'OK' 'Pane'
Check 'E3 "Backup restored" message' ((Find-TILike 'Backup restored').Count -gt 0)
if ($ok) { Press $ok }
Start-Sleep -Seconds 3
Check 'E3 app is running after the restore' (-not $p.HasExited)
Check 'E3 restored task is there' ((Db "select count(*) from Tasks where Title='Kept task'") -eq '1')
$broken = Get-ChildItem "$root\Backups" -Filter 'broken-*.db' | Select-Object -First 1
Check 'E3 damaged file kept in Backups' ($broken -ne $null -and (Get-Content $broken.FullName -Raw) -eq $garbage)
Stop-TI; Start-Sleep -Milliseconds 800

# 4) Locked database: another program holds an exclusive lock â†’ the same dialog (after SQLite's wait).
$lock = Start-Process python -ArgumentList '-I', '-c', "`"import sqlite3,sys,time; c=sqlite3.connect(sys.argv[1], isolation_level=None); c.execute('PRAGMA locking_mode=EXCLUSIVE'); c.execute('BEGIN EXCLUSIVE'); time.sleep(90)`"", "`"$root\Data\taskisland.db`"" -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-TI $root @()
$d = Wait-Find "TaskIsland can't open your tasks" 'Text' 60
Check 'E4 locked database shows the dialog' ($d -ne $null) "after $([int]$sw.Elapsed.TotalSeconds) s"
"info  dialog after about $([int]$sw.Elapsed.TotalSeconds) s"
if ($d) { Press (Find-TI 'Exit' 'Pane') }
Stop-Process -Id $lock.Id -Force -ErrorAction SilentlyContinue
Stop-TI
"--- fails: $global:fails"


