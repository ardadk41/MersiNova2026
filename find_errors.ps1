$lines = Get-Content -Encoding UTF8 "MainWindow.xaml.cs"
for ($i=0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match 'catch' -or $lines[$i] -match 'Exception' -or $lines[$i] -match 'Error' -or $lines[$i] -match 'Toast') {
        Write-Host "$($i+1): $($lines[$i].Trim())"
    }
}
