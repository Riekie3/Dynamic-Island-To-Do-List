$sp = $PSScriptRoot
. "$sp\uia.ps1"
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" }
}
function Focus-Row([string]$title) { (Find-TI $title 'CheckBox').SetFocus(); Start-Sleep -Milliseconds 300 }
function Rows { ,@(Get-TIRows) }
function Priority-Chip-Text { (Find-TILike 'Priority').Count }

$root = "$env:TEMP\TaskIslandP3"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
Remove-Item "$env:TEMP\TaskIsland-debug.log" -ErrorAction SilentlyContinue
$p = Start-TI $root @('--demo','panel','--full-motion','--pop-seconds','3600')

# --- L1 / T1 add with the button, default priority Medium
Check 'L1 starter lists' ((Find-TI 'Personal' 'Button') -ne $null -and (Find-TI 'Office Work' 'Button') -ne $null)
Check 'L1 no tasks at start' ((Get-TIRows).Count -eq 0)
Set-TIText 'Add a new task' 'Buy milk'
Invoke-TI 'Add task'
Start-Sleep -Milliseconds 500
$rows = Get-TIRows
Check 'T1 add via button' ($rows -contains 'Buy milk') ($rows -join '|')
Check 'T1 box cleared after add' ((Get-TIText 'Add a new task') -eq '')

# --- T2 empty / whitespace rejected
Invoke-TI 'Add task'
Set-TIText 'Add a new task' '    '
Invoke-TI 'Add task'
Check 'T2 empty and whitespace rejected' ((Get-TIRows).Count -eq 1) ((Get-TIRows) -join '|')

# --- T3 priorities: cycle chip Medium -> Low -> High; add one of each
Invoke-TI 'Priority'              # Medium -> Low
Set-TIText 'Add a new task' 'Water plants'
Invoke-TI 'Add task'
Invoke-TI 'Priority'              # Low -> High
Set-TIText 'Add a new task' 'Pay rent'
Invoke-TI 'Add task'
Start-Sleep -Milliseconds 400
$rows = Get-TIRows
Check 'T3/T9 order High, Medium, Low' (($rows -join '|') -eq 'Pay rent|Buy milk|Water plants') ($rows -join '|')

# --- T9 stable order inside a group: add a second Medium and a second High
Invoke-TI 'Priority'              # High -> Medium
Set-TIText 'Add a new task' 'Call dentist'
Invoke-TI 'Add task'
Start-Sleep -Milliseconds 400
$rows = Get-TIRows
Check 'T9 new Medium goes below the existing Medium' (($rows -join '|') -eq 'Pay rent|Buy milk|Call dentist|Water plants') ($rows -join '|')

# --- T13 counters
$footer = (Find-TILike '* total *' | Select-Object -First 1).Current.Name
Check 'T13 footer counts 4 total' ($footer -match '^4 total .* 0 done .* 0%$') $footer

# --- T7 tick -> strikethrough + stays 2 s, then leaves Active
Toggle-TI 'Buy milk'
Start-Sleep -Milliseconds 600
Check 'T7 ticked row still visible at 0.6 s' ((Get-TIRows) -contains 'Buy milk')
Check 'T7 checkbox shows ticked' ((Get-TIChecked 'Buy milk') -eq $true)
$footer = (Find-TILike '* total *' | Select-Object -First 1).Current.Name
Check 'T13 footer updates at once (1 done, 25%)' ($footer -match '^4 total .* 1 done .* 25%$') $footer
Start-Sleep -Milliseconds 2600
Check 'T7 row slid out of Active after ~2 s' (-not ((Get-TIRows) -contains 'Buy milk')) ((Get-TIRows) -join '|')

# --- T8 untick within 2 s cancels the slide-out
Toggle-TI 'Water plants'
Start-Sleep -Milliseconds 800
Toggle-TI 'Water plants'
Start-Sleep -Milliseconds 2800
Check 'T8 untick within 2 s keeps the row' ((Get-TIRows) -contains 'Water plants')
Check 'T8 row is unticked again' ((Get-TIChecked 'Water plants') -eq $false)

# --- T12 filters
Invoke-TI 'Completed' 'RadioButton'
Start-Sleep -Milliseconds 500
$rows = Get-TIRows
Check 'T12 Completed shows only the done task' (($rows -join '|') -eq 'Buy milk') ($rows -join '|')
Invoke-TI 'All' 'RadioButton'
Start-Sleep -Milliseconds 500
$rows = Get-TIRows
Check 'T12 All = active block then completed block' (($rows -join '|') -eq 'Pay rent|Call dentist|Water plants|Buy milk') ($rows -join '|')

# --- T8 untick in Completed view works
Invoke-TI 'Completed' 'RadioButton'
Start-Sleep -Milliseconds 400
Toggle-TI 'Buy milk'
Start-Sleep -Milliseconds 3000
Check 'T8 untick in Completed removes it from that view' ((Get-TIRows).Count -eq 0) ((Get-TIRows) -join '|')
Invoke-TI 'Active' 'RadioButton'
Start-Sleep -Milliseconds 500
$rows = Get-TIRows
Check 'T8 and it is back in Active' ($rows -contains 'Buy milk') ($rows -join '|')

# --- T6 delete -> Undo restores
$before = (Get-TIRows).Count
Focus-Row ((Get-TIRows)[0])
Invoke-TI 'Delete task'
Start-Sleep -Milliseconds 700
Check 'T6 delete removes a row' ((Get-TIRows).Count -eq $before - 1) ((Get-TIRows) -join '|')
Check 'T6 undo bar shown' ((Find-TI 'Undo' 'Button') -ne $null)
Invoke-TI 'Undo'
Start-Sleep -Milliseconds 700
Check 'T6 undo restores the row' ((Get-TIRows).Count -eq $before) ((Get-TIRows) -join '|')

# --- T6 permanent after 5 s
Focus-Row ((Get-TIRows)[0])
Invoke-TI 'Delete task'
Start-Sleep -Milliseconds 6500
Check 'T6 undo bar gone after 5 s' ((Find-TI 'Undo' 'Button') -eq $null)
$dbCount = python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print(c.execute('select count(*) from Tasks').fetchone()[0], c.execute('select count(*) from Tasks where DeletedAt is not null').fetchone()[0])" "$root\Data\taskisland.db"
"db tasks/deleted: $dbCount"
Check 'T6 deleted row purged from the database' ($dbCount -eq '3 0') $dbCount

"--- fails so far: $global:fails"
Stop-TI

