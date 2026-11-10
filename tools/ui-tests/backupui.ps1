# Settings > Data: Delete all backups keeps tasks; Reset TaskIsland clears tasks and backups, restores the starter state.
# Uses its own TASKISLAND_ROOT, so the real data folder is never touched. Needs the Debug build.
$ErrorActionPreference = 'Stop'
$sp = $PSScriptRoot
. "$sp\uia.ps1"
$global:fails = 0
$deleteAll = 'Delete all' + [char]0x2026
$resetAll = 'Reset everything' + [char]0x2026

function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" }
}
function Db([string]$db, [string]$sql) {
    python -I -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print('|'.join(str(x) for r in c.execute(sys.argv[2]).fetchall() for x in r))" $db $sql
}
function Wait-Element([string]$name, [string]$type = 'Button', [int]$ms = 5000) {
    $until = [DateTime]::UtcNow.AddMilliseconds($ms)
    do {
        $e = Find-TI $name $type
        if ($e) { return $e }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $until)
    return $null
}
function Click([string]$name, [string]$type = 'Button') {
    $e = Wait-Element $name $type
    if (-not $e) { throw "not found: $type '$name'" }
    $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 700
}

$root = "$env:TEMP\TaskIslandBackupUi"
try {
    if (Test-Path $root) { Remove-Item $root -Recurse -Force }
    $p = Start-TI $root @()
    Stop-TI
    Start-Sleep -Milliseconds 400
    python -I "$sp\seed.py" "$root\Data\taskisland.db" | Out-Null
    $db = "$root\Data\taskisland.db"
    $backups = "$root\Backups"
    New-Item -ItemType Directory -Force $backups | Out-Null
    1..3 | ForEach-Object { Set-Content -Path (Join-Path $backups ("pre-delete-2026010{0}-120000.db" -f $_)) -Value 'x' }
    Set-Content -Path (Join-Path $backups 'manual-20260101-120000.db') -Value 'x'
    $beforeTasks = Db $db "select count(*) from Tasks"

    $p = Start-TI $root @('--settings', 'Data', '--pop-seconds', '3600', '--full-motion')
    Check 'Settings window opened on Data' ((Wait-Element 'Data' 'Text' 8000) -ne $null)
    Check 'Delete all backups button is visible' ((Wait-Element $deleteAll) -ne $null)
    Check 'Reset everything button is visible' ((Wait-Element $resetAll) -ne $null)

    Click $deleteAll
    Click 'Delete all backups'
    Check 'all backup files deleted' (@(Get-ChildItem $backups -File -ErrorAction SilentlyContinue).Count -eq 0) (@(Get-ChildItem $backups -File).Count)
    Check 'tasks kept after deleting backups' ((Db $db 'select count(*) from Tasks') -eq $beforeTasks) (Db $db 'select count(*) from Tasks')
    Check 'status says backups were deleted' ((Find-TILike 'Deleted *backup*') -ne $null)

    Set-Content -Path (Join-Path $backups 'manual-20260102-120000.db') -Value 'x'
    Click $resetAll
    Click 'Reset everything'
    $taskCount = Db $db 'select count(*) from Tasks'
    $lists = Db $db "select group_concat(Name, '+') from (select Name from Lists order by SortOrder)"
    $theme = Db $db "select SettingValue from Settings where SettingKey = 'Theme'"
    $backupCount = @(Get-ChildItem $backups -File -ErrorAction SilentlyContinue).Count
    Check 'tasks cleared by reset' ($taskCount -eq '0') $taskCount
    Check 'starter lists restored' ($lists -eq 'Personal+Office Work') $lists
    Check 'settings returned to the default theme' ($theme -eq 'Dark') $theme
    Check 'backups cleared by reset' ($backupCount -eq 0) $backupCount
    Check 'status says reset finished' ((Find-TILike 'TaskIsland was reset to its first-run state*') -ne $null)
    Check 'Settings still responds after reset' ((Wait-Element $resetAll) -ne $null)
}
finally {
    Stop-TI
}
"backupui done: $global:fails failure(s)"
if ($global:fails -gt 0) { exit 1 }
