# Phase 5: memory with the Settings window opened (all pages visited) and closed again. Release build, seeded data.
param([string]$Config = 'Release')
$sp = $PSScriptRoot
. "$sp\uia.ps1"
function Mem { $x = Get-Process -Id $script:TIProc.Id; "{0:N1} MB private, {1:N1} MB working set" -f ($x.PrivateMemorySize64 / 1MB), ($x.WorkingSet64 / 1MB) }
function Cpu { (Get-Process -Id $script:TIProc.Id).TotalProcessorTime.TotalMilliseconds }
function Pause([int]$ms = 500) { Start-Sleep -Milliseconds $ms }
Add-Type -Namespace P5 -Name W -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr h, uint m, System.IntPtr w, System.IntPtr l);
public delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, System.IntPtr l);
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(System.IntPtr h, out int pid);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(System.IntPtr h, System.Text.StringBuilder s, int n);
'@
function Tray-Menu([string]$like) {
    $list = New-Object System.Collections.Generic.List[IntPtr]
    $cb = [P5.W+EnumProc] { param($h, $l)
        $procId = 0; [void][P5.W]::GetWindowThreadProcessId($h, [ref]$procId)
        if ($procId -eq $script:TIProc.Id) { $sb = New-Object System.Text.StringBuilder 256; [void][P5.W]::GetClassName($h, $sb, 256); if ($sb.ToString() -like 'WindowsForms*') { $list.Add($h) } }
        $true }
    [void][P5.W]::EnumWindows($cb, [IntPtr]::Zero)
    foreach ($h in $list) { [void][P5.W]::PostMessage($h, 0x800, [IntPtr]0, [IntPtr]0x205) }
    Start-Sleep -Milliseconds 700
    $e = Get-TIAll | Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.MenuItem' -and $_.Current.Name -like $like } | Select-Object -First 1
    $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 900
}
$root = "$env:TEMP\TaskIslandP5d"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @('--pop-seconds','3600') $Config; Stop-TI; Start-Sleep -Milliseconds 800
python -I "$sp\seed.py" "$root\Data\taskisland.db" 2>&1 | Out-Null
$p = Start-TI $root @('--pop-seconds','3600') $Config
Start-Sleep -Seconds 20
"before opening Settings : $(Mem)"
for ($round = 1; $round -le 3; $round++) {
    Tray-Menu 'Settings*'
    foreach ($n in 'General','Appearance','Behaviour','Lists','Data','About') {
        $e = Find-TI $n 'RadioButton'; $e.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Pause 400
    }
    "round $round open (all pages)   : $(Mem)"
    $w = Find-TI 'TaskIsland Settings' 'Window'
    $w.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Start-Sleep -Seconds 6
    "round $round closed + 6 s       : $(Mem)"
}
$c0 = Cpu; Start-Sleep -Seconds 30; $c1 = Cpu
"idle CPU after closing, 30 s    : $([math]::Round($c1 - $c0)) ms"
Stop-TI
