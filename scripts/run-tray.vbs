Set WshShell = CreateObject("WScript.Shell")
Set FSO = CreateObject("Scripting.FileSystemObject")

scriptDir = FSO.GetParentFolderName(WScript.ScriptFullName)
rootDir = FSO.GetParentFolderName(scriptDir)

If FSO.FileExists(scriptDir & "\ModelFailoverGateway.exe") Then
    exePath = scriptDir & "\ModelFailoverGateway.exe"
    backendDir = scriptDir
Else
    If Not FSO.FolderExists(rootDir & "\backend") And Not FSO.FileExists(rootDir & "\ModelFailoverGateway.exe") Then
        rootDir = scriptDir
    End If

    exePath = rootDir & "\ModelFailoverGateway.exe"
    If Not FSO.FileExists(exePath) Then
        exePath = rootDir & "\backend\bin\Debug\net10.0-windows\ModelFailoverGateway.exe"
    End If

    backendDir = rootDir & "\backend"
    If Not FSO.FolderExists(backendDir) Then
        backendDir = rootDir
    End If
End If

On Error Resume Next
WshShell.Run "taskkill /F /IM ModelFailoverGateway.exe", 0, True
WScript.Sleep 300

WshShell.CurrentDirectory = backendDir
WshShell.Run Chr(34) & exePath & Chr(34), 1, False
