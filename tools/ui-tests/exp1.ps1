# Which modules / how much memory does the first expand add? Separate root, so it can run next to other tests.
param([string]$Exe = 'D:\Rie\Claude\TaskIsland\src\TaskIsland.App\bin\Release\net10.0-windows\TaskIsland.exe', [string]$Root = "$env:TEMP\TaskIslandExp1")
$sp = $PSScriptRoot
. "$sp\uia.ps1"
Add-Type -Namespace P6 -Name E -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr h, uint m, System.IntPtr w, System.IntPtr l);
public delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, System.IntPtr l);
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(System.IntPtr h, out int pid);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(System.IntPtr h, System.Text.StringBuilder s, int n);
'@
function Tray([int]$msg) {
    $cb = [P6.E+EnumProc] { param($h, $l)
        $x = 0; [void][P6.E]::GetWindowThreadProcessId($h, [ref]$x)
        if ($x -eq $script:p.Id) { $sb = New-Object System.Text.StringBuilder 256; [void][P6.E]::GetClassName($h, $sb, 256); if ($sb.ToString() -like 'WindowsForms*') { [void][P6.E]::PostMessage($h, 0x800, [IntPtr]0, [IntPtr]$msg) } }
        $true }
    [void][P6.E]::EnumWindows($cb, [IntPtr]::Zero); Start-Sleep -Milliseconds 800
}
function M($label) { $x = Get-Process -Id $script:p.Id; "{0,-28} {1,6:N1} MB private {2,6:N1} MB WS" -f $label, ($x.PrivateMemorySize64/1MB), ($x.WorkingSet64/1MB) }
if (Test-Path $Root) { Remove-Item $Root -Recurse -Force }
$env:TASKISLAND_ROOT = $Root
$p = Start-Process $Exe -PassThru; Start-Sleep 4; Stop-Process -Id $p.Id -Force; Start-Sleep 1
python -I "$sp\setkey.py" "$Root\Data\taskisland.db" Companion Off
$p = Start-Process $Exe -PassThru
Start-Sleep 20; M 'collapsed 20 s'
$m0 = @{}; (Get-Process -Id $p.Id).Modules | ForEach-Object { $m0[$_.FileName] = $_.ModuleMemorySize }
Tray 0x203; Start-Sleep 6; M 'expanded 6 s'
$m1 = (Get-Process -Id $p.Id).Modules
"new modules after expand:"
$m1 | Where-Object { -not $m0.ContainsKey($_.FileName) } | Sort-Object ModuleMemorySize -Descending | Select-Object -First 25 | ForEach-Object { "  {0,7:N1} MB  {1}" -f ($_.ModuleMemorySize/1MB), $_.FileName }
Tray 0x202; Tray 0x202; Start-Sleep 10; M 'collapsed again 10 s'
Stop-Process -Id $p.Id -Force
