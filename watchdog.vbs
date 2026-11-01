' watchdog.vbs - starts TempTray when the process is missing.
' Pure VBScript on purpose: no shell helper and no schtasks.exe, so no console window is created.
Option Explicit
On Error Resume Next

Dim shell, fso, logPath, items, stamp, line, f
Dim svc, root, task, started

Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
logPath = shell.ExpandEnvironmentStrings("%LOCALAPPDATA%") & "\Programs\TempTray\watchdog.log"

Set items = GetObject("winmgmts:").ExecQuery("select * from Win32_Process where Name='TempTray.exe'")

If items.Count = 0 Then
    started = False
    ' Preferred path: Task Scheduler COM API, no helper process at all.
    Set svc = CreateObject("Schedule.Service")
    If Err.Number = 0 Then
        svc.Connect
        If Err.Number = 0 Then
            Set root = svc.GetFolder("\")
            If Err.Number = 0 Then
                Set task = root.GetTask("TempTrayAutostart")
                If Err.Number = 0 Then
                    task.Run ""
                    If Err.Number = 0 Then started = True
                End If
            End If
        End If
    End If
    If Not started Then
        ' Fallback only: hidden helper, window style 0 keeps it invisible.
        shell.Run "schtasks /run /tn TempTrayAutostart", 0, False
    End If

    stamp = Year(Now) & "-" & Right("0" & Month(Now), 2) & "-" & Right("0" & Day(Now), 2) & " " & _
            Right("0" & Hour(Now), 2) & ":" & Right("0" & Minute(Now), 2) & ":" & Right("0" & Second(Now), 2)
    line = stamp & " TempTray not running, TempTrayAutostart task started"
    Set f = fso.OpenTextFile(logPath, 8, True)
    If Err.Number = 0 Then
        f.WriteLine line
        f.Close
    End If
End If

On Error Goto 0
