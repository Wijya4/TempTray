# watchdog.ps1 - starts TempTray when the process is missing.
$exe = Join-Path $env:LOCALAPPDATA 'Programs\TempTray\TempTray.exe'
$log = Join-Path $env:LOCALAPPDATA 'Programs\TempTray\watchdog.log'
$proc = Get-Process TempTray -ErrorAction SilentlyContinue
if (-not $proc) {
    schtasks /run /tn TempTrayAutostart | Out-Null
    $stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    "$stamp TempTray not running, TempTrayAutostart task started" | Out-File -FilePath $log -Append -Encoding utf8
}
