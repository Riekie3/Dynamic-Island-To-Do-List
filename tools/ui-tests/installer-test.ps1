# PLAN.md Phase 7 step 5: install -> runs -> upgrade (refused while running, then over the top, tasks kept) -> uninstall (No)
# keeps data, removes the program, Start menu entry and its own Start with Windows entry -> reinstall finds the tasks ->
# uninstall (Yes) deletes the data. No UAC at any point.
#
# Uses a TEST build of installer\TaskIsland.iss (own AppId, title "TaskIsland Test", install folder, data folder, Run key and
# app lock), so a real TaskIsland on this PC is never touched. Last, the REAL installer is started against a temp folder
# while the real TaskIsland is running: it must refuse (AppMutex) and install nothing.
# Needs publish\win-x64 (tools\publish.ps1). Takes about 2 minutes.
$ErrorActionPreference = 'Stop'
$sp = $PSScriptRoot
$repo = Split-Path -Parent (Split-Path -Parent $sp)
. "$sp\uia.ps1"
Add-Type -Namespace TIInst -Name W -MemberDefinition '[DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr h, uint m, System.IntPtr w, System.IntPtr l);'
$global:fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { "PASS  $name" } else { $global:fails++; "FAIL  $name  $detail" }
}
function Scope([string]$s) {
    # same FNV-1a hash as App.Scope(): a test data folder gets its own single-instance lock
    [uint64]$h = 2166136261
    foreach ($c in $s.ToUpperInvariant().ToCharArray()) { $h = $h -bxor [uint64][int]$c; $h = ($h * 16777619) % 4294967296 }
    '{0:x8}' -f $h
}
function Db([string]$sql) {
    python -I -W ignore -c "import sqlite3,sys; c=sqlite3.connect(sys.argv[1]); print('|'.join(str(x) for r in c.execute(sys.argv[2]).fetchall() for x in r))" (Join-Path $root 'Data\taskisland.db') $sql
}
function RunValue { (Get-ItemProperty -Path "HKCU:\$runKey" -Name TaskIsland -ErrorAction SilentlyContinue).TaskIsland }
function UninstallKey([string]$guid) { Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{$guid}_is1" -ErrorAction SilentlyContinue }
function TestApps { @(Get-Process TaskIsland -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($appDir, 'OrdinalIgnoreCase') }) }
function StopTestApps { TestApps | ForEach-Object { Stop-Process -Id $_.Id -Force }; Start-Sleep -Milliseconds 800 }
function Setup([string]$exe, [string]$logName, [string[]]$more = @()) {
    $log = Join-Path $work $logName
    $p = Start-Process $exe -ArgumentList (@('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$log`"") + $more) -PassThru -Wait
    return @{ Code = $p.ExitCode; Log = (Get-Content $log -Raw -ErrorAction SilentlyContinue) }
}
function WaitGone([scriptblock]$cond, [int]$seconds = 60) {
    $t = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $t) { if (& $cond) { return $true }; Start-Sleep -Milliseconds 500 }
    return $false
}
function Uninstall([string]$guid, [string[]]$mode, [string]$logName) {
    $u = (UninstallKey $guid).UninstallString.Trim('"')
    $log = Join-Path $work $logName
    Start-Process $u -ArgumentList (@($mode) + "/LOG=`"$log`"") | Out-Null
    return $log
}
function AnswerDataQuestion([string]$button, [int]$seconds) {
    $t = (Get-Date).AddSeconds($seconds)
    do {
        # only the uninstaller's own windows (its second phase runs from %TEMP% as _unins.tmp), and only a question that
        # names this test's data folder, so another program's uninstaller is never touched
        $uninsIds = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like '_unins*' -or $_.ProcessName -like 'unins*' } | ForEach-Object { $_.Id })
        $wins = @($script:AE::RootElement.FindAll($script:Tree::Children, [System.Windows.Automation.Condition]::TrueCondition) |
            Where-Object { $uninsIds -contains $_.Current.ProcessId })
        foreach ($w in $wins) {
            $all = $w.FindAll($script:Tree::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
            $text = $all | Where-Object { $_.Current.Name -like 'Also delete your tasks*' -and $_.Current.Name -like "*$root*" } | Select-Object -First 1
            if (-not $text) { continue }
            $b = $all | Where-Object { $_.Current.ClassName -eq 'Button' -and $_.Current.Name -match "^&?$button$" } | Select-Object -First 1
            if ($b) {
                # The message box buttons expose no automation pattern: BM_CLICK goes straight to that one button.
                [void][TIInst.W]::PostMessage([IntPtr]$b.Current.NativeWindowHandle, 0xF5, [IntPtr]::Zero, [IntPtr]::Zero)
                return $true
            }
        }
        Start-Sleep -Milliseconds 700
    } while ((Get-Date) -lt $t)
    return $false
}
function LaunchTest {
    $env:TASKISLAND_ROOT = $root
    $p = Start-Process (Join-Path $appDir 'TaskIsland.exe') -ArgumentList '--pop-seconds', '3600' -PassThru
    Start-Sleep -Seconds 5
    return $p
}

$work = Join-Path $env:TEMP 'TaskIslandInstallTest'
$root = Join-Path $work 'data'
$appDir = Join-Path $env:LOCALAPPDATA 'Programs\TaskIsland Test'
$guid = 'B5E2C7A4-6F1D-4E3B-9A80-2D7C41F0E9B6'   # test AppId only
$realGuid = '39F89B18-992E-4E46-8881-1E8CCC6F1D16'
$runKey = 'Software\TaskIsland-Test\Run'
$mutex = "Local\TaskIsland-7F3A9C2E-SingleInstance-$(Scope $root)"
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\TaskIsland Test.lnk'

# ---- clean start ----
if (UninstallKey $guid) { 'leftover test install found: removing it'; StopTestApps; [void](Uninstall $guid @('/VERYSILENT', '/SUPPRESSMSGBOXES') 'pre-uninstall.log'); [void](WaitGone { -not (UninstallKey $guid) -and -not (Test-Path $appDir) }) }
if (AnswerDataQuestion 'No' 2) { 'an old test uninstaller was still asking about the data: answered No'; Start-Sleep -Seconds 2 }
if (Test-Path $appDir) { throw "$appDir exists without a test install entry; look at it by hand" }
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory $work | Out-Null
Remove-ItemProperty -Path "HKCU:\$runKey" -Name TaskIsland -ErrorAction SilentlyContinue

# ---- build the test installer ----
& "$repo\tools\build-installer.ps1" -Extra @("/DAppIdGuid=$guid", '/DAppTitle=TaskIsland Test', "/DAppMutexName=$mutex",
    "/DRunKey=$runKey", "/DDataDir=$root", '/DOutName=TaskIsland-Setup-TEST') | Out-Null
$testSetup = Join-Path $repo 'installer\Output\TaskIsland-Setup-TEST.exe'
$ver = (Get-Item (Join-Path $repo 'publish\win-x64\TaskIsland.exe')).VersionInfo.ProductVersion.Split('+')[0]

# ---- 1. install ----
$r = Setup $testSetup 'install.log'
Check '1 install exit code 0' ($r.Code -eq 0) "exit $($r.Code)"
Check '1 per-user install, no administrator mode (no UAC)' ($r.Log -match 'Non administrative install mode|Administrative install mode: No') (($r.Log -split "`n" | Select-String 'install mode') -join ' ')
Check '1 program installed in %LOCALAPPDATA%\Programs' (Test-Path (Join-Path $appDir 'TaskIsland.exe'))
Check '1 LICENSE installed, portable README.txt left out' ((Test-Path (Join-Path $appDir 'LICENSE')) -and -not (Test-Path (Join-Path $appDir 'README.txt')))
Check '1 Start menu entry' (Test-Path $startMenu)
$k = UninstallKey $guid
Check '1 Apps list entry: version, publisher' ($k -and $k.DisplayVersion -eq $ver -and $k.Publisher -eq 'RieSources') "$($k.DisplayVersion) / $($k.Publisher)"
Check '1 setup writes no Run value (the app owns it)' (-not (RunValue))

# ---- 2. runs ----
$p = LaunchTest
Check '2 installed app runs' ((TestApps).Count -eq 1)
Check '2 data folder created' (Test-Path (Join-Path $root 'Data\taskisland.db'))
$expectedRun = "`"$(Join-Path $appDir 'TaskIsland.exe')`" --startup"
Check '2 first launch registers Start with Windows for the installed exe' ((RunValue) -eq $expectedRun) "$(RunValue)"
StopTestApps
python -I -W ignore "$sp\seed_p5.py" (Join-Path $root 'Data\taskisland.db') - - '1:Keep me one:864000' '2:Keep me two:864000' | Out-Null
Check '2 two tasks saved' ((Db 'select count(*) from Tasks where DeletedAt is null') -eq '2') (Db 'select count(*) from Tasks')

# ---- 3. upgrade ----
$p = LaunchTest
$r = Setup $testSetup 'upgrade-running.log'
Check '3 upgrade refused while TaskIsland runs (AppMutex)' ($r.Code -ne 0 -and (TestApps).Count -eq 1) "exit $($r.Code)"
StopTestApps
$r = Setup $testSetup 'upgrade.log'
Check '3 upgrade over the top: exit code 0' ($r.Code -eq 0) "exit $($r.Code)"
Check '3 still one Apps list entry, program present' ((UninstallKey $guid) -and (Test-Path (Join-Path $appDir 'TaskIsland.exe')))
Check '3 tasks kept' ((Db 'select count(*) from Tasks') -eq '2')

# ---- 4. uninstall, keep data (silent = No) ----
$log = Uninstall $guid @('/VERYSILENT', '/SUPPRESSMSGBOXES') 'uninstall-keep.log'
Check '4 uninstall finished' (WaitGone { -not (UninstallKey $guid) -and -not (Test-Path $appDir) })
Start-Sleep -Seconds 2
Check '4 program files removed' (-not (Test-Path (Join-Path $appDir 'TaskIsland.exe')))
Check '4 Start menu entry removed' (-not (Test-Path $startMenu))
Check '4 Start with Windows entry removed' (-not (RunValue))
Check '4 data kept (No is the default)' ((Test-Path (Join-Path $root 'Data\taskisland.db')) -and (Db 'select count(*) from Tasks') -eq '2')
Check '4 uninstall log says data kept' ((Get-Content $log -Raw -ErrorAction SilentlyContinue) -match 'Data folder kept')

# ---- 5. reinstall finds the tasks ----
$r = Setup $testSetup 'reinstall.log'
Check '5 reinstall exit code 0' ($r.Code -eq 0) "exit $($r.Code)"
$p = LaunchTest
Check '5 reinstalled app runs on the old data' ((TestApps).Count -eq 1 -and (Db 'select count(*) from Tasks') -eq '2')
$appLog = Get-Content (Join-Path $root "Logs\taskisland-$(Get-Date -Format yyyyMMdd).log") -Raw
Check '5 app log shows the restart on the same database' (([regex]::Matches($appLog, 'started')).Count -ge 3)
StopTestApps

# ---- 6. uninstall, delete data (Yes); a Run value pointing elsewhere is left alone ----
$elsewhere = '"C:\Somewhere\Else\TaskIsland.exe" --startup'
New-Item -Path "HKCU:\$runKey" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\$runKey" -Name TaskIsland -Value $elsewhere
$log = Uninstall $guid @('/SILENT') 'uninstall-delete.log'
$asked = AnswerDataQuestion 'Yes' 40
Check '6 uninstaller asks "Also delete your tasks and backups?" (answered Yes)' $asked
Check '6 uninstall finished' (WaitGone { -not (UninstallKey $guid) -and -not (Test-Path $appDir) })
Start-Sleep -Seconds 2
Check '6 Yes deletes the data folder' (-not (Test-Path $root))
Check '6 a Run value pointing elsewhere is left alone' ((RunValue) -eq $elsewhere) "$(RunValue)"
Remove-Item -Path "HKCU:\Software\TaskIsland-Test" -Recurse -ErrorAction SilentlyContinue
Check '6 install folder gone' (-not (Test-Path (Join-Path $appDir 'TaskIsland.exe')))

# ---- 7. the REAL installer refuses while TaskIsland runs (its lock name matches the app's) ----
# The real app's lock is held either by a running TaskIsland or, if none runs, by this script for these few seconds
# (same name as SingleInstance: Local\TaskIsland-7F3A9C2E-SingleInstance). The probe installs into a temp folder.
$real = Join-Path $repo "installer\Output\TaskIsland-Setup-$ver-x64.exe"
if (-not (Test-Path $real)) { 'SKIP  7 real installer check: build it first (tools\build-installer.ps1)' }
elseif (UninstallKey $realGuid) { 'SKIP  7 real installer check: TaskIsland is already installed on this PC' }
else {
    $created = $false
    $lock = New-Object System.Threading.Mutex($true, 'Local\TaskIsland-7F3A9C2E-SingleInstance', [ref]$created)
    "      app lock held by: $(if ($created) { 'this test' } else { 'your running TaskIsland' })"
    try {
        $probe = Join-Path $work 'real-probe'
        $runBefore = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name TaskIsland -ErrorAction SilentlyContinue).TaskIsland
        $r = Setup $real 'real-probe.log' @("/DIR=`"$probe`"")
        $installed = [bool](UninstallKey $realGuid)
        Check '7 real installer refuses while TaskIsland runs, installs nothing' ($r.Code -ne 0 -and -not $installed -and -not (Test-Path (Join-Path $probe 'TaskIsland.exe'))) "exit $($r.Code)"
        if ($installed) {
            # Safety net: undo it at once (silent = keep data; the Run value is only removed if it points into the probe folder).
            [void](Uninstall $realGuid @('/VERYSILENT', '/SUPPRESSMSGBOXES') 'real-probe-undo.log'); [void](WaitGone { -not (UninstallKey $realGuid) })
        }
        $runAfter = (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name TaskIsland -ErrorAction SilentlyContinue).TaskIsland
        Check '7 your real Start with Windows entry unchanged' ($runBefore -eq $runAfter) "$runBefore -> $runAfter"
        "      setup log: $((($r.Log -split "`n") | Select-String 'running|mutex|Exception|Abort|exit code' | Select-Object -First 3 | ForEach-Object { $_.Line.Trim() }) -join ' / ')"
    }
    finally { if ($created) { $lock.ReleaseMutex() }; $lock.Dispose() }
}

Remove-Item (Join-Path $repo 'installer\Output\TaskIsland-Setup-TEST.exe') -ErrorAction SilentlyContinue
"logs: $work"
if ($global:fails -eq 0) { 'ALL PASSED' } else { "$($global:fails) FAILED" }
