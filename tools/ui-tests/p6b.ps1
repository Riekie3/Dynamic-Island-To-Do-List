# Phase 6: L1 (first run, Release), UI4 (panel stays on screen at HorizontalOffset +-400), D9 (daily backup, keep 7).
$sp = $PSScriptRoot
. "$sp\uia.ps1"
Add-Type -Namespace P6 -Name A -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr h, uint m, System.IntPtr w, System.IntPtr l);
public delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, System.IntPtr l);
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(System.IntPtr h, out int pid);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(System.IntPtr h, System.Text.StringBuilder s, int n);
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
public struct RECT { public int L, T, R, B; }
[DllImport("user32.dll")] public static extern bool GetWindowRect(System.IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(System.IntPtr h);
'@
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') { if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" } }
function Tray([int]$msg) {
    $cb = [P6.A+EnumProc] { param($h, $l)
        $x = 0; [void][P6.A]::GetWindowThreadProcessId($h, [ref]$x)
        if ($x -eq $script:TIProc.Id) { $sb = New-Object System.Text.StringBuilder 256; [void][P6.A]::GetClassName($h, $sb, 256); if ($sb.ToString() -like 'WindowsForms*') { [void][P6.A]::PostMessage($h, 0x800, [IntPtr]0, [IntPtr]$msg) } }
        $true }
    [void][P6.A]::EnumWindows($cb, [IntPtr]::Zero); Start-Sleep -Milliseconds 800
}
function Island-Rect {
    $script:best = $null
    $cb = [P6.A+EnumProc] { param($h, $l)
        $x = 0; [void][P6.A]::GetWindowThreadProcessId($h, [ref]$x)
        if ($x -eq $script:TIProc.Id -and [P6.A]::IsWindowVisible($h)) {
            $sb = New-Object System.Text.StringBuilder 256; [void][P6.A]::GetClassName($h, $sb, 256)
            if ($sb.ToString() -like 'HwndWrapper*') {
                $r = New-Object P6.A+RECT; [void][P6.A]::GetWindowRect($h, [ref]$r)
                if ($null -eq $script:best -or (($r.R - $r.L) * ($r.B - $r.T)) -gt (($script:best.R - $script:best.L) * ($script:best.B - $script:best.T))) { $script:best = $r }
            }
        }
        $true }
    [void][P6.A]::EnumWindows($cb, [IntPtr]::Zero)
    $script:best
}
function Expanded { (Find-TI 'Add a new task' 'Edit') -ne $null }
function Db([string]$db, [string]$sql) {
    python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print('|'.join(str(x) for r in c.execute(sys.argv[2]).fetchall() for x in r))" $db $sql
}
Add-Type -AssemblyName System.Windows.Forms
[void][P6.A]::SetProcessDPIAware()
$screen = [pscustomobject]@{ Left = 0; Top = 0; Right = [P6.A]::GetSystemMetrics(0); Bottom = [P6.A]::GetSystemMetrics(1); Width = [P6.A]::GetSystemMetrics(0) }

# ---------- L1: first run, Release ----------
$root = "$env:TEMP\TaskIslandP6a"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @('--pop-seconds','3600') 'Release'
Check 'L1 database has the starter lists' ((Db "$root\Data\taskisland.db" "select group_concat(Name,'+') from (select Name from Lists order by SortOrder)") -eq 'Personal+Office Work') (Db "$root\Data\taskisland.db" "select Name from Lists")
Check 'L1 no tasks' ((Db "$root\Data\taskisland.db" "select count(*) from Tasks") -eq '0')
Tray 0x203; Start-Sleep -Milliseconds 800
Check 'L1 panel opens (tray double-click)' (Expanded)
Check 'L1 both list chips shown' ((Find-TI 'Personal' 'Button') -ne $null -and (Find-TI 'Office Work' 'Button') -ne $null)
Check 'L1 no task rows' ((Get-TIRows).Count -eq 0)
Stop-TI; Start-Sleep -Milliseconds 800

# ---------- UI4: HorizontalOffset +-400 ----------
foreach ($off in -400, 400, 0) {
    python -I "$sp\setkey.py" "$root\Data\taskisland.db" HorizontalOffset $off
    $p = Start-TI $root @('--pop-seconds','3600') 'Release'
    $c = Island-Rect
    Check "UI4 offset ${off}: collapsed window inside the screen" ($c.L -ge $screen.Left -and $c.R -le $screen.Right) "L=$($c.L) R=$($c.R) screen $($screen.Left)..$($screen.Right)"
    Tray 0x203; Start-Sleep -Milliseconds 900
    Check "UI4 offset ${off}: panel open" (Expanded)
    $r = Island-Rect
    Check "UI4 offset ${off}: expanded window inside the screen" ($r.L -ge $screen.Left -and $r.R -le $screen.Right -and $r.T -ge $screen.Top -and $r.B -le $screen.Bottom) "L=$($r.L) T=$($r.T) R=$($r.R) B=$($r.B) screen $($screen.Left),$($screen.Top)..$($screen.Right),$($screen.Bottom)"
    "      (offset $off DIP, collapsed centre x = $(($c.L + $c.R)/2), expanded rect $($r.L)..$($r.R), screen width $($screen.Width))"
    Stop-TI; Start-Sleep -Milliseconds 800
}
python -I "$sp\setkey.py" "$root\Data\taskisland.db" HorizontalOffset 0

"p6b done: $global:fails failure(s)"
