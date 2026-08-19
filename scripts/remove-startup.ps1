Add-Type -AssemblyName System.Windows.Forms

$startup = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Startup)
$shortcutPath = Join-Path $startup "Model Failover Gateway.lnk"

if (Test-Path $shortcutPath) {
    Remove-Item -Path $shortcutPath -Force
    $msg = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String("5bey5oiQ5Yqf5Y+W5raIIFdpbmRvd3Mg5byA5py66Ieq5Yqo5ZCr5Yqo77yB"))
    $title = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String("5Y+W5raI5byA5py66Ieq5ZCr5oiQ5Yqf"))
    [System.Windows.Forms.MessageBox]::Show($msg, $title, [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
} else {
    $msg = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String("5b2T5YmN5pyq6K6+572u5byA5py66Ieq5ZCr5Yqo77yM5peg6ZyA5pON5L2c44CC"))
    $title = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String("5o+Q56S6"))
    [System.Windows.Forms.MessageBox]::Show($msg, $title, [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
}
