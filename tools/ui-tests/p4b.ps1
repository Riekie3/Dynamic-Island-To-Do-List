# Phase 4: reminders, recovery from posted power/display messages, TaskbarCreated, tray menu and clicks via the
# NotifyIcon's own callback message (no mouse, no Explorer restart, nothing sent outside TaskIsland).
$sp = $PSScriptRoot
. "$sp\uia.ps1"
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" }
}
function Log { Get-Content "$root\Logs\taskisland-$(Get-Date -Format yyyyMMdd).log" -Raw -ErrorAction SilentlyContinue }
function Expanded { (Find-TI 'Add a new task' 'Edit') -ne $null }
function Db([string]$sql) {
    python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print('|'.join(str(x) for r in c.execute(sys.argv[2]).fetchall() for x in r))" "$root\Data\taskisland.db" $sql
}
Add-Type -Namespace P4 -Name W -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr h, uint m, System.IntPtr w, System.IntPtr l);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string s);
public delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, System.IntPtr l);
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(System.IntPtr h, out int pid);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(System.IntPtr h, System.Text.StringBuilder s, int n);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(System.IntPtr h);
'@
function Island-Hwnd { [IntPtr](Get-TIWindow).Current.NativeWindowHandle }
function Island-Visible { [P4.W]::IsWindowVisible($script:hwnd) }
function WinForms-Windows {
    $list = New-Object System.Collections.Generic.List[IntPtr]
    $cb = [P4.W+EnumProc] { param($h, $l)
        $procId = 0; [void][P4.W]::GetWindowThreadProcessId($h, [ref]$procId)
        if ($procId -eq $script:TIProc.Id) {
            $sb = New-Object System.Text.StringBuilder 256; [void][P4.W]::GetClassName($h, $sb, 256)
            if ($sb.ToString() -like 'WindowsForms*') { $list.Add($h) }
        }
        $true }
    [void][P4.W]::EnumWindows($cb, [IntPtr]::Zero)
    $list
}
# What the shell sends to a NotifyIcon: WM_USER+1024 with the mouse message in lParam.
function Tray([int]$mouseMsg) { foreach ($h in (WinForms-Windows)) { [void][P4.W]::PostMessage($h, 0x800, [IntPtr]0, [IntPtr]$mouseMsg) } ; Start-Sleep -Milliseconds 700 }
function Tray-Menu([string]$item) {
    Tray 0x205   # WM_RBUTTONUP â†’ menu
    $e = Find-TI $item 'MenuItem'
    if (-not $e) { return $false }
    $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 700
    return $true
}

$root = "$env:TEMP\TaskIslandP4b"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @('--pop-seconds','3600'); Stop-TI; Start-Sleep -Milliseconds 800
python -I "$sp\seed_reminders.py" "$root\Data\taskisland.db" 45 | Out-Null
$t0 = Get-Date
$p = Start-TI $root @('--full-motion','--pop-seconds','3600')

# ---- reminders (section 7.9) ----
$log = Log
Check 'T15 startup: one combined balloon for 2 overdue tasks' ($log -match 'reminder \(Startup\): 2 task\(s\), "â° 2 tasks overdue"')
Check 'T15 startup balloon shown' ($log -match 'balloon: TaskIsland / â° 2 tasks overdue')
Start-Sleep -Milliseconds 500
Check 'T15 overdue tasks marked as reminded' ((Db "select count(*) from Tasks where ReminderShownAt is not null") -eq '2')
$wait = 47 - ((Get-Date) - $t0).TotalSeconds
if ($wait -gt 0) { Start-Sleep -Seconds ([math]::Ceiling($wait)) }
Start-Sleep -Seconds 2
$log = Log
Check 'T15 due-now reminder fires on time' ($log -match 'reminder \(Timer\): 1 task\(s\), "â° Due now: Soon task"')
Check 'T15 balloon heading is the list name' ($log -match 'balloon: Personal / â° Due now: Soon task')
Check 'T15 pill pulse played' ($log -match 'reminder pulse on the island')
Check 'T15 soon task marked once' ((Db "select count(*) from Tasks where Title='Soon task' and ReminderShownAt is not null") -eq '1')
Check 'T15 tomorrow task not reminded' ((Db "select count(*) from Tasks where Title='Tomorrow task' and ReminderShownAt is null") -eq '1')

