Add-Type -AssemblyName System.Windows.Forms

$scriptDir = $PSScriptRoot
$targetVbs = Join-Path $scriptDir "run-tray.vbs"
$iconExe = Join-Path $scriptDir "backend\bin\Debug\net10.0-windows\ModelFailoverGateway.exe"
if (-not (Test-Path $iconExe)) {
    $iconExe = Join-Path $scriptDir "ModelFailoverGateway.exe"
}

$startup = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Startup)
$shortcutPath = Join-Path $startup "Model Failover Gateway.lnk"

$ws = New-Object -ComObject WScript.Shell
$s = $ws.CreateShortcut($shortcutPath)
$s.TargetPath = "wscript.exe"
$s.Arguments = "`"$targetVbs`""
$s.WorkingDirectory = $scriptDir
if (Test-Path $iconExe) {
    $s.IconLocation = "$iconExe,0"
}
$s.Description = "Model Failover Gateway Startup"
$s.Save()

$msg = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String("5bey5oiQ5Yqf5byA5ZCQIFdpbmRvd3Mg5byA5py66Ieq5ZCr5Yqo77yBCgrlu4/mrKHlvIDmnLrnmbvlvZXns7vnu5/lkI7vvIznvZHlhbPlsIblnKjns7vnu5/miZjnm5jlkI7lj7DpnZnpu5jov5DooYzjgII="))
$title = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String("6K6+572u5byA5py66Ieq5ZCr5oiQ5Yqf"))
[System.Windows.Forms.MessageBox]::Show($msg, $title, [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
