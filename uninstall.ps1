<#
    uninstall.ps1 - removes TempTray and its autostart task.
    The PawnIO driver is left in place, because other tools may use it.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'

$ScriptPath = $MyInvocation.MyCommand.Path

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Admin)) {
    $ps = (Get-Process -Id $PID).Path
    Start-Process -FilePath $ps -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $ScriptPath + '"')) -Verb RunAs
    return
}

Get-Process TempTray -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

foreach ($task in @('TempTrayAutostart', 'HermesTempTray')) {
    if (Get-ScheduledTask -TaskName $task -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $task -Confirm:$false
        Write-Host "removed autostart task '$task'"
    }
}

$exe = Join-Path $env:LOCALAPPDATA 'Programs\TempTray\TempTray.exe'
$gpuKey = 'HKCU:\Software\Microsoft\DirectX\UserGpuPreferences'
Remove-ItemProperty -Path $gpuKey -Name $exe -ErrorAction SilentlyContinue

Remove-Item (Join-Path $env:LOCALAPPDATA 'Programs\TempTray') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:LOCALAPPDATA 'TempTray') -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "files removed"
Write-Host ""
Write-Host "TempTray has been removed. The PawnIO driver was left installed."
