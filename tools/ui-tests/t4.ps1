$sp = $PSScriptRoot
. "$sp\uia.ps1"
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" }
}
function Focus-Row([string]$title) { (Find-TI $title 'CheckBox').SetFocus(); Start-Sleep -Milliseconds 300 }
function Keys([string]$k) {
    if ((Get-FgTitle) -ne $script:TIProc.Id) { throw "TaskIsland is not the foreground window; refusing to send keys" }
    [System.Windows.Forms.SendKeys]::SendWait($k); Start-Sleep -Milliseconds 600
}
function Add-Task([string]$title) { Set-TIText 'Add a new task' $title; Invoke-TI 'Add task'; Start-Sleep -Milliseconds 350 }
function Db([string]$sql) {
    python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); c.execute('pragma foreign_keys=on'); print('|'.join(str(x) for r in c.execute(sys.argv[2]).fetchall() for x in r)); c.commit()" "$root\Data\taskisland.db" $sql
}
function Text-Visible([string]$like) { (Find-TILike $like).Count -gt 0 }
function Select-Chip([string]$n) { Invoke-TI $n; Start-Sleep -Milliseconds 500 }
function Radio-Selected([string]$n) { (Find-TI $n 'RadioButton').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected }

$root = "$env:TEMP\TaskIslandP3d"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @('--demo','panel','--full-motion','--pop-seconds','3600')

Check 'D1 database file is in the Data folder' (Test-Path "$root\Data\taskisland.db")
Select-Chip 'Personal'
Add-Task 'A'; Add-Task 'B'; Add-Task 'C'
Check 'T9 same-priority tasks keep insertion order' (((Get-TIRows) -join '|') -eq 'A|B|C') ((Get-TIRows) -join '|')

# T10 reorder with Alt+Up / Alt+Down inside the group
Focus-Row 'C'; Keys '%{UP}'
Check 'T10 Alt+Up moves C above B' (((Get-TIRows) -join '|') -eq 'A|C|B') ((Get-TIRows) -join '|')
Check 'T10 order saved (SortOrder)' ((Db "select Title from Tasks order by SortOrder") -eq 'A|C|B') (Db "select Title from Tasks order by SortOrder")
Focus-Row 'A'; Keys '%{UP}'
Check 'T10 Alt+Up on the first row does nothing' (((Get-TIRows) -join '|') -eq 'A|C|B')
Focus-Row 'A'; Keys '%{DOWN}'
Check 'T10 Alt+Down moves A below C' (((Get-TIRows) -join '|') -eq 'C|A|B') ((Get-TIRows) -join '|')

# not in All lists
Select-Chip 'All'
Focus-Row 'C'; Keys '%{UP}'; Keys '%{DOWN}'
Check 'T10 reordering is off in All lists' ((Db "select Title from Tasks order by SortOrder") -eq 'C|A|B') (Db "select Title from Tasks order by SortOrder")
Select-Chip 'Personal'

# T11 a different priority group is a different group: a High task is not moved among the Mediums
Invoke-TI 'Priority'; Invoke-TI 'Priority'    # Medium -> Low -> High
Add-Task 'H'
Check 'T11 High task sits above the Mediums' (((Get-TIRows) -join '|') -eq 'H|C|A|B') ((Get-TIRows) -join '|')
Focus-Row 'C'; Keys '%{UP}'
Check 'T11 Alt+Up on the first Medium does not cross into High' (((Get-TIRows) -join '|') -eq 'H|C|A|B') ((Get-TIRows) -join '|')

# state to survive a crash: B done, Completed filter selected
Toggle-TI 'B'
Start-Sleep -Milliseconds 2800
Invoke-TI 'Completed' 'RadioButton'
Start-Sleep -Milliseconds 500
Check 'T12 Completed filter shows B' (((Get-TIRows) -join '|') -eq 'B') ((Get-TIRows) -join '|')

# D3 crash: kill -f, relaunch
Stop-Process -Id $p.Id -Force
Start-Sleep -Milliseconds 800
$p = Start-TI $root @('--demo','panel','--full-motion','--pop-seconds','3600')
Check 'D3 data survives taskkill /f; filter remembered (Completed)' (((Get-TIRows) -join '|') -eq 'B' -and (Radio-Selected 'Completed')) ((Get-TIRows) -join '|')
Check 'L5 selected list remembered after restart' (-not (Text-Visible 'List for new tasks'))
Invoke-TI 'Active' 'RadioButton'
Start-Sleep -Milliseconds 500
Check 'D2 tasks and their order survive the restart' (((Get-TIRows) -join '|') -eq 'H|C|A') ((Get-TIRows) -join '|')

# failure path: the list vanishes behind the app's back, then a task is added to it
Select-Chip 'All'
Invoke-TI 'List for new tasks'
Invoke-TI 'New tasks go to Office Work' 'MenuItem'
Db "delete from Lists where Name='Office Work'" | Out-Null
Add-Task 'Should fail'
Start-Sleep -Milliseconds 800
Check 'Failure: error shown inline' (Text-Visible "Couldn't add the task*") ((Find-TILike '*add*' | ForEach-Object { $_.Current.Name }) -join '; ')
Check 'Failure: nothing saved' ((Db "select count(*) from Tasks where Title='Should fail'") -eq '0')
Check 'Failure: UI reloaded from the database (stale chip gone)' ((Find-TI 'Office Work' 'Button') -eq $null)
Check 'Failure: no half-added row' (-not ((Get-TIRows) -contains 'Should fail'))
$log = Get-Content (Get-ChildItem "$root\Logs" | Select-Object -First 1).FullName -Raw
Check 'Failure: logged' ($log -match 'add the task failed')

"--- fails: $global:fails"
Stop-TI

