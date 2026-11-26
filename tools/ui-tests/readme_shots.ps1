# README screenshots: each shot starts a Debug build on a fresh test data folder with the sample tasks (seed.py) and saves a
# PNG of the TaskIsland window itself (--snap / --snap-settings render the window onto a plain backdrop; no screen capture).
# usage: powershell -File tools\ui-tests\readme_shots.ps1   -> docs\screenshots\readme-*.png
$sp = $PSScriptRoot
$repo = Split-Path -Parent (Split-Path -Parent $sp)
$out = Join-Path $repo 'docs\screenshots'
$exe = Join-Path $repo 'src\TaskIsland.App\bin\Debug\net10.0-windows\TaskIsland.exe'

function Shot([string]$name, [string[]]$appArgs, [string]$delay = '2200', [switch]$NoSeed, [switch]$NoReminders, [hashtable]$Settings) {
    $root = Join-Path $env:TEMP "TaskIslandShots\$name"
    if (Test-Path $root) { Remove-Item $root -Recurse -Force }
    $env:TASKISLAND_ROOT = $root
    # first start creates the database, then the sample tasks go in
    $p = Start-Process $exe -ArgumentList '--pop-seconds', '3600' -PassThru; Start-Sleep -Seconds 4; Stop-Process -Id $p.Id -Force; Start-Sleep -Milliseconds 800
    if (-not $NoSeed) { python -I -W ignore "$sp\seed.py" (Join-Path $root 'Data\taskisland.db') | Out-Null }
    # the sample data has an overdue task: its start-up reminder would take the notch in the resting-state shots
    if ($NoReminders) { python -I "$sp\setkey.py" (Join-Path $root 'Data\taskisland.db') RemindersEnabled false | Out-Null }
    if ($Settings) {
        foreach ($k in $Settings.Keys) { python -I "$sp\setkey.py" (Join-Path $root 'Data\taskisland.db') $k $Settings[$k] | Out-Null }
    }
    $png = Join-Path $out "readme-$name.png"
    $p = Start-Process $exe -ArgumentList (@('--pop-seconds', '3600', '--snap-delay', $delay) + $appArgs + @('--snap', $png)) -PassThru
    if (-not $p.WaitForExit(20000)) { Stop-Process -Id $p.Id -Force; "TIMEOUT $name" } else { "saved $png" }
}
function SettingsShot([string]$name, [string]$page, [string]$theme) {
    $root = Join-Path $env:TEMP "TaskIslandShots\$name"
    if (Test-Path $root) { Remove-Item $root -Recurse -Force }
    $env:TASKISLAND_ROOT = $root
    $png = Join-Path $out "readme-$name.png"
    $p = Start-Process $exe -ArgumentList '--pop-seconds', '3600', '--theme', $theme, '--settings', $page, '--snap-settings', $png -PassThru
    if (-not $p.WaitForExit(20000)) { Stop-Process -Id $p.Id -Force; "TIMEOUT $name" } else { "saved $png" }
}

Shot 'notch' @('--theme', 'dark') -NoReminders
# Free placement: a round pill in the middle and a flat pill on the left edge.
Shot 'floating' @('--theme', 'dark') -NoReminders -Settings @{ HorizontalOffset = '0'; TopMargin = '300' }
Shot 'side' @('--theme', 'dark') -NoReminders -Settings @{ HorizontalOffset = '-4000'; TopMargin = '300' }
Shot 'face-happy' @('--theme', 'dark', '--demo', 'face:Happy') '900' -NoReminders
Shot 'peek' @('--theme', 'dark', '--demo', 'peek') '1500' -NoReminders
Shot 'panel-dark' @('--theme', 'dark', '--demo', 'panel')
Shot 'panel-light' @('--theme', 'light', '--demo', 'panel')
Shot 'edit-dark' @('--theme', 'dark', '--demo', 'edit') '2600'
SettingsShot 'settings-appearance' 'appearance' 'dark'
SettingsShot 'settings-reminders' 'reminders' 'dark'
# the reminder: half the screen wide with the red border at its bright moment
& "$sp\alert_snap.ps1" -Out (Join-Path $out 'readme-reminder.png') -Delay 1750 | Out-Null
"saved $(Join-Path $out 'readme-reminder.png')"
