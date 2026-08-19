Add-Type -AssemblyName System.Windows.Forms

$scriptDir = $PSScriptRoot
$targetVbs = Join-Path $scriptDir "run-ui.vbs"
if (-not (Test-Path $targetVbs)) {
    $targetVbs = Join-Path $scriptDir "run-tray.vbs"
}
$iconExe = Join-Path $scriptDir "backend\bin\Debug\net10.0-windows\ModelFailoverGateway.exe"
if (-not (Test-Path $iconExe)) {
    $iconExe = Join-Path $scriptDir "ModelFailoverGateway.exe"
}

$desktop = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Desktop)
$shortcutPath = Join-Path $desktop "Model Failover Gateway.lnk"

$ws = New-Object -ComObject WScript.Shell
$s = $ws.CreateShortcut($shortcutPath)
$s.TargetPath = "wscript.exe"
$s.Arguments = "`"$targetVbs`""
$s.WorkingDirectory = $scriptDir
if (Test-Path $iconExe) {
    $s.IconLocation = "$iconExe,0"
}
$s.Description = "Model Failover Gateway Local LLM Proxy"
$s.Save()

$msg = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String("5bey5oiQ5Yqf5Zyo5qGM6Z2i5Yib5bu65b+r5o235pa55byPOiBNb2RlbCBGYWlsb3ZlciBHYXRld2F577yBCgrlj4zlh7vmoYzpnaLlm77moIfljbPlj6/lkI7lj7DpnZnpu5jlkK/liqjmnI3liqHvvIzlubboh6rliqjmiZPlvIDnrqHnkIbmjqfliLblj7DjgII="))
$title = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String("5Yib5bu65b+r5o235pa55byP5oiQ5Yqf"))
[System.Windows.Forms.MessageBox]::Show($msg, $title, [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
