# List chips: drag one chip in front of another with the real mouse; the new order is saved.
$sp = $PSScriptRoot
. "$sp\uia.ps1"
Add-Type -Namespace CD -Name M -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, int x, int y, uint d, System.IntPtr e);
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr h, uint m, System.IntPtr w, System.IntPtr l);
public delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, System.IntPtr l);
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(System.IntPtr h, out int pid);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(System.IntPtr h, System.Text.StringBuilder s, int n);
'@
[void][CD.M]::SetProcessDPIAware()
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') { if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" } }
function Tray([int]$msg) {
    $cb = [CD.M+EnumProc] { param($h, $l)
        $x = 0; [void][CD.M]::GetWindowThreadProcessId($h, [ref]$x)
        if ($x -eq $script:TIProc.Id) { $sb = New-Object System.Text.StringBuilder 256; [void][CD.M]::GetClassName($h, $sb, 256); if ($sb.ToString() -like 'WindowsForms*') { [void][CD.M]::PostMessage($h, 0x800, [IntPtr]0, [IntPtr]$msg) } }
        $true }
    [void][CD.M]::EnumWindows($cb, [IntPtr]::Zero); Start-Sleep -Milliseconds 900
}
function Db([string]$db, [string]$sql) {
    python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print('|'.join(str(x) for r in c.execute(sys.argv[2]).fetchall() for x in r))" $db $sql
}
function Order { Db "$root\Data\taskisland.db" "select group_concat(Name,'+') from (select Name from Lists order by SortOrder)" }
function Centre([string]$name) {
    $r = (Find-TI $name 'Button').Current.BoundingRectangle
    [pscustomobject]@{ X = [int]($r.Left + $r.Width / 2); Y = [int]($r.Top + $r.Height / 2); L = [int]$r.Left; R = [int]$r.Right }
}
function Drag($fromX, $fromY, $toX, $toY) {
    [void][CD.M]::SetCursorPos($fromX, $fromY); Start-Sleep -Milliseconds 150
    [CD.M]::mouse_event(0x2, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 120
    $steps = 12
    for ($i = 1; $i -le $steps; $i++) {
        [void][CD.M]::SetCursorPos([int]($fromX + ($toX - $fromX) * $i / $steps), [int]($fromY + ($toY - $fromY) * $i / $steps))
        Start-Sleep -Milliseconds 40
    }
    Start-Sleep -Milliseconds 200
    [CD.M]::mouse_event(0x4, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 900
}

$root = "$env:TEMP\TaskIslandChipDrag"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @('--pop-seconds','3600')
Check 'start order' ((Order) -eq 'Personal+Office Work') (Order)
Tray 0x203
$personal = Centre 'Personal'; $office = Centre 'Office Work'
# Drag Office Work onto the left half of Personal.
Drag $office.X $office.Y ($personal.L + 6) $personal.Y
Check 'Office Work dragged in front of Personal' ((Order) -eq 'Office Work+Personal') (Order)
Check 'panel still open' ((Find-TI 'Add a new task' 'Edit') -ne $null)
# Drag it back: Office Work (now first) past the right half of Personal.
$personal = Centre 'Personal'; $office = Centre 'Office Work'
Drag $office.X $office.Y ($personal.R - 4) $personal.Y
Check 'dragged back behind Personal' ((Order) -eq 'Personal+Office Work') (Order)
# A plain click still selects the list and does not reorder.
$office = Centre 'Office Work'
[void][CD.M]::SetCursorPos($office.X, $office.Y); [CD.M]::mouse_event(0x2, 0, 0, 0, [IntPtr]::Zero); [CD.M]::mouse_event(0x4, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 500
Check 'click does not reorder' ((Order) -eq 'Personal+Office Work') (Order)
Stop-TI
"chipdrag done: $global:fails failure(s)"
