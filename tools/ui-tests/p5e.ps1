# Memory control without UI Automation: open Settings via --settings, close with WM_CLOSE, no UIA client attached.
param([string]$Config = 'Release')
$sp = $PSScriptRoot
. "$sp\uia.ps1"
Add-Type -Namespace P5 -Name V -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr h, uint m, System.IntPtr w, System.IntPtr l);
public delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, System.IntPtr l);
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(System.IntPtr h, out int pid);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(System.IntPtr h, System.Text.StringBuilder s, int n);
'@
function Mem { $x = Get-Process -Id $script:TIProc.Id; "{0:N1} MB private, {1:N1} MB working set" -f ($x.PrivateMemorySize64 / 1MB), ($x.WorkingSet64 / 1MB) }
function Close-Settings {
    $cb = [P5.V+EnumProc] { param($h, $l)
        $procId = 0; [void][P5.V]::GetWindowThreadProcessId($h, [ref]$procId)
        if ($procId -eq $script:TIProc.Id) { $sb = New-Object System.Text.StringBuilder 256; [void][P5.V]::GetWindowText($h, $sb, 256); if ($sb.ToString() -like '*Settings*') { [void][P5.V]::PostMessage($h, 0x10, [IntPtr]0, [IntPtr]0) } }
        $true }
    [void][P5.V]::EnumWindows($cb, [IntPtr]::Zero)
}
$root = "$env:TEMP\TaskIslandP5e"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @('--pop-seconds','3600') $Config; Stop-TI; Start-Sleep -Milliseconds 800
python -I "$sp\seed.py" "$root\Data\taskisland.db" 2>&1 | Out-Null
# idle baseline without Settings
$p = Start-TI $root @('--pop-seconds','3600') $Config
Start-Sleep -Seconds 20
"A idle baseline (seeded)      : $(Mem)"
Stop-TI; Start-Sleep -Milliseconds 800
# with Settings opened at start, then closed
$p = Start-TI $root @('--pop-seconds','3600','--settings') $Config
Start-Sleep -Seconds 8
"B Settings open               : $(Mem)"
Close-Settings
Start-Sleep -Seconds 8
"B Settings closed + 8 s       : $(Mem)"
Start-Sleep -Seconds 20
"B Settings closed + 28 s      : $(Mem)"
Stop-TI
