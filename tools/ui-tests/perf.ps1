# Phase 6 performance measurements on the Release build (temp TASKISLAND_ROOT, nothing of the real data is touched).
# usage: perf.ps1 -Mode start|idle|cycles|big [-Companion Off|Calm|Lively] [-Seconds 60]
param([string]$Mode = 'idle', [string]$Companion = 'Calm', [int]$Seconds = 60, [int]$Cycles = 50)
$sp = $PSScriptRoot
. "$sp\uia.ps1"
Add-Type -Namespace P6 -Name W -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr h, uint m, System.IntPtr w, System.IntPtr l);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(System.IntPtr h);
public delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, System.IntPtr l);
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(System.IntPtr h, out int pid);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(System.IntPtr h, System.Text.StringBuilder s, int n);
'@
function Windows-Of([int]$procId, [string]$like) {
    $list = New-Object System.Collections.Generic.List[IntPtr]
    $cb = [P6.W+EnumProc] { param($h, $l)
        $x = 0; [void][P6.W]::GetWindowThreadProcessId($h, [ref]$x)
        if ($x -eq $procId) { $sb = New-Object System.Text.StringBuilder 256; [void][P6.W]::GetClassName($h, $sb, 256); if ($sb.ToString() -like $like) { $list.Add($h) } }
        $true }
    [void][P6.W]::EnumWindows($cb, [IntPtr]::Zero); $list
}
function Tray([int]$msg) { foreach ($h in (Windows-Of $script:TIProc.Id 'WindowsForms*')) { [void][P6.W]::PostMessage($h, 0x800, [IntPtr]0, [IntPtr]$msg) }; Start-Sleep -Milliseconds 700 }
function Snap {
    $x = Get-Process -Id $script:TIProc.Id
    $io = Get-CimInstance Win32_Process -Filter "ProcessId=$($x.Id)"
    [pscustomobject]@{ Cpu = $x.TotalProcessorTime.TotalMilliseconds; Priv = $x.PrivateMemorySize64 / 1MB; WS = $x.WorkingSet64 / 1MB
        Threads = $x.Threads.Count; Handles = $x.HandleCount; WriteOps = [int64]$io.WriteOperationCount; WriteBytes = [int64]$io.WriteTransferCount; GdiUser = 0 }
}
function Fmt($s) { "{0,7:N1} MB private, {1,7:N1} MB WS, {2} threads, {3} handles" -f $s.Priv, $s.WS, $s.Threads, $s.Handles }
function Gpu([int]$procId, [int]$sec) {
    try {
        $set = (Get-Counter -Counter "\GPU Engine(pid_${procId}_*)\Utilization Percentage" -SampleInterval 2 -MaxSamples ([Math]::Max(1, [int]($sec / 2))) -ErrorAction Stop).CounterSamples
        $max = ($set | Measure-Object CookedValue -Maximum).Maximum; $sum = ($set | Measure-Object CookedValue -Sum).Sum
        "GPU engine utilisation over ${sec} s: max sample $([Math]::Round($max,3)) %, sum of all samples $([Math]::Round($sum,3))"
    } catch { "GPU counter not available: $($_.Exception.Message)" }
}

$root = "$env:TEMP\TaskIslandP6$Mode"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$exe = if ($env:TI_EXE) { $env:TI_EXE } else { 'D:\Rie\Claude\TaskIsland\src\TaskIsland.App\bin\Release\net10.0-windows\TaskIsland.exe' }
$env:TASKISLAND_ROOT = $root
# first run creates the database, then we adjust settings / seed
$p = Start-Process $exe -PassThru; $script:TIProc = $p; Start-Sleep -Seconds 4; Stop-TI; Start-Sleep -Milliseconds 800
$db = "$root\Data\taskisland.db"
python -I "$sp\setkey.py" $db Companion $Companion
if ($Mode -eq 'big') { python -I "$sp\seed500.py" $db }

# cold start: time until the first top-level window of the process is visible
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process $exe -PassThru; $script:TIProc = $p
$vis = $null
while ($sw.ElapsedMilliseconds -lt 15000) {
    foreach ($h in (Windows-Of $p.Id 'HwndWrapper*')) { if ([P6.W]::IsWindowVisible($h)) { $vis = $sw.ElapsedMilliseconds; break } }
    if ($vis) { break }; Start-Sleep -Milliseconds 5
}
"cold start to visible window: $vis ms"
Start-Sleep -Seconds 4
"Mode=$Mode Companion=$Companion"
"after 4 s                   : $(Fmt (Snap))"
Start-Sleep -Seconds 56
$a = Snap
"after 60 s                  : $(Fmt $a)"

if ($Mode -in 'start', 'idle') {
    $gj = Start-Job -ScriptBlock { param($pid2, $sec) try { $s = (Get-Counter -Counter "\GPU Engine(pid_${pid2}_*)\Utilization Percentage" -SampleInterval 2 -MaxSamples ([int]($sec/2)) -ErrorAction Stop).CounterSamples; "{0}|{1}" -f ($s | Measure-Object CookedValue -Maximum).Maximum, ($s | Measure-Object CookedValue -Sum).Sum } catch { "n/a $($_.Exception.Message)" } } -ArgumentList $p.Id, 30
    Start-Sleep -Seconds $Seconds
    $b = Snap
    $cpu = $b.Cpu - $a.Cpu
    "idle ${Seconds} s: CPU time {0:N0} ms = {1:N3} % of one core; disk write ops {2}, bytes {3}" -f $cpu, ($cpu / ($Seconds * 1000) * 100), ($b.WriteOps - $a.WriteOps), ($b.WriteBytes - $a.WriteBytes)
    "after idle                  : $(Fmt $b)"
    "GPU (30 s window, raw): $(Receive-Job $gj -Wait)"; Remove-Job $gj
    $tcp = @(Get-NetTCPConnection -OwningProcess $p.Id -ErrorAction SilentlyContinue); $udp = @(Get-NetUDPEndpoint -OwningProcess $p.Id -ErrorAction SilentlyContinue)
    "network: $($tcp.Count) TCP, $($udp.Count) UDP"
}
if ($Mode -eq 'cycles') {
    "start of cycles            : $(Fmt $a)"
    $t0 = Snap
    for ($i = 1; $i -le $Cycles; $i++) {
        Tray 0x203; Start-Sleep -Milliseconds 500      # expand
        Tray 0x202; Tray 0x202; Start-Sleep -Milliseconds 400   # collapse (first button-up is swallowed after a double-click)
        if ($i % 10 -eq 0) { "cycle $i : $(Fmt (Snap))" }
    }
    Start-Sleep -Seconds 20
    $e = Snap
    "after $Cycles cycles + 20 s    : $(Fmt $e)   (delta private {0:N1} MB)" -f ($e.Priv - $t0.Priv)
    $x = Find-TI 'Add a new task' 'Edit'
    "panel collapsed at the end: $($x -eq $null)"
}
if ($Mode -eq 'big') {
    $t0 = Snap; $c0 = $t0.Cpu
    for ($m = 1; $m -le [int]($Seconds / 60); $m++) {
        Start-Sleep -Seconds 60
        $s = Snap
        "minute $m : cpu delta {0:N0} ms total, $(Fmt $s)" -f ($s.Cpu - $c0)
    }
}
Stop-TI
