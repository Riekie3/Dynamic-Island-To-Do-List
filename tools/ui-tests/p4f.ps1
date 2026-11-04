# Phase 4: Alt+F4 only collapses, no network, CPU while paused (Release), hotkey taken by another app. No mouse.
$sp = $PSScriptRoot
. "$sp\uia.ps1"
Add-Type -Namespace P4F -Name W -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr h, uint m, System.IntPtr w, System.IntPtr l);
[DllImport("user32.dll")] public static extern bool RegisterHotKey(System.IntPtr h, int id, uint mods, uint vk);
[DllImport("user32.dll")] public static extern bool UnregisterHotKey(System.IntPtr h, int id);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(System.IntPtr h);
public delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, System.IntPtr l);
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(System.IntPtr h, out int pid);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(System.IntPtr h, System.Text.StringBuilder s, int n);
'@
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" }
}
function Log { Get-Content "$root\Logs\taskisland-$(Get-Date -Format yyyyMMdd).log" -Raw -ErrorAction SilentlyContinue }
function Expanded { (Find-TI 'Add a new task' 'Edit') -ne $null }
function Tray([int]$mouseMsg) {
    $cb = [P4F.W+EnumProc] { param($h, $l)
        $procId = 0; [void][P4F.W]::GetWindowThreadProcessId($h, [ref]$procId)
        if ($procId -eq $script:TIProc.Id) {
            $sb = New-Object System.Text.StringBuilder 256; [void][P4F.W]::GetClassName($h, $sb, 256)
            if ($sb.ToString() -like 'WindowsForms*') { [void][P4F.W]::PostMessage($h, 0x800, [IntPtr]0, [IntPtr]$mouseMsg) }
        }
        $true }
    [void][P4F.W]::EnumWindows($cb, [IntPtr]::Zero); Start-Sleep -Milliseconds 700
}
function Tray-Menu([string]$item) {
    Tray 0x205
    $e = Find-TI $item 'MenuItem'; if (-not $e) { return $false }
    $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 700; $true
}

$root = "$env:TEMP\TaskIslandP4f"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @() 'Release'
$hwnd = [IntPtr](Get-TIWindow).Current.NativeWindowHandle

# W18: no network connections at all.
$tcp = @(Get-NetTCPConnection -OwningProcess $p.Id -ErrorAction SilentlyContinue)
$udp = @(Get-NetUDPEndpoint -OwningProcess $p.Id -ErrorAction SilentlyContinue)
Check 'W18 no TCP connections or listeners' ($tcp.Count -eq 0) "$($tcp.Count)"
Check 'W18 no UDP endpoints' ($udp.Count -eq 0) "$($udp.Count)"

# W1: Alt+F4 on the open panel only collapses.
Tray 0x202
if ((Expanded) -and (Get-FgTitle) -eq $p.Id) {
    [System.Windows.Forms.SendKeys]::SendWait('%{F4}'); Start-Sleep -Milliseconds 900
    Check 'W1 Alt+F4 collapses the panel' (-not (Expanded))
    Check 'W1 Alt+F4 does not exit' (-not $p.HasExited)
} else { "SKIP  W1 Alt+F4 (panel not in front: a posted tray click carries no foreground right)" }
if (Expanded) { Tray 0x202 }

# P10: paused (hidden) → CPU over 30 s.
Check 'pause via tray' (Tray-Menu 'Pause visibility')
Check 'paused: hidden' (-not [P4F.W]::IsWindowVisible($hwnd))
Start-Sleep -Seconds 3
$p.Refresh(); $c0 = $p.TotalProcessorTime.TotalMilliseconds
Start-Sleep -Seconds 30
$p.Refresh(); $c1 = $p.TotalProcessorTime.TotalMilliseconds
"info  paused: $([math]::Round($c1 - $c0)) ms CPU in 30 s"
Check 'P10 paused: CPU about zero (< 30 ms in 30 s)' (($c1 - $c0) -lt 30) "$($c1 - $c0) ms"
Stop-TI; Start-Sleep -Milliseconds 800

# W16: Ctrl+Alt+T already taken by another app → TaskIsland logs it and keeps running.
$ok = [P4F.W]::RegisterHotKey([IntPtr]::Zero, 77, 0x2 -bor 0x1, 0x54)
Check 'setup: this script holds Ctrl+Alt+T' $ok
$p = Start-TI $root @() 'Release'
Check 'W16 conflict is logged (error 1409)' ((Log) -match 'hotkey Ctrl\+Alt\+T could not be registered \(error 1409')
Check 'W16 app keeps running without the hotkey' (-not $p.HasExited)
[void][P4F.W]::UnregisterHotKey([IntPtr]::Zero, 77)
Stop-TI
"--- fails: $global:fails"
