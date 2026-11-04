# Does the island expose "TaskIsland reminder: ..." in UI Automation while the pill alert is up? (short, ~50 s)
$sp = $PSScriptRoot
. "$sp\uia.ps1"
$root = "$env:TEMP\TaskIslandP5f"
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
$p = Start-TI $root @('--pop-seconds','3600'); Stop-TI; Start-Sleep -Milliseconds 800
python -I "$sp\seed_p5.py" "$root\Data\taskisland.db" 5 true '1:Alert probe:305' 2>&1 | Out-Null
$p = Start-TI $root @('--full-motion','--pop-seconds','3600')
$t0 = Get-Date
$names = @{}
while (((Get-Date) - $t0).TotalSeconds -lt 50) {
    $n = @(Find-TILike 'TaskIsland*' | ForEach-Object { $_.Current.Name })
    foreach ($x in $n) { if (-not $names.ContainsKey($x)) { $names[$x] = [int]((Get-Date) - $t0).TotalSeconds } }
}
$names.GetEnumerator() | Sort-Object Value | ForEach-Object { "{0,3}s  {1}" -f $_.Value, $_.Key }
Stop-TI
