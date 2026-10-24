$path = 'HKLM:\SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens'
if (Test-Path $path) {
    Get-ChildItem -Path $path | ForEach-Object {
        Write-Output $_.Name
    }
} else {
    Write-Output "OneCore tokens path not found"
}
