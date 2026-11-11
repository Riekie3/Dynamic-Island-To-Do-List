# Sileo S2: the reminder shows as a toast. Test data folder only. Takes about 1 minute.
# A: task due in ~15 s -> toast appears (tab "Due now", window grown), ends by itself after 5 s, window back to the small size,
#    the notch is visible again.
# B: second task due later -> toast shows, clicking "Open" opens the panel on that task's list and the toast is gone.
param([string]$Config = 'Debug')
$sp = $PSScriptRoot
. "$sp\uia.ps1"
Add-Type -Namespace TA -Name W -MemberDefinition @'
public delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, System.IntPtr l);
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(System.IntPtr h, out int pid);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(System.IntPtr h, System.Text.StringBuilder s, int n);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(System.IntPtr h);
public struct RECT { public int L, T, R, B; }
[DllImport("user32.dll")] public static extern bool GetWindowRect(System.IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
'@
[void][TA.W]::SetProcessDPIAware()
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') { if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" } }
function Log { Get-Content "$root\Logs\taskisland-$(Get-Date -Format yyyyMMdd).log" -Raw -ErrorAction SilentlyContinue }
function Count([string]$text, [string]$pattern) { ([regex]::Matches($text, $pattern)).Count }
function WaitUntil([datetime]$t) { $s = ($t - (Get-Date)).TotalSeconds; if ($s -gt 0) { Start-Sleep -Milliseconds ([int]($s * 1000)) } }
function Island-Height {
    $script:h = 0
    $cb = [TA.W+EnumProc] { param($hwnd, $l)
        $x = 0; [void][TA.W]::GetWindowThreadProcessId($hwnd, [ref]$x)
        if ($x -eq $script:TIProc.Id -and [TA.W]::IsWindowVisible($hwnd)) {
            $sb = New-Object System.Text.StringBuilder 256; [void][TA.W]::GetClassName($hwnd, $sb, 256)
            if ($sb.ToString() -like 'HwndWrapper*') { $r = New-Object TA.W+RECT; [void][TA.W]::GetWindowRect($hwnd, [ref]$r); if (($r.B - $r.T) -gt $script:h) { $script:h = $r.B - $r.T } }
        }
        $true }
    [void][TA.W]::EnumWindows($cb, [IntPtr]::Zero)
    $script:h
}
$root = "$env:TEMP\TaskIslandToastAlert"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @('--pop-seconds', '3600') $Config; Stop-TI; Start-Sleep -Milliseconds 800
$smallHeight = $null
$seed = Get-Date
python -W ignore -I "$sp\seed_p5.py" "$root\Data\taskisland.db" - - '1:Task A:15' '2:Task B:50' | Out-Null
$p = Start-TI $root @('--full-motion', '--pop-seconds', '3600') $Config
$smallHeight = Island-Height
"      (resting window height: $smallHeight px)"

# ---- A ----
WaitUntil $seed.AddSeconds(18)
$log = Log
Check 'A: toast shown for the task due now' ($log -match 'reminder pill alert: ".*Task A" toast Info "Due now"')
Check 'A: window grown for the body' ((Island-Height) -gt $smallHeight) "height $(Island-Height) vs $smallHeight"
Check 'A: the Open button is on screen' ((Find-TI 'Open' 'Button') -ne $null)
Check 'A: notification balloon still shown' ($log -match 'balloon: Personal / .*Due now: Task A')
Start-Sleep -Seconds 6
$log = Log
Check 'A: alert ended after 5 s' ((Count $log 'reminder pill alert ended') -eq 1)
Check 'A: window back to the small size' ((Island-Height) -eq $smallHeight) "height $(Island-Height) vs $smallHeight"
Check 'A: the Open button is gone' ((Find-TI 'Open' 'Button') -eq $null)
Check 'A: panel did not open by itself' ((Find-TI 'Add a new task' 'Edit') -eq $null)

# ---- B ----
WaitUntil $seed.AddSeconds(53)
$log = Log
Check 'B: toast shown for the second task' ($log -match 'reminder pill alert: ".*Task B" toast Info "Due now"')
Start-Sleep -Milliseconds 900
Invoke-TI 'Open' 'Button'
Start-Sleep -Milliseconds 1200
Check 'B: clicking Open opens the panel' ((Find-TI 'Add a new task' 'Edit') -ne $null)
Check 'B: the alert ended' ((Count (Log) 'reminder pill alert ended') -eq 2)
Check 'B: the Open button is gone' ((Find-TI 'Open' 'Button') -eq $null)
Check 'B: panel is on that task list (Office Work chip selected, Task B visible)' ((Get-TIRows) -contains 'Task B')
Stop-TI
"toastalert done: $global:fails failure(s)"
