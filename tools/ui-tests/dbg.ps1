. "$PSScriptRoot\uia.ps1"
$root="$env:TEMP\TaskIslandP5b"
$p = Start-TI $root @('--settings','behaviour')
foreach ($n in 'Remind me before the due time','Collapse by itself','Show on display') {
 $c = Find-TI $n 'ComboBox'
 $v = $c.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
 "$n => '$v'"
}
Stop-TI
