# Phase 4: real Explorer restart (section 7.3 TaskbarCreated). Closes File Explorer windows: run only with the user's OK.
$sp = $PSScriptRoot
. "$sp\uia.ps1"
Add-Type -Namespace P4E -Name W -MemberDefinition '[DllImport("user32.dll")] public static extern bool IsWindowVisible(System.IntPtr h); [DllImport("user32.dll")] public static extern int GetWindowLong(System.IntPtr h, int i);'
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" }
}
function Log { Get-Content "$root\Logs\taskisland-$(Get-Date -Format yyyyMMdd).log" -Raw -ErrorAction SilentlyContinue }
function Tray-Icon {
    $tray = $script:AE::RootElement.FindFirst($script:Tree::Children,
        (New-Object System.Windows.Automation.PropertyCondition($script:AE::ClassNameProperty, 'Shell_TrayWnd')))
    if (-not $tray) { return $null }
    $tray.FindAll($script:Tree::Descendants, [System.Windows.Automation.Condition]::TrueCondition) |
        Where-Object { $_.Current.Name -like 'TaskIsland*' } | Select-Object -First 1
}

$root = "$env:TEMP\TaskIslandP4e"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @('--pop-seconds','3600')
$hwnd = [IntPtr](Get-TIWindow).Current.NativeWindowHandle
$before = Tray-Icon
"info  tray icon in the taskbar before: $(if ($before) { $before.Current.Name } else { 'not found (may be in the hidden-icons flyout)' })"

Stop-Process -Name explorer -Force
$sw = [Diagnostics.Stopwatch]::StartNew()
while ($sw.Elapsed.TotalSeconds -lt 10 -and -not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Sleep -Milliseconds 500 }
if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe; "info  started explorer.exe again myself" }
Start-Sleep -Seconds 8

$log = Log
Check 'X Explorer restart seen (TaskbarCreated)' ($log -match 'explorer restarted \(TaskbarCreated\)')
Check 'X tray icon added again' ($log -match 'tray icon added again')
Check 'X TaskIsland still running' (-not $p.HasExited)
Check 'X island still visible' ([P4E.W]::IsWindowVisible($hwnd))
Check 'X island still topmost (WS_EX_TOPMOST)' (([P4E.W]::GetWindowLong($hwnd, -20) -band 0x8) -ne 0)
$after = Tray-Icon
"info  tray icon in the taskbar after: $(if ($after) { $after.Current.Name } else { 'not found (may be in the hidden-icons flyout)' })"
if ($before) { Check 'X tray icon visible in the taskbar again' ($after -ne $null) }
Stop-TI
"--- fails: $global:fails"
