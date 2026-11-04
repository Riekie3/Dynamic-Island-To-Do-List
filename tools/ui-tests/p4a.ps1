# Phase 4: single instance, Run key (test key), hotkey, pipe hardening, Exit path. No mouse.
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
function DbExec([string]$sql) {
    python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); c.execute(sys.argv[2]); c.commit()" "$root\Data\taskisland.db" $sql
}
function Expanded { (Find-TI 'Add a new task' 'Edit') -ne $null }
function Count-TI { @(Get-Process TaskIsland -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }).Count }
function Scope([string]$s) {
    [uint32]$h = 2166136261
    foreach ($c in $s.ToUpperInvariant().ToCharArray()) { $h = $h -bxor [uint32][char]$c; $h = [uint32](([uint64]$h * 16777619) % 4294967296) }
    '{0:x8}' -f $h
}

$exe = "D:\Rie\Claude\TaskIsland\src\TaskIsland.App\bin\Debug\net10.0-windows\TaskIsland.exe"
$root = "$env:TEMP\TaskIslandP4a"
$testKey = 'HKCU:\Software\TaskIsland-Test\Run'
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
Remove-ItemProperty $testKey -Name TaskIsland -ErrorAction SilentlyContinue
$realBefore = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name TaskIsland -ErrorAction SilentlyContinue).TaskIsland

$p = Start-TI $root @()
Check 'starts and keeps running (collapsed)' (-not $p.HasExited)
Check 'not expanded at start (StartCollapsed)' (-not (Expanded))
$log = Log
Check 'log: tray icon shown' ($log -match 'tray icon shown')
Check 'log: windows integration attached' ($log -match 'windows integration attached')
Check 'log: hotkey Ctrl\+Alt\+T registered' ($log -match 'hotkey Ctrl\+Alt\+T registered')
Check 'first run writes the startup value (test key)' ((Get-ItemProperty $testKey -Name TaskIsland -ErrorAction SilentlyContinue).TaskIsland -eq "`"$exe`" --startup")
$realAfter = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name TaskIsland -ErrorAction SilentlyContinue).TaskIsland
Check 'a test run never touches the real Run key' ($realAfter -eq $realBefore) "$realBefore -> $realAfter"
Check 'FirstRunCompleted saved' ((Db "select SettingValue from Settings where SettingKey='FirstRunCompleted'") -eq 'true')

# Launch twice: the second exits with code 0, the first expands; one process.
$env:TASKISLAND_ROOT = $root
$second = Start-Process $exe -PassThru
$second.WaitForExit(8000) | Out-Null
Check 'second launch exits' ($second.HasExited)
Check 'second launch exit code 0' ($second.ExitCode -eq 0) "$($second.ExitCode)"
Start-Sleep -Milliseconds 800
Check 'first instance expanded' (Expanded)
Check 'one TaskIsland process' ((Count-TI) -eq 1) "$(Count-TI)"
Check 'log: second launch line' ((Log) -match 'second launch: expanding')
Check 'expanded panel has the keyboard focus (foreground)' ((Get-FgTitle) -eq $p.Id)

# Hotkey: Ctrl+Alt+T collapses (sent only while TaskIsland is the foreground window).
if ((Get-FgTitle) -eq $p.Id) {
    [System.Windows.Forms.SendKeys]::SendWait('^%t'); Start-Sleep -Milliseconds 900
    Check 'Ctrl+Alt+T collapses' (-not (Expanded))
    if ((Get-FgTitle) -eq $p.Id) {
        [System.Windows.Forms.SendKeys]::SendWait('^%t'); Start-Sleep -Milliseconds 900
        Check 'Ctrl+Alt+T expands again' (Expanded)
    } else { "SKIP  Ctrl+Alt+T expand (TaskIsland lost the foreground after collapse)" }
} else { $global:fails++; "FAIL  hotkey test skipped: TaskIsland is not foreground" }

# Pipe hardening: anything but EXPAND is ignored.
$sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$pipe = "TaskIsland-7F3A9C2E-$sid-$(Scope $root)"
python -I -c "import sys; f=open(sys.argv[1],'wb'); f.write(b'HELLO-THERE-THIS-IS-TOO-LONG')
try: f.close()
except OSError: pass" "\\.\pipe\$pipe"
Start-Sleep -Milliseconds 600
Check 'pipe: other message ignored and logged' ((Log) -match 'ignored a pipe message of 17 bytes')
Check 'still running after a bad message' (-not $p.HasExited)

# Exit from the panel menu: process ends with code 0, log says so.
$more = Find-TI 'More options' 'Button'
if (-not (Expanded)) { "note: re-expanding for Exit" }
$more.SetFocus(); Start-Sleep -Milliseconds 200
[System.Windows.Forms.SendKeys]::SendWait('+{F10}'); Start-Sleep -Milliseconds 600
try { Invoke-TI 'Exit TaskIsland' 'MenuItem' } catch { "FAIL  exit menu: $_"; $global:fails++; Dump-TI | Select-Object -First 40 }
$p.WaitForExit(6000) | Out-Null
Check 'Exit ends the process' ($p.HasExited)
Check 'Exit code 0' ($p.ExitCode -eq 0) "$($p.ExitCode)"
$log = Log
Check 'log: exit requested + exited (code 0)' ($log -match 'exit requested' -and $log -match 'exited \(code 0\)')

# Startup toggled off (setting false) â†’ the value is removed at the next launch.
DbExec "insert or replace into Settings(SettingKey,SettingValue) values('LaunchAtStartup','false')"
$p = Start-TI $root @()
Check 'setting off removes the startup value' ($null -eq (Get-ItemProperty $testKey -Name TaskIsland -ErrorAction SilentlyContinue))
# Setting back on, not first run, value missing â†’ not re-added silently.
Stop-TI; Start-Sleep -Milliseconds 800
DbExec "insert or replace into Settings(SettingKey,SettingValue) values('LaunchAtStartup','true')"
$p = Start-TI $root @()
Check 'a removed value is not re-added silently' ($null -eq (Get-ItemProperty $testKey -Name TaskIsland -ErrorAction SilentlyContinue))
# An old path is fixed.
Stop-TI; Start-Sleep -Milliseconds 800
New-Item $testKey -Force | Out-Null
Set-ItemProperty $testKey -Name TaskIsland -Value '"C:\Old\TaskIsland.exe" --startup'
$p = Start-TI $root @()
Check 'an old path is rewritten' ((Get-ItemProperty $testKey -Name TaskIsland).TaskIsland -eq "`"$exe`" --startup")
Stop-TI
Remove-ItemProperty $testKey -Name TaskIsland -ErrorAction SilentlyContinue
"--- fails: $global:fails"



