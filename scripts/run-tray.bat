@echo off
cd /d "%~dp0backend"
start "" "%~dp0backend\bin\Debug\net10.0-windows\ModelFailoverGateway.exe" --urls "http://127.0.0.1:5000"
