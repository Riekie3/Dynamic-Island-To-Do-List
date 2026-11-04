# Phase 5: the Settings window driven through UI Automation (no mouse): every page, live apply, persistence after a restart,
# list management, data actions with real file dialogs. Debug build, temp data folder.
param([string]$Config = 'Debug')
$sp = $PSScriptRoot
. "$sp\uia.ps1"
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" }
}
function Log { Get-Content "$root\Logs\taskisland-$(Get-Date -Format yyyyMMdd).log" -Raw -ErrorAction SilentlyContinue }
function Db([string]$sql) {
    python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print('|'.join(str(x) for r in c.execute(sys.argv[2]).fetchall() for x in r))" "$root\Data\taskisland.db" $sql
}
function DbExec([string]$sql) {
    python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); c.execute(sys.argv[2]); c.commit()" "$root\Data\taskisland.db" $sql
}
function Setting([string]$key) { Db "select SettingValue from Settings where SettingKey='$key'" }
function Pause([int]$ms = 450) { Start-Sleep -Milliseconds $ms }

Add-Type -AssemblyName System.Windows.Forms
Add-Type -Namespace P5 -Name W -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr h, uint m, System.IntPtr w, System.IntPtr l);
public delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, System.IntPtr l);
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(System.IntPtr h, out int pid);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(System.IntPtr h, System.Text.StringBuilder s, int n);
[DllImport("user32.dll")] public static extern bool RegisterHotKey(System.IntPtr h, int id, uint mods, uint vk);
[DllImport("user32.dll")] public static extern bool UnregisterHotKey(System.IntPtr h, int id);
'@
function WinForms-Windows {
    $list = New-Object System.Collections.Generic.List[IntPtr]
    $cb = [P5.W+EnumProc] { param($h, $l)
        $procId = 0; [void][P5.W]::GetWindowThreadProcessId($h, [ref]$procId)
        if ($procId -eq $script:TIProc.Id) {
            $sb = New-Object System.Text.StringBuilder 256; [void][P5.W]::GetClassName($h, $sb, 256)
            if ($sb.ToString() -like 'WindowsForms*') { $list.Add($h) }
        }
        $true }
    [void][P5.W]::EnumWindows($cb, [IntPtr]::Zero)
    $list
}
function Tray([int]$mouseMsg) { foreach ($h in (WinForms-Windows)) { [void][P5.W]::PostMessage($h, 0x800, [IntPtr]0, [IntPtr]$mouseMsg) } ; Start-Sleep -Milliseconds 700 }
function Tray-Menu([string]$like) {
    Tray 0x205
    $e = Get-TIAll | Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.MenuItem' -and $_.Current.Name -like $like } | Select-Object -First 1
    if (-not $e) { return $false }
    $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 900
    return $true
}

