# Drag to place (docs/DRAG-PLACE-PLAN.md): drag the collapsed pill anywhere on the screen with the real mouse.
# Near an edge it sticks to it (top/bottom/left/right); in the middle it stays free and round. A bottom-edge pill
# opens its panel upwards, a plain click still opens the panel, and the place survives a restart. Temporary data folder.
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
[DllImport("user32.dll")] public static extern bool IsWindowVisible(System.IntPtr h);
[DllImport("user32.dll")] public static extern bool GetWindowRect(System.IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool SetWindowPos(System.IntPtr h, System.IntPtr after, int x, int y, int cx, int cy, uint flags);
[DllImport("user32.dll")] public static extern System.IntPtr WindowFromPoint(POINT p);
[DllImport("user32.dll")] public static extern System.IntPtr GetAncestor(System.IntPtr h, uint flags);
public struct RECT { public int L, T, R, B; }
public struct POINT { public int X, Y; }
'@
[void][DP.M]::SetProcessDPIAware()
$global:fails = 0
$inv = [System.Globalization.CultureInfo]::InvariantCulture
function Check([string]$name, [bool]$ok, [string]$detail = '') { if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" } }
function Tray([int]$msg) {
    $cb = [DP.M+EnumProc] { param($h, $l)
        $x = 0; [void][DP.M]::GetWindowThreadProcessId($h, [ref]$x)
        if ($x -eq $script:TIProc.Id) { $sb = New-Object System.Text.StringBuilder 256; [void][DP.M]::GetClassName($h, $sb, 256); if ($sb.ToString() -like 'WindowsForms*') { [void][DP.M]::PostMessage($h, 0x800, [IntPtr]0, [IntPtr]$msg) } }
        $true }
    [void][DP.M]::EnumWindows($cb, [IntPtr]::Zero); Start-Sleep -Milliseconds 900
}
function Setting([string]$key) {
    python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); r=c.execute('select SettingValue from Settings where SettingKey=?',(sys.argv[2],)).fetchone(); print(r[0] if r else 0)" "$root\Data\taskisland.db" $key
}
function LogText { Get-Content (Get-ChildItem "$root\Logs\*.log" | Sort-Object LastWriteTime | Select-Object -Last 1).FullName -Raw }
# The last placement from the app log. It carries the monitor origin/scale and the resting pill in monitor DIPs:
# "... (1920x1080 at 0,0, scale 1) at 734,0 452x64; pill 912,0 96x25.6 Top".
function Pill {
    $pat = 'island placed on .*?\((\d+)x(\d+) at (-?\d+),(-?\d+), scale ([\d.]+)\) at (-?\d+),(-?\d+) (\d+)x(\d+)(.*?); pill (-?[\d.]+),(-?[\d.]+) ([\d.]+)x([\d.]+) (\w+)'
    $m = [regex]::Matches((LogText), $pat, 'Multiline')
    $g = $m[$m.Count - 1].Groups
    $scale = [double]::Parse($g[5].Value, $inv)
    $monL = [int]$g[3].Value; $monT = [int]$g[4].Value
    $pl = [double]::Parse($g[11].Value, $inv); $pt = [double]::Parse($g[12].Value, $inv)
    $pw = [double]::Parse($g[13].Value, $inv); $ph = [double]::Parse($g[14].Value, $inv)
    $dir = if ($g[10].Value -match ' expanded (up|down)') { $Matches[1] } else { '' }
    [pscustomobject]@{
        X = [int]($monL + ($pl + $pw / 2) * $scale); Y = [int]($monT + ($pt + $ph / 2) * $scale)
        Left = [int]($monL + $pl * $scale); Top = [int]($monT + $pt * $scale)
        Right = [int]($monL + ($pl + $pw) * $scale); Bottom = [int]($monT + ($pt + $ph) * $scale)
        W = $pw; H = $ph; Edge = $g[15].Value; Dir = $dir
        MonLeft = $monL; MonTop = $monT; MonW = [int]$g[1].Value; MonH = [int]$g[2].Value; Scale = $scale
        WinLeft = [int]$g[6].Value; WinTop = [int]$g[7].Value; WinW = [int]$g[8].Value; WinH = [int]$g[9].Value
    }
}
function Drag($fromX, $fromY, $toX, $toY) {
    Raise-Island
    [void][DP.M]::SetCursorPos($fromX, $fromY); Start-Sleep -Milliseconds 700
    [DP.M]::mouse_event(0x2, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 120
    $steps = 15
    for ($i = 1; $i -le $steps; $i++) {
        [void][DP.M]::SetCursorPos([int]($fromX + ($toX - $fromX) * $i / $steps), [int]($fromY + ($toY - $fromY) * $i / $steps))
        Start-Sleep -Milliseconds 30
    }
    Start-Sleep -Milliseconds 150
    [DP.M]::mouse_event(0x4, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 900
}
function Click($x, $y) {
    Raise-Island
    [void][DP.M]::SetCursorPos($x, $y); Start-Sleep -Milliseconds 700
    [DP.M]::mouse_event(0x2, 0, 0, 0, [IntPtr]::Zero); [void][DP.M]::SetCursorPos($x + 2, $y); Start-Sleep -Milliseconds 60
    [DP.M]::mouse_event(0x4, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 900
}
function Away { [void][DP.M]::SetCursorPos(200, 700); Start-Sleep -Milliseconds 600 }
function Esc { [DP.M]::keybd_event(0x1B, 0, 0, [IntPtr]::Zero); [DP.M]::keybd_event(0x1B, 0, 2, [IntPtr]::Zero) }
# The installed app can sit at the same top-centre spot; raise the test window above it before the real mouse moves.
function Raise-Island {
    $script:IslandHwnd = [IntPtr]::Zero; $script:IslandArea = 0
    $cb = [DP.M+EnumProc] { param($h, $l)
        $x = 0; [void][DP.M]::GetWindowThreadProcessId($h, [ref]$x)
        if ($x -eq $script:TIProc.Id -and [DP.M]::IsWindowVisible($h)) {
            $sb = New-Object System.Text.StringBuilder 256; [void][DP.M]::GetClassName($h, $sb, 256)
            if ($sb.ToString() -notlike 'WindowsForms*') {
                $r = New-Object DP.M+RECT; [void][DP.M]::GetWindowRect($h, [ref]$r)
                $area = ($r.R - $r.L) * ($r.B - $r.T)
                if ($area -gt $script:IslandArea) { $script:IslandArea = $area; $script:IslandHwnd = $h }
            }
        }
        $true }
    [void][DP.M]::EnumWindows($cb, [IntPtr]::Zero)
    if ($script:IslandHwnd -ne [IntPtr]::Zero) {
        [void][DP.M]::SetWindowPos($script:IslandHwnd, [IntPtr]::new(-1), 0, 0, 0, 0, 0x0013)
    }
}
function At-Island([int]$x, [int]$y) {
    $pt = New-Object DP.M+POINT; $pt.X = $x; $pt.Y = $y
    $h = [DP.M]::WindowFromPoint($pt)
    if ($h -eq [IntPtr]::Zero) { return $false }
    $root = [DP.M]::GetAncestor($h, 2)
    $x2 = 0; [void][DP.M]::GetWindowThreadProcessId($root, [ref]$x2)
    $x2 -eq $script:TIProc.Id
}
function Wait-TI([string]$name, [string]$type = '', [int]$ms = 3000) {
    $until = [DateTime]::UtcNow.AddMilliseconds($ms)
    do {
        $e = Find-TI $name $type
        if ($e) { return $e }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $until)
    return $null
}
# A click on a face that popped up by itself makes it laugh; the next click opens the panel (OnHostClick).
function Open-At([int]$x, [int]$y) {
    Click $x $y
    if (Wait-TI 'Add a new task' 'Edit' 1200) { return $true }
    Click $x $y
    return (Wait-TI 'Add a new task' 'Edit' 3000) -ne $null
}

$root = "$env:TEMP\TaskIslandDragPlace"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$debugLog = Join-Path $env:TEMP 'TaskIsland-debug.log'
$p = Start-TI $root @('--pop-seconds','3600')
Start-Sleep -Seconds 2
$start = Pill
Raise-Island
if (-not (At-Island $start.X $start.Y)) {
    Stop-Process -Id $p.Id -Force
    'FAIL  another TaskIsland window is on top at the pill; close the installed app and rerun'
    exit 1
}
Check 'starts centred at the top (offset 0, top 0)' ((Setting 'HorizontalOffset') -eq '0' -and (Setting 'TopMargin') -eq '0') "$(Setting 'HorizontalOffset'),$(Setting 'TopMargin')"
Check 'starts as a top notch' ($start.Edge -eq 'Top' -and [math]::Abs($start.Top - $start.MonTop) -le 1) "edge $($start.Edge) top $($start.Top)"

$before = if (Test-Path $debugLog) { (Get-Item $debugLog).Length } else { 0 }
Drag $start.X $start.Y ($start.X - 300) $start.Y
Away
$o = [int](Setting 'HorizontalOffset')
$expected = [int][math]::Round(-300 / $start.Scale)
Check 'drag left saves a negative offset' ([math]::Abs($o - $expected) -le 3) "offset $o expected $expected"
Check 'log says the island moved' ((LogText) -match 'island moved: offset') ''
Check 'a drag does not open the panel' ((Find-TI 'Add a new task' 'Edit') -eq $null)
$moved = Pill
Check 'window really moved left' ($moved.X -lt $start.X - 200) "x $($start.X) -> $($moved.X)"
Check 'moving along the top keeps it a top notch' ($moved.Edge -eq 'Top' -and [math]::Abs($moved.Top - $moved.MonTop) -le 1) "edge $($moved.Edge)"
if (Test-Path $debugLog) {
    $fs = [IO.File]::Open($debugLog, 'Open', 'Read', 'ReadWrite'); [void]$fs.Seek($before, 'Begin')
    $tail = (New-Object IO.StreamReader($fs)).ReadToEnd(); $fs.Close()
    Check 'face is Excited while carried' ($tail -match 'reaction Excited') ''
    Check 'face is Happy when dropped' ($tail -match 'reaction Happy') ''
}

# A small wobble (under the drag threshold) is still a click: it opens the panel.
Start-Sleep -Seconds 4
Check 'plain click still opens the panel' (Open-At $moved.X $moved.Y)
Check 'offset unchanged by the click' ([int](Setting 'HorizontalOffset') -eq $o) (Setting 'HorizontalOffset')
Esc; Away
Start-Sleep -Seconds 2
Check 'panel closed again' ((Find-TI 'Add a new task' 'Edit') -eq $null)

# Far right, halfway down: the resting pill sticks to the right edge (the open panel still fits because it slides
# inwards). Moving only sideways along the top would keep it a top notch, so the drag goes down as well.
$cur = Pill
Drag $cur.X $cur.Y ($cur.MonLeft + $cur.MonW - 5) ($cur.MonTop + [int]($cur.MonH / 2))
Away
$right = Pill
Check 'far right sticks to the right edge' ($right.Edge -eq 'Right' -and [math]::Abs($right.Right - ($right.MonLeft + $right.MonW)) -le 2) "edge $($right.Edge) right $($right.Right) monitor $($right.MonLeft + $right.MonW)"
Check 'a right-edge drop is saved' ([int](Setting 'HorizontalOffset') -gt 0) (Setting 'HorizontalOffset')

# The middle: free, round, and both axes are kept.
$midX = $right.MonLeft + [int]($right.MonW / 2) - 150
$midY = $right.MonTop + [int]($right.MonH / 2)
Drag $right.X $right.Y $midX $midY
Away
$middle = Pill
Check 'a middle drop is free (round pill)' ($middle.Edge -eq 'None') "edge $($middle.Edge)"
Check 'a middle drop keeps its vertical place' ($middle.Top -gt $middle.MonTop + 100 -and $middle.Bottom -lt $middle.MonTop + $middle.MonH - 100) "top $($middle.Top) bottom $($middle.Bottom) monitor $($middle.MonTop)..$($middle.MonTop + $middle.MonH)"
Check 'the vertical place is saved' ([int](Setting 'TopMargin') -gt 100) (Setting 'TopMargin')
Check 'the window follows the floating pill' ([math]::Abs($middle.WinTop - $middle.Top) -le 2 -and $middle.WinH -le [int](70 * $middle.Scale)) "window $($middle.WinLeft),$($middle.WinTop) $($middle.WinW)x$($middle.WinH)"

# Bottom edge: flush, and the panel opens upwards from it.
Drag $middle.X $middle.Y ($middle.MonLeft + [int]($middle.MonW / 2)) ($middle.MonTop + $middle.MonH - 10)
Away
$bottom = Pill
Check 'the bottom edge sticks' ($bottom.Edge -eq 'Bottom' -and [math]::Abs($bottom.Bottom - ($bottom.MonTop + $bottom.MonH)) -le 2) "edge $($bottom.Edge) bottom $($bottom.Bottom)"
Check 'a bottom pill opens its panel' (Open-At $bottom.X $bottom.Y)
$open = Pill
Check 'the panel opens upwards' ($open.Dir -eq 'up' -and $open.WinTop -lt $bottom.Top - 100) "dir $($open.Dir) window top $($open.WinTop) pill top $($bottom.Top)"
Check 'the upward window is the expanded size' ([math]::Abs($open.WinH - [int](600 * $open.Scale)) -le 2) "height $($open.WinH)"
Check 'the upward window still ends at the screen bottom' ([math]::Abs($open.WinTop + $open.WinH - ($open.MonTop + $open.MonH)) -le 2) "bottom $($open.WinTop + $open.WinH)"
Away; Esc
Start-Sleep -Seconds 2
Check 'the upward panel closes' ((Find-TI 'Add a new task' 'Edit') -eq $null)

# Left edge: flush, panel opens beside it and stays on the screen.
Drag $bottom.X $bottom.Y ($bottom.MonLeft + 2) ($bottom.MonTop + [int]($bottom.MonH / 2))
Away
$left = Pill
Check 'the left edge sticks' ($left.Edge -eq 'Left' -and [math]::Abs($left.Left - $left.MonLeft) -le 2) "edge $($left.Edge) left $($left.Left)"
Check 'a left pill opens its panel' (Open-At $left.X $left.Y)
$leftOpen = Pill
$quick = Wait-TI 'Add a new task' 'Edit'
Check 'the left window stays flush' ([math]::Abs($leftOpen.WinLeft - $leftOpen.MonLeft) -le 2) "window left $($leftOpen.WinLeft) monitor $($leftOpen.MonLeft)"
Check 'the left panel stays inside the screen' ($quick.Current.BoundingRectangle.Left -ge $leftOpen.MonLeft -and $quick.Current.BoundingRectangle.Right -le $leftOpen.MonLeft + $leftOpen.MonW) "panel $($quick.Current.BoundingRectangle.Left)..$($quick.Current.BoundingRectangle.Right)"
Away; Esc
Start-Sleep -Seconds 2

# Restart: the free position and the attached edge come back exactly.
$savedOffset = [int](Setting 'HorizontalOffset'); $savedTop = [int](Setting 'TopMargin')
Stop-Process -Id $p.Id -Force; Wait-Process -Id $p.Id -Timeout 5 -ErrorAction SilentlyContinue; Start-Sleep -Seconds 1
$p = Start-TI $root @('--pop-seconds','3600')
Start-Sleep -Seconds 2
$restored = Pill
Check 'the place is kept after a restart' ([int](Setting 'HorizontalOffset') -eq $savedOffset -and [int](Setting 'TopMargin') -eq $savedTop) "offset $(Setting 'HorizontalOffset') top $(Setting 'TopMargin')"
Check 'the left edge is kept after a restart' ($restored.Edge -eq 'Left' -and [math]::Abs($restored.Left - $restored.MonLeft) -le 2) "edge $($restored.Edge) left $($restored.Left)"
Check 'the saved vertical place is drawn again' ([math]::Abs($restored.Top - ($restored.MonTop + [int]($savedTop * $restored.Scale))) -le 3) "top $($restored.Top) saved $savedTop"

# Back to the top edge from a floating place: the notch shape returns.
Drag $restored.X $restored.Y ($restored.MonLeft + [int]($restored.MonW / 2)) ($restored.MonTop + 4)
Away
$top = Pill
Check 'the top edge still snaps from anywhere' ($top.Edge -eq 'Top' -and [math]::Abs($top.Top - $top.MonTop) -le 1) "edge $($top.Edge) top $($top.Top)"
Stop-Process -Id $p.Id -Force
"dragplace done: $global:fails failure(s)"
