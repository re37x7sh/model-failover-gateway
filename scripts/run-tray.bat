@echo off
cd /d "%~dp0..\backend"
start "" "%~dp0..\backend\bin\Debug\net10.0-windows\ModelFailoverGateway.exe" --urls "http://127.0.0.1:5000"
