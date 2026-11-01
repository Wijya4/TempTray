' watchdog.vbs - runs watchdog.ps1 without opening a console window.
Dim shell, scriptPath
Set shell = CreateObject("WScript.Shell")
scriptPath = shell.ExpandEnvironmentStrings("%LOCALAPPDATA%\Programs\TempTray\watchdog.ps1")
shell.Run "powershell.exe -NoProfile -ExecutionPolicy Bypass -File """ & scriptPath & """", 0, False
