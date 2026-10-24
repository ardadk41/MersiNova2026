$sh = New-Object -ComObject WScript.Shell
$desktop = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Desktop)
$files = Get-ChildItem -Path $desktop -Filter "*MersiNova*.lnk"
foreach ($f in $files) {
    $sc = $sh.CreateShortcut($f.FullName)
    Write-Output "LNK: $($f.Name)"
    Write-Output "Target: $($sc.TargetPath)"
    Write-Output "TargetExists: $(Test-Path $sc.TargetPath)"
    Write-Output "--------------------"
}