# Balloon click (NIN_BALLOONUSERCLICK) â†’ expands with that list selected.
Tray 0x405
Start-Sleep -Milliseconds 600
Check 'T15 balloon click expands' (Expanded)
Check 'T15 balloon click selects the task list (Personal)' ((Db "select SettingValue from Settings where SettingKey='SelectedListId'") -eq '1')
# A posted click carries no foreground right (a real click does), so collapse with the tray toggle.
if (Expanded) { Tray 0x202 }
Check 'collapsed again' (-not (Expanded))

# ---- recovery (section 7.3) via posted messages to our own window ----
$script:hwnd = Island-Hwnd; $hwnd = $script:hwnd
[void][P4.W]::PostMessage($hwnd, 0x218, [IntPtr]0x12, [IntPtr]0)   # WM_POWERBROADCAST / PBT_APMRESUMEAUTOMATIC
[void][P4.W]::PostMessage($hwnd, 0x7E, [IntPtr]32, [IntPtr]0)      # WM_DISPLAYCHANGE right after (debounced together)
Start-Sleep -Seconds 2
$log = Log
Check 'R resume logged' ($log -match 'power: resume')
Check 'R one debounced recovery for resume + display change' ($log -match 'recovery start \(resume, WM_DISPLAYCHANGE\)')
Check 'R recovery done, visible' ($log -match 'recovery done: visible=True')
Start-Sleep -Seconds 6
$log = Log
Check 'R follow-ups at +2 s and +6 s' ($log -match 'recovery follow-up \+2 s' -and $log -match 'recovery follow-up \+6 s')

# Suspend while expanded â†’ collapses.
Tray 0x202   # left click â†’ expand
Check 'tray left-click expands' (Expanded)
[void][P4.W]::PostMessage($hwnd, 0x218, [IntPtr]0x4, [IntPtr]0)   # PBT_APMSUSPEND
Start-Sleep -Milliseconds 900
Check 'R suspend collapses the panel' (-not (Expanded))

# Explorer restart message â†’ tray icon re-added, topmost again.
$tc = [P4.W]::RegisterWindowMessage('TaskbarCreated')
[void][P4.W]::PostMessage($hwnd, $tc, [IntPtr]0, [IntPtr]0)
Start-Sleep -Milliseconds 800
$log = Log
Check 'R TaskbarCreated handled' ($log -match 'explorer restarted \(TaskbarCreated\)')
Check 'R tray icon added again' ($log -match 'tray icon added again')

# ---- tray menu (section 7.5) ----
Check 'tray menu: Hide' (Tray-Menu 'Hide')
Check 'Hide hides the notch' (-not (Island-Visible))
Check 'tray menu: Show' (Tray-Menu 'Show')
Check 'Show brings it back' (Island-Visible)
Check 'tray menu: Pause visibility' (Tray-Menu 'Pause visibility')
Check 'Pause hides the notch' (-not (Island-Visible))
[void][P4.W]::PostMessage($hwnd, 0x218, [IntPtr]0x12, [IntPtr]0)   # resume while paused
Start-Sleep -Seconds 2
Check 'Paused: wake does not show it' (-not (Island-Visible))
Check 'Paused: Open still opens the panel' (Tray-Menu 'Open TaskIsland')
Check 'Paused: panel visible' (Expanded)
"info  foreground after tray Open is TaskIsland: $((Get-FgTitle) -eq $p.Id)"
Tray 0x202; Start-Sleep -Milliseconds 600   # left click collapses
Check 'Paused: hidden again after collapse' (-not (Island-Visible))
Check 'tray menu: Resume visibility' (Tray-Menu 'Resume visibility')
Check 'Resume shows the notch' (Island-Visible)
Check 'tray menu: Expand' (Tray-Menu 'Expand')
Check 'Expand opens the panel' (Expanded)
Tray 0x202; Start-Sleep -Milliseconds 300
Check 'tray left-click collapses when open' (-not (Expanded))
Tray 0x203   # double-click
Check 'tray double-click opens' (Expanded)
Check 'tray menu: Exit' (Tray-Menu 'Exit')
$p.WaitForExit(5000) | Out-Null
Check 'tray Exit ends the process' ($p.HasExited)
"--- fails: $global:fails"
Stop-TI


