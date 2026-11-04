$sp = $PSScriptRoot
. "$sp\uia.ps1"
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" }
}
function Add-Task([string]$title) { Set-TIText 'Add a new task' $title; Invoke-TI 'Add task'; Start-Sleep -Milliseconds 400 }
function Db([string]$sql) {
    python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); c.execute('pragma foreign_keys=on'); print('|'.join(str(x) for r in c.execute(sys.argv[2]).fetchall() for x in r)); c.commit()" "$root\Data\taskisland.db" $sql
}
function Last-Reaction { $l = Get-Content "$env:TEMP\TaskIsland-debug.log" | Where-Object { $_ -match 'reaction (\w+) onPanel=True' } | Select-Object -Last 1; if ($l -match 'reaction (\w+)') { $Matches[1] } }
function Since([int]$n) { @(Get-Content "$env:TEMP\TaskIsland-debug.log" | Where-Object { $_ -match 'reaction (\w+) onPanel=(\w+)$' } | ForEach-Object { if ($_ -match 'reaction (\w+) onPanel=(\w+)') { "$($Matches[1])/$($Matches[2])" } } | Select-Object -Skip $n) }

$root = "$env:TEMP\TaskIslandP3f"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
Remove-Item "$env:TEMP\TaskIsland-debug.log" -ErrorAction SilentlyContinue
$p = Start-TI $root @('--demo','panel','--full-motion','--pop-seconds','3600')
$n0 = (Since 0).Count

# add -> Wink (typing first shows Focused)
Set-TIText 'Add a new task' 'M1'
Check 'M4 typing shows Focused on the panel face' ((Last-Reaction) -eq 'Focused') (Last-Reaction)
Invoke-TI 'Add task'; Start-Sleep -Milliseconds 400
Check 'M4 add shows Wink' ((Last-Reaction) -eq 'Wink') (Last-Reaction)
foreach ($t in 'M2','M3','M4','M5','M6') { Add-Task $t }
Invoke-TI 'Priority'; Invoke-TI 'Priority'    # High
Add-Task 'H1'

$cnt = (Since 0).Count
Toggle-TI 'M1'; Start-Sleep -Milliseconds 500
Check 'M4 tick shows Happy' ((Last-Reaction) -eq 'Happy') (Last-Reaction)
Toggle-TI 'H1'; Start-Sleep -Milliseconds 500
Check 'M4 High-priority tick shows Love' ((Last-Reaction) -eq 'Love') (Last-Reaction)
Toggle-TI 'M2'; Start-Sleep -Milliseconds 500
Check 'M4 third tick is Happy again' ((Last-Reaction) -eq 'Happy') (Last-Reaction)
Toggle-TI 'M3'; Start-Sleep -Milliseconds 500
Toggle-TI 'M4'; Start-Sleep -Milliseconds 500
Check 'M4 fifth tick within 30 min shows Love' ((Last-Reaction) -eq 'Love') (Last-Reaction)
Toggle-TI 'M5'; Start-Sleep -Milliseconds 500
Check 'M4 sixth tick is Happy' ((Last-Reaction) -eq 'Happy') (Last-Reaction)
Toggle-TI 'M6'; Start-Sleep -Milliseconds 500
Check 'M4 last outstanding task shows Excited' ((Last-Reaction) -eq 'Excited') (Last-Reaction)

# untick does not react
$before = (Since 0).Count
Toggle-TI 'M6'; Start-Sleep -Milliseconds 500
Check 'untick shows no face' ((Since 0).Count -eq $before) "$before -> $((Since 0).Count)"

# editing -> Focused
(Find-TI 'M6' 'CheckBox').SetFocus(); Start-Sleep -Milliseconds 300
Invoke-TI 'Edit task'; Start-Sleep -Milliseconds 400
Check 'M4 editing shows Focused' ((Last-Reaction) -eq 'Focused') (Last-Reaction)
Invoke-TI 'Cancel edit'

# failure -> Sad
Invoke-TI 'Personal'; Start-Sleep -Milliseconds 400
Db "delete from Lists where Name='Personal'" | Out-Null
Add-Task 'Should fail'
Start-Sleep -Milliseconds 600
Check 'M4 save failure shows Sad' ((Last-Reaction) -eq 'Sad') (Last-Reaction)

"sequence: " + ((Since 0) -join ' ')
"--- fails: $global:fails"
Stop-TI

