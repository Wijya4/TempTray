# TempTray

A tiny icon in the Windows notification area that shows **CPU and GPU temperature** side by side, without opening any vendor application.

```
 58     <- CPU temperature
 45     <- GPU temperature
```

The numbers change colour on their own: green below 65 °C, amber from 65 to 80 °C, red above that.

![icon example](assets/icon.png)

## What it does

- Shows CPU package temperature and GPU temperature in the notification area, two numbers stacked in a single icon.
- Colour follows the temperature level automatically.
- Right-click menu: refresh now, set the refresh interval (1 to 10 seconds), show CPU only or GPU only, enable or disable autostart, exit.
- Autostart at sign-in uses Task Scheduler with highest privileges, so there is no UAC prompt every time you turn on the machine.

## Installation

Requirements: 64-bit Windows 10 or 11, the .NET Framework 4.8 (already present on modern Windows), and the **PawnIO driver**.

1. **Install the PawnIO driver** (required for CPU temperature).
   Download `PawnIO_setup.exe` from the [PawnIO releases](https://github.com/namazso/PawnIO.Setup/releases/latest/download/PawnIO_setup.exe), run the installer, click Install.
   The driver is digitally signed by namazso.eu and is compatible with Memory Integrity. It is the same driver used by LibreHardwareMonitor and FanControl.
   Without this driver the GPU temperature still appears, but the CPU value shows `--`.

2. **Run the installer.**
   Open PowerShell in this repository folder, then:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\install.ps1
   ```

   The script downloads the required libraries from NuGet, compiles `TempTray.exe`, copies it to `%LOCALAPPDATA%\Programs\TempTray`, registers the autostart task and starts it.
   You will see one UAC prompt, used to register the elevated task. After that it will not appear again.

3. **Show the icon.**
   If the icon is not visible, open the overflow arrow in the notification area and drag the TempTray icon out so it stays there.

To remove everything:

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

## Watchdog so the icon does not disappear

There are two guards. Both are installed once from PowerShell with administrator rights.

**1. Auto-restart on the autostart task.** If the process stops, Task Scheduler starts it
again up to three times, one minute apart.

```powershell
$s = (Get-ScheduledTask -TaskName 'TempTrayAutostart').Settings
$s.RestartCount = 3
$s.RestartInterval = 'PT1M'
Set-ScheduledTask -TaskName 'TempTrayAutostart' -Settings $s
```

**2. Watchdog every five minutes.** `watchdog.ps1` checks the process; if it is missing,
it runs the autostart task and writes one line to `watchdog.log`. It is started through
`watchdog.vbs`, because `wscript.exe` has no console, so no window flashes on screen.

```powershell
$dir = Join-Path $env:LOCALAPPDATA 'Programs\TempTray'
Copy-Item .\watchdog.ps1 $dir -Force
Copy-Item .\watchdog.vbs $dir -Force
$vbs = Join-Path $dir 'watchdog.vbs'
schtasks /create /tn TempTrayWatchdog /tr "wscript.exe //nologo `"$vbs`"" /sc minute /mo 5 /rl highest /f
```

Important: tasks created with `schtasks` do not run on battery by default. Turn that
condition off, otherwise the watchdog stays silent.

```powershell
$s = (Get-ScheduledTask -TaskName 'TempTrayWatchdog').Settings
$s.DisallowStartIfOnBatteries = $false
$s.StopIfGoingOnBatteries = $false
$s.ExecutionTimeLimit = 'PT0S'
Set-ScheduledTask -TaskName 'TempTrayWatchdog' -Settings $s
```

To remove the guards:

```powershell
schtasks /delete /tn TempTrayWatchdog /f
```

## Advantages

- **Light.** `TempTray.exe` is about 15 KB, the whole folder with its libraries is about 3 MB, memory use is roughly 45 to 57 MB, and CPU load is about 0.7 percent of a single core.
- **The discrete GPU stays idle.** GPU temperature is read through a single NVML call every 30 seconds instead of polling every sensor. On the test machine this kept dGPU temperature and power draw the same as when the app was not running.
- **No vendor application needed.** No MSI Center, Dragon Center, or HWiNFO left running in the background.
- **No UAC prompt at sign-in.** Autostart is handled by a scheduled task with highest privileges.
- **No bundled binaries.** All libraries are downloaded from NuGet during the build, so the repository only contains source code and scripts.

## Limitations

- Windows only. There is no Linux or macOS version.
- **GPU support.** GPU temperature requires an NVIDIA card, read through NVML. On a machine with only AMD or Intel graphics the GPU row shows `--` while the CPU row keeps working.
- Requires administrator rights at runtime, because reading CPU temperature needs a kernel driver.
- CPU temperature depends on LibreHardwareMonitor support for a given processor model. If it shows `--`, your model is not supported yet.
- The notification area icon size is decided by Windows (about 24 pixels at 150 percent scaling). That is why the numbers cannot be made any larger without giving up padding.
- Reading sensors is not free: every poll wakes CPU cores briefly. The default 10 second interval was chosen to keep that impact small. A shorter interval gives fresher numbers but a slightly warmer machine.
- Tested on a single machine: MSI laptop, Intel Core Ultra 5 125H, RTX 3050 Laptop, Windows 11.

## Disclaimer

This application was **written by AI**, namely Hermes Agent, at the personal request of its owner. It is not a company product and not the work of a professional programmer.

It started as a private tool for one laptop. If you want to try it, please go ahead. But understand this:

- **The author cannot program.** Do not expect updates, bug fixes, or new features.
- **There is no support.** Issues and pull requests may never be answered.
- **Use it at your own risk.** The script asks for administrator rights and registers an autostart task. Read the code first if you are unsure.
- The code is provided as is, without warranty of any kind, under the MIT licence below.

## Licence

The code in this repository is released under the MIT licence, see [LICENSE](LICENSE).

The third-party libraries used during the build carry their own licences, including **LibreHardwareMonitorLib (MPL-2.0)**. This repository does not ship those binaries; they are downloaded from NuGet during the build. Details are in [THIRD-PARTY.md](THIRD-PARTY.md).
