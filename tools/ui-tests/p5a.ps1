# Phase 5: early reminder (lead 5 min) and the widening pill alert. Takes about 6 minutes (real time).
# Timeline from the seed: "Inside window" due +180 s (its early moment is already past: marked quietly),
# "Early one" due +335 s (early at +35 s), "Date only" due tomorrow (no early reminder).
$sp = $PSScriptRoot
. "$sp\uia.ps1"
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" }
}
function Log { Get-Content "$root\Logs\taskisland-$(Get-Date -Format yyyyMMdd).log" -Raw -ErrorAction SilentlyContinue }
function Count([string]$text, [string]$pattern) { ([regex]::Matches($text, $pattern)).Count }
function Db([string]$sql) {
    python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print('|'.join(str(x) for r in c.execute(sys.argv[2]).fetchall() for x in r))" "$root\Data\taskisland.db" $sql
}
function WaitUntil([datetime]$t) { $s = ($t - (Get-Date)).TotalSeconds; if ($s -gt 0) { Start-Sleep -Milliseconds ([int]($s * 1000)) } }

$dbg = "$env:TEMP\TaskIsland-debug.log"
$dbgLines = if (Test-Path $dbg) { @(Get-Content $dbg).Count } else { 0 }
$root = "$env:TEMP\TaskIslandP5a"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @('--pop-seconds','3600'); Stop-TI; Start-Sleep -Milliseconds 800
$seed = Get-Date
python -I "$sp\seed_p5.py" "$root\Data\taskisland.db" 5 true '1:Inside window:180' '1:Early one:335' '2:Date only:1:date' | Out-Null
$p = Start-TI $root @('--full-motion','--pop-seconds','3600')

# ---- start-up: the task already inside the 5-minute window is marked quietly ----
$log = Log
Check 'early (Startup): task inside the window marked without a notification' ($log -match 'early reminders \(Startup\): 1 already inside the 5-minute window, marked without a notification')
Check 'no early notification at start-up' ((Count $log 'early reminder \(') -eq 0)
Check 'inside-window task has its early mark' ((Db "select count(*) from Tasks where Title='Inside window' and EarlyReminderShownAt is not null") -eq '1')
Check 'early task not marked yet' ((Db "select count(*) from Tasks where Title='Early one' and EarlyReminderShownAt is null") -eq '1')
Check 'date-only task has no early reminder' ((Db "select count(*) from Tasks where Title='Date only' and EarlyReminderShownAt is null") -eq '1')
Check 'no pill alert yet' ((Count $log 'reminder pill alert:') -eq 0)

# ---- the early reminder of "Early one" (+35 s) ----
WaitUntil $seed.AddSeconds(36.5)
# (The island's automation name is not exposed in the UI Automation tree by this scanner at all, so it is not checked here.)
$log = Log
Check 'early reminder fires once on time' ((Count $log 'early reminder \(Timer\): 1 task\(s\), ".*In 5 minutes: Early one"') -eq 1)
Check 'pill alert shows the early text' ($log -match 'reminder pill alert: ".*In 5 minutes: Early one" width \d+')
Check 'pulse after the pill is wide' ($log -match 'reminder pulse on the island')
Check 'balloon heading is the list name' ($log -match 'balloon: Personal / .*In 5 minutes: Early one')
Check 'early task is marked in the database' ((Db "select count(*) from Tasks where Title='Early one' and EarlyReminderShownAt is not null and ReminderShownAt is null") -eq '1')
$dbgNew = @(Get-Content $dbg -ErrorAction SilentlyContinue | Select-Object -Skip $dbgLines) -join "`n"
Check 'no Surprised face for an early reminder' (-not ($dbgNew -match 'reaction Surprised'))
Start-Sleep -Seconds 6
$log = Log
Check 'pill alert ended after 5 s' ($log -match 'reminder pill alert ended')

# ---- restart in the window: the early reminder must not repeat ----
$before = Count (Log) 'early reminder \('
Stop-TI; Start-Sleep -Milliseconds 800
$p = Start-TI $root @('--full-motion','--pop-seconds','3600')
$log = Log
Check 'restart: no second early notification' ((Count $log 'early reminder \(') -eq $before)
Check 'restart: still exactly one early mark per task' ((Db "select count(*) from Tasks where EarlyReminderShownAt is not null") -eq '2')

# ---- "Due now" of "Inside window" (+180 s) ----
WaitUntil $seed.AddSeconds(186)
$log = Log
Check 'due-now fires for the task that got no early reminder' ((Count $log 'reminder \(Timer\): 1 task\(s\), ".*Due now: Inside window"') -eq 1)
Check 'due-now pill shows just the title' ($log -match 'reminder pill alert: ".*Inside window" width \d+' -and -not ($log -match 'reminder pill alert: ".*Due now'))
Check 'due-now marked in the database' ((Db "select count(*) from Tasks where Title='Inside window' and ReminderShownAt is not null") -eq '1')
Start-Sleep -Seconds 6

# ---- "Due now" of "Early one" (+335 s): early and due each exactly once ----
WaitUntil $seed.AddSeconds(342)
$log = Log
Check 'due-now fires for the early task too' ((Count $log 'reminder \(Timer\): 1 task\(s\), ".*Due now: Early one"') -eq 1)
Check 'early task: exactly one early + one due notification' (((Count $log 'early reminder \(Timer\).*Early one') -eq 1) -and ((Count $log 'reminder \(Timer\): 1 task\(s\), ".*Due now: Early one"') -eq 1))
Check 'date-only task never reminded' ((Db "select count(*) from Tasks where Title='Date only' and ReminderShownAt is null and EarlyReminderShownAt is null") -eq '1')
Check 'no unhandled exception in the log' (-not ($log -match 'unhandled exception'))
Stop-TI
"p5a done: $global:fails failure(s)"
