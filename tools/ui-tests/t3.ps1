$sp = $PSScriptRoot
. "$sp\uia.ps1"
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" }
}
function Focus-Row([string]$title) { (Find-TI $title 'CheckBox').SetFocus(); Start-Sleep -Milliseconds 300 }
function Keys([string]$k) {
    if ((Get-FgTitle) -ne $script:TIProc.Id) { throw "TaskIsland is not the foreground window; refusing to send keys" }
    [System.Windows.Forms.SendKeys]::SendWait($k); Start-Sleep -Milliseconds 500
}
function Add-Task([string]$title) { Set-TIText 'Add a new task' $title; Invoke-TI 'Add task'; Start-Sleep -Milliseconds 350 }
function Db([string]$sql) {
    python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print('|'.join(str(x) for r in c.execute(sys.argv[2]).fetchall() for x in r))" "$root\Data\taskisland.db" $sql
}
function Text-Visible([string]$like) { (Find-TILike $like).Count -gt 0 }
function Chip-Menu([string]$name) {
    (Find-TI $name 'Button').SetFocus(); Start-Sleep -Milliseconds 250
    Keys '+{F10}'
}
function Chip-Count([string]$name) {
    $all = @(Get-TIAll); $texts = @()
    for ($i = 0; $i -lt $all.Count; $i++) {
        if ($all[$i].Current.Name -eq $name -and $all[$i].Current.ControlType.ProgrammaticName -eq 'ControlType.Button') {
            $texts = @($all[($i+1)..($i+4)] | Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.Text' } | ForEach-Object { $_.Current.Name })
            return $texts[1]
        }
    }
}
function Select-Chip([string]$n) { Invoke-TI $n; Start-Sleep -Milliseconds 500 }

$root = "$env:TEMP\TaskIslandP3c"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @('--demo','panel','--full-motion','--pop-seconds','3600')

# tasks: Office Work gets Report A (Medium) and Report B (High); Personal gets Groceries
Select-Chip 'Office Work'
Add-Task 'Report A'
Invoke-TI 'Priority'; Invoke-TI 'Priority'     # Medium -> Low -> High
Add-Task 'Report B'
Select-Chip 'Personal'
Add-Task 'Groceries'

# L5 switching shows only that list; selection saved
$rows = Get-TIRows
Check 'L5 Personal shows only its task' (($rows -join '|') -eq 'Groceries') ($rows -join '|')
Select-Chip 'Office Work'
$rows = Get-TIRows
Check 'L5 Office Work shows its two tasks, High first' (($rows -join '|') -eq 'Report B|Report A') ($rows -join '|')
Start-Sleep -Milliseconds 400
Check 'L5 selection saved' ((Db "select SettingValue from Settings where SettingKey='SelectedListId'") -eq (Db "select Id from Lists where Name='Office Work'"))

# L11 counts
Check 'L11 chip counts' ((Chip-Count 'Office Work') -eq '2' -and (Chip-Count 'Personal') -eq '1' -and (Chip-Count 'All') -eq '3') "$(Chip-Count 'Office Work') $(Chip-Count 'Personal') $(Chip-Count 'All')"

# L6 All lists
Select-Chip 'All'
$rows = Get-TIRows
Check 'L6 All: High first (grouped by list order), then Medium' (($rows -join '|') -eq 'Groceries|Report B|Report A') ($rows -join '|')
Check 'L6 rows show the list name' ((Find-TILike 'Office Work').Count -ge 4)

# L7 quick add in All uses the chosen list
Check 'L7 quick-add list defaults to Personal' ((Find-TI 'List for new tasks' 'Button') -ne $null -and (Find-TILike 'Personal').Count -ge 3)
Invoke-TI 'List for new tasks'
Invoke-TI 'New tasks go to Office Work' 'MenuItem'
Add-Task 'From All'
Check 'L7 task went to Office Work' ((Db "select l.Name from Tasks t join Lists l on l.Id=t.ListId where t.Title='From All'") -eq 'Office Work')
Select-Chip 'Personal'
Add-Task 'Personal one'
Check 'L7 task added while Personal is selected goes to Personal' ((Db "select l.Name from Tasks t join Lists l on l.Id=t.ListId where t.Title='Personal one'") -eq 'Personal')

# L8 edit changes the list; the row leaves the Personal view
Focus-Row 'Groceries'
Invoke-TI 'Edit task'
Invoke-TI 'List Office Work'
Invoke-TI 'Save task'
Start-Sleep -Milliseconds 900
Check 'L8 edit moved the task (database)' ((Db "select l.Name from Tasks t join Lists l on l.Id=t.ListId where t.Title='Groceries'") -eq 'Office Work')
Check 'L8 row left the Personal view' (-not ((Get-TIRows) -contains 'Groceries'))

# bad time with a date set is a time error
Focus-Row 'Personal one'
Invoke-TI 'Edit task'
Invoke-TI 'Due date'; Invoke-TI 'Due today'
Set-TIText 'Due time' '25:99'
Invoke-TI 'Save task'
Check 'T4 bad time with a date is rejected with a message' (Text-Visible 'Time must look like*')
Invoke-TI 'Cancel edit'

# L12 keyboard
Select-Chip 'All'
Keys '^1'; Start-Sleep -Milliseconds 300
Check 'L12 Ctrl+1 selects the first list' ((Db "select SettingValue from Settings where SettingKey='SelectedListId'") -eq (Db "select Id from Lists order by SortOrder limit 1"))
Keys '^2'; Start-Sleep -Milliseconds 300
Check 'L12 Ctrl+2 selects the second list' ((Db "select SettingValue from Settings where SettingKey='SelectedListId'") -eq (Db "select Id from Lists order by SortOrder limit 1 offset 1"))
Keys '^0'; Start-Sleep -Milliseconds 300
Check 'L12 Ctrl+0 selects All' ((Db "select SettingValue from Settings where SettingKey='SelectedListId'") -eq '0')
Keys '^{TAB}'; Start-Sleep -Milliseconds 300
Check 'L12 Ctrl+Tab goes to the next chip' ((Db "select SettingValue from Settings where SettingKey='SelectedListId'") -eq (Db "select Id from Lists order by SortOrder limit 1"))
Keys '^+{TAB}'; Start-Sleep -Milliseconds 300
Check 'L12 Ctrl+Shift+Tab goes back to All' ((Db "select SettingValue from Settings where SettingKey='SelectedListId'") -eq '0')

# L4 rename + recolour + move via the chip menu
Chip-Menu 'Office Work'
Check 'L4 chip menu opens (keyboard)' ((Find-TI 'Rename list' 'MenuItem') -ne $null)
Invoke-TI 'Rename list' 'MenuItem'
Set-TIText 'List name' 'Work'
Invoke-TI 'Colour #FF7A45'
Invoke-TI 'Save list'
Start-Sleep -Milliseconds 700
Check 'L4 renamed and recoloured' ((Db "select Name || ' ' || ColorHex from Lists where Id=2") -eq 'Work #FF7A45') (Db 'select Name from Lists')
Chip-Menu 'Work'
Invoke-TI 'Move list left' 'MenuItem'
Start-Sleep -Milliseconds 600
Check 'L4 move left reorders the lists' ((Db 'select Name from Lists order by SortOrder') -eq 'Work|Personal') (Db 'select Name from Lists order by SortOrder')
Chip-Menu 'Work'
Check 'L4 move left is disabled at the left end' ((Find-TI 'Move list left' 'MenuItem').Current.IsEnabled -eq $false)
Invoke-TI 'Move list right' 'MenuItem'
Start-Sleep -Milliseconds 600
Check 'L4 move right reorders back' ((Db 'select Name from Lists order by SortOrder') -eq 'Personal|Work')

# L9 delete a list, moving its tasks; backup is made first
$workTasks = Db "select count(*) from Tasks where ListId=2"
Chip-Menu 'Work'
Invoke-TI 'Delete list' 'MenuItem'
Check 'L9 delete card asks (move is the default)' (Text-Visible 'Delete "Work"?')
Invoke-TI 'Delete list now'
Start-Sleep -Milliseconds 1200
Check 'L9 list gone, its tasks now in Personal' ((Db 'select count(*) from Lists') -eq '1' -and (Db 'select count(*) from Tasks where ListId=1') -eq $workTasks + ' ' -or (Db 'select count(*) from Tasks where ListId=1') -ge $workTasks) "$(Db 'select count(*) from Tasks where ListId=1') vs $workTasks"
$bk = @(Get-ChildItem "$root\Backups" -Filter 'pre-delete-*.db').Count
Check 'L9 backup made first' ($bk -eq 1) "$bk"

# L10 the last list cannot be deleted
Chip-Menu 'Personal'
Invoke-TI 'Delete list' 'MenuItem'
Start-Sleep -Milliseconds 500
Check 'L10 last list cannot be deleted' (Text-Visible "The last remaining list*")

"--- fails: $global:fails"
Stop-TI

