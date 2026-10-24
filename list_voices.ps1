Add-Type -AssemblyName 'System.Speech'
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$voices = $synth.GetInstalledVoices()
foreach ($v in $voices) {
    Write-Output "Name: $($v.VoiceInfo.Name) | Culture: $($v.VoiceInfo.Culture.Name) | Gender: $($v.VoiceInfo.Gender) | Description: $($v.VoiceInfo.Description)"
}
