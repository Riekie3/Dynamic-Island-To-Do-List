# Drag to place (docs/DRAG-PLACE-PLAN.md): drag the collapsed pill along the top with the real mouse; the place is saved,
# a plain click still opens the panel, the face reacts, and the place survives a restart. Temporary data folder.
$sp = $PSScriptRoot
. "$sp\uia.ps1"
Add-Type -Namespace DP -Name M -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, int x, int y, uint d, System.IntPtr e);
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, System.IntPtr e);
[DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr h, uint m, System.IntPtr w, System.IntPtr l);
public delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, System.IntPtr l);
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(System.IntPtr h, out int pid);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(System.IntPtr h, System.Text.StringBuilder s, int n);
'@
[void][DP.M]::SetProcessDPIAware()
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') { if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" } }
function Tray([int]$msg) {
    $cb = [DP.M+EnumProc] { param($h, $l)
        $x = 0; [void][DP.M]::GetWindowThreadProcessId($h, [ref]$x)
        if ($x -eq $script:TIProc.Id) { $sb = New-Object System.Text.StringBuilder 256; [void][DP.M]::GetClassName($h, $sb, 256); if ($sb.ToString() -like 'WindowsForms*') { [void][DP.M]::PostMessage($h, 0x800, [IntPtr]0, [IntPtr]$msg) } }
        $true }
    [void][DP.M]::EnumWindows($cb, [IntPtr]::Zero); Start-Sleep -Milliseconds 900
}
function Offset { python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); r=c.execute('select SettingValue from Settings where SettingKey=?',(sys.argv[2],)).fetchone(); print(r[0] if r else 0)" "$root\Data\taskisland.db" HorizontalOffset }
function LogText { Get-Content (Get-ChildItem "$root\Logs\*.log" | Sort-Object LastWriteTime | Select-Object -Last 1).FullName -Raw }
# The island's last placement from the app log: "... at <left>,<top> <w>x<h>".
function Pill {
    $m = [regex]::Matches((LogText), 'island placed on \S+ .*? at (-?\d+),(-?\d+) (\d+)x(\d+)\r?$', 'Multiline')
    $g = $m[$m.Count - 1].Groups
    [pscustomobject]@{ X = [int]$g[1].Value + [int]$g[3].Value / 2; Y = [int]$g[2].Value + 12 }
}
function Drag($fromX, $fromY, $toX) {
    [void][DP.M]::SetCursorPos($fromX, $fromY); Start-Sleep -Milliseconds 700
    [DP.M]::mouse_event(0x2, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 120
    $steps = 15
    for ($i = 1; $i -le $steps; $i++) { [void][DP.M]::SetCursorPos([int]($fromX + ($toX - $fromX) * $i / $steps), $fromY); Start-Sleep -Milliseconds 30 }
    Start-Sleep -Milliseconds 150
    [DP.M]::mouse_event(0x4, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 900
}
function Away { [void][DP.M]::SetCursorPos(200, 700); Start-Sleep -Milliseconds 600 }

$root = "$env:TEMP\TaskIslandDragPlace"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$debugLog = Join-Path $env:TEMP 'TaskIsland-debug.log'
$p = Start-TI $root @('--pop-seconds','3600')
Start-Sleep -Seconds 2
$start = Pill
Check 'starts centred (offset 0)' ((Offset) -eq '0') (Offset)

$before = if (Test-Path $debugLog) { (Get-Item $debugLog).Length } else { 0 }
Drag $start.X $start.Y ($start.X - 300)
Away
$o = [int](Offset)
Check 'drag left saves a negative offset' ($o -lt -150 -and $o -gt -320) "offset $o"
Check 'log says the island moved' ((LogText) -match 'island moved: offset') ''
Check 'a drag does not open the panel' ((Find-TI 'Add a new task' 'Edit') -eq $null)
$moved = Pill
Check 'window really moved left' ($moved.X -lt $start.X - 200) "x $($start.X) -> $($moved.X)"
if (Test-Path $debugLog) {
    $fs = [IO.File]::Open($debugLog, 'Open', 'Read', 'ReadWrite'); [void]$fs.Seek($before, 'Begin')
    $tail = (New-Object IO.StreamReader($fs)).ReadToEnd(); $fs.Close()
    Check 'face is Excited while carried' ($tail -match 'reaction Excited') ''
    Check 'face is Happy when dropped' ($tail -match 'reaction Happy') ''
}

# A small wobble (under the drag threshold) is still a click: it opens the panel. Wait for the Happy face to end first
# (clicking a face that is showing a reaction makes it laugh, as before).
Start-Sleep -Seconds 4
[void][DP.M]::SetCursorPos($moved.X, $moved.Y); Start-Sleep -Milliseconds 700
[DP.M]::mouse_event(0x2, 0, 0, 0, [IntPtr]::Zero); [void][DP.M]::SetCursorPos($moved.X + 2, $moved.Y); Start-Sleep -Milliseconds 60
[DP.M]::mouse_event(0x4, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 900
Check 'plain click still opens the panel' ((Find-TI 'Add a new task' 'Edit') -ne $null)
Check 'offset unchanged by the click' ([int](Offset) -eq $o) (Offset)
[DP.M]::keybd_event(0x1B, 0, 0, [IntPtr]::Zero); [DP.M]::keybd_event(0x1B, 0, 2, [IntPtr]::Zero)
Away
Start-Sleep -Seconds 2
Check 'panel closed again' ((Find-TI 'Add a new task' 'Edit') -eq $null)

# Dragging far right stops where the open panel still fits.
$cur = Pill
Drag $cur.X $cur.Y ($cur.X + 4000)
Away
$right = [int](Offset)
Check 'far right is clamped, not off screen' ($right -gt 0 -and $right -lt 2000) "offset $right"

Stop-Process -Id $p.Id -Force; Start-Sleep -Seconds 1
$p = Start-TI $root @('--pop-seconds','3600')
Start-Sleep -Seconds 2
Check 'place kept after restart' ([int](Offset) -eq $right -and (LogText) -match 'island placed') "offset $(Offset)"
Stop-Process -Id $p.Id -Force
"dragplace done: $global:fails failure(s)"
