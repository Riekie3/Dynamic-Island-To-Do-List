# UI Automation helpers for driving TaskIsland without touching the mouse.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms
$script:AE = [System.Windows.Automation.AutomationElement]
$script:Tree = [System.Windows.Automation.TreeScope]
$script:PC = [System.Windows.Automation.PropertyCondition]

function Start-TI([string]$Root, [string[]]$ArgList, [string]$Config = 'Debug') {
    $exe = "D:\Rie\Claude\TaskIsland\src\TaskIsland.App\bin\$Config\net10.0-windows\TaskIsland.exe"
    $env:TASKISLAND_ROOT = $Root
    $p = if ($ArgList -and $ArgList.Count -gt 0) { Start-Process $exe -ArgumentList $ArgList -PassThru } else { Start-Process $exe -PassThru }
    Start-Sleep -Seconds 4
    $script:TIProc = $p
    return $p
}

function Get-TIWindow {
    $cond = New-Object System.Windows.Automation.PropertyCondition($script:AE::ProcessIdProperty, $script:TIProc.Id)
    $script:AE::RootElement.FindAll($script:Tree::Children, $cond) | Where-Object { $_.Current.ClassName -notlike 'Popup*' } | Select-Object -First 1
}

# All elements (any depth) of the TaskIsland process, including popups (they are separate top-level windows).
function Get-TIAll {
    $cond = New-Object System.Windows.Automation.PropertyCondition($script:AE::ProcessIdProperty, $script:TIProc.Id)
    $tops = $script:AE::RootElement.FindAll($script:Tree::Children, $cond)
    foreach ($t in $tops) { $t; $t.FindAll($script:Tree::Descendants, [System.Windows.Automation.Condition]::TrueCondition) }
}

function Find-TI([string]$Name, [string]$Type = '') {
    foreach ($e in (Get-TIAll)) {
        if ($e.Current.Name -eq $Name -and ($Type -eq '' -or $e.Current.ControlType.ProgrammaticName -eq "ControlType.$Type")) { return $e }
    }
    return $null
}

function Find-TILike([string]$Pattern) {
    @(Get-TIAll | Where-Object { $_.Current.Name -like $Pattern })
}

function Invoke-TI([string]$Name, [string]$Type = 'Button') {
    $e = Find-TI $Name $Type
    if (-not $e) { throw "not found: $Type '$Name'" }
    $pat = $null
    if ($e.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pat)) { $pat.Invoke() }
    else {
        # RadioButton: UI Automation "Select" does not fire its Command, so focus it and press Space (same as a user).
        if ((Get-FgTitle) -ne $script:TIProc.Id) { throw "TaskIsland is not the foreground window; refusing to send keys" }
        $e.SetFocus(); Start-Sleep -Milliseconds 150
        [System.Windows.Forms.SendKeys]::SendWait(' ')
    }
    Start-Sleep -Milliseconds 350
}

function Set-TIText([string]$Name, [string]$Text) {
    $e = Find-TI $Name 'Edit'
    if (-not $e) { throw "not found: Edit '$Name'" }
    $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($Text)
    Start-Sleep -Milliseconds 250
}

function Get-TIText([string]$Name) {
    $e = Find-TI $Name 'Edit'
    if (-not $e) { return $null }
    $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
}

function Toggle-TI([string]$Name) {
    $e = Find-TI $Name 'CheckBox'
    if (-not $e) { throw "not found: CheckBox '$Name'" }
    $e.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Start-Sleep -Milliseconds 350
}

# Task titles currently shown (the round check boxes carry the title as their name).
function Get-TIRows {
    @(Get-TIAll | Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.CheckBox' } | ForEach-Object { $_.Current.Name })
}

function Get-TIChecked([string]$Name) {
    $e = Find-TI $Name 'CheckBox'
    if (-not $e) { return $null }
    $e.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq 'On'
}

function Dump-TI {
    foreach ($e in (Get-TIAll)) {
        $c = $e.Current
        if ($c.Name -or $c.ControlType.ProgrammaticName -match 'Edit|Button|CheckBox') {
            '{0,-22} {1}' -f ($c.ControlType.ProgrammaticName -replace 'ControlType\.',''), $c.Name
        }
    }
}

function Stop-TI { if ($script:TIProc -and -not $script:TIProc.HasExited) { Stop-Process -Id $script:TIProc.Id -Force } }
function Kill-TI { Stop-TI }

function Get-FgTitle {
    Add-Type -Namespace W -Name U -MemberDefinition '[DllImport("user32.dll")] public static extern System.IntPtr GetForegroundWindow(); [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(System.IntPtr h, out int pid);' -ErrorAction SilentlyContinue
    $h = [W.U]::GetForegroundWindow(); $procId = 0; [void][W.U]::GetWindowThreadProcessId($h, [ref]$procId)
    return $procId
}


