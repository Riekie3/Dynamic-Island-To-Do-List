# Task rows: drag one row below another in the same priority group; the new order is saved.
$sp = $PSScriptRoot
. "$sp\uia.ps1"
Add-Type -Namespace RD -Name M -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, int x, int y, uint d, System.IntPtr e);
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr h, uint m, System.IntPtr w, System.IntPtr l);
public delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, System.IntPtr l);
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(System.IntPtr h, out int pid);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(System.IntPtr h, System.Text.StringBuilder s, int n);
'@
[void][RD.M]::SetProcessDPIAware()
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') { if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" } }
function Tray([int]$msg) {
    $cb = [RD.M+EnumProc] { param($h, $l)
        $x = 0; [void][RD.M]::GetWindowThreadProcessId($h, [ref]$x)
        if ($x -eq $script:TIProc.Id) { $sb = New-Object System.Text.StringBuilder 256; [void][RD.M]::GetClassName($h, $sb, 256); if ($sb.ToString() -like 'WindowsForms*') { [void][RD.M]::PostMessage($h, 0x800, [IntPtr]0, [IntPtr]$msg) } }
        $true }
    [void][RD.M]::EnumWindows($cb, [IntPtr]::Zero); Start-Sleep -Milliseconds 900
}
function Db([string]$db, [string]$sql) {
    python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print('|'.join(str(x) for r in c.execute(sys.argv[2]).fetchall() for x in r))" $db $sql
}
function MediumOrder { Db "$root\Data\taskisland.db" "select group_concat(Title,'+') from (select Title from Tasks where ListId=2 and Priority=1 order by SortOrder, Id)" }
function Wait-Element([string]$name, [string]$type, [int]$ms = 3000) {
    $until = [DateTime]::UtcNow.AddMilliseconds($ms)
    do {
        $e = Find-TI $name $type
        if ($e) { return $e }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $until)
    return $null
}
function OpenPanel {
    if (Wait-Element 'Add a new task' 'Edit') { return }
    Tray 0x203
    if (Wait-Element 'Add a new task' 'Edit') { return }
    throw 'panel did not open'
}
function Centre([string]$name) {
    $row = Wait-Element $name 'CheckBox'
    if (-not $row) { throw "row not found: $name" }
    $r = $row.Current.BoundingRectangle
    [pscustomobject]@{ X = [int]($r.Left + $r.Width / 2); Y = [int]($r.Top + $r.Height / 2); L = [int]$r.Left; R = [int]$r.Right; T = [int]$r.Top; B = [int]$r.Bottom }
}
function Drag($fromX, $fromY, $toX, $toY) {
    [void][RD.M]::SetCursorPos($fromX, $fromY); Start-Sleep -Milliseconds 150
    [RD.M]::mouse_event(0x2, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 120
    $steps = 12
    for ($i = 1; $i -le $steps; $i++) {
        [void][RD.M]::SetCursorPos([int]($fromX + ($toX - $fromX) * $i / $steps), [int]($fromY + ($toY - $fromY) * $i / $steps))
        Start-Sleep -Milliseconds 40
    }
    Start-Sleep -Milliseconds 200
    [RD.M]::mouse_event(0x4, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 900
}

$root = "$env:TEMP\TaskIslandRowDrag"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @()
Stop-TI
Start-Sleep -Milliseconds 400
python -I "$sp\seed.py" "$root\Data\taskisland.db" | Out-Null
$py = @"
import sqlite3, sys, datetime as dt
c = sqlite3.connect(sys.argv[1])
n = dt.datetime.utcnow().strftime('%Y-%m-%dT%H:%M:%S.0000000Z')
c.execute("insert into Tasks(ListId,Title,Priority,IsCompleted,CreatedAt,UpdatedAt,SortOrder) values(2,'Draft roadmap',1,0,?,?,20)", (n, n))
# No startup reminder: its pill resizes the panel and can move the rows out from under the mouse.
c.execute("update Tasks set DueDate=NULL, DueHasTime=0 where ListId=1 and Title='Renew car insurance'")
c.commit()
"@
$py | python -I - "$root\Data\taskisland.db"
Check 'start order' ((MediumOrder) -eq 'Prepare slides for Monday+Draft roadmap') (MediumOrder)
$p = Start-TI $root @('--pop-seconds','3600','--full-motion')
OpenPanel
Invoke-TI 'Office Work' 'Button'
Start-Sleep -Milliseconds 400
$from = Centre 'Prepare slides for Monday'; $to = Centre 'Draft roadmap'
# Start on the title area: the check box swallows a drag like a normal button would.
$fromX = $from.X + 70
Drag $fromX $from.Y $fromX ($to.B - 8)
Check 'row dragged below Draft roadmap' ((MediumOrder) -eq 'Draft roadmap+Prepare slides for Monday') (MediumOrder)
Check 'panel still open' ((Wait-Element 'Add a new task' 'Edit') -ne $null)
# Dropping the Medium row on a High row is refused.
$draft = Centre 'Draft roadmap'; $high = Centre 'Finish project report'
Drag ($draft.X + 70) $draft.Y ($draft.X + 70) $high.Y
Check 'cross-group drop blocked' ((MediumOrder) -eq 'Draft roadmap+Prepare slides for Monday') (MediumOrder)
# A plain click on the row title selects/focuses the row and does not reorder.
$draft = Centre 'Draft roadmap'
[void][RD.M]::SetCursorPos(($draft.X + 70), $draft.Y); [RD.M]::mouse_event(0x2, 0, 0, 0, [IntPtr]::Zero); [RD.M]::mouse_event(0x4, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 500
Check 'click does not reorder' ((MediumOrder) -eq 'Draft roadmap+Prepare slides for Monday') (MediumOrder)
# Restart: the row order is read from the panel again, not just from the database.
Stop-TI
Start-Sleep -Milliseconds 400
$p = Start-TI $root @('--pop-seconds','3600','--full-motion')
OpenPanel
Invoke-TI 'Office Work' 'Button'
Start-Sleep -Milliseconds 400
$rows = @(Get-TIRows)
$draftIndex = [Array]::IndexOf($rows, 'Draft roadmap')
$slidesIndex = [Array]::IndexOf($rows, 'Prepare slides for Monday')
Check 'order survives restart' ($draftIndex -ge 0 -and $slidesIndex -gt $draftIndex) ($rows -join '+')
Stop-TI
"rowdrag done: $global:fails failure(s)"
