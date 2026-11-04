# Phase 4: full-screen detection (section 7.4) with our own test windows on the primary monitor. No mouse.
# Covers the screen for a few seconds: run only with the user's OK.
$sp = $PSScriptRoot
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class FsW {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr h, bool altTab);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
}
'@
[FsW]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null   # physical pixels, like TaskIsland
. "$sp\uia.ps1"
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" }
}
function Log { Get-Content "$root\Logs\taskisland-$(Get-Date -Format yyyyMMdd).log" -Raw -ErrorAction SilentlyContinue }
function Pump([int]$ms) { $sw = [Diagnostics.Stopwatch]::StartNew(); while ($sw.ElapsedMilliseconds -lt $ms) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 30 } }
# Bring a test window to the front (a full-screen app is always the foreground window). Returns whether it worked.
function Front($f) {
    for ($i = 0; $i -lt 4 -and [FsW]::GetForegroundWindow() -ne $f.Handle; $i++) {
        if ($i -eq 0) { [FsW]::SetForegroundWindow($f.Handle) | Out-Null }
        elseif ($i -eq 1) { [FsW]::SwitchToThisWindow($f.Handle, $true) }
        else { [FsW]::ShowWindow($f.Handle, 6) | Out-Null; Pump 200; [FsW]::ShowWindow($f.Handle, 9) | Out-Null }
        Pump 300
    }
    $ok = [FsW]::GetForegroundWindow() -eq $f.Handle
    if (-not $ok) { "info  could not bring the test window to the front" }
    return $ok
}
function Visible { [FsW]::IsWindowVisible($script:hwnd) }
function New-Form([string]$title) {
    $f = New-Object System.Windows.Forms.Form
    $f.Text = $title; $f.BackColor = [System.Drawing.Color]::Black; $f.StartPosition = 'Manual'; $f.ShowInTaskbar = $false
    $f
}

$root = "$env:TEMP\TaskIslandP4d"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @('--pop-seconds','3600')
$script:hwnd = [IntPtr](Get-TIWindow).Current.NativeWindowHandle
$scr = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Check 'notch visible at start' (Visible)

# 1) Borderless window covering the whole primary monitor (a video player) → hides; closing it → back.
$f = New-Form 'TaskIsland full-screen test'
$f.FormBorderStyle = 'None'; $f.Bounds = $scr
$f.Show(); $front = Front $f; Pump 1500
if ($front -eq $true -or $front -contains $true) { Check 'F1 full-screen window hides the notch' (-not (Visible)) } else { $global:fails++; 'FAIL  F1 not run: no foreground' }
$f.Close(); $f.Dispose(); Pump 1200
Check 'F1 notch comes back when it closes' (Visible)

# 2) A maximised normal window is not full screen.
$f = New-Form 'TaskIsland maximised test'
$f.WindowState = 'Maximized'
$f.Show(); $front = Front $f; Pump 1500
Check 'F2 maximised window keeps the notch' (Visible)

# 3) F11 style: the same foreground window turns borderless and covers the screen (location change only).
$f.WindowState = 'Normal'; Pump 300
$f.FormBorderStyle = 'None'; $f.Bounds = $scr; Pump 1500
Check 'F3 going full screen without a foreground change hides the notch' (-not (Visible))
$f.FormBorderStyle = 'Sizable'; $f.Bounds = New-Object System.Drawing.Rectangle(200, 200, 800, 500); Pump 1500
Check 'F3 leaving full screen brings it back' (Visible)
$f.Close(); $f.Dispose(); Pump 500

$log = Log
Check 'F log: full-screen and back' ($log -match 'full-screen app: notch hidden' -and $log -match 'full-screen ended: notch back')
Check 'F companion paused while hidden (log shows the check)' ($log -match 'full-screen check: full-screen')

# 4) HideInFullScreen off → stays visible.
Stop-TI; Start-Sleep -Milliseconds 800
python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); c.execute(sys.argv[2]); c.commit()" "$root\Data\taskisland.db" "insert or replace into Settings(SettingKey,SettingValue) values('HideInFullScreen','false')"
$p = Start-TI $root @('--pop-seconds','3600')
$script:hwnd = [IntPtr](Get-TIWindow).Current.NativeWindowHandle
$f = New-Form 'TaskIsland full-screen test'
$f.FormBorderStyle = 'None'; $f.Bounds = $scr
$f.Show(); $front = Front $f; Pump 1500
Check 'F4 HideInFullScreen off: notch stays' (Visible)
$f.Close(); $f.Dispose()
Stop-TI
"--- fails: $global:fails"
