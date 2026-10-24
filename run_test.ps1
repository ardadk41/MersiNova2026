try {
    $proc = Start-Process -FilePath ".\publish\MersiNova.exe" -PassThru -Wait -RedirectStandardError "err.log" -RedirectStandardOutput "out.log"
    Write-Host "ExitCode: $($proc.ExitCode)"
    if (Test-Path "err.log") { Get-Content "err.log" }
    if (Test-Path "out.log") { Get-Content "out.log" }
} catch {
    Write-Host $_
}
