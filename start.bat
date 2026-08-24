@echo off
cd /d "%~dp0backend"
taskkill /F /IM ModelFailoverGateway.exe >nul 2>&1
start "" "%~dp0backend\bin\Debug\net10.0-windows\ModelFailoverGateway.exe"
ping -n 2 127.0.0.1 >nul
start "" "http://127.0.0.1:5000"
