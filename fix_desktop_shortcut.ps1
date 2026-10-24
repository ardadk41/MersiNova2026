$desktop = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Desktop)
$userProfile = [System.Environment]::GetEnvironmentVariable("USERPROFILE")

$publishDir = Join-Path $userProfile ".gemini\antigravity-ide\scratch\MersiNova\publish"
$targetExe = Join-Path $publishDir "MersiNova.exe"

# Clean any existing MersiNova shortcuts on Desktop
Get-ChildItem -Path $desktop -Filter "*MersiNova*" -ErrorAction SilentlyContinue | Remove-Item -Force

# Unicode character for Turkish 'ü'
$u = [char]0x00FC
$nameWithTurkishU = "MersiNova - G" + $u + "ncel"
$nameAscii = "MersiNova Guncel"

$wsh = New-Object -ComObject WScript.Shell

# 1. Shortcut: MersiNova - Güncel.lnk
$lnkPath1 = Join-Path $desktop ($nameWithTurkishU + ".lnk")
$shortcut1 = $wsh.CreateShortcut($lnkPath1)
$shortcut1.TargetPath = $targetExe
$shortcut1.WorkingDirectory = $publishDir
$shortcut1.Description = "MersiNova Akilli Tarim & Rekolte Analizi"
$shortcut1.Save()

# 2. Shortcut: MersiNova Guncel.lnk (ASCII safe fallback)
$lnkPath2 = Join-Path $desktop ($nameAscii + ".lnk")
$shortcut2 = $wsh.CreateShortcut($lnkPath2)
$shortcut2.TargetPath = $targetExe
$shortcut2.WorkingDirectory = $publishDir
$shortcut2.Description = "MersiNova Akilli Tarim & Rekolte Analizi"
$shortcut2.Save()

# 3. Direct Batch Launcher: MersiNova_Baslat.bat
$batPath = Join-Path $desktop "MersiNova_Baslat.bat"
$batContent = "@echo off`r`ncd /d `"%USERPROFILE%\.gemini\antigravity-ide\scratch\MersiNova\publish`"`r`nstart `"`" `"MersiNova.exe`"`r`n"
[System.IO.File]::WriteAllText($batPath, $batContent, [System.Text.Encoding]::ASCII)

Write-Output "Shortcuts generated successfully:"
Write-Output "1. $lnkPath1 -> TargetExists: $([System.IO.File]::Exists($shortcut1.TargetPath))"
Write-Output "2. $lnkPath2 -> TargetExists: $([System.IO.File]::Exists($shortcut2.TargetPath))"
Write-Output "3. $batPath"
