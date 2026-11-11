# Phase 5: the pill alert setting applies live, and the alert is skipped while the panel is open or the notch is hidden.
# Takes about 2.5 minutes. Tasks: A +30 s (pill on), B +75 s (pill switched off in Settings), C +110 s (pill on, panel open),
# D +140 s (pill on, notch hidden).
param([string]$Config = 'Debug')
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
Add-Type -Namespace P5 -Name W -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr h, uint m, System.IntPtr w, System.IntPtr l);
public delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, System.IntPtr l);
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(System.IntPtr h, out int pid);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(System.IntPtr h, System.Text.StringBuilder s, int n);
'@
function WinForms-Windows {
    $list = New-Object System.Collections.Generic.List[IntPtr]
    $cb = [P5.W+EnumProc] { param($h, $l)
        $procId = 0; [void][P5.W]::GetWindowThreadProcessId($h, [ref]$procId)
        if ($procId -eq $script:TIProc.Id) {
            $sb = New-Object System.Text.StringBuilder 256; [void][P5.W]::GetClassName($h, $sb, 256)
            if ($sb.ToString() -like 'WindowsForms*') { $list.Add($h) }
        }
        $true }
    [void][P5.W]::EnumWindows($cb, [IntPtr]::Zero)
    $list
}
function Tray([int]$mouseMsg) { foreach ($h in (WinForms-Windows)) { [void][P5.W]::PostMessage($h, 0x800, [IntPtr]0, [IntPtr]$mouseMsg) } ; Start-Sleep -Milliseconds 700 }
function Tray-Menu([string]$like) {
    Tray 0x205
    $e = Get-TIAll | Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.MenuItem' -and $_.Current.Name -like $like } | Select-Object -First 1
    if (-not $e) { return $false }
    $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 900
    return $true
}
function Pill-Toggle([bool]$on) {
    [void](Tray-Menu 'Settings*')
    $nav = Find-TI 'Behaviour' 'RadioButton'
    $nav.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 500
    $e = Find-TI 'Show reminders as a card on the island' 'CheckBox'
    $t = $e.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if (($t.Current.ToggleState -eq 'On') -ne $on) { $t.Toggle(); Start-Sleep -Milliseconds 600 }
    $w = Find-TI 'TaskIsland Settings' 'Window'
    $w.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close(); Start-Sleep -Milliseconds 800
}
function Island-Expanded { (Find-TI 'Add a new task' 'Edit') -ne $null }

$dbg = "$env:TEMP\TaskIsland-debug.log"
$dbgLines = if (Test-Path $dbg) { @(Get-Content $dbg).Count } else { 0 }
function Dbg { @(Get-Content $dbg -ErrorAction SilentlyContinue | Select-Object -Skip $dbgLines) -join "`n" }
$root = "$env:TEMP\TaskIslandP5c"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @('--pop-seconds','3600') $Config; Stop-TI; Start-Sleep -Milliseconds 800
$seed = Get-Date
python -I "$sp\seed_p5.py" "$root\Data\taskisland.db" - - '1:Task A:30' '1:Task B:75' '2:Task C:110' '1:Task D:140' | Out-Null
$p = Start-TI $root @('--full-motion','--pop-seconds','3600') $Config

# A: pill on (default)
WaitUntil $seed.AddSeconds(37)
$log = Log
Check 'A: pill alert shown for a task due now' ($log -match 'reminder pill alert: ".*Task A" toast \w+ "')
Check 'A: the toast tab says Due now' ($log -match 'toast Info "Due now"')
Check 'A: notification still shown' ($log -match 'balloon: Personal / .*Due now: Task A')
Start-Sleep -Seconds 6
Check 'A: alert ended after 5 s' ((Count (Log) 'reminder pill alert ended') -eq 1)
Check 'A: the pill took the notch, so no Surprised face on the island' (-not ((Dbg) -match 'reaction Surprised onPanel=False'))

# switch the pill off in Settings: B must use the pulse + face + notification only
Pill-Toggle $false
Check 'the setting is saved' ((Db "select SettingValue from Settings where SettingKey='ReminderPillAlert'") -eq 'false')
WaitUntil $seed.AddSeconds(82)
$log = Log
Check 'B: reminder fired' ($log -match 'reminder \(Timer\): 1 task\(s\), ".*Due now: Task B"')
Check 'B: no pill alert with the setting off' (-not ($log -match 'reminder pill alert: ".*Task B"'))
Check 'B: the pulse ring still plays' ((Count $log 'reminder pulse on the island') -ge 1)
Check 'B: the Surprised face shows on the island' ((Dbg) -match 'reaction Surprised onPanel=False')
Check 'B: notification still shown' ($log -match 'balloon: Personal / .*Due now: Task B')

# C: pill on again, but the panel is open -> skipped, the face shows on the panel
Pill-Toggle $true
Tray 0x203; Start-Sleep -Milliseconds 700
Check 'panel is open before C' (Island-Expanded)
WaitUntil $seed.AddSeconds(117)
$log = Log
Check 'C: reminder fired while the panel was open' ($log -match 'reminder \(Timer\): 1 task\(s\), ".*Due now: Task C"')
Check 'C: no pill alert while the panel is open' (-not ($log -match 'reminder pill alert: ".*Task C"'))
Check 'C: the Surprised face shows on the panel' ((Dbg) -match 'reaction Surprised onPanel=True')
Check 'C: notification still shown' ($log -match 'balloon: Office Work / .*Due now: Task C')
Tray 0x202; Tray 0x202   # WinForms swallows the first button-up after a double-click
Check 'panel collapsed' (-not (Island-Expanded))

# D: the notch is hidden -> skipped, notification still shown
[void](Tray-Menu 'Hide')
WaitUntil $seed.AddSeconds(147)
$log = Log
Check 'D: reminder fired while the notch was hidden' ($log -match 'reminder \(Timer\): 1 task\(s\), ".*Due now: Task D"')
Check 'D: no pill alert while hidden' (-not ($log -match 'reminder pill alert: ".*Task D"'))
Check 'D: notification still shown' ($log -match 'balloon: Personal / .*Due now: Task D')
Check 'all four reminders marked once' ((Db "select count(*) from Tasks where ReminderShownAt is not null") -eq '4')
Check 'no unhandled exception in the log' (-not ($log -match 'unhandled exception'))
Stop-TI
"p5c done: $global:fails failure(s)"
