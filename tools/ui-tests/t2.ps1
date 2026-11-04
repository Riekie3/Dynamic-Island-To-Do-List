$sp = $PSScriptRoot
. "$sp\uia.ps1"
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" }
}
function Focus-Row([string]$title) { (Find-TI $title 'CheckBox').SetFocus(); Start-Sleep -Milliseconds 300 }
function Keys([string]$k) {
    if ((Get-FgTitle) -ne $script:TIProc.Id) { throw "TaskIsland is not the foreground window; refusing to send keys" }
    [System.Windows.Forms.SendKeys]::SendWait($k); Start-Sleep -Milliseconds 400
}
function Add-Task([string]$title, [string]$prio = '') {
    Set-TIText 'Add a new task' $title
    Invoke-TI 'Add task'
    Start-Sleep -Milliseconds 350
}
function Db([string]$sql, [string]$root) {
    python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print('|'.join(str(x) for r in c.execute(sys.argv[2]).fetchall() for x in r))" "$root\Data\taskisland.db" $sql
}
function Text-Visible([string]$like) { (Find-TILike $like).Count -gt 0 }

$root = "$env:TEMP\TaskIslandP3b"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @('--demo','panel','--full-motion','--pop-seconds','3600')

Add-Task 'Write report'
Add-Task 'Book flights'
Add-Task 'Gym'

# --- T4 edit title, priority, notes, due date + time, list -> Save persists
Focus-Row 'Write report'
Invoke-TI 'Edit task'
Check 'T4 edit form opens' ((Find-TI 'Task title' 'Edit') -ne $null)
Set-TIText 'Task title' 'Write the Q4 report'
Invoke-TI 'Priority High'
Set-TIText 'Notes' 'Include the sales chart'
Invoke-TI 'Due date'
Check 'T4 calendar popup opens' ((Find-TI 'Due tomorrow' 'Button') -ne $null)
Invoke-TI 'Due tomorrow'
Set-TIText 'Due time' '17:30'
Invoke-TI 'List Office Work'
Invoke-TI 'Save task'
Start-Sleep -Milliseconds 900
$rows = Get-TIRows
Check 'T4 edited title shown, High first' ($rows[0] -eq 'Write the Q4 report') ($rows -join '|')
$tomorrow = (Get-Date).Date.AddDays(1).ToString('yyyy-MM-dd')
$row = Db "select Title, Priority, Description, DueDate, DueHasTime, ListId from Tasks where Title like 'Write the%'" $root
Check 'T4 saved to the database' ($row -eq "Write the Q4 report|0|Include the sales chart|${tomorrow}T17:30:00|1|2") $row

# --- T5 cancel discards
Focus-Row 'Gym'
Invoke-TI 'Edit task'
Set-TIText 'Task title' 'Gym session changed'
Invoke-TI 'Cancel edit'
Start-Sleep -Milliseconds 400
Check 'T5 cancel keeps the old title' ((Get-TIRows) -contains 'Gym') ((Get-TIRows) -join '|')

# --- edit via F2 and Esc cancels (not collapse)
Focus-Row 'Gym'
Keys '{F2}'
Check 'F2 opens the edit form' ((Find-TI 'Task title' 'Edit') -ne $null)
Keys '{ESC}'
Check 'Esc closes the edit form without collapsing' ((Find-TI 'Task title' 'Edit') -eq $null -and (Find-TI 'Add a new task' 'Edit') -ne $null)

# --- validation in the edit form
Focus-Row 'Gym'
Invoke-TI 'Edit task'
Set-TIText 'Task title' '   '
Invoke-TI 'Save task'
Check 'Empty title in edit is rejected with a message' (Text-Visible 'The title must be*')
Set-TIText 'Task title' 'Gym'
Set-TIText 'Due time' '25:99'
Invoke-TI 'Save task'
# Gym has no due date, so a time on its own is rejected with the 'pick a date' message (the '09:30' format message needs a date).
Check 'A time without a date is rejected with a message' (Text-Visible 'Pick a date for the time*')
Invoke-TI 'Cancel edit'

# --- T14 clear completed -> confirm -> Undo
Toggle-TI 'Book flights'
Toggle-TI 'Gym'
Start-Sleep -Milliseconds 2800
Invoke-TI 'More options'
Invoke-TI 'Clear completed' 'MenuItem'
Check 'T14 confirm card appears' ((Find-TI 'Confirm' 'Button') -ne $null)
Invoke-TI 'Cancel'
Check 'T14 cancel leaves tasks alone' ((Db 'select count(*) from Tasks where DeletedAt is null' $root) -eq '3')
Invoke-TI 'More options'
Invoke-TI 'Clear completed' 'MenuItem'
Invoke-TI 'Confirm'
Start-Sleep -Milliseconds 900
Check 'T14 cleared (2 soft-deleted)' ((Db 'select count(*) from Tasks where DeletedAt is not null' $root) -eq '2')
Check 'T14 undo bar says so' (Text-Visible '2 completed tasks cleared')
Invoke-TI 'Undo'
Start-Sleep -Milliseconds 600
Check 'T14 undo restores them' ((Db 'select count(*) from Tasks where DeletedAt is not null' $root) -eq '0' -and (Db 'select count(*) from Tasks where IsCompleted=1' $root) -eq '2')

# --- Lists: L2 create
Invoke-TI 'New list'
Set-TIText 'List name' 'Project Alpha'
Invoke-TI 'Save list'
Start-Sleep -Milliseconds 800
Check 'L2 new chip appears' ((Find-TI 'Project Alpha' 'Button') -ne $null)
Check 'L2 saved' ((Db "select Name from Lists order by SortOrder" $root) -eq 'Personal|Office Work|Project Alpha')
Check 'L5 new list is selected (setting saved)' ((Db "select SettingValue from Settings where SettingKey='SelectedListId'" $root) -eq (Db "select Id from Lists where Name='Project Alpha'" $root))

# --- L3 duplicate name any case, and the 21st list
Invoke-TI 'New list'
Set-TIText 'List name' 'project ALPHA'
Invoke-TI 'Save list'
Check 'L3 duplicate name (other case) rejected' (Text-Visible '*already exists*')
Invoke-TI 'Cancel list'
$n = [int](Db 'select count(*) from Lists' $root)
for ($i = $n + 1; $i -le 20; $i++) {
    Invoke-TI 'New list'
    Set-TIText 'List name' "List $i"
    Invoke-TI 'Save list'
}
Check 'L3 reached 20 lists' ((Db 'select count(*) from Lists' $root) -eq '20')
Invoke-TI 'New list'
Set-TIText 'List name' 'One too many'
Invoke-TI 'Save list'
Check 'L3 21st list rejected' (Text-Visible '*at most 20*' -and (Db 'select count(*) from Lists' $root) -eq '20')
Invoke-TI 'Cancel list'

"--- fails: $global:fails"
Stop-TI

