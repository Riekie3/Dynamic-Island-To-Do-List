# List switcher style (docs/LIST-SWITCHER-PLAN.md): chips or a drop-down. Temporary data folder.
# Switches the setting live in Settings, uses the drop-down (pick, Esc, Ctrl+number, Move down, New list) and checks that
# the panel stays open while the menu is open. Optional -Shots <folder> saves screenshots of both styles.
param([string]$Shots = '')
$sp = $PSScriptRoot
. "$sp\uia.ps1"
Add-Type -AssemblyName System.Drawing
Add-Type -Namespace LS -Name W -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr h, uint m, System.IntPtr w, System.IntPtr l);
public delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, System.IntPtr l);
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(System.IntPtr h, out int pid);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(System.IntPtr h, System.Text.StringBuilder s, int n);
[DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, System.IntPtr e);
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
'@
[void][LS.W]::SetProcessDPIAware()
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') { if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" } }
function Tray([int]$msg) {
    $cb = [LS.W+EnumProc] { param($h, $l)
        $x = 0; [void][LS.W]::GetWindowThreadProcessId($h, [ref]$x)
        if ($x -eq $script:TIProc.Id) { $sb = New-Object System.Text.StringBuilder 256; [void][LS.W]::GetClassName($h, $sb, 256); if ($sb.ToString() -like 'WindowsForms*') { [void][LS.W]::PostMessage($h, 0x800, [IntPtr]0, [IntPtr]$msg) } }
        $true }
    [void][LS.W]::EnumWindows($cb, [IntPtr]::Zero); Start-Sleep -Milliseconds 900
}
function Tray-Menu([string]$like) {
    Tray 0x205
    $e = Get-TIAll | Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.MenuItem' -and $_.Current.Name -like $like } | Select-Object -First 1
    if (-not $e) { return $false }
    $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 900; return $true
}
function Key([byte]$vk, [byte]$mod = 0) {
    if ($mod) { [LS.W]::keybd_event($mod, 0, 0, [IntPtr]::Zero) }
    [LS.W]::keybd_event($vk, 0, 0, [IntPtr]::Zero); [LS.W]::keybd_event($vk, 0, 2, [IntPtr]::Zero)
    if ($mod) { [LS.W]::keybd_event($mod, 0, 2, [IntPtr]::Zero) }
    Start-Sleep -Milliseconds 600
}
function Db([string]$sql) { python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print('|'.join(str(x) for r in c.execute(sys.argv[2]).fetchall() for x in r))" "$root\Data\taskisland.db" $sql }
function Setting([string]$k) { Db "select SettingValue from Settings where SettingKey='$k'" }
function Order { Db "select group_concat(Name,'+') from (select Name from Lists order by SortOrder)" }
function ListsButton { @(Find-TILike 'Lists: *') | Select-Object -First 1 }
function Expanded { (Find-TI 'Add a new task' 'Edit') -ne $null }
function MenuOpen { (Find-TI 'New list from menu' 'Button') -ne $null }
function Invoke-El($e) { $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 600 }
function Seg([string]$title, [string]$option) {
    $e = Find-TI "${title}: $option" 'RadioButton'; if (-not $e) { throw "segment not found: ${title}: $option" }
    $e.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 700
}
function Shot([string]$name, [int]$h = 520) {
    if (-not $Shots) { return }
    $r = (Get-TIWindow).Current.BoundingRectangle
    $x = [int]$r.Left; $w = [int]$r.Width
    $b = New-Object System.Drawing.Bitmap $w, $h; $g = [System.Drawing.Graphics]::FromImage($b)
    $g.CopyFromScreen($x, 0, 0, 0, $b.Size); $g.Dispose(); $b.Save((Join-Path $Shots "$name.png")); $b.Dispose()
}

$root = "$env:TEMP\TaskIslandListSwitcher"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
if ($Shots) { New-Item -ItemType Directory -Force $Shots | Out-Null }
$p = Start-TI $root @('--pop-seconds','3600'); Stop-TI; Start-Sleep -Seconds 1
python -I "$sp\seed.py" "$root\Data\taskisland.db" | Out-Null
$p = Start-TI $root @('--pop-seconds','3600')
Tray 0x203
Check 'default is chips' ((Find-TI 'Office Work' 'Button') -ne $null -and (ListsButton) -eq $null)
Shot 'chips'
Tray 0x203

# Switch live in Settings > Appearance.
Check 'settings opens' (Tray-Menu 'Settings*')
(Find-TI 'Appearance' 'RadioButton').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 500
Seg 'Lists at the top' 'Drop-down'
Check 'setting saved' ((Setting 'ListSwitcher') -eq 'Dropdown') (Setting 'ListSwitcher')
$sw = Find-TI 'TaskIsland Settings' 'Window'
if ($sw) { $sw.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close(); Start-Sleep -Milliseconds 600 }

Tray 0x203
Check 'panel open' (Expanded)
$lb = ListsButton
Check 'drop-down button shown, chips hidden' ($lb -ne $null -and (Find-TI 'Office Work' 'Button') -eq $null) "$($lb.Current.Name)"
Check 'button shows the current list' ($lb.Current.Name -like 'Lists: All lists*') $lb.Current.Name
Shot 'dropdown-closed'

Invoke-El $lb
Check 'menu opens' (MenuOpen)
Check 'panel stays open with the menu open' (Expanded)
Check 'menu lists every list' ((Find-TI 'Personal' 'Button') -ne $null -and (Find-TI 'Project Alpha' 'Button') -ne $null -and (Find-TI 'All' 'Button') -ne $null)
Shot 'dropdown-open'

Invoke-El (Find-TI 'Project Alpha' 'Button')
Check 'picking a list closes the menu' (-not (MenuOpen))
Check 'picking a list switches to it' ((ListsButton).Current.Name -like 'Lists: Project Alpha*') (ListsButton).Current.Name
$alpha = Db "select Id from Lists where Name='Project Alpha'"
Check 'selected list saved' ((Setting 'SelectedListId') -eq $alpha) "$(Setting 'SelectedListId') vs $alpha"

Invoke-El (ListsButton)
Key 0x1B
Check 'Esc closes only the menu' (-not (MenuOpen) -and (Expanded))
Check 'Esc did not switch lists' ((ListsButton).Current.Name -like 'Lists: Project Alpha*')

Key 0x31 0x11   # Ctrl+1
Check 'Ctrl+1 still switches (first list)' ((ListsButton).Current.Name -like 'Lists: Personal*') (ListsButton).Current.Name
Key 0x30 0x11   # Ctrl+0
Check 'Ctrl+0 shows All lists' ((ListsButton).Current.Name -like 'Lists: All lists*') (ListsButton).Current.Name

# Move down from a row's ... menu (the first list: Move up is greyed out there).
$before = Order
Invoke-El (ListsButton)
$more = @(Get-TIAll | Where-Object { $_.Current.Name -eq 'List options' })
Check 'each list has a ... button (not All)' ($more.Count -eq 3) "$($more.Count)"
Invoke-El $more[0]
$down = Find-TI 'Move list down' 'MenuItem'
$up = Find-TI 'Move list up' 'MenuItem'
Check 'Move up is greyed out on the first list' ($up -ne $null -and -not $up.Current.IsEnabled)
Check 'row menu has Move down' ($down -ne $null)
if ($down) { Invoke-El $down; Start-Sleep -Milliseconds 600 }
$after = Order
Check 'Move down changed the order' ($after -ne $before -and $after.StartsWith('Office Work+Personal')) "$before -> $after"
Check 'panel still open after the row menu' (Expanded)

# New list from the menu opens the list editor.
if (-not (MenuOpen)) { Invoke-El (ListsButton) }
Invoke-El (Find-TI 'New list from menu' 'Button')
Check 'New list from the menu opens the editor' ((Find-TI 'List name' 'Edit') -ne $null) 
Key 0x1B

# Back to chips: same order and selected list.
Tray 0x203
Check 'settings opens again' (Tray-Menu 'Settings*')
(Find-TI 'Appearance' 'RadioButton').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 500
Seg 'Lists at the top' 'Chips'
$sw = Find-TI 'TaskIsland Settings' 'Window'
if ($sw) { $sw.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close(); Start-Sleep -Milliseconds 600 }
Tray 0x203
Check 'chips are back' ((Find-TI 'Office Work' 'Button') -ne $null -and (ListsButton) -eq $null)
Check 'order kept' ((Order) -eq $after) (Order)
Stop-TI
"listswitcher done: $global:fails failure(s)"