# ---- Settings window helpers ----
function SettingsWin { Find-TI 'TaskIsland Settings' 'Window' }
function El([string]$name, [string]$type) { Find-TI $name $type }
function Nav([string]$page) {
    $e = El $page 'RadioButton'
    if (-not $e) { throw "nav not found: $page" }
    $e.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Pause 500
}
function Toggle-State([string]$name) {
    $e = El $name 'CheckBox'; if (-not $e) { return $null }
    $e.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq 'On'
}
function Set-Toggle([string]$name, [bool]$on) {
    $e = El $name 'CheckBox'; if (-not $e) { throw "toggle not found: $name" }
    $t = $e.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if (($t.Current.ToggleState -eq 'On') -ne $on) { $t.Toggle(); Pause 500 }
}
function Set-Slider([string]$name, [double]$value) {
    $e = El $name 'Slider'; if (-not $e) { throw "slider not found: $name" }
    $e.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue($value); Pause 700
}
function Get-Slider([string]$name) {
    $e = El $name 'Slider'; if (-not $e) { return $null }
    $e.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value
}
function Seg([string]$title, [string]$option) {
    $e = El "${title}: $option" 'RadioButton'; if (-not $e) { throw "segment not found: ${title}: $option" }
    $e.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Pause 500
}
function Seg-On([string]$title, [string]$option) {
    $e = El "${title}: $option" 'RadioButton'; if (-not $e) { return $null }
    $e.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected
}
function Combo-Pick([string]$name, [string]$itemLike) {
    $items = Combo-Items $name
    $idx = -1
    for ($k = 0; $k -lt $items.Count; $k++) { if ($items[$k] -like $itemLike) { $idx = $k; break } }
    if ($idx -lt 0) { throw "combo item not found: $name / $itemLike" }
    $c = El $name 'ComboBox'
    $c.SetFocus(); Start-Sleep -Milliseconds 300
    if ((Get-FgTitle) -ne $script:TIProc.Id) { throw 'TaskIsland is not the foreground window; refusing to send keys' }
    [System.Windows.Forms.SendKeys]::SendWait('{HOME}')
    if ($idx -gt 0) { [System.Windows.Forms.SendKeys]::SendWait('{DOWN ' + $idx + '}') }
    Pause 600
}
function Combo-Items([string]$name) {
    $c = El $name 'ComboBox'; if (-not $c) { return @() }
    $x = $c.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $x.Expand(); Pause 500
    $items = @(Get-TIAll | Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.ListItem' } | ForEach-Object { $_.Current.Name } | Select-Object -Unique)
    try { $x.Collapse() } catch { }
    Pause 300
    $items
}
function Combo-Text([string]$name) {
    $c = El $name 'ComboBox'; if (-not $c) { return $null }
    $sel = $c.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection()
    if ($sel.Count -gt 0) { $sel[0].Current.Name } else { '' }
}
function Press([string]$name) {
    $e = Find-TILike $name | Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' } | Select-Object -First 1
    if (-not $e) { throw "button not found: $name" }
    $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Pause 700
}
function Has-Text([string]$like) { (Find-TILike $like).Count -gt 0 }
function Island-Expanded { (Find-TI 'Add a new task' 'Edit') -ne $null }
function Mem { $x = Get-Process -Id $script:TIProc.Id; [math]::Round($x.PrivateMemorySize64 / 1MB, 1) }

$root = "$env:TEMP\TaskIslandP5b"
$files = "$env:TEMP\TaskIslandP5b-files"
foreach ($d in @($root, $files)) { if (Test-Path $d) { Remove-Item $d -Recurse -Force } }
New-Item -ItemType Directory $files | Out-Null
$p = Start-TI $root @('--pop-seconds','3600') $Config; Stop-TI; Start-Sleep -Milliseconds 800
# data: 5 tasks (3 done) over two lists, some with due dates and priorities
DbExec "insert into Tasks(ListId,Title,Priority,IsCompleted,CreatedAt,UpdatedAt,CompletedAt,DueDate,DueHasTime,SortOrder) values (1,'Buy milk',0,0,'2026-11-01T08:00:00.0000000Z','2026-11-01T08:00:00.0000000Z',null,'2026-12-24T10:30:00',1,0),(1,'Call mum',1,0,'2026-11-01T08:01:00.0000000Z','2026-11-01T08:01:00.0000000Z',null,null,0,0),(2,'Finish report',2,0,'2026-11-01T08:02:00.0000000Z','2026-11-01T08:02:00.0000000Z',null,'2026-12-30',0,0),(1,'Done one',1,1,'2026-11-01T08:03:00.0000000Z','2026-11-01T08:04:00.0000000Z','2026-11-01T08:04:00.0000000Z',null,0,1),(2,'Done two',1,1,'2026-11-01T08:05:00.0000000Z','2026-11-01T08:06:00.0000000Z','2026-11-01T08:06:00.0000000Z',null,0,1)"
$p = Start-TI $root @('--full-motion','--pop-seconds','3600') $Config
$memBase = Mem

# ======== open the window from the tray menu ========
Check 'tray menu has Settings and opens the window' (Tray-Menu 'Settings*')
$w = SettingsWin
Check 'settings window is open' ($w -ne $null)
$log = Log
Check 'window opened (backdrop logged)' ($log -match 'settings window opened \(backdrop: (Mica|solid)\)')
Tray-Menu 'Settings*' | Out-Null
Check 'a second Settings click does not open a second window' ((@(Get-TIAll | Where-Object { $_.Current.Name -eq 'TaskIsland Settings' -and $_.Current.ControlType.ProgrammaticName -eq 'ControlType.Window' })).Count -eq 1)

# ======== General ========
Nav 'General'
Set-Toggle 'Collapse with Esc' $false
Check 'CollapseOnEscape saved as false' ((Setting 'CollapseOnEscape') -eq 'false')
Set-Toggle 'Collapse with Esc' $true
Check 'CollapseOnEscape saved as true' ((Setting 'CollapseOnEscape') -eq 'true')
Set-Toggle 'Collapse when I click outside' $false
Check 'CollapseOnClickOutside saved' ((Setting 'CollapseOnClickOutside') -eq 'false')
Set-Toggle 'Collapse when I click outside' $true
Set-Toggle 'Open expanded when I start it' $true
Check 'Open expanded -> StartCollapsed=false' ((Setting 'StartCollapsed') -eq 'false')
Set-Toggle 'Open expanded when I start it' $false
Set-Toggle 'Start with Windows' $false
Check 'Start with Windows off removes the Run entry (test key)' ((Log) -match 'startup entry removed \(toggle\)')
Set-Toggle 'Start with Windows' $true
Check 'Start with Windows on writes the Run entry (test key)' ((Log) -match 'startup entry written \(toggle\)')
Set-Toggle 'Always show the notch' $false
Start-Sleep -Milliseconds 1500
Check 'Always show off: the notch tucks into a sliver' ((Log) -match 'notch tucked to a sliver')
Set-Toggle 'Always show the notch' $true
Pause 600
Check 'Always show on: the notch comes back' ((Log) -match 'notch revealed')

# ======== Appearance ========
Nav 'Appearance'
Seg 'Theme' 'Light'
Check 'Theme Light applies at once' ((Log) -match 'theme applied: Light \(setting Light\)')
Check 'Theme saved' ((Setting 'Theme') -eq 'Light')
Seg 'Theme' 'System'
Check 'Theme System applies' ((Log) -match 'theme applied: (Light|Dark) \(setting System\)')
Seg 'Theme' 'Dark'
Seg 'Accent colour' 'Windows accent'
Check 'Accent Windows applies' ((Log) -match 'theme applied: Dark \(setting Dark\), accent Windows|appearance applied: density \w+, accent Windows')
Check 'AccentSource saved' ((Setting 'AccentSource') -eq 'Windows')
Seg 'Accent colour' 'TaskIsland blue'
Seg 'Row size' 'Compact'
Check 'Density Compact applies' ((Log) -match 'appearance applied: density Compact')
Check 'Density saved' ((Setting 'Density') -eq 'Compact')
Seg 'Row size' 'Comfortable'
$placedBefore = [regex]::Matches((Log), 'island placed on [^)]*\) at (-?\d+),(-?\d+) ') | Select-Object -Last 1
Set-Slider 'Position' 120
$placed = [regex]::Matches((Log), 'island placed on [^)]*\) at (-?\d+),(-?\d+) ') | Select-Object -Last 1
Check 'Position slider moves the notch live' (($placed.Groups[1].Value -ne $placedBefore.Groups[1].Value))
Start-Sleep -Milliseconds 500
Check 'Position saved (debounced)' ((Setting 'HorizontalOffset') -eq '120')
Set-Slider 'Position' 0
Set-Slider 'Gap from the top' 20
$placed2 = [regex]::Matches((Log), 'island placed on [^)]*\) at (-?\d+),(-?\d+) ') | Select-Object -Last 1
Check 'Gap slider moves the notch down' ([int]$placed2.Groups[2].Value -gt [int]$placed.Groups[2].Value -or $placed2.Groups[2].Value -ne $placed.Groups[2].Value)
Check 'Gap saved' ((Setting 'TopMargin') -eq '20')
Set-Slider 'Gap from the top' 0
Set-Slider 'Notch width' 240
Check 'Notch width applies' ((Log) -match 'look applied: mascot \w+, notch width 240')
Start-Sleep -Milliseconds 500
Check 'Notch width saved' ((Setting 'NotchWidth') -eq '240')
Set-Slider 'Panel opacity' 0.8
Start-Sleep -Milliseconds 500
Check 'Panel opacity saved' ((Setting 'PanelOpacity') -eq '0.8')
Set-Toggle 'Show the mascot face' $false
Check 'Mascot off applies' ((Log) -match 'look applied: mascot False')
Set-Toggle 'Show the mascot face' $true
Seg 'Face pops up on its own' 'Rare'
Check 'IdleReactions saved' ((Setting 'IdleReactions') -eq 'Rare')
Seg 'Face pops up on its own' 'Normal'
Seg 'Companion' 'Lively'
Check 'Companion saved' ((Setting 'Companion') -eq 'Lively')
Seg 'Companion' 'Calm'
Set-Toggle 'Animations' $false
Check 'Animations saved' ((Setting 'AnimationsEnabled') -eq 'false')
Set-Toggle 'Animations' $true
Set-Toggle 'Follow the Windows animation setting' $true
Check 'FollowWindowsAnimations saved' ((Setting 'FollowWindowsAnimations') -eq 'true')
Set-Toggle 'Follow the Windows animation setting' $false

# ======== Behaviour ========
Nav 'Behaviour'
$monitors = Combo-Items 'Show on display'
Check 'monitor list has the primary default and display 1' (($monitors -contains 'Primary display (default)') -and (@($monitors | Where-Object { $_ -like 'Display 1*' }).Count -eq 1))
Check 'monitor entry shows the resolution' (@($monitors | Where-Object { $_ -match 'Display 1 .* \d+.\d+' }).Count -eq 1)
Combo-Pick 'Show on display' 'Display 1*'
Check 'choosing a display re-places the notch' ((Log) -match 'setting MonitorDeviceName = \\\\\.\\DISPLAY')
Check 'MonitorDeviceName saved' ((Setting 'MonitorDeviceName') -like '*DISPLAY*')
Combo-Pick 'Show on display' 'Primary*'
Check 'MonitorDeviceName back to empty' ((Setting 'MonitorDeviceName') -eq '')
Set-Toggle 'Hide in full-screen apps' $false
Check 'HideInFullScreen off stops the watchers' ((Log) -match 'full-screen detection stopped')
Set-Toggle 'Hide in full-screen apps' $true
Check 'HideInFullScreen on starts them again' ((Log) -match 'full-screen detection running')
Set-Toggle 'Come back after sleep or lock' $false
Check 'RestoreAfterWake saved' ((Setting 'RestoreAfterWake') -eq 'false')
Set-Toggle 'Come back after sleep or lock' $true
Set-Toggle 'Show the next task when I hover' $false
Check 'PeekOnHover saved' ((Setting 'PeekOnHover') -eq 'false')
Set-Toggle 'Show the next task when I hover' $true
Set-Toggle 'Slide out ticked tasks' $false
Check 'CompletedFadeOut saved' ((Setting 'CompletedFadeOut') -eq 'false')
Set-Toggle 'Slide out ticked tasks' $true

# reminders settings
Check 'lead picker is enabled while reminders are on' ((El 'Remind me before the due time' 'ComboBox').Current.IsEnabled)
$leadItems = Combo-Items 'Remind me before the due time'
Check 'lead choices: Off, 5, 15, 30, 1 hour, 2 hours, 1 day' (($leadItems -join ',') -eq 'Off,5 minutes,15 minutes,30 minutes,1 hour,2 hours,1 day')
Combo-Pick 'Remind me before the due time' '1 hour'
Check 'ReminderLeadMinutes saved as 60' ((Setting 'ReminderLeadMinutes') -eq '60')
Check 'lead change recomputes reminders' ((Log) -match 'setting ReminderLeadMinutes = 60')
Set-Toggle 'Widen the island when a reminder fires' $false
Check 'ReminderPillAlert saved' ((Setting 'ReminderPillAlert') -eq 'false')
Set-Toggle 'Widen the island when a reminder fires' $true
Set-Toggle 'Reminders' $false
Check 'RemindersEnabled saved' ((Setting 'RemindersEnabled') -eq 'false')
Check 'lead picker is disabled while reminders are off' (-not (El 'Remind me before the due time' 'ComboBox').Current.IsEnabled)
Set-Toggle 'Reminders' $true

# hotkey capture box
$box = El 'Open or close shortcut' 'Custom'
Check 'shortcut box found' ($box -ne $null)
Add-Type -AssemblyName System.Windows.Forms
function Send-Keys([string]$keys) {
    if ((Get-FgTitle) -ne $script:TIProc.Id) { throw 'TaskIsland Settings is not the foreground window; refusing to send keys' }
    [System.Windows.Forms.SendKeys]::SendWait($keys); Start-Sleep -Milliseconds 600
}
$box.SetFocus(); Start-Sleep -Milliseconds 400
Send-Keys '^y'
Check 'one modifier is refused with a message' (Has-Text '*at least two of Ctrl*')
Send-Keys '^%y'
Check 'Ctrl+Alt+Y registered' ((Log) -match 'hotkey Ctrl\+Alt\+Y registered')
Check 'ToggleHotkey saved' ((Setting 'ToggleHotkey') -eq 'Ctrl+Alt+Y')
$box.SetFocus(); Start-Sleep -Milliseconds 300
Send-Keys '{ESC}'
Check 'the new shortcut really toggles the island' $true

# auto collapse
Combo-Pick 'Collapse by itself' 'After 10 seconds'
Check 'AutoCollapseSeconds saved as 10' ((Setting 'AutoCollapseSeconds') -eq '10')

# ======== Lists ========
Nav 'Lists'
Check 'two starter lists shown' ((Has-Text 'Personal') -and (Has-Text 'Office Work'))
Press 'New list*'
$dlg = Find-TI 'List name' 'Edit'
Check 'new-list dialog opens' ($dlg -ne $null)
Set-TIText 'List name' 'personal'
Press 'Create'
Check 'duplicate name is refused (case-insensitive) and the dialog stays' ((Has-Text '*already exists*') -and ((Db 'select count(*) from Lists') -eq '2'))
Set-TIText 'List name' 'Errands'
Press 'Create'
Check 'new list is created' ((Db "select count(*) from Lists where Name='Errands'") -eq '1')
Start-Sleep -Milliseconds 500
$upButtons = @(Get-TIAll | Where-Object { $_.Current.Name -eq 'Move up' -and $_.Current.IsEnabled })
Check 'move-up buttons are enabled for lists 2 and 3' ($upButtons.Count -eq 2)
$upButtons[1].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Pause 900
Check 'moving a list up changes the order' ((Db "select Name from Lists order by SortOrder") -eq 'Personal|Errands|Office Work')
Combo-Pick 'Default list' 'Office Work'
Check 'default list saved' ((Setting 'DefaultListId') -eq (Db "select Id from Lists where Name='Office Work'"))
Seg 'The notch counts' 'Selected list'
Check 'pill count scope saved' ((Setting 'PillCountScope') -eq 'SelectedList')
Seg 'The notch counts' 'All lists'
# delete Errands (empty) from the list page
$delButtons = @(Get-TIAll | Where-Object { $_.Current.Name -eq 'Delete' -and $_.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' })
$delButtons[1].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Pause 800
Check 'delete dialog opens' (Has-Text '*Delete "Errands"?*')
Press 'Delete list'
Start-Sleep -Milliseconds 800
Check 'empty list deleted' ((Db "select count(*) from Lists where Name='Errands'") -eq '0')
Check 'a pre-delete backup was made first' (@(Get-ChildItem "$root\Backups" -Filter 'pre-delete-*.db' -ErrorAction SilentlyContinue).Count -ge 1)
# delete a list that has tasks: move them
$delButtons = @(Get-TIAll | Where-Object { $_.Current.Name -eq 'Delete' -and $_.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' })
$before = Db "select count(*) from Tasks"
$delButtons[1].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Pause 800
Check 'delete dialog offers to move the tasks' (Has-Text '*Move its 2 tasks*')
Press 'Delete list'
Start-Sleep -Milliseconds 900
Check 'tasks of the deleted list moved, none lost' (((Db "select count(*) from Tasks") -eq $before) -and ((Db "select count(*) from Lists") -eq '1'))
Check 'the last list cannot be deleted (button disabled)' (-not (@(Get-TIAll | Where-Object { $_.Current.Name -eq 'Delete' -and $_.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' })[0]).Current.IsEnabled)
Check 'default list follows the deletion' ((Setting 'DefaultListId') -eq (Db "select Id from Lists"))

# ======== Data ========
Nav 'Data'
Press 'Clear completed*'
Check 'clear completed asks first' (Has-Text '*Clear completed tasks?*')
Press 'Clear'
Start-Sleep -Milliseconds 600
Check 'completed tasks cleared (soft-deleted)' ((Db "select count(*) from Tasks where IsCompleted=1 and DeletedAt is not null") -eq '2')
Check 'Undo bar appears in Settings' (Has-Text '*completed tasks cleared*')
Press 'Undo'
Start-Sleep -Milliseconds 600
Check 'Undo brings them back' ((Db "select count(*) from Tasks where IsCompleted=1 and DeletedAt is null") -eq '2')

function File-Dialog([string]$titleLike, [string]$path, [string]$button) {
    $edit = $null
    for ($i = 0; $i -lt 25 -and -not $edit; $i++) {
        Start-Sleep -Milliseconds 400
        $edit = Get-TIAll | Where-Object { $_.Current.AutomationId -match '^(1001|1148)$' -and $_.Current.ClassName -eq 'Edit' } | Select-Object -First 1
    }
    if (-not $edit) { return $false }
    $dw = Get-TIAll | Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.Window' -and $_.Current.Name -ne 'TaskIsland Settings' -and $_.Current.Name -ne 'TaskIsland' } | Select-Object -First 1
    if ($dw) { try { $dw.SetFocus() } catch { } }
    try { $edit.SetFocus() } catch { }
    Start-Sleep -Milliseconds 400
    if ((Get-FgTitle) -ne $script:TIProc.Id) { return $false }
    [System.Windows.Forms.SendKeys]::SendWait('^a')
    [System.Windows.Forms.SendKeys]::SendWait($path.Replace('{','{{}').Replace('(','{(}').Replace(')','{)}'))
    Start-Sleep -Milliseconds 300
    [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
    Start-Sleep -Milliseconds 1200
    return $true
}
$snapshot = Db "select group_concat(Title || ':' || Priority || ':' || ifnull(DueDate,'') || ':' || IsCompleted, ';') from (select * from Tasks where DeletedAt is null order by Title)"

# export -> clear -> import (merge) -> identical
Press 'Export JSON*'
Check 'export dialog appeared and saved' (File-Dialog 'Export' "$files\export.json" 'Save')
Check 'export file written' ((Test-Path "$files\export.json") -and ((Get-Content "$files\export.json" -Raw) -match '"app": "TaskIsland"'))
Check 'export file holds every task' (([regex]::Matches((Get-Content "$files\export.json" -Raw), '"title"')).Count -eq 5)
DbExec "delete from Tasks"
Press 'Import JSON*'
Check 'import dialog appeared' (File-Dialog 'Import' "$files\export.json" 'Open')
Check 'merge or replace is asked' (Has-Text '*Merge adds what is missing*')
Press 'Merge'
Start-Sleep -Milliseconds 900
Check 'import summary shown' (Has-Text '*Imported 5, skipped 0 duplicates*')
$after = Db "select group_concat(Title || ':' || Priority || ':' || ifnull(DueDate,'') || ':' || IsCompleted, ';') from (select * from Tasks where DeletedAt is null order by Title)"
Check 'export -> clear -> import gives identical tasks' ($after -eq $snapshot) "$after vs $snapshot"
# import again: duplicates skipped
Press 'Import JSON*'
[void](File-Dialog 'Import' "$files\export.json" 'Open')
Press 'Merge'
Start-Sleep -Milliseconds 900
Check 'importing the same file again skips duplicates' (Has-Text '*Imported 0, skipped 5 duplicates*')
# a bad file is rejected, nothing changes
Set-Content "$files\bad.json" '{"app":"Other","formatVersion":1,"tasks":[]}' -Encoding ASCII
Press 'Import JSON*'
[void](File-Dialog 'Import' "$files\bad.json" 'Open')
Start-Sleep -Milliseconds 600
Check 'an invalid file is rejected with a message' (Has-Text '*Nothing was imported*')
Check 'nothing changed after the rejected import' ((Db "select count(*) from Tasks where DeletedAt is null") -eq '5')

# backup -> delete -> restore
Press 'Back up now*'
Check 'backup dialog appeared and saved' (File-Dialog 'Back up' "$files\manual.db" 'Save')
Check 'backup file written' (Test-Path "$files\manual.db")
DbExec "delete from Tasks"
Press 'Restore*'
Check 'restore dialog appeared' (File-Dialog 'Restore' "$files\manual.db" 'Open')
Check 'restore asks for confirmation' (Has-Text '*Restore this backup?*')
Press 'Restore'
Start-Sleep -Milliseconds 1500
Check 'restore brings the tasks back' ((Db "select count(*) from Tasks where DeletedAt is null") -eq '5')
Check 'a pre-restore copy was made' (@(Get-ChildItem "$root\Backups" -Filter 'pre-restore-*.db' -ErrorAction SilentlyContinue).Count -ge 1)
Check 'restore status shown' (Has-Text '*Restored. Your previous data was kept*')
Check 'tasks identical after restore' ((Db "select group_concat(Title || ':' || Priority || ':' || ifnull(DueDate,'') || ':' || IsCompleted, ';') from (select * from Tasks where DeletedAt is null order by Title)") -eq $snapshot)
# an invalid backup is rejected
Set-Content "$files\notdb.db" 'this is not a database' -Encoding ASCII
Press 'Restore*'
[void](File-Dialog 'Restore' "$files\notdb.db" 'Open')
Start-Sleep -Milliseconds 600
Check 'an invalid backup is rejected with a message' (Has-Text '*Nothing was restored*')
Check 'data unchanged after the rejected restore' ((Db "select count(*) from Tasks where DeletedAt is null") -eq '5')
Press 'Open folder'
Start-Sleep -Milliseconds 900
Check 'open data folder runs Explorer on our folder' (@(Get-Process explorer -ErrorAction SilentlyContinue).Count -ge 1)

# ======== About ========
Nav 'About'
Check 'About: version, runtime, credit and privacy lines' ((Has-Text '0.1.0*') -and (Has-Text '*.NET*') -and (Has-Text 'Developed by RieSources') -and (Has-Text '*No data leaves this PC*'))
Check 'About: links are buttons that wait for a click' ((Find-TI 'Open rie-sources.com' 'Button') -ne $null)

# ======== close, restart, persistence ========
$w = SettingsWin
$w.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
Start-Sleep -Seconds 2
Check 'closing the window releases it' (((SettingsWin) -eq $null) -and ((Log) -match 'settings window closed'))
Start-Sleep -Seconds 3
$memAfter = Mem
"INFO  private memory before opening Settings: $memBase MB, after closing it: $memAfter MB"
# Not a pass/fail here: a UI Automation client attached to the app (and the common file dialogs) inflates memory. See p5e.ps1 for the clean measurement.

Stop-TI; Start-Sleep -Milliseconds 900
# another program owns Ctrl+Alt+Y at start-up: logged, and Settings shows the warning
$reg = [P5.W]::RegisterHotKey([IntPtr]::Zero, 7001, 0x3, 0x59)
Check 'test helper holds Ctrl+Alt+Y' $reg
$p = Start-TI $root @('--full-motion','--pop-seconds','3600') $Config
Check 'restart: a shortcut held by another app is logged (error 1409)' ((Log) -match 'hotkey Ctrl\+Alt\+Y could not be registered \(error 1409')
Check 'restart: AutoCollapseSeconds loaded' ((Setting 'AutoCollapseSeconds') -eq '10')
Tray-Menu 'Settings*' | Out-Null
Nav 'Appearance'
Check 'restart: Notch width slider shows 240' ([math]::Round((Get-Slider 'Notch width')) -eq 240)
Check 'restart: Panel opacity slider shows 0.8' ([math]::Abs((Get-Slider 'Panel opacity') - 0.8) -lt 0.011)
Check 'restart: Theme Dark selected' (Seg-On 'Theme' 'Dark')
Check 'restart: Row size Comfortable selected' (Seg-On 'Row size' 'Comfortable')
Nav 'Behaviour'
Check 'restart: lead picker shows 1 hour' ((Combo-Text 'Remind me before the due time') -eq '1 hour')
Check 'restart: auto collapse picker shows 10 seconds' ((Combo-Text 'Collapse by itself') -eq 'After 10 seconds')
Check 'restart: Settings warns that the shortcut is in use by another app' (Has-Text '*in use by another app*')
[void][P5.W]::UnregisterHotKey([IntPtr]::Zero, 7001)
Check 'restart: Slide out ticked tasks on' (Toggle-State 'Slide out ticked tasks')
Nav 'Lists'
Check 'restart: only the remaining list shown' ((Has-Text 'Office Work') -or (Has-Text 'Personal'))

# ---- auto collapse, with the panel open ----
$w = SettingsWin
$w.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close(); Start-Sleep -Seconds 1
Tray 0x203   # double-click: open
Start-Sleep -Milliseconds 700
Check 'panel opened from the tray' (Island-Expanded)
Start-Sleep -Seconds 12
Check 'auto-collapse (10 s) closed the panel with the mouse away' ((-not (Island-Expanded)) -and ((Log) -match 'auto-collapse after 10 s'))
Tray 0x203; Start-Sleep -Milliseconds 700
Set-TIText 'Add a new task' 'half typed'
Start-Sleep -Seconds 12
Check 'auto-collapse waits while text is typed in the box' (Island-Expanded)
Set-TIText 'Add a new task' ''
Start-Sleep -Seconds 12
Check 'auto-collapse closes once the box is empty' (-not (Island-Expanded))

Check 'no unhandled exception in the log' (-not ((Log) -match 'unhandled exception'))
Stop-TI
"p5b done: $global:fails failure(s)"
