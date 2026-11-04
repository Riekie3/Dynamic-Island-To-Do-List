param([string]$Config='Release')
$sp=$PSScriptRoot; . "$sp\uia.ps1"
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
Add-Type -Namespace P5 -Name W -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(System.IntPtr h, uint m, System.IntPtr w, System.IntPtr l);
public delegate bool EnumProc(System.IntPtr h, System.IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, System.IntPtr l);
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(System.IntPtr h, out int pid);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(System.IntPtr h, System.Text.StringBuilder s, int n);
'@
function Tray([int]$m){ $list=New-Object System.Collections.Generic.List[IntPtr]
 $cb=[P5.W+EnumProc]{param($h,$l) $q=0;[void][P5.W]::GetWindowThreadProcessId($h,[ref]$q); if($q -eq $script:TIProc.Id){$sb=New-Object System.Text.StringBuilder 256;[void][P5.W]::GetClassName($h,$sb,256); if($sb.ToString() -like 'WindowsForms*'){$list.Add($h)}}; $true}
 [void][P5.W]::EnumWindows($cb,[IntPtr]::Zero); foreach($h in $list){[void][P5.W]::PostMessage($h,0x800,[IntPtr]0,[IntPtr]$m)}; Start-Sleep -Milliseconds 700 }
foreach($op in '1','0.7'){
 $root="$env:TEMP\TaskIslandOp"; if(Test-Path $root){Remove-Item $root -Recurse -Force}
 $p=Start-TI $root @('--pop-seconds','3600') $Config; Start-Sleep 3; Stop-TI; Start-Sleep -Milliseconds 800
 python -I "$sp\setop.py" "$root\Data\taskisland.db" $op
 $p=Start-TI $root @('--full-motion','--pop-seconds','3600') $Config
 Tray 0x203; Start-Sleep -Milliseconds 1500
 $sw=[Windows.Forms.SystemInformation]::VirtualScreen.Width
 $bmp=New-Object Drawing.Bitmap 1,1; $g=[Drawing.Graphics]::FromImage($bmp)
 $cx=[int]([Windows.Forms.Screen]::PrimaryScreen.Bounds.Width/2)
 $g.CopyFromScreen($cx-150,300,0,0,(New-Object Drawing.Size 1,1)); $a=$bmp.GetPixel(0,0)
 $full=New-Object Drawing.Bitmap 1920,520; $gg=[Drawing.Graphics]::FromImage($full); $gg.CopyFromScreen(0,0,0,0,$full.Size); $full.Save("$env:TEMP\op_$op.png")
 "opacity $op -> pixel at panel: $($a.R),$($a.G),$($a.B)"
 Stop-TI; Start-Sleep -Milliseconds 800
}
