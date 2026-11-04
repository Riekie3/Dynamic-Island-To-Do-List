$sp = $PSScriptRoot
. "$sp\uia.ps1"
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" }
}
function Keys([string]$k) {
    if ((Get-FgTitle) -ne $script:TIProc.Id) { throw "TaskIsland is not the foreground window; refusing to send keys" }
    [System.Windows.Forms.SendKeys]::SendWait($k); Start-Sleep -Milliseconds 500
}
function Add-Task([string]$title) { Set-TIText 'Add a new task' $title; Invoke-TI 'Add task'; Start-Sleep -Milliseconds 400 }
function Db([string]$sql) {
    python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print('|'.join(str(x) for r in c.execute(sys.argv[2]).fetchall() for x in r))" "$root\Data\taskisland.db" $sql
}
function Text-Visible([string]$like) { (Find-TILike $like).Count -gt 0 }
function Chip-Menu([string]$name) { (Find-TI $name 'Button').SetFocus(); Start-Sleep -Milliseconds 250; Keys '+{F10}' }

$root = "$env:TEMP\TaskIslandP3g"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @('--demo','panel','--full-motion','--pop-seconds','3600')

# L9: delete a list together with its tasks
Invoke-TI 'New list'; Set-TIText 'List name' 'Temp'; Invoke-TI 'Save list'; Start-Sleep -Milliseconds 700
Add-Task 'T1'; Add-Task 'T2'
Check 'L2 new list is selected and takes the tasks' ((Db "select count(*) from Tasks t join Lists l on l.Id=t.ListId where l.Name='Temp'") -eq '2')
Chip-Menu 'Temp'
Invoke-TI 'Delete list' 'MenuItem'
Check 'L9 card names the list and the task count' ((Text-Visible 'Delete "Temp"?') -and (Text-Visible 'Delete the list and its 2 tasks'))
Invoke-TI 'Delete the list and its tasks'
Invoke-TI 'Delete list now'
Start-Sleep -Milliseconds 1200
Check 'L9 list and its tasks are gone' ((Db 'select count(*) from Lists') -eq '2' -and (Db 'select count(*) from Tasks') -eq '0')
Check 'L9 deleted list was selected, so All is shown and saved' ((Db "select SettingValue from Settings where SettingKey='SelectedListId'") -eq '0')
Check 'L9 backup made before deleting' (@(Get-ChildItem "$root\Backups" -Filter 'pre-delete-*.db').Count -eq 1)
# the backup really holds the deleted list
$bk = (Get-ChildItem "$root\Backups" -Filter 'pre-delete-*.db' | Select-Object -First 1).FullName
$bn = python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print(c.execute(""select count(*) from Tasks t join Lists l on l.Id=t.ListId where l.Name='Temp'"").fetchone()[0])" $bk
Check 'L9 backup contains the deleted list tasks' ($bn -eq '2') $bn

# L9: delete the default list, moving its tasks; the default moves to the first remaining list
Invoke-TI 'Personal'; Start-Sleep -Milliseconds 400
Add-Task 'P1'
Invoke-TI 'Office Work'; Start-Sleep -Milliseconds 400
Add-Task 'O1'
$def = Db "select SettingValue from Settings where SettingKey='DefaultListId'"
$pid1 = Db "select Id from Lists where Name='Personal'"
Check 'L9 Personal is the default list' ($def -eq $pid1) "$def vs $pid1"
Chip-Menu 'Personal'
Invoke-TI 'Delete list' 'MenuItem'
Invoke-TI 'Delete list now'
Start-Sleep -Milliseconds 1200
Check 'L9 tasks moved to the remaining list' ((Db "select count(*) from Tasks t join Lists l on l.Id=t.ListId where l.Name='Office Work'") -eq '2')
Check 'L9 default list moved to the remaining list' ((Db "select SettingValue from Settings where SettingKey='DefaultListId'") -eq (Db "select Id from Lists"))

# L10: the last list cannot be deleted
Chip-Menu 'Office Work'
Invoke-TI 'Delete list' 'MenuItem'
Start-Sleep -Milliseconds 500
Check 'L10 last list cannot be deleted (message shown)' (Text-Visible "The last remaining list*")
Check 'L10 and it is still there' ((Db 'select count(*) from Lists') -eq '1')

# quick add still works with only one list (default list repaired)
Invoke-TI 'All'; Start-Sleep -Milliseconds 400
Add-Task 'After deletes'
Check 'Add works after the default list changed' ((Db "select count(*) from Tasks where Title='After deletes'") -eq '1')

"--- fails: $global:fails"
Stop-TI

