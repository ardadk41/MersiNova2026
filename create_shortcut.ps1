[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$desktop = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Desktop)
$targetExe = "C:\Users\HALİDE\.gemini\antigravity-ide\scratch\MersiNova\publish\MersiNova.exe"
$workingDir = "C:\Users\HALİDE\.gemini\antigravity-ide\scratch\MersiNova\publish"

$wshShell = New-Object -ComObject WScript.Shell

# 1. MersiNova - Güncel
$shortcutPath1 = [System.IO.Path]::Combine($desktop, "MersiNova - Güncel.lnk")
$shortcut1 = $wshShell.CreateShortcut($shortcutPath1)
$shortcut1.TargetPath = $targetExe
$shortcut1.WorkingDirectory = $workingDir
$shortcut1.Description = "MersiNova - Akıllı Tarım & Rekolte Analiz Sistemi"
$shortcut1.Save()

# 2. MersiNova - Guncel
$shortcutPath2 = [System.IO.Path]::Combine($desktop, "MersiNova - Guncel.lnk")
$shortcut2 = $wshShell.CreateShortcut($shortcutPath2)
$shortcut2.TargetPath = $targetExe
$shortcut2.WorkingDirectory = $workingDir
$shortcut2.Description = "MersiNova - Akilli Tarim & Rekolte Analiz Sistemi"
$shortcut2.Save()

Write-Output "Shortcuts successfully created on Desktop."
