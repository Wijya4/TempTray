<#
    install.ps1 - installs TempTray for the current user.
    Runs build.ps1, copies the result, then registers an elevated logon task
    so Windows does not show a UAC prompt at every sign-in.
#>
[CmdletBinding()]
param(
    [switch]$NoAutostart
)

$ErrorActionPreference = 'Stop'

$Root = $PSScriptRoot
if (-not $Root) { $Root = Split-Path -Parent $MyInvocation.MyCommand.Path }
$ScriptPath = $MyInvocation.MyCommand.Path

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Admin)) {
    Write-Host "requesting administrator rights..."
    $ps = (Get-Process -Id $PID).Path
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $ScriptPath + '"'))
    if ($NoAutostart) { $argList += '-NoAutostart' }
    Start-Process -FilePath $ps -ArgumentList $argList -Verb RunAs
    return
}

Write-Host "== checking for the PawnIO driver =="
$pawn = Get-Service -Name PawnIO -ErrorAction SilentlyContinue
if (-not $pawn) {
    Write-Warning "PawnIO is not installed. CPU temperature cannot be read without this driver."
    Write-Warning "Download https://github.com/namazso/PawnIO.Setup/releases/latest/download/PawnIO_setup.exe, run the installer, then run this script again."
}

Write-Host ""
Write-Host "== build =="
& (Join-Path $Root 'build.ps1')

$src = Join-Path $Root 'build'
$dst = Join-Path $env:LOCALAPPDATA 'Programs\TempTray'
New-Item -ItemType Directory -Force -Path $dst | Out-Null

Get-Process TempTray -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 4

$copied = $false
for ($i = 1; $i -le 5; $i++) {
    try {
        Get-ChildItem $src | Copy-Item -Destination $dst -Force -ErrorAction Stop
        $copied = $true
        break
    } catch {
        Write-Warning ("copy attempt {0} failed: {1}" -f $i, $_.Exception.Message)
        Start-Sleep -Seconds 2
    }
}
if (-not $copied) { throw "could not copy files to $dst. Right-click the tray icon, choose Exit, then run this script again." }
Write-Host "installed to $dst"

$exe = Join-Path $dst 'TempTray.exe'

# remove the task name used by early builds of this project
if (Get-ScheduledTask -TaskName 'HermesTempTray' -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName 'HermesTempTray' -Confirm:$false
    Write-Host "removed legacy autostart task 'HermesTempTray'"
}

if (-not $NoAutostart) {
    $taskName = 'TempTrayAutostart'
    $action = New-ScheduledTaskAction -Execute $exe -WorkingDirectory $dst
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User ("$env:USERDOMAIN\$env:USERNAME")
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
        -ExecutionTimeLimit ([TimeSpan]::Zero) -StartWhenAvailable -MultipleInstances IgnoreNew
    $principal = New-ScheduledTaskPrincipal -UserId ("$env:USERDOMAIN\$env:USERNAME") -RunLevel Highest -LogonType Interactive
    Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null
    Write-Host "autostart registered: task '$taskName' (highest privileges, no UAC prompt)"
}

$gpuKey = 'HKCU:\Software\Microsoft\DirectX\UserGpuPreferences'
if (-not (Test-Path $gpuKey)) { New-Item -Path $gpuKey -Force | Out-Null }
Set-ItemProperty -Path $gpuKey -Name $exe -Value 'GpuPreference=1;' -ErrorAction SilentlyContinue
Write-Host "GPU preference: integrated (battery friendly)"

Start-Process -FilePath $exe -WorkingDirectory $dst
Start-Sleep -Seconds 4

if (Get-Process TempTray -ErrorAction SilentlyContinue) {
    Write-Host ""
    Write-Host "TempTray is running. If the icon is hidden, open the overflow arrow in the tray and drag the icon out."
} else {
    Write-Warning "TempTray is not running. Check the log at $env:LOCALAPPDATA\TempTray\log.txt"
}
