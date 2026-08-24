Set WshShell = CreateObject("WScript.Shell")
Set FSO = CreateObject("Scripting.FileSystemObject")

scriptDir = FSO.GetParentFolderName(WScript.ScriptFullName)
rootDir = FSO.GetParentFolderName(scriptDir)
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

On Error Resume Next
' 清理可能残留的旧进程，防止端口冲突导致托盘图标丢失
WshShell.Run "taskkill /F /IM ModelFailoverGateway.exe", 0, True
WScript.Sleep 300

WshShell.CurrentDirectory = backendDir
WshShell.Run """" & exePath & """", 0, False

WScript.Sleep 800
WshShell.Run "http://127.0.0.1:5000"
