param([string]$Dir)
Get-ChildItem $Dir -Filter 'TaskIsland*' | Where-Object { $_.Extension -in '.exe', '.dll' } | ForEach-Object {
    '{0,-24} {1,-11} {2,-11} {3,-18} {4}' -f $_.Name, $_.VersionInfo.CompanyName, $_.VersionInfo.ProductName, $_.VersionInfo.ProductVersion, $_.VersionInfo.FileDescription
}
